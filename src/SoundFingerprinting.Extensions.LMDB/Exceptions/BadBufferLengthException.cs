using System;
using System.Runtime.Serialization;

namespace SoundFingerprinting.Extensions.LMDB.Exceptions
{
    public class BadBufferLengthException : Exception
    {
        public BadBufferLengthException() : base("Cursor buffer length is incorrect (probably database is corrupted)")
        {
        }

        public BadBufferLengthException(string message) : base(message)
        {
        }

        public BadBufferLengthException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected BadBufferLengthException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}