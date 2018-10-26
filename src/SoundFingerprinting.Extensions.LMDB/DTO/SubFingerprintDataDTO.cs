using MessagePack;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using System.Collections.Generic;

namespace SoundFingerprinting.Extensions.LMDB.DTO
{
    [MessagePackObject]
    public class SubFingerprintDataDTO
    {
        public SubFingerprintDataDTO()
        {
        }

        internal SubFingerprintDataDTO(int[] hashes, uint sequenceNumber, float sequenceAt,
            IModelReference subFingerprintReference, IModelReference trackReference,
            IEnumerable<string> clusters) : this()
        {
            Hashes = hashes;
            SubFingerprintReference = (ulong)subFingerprintReference.Id;
            TrackReference = (ulong)trackReference.Id;
            SequenceNumber = sequenceNumber;
            SequenceAt = sequenceAt;
            Clusters = clusters;
        }

        internal SubFingerprintDataDTO(SubFingerprintData subFingerprintData)
        {
            Hashes = subFingerprintData.Hashes;
            SequenceNumber = subFingerprintData.SequenceNumber;
            SequenceAt = subFingerprintData.SequenceAt;
            Clusters = subFingerprintData.Clusters;
            SubFingerprintReference = (ulong)subFingerprintData.SubFingerprintReference.Id;
            TrackReference = (ulong)subFingerprintData.TrackReference.Id;
        }

        [Key(1)]
        public int[] Hashes { get; set; }

        [Key(2)]
        public uint SequenceNumber { get; set; }

        [Key(3)]
        public float SequenceAt { get; set; }

        [Key(4)]
        public IEnumerable<string> Clusters { get; set; }

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