# TS.NET

[Thunderscope](https://github.com/EEVengers/ThunderScope)-compatible PC-host software written in C# using high-performing primitives & SIMD.

- Receives stream from PCIe thunderscope and performs oscilloscope triggering & processing operations.
- Exposes SCPI socket & data socket for user interface.
- High emphasis on cross-platform compatibility & ease of build from source.

## Build on Windows

- Install .NET 8 SDK using downloaded installer or winget
```
winget install Microsoft.DotNet.SDK.8
```
- Run build script using command prompt in `build-scripts` directory

```
powershell -ExecutionPolicy Bypass -File "TS.NET.Engine (win-x64).ps1"
```
- Build output can be found at `builds\win-x64\TS.NET.Engine`

## Build on Linux

- Install .NET 8 SDK

```
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

- Run build script
```
./build-scripts/TS.NET.Engine (linux-x64).bash
```
- Build output can be found at `builds/linux-x64/TS.NET.Engine`, run with `sudo`