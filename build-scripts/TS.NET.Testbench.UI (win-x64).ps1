# To run this script from build-scripts directory: powershell -ExecutionPolicy Bypass -File "TS.NET.Testbench.UI (win-x64).ps1"

New-Variable -Name "projectFolder" -Value (Join-Path (Resolve-Path ..) 'source/TS.NET.Testbench.UI')
$xml = [Xml] (Get-Content $projectFolder\TS.NET.Testbench.UI.csproj)
$version = [Version] $xml.Project.PropertyGroup.Version
New-Variable -Name "publishFolder" -Value (Join-Path (Resolve-Path ..) -ChildPath "builds/TS.NET.Testbench.UI/win-x64")

$appSettingsFileExists = Test-Path -Path $publishFolder\variables.json
$tslitexDllExists = Test-Path -Path $publishFolder\tslitex.dll

Write-Host "Project folder:" $projectFolder -ForegroundColor green
Write-Host "Project version:" $version -ForegroundColor green
Write-Host "Publish folder:" $publishFolder -ForegroundColor green

if($appSettingsFileExists)
{
    Write-Host "Found existing variables.json, preserving it." -ForegroundColor green
    Copy-Item -Path $publishFolder\variables.json -Destination $publishFolder\..\variables.json
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
dotnet publish $projectFolder/TS.NET.Testbench.UI.csproj -r win-x64 -c Release --self-contained /p:PublishSingleFile=true /p:PublishTrimmed=true /p:IncludeNativeLibrariesForSelfExtract=true --output $publishFolder
if ($LastExitCode -ne 0) { break }
Write-Host ""

# Remove debug files
rm $publishFolder/*.pdb

if($appSettingsFileExists)
{
    Copy-Item -Path $publishFolder\..\variables.json -Destination $publishFolder\variables.json
    Remove-Item -Path $publishFolder\..\variables.json
}
if($tslitexDllExists)
{
    Copy-Item -Path $publishFolder\..\tslitex.dll -Destination $publishFolder\tslitex.dll
    Remove-Item -Path $publishFolder\..\tslitex.dll
}

Write-Host Build Complete -ForegroundColor green