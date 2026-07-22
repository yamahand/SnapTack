; SnapTack インストーラー (Inno Setup 6)
; 事前に scripts/publish.ps1 でポータブル版を artifacts/publish に出力しておくこと
; コンパイル: iscc installer\SnapTack.iss

#define MyAppName "SnapTack"
#define MyAppExeSource "..\artifacts\publish\SnapTack.exe"
; CI からは iscc /DMyAppVersion=1.4.0 で上書きされる。
; 省略時は publish 済み exe から取得する (exe のバージョンは Directory.Build.props が元)。
; ここに数値を直書きすると props の更新時に取り残されるため書かないこと
#ifndef MyAppVersion
  ; GetVersionNumbersString は 4 桁 (1.3.0.0) を返すため、末尾の .0 を落として
  ; CI が渡す 3 桁 (1.3.0) と表記を揃える
  #define MyAppFileVersion GetVersionNumbersString(AddBackslash(SourcePath) + MyAppExeSource)
  #define MyAppVersion Copy(MyAppFileVersion, 1, RPos(".", MyAppFileVersion) - 1)
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
; 既定は英語。日本語環境ではインストーラーが自動で日本語を選ぶ (アプリ本体と同じ方針)
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[CustomMessages]
english.StartupTask=Start {#MyAppName} automatically when Windows starts
english.AdditionalOptions=Additional options:
japanese.StartupTask=Windows 起動時に {#MyAppName} を自動起動する
japanese.AdditionalOptions=追加オプション:

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:AdditionalOptions}"; Flags: unchecked

[Files]
Source: "{#MyAppExeSource}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
