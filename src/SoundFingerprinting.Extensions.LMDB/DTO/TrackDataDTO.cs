using MessagePack;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;
using System.Collections.Generic;

namespace SoundFingerprinting.Extensions.LMDB.DTO
{
    [MessagePackObject]
    public class TrackDataDTO
    {
        internal TrackDataDTO(TrackInfo trackInfo, double duration, IModelReference modelReference)
        {
            Id = trackInfo.Id;
            Artist = trackInfo.Artist;
            Title = trackInfo.Title;
            Length = duration;
            TrackReference = modelReference.Get<ulong>();
            MetaFields = trackInfo.MetaFields;
        }

        internal TrackDataDTO(TrackData trackData)
        {
            Id = trackData.Id;
            Artist = trackData.Artist;
            Title = trackData.Title;
            Length = trackData.Length;
            TrackReference = trackData.TrackReference.Get<ulong>();
            MetaFields = trackData.MetaFields;
        }

        public TrackDataDTO()
        {
            // this internal parameterless constructor is left here to allow datastorages that leverage reflection to instantiate objects
            // nontheless it is going to be removed in future versions
        }

        [Key(1)]
        public string Id { get; set; }

        [Key(2)]
        public string Artist { get; set; }

        [Key(3)]
        public string Title { get; set; }

        [Key(4)]
        public double Length { get; set; }

        [Key(5)]
        public ulong TrackReference { get; set; }

        [Key(6)]
        public IDictionary<string, string> MetaFields { get; set; }

        internal TrackData ToTrackData()
        {
            return new TrackData(Id, Artist, Title, Length, new ModelReference<ulong>(TrackReference), MetaFields, MediaType.Audio);
        }
    }
}