using SoundFingerprinting.Configuration;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;
using SoundFingerprinting.Extensions.LMDB.LMDBDatabase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SoundFingerprinting.Extensions.LMDB.Tests")]

namespace SoundFingerprinting.Extensions.LMDB
{
    public sealed class LMDBModelService : IModelService, IDisposable
    {
        private const string VIDEO_NOT_SUPPORTED_MESSAGE = "This storage is designed to handle only audio media type of tracks";
        private const string Id = "lmdb-model-service";
        private readonly DatabaseContext databaseContext;

        public LMDBModelService(string pathToDatabase, LMDBConfiguration configuration = null)
        {
            databaseContext = new DatabaseContext(pathToDatabase, configuration ?? new LMDBConfiguration());
            TrackDao = new TrackDao(databaseContext);
            SubFingerprintDao = new SubFingerprintDao(databaseContext);
        }

        private TrackDao TrackDao { get; }
        private SubFingerprintDao SubFingerprintDao { get; }

        public IEnumerable<ModelServiceInfo> Info => new[] { new ModelServiceInfo(Id, TrackDao.Count, SubFingerprintDao.SubFingerprintsCount, SubFingerprintDao.HashCountsPerTable.ToArray()) };

        public void CopyAndCompactLmdbDatabase(string newPath)
        {
            databaseContext.CopyAndCompactLmdbDatabase(newPath);
        }

        public void Insert(TrackInfo trackInfo, AVHashes hashes)
        {
            if (trackInfo.MediaType.HasFlag(MediaType.Video) || hashes.Audio is null)
            {
                throw new NotSupportedException(VIDEO_NOT_SUPPORTED_MESSAGE);
            }

            var audioHashes = hashes.Audio;
            var fingerprints = audioHashes.ToList();
            if (fingerprints.Count == 0)
            {
                return;
            }

            var trackData = TrackDao.InsertTrack(trackInfo, audioHashes.DurationInSeconds);
            SubFingerprintDao.InsertHashDataForTrack(audioHashes, trackData.TrackReference);
        }

        public void UpdateTrack(TrackInfo trackInfo)
        {
            if (trackInfo.MediaType.HasFlag(MediaType.Video))
            {
                throw new NotSupportedException(VIDEO_NOT_SUPPORTED_MESSAGE);
            }

            var track = TrackDao.ReadTrackById(trackInfo.Id);
            if (track == null)
            {
                throw new ArgumentException($"Could not find track {trackInfo.Id} to update", nameof(trackInfo.Id));
            }

            var subFingerprints = SubFingerprintDao.ReadHashedFingerprintsByTrackReference(track.TrackReference);
            var hashes = new Hashes(subFingerprints.Select(subFingerprint => new HashedFingerprint(subFingerprint.Hashes, subFingerprint.SequenceNumber, subFingerprint.SequenceAt, subFingerprint.OriginalPoint)), track.Length, track.MediaType);
            DeleteTrack(trackInfo.Id);
            Insert(trackInfo, new AVHashes(hashes, null));
        }

        public void DeleteTrack(string trackId)
        {
            var track = TrackDao.ReadTrackById(trackId);
            if (track == null)
            {
                return;
            }

            var trackReference = track.TrackReference;
            SubFingerprintDao.DeleteSubFingerprintsByTrackReference(trackReference);
            TrackDao.DeleteTrack(trackReference);
        }

        public IEnumerable<SubFingerprintData> Query(Hashes hashes, QueryConfiguration config)
        {
            if (hashes.MediaType != MediaType.Audio)
            {
                throw new NotSupportedException(VIDEO_NOT_SUPPORTED_MESSAGE);
            }

            var queryHashes = hashes.Select(hashedFingerprint => hashedFingerprint.HashBins);
            return hashes.Count > 0 ? SubFingerprintDao.ReadSubFingerprints(queryHashes, config) : Enumerable.Empty<SubFingerprintData>();
        }

        public AVHashes ReadHashesByTrackId(string trackId)
        {
            var track = TrackDao.ReadTrackById(trackId);
            if (track == null)
            {
                return AVHashes.Empty;
            }

            var fingerprints = SubFingerprintDao
                .ReadHashedFingerprintsByTrackReference(track.TrackReference)
                .Select(subFingerprint => new HashedFingerprint(subFingerprint.Hashes, subFingerprint.SequenceNumber, subFingerprint.SequenceAt, subFingerprint.OriginalPoint));
            return new AVHashes(new Hashes(fingerprints, track.Length, MediaType.Audio), Hashes.GetEmpty(MediaType.Video));
        }

        public IEnumerable<TrackData> ReadTracksByReferences(IEnumerable<IModelReference> references)
        {
            return TrackDao.ReadTracksByReferences(references);
        }

        public TrackInfo ReadTrackById(string trackId)
        {
            var trackData = TrackDao.ReadTrackById(trackId);
            if (trackData == null)
            {
                return null;
            }

            var metaFields = CopyMetaFields(trackData.MetaFields);
            metaFields.Add("TrackLength", $"{trackData.Length: 0.000}");
            return new TrackInfo(trackData.Id, trackData.Title, trackData.Artist, metaFields, trackData.MediaType);
        }

        public IEnumerable<string> GetTrackIds()
        {
            return TrackDao.GetTrackIds();
        }

        private static IDictionary<string, string> CopyMetaFields(IDictionary<string, string> metaFields)
        {
            return metaFields == null ? new Dictionary<string, string>() : metaFields.ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (disposedValue) return;
            databaseContext.Dispose();

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }

            disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~LMDBModelService()
        {
            Dispose(false);
        }

        #endregion IDisposable Support
    }
}
