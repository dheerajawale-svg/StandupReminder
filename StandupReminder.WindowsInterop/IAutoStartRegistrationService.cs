namespace StandupReminder.WindowsInterop;

public interface IAutoStartRegistrationService
{
    bool IsRegistered();
    void Register();
    void Unregister();
}
