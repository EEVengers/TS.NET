# TS.NET

[Thunderscope](https://github.com/EEVengers/ThunderScope)-compatible PC-host software written in C# using high-performing primitives & SIMD.

- Receives stream from TB3/PCIe thunderscope and performs oscilloscope triggering & processing operations.
- Exposes SCPI socket & data socket for user interface.
- High emphasis on cross-platform compatibility & ease of build from source.

## Build on Windows

- Install .NET 9 SDK using downloaded installer or winget
```
winget install Microsoft.DotNet.SDK.9
```
- Run build script using command prompt in `build-scripts` directory

```
powershell -ExecutionPolicy Bypass -File "TS.NET.Engine (win-x64).ps1"
```
- Build output can be found at `builds\win-x64\TS.NET.Engine`

## Build on Linux

TS.NET requires .NET SDK. to install follow below instruction based on your distro

### Ubuntu

 Optionally (depending on Ubuntu version):
 ```
 sudo add-apt-repository ppa:dotnet/backports
 ```

 ```
 sudo apt-get update
 sudo apt-get install -y dotnet-sdk-9.0
 ```
### Arch
  ```
  sudo pacman -S dotnet-sdk
  ```
Run build script
```
./build-scripts/"TS.NET.Engine (linux-x64)"
```

Build output can be found at `builds/linux-x64/TS.NET.Engine/0.1.0/` `0.1.0` indicates the version of TS.NET.Engine and might change depending on the version you are building.

Copy `thunderscope-appsettings.json` and `thunderscope-calibration.json` from the `source` folder to
`builds/linux-64/TS.NET.Engine/0.1.0/` using the following commands

```
cp source/TS.NET.Engine/thunderscope-appsettings.json builds/linux-x64/TS.NET.Engine/0.1.0/
cp source/TS.NET.Engine/thunderscope-calibration.json builds/linux-x64/TS.NET.Engine/0.1.0/
```
to run TS.NET.Engine use below commands, there should be no errors if everything went alright.

```
cd builds/linux-x64/TS.NET.Engine/0.1.0/
./TS.NET.Engine
```
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
