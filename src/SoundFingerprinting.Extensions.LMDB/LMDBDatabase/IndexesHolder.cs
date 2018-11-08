using Spreads.LMDB;
using System;

namespace SoundFingerprinting.Extensions.LMDB.LMDBDatabase
{
    internal class IndexesHolder : IDisposable
    {
        public IndexesHolder(Database idIndex, Database titleIndex, Database tracksSubfingerprintsIndex)
        {
            IdIndex = idIndex;
            TitleIndex = titleIndex;
            TracksSubfingerprintsIndex = tracksSubfingerprintsIndex;
        }

        public Database IdIndex { get; }

        public Database TitleIndex { get; }

        public Database TracksSubfingerprintsIndex { get; }

        public void Dispose()
        {
            IdIndex.Dispose();
            TitleIndex.Dispose();
            TracksSubfingerprintsIndex.Dispose();
        }
    }
}