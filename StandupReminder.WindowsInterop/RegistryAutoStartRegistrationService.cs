using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;
using Microsoft.Win32;

namespace StandupReminder.WindowsInterop;

public sealed class RegistryAutoStartRegistrationService : IAutoStartRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string LegacyValueName = "StandupReminder";
    private const string TaskName = "StandupReminder Startup";
    private const string Delay = "PT30S";
    private const int TaskCreateOrUpdate = 6;
    private const int TaskLogonInteractiveToken = 3;
    private const int TaskRunLevelLeastPrivilege = 0;
    private const int TaskTriggerLogon = 9;
    private const int TaskActionExec = 0;
    private const int TaskInstancesIgnoreNew = 2;

    public bool IsRegistered()
    {
        var processPath = GetProcessPath();
        var userId = GetCurrentUserId();
        dynamic? service = null;
        dynamic? rootFolder = null;
        dynamic? task = null;

        try
        {
            service = CreateTaskService();
            rootFolder = service.GetFolder("\\");
            task = rootFolder.GetTask(TaskName);
            var taskXml = task.Xml as string;

            return MatchesExpectedRegistration(taskXml, processPath, userId);
        }
        catch (COMException ex) when ((uint)ex.HResult == 0x80070002)
        {
            return false;
        }
        finally
        {
            ReleaseComObject(task);
            ReleaseComObject(rootFolder);
            ReleaseComObject(service);
        }
    }

    public void Register()
    {
        var processPath = GetProcessPath();
        var userId = GetCurrentUserId();
        RemoveLegacyRunValue();

        dynamic? service = null;
        dynamic? rootFolder = null;
        dynamic? taskDefinition = null;
        dynamic? registrationInfo = null;
        dynamic? principal = null;
        dynamic? settings = null;
        dynamic? triggers = null;
        dynamic? trigger = null;
        dynamic? actions = null;
        dynamic? action = null;

        try
        {
            service = CreateTaskService();
            rootFolder = service.GetFolder("\\");
            taskDefinition = service.NewTask(0);

            registrationInfo = taskDefinition.RegistrationInfo;
            registrationInfo.Description = "Starts StandupReminder 30 seconds after the current user signs in.";

            principal = taskDefinition.Principal;
            principal.UserId = userId;
            principal.LogonType = TaskLogonInteractiveToken;
            principal.RunLevel = TaskRunLevelLeastPrivilege;

            settings = taskDefinition.Settings;
            settings.Enabled = true;
            settings.StartWhenAvailable = false;
            settings.DisallowStartIfOnBatteries = false;
            settings.StopIfGoingOnBatteries = false;
            settings.AllowDemandStart = true;
            settings.MultipleInstances = TaskInstancesIgnoreNew;

            triggers = taskDefinition.Triggers;
            trigger = triggers.Create(TaskTriggerLogon);
            trigger.UserId = userId;
            trigger.Delay = Delay;
            trigger.Enabled = true;

            actions = taskDefinition.Actions;
            action = actions.Create(TaskActionExec);
            action.Path = processPath;
            action.WorkingDirectory = Path.GetDirectoryName(processPath);

            rootFolder.RegisterTaskDefinition(
                TaskName,
                taskDefinition,
                TaskCreateOrUpdate,
                userId,
                null,
                TaskLogonInteractiveToken,
                null);
        }
        finally
        {
            ReleaseComObject(action);
            ReleaseComObject(actions);
            ReleaseComObject(trigger);
            ReleaseComObject(triggers);
            ReleaseComObject(settings);
            ReleaseComObject(principal);
            ReleaseComObject(registrationInfo);
            ReleaseComObject(taskDefinition);
            ReleaseComObject(rootFolder);
            ReleaseComObject(service);
        }
    }

    public void Unregister()
    {
        dynamic? service = null;
        dynamic? rootFolder = null;

        try
        {
            service = CreateTaskService();
            rootFolder = service.GetFolder("\\");
            rootFolder.DeleteTask(TaskName, 0);
        }
        catch (COMException ex) when ((uint)ex.HResult == 0x80070002)
        {
        }
        finally
        {
            RemoveLegacyRunValue();
            ReleaseComObject(rootFolder);
            ReleaseComObject(service);
        }
    }

    private static bool MatchesExpectedRegistration(string? taskXml, string processPath, string userId)
    {
        if (string.IsNullOrWhiteSpace(taskXml))
        {
            return false;
        }

        var document = XDocument.Parse(taskXml);
        XNamespace ns = document.Root?.Name.Namespace ?? XNamespace.None;

        var command = document.Root?.Element(ns + "Actions")?.Element(ns + "Exec")?.Element(ns + "Command")?.Value;
        var delay = document.Root?.Element(ns + "Triggers")?.Element(ns + "LogonTrigger")?.Element(ns + "Delay")?.Value;
        var triggerUserId = document.Root?.Element(ns + "Triggers")?.Element(ns + "LogonTrigger")?.Element(ns + "UserId")?.Value;
        var principal = document.Root?.Element(ns + "Principals")?.Element(ns + "Principal");
        var principalUserId = principal?.Element(ns + "UserId")?.Value;
        var logonType = principal?.Element(ns + "LogonType")?.Value;
        var runLevel = principal?.Element(ns + "RunLevel")?.Value;
        var multipleInstancesPolicy = document.Root?.Element(ns + "Settings")?.Element(ns + "MultipleInstancesPolicy")?.Value;

        return string.Equals(command, processPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(delay, Delay, StringComparison.OrdinalIgnoreCase)
            && string.Equals(triggerUserId, userId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(principalUserId, userId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(logonType, "InteractiveToken", StringComparison.Ordinal)
            && string.Equals(runLevel, "LeastPrivilege", StringComparison.Ordinal)
            && string.Equals(multipleInstancesPolicy, "IgnoreNew", StringComparison.Ordinal);
    }

    private static dynamic CreateTaskService()
    {
        var scheduleType = Type.GetTypeFromProgID("Schedule.Service")
            ?? throw new InvalidOperationException("The Windows Task Scheduler service is unavailable.");

        dynamic service = Activator.CreateInstance(scheduleType)
            ?? throw new InvalidOperationException("The Windows Task Scheduler service could not be created.");

        service.Connect();
        return service;
    }

    private static string GetProcessPath()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("The application executable path could not be resolved.");
        }

        return processPath;
    }

    private static string GetCurrentUserId()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrWhiteSpace(sid))
        {
            throw new InvalidOperationException("The current Windows user SID could not be resolved.");
        }

        return sid;
    }

    private static void RemoveLegacyRunValue()
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, true)
            ?? throw new InvalidOperationException("The Windows Run registry key could not be opened.");

        runKey.DeleteValue(LegacyValueName, false);
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            Marshal.FinalReleaseComObject(instance);
        }
    }
}
