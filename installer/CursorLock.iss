#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
[Setup]
AppId={{9F904BBF-7891-478D-BC71-C7BAAB38410D}
AppName=CursorLock
AppVersion={#AppVersion}
AppPublisher=CursorLock
DefaultDirName={localappdata}\Programs\CursorLock
DefaultGroupName=CursorLock
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
DisableProgramGroupPage=yes
UsePreviousTasks=no
OutputDir=..\artifacts
OutputBaseFilename=CursorLock-Setup
SetupIconFile=..\src\CursorLock\Assets\CursorLock.ico
UninstallDisplayIcon={app}\CursorLock.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern dark
CloseApplications=no
RestartApplications=no
VersionInfoVersion={#AppVersion}
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "autostart"; Description: "Start CursorLock when I sign in to Windows"; GroupDescription: "Startup:";
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\CursorLock"; Filename: "{app}\CursorLock.exe"
Name: "{autodesktop}\CursorLock"; Filename: "{app}\CursorLock.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CursorLock"; ValueData: """{app}\CursorLock.exe"" --background"; Tasks: autostart; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "CursorLock"; Flags: deletevalue; Check: StartupDisabled
[Run]
Filename: "{app}\CursorLock.exe"; Description: "Launch CursorLock"; Flags: nowait postinstall skipifsilent
[Code]
function StartupDisabled: Boolean;
begin
  Result := not WizardIsTaskSelected('autostart');
end;
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectTasks then
    if FileExists(ExpandConstant('{app}\CursorLock.exe')) then
      WizardForm.TasksList.Checked[0] := RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'CursorLock');
end;
function StopCursorLock: Boolean;
var
  ResultCode: Integer;
  ExePath: String;
begin
  ExePath := ExpandConstant('{app}\CursorLock.exe');
  Result := True;
  if FileExists(ExePath) then
    Result := Exec(ExePath, '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopCursorLock then
    Result := 'CursorLock is still running. Choose Exit from its tray menu and try again.';
end;
function InitializeUninstall: Boolean;
begin
  Result := StopCursorLock;
  if not Result then
    MsgBox('Please choose Exit from the CursorLock tray menu, then retry uninstalling.', mbError, MB_OK);
end;

