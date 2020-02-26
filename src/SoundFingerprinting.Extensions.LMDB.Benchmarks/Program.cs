using BenchmarkDotNet.Running;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.Data;
using SoundFingerprinting.InMemory;
using System;
using System.IO;

namespace SoundFingerprinting.Extensions.LMDB.Benchmarks
{
    internal static class Program
    {
        public static string databasesPath = @"E:\RM\SoundFingerprintingLMDB";

        private static void Main()
        {
            if (!Directory.Exists(Path.Combine(databasesPath, "db")))
            {
                // Create databases
                Console.WriteLine("Building databases");
                var audioService = new SoundFingerprintingAudioService();

                var lmdbModelService = new LMDBModelService(Path.Combine(databasesPath, "db"));
                var inMemoryModelService = new InMemoryModelService();
                foreach (var path in Directory.EnumerateFiles(RecognitionsBenchmark.refs_path, "*.wav"))
                {
                    var filename = Path.GetFileNameWithoutExtension(path);

                    Console.WriteLine($"Adding track {filename}");

                    var track = new TrackInfo(filename, string.Empty, string.Empty);

                    var hashedFingerprints = FingerprintCommandBuilder.Instance
                        .BuildFingerprintCommand()
                        .From(path)
                        .UsingServices(audioService)
                        .Hash()
                        .Result;

                    lmdbModelService.Insert(track, hashedFingerprints);
                    inMemoryModelService.Insert(track, hashedFingerprints);
                }
                inMemoryModelService.Snapshot(Path.Combine(databasesPath, "memory.db"));
                lmdbModelService.Dispose();

                Console.WriteLine("Databases built");
            }

            // Run benchmarks
            var summary = BenchmarkRunner.Run<RecognitionsBenchmark>();
        }
    }
}