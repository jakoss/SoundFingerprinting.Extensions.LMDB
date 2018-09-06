using SoundFingerprinting.LMDB.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SoundFingerprinting.LMDB.LMDBDatabase
{
    internal class ReadOnlyTransaction : BaseTransaction, IDisposable
    {
        private readonly Spreads.LMDB.ReadOnlyTransaction tx;
        private readonly DatabasesHolder databasesHolder;

        public ReadOnlyTransaction(Spreads.LMDB.ReadOnlyTransaction tx, DatabasesHolder databasesHolder)
            : base(databasesHolder)
        {
            this.tx = tx;
            this.databasesHolder = databasesHolder;
        }

        public void Dispose()
        {
            tx.Dispose();
        }

        public SubFingerprintDataDTO GetSubFingerprint(ulong id)
        {
            return GetSubFingerprint(id, tx);
        }

        public Span<ulong> GetSubFingerprintsByHashTableAndHash(int table, int hash)
        {
            return GetSubFingerprintsByHashTableAndHash(table, hash, tx);
        }

        public IEnumerable<TrackDataDTO> GetAllTracks()
        {
            return databasesHolder.Tracks.Values;
        }

        public IEnumerable<TrackDataDTO> GetTracksByArtistAndTitleName(string artist, string title)
        {
            return databasesHolder.Tracks.Where(pair => pair.Value.Artist == artist && pair.Value.Title == title).Select(e => e.Value);
        }

        public TrackDataDTO GetTrackByISRC(string isrc)
        {
            return databasesHolder.Tracks.Select(e => e.Value).FirstOrDefault(value => value.ISRC == isrc);
        }
    }
}