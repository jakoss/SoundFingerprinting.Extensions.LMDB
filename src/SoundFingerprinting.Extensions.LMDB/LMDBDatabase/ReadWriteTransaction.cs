using MessagePack;
using SoundFingerprinting.Extensions.LMDB.DTO;
using Spreads.Buffers;
using Spreads.LMDB;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
// ReSharper disable ImpureMethodCallOnReadonlyValueField

namespace SoundFingerprinting.Extensions.LMDB.LMDBDatabase
{
    internal class ReadWriteTransaction : BaseTransaction, IDisposable
    {
        private static readonly object locker = new object();
        private readonly Transaction tx;
        private readonly DatabasesHolder databasesHolder;
        private readonly IndexesHolder indexesHolder;

        public ReadWriteTransaction(Transaction tx, DatabasesHolder databasesHolder, IndexesHolder indexesHolder)
            : base(databasesHolder, indexesHolder)
        {
            Monitor.Enter(locker);
            this.tx = tx;
            this.databasesHolder = databasesHolder;
            this.indexesHolder = indexesHolder;
        }

        public void Dispose()
        {
            tx.Dispose();
            Monitor.Exit(locker);
        }

        public ulong GetLastTrackId()
        {
            ulong lastTrackId = 0;
            using (var cursor = databasesHolder.TracksDatabase.OpenCursor(tx))
            {
                DirectBuffer key = default;
                DirectBuffer value = default;
                if (cursor.TryGet(ref key, ref value, CursorGetOption.Last))
                {
                    lastTrackId = key.ReadUInt64(0);
                }
            }
            return lastTrackId;
        }

        public ulong GetLastSubFingerprintId()
        {
            ulong lastSubFingerprintId = 0;
            using (var cursor = databasesHolder.SubFingerprintsDatabase.OpenCursor(tx))
            {
                var key = default(DirectBuffer);
                var value = default(DirectBuffer);
                if (cursor.TryGet(ref key, ref value, CursorGetOption.Last))
                {
                    lastSubFingerprintId = key.ReadUInt64(0);
                }
            }
            return lastSubFingerprintId;
        }

        public void PutSubFingerprint(SubFingerprintDataDTO subFingerprintDataDTO)
        {
            var subFingerprintKey = BitConverter.GetBytes(subFingerprintDataDTO.SubFingerprintReference).AsMemory();
            var subFingerprintValue = MessagePackSerializer.Serialize(subFingerprintDataDTO, options: serializerOptions).AsMemory();

            using (subFingerprintKey.Pin())
            {
                var subFingerprintKeyBuffer = new DirectBuffer(subFingerprintKey.Span);
                using (subFingerprintValue.Pin())
                {
                    var valueBuffer = new DirectBuffer(subFingerprintValue.Span);
                    databasesHolder.SubFingerprintsDatabase.Put(tx, ref subFingerprintKeyBuffer, ref valueBuffer);
                }

                var trackKey = BitConverter.GetBytes(subFingerprintDataDTO.TrackReference).AsMemory();
                using (trackKey.Pin())
                {
                    var trackKeyBuffer = new DirectBuffer(trackKey.Span);
                    indexesHolder.TracksSubfingerprintsIndex.Put(tx, ref trackKeyBuffer, ref subFingerprintKeyBuffer);
                }
            }
        }

        public void PutSubFingerprintsByHashTableAndHash(int table, int hash, ulong id)
        {
            var tableDatabase = databasesHolder.HashTables[table];
            var key = hash;
            var value = id;

            using var cursor = tableDatabase.OpenCursor(tx);
            cursor.TryPut(ref key, ref value, CursorPutOptions.None);
        }

