using LightningDB;
using SoundFingerprinting.Extensions.LMDB.DTO;
using System;
using ZeroFormatter;

namespace SoundFingerprinting.Extensions.LMDB.LMDBDatabase
{
    internal sealed class DatabaseContext : IDisposable
    {
        public int HashTablesCount { get; }

        private readonly LightningEnvironment environment;
        private readonly DatabasesHolder databasesHolder;

        public DatabaseContext(string pathToDatabase,
            long mapSize = (1024L * 1024L * 1024L * 10L),
            EnvironmentOpenFlags lmdbOpenFlags = EnvironmentOpenFlags.None
        ) : this(pathToDatabase, mapSize, lmdbOpenFlags, 50)
        {
        }

        private DatabaseContext(string pathToDatabase, long mapSize, EnvironmentOpenFlags lmdbOpenFlags, int hashTablesCount)
        {
            this.HashTablesCount = hashTablesCount;

            environment = new LightningEnvironment(pathToDatabase, new EnvironmentConfiguration
            {
                MaxReaders = 10000,
                MaxDatabases = hashTablesCount + 2,
                MapSize = mapSize
            });
            environment.Open(lmdbOpenFlags);

            // Open all database to make sure they exists
            using (var tx = environment.BeginTransaction())
            {
                var configuration = new DatabaseConfiguration
                {
                    Flags = DatabaseOpenFlags.Create | DatabaseOpenFlags.IntegerKey
                };

                var tracksDatabase = tx.OpenDatabase("tracks", configuration);
                var subFingerprintsDatabase = tx.OpenDatabase("subFingerprints", configuration);

                var hashTables = new LightningDatabase[hashTablesCount];
                for (int i = 0; i < hashTablesCount; i++)
                {
                    hashTables[i] = tx.OpenDatabase($"HashTable{i}", configuration);
                }

                databasesHolder = new DatabasesHolder(tracksDatabase, subFingerprintsDatabase, hashTables);

                ReadAllTracks(tx);

                tx.Commit();
            }
        }

        public ReadOnlyTransaction OpenReadOnlyTransaction()
        {
            return new ReadOnlyTransaction(environment.BeginTransaction(TransactionBeginFlags.ReadOnly), databasesHolder);
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

        private void ReadAllTracks(LightningTransaction tx)
        {
            using (var cursor = tx.CreateCursor(databasesHolder.TracksDatabase))
            {
                foreach (var item in cursor)
                {
                    var key = BitConverter.ToUInt64(item.Key, 0);
                    var trackData = ZeroFormatterSerializer.Deserialize<TrackDataDTO>(item.Value);
                    databasesHolder.Tracks.Add(key, trackData);
                }
            }
        }
    }
}