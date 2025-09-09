using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;

namespace EventLogApi;

/// <summary>
/// Watches a Windows Event Log and raises events when new entries arrive.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class EventLogWatcherService : IDisposable
{
    private readonly string _logName;
    private readonly string _machineName;
    private readonly string? _xPathQuery;

    private EventLogSession? _session;
    private EventLogWatcher? _watcher;

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
        if (string.IsNullOrWhiteSpace(logName))
            throw new ArgumentException("logName must not be null or empty.", nameof(logName));

        _logName = logName;
        _machineName = string.IsNullOrWhiteSpace(machineName) ? "." : machineName;
        _xPathQuery = string.IsNullOrWhiteSpace(xPathQuery) ? null : xPathQuery;
    }

    /// <summary>
    /// Initialize session + watcher if not yet created.
    /// Throws EventLogApiException with clear message if something is wrong.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_watcher is not null) return;

        try
        {
            _session = string.Equals(_machineName, ".", StringComparison.OrdinalIgnoreCase)
                ? new EventLogSession()
                : new EventLogSession(_machineName);

            // Validate the log really exists on the target machine
            if (!LogExists(_session, _logName))
                throw new EventLogApiException($"Log '{_logName}' does not exist on '{_machineName}'.");

            EventLogQuery query = _xPathQuery is null
                ? new EventLogQuery(_logName, PathType.LogName)
                : new EventLogQuery(_logName, PathType.LogName, _xPathQuery);

            query.Session = _session;

            _watcher = new EventLogWatcher(query, null, false);
            _watcher.EventRecordWritten += WatcherOnEventRecordWritten;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new EventLogApiException(
                $"Access denied initializing watcher for '{_logName}' on '{_machineName}'. Try running elevated.", ex);
        }
        catch (EventLogNotFoundException ex)
        {
            throw new EventLogApiException(
                $"Event log '{_logName}' not found or inaccessible on '{_machineName}'.", ex);
        }
        catch (Exception ex)
        {
            throw new EventLogApiException(
                $"Failed to initialize EventLogWatcher for '{_logName}' on '{_machineName}'.", ex);
        }
    }

    private static bool LogExists(EventLogSession session, string logName)
    {
        // Enumerate available logs; safer than EventLog.Exists for Eventing.Reader channels
        foreach (var name in session.GetLogNames())
            if (string.Equals(name, logName, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void WatcherOnEventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        try
        {
            if (e.EventRecord is null) return;
            using var rec = e.EventRecord;

            string? msg = null;
            try { msg = rec.FormatDescription(); } catch { /* ignore formatting issues */ }

            var dto = new EventRecordDto
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

            OnEvent?.Invoke(dto);

            static T? Safe<T>(Func<T> f) { try { return f(); } catch { return default; } }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    public void Start()
    {
        EnsureInitialized();
        try
        {
            _watcher!.Enabled = true;
        }
        catch (Exception ex)
        {
            throw new EventLogApiException($"Failed to start watcher for '{_logName}' on '{_machineName}'.", ex);
        }
    }

    public void Stop()
    {
        try
        {
            if (_watcher is not null)
                _watcher.Enabled = false;
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex);
        }
    }

    public void Dispose()
    {
        try
        {
            if (_watcher is not null)
            {
                _watcher.EventRecordWritten -= WatcherOnEventRecordWritten;
                _watcher.Dispose();
                _watcher = null;
            }
        }
        catch { /* ignore */ }
        finally
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
