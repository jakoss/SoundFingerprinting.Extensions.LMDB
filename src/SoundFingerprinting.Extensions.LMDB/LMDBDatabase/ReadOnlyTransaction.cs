using MessagePack;
using SoundFingerprinting.Extensions.LMDB.DTO;
using Spreads.Buffers;
using Spreads.LMDB;
using System;
using System.Collections.Generic;
using System.Text;

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

        public List<SubFingerprintDataDTO> GetSubFingerprintsForTrack(ulong id)
        {
            return GetSubFingerprintsForTrack(id, tx);
        }

        public Span<ulong> GetSubFingerprintsByHashTableAndHash(int table, int hash)
        {
            return GetSubFingerprintsByHashTableAndHash(table, hash, tx);
        }

        public IEnumerable<TrackDataDTO> GetAllTracks()
        {
            var list = new List<TrackDataDTO>();
            foreach (var item in databasesHolder.TracksDatabase.AsEnumerable(tx))
            {
                var key = item.Key.ReadUInt64(0);
                var trackData = LZ4MessagePackSerializer.Deserialize<TrackDataDTO>(item.Value.Span.ToArray());
                list.Add(trackData);
            }
            return list;
        }

        public IEnumerable<TrackDataDTO> GetTracksByArtistAndTitleName(string artist, string title)
        {
            var titleArtistKey = Encoding.UTF8.GetBytes(title + artist).AsMemory();
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

            using (titleArtistKey.Pin())
            {
                using (var cursor = indexesHolder.TitleArtistIndex.OpenReadOnlyCursor(tx))
                {
                    var keyBuffer = new DirectBuffer(titleArtistKey.Span);
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
            }
            return list;
        }

        public TrackDataDTO GetTrackByISRC(string isrc)
        {
            var isrcKey = Encoding.UTF8.GetBytes(isrc).AsMemory();
            using (isrcKey.Pin())
            {
                var keyBuffer = new DirectBuffer(isrcKey.Span);
                if (indexesHolder.IsrcIndex.TryGet(tx, ref keyBuffer, out DirectBuffer valueBuffer))
                {
                    var trackId = valueBuffer.ReadUInt64(0);
                    var trackKey = BitConverter.GetBytes(trackId).AsMemory();
                    using (trackKey.Pin())
                    {
                        var trackKeyBuffer = new DirectBuffer(trackKey.Span);
                        return GetTrackById(ref trackKeyBuffer, tx);
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        public TrackDataDTO GetTrackById(ulong id)
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