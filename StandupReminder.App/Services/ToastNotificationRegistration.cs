using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;

namespace StandupReminder.App.Services;

internal static class ToastNotificationRegistration
{
    public const string AppUserModelId = "StandupReminder.App";
}

[ComVisible(true)]
[Guid("F4A71B55-EB0A-45F0-97C0-998D1B7BE059")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class ToastNotificationActivator : NotificationActivator
{
    private static readonly object SyncLock = new();
    private static EventHandler<ToastNotificationActivationReceivedEventArgs>? _activated;

    public static event EventHandler<ToastNotificationActivationReceivedEventArgs> Activated
    {
        add
        {
            lock (SyncLock)
            {
                _activated += value;

                foreach (var pending in PendingActivations)
                {
                    value?.Invoke(null, pending);
                }

                PendingActivations.Clear();
            }
        }
        remove
        {
            lock (SyncLock)
            {
                _activated -= value;
            }
        }
    }

    private static List<ToastNotificationActivationReceivedEventArgs> PendingActivations { get; } = [];

    public override void OnActivated(string arguments, NotificationUserInput userInput, string appUserModelId)
    {
        var activation = new ToastNotificationActivationReceivedEventArgs(arguments, appUserModelId);

        lock (SyncLock)
        {
            if (_activated is null)
            {
                PendingActivations.Add(activation);
                return;
            }
        }

        _activated.Invoke(this, activation);
    }
}

public sealed class ToastNotificationActivationReceivedEventArgs : EventArgs
{
    public ToastNotificationActivationReceivedEventArgs(string argument, string appUserModelId)
    {
        Argument = argument;
        AppUserModelId = appUserModelId;
    }

    public string Argument { get; }

    public string AppUserModelId { get; }
}
