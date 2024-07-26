version=$(cat ../source/TS.NET.Engine/TS.NET.Engine.csproj | grep -oPm1 "(?<=<Version>)[^<]+")

cp ../builds/linux-x64/TS.NET.Engine/$version/thunderscope.yaml ../thunderscope.yaml
cp ../builds/linux-x64/TS.NET.Engine/$version/appsettings.json ../appsettings.json

dotnet publish ../source/TS.NET.Engine/TS.NET.Engine.csproj -r linux-x64 -c Release --self-contained /p:PublishSingleFile=true /p:PublishTrimmed=true /p:IncludeNativeLibrariesForSelfExtract=true --output ../builds/linux-x64/TS.NET.Engine/$version

cp ../thunderscope.yaml ../builds/linux-x64/TS.NET.Engine/$version/thunderscope.yaml
cp ../appsettings.json ../builds/linux-x64/TS.NET.Engine/$version/appsettings.json
rm ../thunderscope.yaml
rm ../appsettings.json

