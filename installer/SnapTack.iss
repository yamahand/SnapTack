; SnapTack インストーラー (Inno Setup 6)
; 事前に scripts/publish.ps1 でポータブル版を artifacts/publish に出力しておくこと
; コンパイル: iscc installer\SnapTack.iss

#define MyAppName "SnapTack"
; CI からは iscc /DMyAppVersion=1.4.0 で上書きされる。ここの値はローカルコンパイル時の既定値
#ifndef MyAppVersion
  #define MyAppVersion "1.3.0"
#endif
#define MyAppPublisher "yamahand"
#define MyAppURL "https://github.com/yamahand/SnapTack"
#define MyAppExeName "SnapTack.exe"

[Setup]
; AppId は SnapTack 固有の GUID (変更しないこと)
AppId={{9E1B9C0A-3E32-4B7A-9C69-5A24B1E6D0F4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; 管理者権限なしでもインストール可能にする (その場合は {localappdata}\Programs へ)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\artifacts
OutputBaseFilename=SnapTack-v{#MyAppVersion}-setup
SetupIconFile=..\SnapTack\Assets\SnapTack.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Windows 起動時に {#MyAppName} を自動起動する"; GroupDescription: "追加オプション:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
