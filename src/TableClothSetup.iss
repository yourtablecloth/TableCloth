#ifexist "commit.iss"
  #include "commit.iss"
#endif

#ifndef GitCommit
  #define GitCommit "raw"
#endif

#ifndef Configuration
  #define Configuration "Release"
#endif

#ifndef Architecture
  #define Architecture "x64"
#endif

#define MyAppName "TableCloth"
#define MyAppDisplayName "식탁보"
#define MyAppPublisher "TableCloth Project"
#define MyAppURL "https://yourtablecloth.app"
#define MyAppExeName "TableCloth.exe"

#define MyAppSourceDirectory "TableCloth\bin\" + Architecture + "\" + Configuration + "\net8.0-windows10.0.18362.0\win-" + Architecture + "\publish"
#define MyAppVersion GetVersionNumbersString(MyAppSourceDirectory + "\TableCloth.exe")
#define MyAppCommitId Copy(GitCommit, 1, 7)

#if Configuration == "Debug"
  #define MyAppVerName MyAppDisplayName + " " + MyAppVersion + " (" + Configuration + ") #" + MyAppCommitId
#else
  #define MyAppVerName MyAppDisplayName + " " + MyAppVersion + " #" + MyAppCommitId
#endif

[Setup]
AppId={{69668D6A-E89D-4990-961D-AC7CE40529C4}
AppName={#MyAppDisplayName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppVerName}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableDirPage=yes
DisableStartupPrompt=yes
DirExistsWarning=no
LicenseFile=License.rtf
PrivilegesRequired=lowest
OutputBaseFilename=TableCloth_{#MyAppVersion}_{#Configuration}_{#MyAppCommitId}
SetupIconFile=TableCloth\Resources\SandboxIcon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "korean"; MessagesFile: "Korean.isl"
//Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "{#MyAppSourceDirectory}\*.*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
