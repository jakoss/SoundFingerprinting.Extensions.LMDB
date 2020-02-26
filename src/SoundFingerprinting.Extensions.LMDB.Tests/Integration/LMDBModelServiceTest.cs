using FluentAssertions;
using SoundFingerprinting.Configuration;
using SoundFingerprinting.DAO;
using SoundFingerprinting.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SoundFingerprinting.Extensions.LMDB.Tests.Integration
{
    public sealed class LMDBModelServiceTest : AbstractTest, IDisposable
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
            var track = new TrackInfo("isrc", "title", "artist");

            modelService.Insert(track, GetGenericHashesFingerprints());

            var trackInfo = modelService.ReadTrackById(track.Id);
            trackInfo.Should().NotBeNull();
        }

        [Fact]
        public void ReadTrackByTrackReferenceTest()
        {
            var expectedTrack = new TrackInfo("isrc", "title", "artist");
            modelService.Insert(expectedTrack, GetGenericHashesFingerprints());

            var actualTrack = modelService.ReadTrackById(expectedTrack.Id);

            AssertTracksAreEqual(expectedTrack, actualTrack);

            modelService.DeleteTrack(actualTrack.Id);

            var result = modelService.ReadTrackById(actualTrack.Id);

            result.Should().BeNull();
        }

        [Fact]
        public void ReadTrackByISRCTest()
        {
            var expectedTrack = new TrackInfo("isrc", "title", "artist");
            modelService.Insert(expectedTrack, GetGenericHashesFingerprints());

            var actualTrack = modelService.ReadTrackById(expectedTrack.Id);

            AssertTracksAreEqual(expectedTrack, actualTrack);
        }

        [Fact]
        public void ReadTrackByTitleTest()
        {
            var expectedTrack = new TrackInfo("isrc", "title", "artist");
            modelService.Insert(expectedTrack, GetGenericHashesFingerprints());

            var actualTracks = modelService.ReadTrackByTitle("title").ToList();

            actualTracks.Count.Should().Be(1);
            AssertTracksAreEqual(expectedTrack, actualTracks[0]);
        }

        [Fact]
        public void ReadMultipleTracksTest()
        {
            const int NumberOfTracks = 100;
            for (int i = 0; i < NumberOfTracks; i++)
            {
                var track = new TrackInfo("isrc" + i, "title", "artist");
                modelService.Insert(track, GetGenericHashesFingerprints());
            }

            var actualTracks = modelService.ReadAllTracks().ToList();

            actualTracks.Count.Should().Be(NumberOfTracks);
        }

        [Fact]
        public void DeleteTrackTest()
        {
            var track = new TrackInfo("isrc", "title", "artist");
            modelService.Insert(track, GetGenericHashesFingerprints());

            modelService.DeleteTrack(track.Id);

            var subFingerprints = modelService.Query(new[] { GenericHashBuckets() }, new DefaultQueryConfiguration()).ToList();
            subFingerprints.Should().BeEmpty();
            var actualTrack = modelService.ReadTrackById("irsc");
            actualTrack.Should().BeNull();
        }

        [Fact]
        public void InsertHashDataTest()
        {
            var expectedTrack = new TrackInfo("isrc", "title", "artist");
            modelService.Insert(expectedTrack, GetGenericHashesFingerprints());

            var subFingerprints = modelService.Query(new[] { GenericHashBuckets() }, new DefaultQueryConfiguration()).ToList();

            subFingerprints.Count.Should().Be(1);
            var track = modelService.ReadAllTracks().First();
            track.TrackReference.Should().Be(subFingerprints[0].TrackReference);
            subFingerprints[0].SubFingerprintReference.GetHashCode().Should().NotBe(0);
            subFingerprints[0].Hashes.Should().BeEquivalentTo(GenericHashBuckets());
        }

        [Fact]
        public void ReadSubFingerprintsByHashBucketsHavingThresholdTest()
        {
            var firstTrack = new TrackInfo("isrc1", "title", "artist");
            var secondTrack = new TrackInfo("isrc2", "title", "artist");

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

            modelService.Insert(firstTrack, new Hashes(new[] { firstHashData }, 200));
            modelService.Insert(secondTrack, new Hashes(new[] { secondHashData }, 200));

            var firstTrackReference = modelService.ReadAllTracks().First(t => t.Id == firstTrack.Id).TrackReference;
            var secondTrackReference = modelService.ReadAllTracks().First(t => t.Id == secondTrack.Id).TrackReference;


            firstTrackReference.Should().NotBe(secondTrackReference);

            // query buckets are similar with 5 elements from first track and 4 elements from second track
            int[] queryBuckets =
                {
                    3, 2, 5, 6, 7, 8, 7, 10, 11, 12, 13, 14, 15, 14, 17, 18, 19, 20, 21, 20, 23, 24, 25, 26, 25
                };

            var subFingerprints = modelService.Query(new[] { queryBuckets }, new LowLatencyQueryConfiguration()).ToList();

            subFingerprints.Count.Should().Be(1);
            firstTrackReference.Should().Be(subFingerprints[0].TrackReference);
        }

        [Fact]
        public void ReadSubFingerprintsByHashBucketsHavingThresholdWithGroupIdTest()
        {
            var firstTrack = new TrackInfo("isrc1", "title", "artist");
            var secondTrack = new TrackInfo("isrc2", "title", "artist");

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

            modelService.Insert(firstTrack, new Hashes(new[] { firstHashData }, 200));
            modelService.Insert(secondTrack, new Hashes(new[] { secondHashData }, 200));

            var firstTrackReference = modelService.ReadAllTracks().First(t => t.Id == firstTrack.Id).TrackReference;
            var secondTrackReference = modelService.ReadAllTracks().First(t => t.Id == secondTrack.Id).TrackReference;

            firstTrackReference.Should().NotBe(secondTrackReference);

            // query buckets are similar with 5 elements from first track and 4 elements from second track
            int[] queryBuckets =
                {
                    3, 2, 5, 6, 7, 8, 7, 10, 11, 12, 13, 14, 15, 14, 17, 18, 19, 20, 21, 20, 23, 24, 25, 26, 25
                };

            var subFingerprints = modelService.Query(new[] { queryBuckets }, new DefaultQueryConfiguration { Clusters = new HashSet<string>(new [] { "first-group-id" }) }).ToList();

            subFingerprints.Count.Should().Be(1);
            firstTrackReference.Should().Be(subFingerprints[0].TrackReference);
        }

        private Hashes GetGenericHashesFingerprints()
        {
            return new Hashes(new[] { new HashedFingerprint(GenericHashBuckets(), 0, 0f, Enumerable.Empty<string>()) }, 200);
        }
    }
}