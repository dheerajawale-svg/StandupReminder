namespace StandupReminder.App.Services;

public interface IAutoStartRegistrationService
{
    bool IsRegistered();
    void Register();
    void Unregister();
}
