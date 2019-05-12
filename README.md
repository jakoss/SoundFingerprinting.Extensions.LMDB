# SoundFingerprinting.LMDB

[![Build status](https://dev.azure.com/NekroVision/SoundFingerprinting.Extensions.LMDB/_apis/build/status/SoundFingerprinting.Extensions.LMDB-CI)](https://dev.azure.com/NekroVision/SoundFingerprinting.Extensions.LMDB/_build/latest?definitionId=1) [![NuGet](https://img.shields.io/nuget/v/SoundFingerprinting.Extensions.LMDB.svg?style=flat-square)](https://www.nuget.org/packages/SoundFingerprinting.Extensions.LMDB) [![NuGet](https://img.shields.io/nuget/dt/SoundFingerprinting.Extensions.LMDB.svg?style=flat-square)](https://www.nuget.org/packages/SoundFingerprinting.Extensions.LMDB) [![GitHub issues](https://img.shields.io/github/issues/Nekromancer/SoundFingerprinting.Extensions.LMDB.svg?style=flat-square)](https://github.com/Nekromancer/SoundFingerprinting.Extensions.LMDB/issues) [![MIT License](http://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE) [![SonarCloud](https://sonarcloud.io/api/project_badges/measure?project=Nekromancer_SoundFingerprinting.Extensions.LMDB&metric=alert_status)](https://sonarcloud.io/dashboard?id=Nekromancer_SoundFingerprinting.Extensions.LMDB)



Database adapter for SoundFingerprinting algorithm using LMDB database. It's fast, it's persistent and safe from data violation.

## Usage

To get library simply install it from [Nuget](https://www.nuget.org/packages/SoundFingerprinting.Extensions.LMDB):

```
Install-Package SoundFingerprinting.Extensions.LMDB
```

or using `dotnet cli`

```
dotnet add package SoundFingerprinting.Extensions.LMDB
```

As a requirement from dependent library `Spreads.LMDB` you have to provide native lmdb library yourself (considering your application architecture target). Take proper native library from [here](https://github.com/Spreads/Spreads.LMDB/tree/master/lib/runtimes) and make sure it always get copied to your compiled application folder (the simplest way is to attach this file to project and mark it as "Copy on Build").

To use LMDB database with SoundFingerprinting create `LMDBModelService` object and use it in algorithm, like this:

```csharp
var audioService = new SoundFingerprintingAudioService();
using(var modelService = new LMDBModelService("db")){
	var track = new TrackData("GBBKS1200164", "Adele", "Skyfall", "Skyfall", 2012, 290);
	
    // store track metadata in the datasource
    var trackReference = modelService.InsertTrack(track);

    // create hashed fingerprints
    var hashedFingerprints = FingerprintCommandBuilder.Instance
                                .BuildFingerprintCommand()
                                .From(pathToAudioFile)
                                .UsingServices(audioService)
                                .Hash()
                                .Result;
								
    // store hashes in the database for later retrieval
    modelService.InsertHashDataForTrack(hashedFingerprints, trackReference);
}
```
Parameter of `LMDBModelService` constructor is path to directory. LMDB will create its files in this directory.

You need to build your application targeting x64 architecture, since LMDB supports only that. On x32 you will encounter runtime errors!

It's **VERY** important to dispose modelService after usage (although it's best to keep instance for whole application life and dispose it on application closing). Not doing it might cause memory dump, which tries to dump whole VirtualMemory of process. Memory Mapped File is part of VirtualMemory, so it will be dumped as well. That might result in system getting unresponsive for even a couple of minutes!

## Updates

Version **5.2.2.3** brings totally new LMDB .NET wrapper. It uses `Span` and `Memory` constructs to bring zero-copy reads from LMDB library. This means whole new level of optimization in .NET Core 2.1 (and newer) runtimes. It still work on .NET Framework, but it doesn't have these optimizations implemented. Optimizations are mainly in allocation counts. Older version on single recognition could allocate even 250 MB. New version does the same little bit faster while allocating only 1.5 KB!

Please note that this new .NET wrapper is in preview now. **It require MyGet feed** in NuGet configuration to get it's dependencies. I'm using it in production and i do not note any problems with stability so far.

Version **5.2.2.3** is **NOT** backwards compatibile in any way. You'll need to rebuild database from the ground after the update.

## Technical details

LMDB itself is very fast key-value database based on B+Tree and Memory Mapped File.

This storage is slow to write (because inserts and deletes are single threaded - locks are already in code) but very fast to random reads (very efficent reading in highly concurrent environments).

LMDB is file-based database, so there is no network protocol used for communication. As a downside to this we can't use this database between machines (due to how Memory Mapped File works it's forbidden to use LMDB database file by network shares - more on this in [LMDB documentation](http://www.lmdb.tech/doc/)).

## Third party dependencies

Huge thanks to all library creators for making this all possible.

- [SoundFingerprinting](https://github.com/AddictedCS/soundfingerprinting)
- [LMDB](https://github.com/LMDB)
- [Spreads.LMDB (.NET wrapper over LMDB)](https://github.com/Spreads/Spreads.LMDB)
- [MessagePack (extremly fast binary serializer)](https://github.com/neuecc/MessagePack-CSharp)

## Performance

Benchmark (source is in repo) is made using 10 sample tracks. `LMDBModelService` is around **10-20%** slower than `InMemoryService` which is decent enough. I'm still working on optimizations in allocation count and overall performace.

I'm using this adapter in production with 4000 tracks in database. As far as i can tell - 10-20% performance difference still apply on such dataset. I'd love if somebody could test this out on bigger dataset and share his experience.

As you can see in the benchmark - .NET Core 2.1 is much more optimized to work with this adapter. Scaled performance is better, but not so much. But allocations can get crazy low - from 250MB on .NET Framework to 1.4KB on .NET Core 2.1. This is because .NET Core 2.1 can take advantage of `Span` and `Memory` constructs leading to zero-copy reads from LMDB database. So i strongly recommend using .NET Core 2.1 (or newer) to get the best performance and allocation count.

Whole benchmark results:

``` ini

BenchmarkDotNet=v0.11.1, OS=Windows 10.0.17134.285 (1803/April2018Update/Redstone4)
Intel Core i5-4590 CPU 3.30GHz (Haswell), 1 CPU, 4 logical and 4 physical cores
Frequency=3222652 Hz, Resolution=310.3034 ns, Timer=TSC
.NET Core SDK=2.1.402
  [Host]     : .NET Core 2.1.4 (CoreCLR 4.6.26814.03, CoreFX 4.6.26814.02), 64bit RyuJIT
  Job-JGDCBG : .NET Core 2.1.4 (CoreCLR 4.6.26814.03, CoreFX 4.6.26814.02), 64bit RyuJIT
  Job-YXOQMN : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3163.0

Platform=X64  

```
|               Method |     Toolchain |            audioFile |       Mean |     Error |    StdDev | Scaled | ScaledSD |      Gen 0 |      Gen 1 |     Gen 2 |    Allocated |
|--------------------- |-------------- |--------------------- |-----------:|----------:|----------:|-------:|---------:|-----------:|-----------:|----------:|-------------:|
| **InMemoryRecognitions** | **.NET Core 2.1** | **Aura_(...)a.wav [67]** | **1,272.2 ms** |  **4.528 ms** |  **4.235 ms** |   **1.00** |     **0.00** | **24000.0000** |  **7000.0000** | **1000.0000** |       **1.4 KB** |
|     LMDBRecognitions | .NET Core 2.1 | Aura_(...)a.wav [67] | 1,382.7 ms |  3.974 ms |  3.718 ms |   1.09 |     0.00 | 30000.0000 |  9000.0000 | 1000.0000 |       1.4 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| InMemoryRecognitions |   CsProjnet47 | Aura_(...)a.wav [67] | 1,021.9 ms |  4.656 ms |  4.355 ms |   1.00 |     0.00 | 25000.0000 |  7000.0000 | 1000.0000 | 245021.52 KB |
|     LMDBRecognitions |   CsProjnet47 | Aura_(...)a.wav [67] | 1,141.0 ms |  3.572 ms |  3.341 ms |   1.12 |     0.01 | 29000.0000 |  7000.0000 | 1000.0000 |  256179.2 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| **InMemoryRecognitions** | **.NET Core 2.1** | **Hasna(...)n.wav [34]** | **4,025.8 ms** | **14.390 ms** | **13.460 ms** |   **1.00** |     **0.00** | **83000.0000** | **18000.0000** | **2000.0000** |      **1.34 KB** |
|     LMDBRecognitions | .NET Core 2.1 | Hasna(...)n.wav [34] | 5,057.8 ms | 20.140 ms | 18.839 ms |   1.26 |     0.01 | 97000.0000 | 18000.0000 | 2000.0000 |      1.34 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| InMemoryRecognitions |   CsProjnet47 | Hasna(...)n.wav [34] | 3,253.2 ms | 15.975 ms | 14.943 ms |   1.00 |     0.00 | 84000.0000 | 19000.0000 | 2000.0000 | 795769.85 KB |
|     LMDBRecognitions |   CsProjnet47 | Hasna(...)n.wav [34] | 4,277.3 ms | 14.320 ms | 13.395 ms |   1.31 |     0.01 | 99000.0000 | 18000.0000 | 2000.0000 | 832559.72 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| **InMemoryRecognitions** | **.NET Core 2.1** | **OKAM_(...)e.wav [46]** | **3,057.6 ms** | **14.020 ms** | **12.428 ms** |   **1.00** |     **0.00** | **61000.0000** | **14000.0000** | **2000.0000** |      **1.36 KB** |
|     LMDBRecognitions | .NET Core 2.1 | OKAM_(...)e.wav [46] | 3,628.3 ms | 11.664 ms | 10.340 ms |   1.19 |     0.01 | 72000.0000 | 14000.0000 | 2000.0000 |      1.36 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| InMemoryRecognitions |   CsProjnet47 | OKAM_(...)e.wav [46] | 2,459.4 ms |  8.110 ms |  7.586 ms |   1.00 |     0.00 | 63000.0000 | 14000.0000 | 2000.0000 | 606808.39 KB |
|     LMDBRecognitions |   CsProjnet47 | OKAM_(...)e.wav [46] | 3,026.6 ms | 17.352 ms | 15.382 ms |   1.23 |     0.01 | 73000.0000 | 15000.0000 | 3000.0000 |  630181.7 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| **InMemoryRecognitions** | **.NET Core 2.1** | **OKAM_(...)n.wav [62]** | **2,897.6 ms** | **12.073 ms** | **11.293 ms** |   **1.00** |     **0.00** | **62000.0000** | **14000.0000** | **2000.0000** |      **1.39 KB** |
|     LMDBRecognitions | .NET Core 2.1 | OKAM_(...)n.wav [62] | 3,442.0 ms | 12.055 ms | 11.276 ms |   1.19 |     0.01 | 73000.0000 | 17000.0000 | 2000.0000 |      1.39 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| InMemoryRecognitions |   CsProjnet47 | OKAM_(...)n.wav [62] | 2,326.0 ms | 10.039 ms |  8.899 ms |   1.00 |     0.00 | 62000.0000 | 13000.0000 | 2000.0000 | 583481.48 KB |
|     LMDBRecognitions |   CsProjnet47 | OKAM_(...)n.wav [62] | 2,855.8 ms | 12.769 ms | 10.662 ms |   1.23 |     0.01 | 71000.0000 | 13000.0000 | 2000.0000 | 610580.79 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| **InMemoryRecognitions** | **.NET Core 2.1** | **OKAM_(...)y.wav [58]** | **3,255.5 ms** | **13.854 ms** | **12.959 ms** |   **1.00** |     **0.00** | **56000.0000** | **15000.0000** | **2000.0000** |      **1.38 KB** |
|     LMDBRecognitions | .NET Core 2.1 | OKAM_(...)y.wav [58] | 3,944.7 ms | 16.429 ms | 15.368 ms |   1.21 |     0.01 | 67000.0000 | 16000.0000 | 2000.0000 |      1.38 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| InMemoryRecognitions |   CsProjnet47 | OKAM_(...)y.wav [58] | 2,627.3 ms | 20.697 ms | 19.360 ms |   1.00 |     0.00 | 58000.0000 | 15000.0000 | 2000.0000 |  620332.3 KB |
|     LMDBRecognitions |   CsProjnet47 | OKAM_(...)y.wav [58] | 3,296.0 ms | 12.311 ms | 10.913 ms |   1.25 |     0.01 | 66000.0000 | 17000.0000 | 2000.0000 | 646854.41 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| **InMemoryRecognitions** | **.NET Core 2.1** | **OKAM_(...)n.wav [72]** | **2,513.2 ms** |  **6.205 ms** |  **5.182 ms** |   **1.00** |     **0.00** | **53000.0000** | **12000.0000** | **2000.0000** |      **1.41 KB** |
|     LMDBRecognitions | .NET Core 2.1 | OKAM_(...)n.wav [72] | 2,900.6 ms | 14.386 ms | 13.457 ms |   1.15 |     0.01 | 63000.0000 | 11000.0000 | 2000.0000 |      1.41 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| InMemoryRecognitions |   CsProjnet47 | OKAM_(...)n.wav [72] | 2,023.3 ms | 24.852 ms | 23.246 ms |   1.00 |     0.00 | 54000.0000 | 12000.0000 | 2000.0000 |  502197.5 KB |
|     LMDBRecognitions |   CsProjnet47 | OKAM_(...)n.wav [72] | 2,404.6 ms | 22.445 ms | 20.995 ms |   1.19 |     0.02 | 63000.0000 | 12000.0000 | 2000.0000 | 526866.66 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| **InMemoryRecognitions** | **.NET Core 2.1** | **Pataf(...)n.wav [28]** | **3,753.5 ms** | **13.487 ms** | **12.616 ms** |   **1.00** |     **0.00** | **74000.0000** | **17000.0000** | **2000.0000** |      **1.32 KB** |
|     LMDBRecognitions | .NET Core 2.1 | Pataf(...)n.wav [28] | 4,693.0 ms | 15.925 ms | 14.896 ms |   1.25 |     0.01 | 85000.0000 | 17000.0000 | 2000.0000 |      1.32 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| InMemoryRecognitions |   CsProjnet47 | Pataf(...)n.wav [28] | 3,016.5 ms | 10.618 ms |  9.932 ms |   1.00 |     0.00 | 73000.0000 | 17000.0000 | 2000.0000 | 730344.32 KB |
|     LMDBRecognitions |   CsProjnet47 | Pataf(...)n.wav [28] | 3,957.1 ms | 10.402 ms |  9.730 ms |   1.31 |     0.01 | 85000.0000 | 17000.0000 | 2000.0000 | 764489.39 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| **InMemoryRecognitions** | **.NET Core 2.1** | **Sara_(...)r.wav [33]** |   **225.3 ms** |  **1.575 ms** |  **1.473 ms** |   **1.00** |     **0.00** |  **4000.0000** |  **1000.0000** |         **-** |      **1.34 KB** |
|     LMDBRecognitions | .NET Core 2.1 | Sara_(...)r.wav [33] |   234.0 ms |  1.734 ms |  1.622 ms |   1.04 |     0.01 |  4000.0000 |  1000.0000 |         - |      1.34 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| InMemoryRecognitions |   CsProjnet47 | Sara_(...)r.wav [33] |   216.3 ms |  1.320 ms |  1.235 ms |   1.00 |     0.00 |  4333.3333 |  1666.6667 |  333.3333 |  48542.73 KB |
|     LMDBRecognitions |   CsProjnet47 | Sara_(...)r.wav [33] |   188.7 ms |  1.396 ms |  1.306 ms |   0.87 |     0.01 |  4000.0000 |  1000.0000 |         - |  51842.07 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| **InMemoryRecognitions** | **.NET Core 2.1** | **Sara_(...)h.wav [37]** |   **561.7 ms** |  **4.141 ms** |  **3.874 ms** |   **1.00** |     **0.00** |  **8000.0000** |  **2000.0000** |         **-** |      **1.34 KB** |
|     LMDBRecognitions | .NET Core 2.1 | Sara_(...)h.wav [37] |   593.7 ms |  4.661 ms |  4.360 ms |   1.06 |     0.01 | 10000.0000 |  2000.0000 |         - |      1.34 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| InMemoryRecognitions |   CsProjnet47 | Sara_(...)h.wav [37] |   440.9 ms |  4.579 ms |  4.283 ms |   1.00 |     0.00 |  8000.0000 |  2000.0000 |         - | 111955.92 KB |
|     LMDBRecognitions |   CsProjnet47 | Sara_(...)h.wav [37] |   471.4 ms |  3.798 ms |  3.553 ms |   1.07 |     0.01 | 10000.0000 |  2000.0000 |         - |  116091.4 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| **InMemoryRecognitions** | **.NET Core 2.1** | **Scang(...)k.wav [28]** | **1,127.0 ms** |  **2.706 ms** |  **2.531 ms** |   **1.00** |     **0.00** | **24000.0000** |  **5000.0000** | **1000.0000** |      **1.32 KB** |
|     LMDBRecognitions | .NET Core 2.1 | Scang(...)k.wav [28] | 1,229.8 ms |  3.566 ms |  3.336 ms |   1.09 |     0.00 | 29000.0000 |  7000.0000 | 1000.0000 |      1.32 KB |
|                      |               |                      |            |           |           |        |          |            |            |           |              |
| InMemoryRecognitions |   CsProjnet47 | Scang(...)k.wav [28] |   915.8 ms |  3.083 ms |  2.884 ms |   1.00 |     0.00 | 24000.0000 |  5000.0000 | 1000.0000 | 223863.72 KB |
|     LMDBRecognitions |   CsProjnet47 | Scang(...)k.wav [28] | 1,006.4 ms |  3.330 ms |  3.115 ms |   1.10 |     0.00 | 29000.0000 |  5000.0000 | 1000.0000 | 236949.32 KB |


## Contribute

All are welcome to open Issues and Pull Requests. You can contact me on email [jakub.nekro@gmail.com](mailto:jakub.nekro@gmail.com) if you have further questions, opinions and feature ideas.

## License

The framework is provided under [MIT](LICENSE) license agreement.



Thanks to [JetBrains](https://www.jetbrains.com/?from=SoundFingerprinting.Extensions.LMDB) for granting this project with license for Rider and .NET diagnostic Tools

![JetBrains Logo](./images/jetbrains.svg)