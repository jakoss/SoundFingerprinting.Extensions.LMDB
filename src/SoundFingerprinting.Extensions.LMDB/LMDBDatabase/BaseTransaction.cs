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

        protected TrackDataDTO GetTrackById(ref DirectBuffer id, object transaction)
        {
            if (transaction is Transaction tx)
            {
                if (databasesHolder.TracksDatabase.TryGet(tx, ref id, out DirectBuffer value))
                {
                    return ZeroFormatterSerializer.Deserialize<TrackDataDTO>(value.Span.ToArray());
                }
                else
                {
                    return null;
                }
            }
            else if (transaction is Spreads.LMDB.ReadOnlyTransaction rotx)
            {
                if (databasesHolder.TracksDatabase.TryGet(rotx, ref id, out DirectBuffer value))
                {
                    return ZeroFormatterSerializer.Deserialize<TrackDataDTO>(value.Span.ToArray());
                }
                else
                {
                    return null;
                }
            }
            else
            {
                throw new ArgumentException("Not an Transaction object", nameof(transaction));
            }
        }

        protected SubFingerprintDataDTO GetSubFingerprint(ulong id, object transaction)
        {
            var subFingerprintKey = BitConverter.GetBytes(id).AsMemory();
            var value = default(DirectBuffer);
            var result = false;
            using (subFingerprintKey.Pin())
            {
                var keyBuffer = new DirectBuffer(subFingerprintKey.Span);
                if (transaction is Transaction tx)
                {
                    result = databasesHolder.SubFingerprintsDatabase.TryGet(tx, ref keyBuffer, out value);
                }
                else if (transaction is Spreads.LMDB.ReadOnlyTransaction rotx)
                {
                    result = databasesHolder.SubFingerprintsDatabase.TryGet(rotx, ref keyBuffer, out value);
                }
                else
                {
                    throw new ArgumentException("Not an Transaction object", nameof(transaction));
                }
            }

            if (result)
            {
                return ZeroFormatterSerializer.Deserialize<SubFingerprintDataDTO>(value.Span.ToArray());
            }
            else
            {
                throw new KeyNotFoundException();
            }
        }

        protected List<SubFingerprintDataDTO> GetSubFingerprintsForTrack(ulong id, object transaction)
        {
            var trackKey = BitConverter.GetBytes(id).AsMemory();
            var list = new List<SubFingerprintDataDTO>();

            if (transaction is Transaction tx)
            {
                using (trackKey.Pin())
                using (var cursor = indexesHolder.TracksSubfingerprintsIndex.OpenCursor(tx))
                {
                    var keyBuffer = new DirectBuffer(trackKey.Span);
                    var valueBuffer = default(DirectBuffer);
                    if (cursor.TryGet(ref keyBuffer, ref valueBuffer, CursorGetOption.Set)
                        && cursor.TryGet(ref keyBuffer, ref valueBuffer, CursorGetOption.FirstDuplicate))
                    {
                        var subFingerprintId = valueBuffer.ReadUInt64(0);
                        list.Add(GetSubFingerprint(subFingerprintId, tx));

                        while (cursor.TryGet(ref keyBuffer, ref valueBuffer, CursorGetOption.NextDuplicate))
                        {
                            subFingerprintId = valueBuffer.ReadUInt64(0);
                            list.Add(GetSubFingerprint(subFingerprintId, tx));
                        }
                    }
                }
            }
            else if (transaction is Spreads.LMDB.ReadOnlyTransaction rotx)
            {
                using (trackKey.Pin())
                using (var cursor = indexesHolder.TracksSubfingerprintsIndex.OpenReadOnlyCursor(rotx))
                {
                    var keyBuffer = new DirectBuffer(trackKey.Span);
                    var valueBuffer = default(DirectBuffer);
                    if (cursor.TryGet(ref keyBuffer, ref valueBuffer, CursorGetOption.Set)
                        && cursor.TryGet(ref keyBuffer, ref valueBuffer, CursorGetOption.FirstDuplicate))
                    {
                        var subFingerprintId = valueBuffer.ReadUInt64(0);
                        list.Add(GetSubFingerprint(subFingerprintId, rotx));

                        while (cursor.TryGet(ref keyBuffer, ref valueBuffer, CursorGetOption.NextDuplicate))
                        {
                            subFingerprintId = valueBuffer.ReadUInt64(0);
                            list.Add(GetSubFingerprint(subFingerprintId, rotx));
                        }
                    }
                }
            }
            else
            {
                throw new ArgumentException("Not an Transaction object", nameof(transaction));
            }
            return list;
        }

        protected Span<ulong> GetSubFingerprintsByHashTableAndHash(int table, int hash, object transaction)
        {
            var tableDatabase = databasesHolder.HashTables[table];
            var key = hash;
            var value = default(ulong);

            ulong[] buffer = null;

            if (transaction is Transaction tx)
            {
                using (var cursor = tableDatabase.OpenCursor(tx))
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
            }
            else if (transaction is Spreads.LMDB.ReadOnlyTransaction rotx)
            {
                using (var cursor = tableDatabase.OpenReadOnlyCursor(rotx))
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
            }
            else
            {
                throw new ArgumentException("Not an Transaction object", nameof(transaction));
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