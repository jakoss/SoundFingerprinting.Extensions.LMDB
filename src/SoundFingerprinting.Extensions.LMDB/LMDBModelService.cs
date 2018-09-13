using SoundFingerprinting.Extensions.LMDB.LMDBDatabase;
using Spreads.LMDB;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SoundFingerprinting.Extensions.LMDB.Tests")]

namespace SoundFingerprinting.Extensions.LMDB
{
    public class LMDBModelService : ModelService, IDisposable
    {
        private readonly DatabaseContext databaseContext;

        // Default map size is 10 GB
        public LMDBModelService(string pathToDatabase,
            long mapSize = (1024L * 1024L * 1024L * 10L),
            DbEnvironmentFlags lmdbOpenFlags = DbEnvironmentFlags.None
        ) : this(new DatabaseContext(pathToDatabase, mapSize, lmdbOpenFlags))

        {
        }

        private LMDBModelService(DatabaseContext databaseContext) : base(
            new TrackDao(databaseContext),
            new SubFingerprintDao(databaseContext)
        )
        {
            this.databaseContext = databaseContext;
        }

        public override bool SupportsBatchedSubFingerprintQuery => true;

        #region IDisposable Support

        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                databaseContext.Dispose();

                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }

                disposedValue = true;
            }
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