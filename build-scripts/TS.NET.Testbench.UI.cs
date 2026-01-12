#!/usr/bin/dotnet

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

// TS.NET.Testbench.UI cross-platform build script using .NET script capabilities.
// Intended to be run from the build-scripts directory, e.g.:
//   dotnet TS.NET.Testbench.UI.cs
// On unix platforms, file can be executed directly if made executable

var currentDirectory = Directory.GetCurrentDirectory();
var scriptDirectory = currentDirectory;
var rootDirectory = Path.GetFullPath(Path.Combine(scriptDirectory, ".."));

var projectFolder = Path.Combine(rootDirectory, "source", "TS.NET.Testbench.UI");
var csprojPath = Path.Combine(projectFolder, "TS.NET.Testbench.UI.csproj");

if (!File.Exists(csprojPath))
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine($"Could not find project file at '{csprojPath}'.");
	Console.ResetColor();
	return;
}

string? version = TryReadVersion(csprojPath);

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"Project folder: {projectFolder}");
Console.WriteLine($"Project version: {version ?? "unknown"}");
Console.ResetColor();

var buildRoot = Path.Combine(rootDirectory, "build", "TS.NET.Testbench.UI");
Directory.CreateDirectory(buildRoot);

// Determine target runtime identifiers from command-line arguments.
var targetRids = ResolveTargetRids(args);
if (targetRids.Count == 0)
{
	return;
}

foreach (var rid in targetRids)
{
	Console.ForegroundColor = ConsoleColor.Green;
	Console.WriteLine($"Target RID: {rid}");
	Console.ResetColor();

	var success = BuildForRid(rid, rootDirectory, scriptDirectory, csprojPath, buildRoot);
	if (!success)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine($"Build failed for RID {rid}. Aborting.");
		Console.ResetColor();
		return;
	}
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Build complete");
Console.ResetColor();

// ----- Local functions -----

static IReadOnlyList<string> ResolveTargetRids(string[] args)
{
	// No rid specified = build for the current host.
	if (args.Length == 0)
	{
		var (rid, _) = GetRuntimeInfo();
		return new[] { rid };
	}

	// Expect: -rid <rid>
	if (args.Length < 2 ||
		!string.Equals(args[0], "-rid", StringComparison.OrdinalIgnoreCase))
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine("Usage: dotnet TS.NET.Testbench.UI.cs [-rid <win-x64|linux-x64|osx-x64|osx-arm64|all>]");
		Console.ResetColor();
		return Array.Empty<string>();
	}

	var ridArg = args[1];

	if (ridArg.Equals("all", StringComparison.OrdinalIgnoreCase))
	{
		return new[] { "win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64" };
	}

	var knownRids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"win-x64",
		"win-arm64",
		"linux-x64",
		"linux-arm64",
		"osx-x64",
		"osx-arm64"
	};

	if (!knownRids.Contains(ridArg))
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine($"Unknown RID '{ridArg}'. Supported values: win-x64, linux-x64, osx-x64, osx-arm64, all.");
		Console.ResetColor();
		return Array.Empty<string>();
	}

	return new[] { ridArg };
}

