# EventLogApi

A modern .NET wrapper for the **Windows Event Log**.  
It allows you to **write**, **read**, and **watch** events in a strongly typed and developer-friendly way.

## ✨ Features
- **Write events** (Information, Warning, Error) with optional structured JSON payload
- **Read events** with filtering (by severity, provider, text, time, etc.)
- **Watch events** in real-time via `EventLogWatcherService`
- Provides a **flattened DTO** (`EventRecordDto`) with safe access to all key fields
- Clear error handling via `EventLogApiException`

## 📦 Installation
Install via NuGet:

```bash
dotnet add package EventLogApi
````

Or via the Visual Studio NuGet Package Manager.

## 🚀 Usage

### Write Events

```csharp
using EventLogApi;

var writeOpts = new WriteOptions
{
    Source = "MyApp",
    LogName = "Application",
    AutoCreateSource = true // requires admin on first run
};

EventLogWriter.Info("Hello World (Information)", writeOpts);
EventLogWriter.Warn("Heads-up (Warning)", writeOpts);
EventLogWriter.Error("Something failed (Error)", writeOpts);

// Write an exception
try
{
    throw new InvalidOperationException("Something broke");
}
catch (Exception ex)
{
    EventLogWriter.WriteException(ex, "Error while processing request", writeOpts);
}

// Write structured payload
EventLogWriter.WriteStructured(
    "User {UserId} performed {Action}",
    new { UserId = 42, Action = "Login" },
    EventSeverity.Information,
    writeOpts);
```

### Read Events

```csharp
using EventLogApi;

var options = new QueryOptions
{
    LogName = "Application",
    MaxEvents = 20,
    ContainsText = "MyApp",
    NewestFirst = true
};

var events = EventLogReader.Read(options);

foreach (var e in events)
{
    Console.WriteLine($"{e.TimeCreated:u} [{e.LevelDisplayName}] {e.ProviderName} - {e.Message}");
}
```

### Watch Events in Real-Time

```csharp
using EventLogApi;

using var watcher = new EventLogWatcherService("Application", ".", "*[System/Level=2]"); // errors only

watcher.OnEvent += e =>
{
    Console.WriteLine($"[WATCH] {e.TimeCreated:u} {e.ProviderName}: {e.Message}");
};

watcher.OnError += ex =>
{
    Console.WriteLine($"[WATCH][ERROR] {ex.Message}");
};

watcher.Start();
Console.ReadKey(); // keep alive
```

## 📖 API Overview

* **`EventLogWriter`**

  * `Info(string message, WriteOptions? opt = null)`
  * `Warn(string message, WriteOptions? opt = null)`
  * `Error(string message, WriteOptions? opt = null)`
  * `WriteException(Exception ex, string? contextMessage = null, WriteOptions? options = null)`
  * `WriteStructured(string messageTemplate, object payload, EventSeverity severity, WriteOptions? options = null)`

* **`EventLogReader.Read(QueryOptions options)`**

  * Reads events with filtering (by time, text, severity, provider, etc.)

* **`EventLogWatcherService`**

  * Watches a log in real-time and raises `OnEvent` for new entries

* **`EventRecordDto`**

  * Flattened event with Id, Level, ProviderName, TimeCreated, Message, etc.

* **`EventLogApiException`**

  * Clear exception type for all API errors

## ✅ Supported Platforms

* Windows 10, Windows 11, Windows Server
* .NET 6, .NET 7, .NET 8

## ⚠️ Notes

* Creating a new source requires **Administrator rights** on first run
* Reading certain logs (like *Security*) requires elevated permissions
* The API uses `System.Diagnostics.EventLog` and `System.Diagnostics.Eventing.Reader` internally

## 📜 License

MIT License