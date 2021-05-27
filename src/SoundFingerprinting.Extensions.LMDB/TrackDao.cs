using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;
using SoundFingerprinting.Extensions.LMDB.DTO;
using SoundFingerprinting.Extensions.LMDB.Exceptions;
using SoundFingerprinting.Extensions.LMDB.LMDBDatabase;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SoundFingerprinting.Extensions.LMDB
{
    internal class TrackDao
    {
        private readonly DatabaseContext databaseContext;

        public int Count => GetTracksCount();

        internal TrackDao(DatabaseContext databaseContext)
        {
            this.databaseContext = databaseContext;
        }

        public int DeleteTrack(IModelReference trackReference)
        {
            using var tx = databaseContext.OpenReadWriteTransaction();
            try
            {
                var trackId = trackReference.Get<ulong>();
                var trackData = tx.GetTrackById(trackId);
                if (trackData == null) throw new TrackNotFoundException(trackId);
                tx.RemoveTrack(trackData);

                tx.Commit();
                return 1;
            }
            catch (Exception)
            {
                tx.Abort();
                throw;
            }
        }

        public TrackData InsertTrack(TrackInfo track, double durationInSeconds)
        {
            using var tx = databaseContext.OpenReadWriteTransaction();
            try
            {
                var newTrackId = tx.GetLastTrackId();
                newTrackId++;
                var trackReference = new ModelReference<ulong>(newTrackId);
                var newTrack = new TrackDataDTO(track, durationInSeconds, trackReference);

                tx.PutTrack(newTrack);

                tx.Commit();
                return newTrack.ToTrackData();
            }
            catch (Exception)
            {
                tx.Abort();
                throw;
            }
        }

        public IEnumerable<TrackData> ReadAll()
        {
            using var tx = databaseContext.OpenReadOnlyTransaction();
            foreach (var track in tx.GetAllTracks())
            {
                yield return track.ToTrackData();
            }
        }

        public IEnumerable<string> GetTrackIds()
        {
            using var tx = databaseContext.OpenReadOnlyTransaction();
            foreach (var track in tx.GetAllTracks())
            {
                yield return track.Id;
            }
        }

        public IEnumerable<TrackData> ReadTrackByTitle(string title)
        {
            using var tx = databaseContext.OpenReadOnlyTransaction();
            return tx.GetTracksByTitle(title).Select(e => e.ToTrackData());
        }

        public TrackData ReadTrackById(string id)
        {
            using var tx = databaseContext.OpenReadOnlyTransaction();
            return tx.GetTrackById(id)?.ToTrackData();
        }

        public IEnumerable<TrackData> ReadTracksByReferences(IEnumerable<IModelReference> references)
        {
            using var tx = databaseContext.OpenReadOnlyTransaction();
            foreach (var trackReference in references)
            {
                yield return tx.GetTrackByReference(trackReference.Get<ulong>())?.ToTrackData();
            }
        }

        private int GetTracksCount()
        {
            using var tx = databaseContext.OpenReadOnlyTransaction();
            return tx.GetTracksCount();
        }
    }
}