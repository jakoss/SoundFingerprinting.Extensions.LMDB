using MessagePack;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;

namespace SoundFingerprinting.Extensions.LMDB.DTO
{
    [MessagePackObject]
    public class SubFingerprintDataDTO
    {
        public SubFingerprintDataDTO()
        {
        }

        internal SubFingerprintDataDTO(int[] hashes, uint sequenceNumber, float sequenceAt,
            IModelReference subFingerprintReference, IModelReference trackReference) : this()
        {
            Hashes = hashes;
            SubFingerprintReference = subFingerprintReference.Get<ulong>();
            TrackReference = trackReference.Get<ulong>();
            SequenceNumber = sequenceNumber;
            SequenceAt = sequenceAt;
        }

        internal SubFingerprintDataDTO(SubFingerprintData subFingerprintData)
        {
            Hashes = subFingerprintData.Hashes;
            SequenceNumber = subFingerprintData.SequenceNumber;
            SequenceAt = subFingerprintData.SequenceAt;
            SubFingerprintReference = subFingerprintData.SubFingerprintReference.Get<ulong>();
            TrackReference = subFingerprintData.TrackReference.Get<ulong>();
        }

        [Key(1)]
        public int[] Hashes { get; set; }

        [Key(2)]
        public uint SequenceNumber { get; set; }

        [Key(3)]
        public float SequenceAt { get; set; }

        [Key(5)]
        public ulong SubFingerprintReference { get; set; }

        [Key(6)]
        public ulong TrackReference { get; set; }

        internal SubFingerprintData ToSubFingerprintData()
        {
            return new SubFingerprintData(
                Hashes, SequenceNumber, SequenceAt,
                new ModelReference<ulong>(SubFingerprintReference),
                new ModelReference<ulong>(TrackReference)
            );
        }
    }
}
