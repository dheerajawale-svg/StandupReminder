using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace StandupReminder.App.Services;

/// <summary>
/// Extension methods for adding file logging provider to ILoggingBuilder.
/// </summary>
internal static class FileLoggingExtensions
{
    /// <summary>
    /// Adds a simple file logging provider.
    /// Logs are appended to the specified file with timestamps.
    /// </summary>
    /// <param name="builder">The ILoggingBuilder to configure.</param>
    /// <param name="filePath">The full path to the log file.</param>
    /// <returns>The ILoggingBuilder for method chaining.</returns>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath)
    {
        builder.AddProvider(new FileLoggingProvider(filePath));
        return builder;
    }
}

/// <summary>
/// Logging provider that writes logs to a file.
/// Thread-safe and handles file creation/appending automatically.
/// </summary>
internal sealed class FileLoggingProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public FileLoggingProvider(string filePath)
    {
        _filePath = filePath;
        EnsureDirectoryExists();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(_filePath, categoryName, _lock);
    }

    public void Dispose()
    {
        // No resources to dispose
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }
}

/// <summary>
/// File-based logger implementation.
/// Writes structured log entries to a file with timestamps and log levels.
/// </summary>
internal sealed class FileLogger : ILogger
{
    private readonly string _filePath;
    private readonly string _categoryName;
    private readonly object _lock;

    public FileLogger(string filePath, string categoryName, object lockObject)
    {
        _filePath = filePath;
        _categoryName = categoryName;
        _lock = lockObject;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null; // Scopes are not supported for file logging
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Information;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);

        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{logLevel}] [{_categoryName}] {message}";

        if (exception is not null)
        {
            logEntry += Environment.NewLine + exception;
        }

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_filePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Silently fail if we can't write to the log file
                // to avoid disrupting the application
            }
        }
    }
}
