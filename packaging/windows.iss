#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
[Setup]
AppId=ESP-HackTheNest
AppName=ESP
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\ESP
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=ESP-{#AppVersion}-Windows-Setup
Compression=lzma2
SolidCompression=yes
[Files]
Source: "..\build\StandaloneWindows64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\ESP"; Filename: "{app}\ESP.exe"
