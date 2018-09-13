using Spreads.Buffers;
using System;

namespace SoundFingerprinting.LMDB
{
    internal static class Extensions
    {
        public static DirectBuffer GetDirectBuffer(this ulong value)
        {
            return new DirectBuffer(BitConverter.GetBytes(value));
        }

        public static DirectBuffer GetDirectBuffer(this int value)
        {
            return new DirectBuffer(BitConverter.GetBytes(value));
        }
    }
}