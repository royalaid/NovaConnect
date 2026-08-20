#include "..\artifacts\installer\version.iss"
#define SourceExe "..\artifacts\publish\single\NovaConnect.exe"
#define AppName "NovaConnect"
#define AppPublisher "NovaConnect contributors"
#define AppUrl "https://github.com/royalaid/NovaConnect"
#define AppExeName "NovaConnect.exe"

[Setup]
AppId={{A19467AF-B45A-40B1-A4B5-44A9760BF066}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=NovaConnect-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
AppMutex=Local\NovaConnect-3A01D694-8D08-4FA8-A3BF-A70888DB5F56

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autoprograms}\{#AppName}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  RunValueName = 'NovaConnect';

function InstalledStartupCommand: String;
begin
  Result := '"' + ExpandConstant('{app}\{#AppExeName}') + '" --background';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { Preserve the user's existing startup preference while moving the executable. }
  if (CurStep = ssPostInstall) and RegValueExists(HKCU, RunKey, RunValueName) then
    RegWriteStringValue(HKCU, RunKey, RunValueName, InstalledStartupCommand);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  CurrentCommand: String;
begin
  { Remove only the startup value owned by this installed copy. }
  if (CurUninstallStep = usUninstall) and
     RegQueryStringValue(HKCU, RunKey, RunValueName, CurrentCommand) and
     (CompareText(CurrentCommand, InstalledStartupCommand) = 0) then
    RegDeleteValue(HKCU, RunKey, RunValueName);
end;
