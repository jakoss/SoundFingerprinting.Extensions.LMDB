using FluentAssertions;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;
using SoundFingerprinting.Extensions.LMDB.LMDBDatabase;
using SoundFingerprinting.Strides;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SoundFingerprinting.Extensions.LMDB.Tests.Integration
{
    public class TrackDaoFact : IntegrationWithSampleFilesTest, IDisposable
    {
        private readonly IAudioService audioService = new SoundFingerprintingAudioService();
        private readonly ITrackDao trackDao;
        private readonly ISubFingerprintDao subFingerprintDao;
        private readonly DatabaseContext context;
        private readonly string tempDirectory;

        public TrackDaoFact()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            File.Delete(tempDirectory);
            Directory.CreateDirectory(tempDirectory);

            context = new DatabaseContext(tempDirectory, new LMDBConfiguration());

            trackDao = new TrackDao(context);
            subFingerprintDao = new SubFingerprintDao(context);
        }

        public void Dispose()
        {
            context.Dispose();
            Directory.Delete(tempDirectory, true);
        }

        [Fact]
        public void InsertTrackFact()
        {
            var track = GetTrack();

            var trackReference = trackDao.InsertTrack(track).TrackReference;

            AssertModelReferenceIsInitialized(trackReference);
        }

        [Fact]
        public void MultipleInsertFact()
        {
            var modelReferences = new ConcurrentBag<IModelReference>();
            for (int i = 0; i < 1000; i++)
            {
                var modelReference = trackDao.InsertTrack(new TrackInfo($"isrc{i}", "title", "artist", 200)).TrackReference;

                modelReferences.Should().NotContain(modelReference);
                modelReferences.Add(modelReference);
            }
        }

        [Fact]
        public void ReadAllTracksFact()
        {
            const int TrackCount = 5;
            var expectedTracks = InsertTracks(TrackCount);

            var tracks = trackDao.ReadAll().ToList();

            TrackCount.Should().Be(tracks.Count);
            foreach (var expectedTrack in expectedTracks)
            {
                tracks.Should().Contain(track => track.ISRC == expectedTrack.ISRC);
            }
        }

        [Fact]
        public void ReadByIdFact()
        {
            var track = new TrackInfo("isrc", "title", "artist", 200);

            var trackReference = trackDao.InsertTrack(track).TrackReference;

            AssertTracksAreEqual(track, trackDao.ReadTrack(trackReference));
        }

        [Fact]
        public void InsertMultipleTrackAtOnceFact()
        {
            const int TrackCount = 100;
            var tracks = InsertTracks(TrackCount);

            var actualTracks = trackDao.ReadAll().ToList();

            tracks.Count.Should().Be(actualTracks.Count);
        }

        [Fact]
        public void ReadTrackByTitleFact()
        {
            var track = GetTrack();
            trackDao.InsertTrack(track);

            var tracks = trackDao.ReadTrackByTitle(track.Title).ToList();

            tracks.Should().NotBeNullOrEmpty();
            tracks.Count.Should().Be(1);
            AssertTracksAreEqual(track, tracks[0]);
        }

        [Fact]
        public void ReadByNonExistentTitleFact()
        {
            var tracks = trackDao.ReadTrackByTitle("title");

            tracks.Should().BeEmpty();
        }

        [Fact]
        public void ReadTrackByIdFact()
        {
            var expectedTrack = GetTrack();
            trackDao.InsertTrack(expectedTrack);

            TrackData actualTrack = trackDao.ReadTrackById(expectedTrack.Id);

            AssertTracksAreEqual(expectedTrack, actualTrack);
        }

        [Fact]
        public void DeleteCollectionOfTracksFact()
        {
            const int NumberOfTracks = 10;
            var tracks = InsertTracks(NumberOfTracks);

            var allTracks = trackDao.ReadAll().ToList();

            allTracks.Count.Should().Be(NumberOfTracks);
            foreach (var track in tracks)
            {
                trackDao.DeleteTrack(track.TrackReference);
            }

            trackDao.ReadAll().Should().BeEmpty();
        }

        [Fact]
        public void DeleteOneTrackFact()
        {
            var track = GetTrack();
            var trackReference = trackDao.InsertTrack(track).TrackReference;

            var count = trackDao.DeleteTrack(trackReference);

            trackDao.ReadTrack(trackReference).Should().BeNull();
            count.Should().Be(1);
        }

        [Fact]
        public void DeleteHashBinsAndSubfingerprintsOnTrackDelete()
        {
            TagInfo tagInfo = GetTagInfo(1);
            var track = new TrackInfo(tagInfo.ISRC, tagInfo.Title, tagInfo.Artist, tagInfo.Duration);
            var trackReference = trackDao.InsertTrack(track).TrackReference;
            var hashData = FingerprintCommandBuilder.Instance
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .WithFingerprintConfig(config =>
                    {
                        config.Stride = new StaticStride(0);
                        return config;
                    })
                .UsingServices(audioService)
                .Hash()
                .Result;

            subFingerprintDao.InsertHashDataForTrack(hashData, trackReference);
            var actualTrack = trackDao.ReadTrackById(tagInfo.ISRC);
            actualTrack.Should().NotBeNull();
            AssertTracksAreEqual(track, actualTrack);

            // Act
            int modifiedRows = trackDao.DeleteTrack(trackReference) + subFingerprintDao.DeleteSubFingerprintsByTrackReference(trackReference);

            trackDao.ReadTrackById(tagInfo.ISRC).Should().BeNull();
            subFingerprintDao.ReadHashedFingerprintsByTrackReference(actualTrack.TrackReference).Should().BeEmpty();
            modifiedRows.Should().Be(1 + hashData.Count + (25 * hashData.Count));
        }

        [Fact]
        public void InsertTrackShouldAcceptEmptyEntriesCodes()
        {
            var track = new TrackInfo("isrc", string.Empty, string.Empty, 200);
            var trackReference = trackDao.InsertTrack(track).TrackReference;

            var actualTrack = trackDao.ReadTrack(trackReference);

            AssertModelReferenceIsInitialized(trackReference);
            AssertTracksAreEqual(track, actualTrack);
        }

        private List<TrackData> InsertTracks(int trackCount)
        {
            var tracks = new List<TrackData>();
            for (int i = 0; i < trackCount; i++)
            {
                var track = GetTrack();
                tracks.Add(trackDao.InsertTrack(track));
            }

            return tracks;
        }

        private TrackInfo GetTrack()
        {
            return new TrackInfo(Guid.NewGuid().ToString(), "title", "artist", 360);
        }
    }
}