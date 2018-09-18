namespace SoundFingerprinting.Extensions.LMDB
{
    public class LMDBConfiguration
    {
        /// <summary>
        /// Set this to as much as you can. This is maximum size of your database.
        /// Disk size will be adjusted to used data. MapSize is just a maximum possible size!
        /// </summary>
        public long MapSize { get; set; } = (1024L * 1024L * 1024L * 10L);

        /// <summary>
        /// Set this to true if you want faster writes but without ACID guarantees (async disk flushes and no locks for writes)
        /// </summary>
        public bool UnsafeAsync { get; set; } = false;
    }
}