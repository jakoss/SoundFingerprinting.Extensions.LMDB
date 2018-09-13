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

            var hashedFingerprintss = subFingerprintDao.ReadHashedFingerprintsByTrackReference(trackReference);
            NumberOfHashBins.Should().Be(hashedFingerprintss.Count);
            foreach (var hashedFingerprint in hashedFingerprintss)
            {
                genericHashBuckets.Should().BeEquivalentTo(hashedFingerprint.HashBins);
            }
        }

        [Fact]
        public void SameNumberOfHashBinsIsInsertedInAllTablesWhenFingerprintingEntireSongTest()
        {
            var track = new TrackData(GetTagInfo());
            var trackReference = trackDao.InsertTrack(track);
            var hashedFingerprints = FingerprintCommandBuilder.Instance
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;

            InsertHashedFingerprintsForTrack(hashedFingerprints, trackReference);

            var hashes = subFingerprintDao.ReadHashedFingerprintsByTrackReference(trackReference);
            hashedFingerprints.Count.Should().Be(hashes.Count);
            foreach (var data in hashes)
            {
                data.HashBins.Length.Should().Be(25);
            }
        }

        [Fact]
        public void ReadByTrackGroupIdWorksAsExpectedTest()
        {
            TagInfo tagInfo = GetTagInfo();
            TrackData firstTrack = new TrackData(tagInfo);
            TrackData secondTrack = new TrackData(tagInfo);

            var firstTrackReference = trackDao.InsertTrack(firstTrack);
            var secondTrackReference = trackDao.InsertTrack(secondTrack);

            var hashedFingerprintsForFirstTrack = FingerprintCommandBuilder.Instance
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

            InsertHashedFingerprintsForTrack(hashedFingerprintsForFirstTrack, firstTrackReference);

            var hashedFingerprintsForSecondTrack = FingerprintCommandBuilder.Instance
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
            InsertHashedFingerprintsForTrack(hashedFingerprintsForSecondTrack, secondTrackReference);

            const int ThresholdVotes = 25;
            foreach (var hashedFingerprint in hashedFingerprintsForFirstTrack)
            {
                var subFingerprintData = subFingerprintDao.ReadSubFingerprints(new[] { hashedFingerprint.HashBins }, ThresholdVotes, new[] { "first-group-id" }).ToList();

                subFingerprintData.Count.Should().Be(1);
                firstTrackReference.Should().BeEquivalentTo(subFingerprintData[0].TrackReference);

                subFingerprintData = subFingerprintDao.ReadSubFingerprints(new[] { hashedFingerprint.HashBins }, ThresholdVotes, new[] { "second-group-id" }).ToList();

                subFingerprintData.Count.Should().Be(1);
                secondTrackReference.Should().BeEquivalentTo(subFingerprintData[0].TrackReference);

                subFingerprintData = subFingerprintDao.ReadSubFingerprints(new[] { hashedFingerprint.HashBins }, ThresholdVotes, Enumerable.Empty<string>()).ToList();
                subFingerprintData.Count.Should().Be(2);
            }
        }

        [Fact]
        public void ReadHashDataByTrackTest()
        {
            var firstTrack = new TrackData("isrc", "artist", "title", "album", 2012, 200);

            var firstTrackReference = trackDao.InsertTrack(firstTrack);

            var firstHashData = FingerprintCommandBuilder.Instance
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;

            InsertHashedFingerprintsForTrack(firstHashData, firstTrackReference);

            var secondTrack = new TrackData("isrc", "artist", "title", "album", 2012, 200);

            var secondTrackReference = trackDao.InsertTrack(secondTrack);

            var secondHashData = FingerprintCommandBuilder.Instance
                .BuildFingerprintCommand()
                .From(GetAudioSamples())
                .UsingServices(audioService)
                .Hash()
                .Result;

            InsertHashedFingerprintsForTrack(secondHashData, secondTrackReference);

            var resultFirstHashData = subFingerprintDao.ReadHashedFingerprintsByTrackReference(firstTrackReference);
            AssertHashDatasAreTheSame(firstHashData, resultFirstHashData);

            var resultSecondHashData = subFingerprintDao.ReadHashedFingerprintsByTrackReference(secondTrackReference);
            AssertHashDatasAreTheSame(secondHashData, resultSecondHashData);
        }

        private void InsertHashedFingerprintsForTrack(IEnumerable<HashedFingerprint> hashedFingerprints, IModelReference trackReference)
        {
            subFingerprintDao.InsertHashDataForTrack(hashedFingerprints, trackReference);
        }
    }
}