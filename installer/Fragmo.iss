; Instalador do Fragmo (Inno Setup 6). Gerado pelo publish.ps1:
;   ISCC /DAppVersion=x.y.z installer\Fragmo.iss
; Instala só para o usuário atual, sem pedir administrador, em %LOCALAPPDATA%\Programs\Fragmo.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{7A932716-B3B9-45FF-B3DE-A1CA42E0D12A}
AppName=Fragmo
AppVersion={#AppVersion}
AppVerName=Fragmo {#AppVersion}
AppPublisher=Bruno K. Dal Castel
AppPublisherURL=https://github.com/brunokdalcastel/fragmo
AppSupportURL=https://github.com/brunokdalcastel/fragmo/issues
DefaultDirName={autopf}\Fragmo
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts
OutputBaseFilename=Fragmo-Setup
SetupIconFile=..\src\Fragmo\Resources\fragmo.ico
UninstallDisplayIcon={app}\Fragmo.exe
UninstallDisplayName=Fragmo
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; O app é fechado no PrepareToInstall; não precisa do Restart Manager.
CloseApplications=no
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
brazilianportuguese.StartupTask=Iniciar o Fragmo com o Windows (recomendado)
english.StartupTask=Start Fragmo with Windows (recommended)
brazilianportuguese.Options=Opções:
english.Options=Options:

[Tasks]
Name: "startup"; Description: "{cm:StartupTask}"; GroupDescription: "{cm:Options}"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:Options}"

[Files]
Source: "..\artifacts\publish\Fragmo.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Fragmo"; Filename: "{app}\Fragmo.exe"
Name: "{autodesktop}\Fragmo"; Filename: "{app}\Fragmo.exe"; Tasks: desktopicon

[Registry]
; Mesmo valor que "Iniciar com o Windows" nas configurações do app.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Fragmo"; \
  ValueData: """{app}\Fragmo.exe"" --tray"; Tasks: startup
; Na desinstalação remove o valor, mesmo que tenha sido ligado depois, pelas configurações do app.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "Fragmo"; \
  Flags: uninsdeletevalue

[Run]
Filename: "{app}\Fragmo.exe"; Description: "{cm:LaunchProgram,Fragmo}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM Fragmo.exe"; Flags: runhidden; RunOnceId: "StopFragmo"

[Code]
// Fecha o Fragmo se estiver aberto, para poder substituir o EXE numa atualização.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Fragmo.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(300);
  Result := '';
end;
