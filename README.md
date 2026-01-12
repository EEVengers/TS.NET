# TS.NET

[Thunderscope](https://github.com/EEVengers/ThunderScope)-compatible PC-host software written in C# using high-performing primitives & SIMD.

- Receives stream from TB3/PCIe thunderscope and performs oscilloscope triggering & processing operations.
- Exposes SCPI socket & data socket for user interface.
- High emphasis on cross-platform compatibility & ease of build from source.

## Build on Windows

- Install .NET 10 SDK using downloaded installer or winget
```
winget install Microsoft.DotNet.SDK.10
```
- Run build script using command prompt in `build-scripts` directory

```
powershell -ExecutionPolicy Bypass -File "TS.NET.Engine (win-x64).ps1"
```
- Build output can be found at `builds\TS.NET.Engine\win-x64`

## Build on Linux

- Install .NET 10 SDK

https://learn.microsoft.com/en-us/dotnet/core/install/linux

- Run build script

```
./build-scripts/"TS.NET.Engine (linux-x64)"
```
- Build output can be found at `builds/TS.NET.Engine/linux-x64`

## Profiling on Windows

- Install profiling tool
```
dotnet tool install -g Ultra
```
- Run profiling tool from build directory
```
ultra.exe profile -- TS.NET.Engine.exe -seconds 10
```
- Use the generated json.gz on https://profiler.firefox.com

## Testbench on linux

If there is a blank screen on startup of TS.NET.Testbench.UI, one of these env vars may fix it:

`WEBKIT_DISABLE_DMABUF_RENDERER=1`

`LIBGL_ALWAYS_SOFTWARE=1`
