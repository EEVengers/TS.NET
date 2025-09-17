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

TS.NET requires .NET SDK. to build, installation varies by distro.

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
Build output can be found at `builds/TS.NET.Engine/linux-x64`

to run TS.NET.Engine use below commands, there should be no errors if everything went alright.
```
cd builds/TS.NET.Engine/linux-x64
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
