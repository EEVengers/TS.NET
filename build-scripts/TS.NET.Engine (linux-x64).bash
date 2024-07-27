version=$(cat ../source/TS.NET.Engine/TS.NET.Engine.csproj | grep -oPm1 "(?<=<Version>)[^<]+")

if test -f ../builds/linux-x64/TS.NET.Engine/$version/thunderscope.yaml; then
  cp ../builds/linux-x64/TS.NET.Engine/$version/thunderscope.yaml ../thunderscope.yaml
fi

if test -f ../builds/linux-x64/TS.NET.Engine/$version/appsettings.json; then
  cp ../builds/linux-x64/TS.NET.Engine/$version/appsettings.json ../appsettings.json
fi

dotnet publish ../source/TS.NET.Engine/TS.NET.Engine.csproj -r linux-x64 -c Release --self-contained /p:PublishSingleFile=true /p:PublishTrimmed=true /p:IncludeNativeLibrariesForSelfExtract=true --output ../builds/linux-x64/TS.NET.Engine/$version

if test -f ../thunderscope.yaml; then
  cp ../thunderscope.yaml ../builds/linux-x64/TS.NET.Engine/$version/thunderscope.yaml
  rm ../thunderscope.yaml
fi

if test -f ../appsettings.json; then
  cp ../appsettings.json ../builds/linux-x64/TS.NET.Engine/$version/appsettings.json
  rm ../appsettings.json
fi



