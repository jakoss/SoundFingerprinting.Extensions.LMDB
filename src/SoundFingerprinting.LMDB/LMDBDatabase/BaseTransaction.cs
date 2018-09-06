using SoundFingerprinting.LMDB.DTO;
using Spreads.Buffers;
using Spreads.LMDB;
using System;
using System.Collections.Generic;
using ZeroFormatter;

namespace SoundFingerprinting.LMDB.LMDBDatabase
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
            var key = id.GetDirectBuffer();
            var value = default(DirectBuffer);
            var result = false;
            if (transaction is Transaction tx)
            {
                result = databasesHolder.SubFingerprintsDatabase.TryGet(tx, ref key, out value);
            }
            else if (transaction is Spreads.LMDB.ReadOnlyTransaction rotx)
            {
                result = databasesHolder.SubFingerprintsDatabase.TryGet(rotx, ref key, out value);
            }
            else
            {
                throw new ArgumentException("Not an Transaction object", nameof(transaction));
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
            var key = hash.GetDirectBuffer();
            var value = default(DirectBuffer);
            ulong[] buffer = null;
            /* FIXME : waiting for better api https://github.com/Spreads/Spreads.LMDB/issues/5
            if (transaction is Transaction tx)
            {
                using (var cursor = tableDatabase.OpenCursor(tx))
                {
                    result = cursor.TryGet(ref key, ref value, CursorGetOption.GetMultiple);
                }
            }
            else if (transaction is Spreads.LMDB.ReadOnlyTransaction rotx)
            {
                using (var cursor = tableDatabase.OpenReadOnlyCursor(rotx))
                {
                    result = cursor.TryGet(ref key, ref value, CursorGetOption.GetMultiple);
                }
            }
            else
            {
                throw new ArgumentException("Not an Transaction object", nameof(transaction));
            }
            */
            if (transaction is Transaction tx)
            {
                using (var cursor = tableDatabase.OpenCursor(tx))
                {
                    if (cursor.TryGet(ref key, ref value, CursorGetOption.First) && cursor.TryGet(ref key, ref value, CursorGetOption.FirstDuplicate))
                    {
                        var counter = 0;
                        buffer = new ulong[cursor.Count()];
                        buffer[counter] = value.ReadUInt64(0);

                        while (cursor.TryGet(ref key, ref value, CursorGetOption.NextDuplicate))
                        {
                            counter++;
                            buffer[counter] = value.ReadUInt64(0);
                            value = default;
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
                    if (cursor.TryGet(ref key, ref value, CursorGetOption.First) && cursor.TryGet(ref key, ref value, CursorGetOption.FirstDuplicate))
                    {
                        var counter = 0;
                        buffer = new ulong[cursor.Count()];
                        buffer[counter] = value.ReadUInt64(0);

                        while (cursor.TryGet(ref key, ref value, CursorGetOption.NextDuplicate))
                        {
                            counter++;
                            buffer[counter] = value.ReadUInt64(0);
                            value = default;
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

            return buffer != null ? buffer : new Span<ulong>();
        }
    }
}