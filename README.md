# SoundFingerprinting.LMDB

[![NuGet](https://img.shields.io/nuget/dt/SoundFingerprinting.LMDB.svg?style=flat-square)](https://www.nuget.org/packages/SoundFingerprinting.LMDB/1.0.0) [![MIT License](http://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE) [![GitHub issues](https://img.shields.io/github/issues/Nekromancer/SoundFingerprinting.LMDB.svg?style=flat-square)](https://github.com/Nekromancer/SoundFingerprinting.LMDB) [![NuGet](https://img.shields.io/nuget/v/SoundFingerprinting.LMDB.svg?style=flat-square)](https://www.nuget.org/packages/SoundFingerprinting.LMDB/1.0.0)



Database adapter for SoundFingerprinting algorithm using LMDB database. It's fast, it's persistent and safe from data violation.

## Usage

To get library simply install it from [Nuget](https://www.nuget.org/packages/SoundFingerprinting.LMDB/1.0.0):

```
Install-Package SoundFingerprinting.LMDB
```

or using `dotnet cli`

```
dotnet add package SoundFingerprinting.LMDB
```

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

It's **VERY** important to dispose modelService after usage (although it's best to keep instance for whole application life and dispose it on application closing). Not doing it might cause memory dump, which tries to dump whole VirtualMemory of process. Memory Mapped File is part of VirtualMemory, so it will be dumped as well. That might result in system getting unresponsive for even a couple of minutes!

## Technical details

LMDB itself is very fast key-value database based on B+Tree and Memory Mapped File.

This storage is slow to write (because inserts and deletes are single threaded - locks are already in code) but very fast to random reads (very efficent reading in highly concurrent environments).

LMDB is file-based database, so there is no network protocol used for communication. As a downside to this we can't use this database between machines (due to how Memory Mapped File works it's forbidden to use LMDB database file by network shares - more on this in [LMDB documentation](http://www.lmdb.tech/doc/)).

## Third party dependencies

Huge thanks to all library creators for making this all possible.

- [SoundFingerprinting](https://github.com/AddictedCS/soundfingerprinting)
- [LMDB](https://github.com/LMDB)
- [Lightning.NET (.NET wrapper over LMDB)](https://github.com/CoreyKaylor/Lightning.NET)
- [ZeroFormatter (the fastest binary serializer available right now)](https://github.com/neuecc/ZeroFormatter)

## Performance

Benchmark (source is in repo) is made using 50 sample tracks. `LMDBModelService` is around **10-20%** slower than `InMemoryService` which is decent enough. I'm still working on optimizations in allocation count and overall performace.

Whole benchmark results:

``` ini

BenchmarkDotNet=v0.11.1, OS=Windows 10.0.17134.228 (1803/April2018Update/Redstone4)
Intel Core i5-4590 CPU 3.30GHz (Haswell), 1 CPU, 4 logical and 4 physical cores
Frequency=3222656 Hz, Resolution=310.3031 ns, Timer=TSC
  [Host]     : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3132.0
  DefaultJob : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3132.0


```
|               Method |            audioFile |       Mean |      Error |     StdDev | Scaled | ScaledSD |       Gen 0 |      Gen 1 |     Gen 2 | Allocated |
|--------------------- |--------------------- |-----------:|-----------:|-----------:|-------:|---------:|------------:|-----------:|----------:|----------:|
| **InMemoryRecognitions** | **Amara(...)h.wav [43]** | **1,006.8 ms** |  **5.8736 ms** |  **5.4942 ms** |   **1.00** |     **0.00** |  **37000.0000** |  **5000.0000** |         **-** | **287.59 MB** |
|     LMDBRecognitions | Amara(...)h.wav [43] | 1,171.7 ms |  8.0256 ms |  7.5071 ms |   1.16 |     0.01 |  63000.0000 |  5000.0000 |         - | 357.29 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Audio(...)e.wav [34]** |   **743.0 ms** |  **4.5712 ms** |  **4.2759 ms** |   **1.00** |     **0.00** |  **26000.0000** |  **4000.0000** |         **-** | **209.98 MB** |
|     LMDBRecognitions | Audio(...)e.wav [34] |   845.1 ms |  7.8071 ms |  7.3027 ms |   1.14 |     0.01 |  46000.0000 |  4000.0000 |         - | 263.64 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Axlet(...)m.wav [42]** |   **123.7 ms** |  **1.2559 ms** |  **1.1748 ms** |   **1.00** |     **0.00** |   **3000.0000** |  **1000.0000** |         **-** |  **33.36 MB** |
|     LMDBRecognitions | Axlet(...)m.wav [42] |   130.8 ms |  1.0457 ms |  0.9270 ms |   1.06 |     0.01 |   6000.0000 |  1250.0000 |         - |  40.03 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Axlet(...)e.wav [75]** |   **841.3 ms** |  **4.9734 ms** |  **4.6521 ms** |   **1.00** |     **0.00** |  **26000.0000** |  **4000.0000** |         **-** | **225.71 MB** |
|     LMDBRecognitions | Axlet(...)e.wav [75] |   960.7 ms |  4.5410 ms |  4.2476 ms |   1.14 |     0.01 |  45000.0000 |  4000.0000 |         - | 274.49 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Black(...)e.wav [30]** |   **586.1 ms** |  **3.5721 ms** |  **3.3413 ms** |   **1.00** |     **0.00** |  **15000.0000** |  **3000.0000** |         **-** | **151.37 MB** |
|     LMDBRecognitions | Black(...)e.wav [30] |   652.7 ms |  2.9789 ms |  2.7865 ms |   1.11 |     0.01 |  27000.0000 |  3000.0000 |         - | 183.42 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Blue_(...)7.wav [36]** |   **944.9 ms** |  **8.0163 ms** |  **7.4985 ms** |   **1.00** |     **0.00** |  **28000.0000** |  **5000.0000** |         **-** | **250.08 MB** |
|     LMDBRecognitions | Blue_(...)7.wav [36] | 1,103.7 ms | 14.3639 ms | 13.4360 ms |   1.17 |     0.02 |  48000.0000 |  6000.0000 |         - | 316.67 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **BoxCa(...)g.wav [33]** |   **212.8 ms** |  **2.0787 ms** |  **1.9444 ms** |   **1.00** |     **0.00** |   **8000.0000** |  **1000.0000** |         **-** |  **66.33 MB** |
|     LMDBRecognitions | BoxCa(...)g.wav [33] |   230.2 ms |  3.1408 ms |  2.9379 ms |   1.08 |     0.02 |  13000.0000 |  1000.0000 |         - |  80.91 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Brevy(...)k.wav [27]** |   **533.2 ms** |  **2.0852 ms** |  **1.9505 ms** |   **1.00** |     **0.00** |  **19000.0000** |  **3000.0000** |         **-** | **157.27 MB** |
|     LMDBRecognitions | Brevy(...)k.wav [27] |   598.8 ms |  6.2894 ms |  5.8831 ms |   1.12 |     0.01 |  35000.0000 |  3000.0000 |         - | 193.56 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Broke(...)l.wav [35]** |   **799.4 ms** |  **6.4445 ms** |  **6.0282 ms** |   **1.00** |     **0.00** |  **23000.0000** |  **4000.0000** |         **-** | **210.16 MB** |
|     LMDBRecognitions | Broke(...)l.wav [35] |   901.1 ms | 10.6877 ms |  9.9973 ms |   1.13 |     0.01 |  41000.0000 |  4000.0000 |         - | 257.09 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Broke(...)d.wav [42]** |   **970.0 ms** |  **6.4735 ms** |  **6.0553 ms** |   **1.00** |     **0.00** |  **31000.0000** |  **5000.0000** |         **-** | **265.14 MB** |
|     LMDBRecognitions | Broke(...)d.wav [42] | 1,127.6 ms | 11.1102 ms | 10.3925 ms |   1.16 |     0.01 |  54000.0000 |  5000.0000 |         - | 325.36 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Chad_(...)t.wav [31]** |   **444.9 ms** |  **2.6826 ms** |  **2.5093 ms** |   **1.00** |     **0.00** |   **9000.0000** |  **2000.0000** |         **-** |  **111.2 MB** |
|     LMDBRecognitions | Chad_(...)t.wav [31] |   496.0 ms |  4.8297 ms |  4.5177 ms |   1.11 |     0.01 |  18000.0000 |  2000.0000 |         - | 133.62 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **David(...)a.wav [54]** |   **947.0 ms** |  **8.0303 ms** |  **7.5116 ms** |   **1.00** |     **0.00** |  **33000.0000** |  **5000.0000** |         **-** | **270.02 MB** |
|     LMDBRecognitions | David(...)a.wav [54] | 1,106.6 ms | 13.7472 ms | 12.8592 ms |   1.17 |     0.02 |  58000.0000 |  5000.0000 |         - | 338.03 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **David(...)I.wav [39]** |   **383.9 ms** |  **4.0525 ms** |  **3.7907 ms** |   **1.00** |     **0.00** |  **15000.0000** |  **2000.0000** |         **-** | **121.61 MB** |
|     LMDBRecognitions | David(...)I.wav [39] |   424.9 ms |  4.4570 ms |  4.1691 ms |   1.11 |     0.01 |  28000.0000 |  2000.0000 |         - |  153.8 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Greg_(...)l.wav [65]** |   **892.0 ms** |  **4.0677 ms** |  **3.3967 ms** |   **1.00** |     **0.00** |  **36000.0000** |  **5000.0000** |         **-** | **269.89 MB** |
|     LMDBRecognitions | Greg_(...)l.wav [65] | 1,021.4 ms |  9.0833 ms |  8.0521 ms |   1.15 |     0.01 |  62000.0000 |  5000.0000 |         - | 342.87 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Inaeq(...)p.wav [42]** |   **875.9 ms** |  **7.4603 ms** |  **6.9783 ms** |   **1.00** |     **0.00** |  **34000.0000** |  **5000.0000** |         **-** | **263.91 MB** |
|     LMDBRecognitions | Inaeq(...)p.wav [42] | 1,002.2 ms | 14.4240 ms | 13.4922 ms |   1.14 |     0.02 |  62000.0000 |  5000.0000 |         - | 329.59 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Jahzz(...)a.wav [25]** |   **563.2 ms** |  **3.7002 ms** |  **3.4611 ms** |   **1.00** |     **0.00** |  **23000.0000** |  **3000.0000** |         **-** |  **175.4 MB** |
|     LMDBRecognitions | Jahzz(...)a.wav [25] |   640.1 ms |  9.4779 ms |  8.4019 ms |   1.14 |     0.02 |  41000.0000 |  3000.0000 |         - | 222.89 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Jahzz(...)r.wav [33]** |   **722.0 ms** |  **8.9102 ms** |  **8.3346 ms** |   **1.00** |     **0.00** |  **23000.0000** |  **4000.0000** |         **-** | **196.11 MB** |
|     LMDBRecognitions | Jahzz(...)r.wav [33] |   817.3 ms |  6.3930 ms |  5.6673 ms |   1.13 |     0.01 |  41000.0000 |  4000.0000 |         - | 242.12 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Jason(...)S.wav [31]** |   **701.0 ms** |  **2.5103 ms** |  **2.3481 ms** |   **1.00** |     **0.00** |  **23000.0000** |  **4000.0000** |         **-** | **193.63 MB** |
|     LMDBRecognitions | Jason(...)S.wav [31] |   782.7 ms |  8.1496 ms |  7.6232 ms |   1.12 |     0.01 |  41000.0000 |  3000.0000 |         - | 239.41 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Kai_E(...)e.wav [38]** |   **761.6 ms** |  **4.0875 ms** |  **3.8235 ms** |   **1.00** |     **0.00** |  **24000.0000** |  **4000.0000** |         **-** | **207.73 MB** |
|     LMDBRecognitions | Kai_E(...)e.wav [38] |   858.5 ms |  6.4623 ms |  6.0449 ms |   1.13 |     0.01 |  44000.0000 |  4000.0000 |         - | 255.48 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Kai_E(...)i.wav [27]** |   **704.3 ms** |  **5.3299 ms** |  **4.9856 ms** |   **1.00** |     **0.00** |  **20000.0000** |  **4000.0000** |         **-** | **180.67 MB** |
|     LMDBRecognitions | Kai_E(...)i.wav [27] |   789.6 ms |  8.5619 ms |  8.0088 ms |   1.12 |     0.01 |  35000.0000 |  4000.0000 |         - | 219.31 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **KieLo(...)9.wav [48]** | **1,686.5 ms** |  **6.0295 ms** |  **5.6400 ms** |   **1.00** |     **0.00** |  **56000.0000** |  **8000.0000** | **1000.0000** | **422.23 MB** |
|     LMDBRecognitions | KieLo(...)9.wav [48] | 1,989.1 ms | 14.4558 ms | 12.8147 ms |   1.18 |     0.01 |  95000.0000 |  8000.0000 | 1000.0000 | 525.01 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Konst(...)8.wav [52]** | **2,585.0 ms** | **10.5625 ms** |  **9.8802 ms** |   **1.00** |     **0.00** |  **64000.0000** | **13000.0000** | **1000.0000** | **616.07 MB** |
|     LMDBRecognitions | Konst(...)8.wav [52] | 3,266.7 ms | 59.5605 ms | 55.7129 ms |   1.26 |     0.02 | 112000.0000 | 14000.0000 | 1000.0000 | 739.34 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Krest(...)d.wav [29]** |   **786.0 ms** |  **4.4375 ms** |  **4.1509 ms** |   **1.00** |     **0.00** |  **29000.0000** |  **4000.0000** |         **-** | **231.24 MB** |
|     LMDBRecognitions | Krest(...)d.wav [29] |   910.0 ms |  6.9717 ms |  6.5213 ms |   1.16 |     0.01 |  51000.0000 |  4000.0000 |         - | 286.07 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Lobo_(...)4.wav [45]** | **1,329.0 ms** |  **8.4477 ms** |  **7.9020 ms** |   **1.00** |     **0.00** |  **41000.0000** |  **7000.0000** | **1000.0000** |  **319.3 MB** |
|     LMDBRecognitions | Lobo_(...)4.wav [45] | 1,508.5 ms |  4.7961 ms |  4.4862 ms |   1.14 |     0.01 |  69000.0000 |  7000.0000 | 1000.0000 | 395.28 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Loren(...)t.wav [35]** |   **790.5 ms** |  **7.6480 ms** |  **7.1539 ms** |   **1.00** |     **0.00** |  **29000.0000** |  **4000.0000** |         **-** | **229.51 MB** |
|     LMDBRecognitions | Loren(...)t.wav [35] |   903.5 ms |  4.9345 ms |  4.6157 ms |   1.14 |     0.01 |  50000.0000 |  4000.0000 |         - | 284.72 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Monk_(...)y.wav [51]** |   **141.0 ms** |  **0.6948 ms** |  **0.6499 ms** |   **1.00** |     **0.00** |   **4250.0000** |  **1250.0000** |         **-** |  **40.34 MB** |
|     LMDBRecognitions | Monk_(...)y.wav [51] |   149.6 ms |  1.6736 ms |  1.5655 ms |   1.06 |     0.01 |   8000.0000 |  1000.0000 |         - |  49.45 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Nctrn(...)w.wav [25]** |   **117.2 ms** |  **1.0528 ms** |  **0.9848 ms** |   **1.00** |     **0.00** |   **2200.0000** |   **800.0000** |         **-** |  **29.55 MB** |
|     LMDBRecognitions | Nctrn(...)w.wav [25] |   123.1 ms |  0.9466 ms |  0.8855 ms |   1.05 |     0.01 |   5000.0000 |  1000.0000 |         - |   35.2 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Podin(...)k.wav [33]** |   **710.8 ms** |  **2.6627 ms** |  **2.3604 ms** |   **1.00** |     **0.00** |  **23000.0000** |  **4000.0000** |         **-** | **196.09 MB** |
|     LMDBRecognitions | Podin(...)k.wav [33] |   799.4 ms |  5.3034 ms |  4.9608 ms |   1.12 |     0.01 |  40000.0000 |  4000.0000 |         - | 241.55 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Podin(...)g.wav [29]** |   **423.0 ms** |  **1.7708 ms** |  **1.6565 ms** |   **1.00** |     **0.00** |  **10000.0000** |  **2000.0000** |         **-** | **108.09 MB** |
|     LMDBRecognitions | Podin(...)g.wav [29] |   470.7 ms |  5.5547 ms |  5.1959 ms |   1.11 |     0.01 |  20000.0000 |  2000.0000 |         - | 131.29 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Scott(...)s.wav [41]** |   **763.0 ms** |  **4.2921 ms** |  **3.8048 ms** |   **1.00** |     **0.00** |  **30000.0000** |  **4000.0000** |         **-** | **231.17 MB** |
|     LMDBRecognitions | Scott(...)s.wav [41] |   868.0 ms |  8.1087 ms |  7.5849 ms |   1.14 |     0.01 |  54000.0000 |  4000.0000 |         - | 289.05 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Scott(...)s.wav [40]** |   **714.3 ms** |  **3.7069 ms** |  **3.4675 ms** |   **1.00** |     **0.00** |  **24000.0000** |  **4000.0000** |         **-** | **197.65 MB** |
|     LMDBRecognitions | Scott(...)s.wav [40] |   802.9 ms | 10.8335 ms | 10.1336 ms |   1.12 |     0.01 |  43000.0000 |  4000.0000 |         - | 244.47 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Scott(...)e.wav [30]** |   **805.2 ms** |  **3.8078 ms** |  **3.5619 ms** |   **1.00** |     **0.00** |  **38000.0000** |  **4000.0000** |         **-** | **258.57 MB** |
|     LMDBRecognitions | Scott(...)e.wav [30] |   948.8 ms | 13.8680 ms | 12.9722 ms |   1.18 |     0.02 |  64000.0000 |  6000.0000 |         - | 338.39 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Scott(...)s.wav [37]** |   **434.3 ms** |  **2.8958 ms** |  **2.7087 ms** |   **1.00** |     **0.00** |  **18000.0000** |  **2000.0000** |         **-** | **134.01 MB** |
|     LMDBRecognitions | Scott(...)s.wav [37] |   488.6 ms |  5.7061 ms |  5.3375 ms |   1.12 |     0.01 |  31000.0000 |  2000.0000 |         - | 170.36 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Scott(...)k.wav [32]** |   **670.7 ms** |  **6.3052 ms** |  **5.8979 ms** |   **1.00** |     **0.00** |  **29000.0000** |  **3000.0000** |         **-** | **210.62 MB** |
|     LMDBRecognitions | Scott(...)k.wav [32] |   776.3 ms | 12.6744 ms | 11.8557 ms |   1.16 |     0.02 |  50000.0000 |  3000.0000 |         - | 263.71 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Scott(...)t.wav [34]** |   **898.4 ms** |  **3.1121 ms** |  **2.9110 ms** |   **1.00** |     **0.00** |  **43000.0000** |  **5000.0000** |         **-** | **289.03 MB** |
|     LMDBRecognitions | Scott(...)t.wav [34] | 1,037.3 ms | 10.8171 ms | 10.1184 ms |   1.15 |     0.01 |  70000.0000 |  7000.0000 |         - | 369.26 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Scott(...)s.wav [42]** |   **675.5 ms** |  **6.0262 ms** |  **5.3421 ms** |   **1.00** |     **0.00** |  **25000.0000** |  **3000.0000** |         **-** | **198.16 MB** |
|     LMDBRecognitions | Scott(...)s.wav [42] |   772.0 ms |  7.9155 ms |  7.4042 ms |   1.14 |     0.01 |  43000.0000 |  3000.0000 |         - | 246.02 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Serge(...)I.wav [45]** |   **663.7 ms** |  **3.2392 ms** |  **3.0299 ms** |   **1.00** |     **0.00** |  **18000.0000** |  **3000.0000** |         **-** | **172.88 MB** |
|     LMDBRecognitions | Serge(...)I.wav [45] |   746.2 ms |  9.4650 ms |  8.8536 ms |   1.12 |     0.01 |  33000.0000 |  3000.0000 |         - | 208.24 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **So_Fa(...)e.wav [38]** |   **549.9 ms** |  **2.4271 ms** |  **1.8949 ms** |   **1.00** |     **0.00** |  **21000.0000** |  **3000.0000** |         **-** |  **167.3 MB** |
|     LMDBRecognitions | So_Fa(...)e.wav [38] |   616.0 ms |  5.9375 ms |  5.2634 ms |   1.12 |     0.01 |  37000.0000 |  3000.0000 |         - | 208.08 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **The_K(...)g.wav [57]** |   **778.5 ms** |  **3.9930 ms** |  **3.5397 ms** |   **1.00** |     **0.00** |  **28000.0000** |  **4000.0000** |         **-** | **222.82 MB** |
|     LMDBRecognitions | The_K(...)g.wav [57] |   891.4 ms |  8.1432 ms |  7.6172 ms |   1.15 |     0.01 |  48000.0000 |  4000.0000 |         - | 280.03 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Tours(...)t.wav [27]** |   **727.1 ms** |  **4.6740 ms** |  **4.3721 ms** |   **1.00** |     **0.00** |  **29000.0000** |  **4000.0000** |         **-** | **225.93 MB** |
|     LMDBRecognitions | Tours(...)t.wav [27] |   825.8 ms | 11.2063 ms | 10.4824 ms |   1.14 |     0.02 |  55000.0000 |  4000.0000 |         - |  286.1 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Uncle(...)u.wav [39]** |   **629.4 ms** |  **3.2745 ms** |  **3.0629 ms** |   **1.00** |     **0.00** |  **24000.0000** |  **3000.0000** |         **-** | **185.33 MB** |
|     LMDBRecognitions | Uncle(...)u.wav [39] |   713.7 ms |  7.1105 ms |  6.6512 ms |   1.13 |     0.01 |  42000.0000 |  3000.0000 |         - | 231.17 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Vince(...)k.wav [28]** |   **497.0 ms** |  **2.5089 ms** |  **2.3469 ms** |   **1.00** |     **0.00** |  **17000.0000** |  **3000.0000** |         **-** | **141.68 MB** |
|     LMDBRecognitions | Vince(...)k.wav [28] |   547.5 ms |  3.7080 ms |  3.4685 ms |   1.10 |     0.01 |  30000.0000 |  3000.0000 |         - | 174.54 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Yan_T(...)a.wav [32]** | **1,333.7 ms** |  **5.3886 ms** |  **4.7768 ms** |   **1.00** |     **0.00** |  **45000.0000** |  **7000.0000** | **1000.0000** |  **336.1 MB** |
|     LMDBRecognitions | Yan_T(...)a.wav [32] | 1,560.1 ms | 20.0567 ms | 18.7611 ms |   1.17 |     0.01 |  77000.0000 |  7000.0000 | 1000.0000 | 426.15 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Yan_T(...)2.wav [29]** | **2,183.4 ms** |  **5.0096 ms** |  **4.6860 ms** |   **1.00** |     **0.00** |  **81000.0000** | **12000.0000** | **1000.0000** | **589.18 MB** |
|     LMDBRecognitions | Yan_T(...)2.wav [29] | 2,746.0 ms | 26.4757 ms | 24.7654 ms |   1.26 |     0.01 | 135000.0000 | 12000.0000 | 1000.0000 | 740.91 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Yung_(...)e.wav [25]** |   **712.2 ms** |  **5.7083 ms** |  **5.3395 ms** |   **1.00** |     **0.00** |  **27000.0000** |  **3000.0000** |         **-** | **206.91 MB** |
|     LMDBRecognitions | Yung_(...)e.wav [25] |   824.5 ms |  8.8262 ms |  8.2561 ms |   1.16 |     0.01 |  46000.0000 |  5000.0000 |         - | 272.63 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Yung_(...)e.wav [29]** |   **709.9 ms** |  **3.5669 ms** |  **2.9785 ms** |   **1.00** |     **0.00** |  **25000.0000** |  **4000.0000** |         **-** | **201.34 MB** |
|     LMDBRecognitions | Yung_(...)e.wav [29] |   801.1 ms |  5.6005 ms |  5.2387 ms |   1.13 |     0.01 |  43000.0000 |  4000.0000 |         - | 252.45 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Yung_(...)0.wav [26]** |   **898.4 ms** | **10.2097 ms** |  **9.5501 ms** |   **1.00** |     **0.00** |  **33000.0000** |  **5000.0000** |         **-** | **256.55 MB** |
|     LMDBRecognitions | Yung_(...)0.wav [26] | 1,062.7 ms |  8.4634 ms |  7.9167 ms |   1.18 |     0.01 |  55000.0000 |  7000.0000 |         - | 330.12 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **Yung_(...)y.wav [30]** |   **810.0 ms** |  **4.1760 ms** |  **3.9062 ms** |   **1.00** |     **0.00** |  **25000.0000** |  **4000.0000** |         **-** | **220.12 MB** |
|     LMDBRecognitions | Yung_(...)y.wav [30] |   947.2 ms | 15.3386 ms | 14.3478 ms |   1.17 |     0.02 |  44000.0000 |  6000.0000 |         - | 284.31 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **eddy_(...)s.wav [34]** |   **604.6 ms** |  **3.0506 ms** |  **2.8535 ms** |   **1.00** |     **0.00** |  **24000.0000** |  **3000.0000** |         **-** | **184.56 MB** |
|     LMDBRecognitions | eddy_(...)s.wav [34] |   682.0 ms |  6.0640 ms |  5.6723 ms |   1.13 |     0.01 |  42000.0000 |  3000.0000 |         - | 230.05 MB |
|                      |                      |            |            |            |        |          |             |            |           |           |
| **InMemoryRecognitions** | **eddy_(...)t.wav [26]** |   **229.5 ms** |  **2.6037 ms** |  **2.4355 ms** |   **1.00** |     **0.00** |   **9000.0000** |  **1000.0000** |         **-** |  **71.15 MB** |
|     LMDBRecognitions | eddy_(...)t.wav [26] |   254.2 ms |  3.8611 ms |  3.2242 ms |   1.11 |     0.02 |  15000.0000 |  1000.0000 |         - |  88.86 MB |

## Contribute

All are welcome to open Issues and Pull Requests. You can contact me on email [jakub.nekro@gmail.com](mailto:jakub.nekro@gmail.com) if you have further questions, opinions and feature ideas.

## License

The framework is provided under [MIT](LICENSE) license agreement.
