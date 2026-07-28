#!/usr/bin/dotnet

// Before first run:
// dotnet tool install --global wix --version 4.0.6
// wix extension add -g WixToolset.UI.wixext/4.0.6
#:package WixSharp_wix4@2.14.1
#:package System.Drawing.Common@6.0.0

using System.Xml.Linq;
using WixSharp;
using System.Diagnostics;

static string RunWix(params string[] arguments)
{
    var startInfo = new ProcessStartInfo("wix")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo);

    if (process is null)
    {
        throw new InvalidOperationException("Could not start the WiX CLI.");
    }

    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"WiX prerequisite check failed: {error.Trim()}");
    }

    return output;
}

var wixVersion = RunWix("--version");

if (!wixVersion.Contains("4.0.6", StringComparison.Ordinal))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("WiX Toolset 4.0.6 is required. Run: dotnet tool install --global wix --version 4.0.6");
    Console.ResetColor();
    return;
}

var wixExtensions = RunWix("extension", "list", "-g");

if (!wixExtensions.Contains("WixToolset.UI.wixext 4.0.6", StringComparison.OrdinalIgnoreCase))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("WiX UI extension is required. Run: wix extension add -g WixToolset.UI.wixext/4.0.6");
    Console.ResetColor();
    return;
}

var currentDirectory = Directory.GetCurrentDirectory();
var rootDirectory = currentDirectory;

if (!Directory.Exists(Path.Combine(rootDirectory, "source", "TS.NET.Engine")))
{
    rootDirectory = Path.GetFullPath(Path.Combine(currentDirectory, ".."));
}

var projectFile = Path.Combine(rootDirectory, "source", "TS.NET.Engine", "TS.NET.Engine.csproj");
var publishDirectory = Path.Combine(rootDirectory, "build", "TS.NET.Engine", "win-x64");
var installerDirectory = Path.Combine(rootDirectory, "build", "TS.NET.Engine", "msi");
var installerAssetsDirectory = Path.Combine(rootDirectory, "build-scripts", "assets");
var bannerBitmap = Path.Combine(installerAssetsDirectory, "installer-banner.bmp");
var dialogBitmap = Path.Combine(installerAssetsDirectory, "installer-dialog.bmp");
var engineIcon = Path.Combine(rootDirectory, "source", "TS.NET.Engine", "icon.ico");

if (!System.IO.File.Exists(projectFile))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Could not find project file at '{projectFile}'.");
    Console.ResetColor();
    return;
}

if (!Directory.Exists(publishDirectory) || !Directory.EnumerateFiles(publishDirectory, "*", SearchOption.AllDirectories).Any())
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"No published win-x64 files were found in '{publishDirectory}'. Run TS.NET.Engine.cs first.");
    Console.ResetColor();
    return;
}

if (!System.IO.File.Exists(bannerBitmap) || !System.IO.File.Exists(dialogBitmap) || !System.IO.File.Exists(engineIcon))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine("Could not find a required installer asset.");
    Console.ResetColor();
    return;
}

var version = XDocument.Load(projectFile).Descendants().FirstOrDefault(element => string.Equals(element.Name.LocalName, "Version", StringComparison.OrdinalIgnoreCase))?.Value.Trim();

if (!Version.TryParse(version, out var installerVersion))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Could not read a valid version from '{projectFile}'.");
    Console.ResetColor();
    return;
}

Directory.CreateDirectory(installerDirectory);

var engineExecutable = Path.Combine(publishDirectory, "TS.NET.Engine.exe");
var project = new Project("TS.NET.Engine",
    new Dir(@"%LocalAppData%\TS.NET\Engine",
        new WixSharp.File(
            engineExecutable,
            new FileShortcut("TS.NET.Engine", @"%ProgramMenuFolder%\EEVengers")
            {
                WorkingDirectory = "INSTALLDIR",
                IconFile = engineIcon
            }),
        new Files(Path.Combine(publishDirectory, "*"), path => !string.Equals(path, engineExecutable, StringComparison.OrdinalIgnoreCase))))
{
    GUID = new Guid("34a037a3-40b6-4faa-9b95-119952e4413c"),
    Version = installerVersion,
    Platform = Platform.x64,
    Scope = InstallScope.perUser,
    UI = WUI.WixUI_Common,
    MajorUpgrade = new MajorUpgrade
    {
        AllowDowngrades = false,
        DowngradeErrorMessage = "A newer version of TS.NET.Engine is already installed.",
        Schedule = UpgradeSchedule.afterInstallInitialize
    },
    OutDir = installerDirectory,
    OutFileName = $"TS.NET.Engine-{installerVersion}-win-x64"
};
project.ControlPanelInfo.Manufacturer = "EEVengers";
project.ControlPanelInfo.ProductIcon = engineIcon;

