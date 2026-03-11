using Microsoft.UI.Dispatching;
using StandupReminder.Core.Services;

namespace StandupReminder.WinUI;

internal sealed class StandUpReminderPromptHost : IReminderPromptHost, IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly object _syncRoot = new();
    private StandUpReminderWindow? _promptWindow;

    public StandUpReminderPromptHost()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("Stand-up prompt host must be created on the WinUI dispatcher thread.");
    }

    public event EventHandler? Confirmed;
    public event EventHandler? Snoozed;

    public bool IsVisible
    {
        get
        {
            lock (_syncRoot)
            {
                return _promptWindow is not null;
            }
        }
    }

    public void Show(TimeSpan standDuration, string backgroundArgbHex)
    {
        RunOnUiThread(() =>
        {
            if (TryGetPromptWindow(out var promptWindow))
            {
                promptWindow.UpdateStandDuration(standDuration);
                promptWindow.UpdateBackground(backgroundArgbHex);
                return;
            }

            promptWindow = new StandUpReminderWindow(standDuration, backgroundArgbHex);
            promptWindow.Confirmed += OnPromptConfirmed;
            promptWindow.Snoozed += OnPromptSnoozed;
            promptWindow.Closed += OnPromptClosed;

            lock (_syncRoot)
            {
                _promptWindow = promptWindow;
            }

            promptWindow.Activate();
        });
    }

    public void Activate()
    {
        RunOnUiThread(() =>
        {
            if (TryGetPromptWindow(out var promptWindow))
            {
                promptWindow.Activate();
            }
        });
    }

    public void UpdateStandDuration(TimeSpan standDuration)
    {
        RunOnUiThread(() =>
        {
            if (TryGetPromptWindow(out var promptWindow))
            {
                promptWindow.UpdateStandDuration(standDuration);
            }
        });
    }

    public void UpdateBackground(string backgroundArgbHex)
    {
        RunOnUiThread(() =>
        {
            if (TryGetPromptWindow(out var promptWindow))
            {
                promptWindow.UpdateBackground(backgroundArgbHex);
            }
        });
    }

    public void DismissForLock()
    {
        ClosePrompt(window => window.DismissForLock());
    }

    public void DismissForPause()
    {
        ClosePrompt(window => window.DismissForPause());
    }

    public void DismissForShutdown()
    {
        ClosePrompt(window => window.DismissForShutdown());
    }

    public void Dispose()
    {
        DismissForShutdown();
    }

    private void OnPromptConfirmed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    private void OnPromptSnoozed(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        Snoozed?.Invoke(this, EventArgs.Empty);
    }

    private void OnPromptClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs e)
    {
        _ = e;

        if (sender is not StandUpReminderWindow promptWindow)
        {
            return;
        }

        DetachPrompt(promptWindow);

        lock (_syncRoot)
        {
            if (ReferenceEquals(_promptWindow, promptWindow))
            {
                _promptWindow = null;
            }
        }
    }

    private void ClosePrompt(Action<StandUpReminderWindow> closeAction)
    {
        RunOnUiThread(() =>
        {
            if (!TryGetPromptWindow(out var promptWindow))
            {
                return;
            }

            DetachPrompt(promptWindow);

            lock (_syncRoot)
            {
                _promptWindow = null;
            }

            closeAction(promptWindow);
        });
    }

    private bool TryGetPromptWindow(out StandUpReminderWindow promptWindow)
    {
        lock (_syncRoot)
        {
            if (_promptWindow is null)
            {
                promptWindow = null!;
                return false;
            }

            promptWindow = _promptWindow;
            return true;
        }
    }

    private void DetachPrompt(StandUpReminderWindow promptWindow)
    {
        promptWindow.Confirmed -= OnPromptConfirmed;
        promptWindow.Snoozed -= OnPromptSnoozed;
        promptWindow.Closed -= OnPromptClosed;
    }

    private void RunOnUiThread(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        Exception? exception = null;
        using var completed = new ManualResetEventSlim(false);

        if (!_dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
                finally
                {
                    completed.Set();
                }
            }))
        {
            throw new InvalidOperationException("Failed to marshal prompt work to the WinUI dispatcher.");
        }

        completed.Wait();

        if (exception is not null)
        {
            throw exception;
        }
    }
}