using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Content.Redactor.Redactor;

/// <summary>
/// Watches the prototypes directory for external changes (file edits, renames,
/// deletions, folder operations) and emits debounced change events.
/// </summary>
/// <remarks>
/// File-system watchers commonly fire several events for a single logical save.
/// Events are coalesced per-path within a short window to avoid spamming
/// connected clients and re-indexing the same file repeatedly.
/// </remarks>
internal sealed class FileWatcherService : IDisposable
{
    private readonly string _root;
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, Timer> _pending = new();
    private readonly ConcurrentDictionary<string, DateTime> _suppress = new();
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan SuppressWindow = TimeSpan.FromSeconds(5);

    public event Action<FileChangeEvent>? Changed;

    public FileWatcherService(string prototypesDir)
    {
        _root = Path.GetFullPath(prototypesDir);
        _watcher = new FileSystemWatcher(_root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
            EnableRaisingEvents = false,
        };
        _watcher.Filters.Add("*.yml");
        _watcher.Filters.Add("*.yaml");
        _watcher.Changed += (_, e) => Schedule(e.FullPath, FileChangeKind.Changed);
        _watcher.Created += (_, e) => Schedule(e.FullPath, FileChangeKind.Created);
        _watcher.Deleted += (_, e) => Schedule(e.FullPath, FileChangeKind.Deleted);
        _watcher.Renamed += (_, e) =>
        {
            Schedule(e.OldFullPath, FileChangeKind.Deleted);
            Schedule(e.FullPath, FileChangeKind.Created);
        };
        _watcher.Error += (_, e) =>
            Console.Error.WriteLine($"[Redactor] FileWatcher error: {e.GetException().Message}");
    }

    public void Start() => _watcher.EnableRaisingEvents = true;

    /// <summary>
    /// Tells the watcher to ignore the next change event for the supplied
    /// path. Called immediately after the redactor itself writes to a file so
    /// connected clients don't receive a self-induced "external change" notice.
    /// </summary>
    public void SuppressNext(string fullPath)
    {
        _suppress[Path.GetFullPath(fullPath)] = DateTime.UtcNow;
    }

    private void Schedule(string fullPath, FileChangeKind kind)
    {
        var canonical = Path.GetFullPath(fullPath);
        // A single redactor write usually produces several FileSystemWatcher
        // events (Created+Changed, or multiple Changed).  Keep the suppression
        // timestamp until the window elapses so every event in that burst is
        // filtered, not just the first one.
        if (_suppress.TryGetValue(canonical, out var suppressedAt))
        {
            if (DateTime.UtcNow - suppressedAt < SuppressWindow)
                return;
            _suppress.TryRemove(canonical, out _);
        }

        _pending.AddOrUpdate(
            canonical,
            _ => new Timer(_ => Fire(canonical, kind), null, DebounceDelay, Timeout.InfiniteTimeSpan),
            (_, existing) =>
            {
                existing.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
                return existing;
            });
    }

    private void Fire(string fullPath, FileChangeKind kind)
    {
        if (_pending.TryRemove(fullPath, out var timer))
            timer.Dispose();

        string rel;
        try
        {
            rel = Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
        }
        catch
        {
            return;
        }
        if (rel.StartsWith("..", StringComparison.Ordinal))
            return;

        try
        {
            Changed?.Invoke(new FileChangeEvent(kind, rel, fullPath));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Redactor] FileWatcher listener error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        foreach (var (_, timer) in _pending) timer.Dispose();
        _pending.Clear();
    }
}

internal enum FileChangeKind
{
    Created,
    Changed,
    Deleted,
}

internal readonly record struct FileChangeEvent(FileChangeKind Kind, string RelativePath, string FullPath);
