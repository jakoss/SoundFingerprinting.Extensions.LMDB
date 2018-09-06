using SoundFingerprinting.LMDB.DTO;
using Spreads.LMDB;
using System;
using System.Collections.Generic;

namespace SoundFingerprinting.LMDB.LMDBDatabase
{
    internal class DatabasesHolder : IDisposable
    {
        public DatabasesHolder(Database tracksDatabase, Database subFingerprintsDatabase, Database[] hashTables)
        {
            TracksDatabase = tracksDatabase;
            SubFingerprintsDatabase = subFingerprintsDatabase;
            HashTables = hashTables;
        }

        public Database TracksDatabase { get; }
        public Database SubFingerprintsDatabase { get; }
        public Database[] HashTables { get; }

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