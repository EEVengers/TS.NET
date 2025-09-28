using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;

namespace TS.NET.Photino;

public class PhotinoServer
{
    public static WebApplication CreateStaticFileServer(
        string[] args,
        out string baseUrl)
    {
        return CreateStaticFileServer(
            args,
            startPort: 8000,
            portRange: 100,
            webRootFolder: "wwwroot",
            out baseUrl);
    }

    public static WebApplication CreateStaticFileServer(
        string[] args,
        int startPort,
        int portRange,
        string webRootFolder,
        out string baseUrl)
    {
        // This varies from the version in the PhotinoServer package as it won't create a wwwroot folder, and 
        // only creates a PhysicalFileProvider if the folder exists

        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();

        //Try to read files from the embedded resources - from a slightly different path, prefixed with Resources/
        var manifestEmbeddedFileProvider =
            new ManifestEmbeddedFileProvider(
                System.Reflection.Assembly.GetEntryAssembly(),
                $"Resources/{webRootFolder}");

        var physicalWebRoot = Path.Combine(builder.Environment.ContentRootPath, webRootFolder);
        IFileProvider physicalFileProvider;

        if (Directory.Exists(physicalWebRoot))
        {
            physicalFileProvider = new PhysicalFileProvider(physicalWebRoot);
        }
        else
        {
            physicalFileProvider = new NullFileProvider();
        }

        //Try to read from disk first, if not found, try to read from embedded resources.
        CompositeFileProvider compositeWebProvider
            = new(physicalFileProvider, manifestEmbeddedFileProvider);

        builder.Environment.WebRootFileProvider = compositeWebProvider;
        builder.Environment.WebRootPath = webRootFolder;

        int port = startPort;

        // Try ports until available port is found
        while (IPGlobalProperties
            .GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Any(x => x.Port == port))
        {
            if (port > port + portRange)
                throw new SystemException($"Couldn't find open port within range {port - portRange} - {port}.");
            port++;
        }

        baseUrl = $"http://localhost:{port}";

        builder.WebHost.UseUrls(baseUrl);

        WebApplication app = builder.Build();
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            DefaultContentType = "text/plain"
        });

        return app;
    }
}