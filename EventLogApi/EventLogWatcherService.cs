using System;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;

namespace EventLogApi;

/// <summary>
/// Watches a Windows Event Log and raises events when new entries arrive.
/// Uses EventLogWatcher (Eventing.Reader).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EventLogWatcherService : IDisposable
{
    private readonly EventLogWatcher _watcher;

    /// <summary>Raised for each matching event.</summary>
    public event Action<EventRecordDto>? OnEvent;

    /// <summary>Raised when the watcher encounters an error.</summary>
    public event Action<Exception>? OnError;

    /// <param name="logName">The log to watch, e.g., "Application", "System".</param>
    /// <param name="machineName">"." for local machine or a remote machine.</param>
    /// <param name="xPathQuery">
    /// Optional XPath filter, e.g.: "*[System/Level=2]" for errors only.
    /// If null, all events in the log are watched.
    /// </param>
    public EventLogWatcherService(string logName, string machineName = ".", string? xPathQuery = null)
    {
        var session = string.Equals(machineName, ".", StringComparison.OrdinalIgnoreCase)
            ? null
            : new EventLogSession(machineName);

        EventLogQuery query = xPathQuery is null
            ? new EventLogQuery(logName, PathType.LogName) { Session = session }
            : new EventLogQuery(logName, PathType.LogName, xPathQuery) { Session = session };

        _watcher = new EventLogWatcher(query, null, false);
        _watcher.EventRecordWritten += WatcherOnEventRecordWritten;
    }

    private void WatcherOnEventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        try
        {
            if (e.EventRecord is null) return;
            using var rec = e.EventRecord;
            var dto = EventLogReader.Read(new QueryOptions { LogName = rec.LogName ?? "Application", MaxEvents = 0 }); // not used
            // Convert directly to DTO (avoid re-read)
            var mapped = Map(rec);
            OnEvent?.Invoke(mapped);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }

        static EventRecordDto Map(EventRecord rec)
        {
            string? msg = null;
            try { msg = rec.FormatDescription(); } catch { }
            return new EventRecordDto
            {
                Id = rec.Id,
                Level = rec.Level,
                LevelDisplayName = Safe(() => rec.LevelDisplayName),
                ProviderName = Safe(() => rec.ProviderName),
                LogName = Safe(() => rec.LogName),
                TimeCreated = rec.TimeCreated,
                MachineName = Safe(() => rec.MachineName),
                UserId = Safe(() => rec.UserId?.Value),
                Message = msg,
                Task = rec.Task,
                Opcode = rec.Opcode,
                Keywords = rec.Keywords,
                RawXml = Safe(rec.ToXml)
            };

            static T? Safe<T>(Func<T> f) { try { return f(); } catch { return default; } }
        }
    }

    public void Start() => _watcher.Enabled = true;
    public void Stop() => _watcher.Enabled = false;

    public void Dispose()
    {
        try
        {
            _watcher.EventRecordWritten -= WatcherOnEventRecordWritten;
            _watcher.Dispose();
        }
        catch { /* ignore */ }
    }
}
