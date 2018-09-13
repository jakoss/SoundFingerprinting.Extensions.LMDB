namespace SoundFingerprinting.Extensions.LMDB.Tests
{
    using FluentAssertions;
    using SoundFingerprinting.DAO;
    using SoundFingerprinting.DAO.Data;

    public abstract class AbstractTest
    {
        protected const double Epsilon = 0.0001;

        protected const int SampleRate = 5512;

        protected const int NumberOfHashTables = 25;

        private readonly bool[] genericFingerprintArray =
            {
                true, false, true, false, true, false, true, false, true, false, true, false, false, true, false, true,
                false, true, false, true, false, true, false, true, true, false, true, false, true, false, true, false,
                true, false, true, false, false, true, false, true, false, true, false, true, false, true, false, true,
                true, false, true, false, true, false, true, false, true, false, true, false, false, true, false, true,
                false, true, false, true, false, true, false, true, true, false, true, false, true, false, true, false,
                true, false, true, false, false, true, false, true, false, true, false, true, false, true, false, true,
                true, false, true, false, true, false, true, false, true, false, true, false, false, true, false, true,
                false, true, false, true, false, true, false, true
            };

        private readonly int[] genericHashBucketsArray =
            {
                256, 770, 1284, 1798, 2312, 2826, 3340, 3854, 4368, 4882, 5396, 5910, 6424, 6938, 7452, 7966, 8480,
                9506, 10022, 10536, 11050, 11564, 12078, 12592, 13106
            };

        protected bool[] GenericFingerprint()
        {
            return (bool[])genericFingerprintArray.Clone();
        }

        protected int[] GenericHashBuckets()
        {
            return (int[])genericHashBucketsArray.Clone();
        }

        protected void AssertTracksAreEqual(TrackData expectedTrack, TrackData actualTrack)
        {
            // FIXME : https://github.com/AddictedCS/soundfingerprinting/issues/98
            // expectedTrack.TrackReference.Should().BeEquivalentTo(actualTrack.TrackReference);
            expectedTrack.Album.Should().BeEquivalentTo(actualTrack.Album);
            expectedTrack.Artist.Should().BeEquivalentTo(actualTrack.Artist);
            expectedTrack.Title.Should().BeEquivalentTo(actualTrack.Title);
            expectedTrack.Length.Should().Be(actualTrack.Length);
            expectedTrack.ISRC.Should().BeEquivalentTo(actualTrack.ISRC);
        }

        protected void AssertModelReferenceIsInitialized(IModelReference modelReference)
        {
            modelReference.Should().NotBeNull();
            modelReference.GetHashCode().Should().NotBe(0);
        }
    }
}