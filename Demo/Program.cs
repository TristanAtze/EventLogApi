using System;
using EventLogApi;

namespace Demo;

internal static class Program
{
    private static void Main()
    {
        Console.WriteLine("EventLogApi Demo\n");

        // --- WRITE DEMO ---
        var writeOpts = new WriteOptions
        {
            Source = "EventLogApi.Demo",
            LogName = "Application",
            AutoCreateSource = true // requires admin on first run to create the source
        };

        try
        {
            EventLogWriter.Info("Hello from EventLogApi (Information)", writeOpts);
            EventLogWriter.Warn("Heads-up from EventLogApi (Warning)", writeOpts);
            EventLogWriter.Error("Something went wrong (Error)", writeOpts);

            try
            {
                ThrowSomething();
            }
            catch (Exception ex)
            {
                EventLogWriter.WriteException(ex, "Demo caught exception", writeOpts);
            }

            EventLogWriter.WriteStructured(
                "User {UserId} performed {Action}",
                new { UserId = 42, Action = "Login" },
                EventSeverity.Information,
                writeOpts);
        }
        catch (EventLogApiException ex)
        {
            Console.WriteLine($"WRITE ERROR: {ex.Message}");
            if (ex.InnerException is not null) Console.WriteLine(ex.InnerException);
        }

        // --- READ DEMO ---
        Console.WriteLine("\nReading last 20 Application events that contain 'EventLogApi'...\n");
        var q = new QueryOptions
        {
            LogName = "Application",
            MaxEvents = 20,
            ContainsText = "EventLogApi",
            NewestFirst = true
        };
        try
        {
            var events = EventLogReader.Read(q);
            foreach (var e in events)
            {
                Console.WriteLine($"{e.TimeCreated:u} [{e.LevelDisplayName}] {e.ProviderName} - {e.Message}");
            }
        }
        catch (EventLogApiException ex)
        {
            Console.WriteLine($"READ ERROR: {ex.Message}");
        }

        // --- WATCH DEMO ---
        Console.WriteLine("\nWatching Application log (errors only). Press any key to exit.\n");
        using var watcher = new EventLogWatcherService("Application", ".", "*[System/Level=2]");
        watcher.OnEvent += e =>
        {
            Console.WriteLine($"[WATCH][ERROR] {e.TimeCreated:u} {e.ProviderName}: {e.Message}");
        };
        watcher.OnError += ex =>
        {
            Console.WriteLine($"[WATCH][ERROR] {ex.Message}");
        };
        watcher.Start();

        Console.ReadKey(); // keep console open per your preference
    }

    private static void ThrowSomething()
    {
        throw new InvalidOperationException("Demo exception from EventLogApi");
    }
}
