version=$(cat ../source/TS.NET.Engine/TS.NET.Engine.csproj | grep -oPm1 "(?<=<Version>)[^<]+")
dotnet publish ../source/TS.NET.Engine/TS.NET.Engine.csproj -r linux-x64 -c Release --self-contained /p:PublishSingleFile=true /p:PublishTrimmed=true /p:IncludeNativeLibrariesForSelfExtract=true --output ../builds/linux-x64/TS.NET.Engine/$version
cp ../source/TS.NET.Engine/thunderscope.yaml ../builds/linux-x64/TS.NET.Engine/$version/thunderscope.yaml
cp ../source/TS.NET.Engine/appsettings.json ../builds/linux-x64/TS.NET.Engine/$version/appsettings.json
