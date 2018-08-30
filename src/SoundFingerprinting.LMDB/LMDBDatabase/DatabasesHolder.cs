using LightningDB;
using SoundFingerprinting.LMDB.DTO;
using System;
using System.Collections.Generic;

namespace SoundFingerprinting.LMDB.LMDBDatabase
{
    internal class DatabasesHolder : IDisposable
    {
        public DatabasesHolder(LightningDatabase tracksDatabase, LightningDatabase subFingerprintsDatabase, LightningDatabase[] hashTables)
        {
            TracksDatabase = tracksDatabase;
            SubFingerprintsDatabase = subFingerprintsDatabase;
            HashTables = hashTables;
        }

        public LightningDatabase TracksDatabase { get; }
        public LightningDatabase SubFingerprintsDatabase { get; }
        public LightningDatabase[] HashTables { get; }

        public readonly IDictionary<ulong, TrackDataDTO> Tracks = new Dictionary<ulong, TrackDataDTO>();

        public void Dispose()
        {
            foreach (var table in HashTables)
            {
                table.Dispose();
            }
            TracksDatabase.Dispose();
            SubFingerprintsDatabase.Dispose();
        }
    }
}