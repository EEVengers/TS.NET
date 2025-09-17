using Photino.NET;
using System.Diagnostics;
using System.Timers;
using Timer = System.Timers.Timer;

namespace TS.NET.Calibration.UI;

public class LiveReloadService : IDisposable
{
    private readonly PhotinoWindow window;
    private readonly FileSystemWatcher watcher;
    private readonly Timer debounceTimer;
    private readonly HashSet<string> pendingChanges;
    private readonly Lock pendingChangesLock = new();
    private readonly string loadPath;
    private bool disposed;

    public LiveReloadService(PhotinoWindow window, string watchPath, string loadPath)
    {
        this.window = window ?? throw new ArgumentNullException(nameof(window));
        this.loadPath = loadPath;
        pendingChanges = [];

        // Set up debounce timer to avoid multiple rapid refreshes
        debounceTimer = new Timer(500);
        debounceTimer.Elapsed += OnDebounceTimerElapsed;
        debounceTimer.AutoReset = false;

        watcher = new FileSystemWatcher(watchPath)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true,
            EnableRaisingEvents = false
        };

        watcher.Filters.Add("*.html");
        watcher.Filters.Add("*.js");
        watcher.Filters.Add("*.css");

        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileChanged;
        watcher.Deleted += OnFileChanged;
        watcher.Renamed += OnFileRenamed;
    }

    public void Start()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(LiveReloadService));

        watcher.EnableRaisingEvents = true;
        Debug.WriteLine($"[LiveReload] Monitoring: {watcher.Path}");
        Debug.WriteLine("[LiveReload] Watching for changes to: *.html, *.js, *.css");
    }

    public void Stop()
    {
        if (disposed)
            return;

        watcher.EnableRaisingEvents = false;
        debounceTimer.Stop();
        lock (pendingChangesLock)
        {
            pendingChanges.Clear();
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Ignore temporary files and editor backup files
        if (e.Name.Contains("~") || e.Name.Contains(".tmp"))
            return;

        lock (pendingChangesLock)
        {
            // Store just the relative path (e.Name is already relative to the watch path)
            pendingChanges.Add(e.Name);
        }

        Debug.WriteLine($"[LiveReload] Detected change: {e.Name}");

        debounceTimer.Stop();
        debounceTimer.Start();
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        OnFileChanged(sender, new FileSystemEventArgs(WatcherChangeTypes.Changed, e.FullPath, e.Name));
    }

    private void OnDebounceTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        List<string> changes;
        lock (pendingChangesLock)
        {
            if (pendingChanges.Count == 0)
                return;

            changes = new(pendingChanges);
            pendingChanges.Clear();
        }

        Debug.WriteLine($"[LiveReload] Triggering reload for {changes.Count} file(s)");

        window.Load(loadPath);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        Stop();
        watcher?.Dispose();
        debounceTimer?.Dispose();
        disposed = true;
    }
}
