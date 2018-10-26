using MessagePack;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;

namespace SoundFingerprinting.Extensions.LMDB.DTO
{
    [MessagePackObject]
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
        }

        public TrackDataDTO()
        {
            // this internal parameterless constructor is left here to allow datastorages that leverage reflection to instantiate objects
            // nontheless it is going to be removed in future versions
        }

        [Key(1)]
        public string Artist { get; set; }

        [Key(2)]
        public string Title { get; set; }

        [Key(3)]
        public string ISRC { get; set; }

        [Key(4)]
        public string Album { get; set; }

        [Key(5)]
        public int ReleaseYear { get; set; }

        [Key(6)]
        public double Length { get; set; }

        [Key(7)]
        public ulong TrackReference { get; set; }

        internal TrackData ToTrackData()
        {
            return new TrackData(ISRC, Artist, Title, Album, ReleaseYear, Length, new ModelReference<ulong>(TrackReference));
        }
    }
}