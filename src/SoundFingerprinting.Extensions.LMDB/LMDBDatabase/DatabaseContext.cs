using SoundFingerprinting.Extensions.LMDB.Exceptions;
using Spreads.LMDB;
using System;

namespace SoundFingerprinting.Extensions.LMDB.LMDBDatabase
{
    internal sealed class DatabaseContext : IDisposable
    {
        private int HashTablesCount { get; }
        private bool disposed;

        private readonly LMDBEnvironment environment;
        private readonly DatabasesHolder databasesHolder;
        private readonly IndexesHolder indexesHolder;

        public DatabaseContext(string pathToDatabase,
            LMDBConfiguration configuration
        ) : this(pathToDatabase, configuration.MapSize, configuration.UnsafeAsync, 50)
        {
        }

        private DatabaseContext(string pathToDatabase, long mapSize, bool unsafeAsync, int hashTablesCount)
        {
            HashTablesCount = hashTablesCount;

            // Check if current process is 64 bit one (needed for lmdb to run)
            if (!Environment.Is64BitProcess)
            {
                throw new BadRuntimeException();
            }

            var lmdbOpenFlags = LMDBEnvironmentFlags.NoMemInit;
            if (unsafeAsync)
            {
                lmdbOpenFlags = lmdbOpenFlags | LMDBEnvironmentFlags.MapAsync | LMDBEnvironmentFlags.NoLock | LMDBEnvironmentFlags.WriteMap;
            }
            environment = LMDBEnvironment.Create(pathToDatabase, lmdbOpenFlags, disableAsync: true);
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
            for (var i = 0; i < hashTablesCount; i++)
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

        public void CopyAndCompactLmdbDatabase(string newPath)
        {
            ThrowIfDisposed();
            environment.CopyTo(newPath, true);
        }

        public ReadOnlyTransaction OpenReadOnlyTransaction()
        {
            ThrowIfDisposed();
            return new ReadOnlyTransaction(environment.BeginReadOnlyTransaction(), databasesHolder, indexesHolder);
        }

        public ReadWriteTransaction OpenReadWriteTransaction()
        {
            ThrowIfDisposed();
            return new ReadWriteTransaction(environment.BeginTransaction(), databasesHolder, indexesHolder);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            databasesHolder.Dispose();
            indexesHolder.Dispose();
            environment.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("Cannot use already disposed LMDBModelService");
            }
        }
    }
}