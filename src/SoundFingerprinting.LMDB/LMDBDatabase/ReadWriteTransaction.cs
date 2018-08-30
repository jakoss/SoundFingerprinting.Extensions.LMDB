using LightningDB;
using SoundFingerprinting.LMDB.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ZeroFormatter;

namespace SoundFingerprinting.LMDB.LMDBDatabase
{
    internal class ReadWriteTransaction : ReadOnlyTransaction
    {
        private static readonly object locker = new object();

        public ReadWriteTransaction(LightningTransaction tx, DatabasesHolder databasesHolder) : base(tx, databasesHolder)
        {
            Monitor.Enter(locker);
        }

        public override void Dispose()
        {
            base.Dispose();
            Monitor.Exit(locker);
        }

        public ulong GetLastTrackId()
        {
            ulong newTrackId = 0;
            using (var cursor = tx.CreateCursor(databasesHolder.TracksDatabase))
            {
                if (cursor.MoveToLast())
                {
                    newTrackId = BitConverter.ToUInt64(cursor.Current.Key, 0);
                }
            }
            return newTrackId;
        }

        public ulong GetLastSubFingerprintId()
        {
            ulong newSubFingerprintId = 0;
            using (var cursor = tx.CreateCursor(databasesHolder.SubFingerprintsDatabase))
            {
                if (cursor.MoveToLast())
                {
                    newSubFingerprintId = BitConverter.ToUInt64(cursor.Current.Key, 0);
                }
            }
            return newSubFingerprintId;
        }

        public void PutSubFingerprint(SubFingerprintDataDTO subFingerprintDataDTO)
        {
            var subFingerprintKey = BitConverter.GetBytes(subFingerprintDataDTO.SubFingerprintReference);
            var bytes = ZeroFormatterSerializer.Serialize(subFingerprintDataDTO);
            tx.Put(databasesHolder.SubFingerprintsDatabase, subFingerprintKey, bytes);
        }

        public void PutSubFingerprintsByHashTableAndHash(int table, int hash, ulong id)
        {
            var hashTable = databasesHolder.HashTables[table];
            var key = BitConverter.GetBytes(hash);
            var value = tx.Get(hashTable, key);
            List<ulong> hashBin;
            if (value == null)
            {
                hashBin = new List<ulong>();
            }
            else
            {
                hashBin = Enumerable.Range(0, value.Length / sizeof(ulong))
                    .Select(i => BitConverter.ToUInt64(value, i * sizeof(ulong)))
                    .ToList();
            }
            hashBin.Add(id);
            var hashBinBytes = hashBin.SelectMany(BitConverter.GetBytes).ToArray();
            tx.Put(hashTable, key, hashBinBytes);
        }

        public void PutTrack(TrackDataDTO trackDataDTO)
        {
            var trackKey = BitConverter.GetBytes(trackDataDTO.TrackReference);
            var trackValue = ZeroFormatterSerializer.Serialize(trackDataDTO);
            tx.Put(databasesHolder.TracksDatabase, trackKey, trackValue);

            if (!databasesHolder.Tracks.ContainsKey(trackDataDTO.TrackReference))
            {
                databasesHolder.Tracks.Add(trackDataDTO.TrackReference, trackDataDTO);
            }
        }

        public void RemoveTrack(TrackDataDTO trackDataDTO)
        {
            var trackKey = BitConverter.GetBytes(trackDataDTO.TrackReference);
            tx.Delete(databasesHolder.TracksDatabase, trackKey);
            databasesHolder.Tracks.Remove(trackDataDTO.TrackReference);
        }

        public void RemoveSubFingerprint(SubFingerprintDataDTO subFingerprintDataDTO)
        {
            var subFingerprintKey = BitConverter.GetBytes(subFingerprintDataDTO.SubFingerprintReference);
            tx.Delete(databasesHolder.SubFingerprintsDatabase, subFingerprintKey);
        }

        public void RemoveSubFingerprintsByHashTableAndHash(int table, int hash, ulong id)
        {
            var tableDatabase = databasesHolder.HashTables[table];
            var hashKey = BitConverter.GetBytes(hash);
            var idsValue = tx.Get(tableDatabase, hashKey);
            if (idsValue == null) return;

            var ids = Enumerable.Range(0, idsValue.Length / sizeof(ulong))
                .Select(i => BitConverter.ToUInt64(idsValue, i * sizeof(ulong)))
                .ToList();
            if (ids.Remove(id))
            {
                tx.Put(tableDatabase, hashKey, ids.SelectMany(BitConverter.GetBytes).ToArray());
            }
            else
            {
                throw new Exception("Couldn't remove subFingerprintId from hash table");
            }
        }

        public void Commit()
        {
            tx.Commit();
        }
    }
}