static bool BuildForRid(string rid, string rootDirectory, string scriptDirectory, string csprojPath, string buildRoot)
{
	var nativeLibraryFileName = GetNativeLibraryFileName(rid);
	var publishFolder = Path.Combine(buildRoot, rid);

	Console.ForegroundColor = ConsoleColor.Green;
	Console.WriteLine($"Publish folder: {publishFolder}");
	Console.ResetColor();

	// Files to preserve across publishes (if they already exist in the publish folder).
	var filesToPreserve = new List<string>
	{
		"variables.json",
		nativeLibraryFileName
	};

	var preservedFiles = new List<string>();

	foreach (var file in filesToPreserve.Distinct(StringComparer.OrdinalIgnoreCase))
	{
		var sourcePath = Path.Combine(publishFolder, file);
		if (!File.Exists(sourcePath))
		{
			continue;
		}

		var tempPath = Path.Combine(buildRoot, file);

		Console.ForegroundColor = ConsoleColor.Green;
		Console.WriteLine($"Preserving existing {file} for {rid}.");
		Console.ResetColor();

		File.Copy(sourcePath, tempPath, overwrite: true);
		preservedFiles.Add(file);
	}

	Console.ForegroundColor = ConsoleColor.Yellow;
	Console.WriteLine($"Publishing project for {rid}...");
	Console.ResetColor();

	var publishArgs = string.Join(" ", new[]
	{
		"publish",
		Quote(csprojPath),
		"-r", rid,
		"-c", "Release",
		"--self-contained",
		"/p:PublishSingleFile=true",
		"/p:PublishTrimmed=true",
		"/p:IncludeNativeLibrariesForSelfExtract=true",
		"--output",
		Quote(publishFolder)
	});

	var exitCode = RunProcess("dotnet", publishArgs, scriptDirectory);
	if (exitCode != 0)
	{
		Console.ForegroundColor = ConsoleColor.Red;
		Console.WriteLine($"dotnet publish failed for {rid} with exit code {exitCode}.");
		Console.ResetColor();
		return false;
	}

	// Remove debug symbols.
	if (Directory.Exists(publishFolder))
	{
		foreach (var pdb in Directory.EnumerateFiles(publishFolder, "*.pdb", SearchOption.TopDirectoryOnly))
		{
			try
			{
				File.Delete(pdb);
			}
			catch
			{
				// Ignore failures when cleaning up debug symbols.
			}
		}
	}

	// Restore preserved files back into the publish folder.
	foreach (var file in preservedFiles)
	{
		var tempPath = Path.Combine(buildRoot, file);
		var destPath = Path.Combine(publishFolder, file);

		if (!File.Exists(tempPath))
		{
			continue;
		}

		File.Copy(tempPath, destPath, overwrite: true);
		File.Delete(tempPath);
	}

	// Repeat publish folder message to make it more visible after build.
	Console.ForegroundColor = ConsoleColor.Green;
	Console.WriteLine($"Publish folder: {publishFolder}");
	Console.ResetColor();

	return true;
}

static string GetNativeLibraryFileName(string rid)
{
	if (rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
	{
		return "tslitex.dll";
	}

	// Linux and macOS use the same library name in existing scripts.
	return "libtslitex.so";
}

static (string rid, string nativeLibraryFileName) GetRuntimeInfo()
{
	if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
	{
		return ("win-x64", "tslitex.dll");
	}

	if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
	{
		return ("linux-x64", "libtslitex.so");
	}

	if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
	{
		var arch = RuntimeInformation.OSArchitecture;
		var rid = arch == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
		return (rid, "libtslitex.so");
	}

	throw new NotSupportedException($"Unsupported OS platform: {RuntimeInformation.OSDescription}");
}

static string? TryReadVersion(string csprojPath)
{
	try
	{
		var doc = XDocument.Load(csprojPath);
		var versionElement = doc
			.Descendants()
			.FirstOrDefault(e => string.Equals(e.Name.LocalName, "Version", StringComparison.OrdinalIgnoreCase));

		return versionElement?.Value?.Trim();
	}
	catch
	{
		return null;
	}
}

static int RunProcess(string fileName, string arguments, string workingDirectory)
{
	using var process = new Process
	{
		StartInfo = new ProcessStartInfo
		{
			FileName = fileName,
			Arguments = arguments,
			WorkingDirectory = workingDirectory,
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		}
	};

	process.OutputDataReceived += (_, e) =>
	{
		if (e.Data is not null)
		{
			Console.WriteLine(e.Data);
		}
	};

	process.ErrorDataReceived += (_, e) =>
	{
		if (e.Data is not null)
		{
			Console.Error.WriteLine(e.Data);
		}
	};

	process.Start();
	process.BeginOutputReadLine();
	process.BeginErrorReadLine();
	process.WaitForExit();

	return process.ExitCode;
}

static string Quote(string path)
{
	if (string.IsNullOrEmpty(path))
	{
		return path;
	}

	if (path.Contains('"'))
	{
		return path;
	}

	if (path.Contains(' '))
	{
		return $"\"{path}\"";
	}

	return path;
}

