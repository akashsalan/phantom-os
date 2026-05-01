[Setup]
; Basic Application Information
AppName=Phantom-OS
AppVersion=1.0.0
AppPublisher=PhantomOS Developer
AppPublisherURL=https://github.com/PhantomOS
AppSupportURL=https://github.com/PhantomOS
AppUpdatesURL=https://github.com/PhantomOS

; Default Installation Directory
; {autopf} expands to Program Files or Program Files (x86) depending on the system
DefaultDirName={autopf}\PhantomOS
DefaultGroupName=Phantom-OS

; Installer Output
OutputDir=.\Installer
OutputBaseFilename=PhantomOS_Setup
Compression=lzma2/ultra64
SolidCompression=yes

; Administrative privileges required for Program Files
PrivilegesRequired=admin

; Icon for the installer (optional, using default if not provided)
; SetupIconFile=icon.ico

; Uninstall Settings
UninstallDisplayIcon={app}\PhantomOS.exe
UninstallDisplayName=Phantom-OS

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The source should be the self-contained publish directory
; We use recursesubdirs to include all DLLs, runtimes, and native libraries (like x64/tesseract50.dll)
Source: "bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu Shortcut
Name: "{group}\Phantom-OS"; Filename: "{app}\PhantomOS.exe"
Name: "{group}\{cm:UninstallProgram,Phantom-OS}"; Filename: "{uninstallexe}"

; Desktop Shortcut
Name: "{autodesktop}\Phantom-OS"; Filename: "{app}\PhantomOS.exe"; Tasks: desktopicon

[Run]
; Option to launch the app after installation
Filename: "{app}\PhantomOS.exe"; Description: "{cm:LaunchProgram,Phantom-OS}"; Flags: nowait postinstall skipifsilent

[Code]
// Optional: Code to check for previous installations or custom logic
