using SoundFingerprinting.Extensions.LMDB.DTO;
using Spreads.Buffers;
using Spreads.LMDB;
using System;
using System.Collections.Generic;
using ZeroFormatter;

namespace SoundFingerprinting.Extensions.LMDB.LMDBDatabase
{
    internal class BaseTransaction
    {
        private readonly DatabasesHolder databasesHolder;
        private readonly IndexesHolder indexesHolder;

        public BaseTransaction(DatabasesHolder databasesHolder, IndexesHolder indexesHolder)
        {
            this.databasesHolder = databasesHolder;
            this.indexesHolder = indexesHolder;
        }

        protected TrackDataDTO GetTrackById(ref DirectBuffer id, Spreads.LMDB.ReadOnlyTransaction transaction)
        {
            if (databasesHolder.TracksDatabase.TryGet(transaction, ref id, out DirectBuffer value))
            {
                return ZeroFormatterSerializer.Deserialize<TrackDataDTO>(value.Span.ToArray());
            }
            else
            {
                return null;
            }
        }

        protected SubFingerprintDataDTO GetSubFingerprint(ulong id, Spreads.LMDB.ReadOnlyTransaction transaction)
        {
            var subFingerprintKey = BitConverter.GetBytes(id).AsMemory();
            var value = default(DirectBuffer);
            using (subFingerprintKey.Pin())
            {
                var keyBuffer = new DirectBuffer(subFingerprintKey.Span);
                if (databasesHolder.SubFingerprintsDatabase.TryGet(transaction, ref keyBuffer, out value))
                {
                    return ZeroFormatterSerializer.Deserialize<SubFingerprintDataDTO>(value.Span.ToArray());
                }
                else
                {
                    throw new KeyNotFoundException();
                }
            }
        }

        protected List<SubFingerprintDataDTO> GetSubFingerprintsForTrack(ulong id, Spreads.LMDB.ReadOnlyTransaction transaction)
        {
            var trackKey = BitConverter.GetBytes(id).AsMemory();
            var list = new List<SubFingerprintDataDTO>();

            using (trackKey.Pin())
            using (var cursor = indexesHolder.TracksSubfingerprintsIndex.OpenReadOnlyCursor(transaction))
            {
                var keyBuffer = new DirectBuffer(trackKey.Span);
                var valueBuffer = default(DirectBuffer);
                if (cursor.TryGet(ref keyBuffer, ref valueBuffer, CursorGetOption.Set)
                    && cursor.TryGet(ref keyBuffer, ref valueBuffer, CursorGetOption.FirstDuplicate))
                {
                    var subFingerprintId = valueBuffer.ReadUInt64(0);
                    list.Add(GetSubFingerprint(subFingerprintId, transaction));

                    while (cursor.TryGet(ref keyBuffer, ref valueBuffer, CursorGetOption.NextDuplicate))
                    {
                        subFingerprintId = valueBuffer.ReadUInt64(0);
                        list.Add(GetSubFingerprint(subFingerprintId, transaction));
                    }
                }
            }
            return list;
        }

        protected Span<ulong> GetSubFingerprintsByHashTableAndHash(int table, int hash, Spreads.LMDB.ReadOnlyTransaction transaction)
        {
            var tableDatabase = databasesHolder.HashTables[table];
            var key = hash;
            var value = default(ulong);

            ulong[] buffer = null;

            using (var cursor = tableDatabase.OpenReadOnlyCursor(transaction))
            {
                if (cursor.TryGet(ref key, ref value, CursorGetOption.Set)
                    && cursor.TryGet(ref key, ref value, CursorGetOption.FirstDuplicate))
                {
                    var counter = 0;
                    buffer = new ulong[cursor.Count()];
                    buffer[counter] = value;

                    while (cursor.TryGet(ref key, ref value, CursorGetOption.NextDuplicate))
                    {
                        counter++;
                        buffer[counter] = value;
                    }

                    if (counter != (buffer.Length - 1))
                    {
                        throw new Exception("Bad buffer length");
                    }
                }
            }

#pragma warning disable RCS1084 // Use coalesce expression instead of conditional expression.
#pragma warning disable IDE0029 // Use coalesce expression
            return buffer != null ? buffer : new Span<ulong>();
#pragma warning restore IDE0029 // Use coalesce expression
#pragma warning restore RCS1084 // Use coalesce expression instead of conditional expression.

            // FIXME : probably Visual Studio bug
            // return buffer ?? new Span<ulong>();
        }
    }
}