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
        public static string databasesPath = @"C:\Users\Jakub\Desktop\test_refs\databases";

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

                    var duration = audioService.GetLengthInSeconds(path);

                    Console.WriteLine($"Adding track {filename} ({duration:0.00})");

                    var track = new TrackInfo(filename, string.Empty, string.Empty, duration);

                    var hashedFingerprints = FingerprintCommandBuilder.Instance
                        .BuildFingerprintCommand()
                        .From(path)
                        .UsingServices(audioService)
                        .Hash()
                        .Result;

                    var lmdbTrackReference = lmdbModelService.Insert(track, hashedFingerprints);
                    var inMemoryTrackReference = inMemoryModelService.Insert(track, hashedFingerprints);
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