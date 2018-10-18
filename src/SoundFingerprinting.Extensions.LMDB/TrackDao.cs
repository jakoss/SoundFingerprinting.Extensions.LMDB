using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Extensions.LMDB.DTO;
using SoundFingerprinting.Extensions.LMDB.LMDBDatabase;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SoundFingerprinting.Extensions.LMDB
{
    internal class TrackDao : ITrackDao
    {
        private readonly DatabaseContext databaseContext;

        internal TrackDao(DatabaseContext databaseContext)
        {
            this.databaseContext = databaseContext;
        }

        public int DeleteTrack(IModelReference trackReference)
        {
            using (var tx = databaseContext.OpenReadWriteTransaction())
            {
                try
                {
                    var count = 0;
                    var trackId = (ulong)trackReference.Id;
                    var trackData = tx.GetTrackById(trackId);
                    if (trackData == null) throw new Exception("Track not found");

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

                    tx.RemoveTrack(trackData);
                    count++;

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

        public IModelReference InsertTrack(TrackData track)
        {
            using (var tx = databaseContext.OpenReadWriteTransaction())
            {
                try
                {
                    var newTrackId = tx.GetLastTrackId();
                    newTrackId++;
                    var trackReference = new ModelReference<ulong>(newTrackId);
                    var newTrack = new TrackDataDTO(track, trackReference);

                    tx.PutTrack(newTrack);

                    tx.Commit();
                    return trackReference;
                }
                catch (Exception)
                {
                    tx.Abort();
                    throw;
                }
            }
        }

        public IList<TrackData> ReadAll()
        {
            using (var tx = databaseContext.OpenReadOnlyTransaction())
            {
                return tx.GetAllTracks().Select(e => e.ToTrackData()).ToList();
            }
        }

        public TrackData ReadTrack(IModelReference trackReference)
        {
            using (var tx = databaseContext.OpenReadOnlyTransaction())
            {
                return tx.GetTrackById((ulong)trackReference.Id)?.ToTrackData();
            }
        }

        public IList<TrackData> ReadTrackByArtistAndTitleName(string artist, string title)
        {
            using (var tx = databaseContext.OpenReadOnlyTransaction())
            {
                return tx.GetTracksByArtistAndTitleName(artist, title).Select(e => e.ToTrackData()).ToList();
            }
        }

        public TrackData ReadTrackByISRC(string isrc)
        {
            using (var tx = databaseContext.OpenReadOnlyTransaction())
            {
                return tx.GetTrackByISRC(isrc)?.ToTrackData();
            }
        }

        public List<TrackData> ReadTracks(IEnumerable<IModelReference> ids)
        {
            var result = new List<TrackData>();
            foreach (var id in ids)
            {
                result.Add(ReadTrack(id));
            }

            return result;
        }
    }
}