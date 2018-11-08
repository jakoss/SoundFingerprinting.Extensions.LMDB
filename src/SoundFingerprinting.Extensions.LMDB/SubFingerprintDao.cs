using SoundFingerprinting.Configuration;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;
using SoundFingerprinting.Extensions.LMDB.DTO;
using SoundFingerprinting.Extensions.LMDB.LMDBDatabase;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SoundFingerprinting.Extensions.LMDB
{
    internal class SubFingerprintDao : ISubFingerprintDao
    {
        private readonly DatabaseContext databaseContext;

        public int SubFingerprintsCount => GetSubFingerprintCounts();

        public IEnumerable<int> HashCountsPerTable => GetHashCountsPerTable();

        internal SubFingerprintDao(DatabaseContext databaseContext)
        {
            this.databaseContext = databaseContext;
        }

        public IEnumerable<SubFingerprintData> InsertHashDataForTrack(IEnumerable<HashedFingerprint> hashedFingerprints, IModelReference trackReference)
        {
            using (var tx = databaseContext.OpenReadWriteTransaction())
            {
                var trackId = (ulong)trackReference.Id;
                var trackData = tx.GetTrackById(trackId);
                if (trackData == null) throw new Exception("Track not found");

                ulong newSubFingerprintId = tx.GetLastSubFingerprintId();
                var result = new List<SubFingerprintData>();

                foreach (var hashedFingerprint in hashedFingerprints)
                {
                    newSubFingerprintId++;
                    var subFingerprintReference = new ModelReference<ulong>(newSubFingerprintId);
                    var subFingerprint = new SubFingerprintDataDTO(hashedFingerprint.HashBins,
                                            hashedFingerprint.SequenceNumber,
                                            hashedFingerprint.StartsAt,
                                            subFingerprintReference,
                                            trackReference,
                                            hashedFingerprint.Clusters);
                    tx.PutSubFingerprint(subFingerprint);

                    // Insert hashes to hashTable
                    int table = 0;
                    foreach (var hash in hashedFingerprint.HashBins)
                    {
                        tx.PutSubFingerprintsByHashTableAndHash(table, hash, newSubFingerprintId);
                        table++;
                    }

                    result.Add(subFingerprint.ToSubFingerprintData());
                }

                tx.Commit();
                return result;
            }
        }

        public void InsertSubFingerprints(IEnumerable<SubFingerprintData> subFingerprints)
        {
            using (var tx = databaseContext.OpenReadWriteTransaction())
            {
                foreach (var subFingerprint in subFingerprints)
                {
                    var subFingerprintDto = new SubFingerprintDataDTO(subFingerprint);
                    tx.PutSubFingerprint(subFingerprintDto);
                }
            }
        }

        public int DeleteSubFingerprintsByTrackReference(IModelReference trackReference)
        {
            using (var tx = databaseContext.OpenReadWriteTransaction())
            {
                try
                {
                    var count = 0;
                    var trackId = (ulong)trackReference.Id;

                    foreach (var subFingerprint in tx.GetSubFingerprintsForTrack(trackId))
                    {
                        // Remove hashes from hashTable
                        int table = 0;
                        foreach (var hash in subFingerprint.Hashes)
                        {
                            tx.RemoveSubFingerprintsByHashTableAndHash(table, hash, subFingerprint.SubFingerprintReference);
                            count++;
                            table++;
                        }

                        tx.RemoveSubFingerprint(subFingerprint);
                        count++;
                    }
                    tx.Commit();
                    return count;
                }
                catch (Exception)
                {
                    tx.Abort();
                    throw;
                }
            }
        }

        public IEnumerable<SubFingerprintData> ReadHashedFingerprintsByTrackReference(IModelReference trackReference)
        {
            using (var tx = databaseContext.OpenReadOnlyTransaction())
            {
                var result = new List<SubFingerprintData>();
                foreach (var subFingerprint in tx.GetSubFingerprintsForTrack((ulong)trackReference.Id))
                {
                    result.Add(subFingerprint.ToSubFingerprintData());
                }
                return result;
            }
        }

        public IEnumerable<SubFingerprintData> ReadSubFingerprints(IEnumerable<int[]> hashes, QueryConfiguration queryConfiguration)
        {
            var allSubs = new ConcurrentBag<SubFingerprintData>();
            using (var tx = databaseContext.OpenReadOnlyTransaction())
            {
                Parallel.ForEach(hashes, hashedFingerprint =>
                {
                    foreach (var subFingerprint in ReadSubFingerprints(hashedFingerprint,
                        queryConfiguration.ThresholdVotes,
                        queryConfiguration.Clusters,
                        tx
                    ))
                    {
                        allSubs.Add(subFingerprint);
                    }
                });

                return allSubs;
            }
        }

        private IEnumerable<SubFingerprintData> ReadSubFingerprints(int[] hashes, int thresholdVotes, IEnumerable<string> assignedClusters,
            ReadOnlyTransaction tx)
        {
            var subFingeprintIds = GetSubFingerprintMatches(hashes, thresholdVotes, tx);
            var subFingerprints = subFingeprintIds.Select(id => tx.GetSubFingerprint(id));

            var clusters = assignedClusters as List<string> ?? assignedClusters.ToList();
            if (clusters.Count > 0)
            {
                return subFingerprints.Where(subFingerprint => subFingerprint.Clusters.Intersect(clusters).Any()).Select(e => e.ToSubFingerprintData());
            }

            return subFingerprints.Select(e => e.ToSubFingerprintData());
        }

        private IEnumerable<ulong> GetSubFingerprintMatches(int[] hashes, int thresholdVotes, ReadOnlyTransaction tx)
        {
            var counter = new Dictionary<ulong, int>();
            for (int table = 0; table < hashes.Length; ++table)
            {
                int hashBin = hashes[table];
                var ids = tx.GetSubFingerprintsByHashTableAndHash(table, hashBin);
                foreach (var id in ids)
                {
                    counter.TryGetValue(id, out var count);
                    counter[id] = count + 1;
                }
            }

            return counter.Where(pair => pair.Value >= thresholdVotes).Select(p => p.Key);
        }

        private int GetSubFingerprintCounts()
        {
            using (var tx = databaseContext.OpenReadOnlyTransaction())
            {
                return tx.GetSubFingerprintsCount();
            }
        }

        private IEnumerable<int> GetHashCountsPerTable()
        {
            using (var tx = databaseContext.OpenReadOnlyTransaction())
            {
                return tx.GetHashCountsPerTable();
            }
        }
    }
}