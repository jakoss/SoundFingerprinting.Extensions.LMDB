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

        internal SubFingerprintDao(DatabaseContext databaseContext)
        {
            this.databaseContext = databaseContext;
        }

        public void InsertHashDataForTrack(IEnumerable<HashedFingerprint> hashes, IModelReference trackReference)
        {
            using (var tx = databaseContext.OpenReadWriteTransaction())
            {
                var trackId = (ulong)trackReference.Id;
                var trackData = tx.GetTrackById(trackId);
                if (trackData == null) throw new Exception("Track not found");

                var newIds = new List<ulong>();

                ulong newSubFingerprintId = tx.GetLastSubFingerprintId();

                foreach (var hashedFingerprint in hashes)
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
                    newIds.Add(newSubFingerprintId);

                    // Insert hashes to hashTable
                    int table = 0;
                    foreach (var hash in hashedFingerprint.HashBins)
                    {
                        tx.PutSubFingerprintsByHashTableAndHash(table, hash, newSubFingerprintId);
                        table++;
                    }
                }

                foreach (var id in newIds)
                {
                    trackData.Subfingerprints.Add(id);
                }
                tx.PutTrack(trackData);

                tx.Commit();
            }
        }

        public IList<HashedFingerprint> ReadHashedFingerprintsByTrackReference(IModelReference trackReference)
        {
            using (var tx = databaseContext.OpenReadOnlyTransaction())
            {
                var trackId = (ulong)trackReference.Id;
                var result = new List<HashedFingerprint>();
                var trackData = tx.GetTrackById(trackId);
                if (trackData != null)
                {
                    // TODO : Move Subfingerprints to separate database with multiple values
                    foreach (var id in trackData.Subfingerprints)
                    {
                        var subFingerprint = tx.GetSubFingerprint(id);
                        result.Add(new HashedFingerprint(
                            subFingerprint.Hashes,
                            subFingerprint.SequenceNumber,
                            subFingerprint.SequenceAt,
                            subFingerprint.Clusters
                        ));
                    }
                }
                return result;
            }
        }

        public IEnumerable<SubFingerprintData> ReadSubFingerprints(int[] hashes, int thresholdVotes, IEnumerable<string> assignedClusters)
        {
            using (var tx = databaseContext.OpenReadOnlyTransaction())
            {
                return ReadSubFingerprints(hashes, thresholdVotes, assignedClusters, tx);
            }
        }

        public ISet<SubFingerprintData> ReadSubFingerprints(IEnumerable<int[]> hashes, int threshold, IEnumerable<string> assignedClusters)
        {
            var allSubs = new ConcurrentBag<SubFingerprintData>();
            using (var tx = databaseContext.OpenReadOnlyTransaction())
            {
                Parallel.ForEach(hashes, hashedFingerprint =>
                {
                    foreach (var subFingerprint in ReadSubFingerprints(hashedFingerprint, threshold, assignedClusters, tx))
                    {
                        allSubs.Add(subFingerprint);
                    }
                });

                return new HashSet<SubFingerprintData>(allSubs);
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
    }
}