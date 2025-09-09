using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Eventing.Reader;
using System.Numerics;
using System.Runtime.Versioning;

namespace EventLogApi;

[SupportedOSPlatform("windows")]
public enum EventSeverity
{
    Information = 0,
    Warning = 1,
    Error = 2
}

/// <summary>Write options for creating sources and target log routing.</summary>
[SupportedOSPlatform("windows")]
public sealed class WriteOptions
{
    public string Source { get; set; } = "EventLogApi";
    public string LogName { get; set; } = "Application";
    public string MachineName { get; set; } = ".";

    /// <summary>Automatically create source if missing (requires elevation).</summary>
    public bool AutoCreateSource { get; set; } = true;

    /// <summary>Optional Event ID (0..65535).</summary>
    public int EventId { get; set; } = 0;

    /// <summary>Optional category (0..65535).</summary>
    public short Category { get; set; } = 0;

    /// <summary>Optional binary payload.</summary>
    public byte[]? Data { get; set; }
}

/// <summary>Query options for reading Windows Event Log.</summary>
[SupportedOSPlatform("windows")]
public sealed class QueryOptions
{
    public string LogName { get; set; } = "Application";
    public string MachineName { get; set; } = ".";
    public int? MaxEvents { get; set; } = 200;        // how many to return
    public DateTimeOffset? Since { get; set; } = null; // filter by time
    public EventSeverity[]? Severities { get; set; } = null; // filter by severity
    public string[]? Sources { get; set; } = null;     // filter by provider/source name
    public string? ContainsText { get; set; } = null;  // substring search (message)
    public bool NewestFirst { get; set; } = true;      // sort order
}

/// <summary>Flattened representation of an event (Eventing.Reader).</summary>
[SupportedOSPlatform("windows")]
public sealed class EventRecordDto
{
    public int? Id { get; init; }
    public int? Level { get; init; } 
    public string? LevelDisplayName { get; init; }
    public string? ProviderName { get; init; }
    public string? LogName { get; init; }
    public DateTime? TimeCreated { get; init; }
    public string? MachineName { get; init; }
    public string? UserId { get; init; }
    public string? Message { get; init; }
    public int? Task { get; init; }
    public int? Opcode { get; init; }
    public long? Keywords { get; init; }
    public string? RawXml { get; init; }

    public EventSeverity? ToSeverity() =>
        Level switch { 2 => EventSeverity.Error, 3 => EventSeverity.Warning, 4 => EventSeverity.Information, _ => null };

    public override string ToString() =>
        $"{TimeCreated:u} [{ProviderName}] ({LevelDisplayName}) {Message}";
}

/// <summary>Exception for EventLogApi errors.</summary>
[SupportedOSPlatform("windows")]
public sealed class EventLogApiException : Exception
{
    public EventLogApiException(string message) : base(message) { }
    public EventLogApiException(string message, Exception inner) : base(message, inner) { }
}
