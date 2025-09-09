using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace EventLogApi;

[SupportedOSPlatform("windows")]
public static class EventLogWriter
{
    /// <summary>
    /// Write a plain message to Windows Event Log.
    /// </summary>
    public static void Write(string message, EventSeverity severity, WriteOptions? options = null)
    {
        options ??= new WriteOptions();
        EventLogAdmin.EnsureSource(options.Source, options.LogName, options.MachineName, options.AutoCreateSource);

        var type = severity switch
        {
            EventSeverity.Error => EventLogEntryType.Error,
            EventSeverity.Warning => EventLogEntryType.Warning,
            _ => EventLogEntryType.Information
        };

        try
        {
            EventLog.WriteEntry(
                source: options.Source,
                message: message ?? string.Empty,
                type: type,
                eventID: options.EventId,
                category: options.Category,
                rawData: options.Data);
        }
        catch (Exception ex)
        {
            throw new EventLogApiException($"Failed to write event (source='{options.Source}', log='{options.LogName}').", ex);
        }
    }

    /// <summary>
    /// Write an exception with optional context. Exception.ToString() is included.
    /// </summary>
    public static void WriteException(Exception ex, string? contextMessage = null, WriteOptions? options = null)
    {
        options ??= new WriteOptions();
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(contextMessage))
            sb.AppendLine(contextMessage);
        sb.AppendLine(ex.ToString());
        Write(sb.ToString(), EventSeverity.Error, options);
    }

    /// <summary>
    /// Write a structured JSON payload; messageTemplate is included for readability.
    /// </summary>
    public static void WriteStructured(string messageTemplate, object payload, EventSeverity severity, WriteOptions? options = null, JsonSerializerOptions? jsonOptions = null)
    {
        options ??= new WriteOptions();
        jsonOptions ??= new JsonSerializerOptions { WriteIndented = false };

        var wrapper = new
        {
            template = messageTemplate,
            payload,
            ts = DateTimeOffset.UtcNow
        };

        string json = JsonSerializer.Serialize(wrapper, jsonOptions);
        Write(json, severity, options);
    }

    /// <summary>
    /// Convenience helpers.
    /// </summary>
    public static void Info(string message, WriteOptions? opt = null) => Write(message, EventSeverity.Information, opt);
    public static void Warn(string message, WriteOptions? opt = null) => Write(message, EventSeverity.Warning, opt);
    public static void Error(string message, WriteOptions? opt = null) => Write(message, EventSeverity.Error, opt);
}
