# SoundFingerprinting.LMDB

[![Build status](https://dev.azure.com/NekroVision/SoundFingerprinting.Extensions.LMDB/_apis/build/status/SoundFingerprinting.Extensions.LMDB-CI)](https://dev.azure.com/NekroVision/SoundFingerprinting.Extensions.LMDB/_build/latest?definitionId=1) [![NuGet](https://img.shields.io/nuget/v/SoundFingerprinting.Extensions.LMDB.svg?style=flat-square)](https://www.nuget.org/packages/SoundFingerprinting.Extensions.LMDB) [![NuGet](https://img.shields.io/nuget/dt/SoundFingerprinting.Extensions.LMDB.svg?style=flat-square)](https://www.nuget.org/packages/SoundFingerprinting.Extensions.LMDB) [![GitHub issues](https://img.shields.io/github/issues/Nekromancer/SoundFingerprinting.Extensions.LMDB.svg?style=flat-square)](https://github.com/Nekromancer/SoundFingerprinting.Extensions.LMDB/issues) [![MIT License](http://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE) [![SonarCloud](https://sonarcloud.io/api/project_badges/measure?project=Nekromancer_SoundFingerprinting.Extensions.LMDB&metric=alert_status)](https://sonarcloud.io/dashboard?id=Nekromancer_SoundFingerprinting.Extensions.LMDB)



Database adapter for SoundFingerprinting algorithm using LMDB database. It's fast, it's persistent and safe from data violation.

# **Beware** that this adapter supports only audio fingerprints storage. It should be possible to implement video storage as well, but i do not have time to proceed with that functionality. Library is open for contributions

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

As you can see in the benchmark - .NET Core is much more optimized to work with this adapter. Scaled performance is better, but not so much. But allocations can get crazy low - from 250MB on .NET Framework to 1.4KB on .NET Core. This is because .NET Core can take advantage of `Span` and `Memory` constructs leading to zero-copy reads from LMDB database. So i strongly recommend using .NET Core to get the best performance and allocation count.

Whole benchmark results available [Here](https://github.com/Nekromancer/SoundFingerprinting.Extensions.LMDB/blob/master/Performance.md)

## Contribute

All are welcome to open Issues and Pull Requests. You can contact me on email [jakub.nekro@gmail.com](mailto:jakub.nekro@gmail.com) if you have further questions, opinions and feature ideas.

## License

The framework is provided under [MIT](LICENSE) license agreement.
