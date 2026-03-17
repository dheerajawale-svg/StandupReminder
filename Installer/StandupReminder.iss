#define MyAppId "{{9A203D4A-8BD2-497A-BE8F-4F8046AC3A82}"
#define MyAppName "StandupReminder"
#define MyAppPublisher "StandupReminder"
#define MyAppExeName "StandupReminder.exe"
#define MyPublishDir "artifacts\\publish\\StandupReminder"

[Setup]
SourceDir=..
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion=1.0.0
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma
SolidCompression=yes
WizardStyle=modern dark
WizardImageFile=Installer\Assets\wizard-side-dark.bmp
WizardSmallImageFile=Installer\Assets\wizard-small-dark.bmp
SetupIconFile=StandupReminder.App\Assets\reminder_17382582.ico
OutputDir=artifacts\installer
OutputBaseFilename=StandupReminder-Setup
UninstallDisplayIcon={app}\{#MyAppExeName}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "StandupReminder"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--cleanup-toast"; RunOnceId: "StandupReminderToastCleanup"; Flags: runhidden waituntilterminated skipifdoesntexist

[Messages]
WelcomeLabel1=Apps By Dheeraj
WelcomeLabel2=This will install [name/ver] on your computer.%n%nStandupReminder is configured to start automatically with Windows by default. You can change this later from Windows Startup Apps.