project.WixSourceGenerated += document =>
{
    var wixNamespace = document.Root!.Name.Namespace;
    var package = document.Root.Element(wixNamespace + "Package")!;

    package.Add(
        new XElement(wixNamespace + "StandardDirectory",
            new XAttribute("Id", "ProgramMenuFolder"),
            new XElement(wixNamespace + "Directory",
                new XAttribute("Id", "ProgramMenuFolder.EEVengers"),
                new XAttribute("Name", "EEVengers"),
                new XElement(wixNamespace + "Component",
                    new XAttribute("Id", "ProgramMenuFolder.EEVengers.Component"),
                    new XAttribute("Guid", "19CD81D5-91A9-47DD-8D5D-A2CFC86719AB"),
                    new XElement(wixNamespace + "RemoveFolder",
                        new XAttribute("Id", "ProgramMenuFolder.EEVengers.Remove"),
                        new XAttribute("On", "uninstall"))))),
        new XElement(wixNamespace + "WixVariable",
            new XAttribute("Id", "WixUIBannerBmp"),
            new XAttribute("Value", bannerBitmap)),
        new XElement(wixNamespace + "WixVariable",
            new XAttribute("Id", "WixUIDialogBmp"),
            new XAttribute("Value", dialogBitmap)),
        new XElement(wixNamespace + "UI",
            new XElement(wixNamespace + "TextStyle",
                new XAttribute("Id", "WixUI_Font_Normal"),
                new XAttribute("FaceName", "Tahoma"),
                new XAttribute("Size", "8")),
            new XElement(wixNamespace + "TextStyle",
                new XAttribute("Id", "WixUI_Font_Bigger"),
                new XAttribute("FaceName", "Tahoma"),
                new XAttribute("Size", "12")),
            new XElement(wixNamespace + "TextStyle",
                new XAttribute("Id", "WixUI_Font_Title"),
                new XAttribute("FaceName", "Tahoma"),
                new XAttribute("Size", "9"),
                new XAttribute("Bold", "yes")),
            new XElement(wixNamespace + "Property",
                new XAttribute("Id", "DefaultUIFont"),
                new XAttribute("Value", "WixUI_Font_Normal")),
            new XElement(wixNamespace + "Property",
                new XAttribute("Id", "ARPNOMODIFY"),
                new XAttribute("Value", "1")),
            new XElement(wixNamespace + "DialogRef", new XAttribute("Id", "WelcomeDlg")),
            new XElement(wixNamespace + "DialogRef", new XAttribute("Id", "MaintenanceWelcomeDlg")),
            new XElement(wixNamespace + "DialogRef", new XAttribute("Id", "MaintenanceTypeDlg")),
            new XElement(wixNamespace + "DialogRef", new XAttribute("Id", "VerifyReadyDlg")),
            new XElement(wixNamespace + "DialogRef", new XAttribute("Id", "ExitDialog")),
            new XElement(wixNamespace + "Publish",
                new XAttribute("Dialog", "WelcomeDlg"),
                new XAttribute("Control", "Next"),
                new XAttribute("Event", "NewDialog"),
                new XAttribute("Value", "PrepareDlg")),
            new XElement(wixNamespace + "Publish",
                new XAttribute("Dialog", "MaintenanceWelcomeDlg"),
                new XAttribute("Control", "Next"),
                new XAttribute("Event", "NewDialog"),
                new XAttribute("Value", "MaintenanceTypeDlg")),
            new XElement(wixNamespace + "Publish",
                new XAttribute("Dialog", "MaintenanceTypeDlg"),
                new XAttribute("Control", "RepairButton"),
                new XAttribute("Event", "NewDialog"),
                new XAttribute("Value", "VerifyReadyDlg")),
            new XElement(wixNamespace + "Publish",
                new XAttribute("Dialog", "MaintenanceTypeDlg"),
                new XAttribute("Control", "RemoveButton"),
                new XAttribute("Event", "NewDialog"),
                new XAttribute("Value", "VerifyReadyDlg")),
            new XElement(wixNamespace + "Publish",
                new XAttribute("Dialog", "MaintenanceTypeDlg"),
                new XAttribute("Control", "Back"),
                new XAttribute("Event", "NewDialog"),
                new XAttribute("Value", "MaintenanceWelcomeDlg")),
            new XElement(wixNamespace + "Publish",
                new XAttribute("Dialog", "VerifyReadyDlg"),
                new XAttribute("Control", "Back"),
                new XAttribute("Event", "NewDialog"),
                new XAttribute("Value", "MaintenanceTypeDlg")),
            new XElement(wixNamespace + "Publish",
                new XAttribute("Dialog", "ExitDialog"),
                new XAttribute("Control", "Finish"),
                new XAttribute("Event", "EndDialog"),
                new XAttribute("Value", "Return"),
                new XAttribute("Order", "999")),
            new XElement(wixNamespace + "InstallUISequence",
                new XElement(wixNamespace + "Show",
                    new XAttribute("Dialog", "WelcomeDlg"),
                    new XAttribute("Before", "ProgressDlg"),
                    new XAttribute("Condition", "NOT Installed")),
                new XElement(wixNamespace + "Show",
                    new XAttribute("Dialog", "MaintenanceWelcomeDlg"),
                    new XAttribute("Before", "ProgressDlg"),
                    new XAttribute("Condition", "Installed AND NOT PATCH")))));

    package.Element(wixNamespace + "Feature")!.Add(new XElement(wixNamespace + "ComponentRef", new XAttribute("Id", "ProgramMenuFolder.EEVengers.Component")));
};

//Compiler.PreserveTempFiles = true;
Console.WriteLine($"Packaging '{publishDirectory}'...");
var msiPath = project.BuildMsi();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"MSI created: {msiPath}");
Console.ResetColor();