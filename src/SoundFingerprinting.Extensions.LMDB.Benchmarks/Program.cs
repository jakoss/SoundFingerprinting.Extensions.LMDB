using BenchmarkDotNet.Running;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.DAO.Data;
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

                    var track = new TrackData(filename, string.Empty, string.Empty, string.Empty, 2018, duration);
                    var lmdbTrackReference = lmdbModelService.InsertTrack(track);
                    var inMemoryTrackReference = inMemoryModelService.InsertTrack(track);

                    var hashedFingerprints = FingerprintCommandBuilder.Instance
                        .BuildFingerprintCommand()
                        .From(path)
                        .UsingServices(audioService)
                        .Hash()
                        .Result;

                    lmdbModelService.InsertHashDataForTrack(hashedFingerprints, lmdbTrackReference);
                    inMemoryModelService.InsertHashDataForTrack(hashedFingerprints, inMemoryTrackReference);
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