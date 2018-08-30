using BenchmarkDotNet.Running;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.InMemory;
using System;
using System.IO;

namespace SoundFingerprinting.LMDB.Benchmarks
{
    internal static class Program
    {
        private static void Main()
        {
            // Create databases
            Console.WriteLine("Building databases");
            var audioService = new SoundFingerprintingAudioService();

            var lmdbModelService = new LMDBModelService("db");
            var inMemoryModelService = new InMemoryModelService();
            foreach (var path in Directory.EnumerateFiles(RecognitionsBenchmark.refs_path, "*.wav"))
            {
                var filename = Path.GetFileNameWithoutExtension(path);

                var duration = audioService.GetLengthInSeconds(path);

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
            inMemoryModelService.Snapshot("memory.db");
            lmdbModelService.Dispose();
            Console.WriteLine("Databases built");

            // Run benchmarks
            var summary = BenchmarkRunner.Run<RecognitionsBenchmark>();

            // Remove databases
            Console.WriteLine("Removing databases");
            Directory.Delete("db", true);
            File.Delete("memory.db");
            Console.WriteLine("Databases removed");
        }
    }
}