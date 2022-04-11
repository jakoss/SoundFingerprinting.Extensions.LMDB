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
    public sealed class SubFingerprintDaoTest : IntegrationWithSampleFilesTest, IDisposable
    {
        private readonly FingerprintCommandBuilder fingerprintCommandBuilder = new FingerprintCommandBuilder();
        private readonly IAudioService audioService = new SoundFingerprintingAudioService();
        private readonly TrackDao trackDao;
        private readonly SubFingerprintDao subFingerprintDao;
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
            var track = new TrackInfo("isrc", "title", "artist");
            var trackData = trackDao.InsertTrack(track, 200);
            const int numberOfHashBins = 100;
            var genericHashBuckets = GenericHashBuckets();
            var hashedFingerprints =
                Enumerable.Range(0, numberOfHashBins)
                    .Select(
                        sequenceNumber =>
                            new HashedFingerprint(
                                genericHashBuckets,
                                (uint)sequenceNumber,
                                sequenceNumber * 0.928f,
                                Array.Empty<byte>()));

            InsertHashedFingerprintsForTrack(hashedFingerprints, trackData.TrackReference);

            var fingerprints = subFingerprintDao.ReadHashedFingerprintsByTrackReference(trackData.TrackReference)
                .Select(ToHashedFingerprint()).ToList();
            numberOfHashBins.Should().Be(fingerprints.Count);
            foreach (var hashedFingerprint in fingerprints)
            {
                genericHashBuckets.Should().BeEquivalentTo(hashedFingerprint.HashBins);
            }
        }

        [Fact]
        public void SameNumberOfHashBinsIsInsertedInAllTablesWhenFingerprintingEntireSongTest()
        {
            var track = new TrackInfo("isrc", "title", "artist");
            var trackData = trackDao.InsertTrack(track, 120d);
            var hashedFingerprints = fingerprintCommandBuilder
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;

            InsertHashedFingerprintsForTrack(hashedFingerprints.Audio, trackData.TrackReference);

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
            var firstTrack = new TrackInfo("isrc1", "title", "artist",
                new Dictionary<string, string> { { "group-id", "first-group-id" } });
            var secondTrack = new TrackInfo("isrc2", "title", "artist",
                new Dictionary<string, string> { { "group-id", "second-group-id" } });

            var firstTrackData = trackDao.InsertTrack(firstTrack, 120d);
            var secondTrackData = trackDao.InsertTrack(secondTrack, 120d);

            var hashedFingerprintsForFirstTrack = fingerprintCommandBuilder
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;
            //.OrderBy(e => e.StartsAt);

            InsertHashedFingerprintsForTrack(hashedFingerprintsForFirstTrack.Audio, firstTrackData.TrackReference);

            var hashedFingerprintsForSecondTrack = fingerprintCommandBuilder
               .BuildFingerprintCommand()
               .From(GetAudioSamples())
               .UsingServices(audioService)
               .Hash()
               .Result;
            //.OrderBy(e => e.StartsAt);
            InsertHashedFingerprintsForTrack(hashedFingerprintsForSecondTrack.Audio, secondTrackData.TrackReference);

            const int thresholdVotes = 25;
            foreach (var hashedFingerprint in hashedFingerprintsForFirstTrack.Audio)
            {
                var subFingerprintData = subFingerprintDao.ReadSubFingerprints(
                    new[] { hashedFingerprint.HashBins }, new DefaultQueryConfiguration
                    {
                        ThresholdVotes = thresholdVotes,
                        YesMetaFieldsFilters = firstTrack.MetaFields
                    }).ToList();

                subFingerprintData.Count.Should().Be(1);
                firstTrackData.TrackReference.Get<ulong>().Should().Be(subFingerprintData[0].TrackReference.Get<ulong>());

                subFingerprintData = subFingerprintDao.ReadSubFingerprints(
                    new[] { hashedFingerprint.HashBins }, new DefaultQueryConfiguration
                    {
                        ThresholdVotes = thresholdVotes,
                        YesMetaFieldsFilters = secondTrack.MetaFields
                    }).ToList();

                subFingerprintData.Count.Should().Be(1);
                secondTrackData.TrackReference.Get<ulong>().Should().Be(subFingerprintData[0].TrackReference.Get<ulong>());

                subFingerprintData = subFingerprintDao.ReadSubFingerprints(
                    new[] { hashedFingerprint.HashBins }, new DefaultQueryConfiguration
                    {
                        ThresholdVotes = thresholdVotes
                    }).ToList();
                subFingerprintData.Count.Should().Be(2);
            }
        }

        [Fact]
        public void ReadHashDataByTrackTest()
        {
            var firstTrack = new TrackInfo("isrc1", "title", "artist");

            var firstTrackData = trackDao.InsertTrack(firstTrack, 200);

            var firstHashData = fingerprintCommandBuilder
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;

            InsertHashedFingerprintsForTrack(firstHashData.Audio, firstTrackData.TrackReference);

            var secondTrack = new TrackInfo("isrc2", "title", "artist");

            var secondTrackData = trackDao.InsertTrack(secondTrack, 200);

            var secondHashData = fingerprintCommandBuilder
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;

            InsertHashedFingerprintsForTrack(secondHashData.Audio, secondTrackData.TrackReference);

            var resultFirstHashData = subFingerprintDao.ReadHashedFingerprintsByTrackReference(firstTrackData.TrackReference)
                .Select(ToHashedFingerprint()).ToList();
            AssertHashDatasAreTheSame(firstHashData.Audio, resultFirstHashData);

            var resultSecondHashData = subFingerprintDao.ReadHashedFingerprintsByTrackReference(secondTrackData.TrackReference)
                .Select(ToHashedFingerprint()).ToList();
            AssertHashDatasAreTheSame(secondHashData.Audio, resultSecondHashData);
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
                subFingerprint.OriginalPoint);
        }
    }
}