        public void PutTrack(TrackDataDTO trackDataDTO)
        {
            if (string.IsNullOrWhiteSpace(trackDataDTO.Id))
            {
                throw new ArgumentException("Id have to be unique and not empty", nameof(trackDataDTO.Id));
            }
            // check for isrc in database
            var idKey = Encoding.UTF8.GetBytes(trackDataDTO.Id).AsMemory();
            using (idKey.Pin())
            {
                var keyBuffer = new DirectBuffer(idKey.Span);
                if (indexesHolder.IdIndex.TryGet(tx, ref keyBuffer, out _))
                {
                    throw new ArgumentException("Track with given Id already exists", nameof(trackDataDTO.Id));
                }
            }

            var trackKey = BitConverter.GetBytes(trackDataDTO.TrackReference).AsMemory();
            var trackValue = MessagePackSerializer.Serialize(trackDataDTO, options: serializerOptions).AsMemory();

            using (trackKey.Pin())
            {
                var trackKeyBuffer = new DirectBuffer(trackKey.Span);
                using (trackValue.Pin())
                {
                    var valueBuffer = new DirectBuffer(trackValue.Span);
                    databasesHolder.TracksDatabase.Put(tx, ref trackKeyBuffer, ref valueBuffer);
                }

                // create indexes
                using (idKey.Pin())
                {
                    var keyBuffer = new DirectBuffer(idKey.Span);
                    indexesHolder.IdIndex.Put(tx, ref keyBuffer, ref trackKeyBuffer);
                }

                var titleKey = Encoding.UTF8.GetBytes(trackDataDTO.Title).AsMemory();
                if (!titleKey.IsEmpty)
                {
                    using (titleKey.Pin())
                    {
                        var keyBuffer = new DirectBuffer(titleKey.Span);
                        indexesHolder.TitleIndex.Put(tx, ref keyBuffer, ref trackKeyBuffer);
                    }
                }
            }
        }

        public void RemoveTrack(TrackDataDTO trackDataDTO)
        {
            var trackKey = BitConverter.GetBytes(trackDataDTO.TrackReference).AsMemory();
            using (trackKey.Pin())
            {
                var trackKeyBuffer = new DirectBuffer(trackKey.Span);
                // get track from database
                var track = GetTrackById(ref trackKeyBuffer, tx);

                // remove indexes
                var idKey = Encoding.UTF8.GetBytes(track.Id).AsMemory();
                using (idKey.Pin())
                {
                    var keyBuffer = new DirectBuffer(idKey.Span);
                    indexesHolder.IdIndex.Delete(tx, ref keyBuffer);
                }

                var titleKey = Encoding.UTF8.GetBytes(track.Title).AsMemory();
                if (!titleKey.IsEmpty)
                {
                    using (titleKey.Pin())
                    using (var cursor = indexesHolder.TitleIndex.OpenCursor(tx))
                    {
                        var keyBuffer = new DirectBuffer(titleKey.Span);
                        if (cursor.TryGet(ref keyBuffer, ref trackKeyBuffer, CursorGetOption.GetBoth))
                            cursor.Delete(false);
                    }
                }

                // remove track
                databasesHolder.TracksDatabase.Delete(tx, ref trackKeyBuffer);
            }
        }

        public void RemoveSubFingerprint(SubFingerprintDataDTO subFingerprintDataDTO)
        {
            var subFingerprintKey = BitConverter.GetBytes(subFingerprintDataDTO.SubFingerprintReference).AsMemory();
            using (subFingerprintKey.Pin())
            {
                var keyBuffer = new DirectBuffer(subFingerprintKey.Span);
                databasesHolder.SubFingerprintsDatabase.Delete(tx, ref keyBuffer);

                var trackKey = BitConverter.GetBytes(subFingerprintDataDTO.TrackReference).AsMemory();
                using (trackKey.Pin())
                {
                    var trackKeyBuffer = new DirectBuffer(trackKey.Span);
                    using var cursor = indexesHolder.TracksSubfingerprintsIndex.OpenCursor(tx);
                    if (cursor.TryGet(ref trackKeyBuffer, ref keyBuffer, CursorGetOption.GetBoth))
                        cursor.Delete(false);
                }
            }
        }

        public void RemoveSubFingerprintsByHashTableAndHash(int table, int hash, ulong id)
        {
            var tableDatabase = databasesHolder.HashTables[table];
            var hashKey = hash;
            var value = id;

            using var cursor = tableDatabase.OpenCursor(tx);
            if (cursor.TryGet(ref hashKey, ref value, CursorGetOption.GetBoth))
                cursor.Delete(false);
        }

        public void Commit()
        {
            tx.Commit();
        }

        public void Abort()
        {
            tx.Abort();
        }

        public TrackDataDTO GetTrackById(ulong id)
        {
            var trackKey = BitConverter.GetBytes(id).AsMemory();
            using (trackKey.Pin())
            {
                var keyBuffer = new DirectBuffer(trackKey.Span);
                return GetTrackById(ref keyBuffer, tx);
            }
        }

        public IEnumerable<SubFingerprintDataDTO> GetSubFingerprintsForTrack(ulong id)
        {
            return GetSubFingerprintsForTrack(id, tx);
        }
    }
}