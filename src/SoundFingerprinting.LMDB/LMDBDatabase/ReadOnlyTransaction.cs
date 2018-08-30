using LightningDB;
using SoundFingerprinting.LMDB.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using ZeroFormatter;

namespace SoundFingerprinting.LMDB.LMDBDatabase
{
    internal class ReadOnlyTransaction : IDisposable
    {
        protected readonly LightningTransaction tx;
        protected readonly DatabasesHolder databasesHolder;

        public ReadOnlyTransaction(LightningTransaction tx, DatabasesHolder databasesHolder)
        {
            this.tx = tx;
            this.databasesHolder = databasesHolder;
        }

        public virtual void Dispose()
        {
            tx.Dispose();
        }

        public SubFingerprintDataDTO GetSubFingerprint(ulong id)
        {
            var key = BitConverter.GetBytes(id);
            var value = tx.Get(databasesHolder.SubFingerprintsDatabase, key);
            return ZeroFormatterSerializer.Deserialize<SubFingerprintDataDTO>(value);
        }

        public List<ulong> GetSubFingerprintsByHashTableAndHash(int table, int hash)
        {
            var tableDatabase = databasesHolder.HashTables[table];
            var key = BitConverter.GetBytes(hash);
            var value = tx.Get(tableDatabase, key);
            var result = new List<ulong>();
            if (value?.Length > 0)
            {
                result.AddRange(Enumerable.Range(0, value.Length / sizeof(ulong))
                    .Select(i => BitConverter.ToUInt64(value, i * sizeof(ulong))));
            }
            return result;
        }

        public IEnumerable<TrackDataDTO> GetAllTracks()
        {
            return databasesHolder.Tracks.Values;
        }

        public IEnumerable<TrackDataDTO> GetTracksByArtistAndTitleName(string artist, string title)
        {
            return databasesHolder.Tracks.Where(pair => pair.Value.Artist == artist && pair.Value.Title == title).Select(e => e.Value);
        }

        public TrackDataDTO GetTrackById(ulong id)
        {
            if (databasesHolder.Tracks.TryGetValue(id, out var trackData))
            {
                return trackData;
            }
            else
            {
                return null;
            }
        }

        public TrackDataDTO GetTrackByISRC(string isrc)
        {
            return databasesHolder.Tracks.Select(e => e.Value).FirstOrDefault(value => value.ISRC == isrc);
        }
    }
}