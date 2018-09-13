using SoundFingerprinting.Extensions.LMDB.DTO;
using Spreads.Buffers;
using Spreads.LMDB;
using System;
using System.Threading;
using ZeroFormatter;

namespace SoundFingerprinting.Extensions.LMDB.LMDBDatabase
{
    internal class ReadWriteTransaction : BaseTransaction, IDisposable
    {
        private static readonly object locker = new object();
        private readonly Transaction tx;
        private readonly DatabasesHolder databasesHolder;

        public ReadWriteTransaction(Transaction tx, DatabasesHolder databasesHolder)
            : base(databasesHolder)
        {
            Monitor.Enter(locker);
            this.tx = tx;
            this.databasesHolder = databasesHolder;
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
            using (subFingerprintValue.Pin())
            {
                var keyBuffer = new DirectBuffer(subFingerprintKey.Span);
                var valueBuffer = new DirectBuffer(subFingerprintValue.Span);
                databasesHolder.SubFingerprintsDatabase.Put(tx, ref keyBuffer, ref valueBuffer);
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
            var trackKey = BitConverter.GetBytes(trackDataDTO.TrackReference).AsMemory();
            var trackValue = ZeroFormatterSerializer.Serialize(trackDataDTO).AsMemory();

            using (trackKey.Pin())
            using (trackValue.Pin())
            {
                var keyBuffer = new DirectBuffer(trackKey.Span);
                var valueBuffer = new DirectBuffer(trackValue.Span);
                databasesHolder.TracksDatabase.Put(tx, ref keyBuffer, ref valueBuffer);
            }

            if (!databasesHolder.Tracks.ContainsKey(trackDataDTO.TrackReference))
            {
                databasesHolder.Tracks.Add(trackDataDTO.TrackReference, trackDataDTO);
            }
        }

        public void RemoveTrack(TrackDataDTO trackDataDTO)
        {
            var trackKey = BitConverter.GetBytes(trackDataDTO.TrackReference).AsMemory();
            using (trackKey.Pin())
            {
                var keyBuffer = new DirectBuffer(trackKey.Span);
                databasesHolder.TracksDatabase.Delete(tx, ref keyBuffer);
            }
            databasesHolder.Tracks.Remove(trackDataDTO.TrackReference);
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

        public SubFingerprintDataDTO GetSubFingerprint(ulong id)
        {
            return GetSubFingerprint(id, tx);
        }

        public Span<ulong> GetSubFingerprintsByHashTableAndHash(int table, int hash)
        {
            return GetSubFingerprintsByHashTableAndHash(table, hash, tx);
        }
    }
}