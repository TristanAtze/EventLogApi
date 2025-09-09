using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        var machine = string.IsNullOrWhiteSpace(options.MachineName) ? "." : options.MachineName;
        var logName = string.IsNullOrWhiteSpace(options.LogName) ? "Application" : options.LogName;

        // 1) Fail fast if the log doesn't exist (avoids obscure NREs deep inside EventLogReader)
        try
        {
            if (!EventLog.Exists(logName, machine))
                throw new EventLogApiException($"Log '{logName}' not found on '{machine}'.");
        }
        catch (Exception ex)
        {
            throw new EventLogApiException($"Failed to verify existence of log '{logName}' on '{machine}'.", ex);
        }

        using var session =
            string.Equals(machine, ".", StringComparison.OrdinalIgnoreCase)
                ? new EventLogSession()
                : new EventLogSession(machine);

        var query = new EventLogQuery(logName, PathType.LogName)
        {
            Session = session
        };

        var list = new List<EventRecordDto>();
        try
        {
            using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(query);

            int count = 0;
            while (true)
            {
                EventRecord? rec;
                try
                {
                    rec = reader.ReadEvent();
                }
                catch (EventLogNotFoundException ex)
                {
                    // Kanal wurde unterwegs unzugänglich → sauber beenden, statt NRE
                    throw new EventLogApiException($"Log '{logName}' wurde während des Lesens unzugänglich.", ex);
                }
                catch (EventLogException ex)
                {
                    throw new EventLogApiException($"Fehler beim Lesen aus '{logName}'.", ex);
                }

                if (rec is null) break;

                using (rec)
                {
                    var dto = ToDto(rec);
                    if (ApplyFilters(dto, options))
                    {
                        list.Add(dto);
                        count++;
                        if (options.MaxEvents is int max && count >= max) break;
                    }
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new EventLogApiException($"Zugriff verweigert auf '{logName}' (Maschine: '{machine}'). Bitte als Administrator ausführen.", ex);
        }
        catch (NullReferenceException ex)
        {
            // Some environments can throw NRE inside EventLogReader if session/log are odd.
            throw new EventLogApiException($"Internal reader error for '{logName}' on '{machine}'.", ex);
        }
        catch (Exception ex)
        {
            throw new EventLogApiException($"Unexpected error while reading '{logName}' on '{machine}'.", ex);
        }

        // Order as requested
        list = (options.NewestFirst
            ? list.OrderByDescending(e => e.TimeCreated)
            : list.OrderBy(e => e.TimeCreated)).ToList();

        return list;
    }

    /// <summary>Converts EventRecord to DTO, handling possible nulls.</summary>
    private static EventRecordDto ToDto(EventRecord rec)
    {
        string? message = null;
        try { message = rec.FormatDescription(); } catch { /* some providers may throw on formatting; ignore */ }

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