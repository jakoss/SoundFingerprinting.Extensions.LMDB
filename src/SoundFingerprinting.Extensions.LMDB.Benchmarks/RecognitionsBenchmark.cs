using BenchmarkDotNet.Attributes;
using SoundFingerprinting.Audio;
using SoundFingerprinting.Builder;
using SoundFingerprinting.InMemory;
using SoundFingerprinting.Query;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SoundFingerprinting.Extensions.LMDB.Benchmarks
{
    [Config(typeof(MultipleRuntimesConfig))]
    public class RecognitionsBenchmark
    {
        private LMDBModelService lmdbModelService;
        private InMemoryModelService inMemoryModelService;
        private IAudioService audioService;
        public const string refs_path = @"C:\Users\Jakub\Desktop\test_refs\waves";

        [GlobalSetup]
        public void Setup()
        {
            audioService = new SoundFingerprintingAudioService();

            lmdbModelService = new LMDBModelService(Path.Combine(Program.databasesPath, "db"));
            inMemoryModelService = new InMemoryModelService(Path.Combine(Program.databasesPath, "memory.db"));
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            lmdbModelService.Dispose();
        }

        [Benchmark(Baseline = true)]
        [ArgumentsSource(nameof(Files))]
        public QueryResult InMemoryRecognitions(string audioFile)
        {
            return QueryCommandBuilder.Instance.BuildQueryCommand()
                .From(Path.Combine(refs_path, audioFile))
                .UsingServices(inMemoryModelService, audioService)
                .Query()
                .Result;
        }

        [Benchmark]
        [ArgumentsSource(nameof(Files))]
        public QueryResult LMDBRecognitions(string audioFile)
        {
            return QueryCommandBuilder.Instance.BuildQueryCommand()
                .From(Path.Combine(refs_path, audioFile))
                .UsingServices(lmdbModelService, audioService)
                .Query()
                .Result;
        }

        public IEnumerable<string> Files()
        {
            return Directory.EnumerateFiles(refs_path, "*.wav").Select(Path.GetFileName);
        }
    }
}