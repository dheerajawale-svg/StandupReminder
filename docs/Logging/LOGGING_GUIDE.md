# Using the Application Logger

## Quick Start

### Logging in Your Code

```csharp
using StandupReminder.App.Services;

public class MyService
{
    private readonly ILogger<MyService> _logger;
    
    public MyService()
    {
        _logger = ApplicationLogger.CreateLogger<MyService>();
    }
    
    public void DoSomething()
    {
        _logger.LogInformation("Starting operation");
        
        try
        {
            // ... your code ...
            _logger.LogInformation("Operation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed");
        }
    }
}
```

## Log Levels

Use these log levels consistently:

| Level | When to Use | Example |
|-------|-----------|---------|
| **Information** | Normal application flow | "Session locked", "Reminder started", "Settings saved" |
| **Warning** | Unexpected but non-critical | "Failed to initialize hook", "Handle unavailable" |
| **Error** | Failures affecting functionality | "WTS registration failed", exceptions |
| **Debug** | Detailed diagnostic info (disabled in prod) | Variable values, flow tracing |
| **Trace** | Very detailed info (disabled in production) | Entry/exit logging |

## Structured Logging

Always use structured logging with named parameters:

```csharp
// ✓ GOOD: Structured logging
_logger.LogInformation("Session event: {EventType} for session {SessionId}", 
    eventType, sessionId);

// ✗ AVOID: String concatenation
_logger.LogInformation($"Session event: {eventType} for session {sessionId}");
```

## Exception Logging

```csharp
try
{
    // operation
}
catch (Exception ex)
{
    // Exception is included in the log entry
    _logger.LogError(ex, "Operation failed while processing {Id}", id);
}
```

## Log File Location

Logs are written to: `%LocalAppData%\StandupReminder\Logs\app.log`

In Windows, this expands to:
- For user `john`: `C:\Users\john\AppData\Local\StandupReminder\Logs\app.log`

## Architecture

```
ILogger<T> (Injected)
    ↓
ApplicationLogger (Static Factory)
    ↓
ILoggerFactory (Singleton)
    ↓
FileLoggingProvider
    ↓
FileLogger (Thread-safe file writes)
    ↓
app.log (Persisted log file)
```

## Best Practices

1. **Always use generic `ILogger<T>`** instead of creating string categories
2. **Inject the logger** - don't create new loggers in methods
3. **Use appropriate log levels** - don't log everything as Information
4. **Include context** - use structured parameters, not string interpolation
5. **Include exceptions** - pass the exception to the logger
6. **Don't log sensitive data** - be careful with passwords, tokens, keys

## Example: Logging Session Events

```csharp
public class SessionMonitor
{
    private readonly ILogger<SessionMonitor> _logger;
    
    public SessionMonitor()
    {
        _logger = ApplicationLogger.CreateLogger<SessionMonitor>();
    }
    
    public void OnSessionLocked(int sessionId)
    {
        _logger.LogInformation(
            "Windows session locked: SessionId={SessionId}", sessionId);
    }
    
    public void OnSessionUnlocked(int sessionId)
    {
        _logger.LogInformation(
            "Windows session unlocked: SessionId={SessionId}", sessionId);
    }
    
    public void OnRegistrationFailed(int errorCode)
    {
        _logger.LogError(
            "WTSRegisterSessionNotification failed: ErrorCode=0x{ErrorCode:X}", 
            errorCode);
    }
}
```

## Troubleshooting

**Q: Where are the log files?**
A: Check `%LocalAppData%\StandupReminder\Logs\app.log`

**Q: Why aren't my logs appearing?**
A: 
- Ensure `ApplicationLogger.Initialize()` was called in `App.OnStartup()`
- Check that the log level is high enough (default: Information)
- Ensure the directory exists and is writable

**Q: Can I change log levels at runtime?**
A: Yes, if you add appsettings.json configuration support to ApplicationLogger.Initialize()

**Q: What happens if the log file can't be written?**
A: The FileLogger catches exceptions silently to prevent disrupting the application. Log writes that fail are simply skipped.
