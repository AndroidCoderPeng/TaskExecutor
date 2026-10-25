[Setup]
AppName=TaskDispatcher
AppVersion=1.0.0
DefaultDirName={commonpf}\TaskDispatcher
DefaultGroupName=TaskDispatcher
OutputDir=C:\Users\Administrator\Desktop
OutputBaseFilename=TaskDispatcher_Setup
SetupIconFile=D:\Code\QtProjects\TaskDispatcher\application.ico
UninstallDisplayIcon={app}\TaskDispatcher.exe
Compression=lzma
SolidCompression=yes
PrivilegesRequired=none
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "C:\Users\Administrator\Desktop\temp_build\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TaskDispatcher"; Filename: "{app}\TaskDispatcher.exe"
Name: "{commondesktop}\TaskDispatcher"; Filename: "{app}\TaskDispatcher.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\TaskDispatcher.exe"; Description: "{cm:LaunchProgram,TaskDispatcher}"; Flags: nowait postinstall skipifsilent