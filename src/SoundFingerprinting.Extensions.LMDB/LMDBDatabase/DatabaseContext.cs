using SoundFingerprinting.Extensions.LMDB.DTO;
using Spreads.LMDB;
using System;
using ZeroFormatter;

namespace SoundFingerprinting.Extensions.LMDB.LMDBDatabase
{
    internal sealed class DatabaseContext : IDisposable
    {
        public int HashTablesCount { get; }

        private readonly LMDBEnvironment environment;
        private readonly DatabasesHolder databasesHolder;

        public DatabaseContext(string pathToDatabase,
            long mapSize = (1024L * 1024L * 1024L * 10L),
            DbEnvironmentFlags lmdbOpenFlags = DbEnvironmentFlags.None
        ) : this(pathToDatabase, mapSize, lmdbOpenFlags, 50)
        {
        }

        private DatabaseContext(string pathToDatabase, long mapSize, DbEnvironmentFlags lmdbOpenFlags, int hashTablesCount)
        {
            HashTablesCount = hashTablesCount;

            environment = LMDBEnvironment.Create(pathToDatabase, lmdbOpenFlags);
            environment.MapSize = mapSize;
            environment.MaxDatabases = HashTablesCount + 2;
            environment.MaxReaders = 1000;
            environment.Open();

            // Open all database to make sure they exists and to hold their handles
            var configuration = new DatabaseConfig(DbFlags.Create | DbFlags.IntegerKey);

            var tracksDatabase = environment.OpenDatabase("tracks", configuration);
            var subFingerprintsDatabase = environment.OpenDatabase("subFingerprints", configuration);

            var hashTables = new Database[hashTablesCount];
            var hashTableConfig = new DatabaseConfig(
                DbFlags.Create | DbFlags.DuplicatesSort
            //| DbFlags.IntegerKey
            //| DbFlags.IntegerDuplicates
            );
            for (int i = 0; i < hashTablesCount; i++)
            {
                hashTables[i] = environment.OpenDatabase($"HashTable{i}", hashTableConfig);
            }

            databasesHolder = new DatabasesHolder(tracksDatabase, subFingerprintsDatabase, hashTables);
            ReadAllTracks();
        }

        public ReadOnlyTransaction OpenReadOnlyTransaction()
        {
            return new ReadOnlyTransaction(environment.BeginReadOnlyTransaction(), databasesHolder);
        }

        public ReadWriteTransaction OpenReadWriteTransaction()
        {
            return new ReadWriteTransaction(environment.BeginTransaction(), databasesHolder);
        }

        public void Dispose()
        {
            databasesHolder.Dispose();
            environment.Dispose();
        }

        private void ReadAllTracks()
        {
            environment.Read(tx =>
            {
                foreach (var item in databasesHolder.TracksDatabase.AsEnumerable(tx))
                {
                    var key = item.Key.ReadUInt64(0);
                    var trackData = ZeroFormatterSerializer.Deserialize<TrackDataDTO>(item.Value.Span.ToArray());
                    databasesHolder.Tracks.Add(key, trackData);
                }
            });
        }
    }
}