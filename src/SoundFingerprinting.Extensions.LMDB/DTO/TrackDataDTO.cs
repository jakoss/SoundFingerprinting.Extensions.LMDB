using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using System.Collections.Generic;
using ZeroFormatter;

namespace SoundFingerprinting.Extensions.LMDB.DTO
{
    [ZeroFormattable]
    public class TrackDataDTO
    {
        internal TrackDataDTO(TrackData trackData, IModelReference modelReference)
        {
            ISRC = trackData.ISRC;
            Artist = trackData.Artist;
            Title = trackData.Title;
            Album = trackData.Album;
            ReleaseYear = trackData.ReleaseYear;
            Length = trackData.Length;
            TrackReference = (ulong)modelReference.Id;
            Subfingerprints = new HashSet<ulong>();
        }

        public TrackDataDTO()
        {
            // this internal parameterless constructor is left here to allow datastorages that leverage reflection to instantiate objects
            // nontheless it is going to be removed in future versions
        }

        [Index(1)]
        public virtual string Artist { get; internal set; }

        [Index(2)]
        public virtual string Title { get; internal set; }

        [Index(3)]
        public virtual string ISRC { get; internal set; }

        [Index(4)]
        public virtual string Album { get; internal set; }

        [Index(5)]
        public virtual int ReleaseYear { get; internal set; }

        [Index(6)]
        public virtual double Length { get; internal set; }

        [Index(7)]
        public virtual ulong TrackReference { get; internal set; }

        [Index(8)]
        public virtual HashSet<ulong> Subfingerprints { get; internal set; }

        internal TrackData ToTrackData()
        {
            return new TrackData(ISRC, Artist, Title, Album, ReleaseYear, Length, new ModelReference<ulong>(TrackReference));
        }
    }
}