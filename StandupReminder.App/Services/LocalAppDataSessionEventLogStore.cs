using System.IO;
using System.Text.Json;
using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public sealed class LocalAppDataSessionEventLogStore : ISessionEventLogStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _logFilePath;

    public LocalAppDataSessionEventLogStore()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StandupReminder");

        _logFilePath = Path.Combine(dataDirectory, "session-events.json");
    }

    public IReadOnlyList<SessionEventLogEntry> Load()
    {
        if (!File.Exists(_logFilePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_logFilePath);
            var entries = JsonSerializer.Deserialize<List<SessionEventLogEntry>>(json, SerializerOptions);
            return entries ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public void Save(IReadOnlyCollection<SessionEventLogEntry> entries)
    {
        var directoryPath = Path.GetDirectoryName(_logFilePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonSerializer.Serialize(entries, SerializerOptions);
        File.WriteAllText(_logFilePath, json);
    }
}
