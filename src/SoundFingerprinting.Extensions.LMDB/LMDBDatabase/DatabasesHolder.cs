using Spreads.LMDB;
using System;

namespace SoundFingerprinting.Extensions.LMDB.LMDBDatabase
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