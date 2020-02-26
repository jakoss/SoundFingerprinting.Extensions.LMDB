namespace SoundFingerprinting.Extensions.LMDB.Tests.Integration
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

        protected void AssertHashDatasAreTheSame(Hashes firstHashDatas, IList<HashedFingerprint> secondHashDatas)
        {
            firstHashDatas.Count.Should().Be(secondHashDatas.Count);

            // hashes are not ordered as parallel computation is involved
            var localFirstHashDatas = SortHashesBySequenceNumber(firstHashDatas);
            var localSecondHashDatas = SortHashesBySequenceNumber(secondHashDatas);

            for (int i = 0; i < firstHashDatas.Count; i++)
            {
                localFirstHashDatas[i].SequenceNumber.Should().Be(localSecondHashDatas[i].SequenceNumber);
                localFirstHashDatas[i].StartsAt.Should().BeApproximately(localSecondHashDatas[i].StartsAt, (float)Epsilon);

                localFirstHashDatas[i].HashBins.Should().BeEquivalentTo(localSecondHashDatas[i].HashBins);
            }
        }

        protected static TagInfo GetTagInfo(int number)
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
                ISRC = $"ISRC{number}",
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