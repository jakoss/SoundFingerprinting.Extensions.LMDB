namespace SoundFingerprinting.LMDB.Tests.Integration
{
    using Audio;
    using Data;
    using FluentAssertions;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization.Formatters.Binary;

    public abstract class IntegrationWithSampleFilesTest : AbstractTest
    {
        protected readonly string PathToSamples = Path.Combine(Path.GetDirectoryName(typeof(IntegrationWithSampleFilesTest).Assembly.Location), "TestEnvironment", "chopinsamples.bin");
        protected readonly string PathToWav = Path.Combine(Path.GetDirectoryName(typeof(IntegrationWithSampleFilesTest).Assembly.Location), "TestEnvironment", "chopin_short.wav");

        private readonly object locker = new object();

        protected void AssertHashDatasAreTheSame(IList<HashedFingerprint> firstHashDatas, IList<HashedFingerprint> secondHashDatas)
        {
            firstHashDatas.Count.Should().Be(secondHashDatas.Count);

            // hashes are not ordered as parallel computation is involved
            firstHashDatas = SortHashesBySequenceNumber(firstHashDatas);
            secondHashDatas = SortHashesBySequenceNumber(secondHashDatas);

            for (int i = 0; i < firstHashDatas.Count; i++)
            {
                firstHashDatas[i].SequenceNumber.Should().Be(secondHashDatas[i].SequenceNumber);
                firstHashDatas[i].StartsAt.Should().BeApproximately(secondHashDatas[i].StartsAt, (float)Epsilon);

                firstHashDatas[i].HashBins.Should().BeEquivalentTo(secondHashDatas[i].HashBins);
            }
        }

        protected TagInfo GetTagInfo()
        {
            return new TagInfo
            {
                Album = "Album",
                AlbumArtist = "AlbumArtist",
                Artist = "Chopin",
                Composer = "Composer",
                Duration = 10.0d,
                Genre = "Genre",
                IsEmpty = false,
                ISRC = "ISRC",
                Title = "Nocture",
                Year = 1857
            };
        }

        protected AudioSamples GetAudioSamples()
        {
            lock (locker)
            {
                var serializer = new BinaryFormatter();
                using (Stream stream = new FileStream(PathToSamples, FileMode.Open, FileAccess.Read))
                {
                    return (AudioSamples)serializer.Deserialize(stream);
                }
            }
        }

        private List<HashedFingerprint> SortHashesBySequenceNumber(IEnumerable<HashedFingerprint> hashDatasFromFile)
        {
            return hashDatasFromFile.OrderBy(hashData => hashData.SequenceNumber).ToList();
        }
    }
}