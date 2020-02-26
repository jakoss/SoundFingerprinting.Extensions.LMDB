using MessagePack;
using SoundFingerprinting.Extensions.LMDB.DTO;
using Spreads.Buffers;
using Spreads.LMDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
// ReSharper disable ImpureMethodCallOnReadonlyValueField

namespace SoundFingerprinting.Extensions.LMDB.LMDBDatabase
{
    internal class ReadOnlyTransaction : BaseTransaction, IDisposable
    {
        private readonly Spreads.LMDB.ReadOnlyTransaction tx;
        private readonly DatabasesHolder databasesHolder;
        private readonly IndexesHolder indexesHolder;

        public ReadOnlyTransaction(Spreads.LMDB.ReadOnlyTransaction tx, DatabasesHolder databasesHolder, IndexesHolder indexesHolder)
            : base(databasesHolder, indexesHolder)
        {
            this.tx = tx;
            this.databasesHolder = databasesHolder;
            this.indexesHolder = indexesHolder;
        }

        public void Dispose()
        {
            tx.Dispose();
        }

        public SubFingerprintDataDTO GetSubFingerprint(ulong id)
        {
            return GetSubFingerprint(id, tx);
        }

        public IEnumerable<SubFingerprintDataDTO> GetSubFingerprintsForTrack(ulong id)
        {
            return GetSubFingerprintsForTrack(id, tx);
        }

        public Span<ulong> GetSubFingerprintsByHashTableAndHash(int table, int hash)
        {
            return GetSubFingerprintsByHashTableAndHash(table, hash, tx);
        }

        public int GetSubFingerprintsCount()
        {
            using var cursor = databasesHolder.SubFingerprintsDatabase.OpenReadOnlyCursor(tx);
            return (int)cursor.Count();
        }

        public int GetTracksCount()
        {
            using var cursor = databasesHolder.TracksDatabase.OpenReadOnlyCursor(tx);
            return (int)cursor.Count();
        }

        public IEnumerable<int> GetHashCountsPerTable()
        {
            var counters = new List<int>();
            foreach (var table in databasesHolder.HashTables)
            {
                using var cursor = table.OpenReadOnlyCursor(tx);
                counters.Add((int)cursor.Count());
            }
            return counters;
        }

        public IEnumerable<TrackDataDTO> GetAllTracks()
        {
            return databasesHolder.TracksDatabase.AsEnumerable(tx)
                .Select(item => MessagePackSerializer.Deserialize<TrackDataDTO>(item.Value.Span.ToArray(), options: serializerOptions));
        }

        public IEnumerable<TrackDataDTO> GetTracksByTitle(string title)
        {
            var titleKey = Encoding.UTF8.GetBytes(title).AsMemory();
            var list = new List<TrackDataDTO>();

            void GetTrack(ref DirectBuffer directBuffer)
            {
                var trackId = directBuffer.ReadUInt64(0);
                var key = BitConverter.GetBytes(trackId).AsMemory();
                using (key.Pin())
                {
                    var keyBuffer = new DirectBuffer(key.Span);
                    list.Add(GetTrackById(ref keyBuffer, tx));
                }
            }

            using (titleKey.Pin())
            {
                using var cursor = indexesHolder.TitleIndex.OpenReadOnlyCursor(tx);
                var keyBuffer = new DirectBuffer(titleKey.Span);
                var valueBuffer = default(DirectBuffer);
                if (cursor.TryGet(ref keyBuffer, ref valueBuffer, CursorGetOption.Set)
                    && cursor.TryGet(ref keyBuffer, ref valueBuffer, CursorGetOption.FirstDuplicate))
                {
                    GetTrack(ref valueBuffer);

                    while (cursor.TryGet(ref keyBuffer, ref valueBuffer, CursorGetOption.NextDuplicate))
                    {
                        GetTrack(ref valueBuffer);
                    }
                }
            }
            return list;
        }

        public TrackDataDTO GetTrackById(string id)
        {
            var idKey = Encoding.UTF8.GetBytes(id).AsMemory();
            using (idKey.Pin())
            {
                var keyBuffer = new DirectBuffer(idKey.Span);
                if (indexesHolder.IdIndex.TryGet(tx, ref keyBuffer, out var valueBuffer))
                {
                    var trackId = valueBuffer.ReadUInt64(0);
                    var trackKey = BitConverter.GetBytes(trackId).AsMemory();
                    using (trackKey.Pin())
                    {
                        var trackKeyBuffer = new DirectBuffer(trackKey.Span);
                        return GetTrackById(ref trackKeyBuffer, tx);
                    }
                }

                return null;
            }
        }

        public TrackDataDTO GetTrackByReference(ulong id)
        {
            var trackKey = BitConverter.GetBytes(id).AsMemory();
            using (trackKey.Pin())
            {
                var keyBuffer = new DirectBuffer(trackKey.Span);
                return GetTrackById(ref keyBuffer, tx);
            }
        }
    }
}