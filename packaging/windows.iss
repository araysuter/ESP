[Setup]
AppId=ESP-HackTheNest
AppName=ESP
AppVersion=0.1
DefaultDirName={localappdata}\Programs\ESP
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=ESP-Windows-Setup
Compression=lzma2
SolidCompression=yes
[Files]
Source: "..\Builds\Windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\ESP"; Filename: "{app}\ESP.exe"
