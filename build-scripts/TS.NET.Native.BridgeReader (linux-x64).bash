version=$(cat ../source/TS.NET.Native.BridgeReader/TS.NET.Native.BridgeReader.csproj | grep -oPm1 "(?<=<Version>)[^<]+")
dotnet publish ../source/TS.NET.Native.BridgeReader/TS.NET.Native.BridgeReader.csproj -r linux-x64 -c Release /p:NativeLib=Shared /p:SelfContained=true --output ../builds/linux-x64/TS.NET.Native.BridgeReader/$version
