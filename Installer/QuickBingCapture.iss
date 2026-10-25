; Inno Setup 6 script for Quick Bing Capture
; Build with:  ISCC.exe Installer\QuickBingCapture.iss   (or just run build.ps1)

#define MyAppName      "Quick Bing Capture"
#define MyAppVersion   "1.1.0"
#define MyAppPublisher "Heshan Induwara"
#define MyAppURL       "https://github.com/hinduwara-devsci"
#define MyAppExeName   "QuickBingCapture.exe"
#define RunKey         "Software\Microsoft\Windows\CurrentVersion\Run"

[Setup]
AppId={{7D3F5B1E-2C84-4A6B-9E0D-5B8A1C4F2E97}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright=Copyright (c) 2026 {#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup

; Per-user install: no administrator prompt, installs to %LocalAppData%\Programs
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
MinVersion=10.0

LicenseFile=..\LICENSE.txt
SetupIconFile=..\QuickBingCapture\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

OutputDir=..\dist
OutputBaseFilename=QuickBingCapture-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start {#MyAppName} automatically when I sign in to Windows (recommended)"; GroupDescription: "Startup:"
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Same value the app's own "Start with Windows" menu item writes.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "QuickBingCapture"; ValueData: """{app}\{#MyAppExeName}"" --autostart"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{%TEMP}\QuickBingCapture"

[Code]
function IsDotNet48Installed: Boolean;
var
  Release: Cardinal;
begin
  Release := 0;
  RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release);
  Result := Release >= 528040;
end;

procedure StopRunningApp;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not IsDotNet48Installed then
    Result := MsgBox('{#MyAppName} needs .NET Framework 4.8, which is built into Windows 10 (version 1903 or newer) and Windows 11.' + #13#10 +
                     'It was not detected on this PC. Continue anyway?', mbConfirmation, MB_YESNO) = IDYES;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp;
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Re-installing with the startup box unticked should turn auto-start off.
  if (CurStep = ssPostInstall) and (not WizardIsTaskSelected('startup')) then
    RegDeleteValue(HKCU, '{#RunKey}', 'QuickBingCapture');
end;

function InitializeUninstall: Boolean;
begin
  StopRunningApp;
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Also covers the case where the user enabled startup later from the tray menu.
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKCU, '{#RunKey}', 'QuickBingCapture');
end;
