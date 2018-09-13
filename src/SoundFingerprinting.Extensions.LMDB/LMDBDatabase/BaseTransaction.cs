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

        public BaseTransaction(DatabasesHolder databasesHolder)
        {
            this.databasesHolder = databasesHolder;
        }

        public TrackDataDTO GetTrackById(ulong id)
        {
            if (databasesHolder.Tracks.TryGetValue(id, out var trackData))
            {
                return trackData;
            }
            else
            {
                return null;
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