using System;
using System.Runtime.Serialization;

namespace SoundFingerprinting.Extensions.LMDB.Exceptions
{
    public class BadRuntimeException : Exception
    {
        public BadRuntimeException() : base("You need to be running on x64 runtime")
        {
        }

        public BadRuntimeException(string message) : base(message)
        {
        }

        public BadRuntimeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected BadRuntimeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}