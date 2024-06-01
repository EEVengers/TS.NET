# To run this script from build-scripts directory: powershell -ExecutionPolicy Bypass -File "TS.NET.UI.Desktop (win-x64).ps1"

New-Variable -Name "projectFolder" -Value (Join-Path (Resolve-Path ..) 'source/TS.NET.UI.Desktop')
$xml = [Xml] (Get-Content $projectFolder\TS.NET.UI.Desktop.csproj)
$version = [Version] $xml.Project.PropertyGroup.Version
New-Variable -Name "publishFolder" -Value (Join-Path (Resolve-Path ..) -ChildPath 'builds/win-x64/TS.NET.UI.Desktop' | Join-Path -ChildPath $version)

Write-Host "Project folder:" $projectFolder -ForegroundColor green
Write-Host "Project version:" $version -ForegroundColor green
Write-Host "Publish folder:" $publishFolder -ForegroundColor green

# Remove destination folder if exists
if(Test-Path $publishFolder -PathType Container) { 
    rm -r $publishFolder
}

# Publish application
Write-Host "Publishing project..." -ForegroundColor yellow
dotnet publish $projectFolder/TS.NET.UI.Desktop.csproj -r win-x64 -c Release --self-contained /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --output $publishFolder
if ($LastExitCode -ne 0) { break }
Write-Host ""

# Remove debug files
rm $publishFolder/*.pdb

# Compress-Archive -Force -Path $publishFolder\* -DestinationPath $publishFolder/../TS.NET.UI.Desktop_win-x64_v$version.zip

Write-Host Build Complete -ForegroundColor green