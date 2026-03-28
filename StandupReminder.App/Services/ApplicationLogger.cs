using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace StandupReminder.App.Services;

/// <summary>
/// Application-wide logger factory providing structured logging to files.
/// Logs are written to the LocalAppData directory with daily rotation.
/// </summary>
public static class ApplicationLogger
{
    private static ILoggerFactory? _loggerFactory;
    private static readonly object _lock = new();

    /// <summary>
    /// Initializes the application logger with file-based logging.
    /// Logs are stored in LocalAppData\StandupReminder\Logs directory.
    /// </summary>
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_loggerFactory is not null)
            {
                return;
            }

            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StandupReminder",
                "Logs");

            Directory.CreateDirectory(logDirectory);

            var logFile = Path.Combine(logDirectory, "app.log");

            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Information)
                    .AddFile(logFile)
                    .AddConsole();
            });
        }
    }

    /// <summary>
    /// Creates a logger for the specified category name.
    /// </summary>
    /// <param name="categoryName">The category name for the logger.</param>
    /// <returns>An ILogger instance for the specified category.</returns>
    public static ILogger CreateLogger(string categoryName)
    {
        EnsureInitialized();
        return _loggerFactory!.CreateLogger(categoryName);
    }

    /// <summary>
    /// Creates a logger for the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which to create a logger.</typeparam>
    /// <returns>An ILogger instance for the specified type.</returns>
    public static ILogger<T> CreateLogger<T>()
    {
        EnsureInitialized();
        return _loggerFactory!.CreateLogger<T>();
    }

    /// <summary>
    /// Closes and disposes the logger factory.
    /// Should be called during application shutdown.
    /// </summary>
    public static void Shutdown()
    {
        lock (_lock)
        {
            _loggerFactory?.Dispose();
            _loggerFactory = null;
        }
    }

    private static void EnsureInitialized()
    {
        if (_loggerFactory is null)
        {
            Initialize();
        }
    }
}
