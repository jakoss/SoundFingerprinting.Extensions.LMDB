using FluentAssertions;
using SoundFingerprinting.Configuration;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SoundFingerprinting.LMDB.Tests.Integration
{
    public class LMDBModelServiceTest : AbstractTest, IDisposable
    {
        private readonly LMDBModelService modelService;
        private readonly string tempDirectory;

        public LMDBModelServiceTest()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            File.Delete(tempDirectory);
            Directory.CreateDirectory(tempDirectory);
            modelService = new LMDBModelService(tempDirectory);
        }

        public void Dispose()
        {
            modelService.Dispose();
            Directory.Delete(tempDirectory, true);
        }

        [Fact]
        public void InsertTrackTest()
        {
            var track = new TrackData("isrc", "artist", "title", "album", 1986, 200);

            var trackReference = modelService.InsertTrack(track);

            AssertModelReferenceIsInitialized(trackReference);
        }

        [Fact]
        public void ReadTrackByTrackReferenceTest()
        {
            var expectedTrack = new TrackData("isrc", "artist", "title", "album", 1986, 200);
            var trackReference = modelService.InsertTrack(expectedTrack);

            var actualTrack = modelService.ReadTrackByReference(trackReference);

            AssertTracksAreEqual(expectedTrack, actualTrack);
        }

        [Fact]
        public void ReadTrackByISRCTest()
        {
            var expectedTrack = new TrackData("isrc", "artist", "title", "album", 1986, 200);
            modelService.InsertTrack(expectedTrack);

            var actualTrack = modelService.ReadTrackByISRC("isrc");

            AssertTracksAreEqual(expectedTrack, actualTrack);
        }

        [Fact]
        public void ReadTrackByArtistAndTitleTest()
        {
            var expectedTrack = new TrackData("isrc", "artist", "title", "album", 1986, 200);
            modelService.InsertTrack(expectedTrack);

            var actualTracks = modelService.ReadTrackByArtistAndTitleName("artist", "title");

            actualTracks.Count.Should().Be(1);
            AssertTracksAreEqual(expectedTrack, actualTracks[0]);
        }

        [Fact]
        public void ReadMultipleTracksTest()
        {
            const int NumberOfTracks = 100;
            var references = new HashSet<IModelReference>();
            for (int i = 0; i < NumberOfTracks; i++)
            {
                var track = new TrackData("isrc" + i, "artist", "title", "album", 1986, 200);
                var reference = modelService.InsertTrack(track);
                references.Add(reference).Should().BeTrue("Same primary key identifier must be returned after inserting a track to the collection.");
            }

            var actualTracks = modelService.ReadAllTracks();

            actualTracks.Count.Should().Be(NumberOfTracks);
        }

        [Fact]
        public void DeleteTrackTest()
        {
            var track = new TrackData("isrc", "artist", "title", "album", 1986, 200);
            var trackReference = modelService.InsertTrack(track);
            var hashedFingerprints = new HashedFingerprint(GenericHashBuckets(), 1, 0.928f, Enumerable.Empty<string>());
            modelService.InsertHashDataForTrack(new[] { hashedFingerprints }, trackReference);

            modelService.DeleteTrack(trackReference);

            var subFingerprints = modelService.ReadSubFingerprints(new[] { GenericHashBuckets() }, new DefaultQueryConfiguration()).ToList();
            subFingerprints.Should().BeEmpty();
            var actualTrack = modelService.ReadTrackByReference(trackReference);
            actualTrack.Should().BeNull();
        }

        [Fact]
        public void InsertHashDataTest()
        {
            var expectedTrack = new TrackData("isrc", "artist", "title", "album", 1986, 200);
            var trackReference = modelService.InsertTrack(expectedTrack);
            var hashedFingerprints = new HashedFingerprint(GenericHashBuckets(), 1, 0.928f, Enumerable.Empty<string>());
            modelService.InsertHashDataForTrack(new[] { hashedFingerprints }, trackReference);

            var subFingerprints = modelService.ReadSubFingerprints(new[] { GenericHashBuckets() }, new DefaultQueryConfiguration()).ToList();

            subFingerprints.Count.Should().Be(1);
            trackReference.Should().Be(subFingerprints[0].TrackReference);
            subFingerprints[0].SubFingerprintReference.GetHashCode().Should().NotBe(0);
            subFingerprints[0].Hashes.Should().BeEquivalentTo(GenericHashBuckets());
        }

        [Fact]
        public void ReadSubFingerprintsByHashBucketsHavingThresholdTest()
        {
            TrackData firstTrack = new TrackData("isrc1", "artist", "title", "album", 1986, 200);
            var firstTrackReference = modelService.InsertTrack(firstTrack);
            TrackData secondTrack = new TrackData("isrc2", "artist", "title", "album", 1986, 200);
            var secondTrackReference = modelService.InsertTrack(secondTrack);
            firstTrackReference.Should().NotBe(secondTrackReference);
            int[] firstTrackBuckets =
                {
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25
                };
            int[] secondTrackBuckets =
                {
                    2, 2, 4, 5, 6, 7, 7, 9, 10, 11, 12, 13, 14, 14, 16, 17, 18, 19, 20, 20, 22, 23, 24, 25, 26
                };
            var firstHashData = new HashedFingerprint(firstTrackBuckets, 1, 0.928f, Enumerable.Empty<string>());
            var secondHashData = new HashedFingerprint(secondTrackBuckets, 1, 0.928f, Enumerable.Empty<string>());

            modelService.InsertHashDataForTrack(new[] { firstHashData }, firstTrackReference);
            modelService.InsertHashDataForTrack(new[] { secondHashData }, secondTrackReference);

            // query buckets are similar with 5 elements from first track and 4 elements from second track
            int[] queryBuckets =
                {
                    3, 2, 5, 6, 7, 8, 7, 10, 11, 12, 13, 14, 15, 14, 17, 18, 19, 20, 21, 20, 23, 24, 25, 26, 25
                };

            var subFingerprints = modelService.ReadSubFingerprints(new[] { queryBuckets }, new LowLatencyQueryConfiguration()).ToList();

            subFingerprints.Count.Should().Be(1);
            firstTrackReference.Should().Be(subFingerprints[0].TrackReference);
        }

        [Fact]
        public void ReadSubFingerprintsByHashBucketsHavingThresholdWithGroupIdTest()
        {
            TrackData firstTrack = new TrackData("isrc1", "artist", "title", "album", 1986, 200);
            var firstTrackReference = modelService.InsertTrack(firstTrack);
            TrackData secondTrack = new TrackData("isrc2", "artist", "title", "album", 1986, 200);
            var secondTrackReference = modelService.InsertTrack(secondTrack);
            firstTrackReference.Should().NotBe(secondTrackReference);
            int[] firstTrackBuckets =
                {
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25
                };
            int[] secondTrackBuckets =
                {
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25
                };
            var firstHashData = new HashedFingerprint(firstTrackBuckets, 1, 0.928f, new[] { "first-group-id" });
            var secondHashData = new HashedFingerprint(secondTrackBuckets, 1, 0.928f, new[] { "second-group-id" });

            modelService.InsertHashDataForTrack(new[] { firstHashData }, firstTrackReference);
            modelService.InsertHashDataForTrack(new[] { secondHashData }, secondTrackReference);

            // query buckets are similar with 5 elements from first track and 4 elements from second track
            int[] queryBuckets =
                {
                    3, 2, 5, 6, 7, 8, 7, 10, 11, 12, 13, 14, 15, 14, 17, 18, 19, 20, 21, 20, 23, 24, 25, 26, 25
                };

            var subFingerprints = modelService.ReadSubFingerprints(new[] { queryBuckets }, new DefaultQueryConfiguration { Clusters = new[] { "first-group-id" } }).ToList();

            subFingerprints.Count.Should().Be(1);
            firstTrackReference.Should().Be(subFingerprints[0].TrackReference);
        }
    }
}