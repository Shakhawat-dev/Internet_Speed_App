; Inno Setup script for Internet Speed Monitor
;
; Two variants build from this one script:
;
;   Full (self-contained, ~60 MB, no prerequisites):
;     dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
;     ISCC installer\installer.iss
;
;   Lite (framework-dependent, ~1 MB, needs .NET 8 Desktop Runtime on the target):
;     dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o bin\Release\fdd-publish
;     ISCC /DLite installer\installer.iss

#define MyAppName      "Internet Speed Monitor"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "Md. Shakhawat Hossain Shahin"
#define MyAppURL       "https://github.com/shakhawat-dev/Internet_Speed_App"
#define MyAppExeName   "InternetSpeedApp.exe"

#ifdef Lite
  #define PublishDir   "..\bin\Release\fdd-publish"
  #define OutputSuffix "-lite"
#else
  #define PublishDir   "..\bin\Release\net8.0-windows\win-x64\publish"
  #define OutputSuffix ""
#endif

[Setup]
; Fixed GUID so upgrades replace the existing install instead of duplicating it
AppId={{8B1F2E63-9C4A-4D7B-A2E5-6F3D8C9B1A47}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-user install — no admin prompt; {autopf} resolves to %LOCALAPPDATA%\Programs
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=InternetSpeedMonitor-Setup-{#MyAppVersion}{#OutputSuffix}
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Gracefully close a running instance before install/uninstall
CloseApplications=yes
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart";   Description: "Start automatically with Windows";  GroupDescription: "Startup:"

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}";       Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Autostart entry, removed automatically on uninstall
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "InternetSpeedApp"; ValueData: """{app}\{#MyAppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Make sure no instance is holding the exe when files are removed
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; Flags: runhidden skipifdoesntexist; RunOnceId: "KillApp"

[UninstallDelete]
; Offer nothing silently destructive: settings/usage stay unless the user removes them,
; but clean up the empty install dir
Type: dirifempty; Name: "{app}"

[Code]
#ifdef Lite
// The lite build carries no runtime — make sure .NET 8 Desktop Runtime is present.
function DotNet8DesktopInstalled(): Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(
    ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not DotNet8DesktopInstalled() then
  begin
    if MsgBox('This app requires the .NET 8 Desktop Runtime, which was not found on this PC.'
        + #13#10#13#10 + 'Open the download page now? (Install it, then run this setup again.)'
        + #13#10#13#10 + 'Choose No to install the app anyway.',
        mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExecAsOriginalUser('open',
        'https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime',
        '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
      Result := False;  // stop setup; user installs the runtime first
    end;
  end;
end;
#endif

// Remove the autostart value the app itself may have written via its own toggle
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', 'InternetSpeedApp');
end;
