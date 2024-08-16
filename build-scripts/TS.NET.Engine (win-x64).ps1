# To run this script from build-scripts directory: powershell -ExecutionPolicy Bypass -File "TS.NET.Engine (win-x64).ps1"

New-Variable -Name "projectFolder" -Value (Join-Path (Resolve-Path ..) 'source/TS.NET.Engine')
$xml = [Xml] (Get-Content $projectFolder\TS.NET.Engine.csproj)
$version = [Version] $xml.Project.PropertyGroup.Version
New-Variable -Name "publishFolder" -Value (Join-Path (Resolve-Path ..) -ChildPath 'builds/win-x64/TS.NET.Engine' | Join-Path -ChildPath $version)
$tsYamlFileExists = Test-Path -Path $publishFolder\thunderscope.yaml
$appSettingsFileExists = Test-Path -Path $publishFolder\appsettings.json
$tslitexDllExists = Test-Path -Path $publishFolder\tslitex.dll

Write-Host "Project folder:" $projectFolder -ForegroundColor green
Write-Host "Project version:" $version -ForegroundColor green
Write-Host "Publish folder:" $publishFolder -ForegroundColor green

# If any of the config files already exist, preserve them. If developer needs a fresh copy, they need to delete them before build.
if($tsYamlFileExists)
{
    Write-Host "Found existing thunderscope.yaml, preserving it." -ForegroundColor green
    Copy-Item -Path $publishFolder\thunderscope.yaml -Destination $publishFolder\..\thunderscope.yaml
}
if($appSettingsFileExists)
{
    Write-Host "Found existing appsettings.json, preserving it." -ForegroundColor green
    Copy-Item -Path $publishFolder\appsettings.json -Destination $publishFolder\..\appsettings.json
}
if($tslitexDllExists)
{
    Write-Host "Found existing tslitex DLL, preserving it." -ForegroundColor green
    Copy-Item -Path $publishFolder\tslitex.dll -Destination $publishFolder\..\tslitex.dll
}

# Remove destination folder if exists
if(Test-Path $publishFolder -PathType Container) { 
    rm -r $publishFolder
}

# Publish application
Write-Host "Publishing project..." -ForegroundColor yellow
dotnet publish $projectFolder/TS.NET.Engine.csproj -r win-x64 -c Release --self-contained /p:PublishSingleFile=true /p:PublishTrimmed=true /p:IncludeNativeLibrariesForSelfExtract=true --output $publishFolder
if ($LastExitCode -ne 0) { break }
Write-Host ""

# Remove debug files
rm $publishFolder/*.pdb

if($tsYamlFileExists)
{
    Copy-Item -Path $publishFolder\..\thunderscope.yaml -Destination $publishFolder\thunderscope.yaml
    Remove-Item -Path $publishFolder\..\thunderscope.yaml
}
if($appSettingsFileExists)
{
    Copy-Item -Path $publishFolder\..\appsettings.json -Destination $publishFolder\appsettings.json
    Remove-Item -Path $publishFolder\..\appsettings.json
}
if($tslitexDllExists)
{
    Copy-Item -Path $publishFolder\..\tslitex.dll -Destination $publishFolder\tslitex.dll
    Remove-Item -Path $publishFolder\..\tslitex.dll
}

# Compress-Archive -Force -Path $publishFolder\* -DestinationPath $publishFolder/../TS.NET.Engine_win-x64_v$version.zip

Write-Host Build Complete -ForegroundColor green