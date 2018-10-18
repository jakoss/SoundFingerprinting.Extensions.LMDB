using SoundFingerprinting.Extensions.LMDB.DTO;
using Spreads.Buffers;
using Spreads.LMDB;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using ZeroFormatter;

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
            var subFingerprintValue = ZeroFormatterSerializer.Serialize(subFingerprintDataDTO).AsMemory();

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

            using (var cursor = tableDatabase.OpenCursor(tx))
            {
                cursor.TryPut(ref key, ref value, CursorPutOptions.None);
            }
        }

        public void PutTrack(TrackDataDTO trackDataDTO)
        {
            if (string.IsNullOrWhiteSpace(trackDataDTO.ISRC))
            {
                throw new ArgumentException("ISRC have to be unique and not empty", nameof(trackDataDTO.ISRC));
            }
            // check for isrc in database
            var isrcKey = Encoding.UTF8.GetBytes(trackDataDTO.ISRC).AsMemory();
            using (isrcKey.Pin())
            {
                var keyBuffer = new DirectBuffer(isrcKey.Span);
                if (indexesHolder.IsrcIndex.TryGet(tx, ref keyBuffer, out DirectBuffer testBuffer))
                {
                    throw new ArgumentException("Track with given ISRC already exists", nameof(trackDataDTO.ISRC));
                }
            }

            var trackKey = BitConverter.GetBytes(trackDataDTO.TrackReference).AsMemory();
            var trackValue = ZeroFormatterSerializer.Serialize(trackDataDTO).AsMemory();

            using (trackKey.Pin())
            {
                var trackKeyBuffer = new DirectBuffer(trackKey.Span);
                using (trackValue.Pin())
                {
                    var valueBuffer = new DirectBuffer(trackValue.Span);
                    databasesHolder.TracksDatabase.Put(tx, ref trackKeyBuffer, ref valueBuffer);
                }

                // create indexes
                using (isrcKey.Pin())
                {
                    var keyBuffer = new DirectBuffer(isrcKey.Span);
                    indexesHolder.IsrcIndex.Put(tx, ref keyBuffer, ref trackKeyBuffer);
                }

                var titleArtistKey = Encoding.UTF8.GetBytes(trackDataDTO.Title + trackDataDTO.Artist).AsMemory();
                if (!titleArtistKey.IsEmpty)
                {
                    using (titleArtistKey.Pin())
                    {
                        var keyBuffer = new DirectBuffer(titleArtistKey.Span);
                        indexesHolder.TitleArtistIndex.Put(tx, ref keyBuffer, ref trackKeyBuffer);
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
                var isrcKey = Encoding.UTF8.GetBytes(track.ISRC).AsMemory();
                using (isrcKey.Pin())
                {
                    var keyBuffer = new DirectBuffer(isrcKey.Span);
                    indexesHolder.IsrcIndex.Delete(tx, ref keyBuffer);
                }

                var titleArtistKey = Encoding.UTF8.GetBytes(track.Title + track.Artist).AsMemory();
                if (!titleArtistKey.IsEmpty)
                {
                    using (titleArtistKey.Pin())
                    using (var cursor = indexesHolder.TitleArtistIndex.OpenCursor(tx))
                    {
                        var keyBuffer = new DirectBuffer(titleArtistKey.Span);
                        if (cursor.TryGet(ref keyBuffer, ref trackKeyBuffer, CursorGetOption.GetBoth))
                            cursor.Delete(false);
                    }
                }

                if (indexesHolder.TracksSubfingerprintsIndex.TryGet(tx, ref trackKeyBuffer, out DirectBuffer value))
                    indexesHolder.TracksSubfingerprintsIndex.Delete(tx, ref trackKeyBuffer);

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
            }
        }

        public void RemoveSubFingerprintsByHashTableAndHash(int table, int hash, ulong id)
        {
            var tableDatabase = databasesHolder.HashTables[table];
            var hashKey = hash;
            var value = id;

            using (var cursor = tableDatabase.OpenCursor(tx))
            {
                if (cursor.TryGet(ref hashKey, ref value, CursorGetOption.GetBoth))
                    cursor.Delete(false);
            }
        }

        public void Commit()
        {
            tx.Commit();
        }

        public void Abort()
        {
            tx.Abort();
        }

        public SubFingerprintDataDTO GetSubFingerprint(ulong id)
        {
            return GetSubFingerprint(id, tx);
        }

        public Span<ulong> GetSubFingerprintsByHashTableAndHash(int table, int hash)
        {
            return GetSubFingerprintsByHashTableAndHash(table, hash, tx);
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

        public List<SubFingerprintDataDTO> GetSubFingerprintsForTrack(ulong id)
        {
            return GetSubFingerprintsForTrack(id, tx);
        }
    }
}