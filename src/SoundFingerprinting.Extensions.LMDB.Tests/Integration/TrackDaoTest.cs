using FluentAssertions;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
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

            context = new DatabaseContext(tempDirectory);

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

            var trackReference = trackDao.InsertTrack(track);

            AssertModelReferenceIsInitialized(trackReference);
        }

        [Fact]
        public void MultipleInsertFact()
        {
            var modelReferences = new ConcurrentBag<IModelReference>();
            for (int i = 0; i < 1000; i++)
            {
                var modelReference = trackDao.InsertTrack(new TrackData($"isrc{i}", "artist", "title", "album", 2012, 200));

                modelReferences.Should().NotContain(modelReference);
                modelReferences.Add(modelReference);
            }
        }

        [Fact]
        public void ReadAllTracksFact()
        {
            const int TrackCount = 5;
            var expectedTracks = InsertTracks(TrackCount);

            var tracks = trackDao.ReadAll();

            TrackCount.Should().Be(tracks.Count);
            foreach (var expectedTrack in expectedTracks)
            {
                tracks.Should().Contain(track => track.ISRC == expectedTrack.ISRC);
            }
        }

        [Fact]
        public void ReadByIdFact()
        {
            var track = new TrackData("isrc", "artist", "title", "album", 2012, 200);

            var trackReference = trackDao.InsertTrack(track);

            AssertTracksAreEqual(track, trackDao.ReadTrack(trackReference));
        }

        [Fact]
        public void InsertMultipleTrackAtOnceFact()
        {
            const int TrackCount = 100;
            var tracks = InsertTracks(TrackCount);

            var actualTracks = trackDao.ReadAll();

            tracks.Count.Should().Be(actualTracks.Count);
            for (int i = 0; i < actualTracks.Count; i++)
            {
                AssertModelReferenceIsInitialized(actualTracks[i].TrackReference);
                AssertTracksAreEqual(tracks[i], actualTracks.First(track => track.TrackReference.Equals(tracks[i].TrackReference)));
            }
        }

        [Fact]
        public void ReadTrackByArtistAndTitleFact()
        {
            TrackData track = GetTrack();
            trackDao.InsertTrack(track);

            var tracks = trackDao.ReadTrackByArtistAndTitleName(track.Artist, track.Title);

            tracks.Should().NotBeNullOrEmpty();
            tracks.Count.Should().Be(1);
            AssertTracksAreEqual(track, tracks[0]);
        }

        [Fact]
        public void ReadByNonExistentArtistAndTitleFact()
        {
            var tracks = trackDao.ReadTrackByArtistAndTitleName("artist", "title");

            tracks.Should().BeEmpty();
        }

        [Fact]
        public void ReadTrackByISRCFact()
        {
            TrackData expectedTrack = GetTrack();
            trackDao.InsertTrack(expectedTrack);

            TrackData actualTrack = trackDao.ReadTrackByISRC(expectedTrack.ISRC);

            AssertTracksAreEqual(expectedTrack, actualTrack);
        }

        [Fact]
        public void DeleteCollectionOfTracksFact()
        {
            const int NumberOfTracks = 10;
            var tracks = InsertTracks(NumberOfTracks);

            var allTracks = trackDao.ReadAll();

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
            TrackData track = GetTrack();
            var trackReference = trackDao.InsertTrack(track);

            var count = trackDao.DeleteTrack(trackReference);

            trackDao.ReadTrack(trackReference).Should().BeNull();
            count.Should().Be(1);
        }

        [Fact]
        public void DeleteHashBinsAndSubfingerprintsOnTrackDelete()
        {
            TagInfo tagInfo = GetTagInfo(1);
            int releaseYear = tagInfo.Year;
            var track = new TrackData(tagInfo.ISRC, tagInfo.Artist, tagInfo.Title, tagInfo.Album, releaseYear, (int)tagInfo.Duration);
            var trackReference = trackDao.InsertTrack(track);
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
            var actualTrack = trackDao.ReadTrackByISRC(tagInfo.ISRC);
            actualTrack.Should().NotBeNull();
            AssertTracksAreEqual(track, actualTrack);

            // Act
            int modifiedRows = trackDao.DeleteTrack(trackReference);

            trackDao.ReadTrackByISRC(tagInfo.ISRC).Should().BeNull();
            subFingerprintDao.ReadHashedFingerprintsByTrackReference(actualTrack.TrackReference).Should().BeEmpty();
            modifiedRows.Should().Be(1 + hashData.Count + (25 * hashData.Count));
        }

        [Fact]
        public void InserTrackShouldAcceptEmptyEntriesCodes()
        {
            TrackData track = new TrackData("isrc", string.Empty, string.Empty, string.Empty, 1986, 200);
            var trackReference = trackDao.InsertTrack(track);

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
                var modelReference = trackDao.InsertTrack(track);
                tracks.Add(new TrackData(track.ISRC, track.Artist, track.Title, track.Album, track.ReleaseYear, track.Length, modelReference));
            }

            return tracks;
        }

        private TrackData GetTrack()
        {
            return new TrackData(Guid.NewGuid().ToString(), "artist", "title", "album", 1986, 360);
        }
    }
}