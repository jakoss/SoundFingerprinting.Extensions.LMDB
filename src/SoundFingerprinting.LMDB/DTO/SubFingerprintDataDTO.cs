using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using System.Collections.Generic;
using ZeroFormatter;

namespace SoundFingerprinting.LMDB.DTO
{
    [ZeroFormattable]
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

        [Index(1)]
        public virtual int[] Hashes { get; set; }

        [Index(2)]
        public virtual uint SequenceNumber { get; set; }

        [Index(3)]
        public virtual float SequenceAt { get; set; }

        [Index(4)]
        public virtual IEnumerable<string> Clusters { get; set; }

        [Index(5)]
        public virtual ulong SubFingerprintReference { get; set; }

        [Index(6)]
        public virtual ulong TrackReference { get; set; }

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