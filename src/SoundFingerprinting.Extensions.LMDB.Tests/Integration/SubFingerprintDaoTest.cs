using FluentAssertions;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.Configuration;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;
using SoundFingerprinting.Extensions.LMDB.LMDBDatabase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SoundFingerprinting.Extensions.LMDB.Tests.Integration
{
    public class SubFingerprintDaoTest : IntegrationWithSampleFilesTest, IDisposable
    {
        private readonly FingerprintCommandBuilder fingerprintCommandBuilder = new FingerprintCommandBuilder();
        private readonly IAudioService audioService = new SoundFingerprintingAudioService();
        private readonly ITrackDao trackDao;
        private readonly ISubFingerprintDao subFingerprintDao;
        private readonly DatabaseContext context;
        private readonly string tempDirectory;

        public SubFingerprintDaoTest()
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
        public void ShouldInsertAndReadSubFingerprints()
        {
            var track = new TrackInfo("isrc", "artist", "title", 200);
            var trackData = trackDao.InsertTrack(track);
            const int NumberOfHashBins = 100;
            var genericHashBuckets = GenericHashBuckets();
            var hashedFingerprints =
                Enumerable.Range(0, NumberOfHashBins)
                    .Select(
                        sequenceNumber =>
                            new HashedFingerprint(
                                genericHashBuckets,
                                (uint)sequenceNumber,
                                sequenceNumber * 0.928f,
                                Enumerable.Empty<string>()));

            InsertHashedFingerprintsForTrack(hashedFingerprints, trackData.TrackReference);

            var fingerprints = subFingerprintDao.ReadHashedFingerprintsByTrackReference(trackData.TrackReference)
                .Select(ToHashedFingerprint()).ToList();
            NumberOfHashBins.Should().Be(fingerprints.Count);
            foreach (var hashedFingerprint in fingerprints)
            {
                genericHashBuckets.Should().BeEquivalentTo(hashedFingerprint.HashBins);
            }
        }

        [Fact]
        public void SameNumberOfHashBinsIsInsertedInAllTablesWhenFingerprintingEntireSongTest()
        {
            var track = new TrackInfo("isrc", "artist", "title", 120d);
            var trackData = trackDao.InsertTrack(track);
            var hashedFingerprints = fingerprintCommandBuilder
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;

            InsertHashedFingerprintsForTrack(hashedFingerprints, trackData.TrackReference);

            var hashes = subFingerprintDao.ReadHashedFingerprintsByTrackReference(trackData.TrackReference)
                .Select(ToHashedFingerprint()).ToList();
            hashedFingerprints.Count.Should().Be(hashes.Count);
            foreach (var data in hashes)
            {
                data.HashBins.Length.Should().Be(25);
            }
        }

        [Fact]
        public void ReadByTrackGroupIdWorksAsExpectedTest()
        {
            var firstTrack = new TrackInfo("isrc1", "artist", "title", 120d);
            var secondTrack = new TrackInfo("isrc2", "artist", "title", 120d);

            var firstTrackData = trackDao.InsertTrack(firstTrack);
            var secondTrackData = trackDao.InsertTrack(secondTrack);

            var hashedFingerprintsForFirstTrack = fingerprintCommandBuilder
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .WithFingerprintConfig(config =>
                {
                    config.Clusters = new[] { "first-group-id" };
                    return config;
                })
                .UsingServices(audioService)
                .Hash()
                .Result;
            //.OrderBy(e => e.StartsAt);

            InsertHashedFingerprintsForTrack(hashedFingerprintsForFirstTrack, firstTrackData.TrackReference);

            var hashedFingerprintsForSecondTrack = fingerprintCommandBuilder
               .BuildFingerprintCommand()
               .From(GetAudioSamples())
               .WithFingerprintConfig(config =>
               {
                   config.Clusters = new[] { "second-group-id" };
                   return config;
               })
               .UsingServices(audioService)
               .Hash()
               .Result;
            //.OrderBy(e => e.StartsAt);
            InsertHashedFingerprintsForTrack(hashedFingerprintsForSecondTrack, secondTrackData.TrackReference);

            var metaFields = new Dictionary<string, string>();

            const int ThresholdVotes = 25;
            foreach (var hashedFingerprint in hashedFingerprintsForFirstTrack)
            {
                var subFingerprintData = subFingerprintDao.ReadSubFingerprints(
                    new[] { hashedFingerprint.HashBins }, new DefaultQueryConfiguration
                    {
                        ThresholdVotes = ThresholdVotes,
                        Clusters = new[] { "first-group-id" }
                    }).ToList();

                subFingerprintData.Count.Should().Be(1);
                firstTrackData.TrackReference.Should().BeEquivalentTo(subFingerprintData[0].TrackReference);

                subFingerprintData = subFingerprintDao.ReadSubFingerprints(
                    new[] { hashedFingerprint.HashBins }, new DefaultQueryConfiguration
                    {
                        ThresholdVotes = ThresholdVotes,
                        Clusters = new[] { "second-group-id" }
                    }).ToList();

                subFingerprintData.Count.Should().Be(1);
                secondTrackData.TrackReference.Should().BeEquivalentTo(subFingerprintData[0].TrackReference);

                subFingerprintData = subFingerprintDao.ReadSubFingerprints(
                    new[] { hashedFingerprint.HashBins }, new DefaultQueryConfiguration
                    {
                        ThresholdVotes = ThresholdVotes
                    }).ToList();
                subFingerprintData.Count.Should().Be(2);
            }
        }

        [Fact]
        public void ReadHashDataByTrackTest()
        {
            var firstTrack = new TrackInfo("isrc1", "artist", "title", 200);

            var firstTrackData = trackDao.InsertTrack(firstTrack);

            var firstHashData = fingerprintCommandBuilder
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;

            InsertHashedFingerprintsForTrack(firstHashData, firstTrackData.TrackReference);

            var secondTrack = new TrackInfo("isrc2", "artist", "title", 200);

            var secondTrackData = trackDao.InsertTrack(secondTrack);

            var secondHashData = fingerprintCommandBuilder
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;

            InsertHashedFingerprintsForTrack(secondHashData, secondTrackData.TrackReference);

            var resultFirstHashData = subFingerprintDao.ReadHashedFingerprintsByTrackReference(firstTrackData.TrackReference)
                .Select(ToHashedFingerprint()).ToList();
            AssertHashDatasAreTheSame(firstHashData, resultFirstHashData);

            var resultSecondHashData = subFingerprintDao.ReadHashedFingerprintsByTrackReference(secondTrackData.TrackReference)
                .Select(ToHashedFingerprint()).ToList();
            AssertHashDatasAreTheSame(secondHashData, resultSecondHashData);
        }

        private void InsertHashedFingerprintsForTrack(IEnumerable<HashedFingerprint> hashedFingerprints, IModelReference trackReference)
        {
            subFingerprintDao.InsertHashDataForTrack(hashedFingerprints, trackReference);
        }

        private static Func<SubFingerprintData, HashedFingerprint> ToHashedFingerprint()
        {
            return subFingerprint => new HashedFingerprint(
                subFingerprint.Hashes,
                subFingerprint.SequenceNumber,
                subFingerprint.SequenceAt,
                subFingerprint.Clusters);
        }
    }
}