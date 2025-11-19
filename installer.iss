; AMICUS Desktop Pet - Inno Setup Installer Script
; This script creates a Windows installer for the AMICUS desktop pet application

#define MyAppName "AMICUS"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SB Apps"
#define MyAppURL "https://github.com/Z-Oleksandr/amicus"
#define MyAppExeName "AMICUS.exe"
#define MyAppDescription "Your friendly desktop pet companion"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
AppId={{A7F8B9C1-2D3E-4F5A-8B9C-1D2E3F4A5B6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
; Allow user to change installation directory
DisableDirPage=no
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Output configuration
OutputDir=installer_output
OutputBaseFilename=Setup_AMICUS_v{#MyAppVersion}
; Compression
Compression=lzma2
SolidCompression=yes
; Windows version requirements (Windows 10 and later)
MinVersion=10.0
; Architecture
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
; Icon
SetupIconFile=Resources\Icon\Icon1.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
; Privileges
PrivilegesRequired=lowest
; Miscellaneous
WizardStyle=modern
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce
Name: "startupicon"; Description: "Run {#MyAppName} at &Windows startup"; GroupDescription: "Startup options:"; Flags: checkedonce

[Files]
; Include all files from the publish directory
Source: "bin\Release\net9.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu shortcut (always created)
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Resources\Icon\Icon1.ico"; Comment: "{#MyAppDescription}"
; Desktop shortcut (optional, based on task selection)
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Resources\Icon\Icon1.ico"; Comment: "{#MyAppDescription}"; Tasks: desktopicon

[Registry]
; Add to Windows startup (optional, based on task selection)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
; Option to launch the application after installation
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up any files created by the application
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}"

[Code]
// Custom message shown during installation
function InitializeSetup(): Boolean;
begin
  Result := True;
  MsgBox('Welcome to the setup of AMICUS - Your Desktop Pet Cat!' + #13#10 + #13#10 +
         'This installer will install AMICUS and its required components.' + #13#10 +
         'Click OK to continue.', mbInformation, MB_OK);
end;

// Custom message after installation
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // You could add post-install logic here if needed
  end;
end;
