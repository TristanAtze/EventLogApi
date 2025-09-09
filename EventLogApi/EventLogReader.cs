using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.Versioning;

namespace EventLogApi;

[SupportedOSPlatform("windows")]
public static class EventLogReader
{
    /// <summary>
    /// Reads events from the specified Windows Event Log (classic + modern), applying simple in-memory filters.
    /// </summary>
    public static IReadOnlyList<EventRecordDto> Read(QueryOptions? options = null)
    {
        options ??= new QueryOptions();
        var list = new List<EventRecordDto>();
        var pathType = PathType.LogName;
        var query = new EventLogQuery(options.LogName, pathType)
        {
            Session = string.Equals(options.MachineName, ".", StringComparison.OrdinalIgnoreCase)
                ? null
                : new EventLogSession(options.MachineName)
        };

        using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(query);

        EventRecord? rec;
        int count = 0;
        while ((rec = reader.ReadEvent()) != null)
        {
            using (rec)
            {
                var dto = ToDto(rec);
                if (ApplyFilters(dto, options))
                {
                    list.Add(dto);
                    count++;
                    if (options.MaxEvents is int max && count >= max)
                        break;
                }
            }
        }

        // Order as requested
        if (options.NewestFirst)
            list = list.OrderByDescending(e => e.TimeCreated).ToList();
        else
            list = list.OrderBy(e => e.TimeCreated).ToList();

        return list;
    }

    /// <summary>
    /// Converts EventRecord to DTO, handling possible nulls.
    /// </summary>
    private static EventRecordDto ToDto(EventRecord rec)
    {
        string? message = null;
        try { message = rec.FormatDescription(); }
        catch { /* some providers may throw on formatting; ignore */ }

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
            Message = message,
            Task = rec.Task,
            Opcode = rec.Opcode,
            Keywords = rec.Keywords,
            RawXml = Safe(rec.ToXml)
        };

        static T? Safe<T>(Func<T> f)
        {
            try { return f(); } catch { return default; }
        }
    }

    private static bool ApplyFilters(EventRecordDto dto, QueryOptions opt)
    {
        if (opt.Since is DateTimeOffset since && dto.TimeCreated is DateTime t && t < since.UtcDateTime)
            return false;

        if (!string.IsNullOrWhiteSpace(opt.ContainsText))
        {
            var s = opt.ContainsText!;
            if ((dto.Message ?? string.Empty).IndexOf(s, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }

        if (opt.Sources is { Length: > 0 })
        {
            if (dto.ProviderName is null || !opt.Sources.Contains(dto.ProviderName, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        if (opt.Severities is { Length: > 0 })
        {
            var sev = dto.ToSeverity();
            if (sev is null || !opt.Severities.Contains(sev.Value))
                return false;
        }

        return true;
    }
}
