using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace EventLogApi;

[SupportedOSPlatform("windows")]
internal static class EventLogAdmin
{
    private static readonly ConcurrentDictionary<string, bool> _sourceChecked = new();

    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void EnsureSource(string source, string logName, string machineName, bool autoCreate)
    {
        var key = $"{machineName}::{source}";
        if (_sourceChecked.ContainsKey(key)) return;

        try
        {
            if (!EventLog.SourceExists(source, machineName))
            {
                if (!autoCreate)
                    throw new EventLogApiException($"Event source '{source}' does not exist on '{machineName}'. Enable AutoCreateSource or create it manually.");

                if (!IsElevated())
                    throw new EventLogApiException($"Event source '{source}' missing and process is not elevated. Run as Administrator or pre-create the source.");

                var data = new EventSourceCreationData(source, logName)
                {
                    MachineName = machineName
                };
                EventLog.CreateEventSource(data);
            }

            _sourceChecked[key] = true;
        }
        catch (Exception ex)
        {
            throw new EventLogApiException($"Failed to ensure event source '{source}' on '{machineName}'.", ex);
        }
    }
}
