using FluentAssertions;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
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
            var track = new TrackData("isrc", "artist", "title", "album", 1986, 200);
            var trackReference = trackDao.InsertTrack(track);
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

            InsertHashedFingerprintsForTrack(hashedFingerprints, trackReference);

#pragma warning disable CS0612 // Type or member is obsolete
            var hashedFingerprintss = subFingerprintDao.ReadHashedFingerprintsByTrackReference(trackReference);
#pragma warning restore CS0612 // Type or member is obsolete
            NumberOfHashBins.Should().Be(hashedFingerprintss.Count);
            foreach (var hashedFingerprint in hashedFingerprintss)
            {
                genericHashBuckets.Should().BeEquivalentTo(hashedFingerprint.HashBins);
            }
        }

        [Fact]
        public void SameNumberOfHashBinsIsInsertedInAllTablesWhenFingerprintingEntireSongTest()
        {
            var track = new TrackData(GetTagInfo(1));
            var trackReference = trackDao.InsertTrack(track);
            var hashedFingerprints = fingerprintCommandBuilder
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;

            InsertHashedFingerprintsForTrack(hashedFingerprints, trackReference);

#pragma warning disable CS0612 // Type or member is obsolete
            var hashes = subFingerprintDao.ReadHashedFingerprintsByTrackReference(trackReference);
#pragma warning restore CS0612 // Type or member is obsolete
            hashedFingerprints.Count.Should().Be(hashes.Count);
            foreach (var data in hashes)
            {
                data.HashBins.Length.Should().Be(25);
            }
        }

        [Fact]
        public void ReadByTrackGroupIdWorksAsExpectedTest()
        {
            TrackData firstTrack = new TrackData(GetTagInfo(1));
            TrackData secondTrack = new TrackData(GetTagInfo(2));

            var firstTrackReference = trackDao.InsertTrack(firstTrack);
            var secondTrackReference = trackDao.InsertTrack(secondTrack);

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
                .Result
                .OrderBy(e => e.StartsAt);

            InsertHashedFingerprintsForTrack(hashedFingerprintsForFirstTrack, firstTrackReference);

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
               .Result
               .OrderBy(e => e.StartsAt);
            InsertHashedFingerprintsForTrack(hashedFingerprintsForSecondTrack, secondTrackReference);

            var metaFields = new Dictionary<string, string>();

            const int ThresholdVotes = 25;
            foreach (var hashedFingerprint in hashedFingerprintsForFirstTrack)
            {
                var subFingerprintData = subFingerprintDao.ReadSubFingerprints(
                    new[] { hashedFingerprint.HashBins }, ThresholdVotes, new[] { "first-group-id" }, metaFields).ToList();

                subFingerprintData.Count.Should().Be(1);
                firstTrackReference.Should().BeEquivalentTo(subFingerprintData[0].TrackReference);

                subFingerprintData = subFingerprintDao.ReadSubFingerprints(
                    new[] { hashedFingerprint.HashBins }, ThresholdVotes, new[] { "second-group-id" }, metaFields).ToList();

                subFingerprintData.Count.Should().Be(1);
                secondTrackReference.Should().BeEquivalentTo(subFingerprintData[0].TrackReference);

                subFingerprintData = subFingerprintDao.ReadSubFingerprints(
                    new[] { hashedFingerprint.HashBins }, ThresholdVotes, Enumerable.Empty<string>(), metaFields).ToList();
                subFingerprintData.Count.Should().Be(2);
            }
        }

        [Fact]
        public void ReadHashDataByTrackTest()
        {
            var firstTrack = new TrackData("isrc1", "artist", "title", "album", 2012, 200);

            var firstTrackReference = trackDao.InsertTrack(firstTrack);

            var firstHashData = fingerprintCommandBuilder
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;

            InsertHashedFingerprintsForTrack(firstHashData, firstTrackReference);

            var secondTrack = new TrackData("isrc2", "artist", "title", "album", 2012, 200);

            var secondTrackReference = trackDao.InsertTrack(secondTrack);

            var secondHashData = fingerprintCommandBuilder
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;

            InsertHashedFingerprintsForTrack(secondHashData, secondTrackReference);

#pragma warning disable CS0612 // Type or member is obsolete
            var resultFirstHashData = subFingerprintDao.ReadHashedFingerprintsByTrackReference(firstTrackReference);
            AssertHashDatasAreTheSame(firstHashData, resultFirstHashData);

            var resultSecondHashData = subFingerprintDao.ReadHashedFingerprintsByTrackReference(secondTrackReference);
            AssertHashDatasAreTheSame(secondHashData, resultSecondHashData);
#pragma warning restore CS0612 // Type or member is obsolete
        }

        private void InsertHashedFingerprintsForTrack(IEnumerable<HashedFingerprint> hashedFingerprints, IModelReference trackReference)
        {
            subFingerprintDao.InsertHashDataForTrack(hashedFingerprints, trackReference);
        }
    }
}