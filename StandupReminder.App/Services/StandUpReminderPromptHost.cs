using StandupReminder.Core.Services;

namespace StandupReminder.App.Services;

internal sealed class StandUpReminderPromptHost : IReminderPromptHost, IDisposable
{
    private StandUpReminderWindow? _promptWindow;

    public event EventHandler? Confirmed;

    public event EventHandler? Snoozed;

    public bool IsVisible => _promptWindow is not null;

    public void Show(TimeSpan standDuration, string backgroundArgbHex)
    {
        if (_promptWindow is not null)
        {
            _promptWindow.UpdateStandDuration(standDuration);
            _promptWindow.UpdateBackground(backgroundArgbHex);
            return;
        }

        _promptWindow = new StandUpReminderWindow(standDuration, backgroundArgbHex);
        _promptWindow.Confirmed += OnPromptConfirmed;
        _promptWindow.Snoozed += OnPromptSnoozed;
        _promptWindow.Closed += OnPromptClosed;
        _promptWindow.Show();
    }

    public void Activate()
    {
        _promptWindow?.Activate();
    }

    public void UpdateStandDuration(TimeSpan standDuration)
    {
        _promptWindow?.UpdateStandDuration(standDuration);
    }

    public void UpdateBackground(string backgroundArgbHex)
    {
        _promptWindow?.UpdateBackground(backgroundArgbHex);
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
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    private void OnPromptSnoozed(object? sender, EventArgs e)
    {
        Snoozed?.Invoke(this, EventArgs.Empty);
    }

    private void OnPromptClosed(object? sender, EventArgs e)
    {
        _ = e;

        if (sender is StandUpReminderWindow promptWindow)
        {
            DetachPrompt(promptWindow);

            if (ReferenceEquals(_promptWindow, promptWindow))
            {
                _promptWindow = null;
            }
        }
    }

    private void ClosePrompt(Action<StandUpReminderWindow> closeAction)
    {
        if (_promptWindow is not StandUpReminderWindow promptWindow)
        {
            return;
        }

        DetachPrompt(promptWindow);
        _promptWindow = null;
        closeAction(promptWindow);
    }

    private void DetachPrompt(StandUpReminderWindow promptWindow)
    {
        promptWindow.Confirmed -= OnPromptConfirmed;
        promptWindow.Snoozed -= OnPromptSnoozed;
        promptWindow.Closed -= OnPromptClosed;
    }
}