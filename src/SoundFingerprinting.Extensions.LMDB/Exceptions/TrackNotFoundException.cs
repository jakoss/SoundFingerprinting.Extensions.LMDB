using System;
using System.Runtime.Serialization;

namespace SoundFingerprinting.Extensions.LMDB.Exceptions
{
    public class TrackNotFoundException : Exception
    {
        public ulong TrackId { get; }

        public TrackNotFoundException(ulong trackId) : base($"Track {trackId} is not found")
        {
            TrackId = trackId;
        }

        public TrackNotFoundException(string message) : base(message)
        {
        }

        public TrackNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TrackNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}