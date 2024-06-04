version=$(cat ../source/TS.NET.UI.Desktop/TS.NET.UI.Desktop.csproj | grep -oPm1 "(?<=<Version>)[^<]+")
dotnet publish ../source/TS.NET.UI.Desktop/TS.NET.UI.Desktop.csproj -r linux-x64 -c Release --self-contained /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=true --output ../builds/linux-x64/TS.NET.UI.Desktop/$version
