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
            Add(MemoryDiagnoser.Default);
            Add(Job.Default.With(CsProjCoreToolchain.NetCoreApp21).With(Platform.X64));
            /*
            Add(Job.Default.With(CsProjCoreToolchain.From(
                new BenchmarkDotNet.Toolchains.DotNetCli.NetCoreAppSettings(
                    targetFrameworkMoniker: "netcoreapp2.1",
                    runtimeFrameworkVersion: "2.1.4",
                    name: ".NET Core 2.1"
                    ))));
            */
            Add(Job.Default.With(CsProjClassicNetToolchain.Net47).With(Platform.X64));

            Add(HtmlExporter.Default);
            Add(MarkdownExporter.GitHub);
        }
    }
}