using Spreads.LMDB;
using System;

namespace SoundFingerprinting.Extensions.LMDB.LMDBDatabase
{
    internal class IndexesHolder : IDisposable
    {
        public IndexesHolder(Database isrcIndex, Database titleArtistIndex, Database tracksSubfingerprintsIndex)
        {
            IsrcIndex = isrcIndex;
            TitleArtistIndex = titleArtistIndex;
            TracksSubfingerprintsIndex = tracksSubfingerprintsIndex;
        }

        public Database IsrcIndex { get; }

        public Database TitleArtistIndex { get; }

        public Database TracksSubfingerprintsIndex { get; }

        public void Dispose()
        {
            IsrcIndex.Dispose();
            TitleArtistIndex.Dispose();
            TracksSubfingerprintsIndex.Dispose();
        }
    }
}