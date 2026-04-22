using System.IO;
using System.Text.Json;
using StandupReminder.App.Models;

namespace StandupReminder.App.Services;

public sealed class LocalAppDataReminderRuntimeStateStore : IReminderRuntimeStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _runtimeStateFilePath;

    public LocalAppDataReminderRuntimeStateStore()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StandupReminder");

        _runtimeStateFilePath = Path.Combine(dataDirectory, "runtime-state.json");
    }

    public ReminderRuntimeStateLoadResult Load()
    {
        if (!File.Exists(_runtimeStateFilePath))
        {
            return new ReminderRuntimeStateLoadResult(null, null);
        }

        try
        {
            var json = File.ReadAllText(_runtimeStateFilePath);
            var state = JsonSerializer.Deserialize<ReminderRuntimeState>(json, SerializerOptions);
            if (!IsValid(state))
            {
                return CreateFallbackResult();
            }

            return new ReminderRuntimeStateLoadResult(state, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return CreateFallbackResult();
        }
    }

    public void Save(ReminderRuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var directoryPath = Path.GetDirectoryName(_runtimeStateFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var tempFilePath = $"{_runtimeStateFilePath}.{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(state, SerializerOptions);

        try
        {
            File.WriteAllText(tempFilePath, json);

            if (File.Exists(_runtimeStateFilePath))
            {
                File.Replace(tempFilePath, _runtimeStateFilePath, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempFilePath, _runtimeStateFilePath);
            }
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_runtimeStateFilePath))
            {
                File.Delete(_runtimeStateFilePath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private ReminderRuntimeStateLoadResult CreateFallbackResult()
    {
        return new ReminderRuntimeStateLoadResult(
            null,
            $"Could not read reminder runtime state from {_runtimeStateFilePath}. Starting a fresh reminder cycle.");
    }

    private static bool IsValid(ReminderRuntimeState? state)
    {
        if (state is null || state.SchemaVersion != ReminderRuntimeState.CurrentSchemaVersion)
        {
            return false;
        }

        if (!Enum.IsDefined(state.Phase)
            || !Enum.IsDefined(state.PhaseBeforePause)
            || !Enum.IsDefined(state.PhaseBeforeManualPause))
        {
            return false;
        }

        if (state.RemainingTime < TimeSpan.Zero
            || state.LastStartedSittingDuration < TimeSpan.Zero
            || state.PendingRecurringSitExtension < TimeSpan.Zero
            || state.SnoozedDuration < TimeSpan.Zero)
        {
            return false;
        }

        return true;
    }
}
