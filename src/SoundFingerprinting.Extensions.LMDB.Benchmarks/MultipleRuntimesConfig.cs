using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.CsProj;

namespace SoundFingerprinting.Extensions.LMDB.Benchmarks
{
    public class MultipleRuntimesConfig : ManualConfig
    {
        public MultipleRuntimesConfig()
        {
            AddDiagnoser(MemoryDiagnoser.Default);
            AddJob(Job.Default.WithToolchain(CsProjCoreToolchain.NetCoreApp31).WithPlatform(Platform.X64));
            AddJob(Job.Default.WithToolchain(CsProjClassicNetToolchain.Net48).WithPlatform(Platform.X64));

            AddExporter(HtmlExporter.Default);
            AddExporter(MarkdownExporter.GitHub);
            AddExporter(RPlotExporter.Default);
        }
    }
}