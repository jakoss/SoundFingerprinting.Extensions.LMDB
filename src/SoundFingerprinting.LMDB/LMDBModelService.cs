using SoundFingerprinting.LMDB.LMDBDatabase;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SoundFingerprinting.LMDB.Tests")]

namespace SoundFingerprinting.LMDB
{
    public class LMDBModelService : ModelService, IDisposable
    {
        private readonly DatabaseContext databaseContext;

        // Default map size is 10 GB
        public LMDBModelService(string pathToDatabase, long mapSize = (1024L * 1024L * 1024L * 10L)) : this(new DatabaseContext(pathToDatabase, mapSize))

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

        private bool disposedValue = false; // To detect redundant calls

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