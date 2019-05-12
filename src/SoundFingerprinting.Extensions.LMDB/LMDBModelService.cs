using SoundFingerprinting.Extensions.LMDB.LMDBDatabase;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SoundFingerprinting.Extensions.LMDB.Tests")]

namespace SoundFingerprinting.Extensions.LMDB
{
    public sealed class LMDBModelService : ModelService, IDisposable
    {
        private readonly DatabaseContext databaseContext;

        public LMDBModelService(string pathToDatabase,
            LMDBConfiguration configuration = null
        ) : this(new DatabaseContext(pathToDatabase, configuration ?? new LMDBConfiguration()))

        {
        }

        private LMDBModelService(DatabaseContext databaseContext) : base(
            new TrackDao(databaseContext),
            new SubFingerprintDao(databaseContext)
        )
        {
            this.databaseContext = databaseContext;
        }

        public void CopyAndCompactLmdbDatabase(string newPath)
        {
            databaseContext.CopyAndCompactLmdbDatabase(newPath);
        }

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (disposedValue) return;
            databaseContext.Dispose();

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }

            disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~LMDBModelService()
        {
            Dispose(false);
        }

        #endregion IDisposable Support
    }
}