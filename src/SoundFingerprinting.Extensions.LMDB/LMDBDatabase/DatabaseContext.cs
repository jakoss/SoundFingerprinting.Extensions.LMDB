using Spreads.LMDB;
using System;

namespace SoundFingerprinting.Extensions.LMDB.LMDBDatabase
{
    internal sealed class DatabaseContext : IDisposable
    {
        public int HashTablesCount { get; }

        private readonly LMDBEnvironment environment;
        private readonly DatabasesHolder databasesHolder;
        private readonly IndexesHolder indexesHolder;

        public DatabaseContext(string pathToDatabase,
            long mapSize = (1024L * 1024L * 1024L * 10L),
            DbEnvironmentFlags lmdbOpenFlags = DbEnvironmentFlags.None
        ) : this(pathToDatabase, mapSize, lmdbOpenFlags, 50)
        {
        }

        private DatabaseContext(string pathToDatabase, long mapSize, DbEnvironmentFlags lmdbOpenFlags, int hashTablesCount)
        {
            HashTablesCount = hashTablesCount;

            environment = LMDBEnvironment.Create(pathToDatabase, lmdbOpenFlags | DbEnvironmentFlags.NoMemInit);
            environment.MapSize = mapSize;
            environment.MaxDatabases = HashTablesCount + 5;
            environment.MaxReaders = 1000;
            environment.Open();

            // Open all database to make sure they exists and to hold their handles
            var configuration = new DatabaseConfig(DbFlags.Create | DbFlags.IntegerKey);

            var tracksDatabase = environment.OpenDatabase("tracks", configuration);
            var subFingerprintsDatabase = environment.OpenDatabase("subFingerprints", configuration);

            var hashTables = new Database[hashTablesCount];
            var hashTableConfig = new DatabaseConfig(
                DbFlags.Create
                | DbFlags.DuplicatesSort
                | DbFlags.IntegerKey
                | DbFlags.IntegerDuplicates
            );
            for (int i = 0; i < hashTablesCount; i++)
            {
                hashTables[i] = environment.OpenDatabase($"HashTable{i}", hashTableConfig);
            }

            databasesHolder = new DatabasesHolder(tracksDatabase, subFingerprintsDatabase, hashTables);

            // Open all databases for indexes
            var isrcIndex = environment.OpenDatabase("isrcIndex", new DatabaseConfig(DbFlags.Create));
            var titleArtistIndex = environment.OpenDatabase("titleArtistIndex", new DatabaseConfig(DbFlags.Create | DbFlags.DuplicatesSort));
            var tracksSubfingerprintsIndex = environment.OpenDatabase("tracksSubfingerprintsIndex", new DatabaseConfig(
                DbFlags.Create
                | DbFlags.DuplicatesSort
                | DbFlags.IntegerKey
                | DbFlags.IntegerDuplicates
            ));
            indexesHolder = new IndexesHolder(isrcIndex, titleArtistIndex, tracksSubfingerprintsIndex);
        }

        public ReadOnlyTransaction OpenReadOnlyTransaction()
        {
            return new ReadOnlyTransaction(environment.BeginReadOnlyTransaction(), databasesHolder, indexesHolder);
        }

        public ReadWriteTransaction OpenReadWriteTransaction()
        {
            return new ReadWriteTransaction(environment.BeginTransaction(), databasesHolder, indexesHolder);
        }

        public void Dispose()
        {
            databasesHolder.Dispose();
            indexesHolder.Dispose();
            environment.Dispose();
        }
    }
}