; Instalador do Fragmo (Inno Setup 6). Gerado pelo publish.ps1:
;   ISCC /DAppVersion=x.y.z installer\Fragmo.iss
; Instala só para o usuário atual, sem pedir administrador, em %LOCALAPPDATA%\Programs\Fragmo.

#ifndef AppVersion
  #define AppVersion "0.3.2"
#endif

[Setup]
AppId={{C9E49E11-BEE3-46AC-9C28-3E66293C297C}
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
; O app é fechado no PrepareToInstall/InitializeUninstall (pedido normal, à força só se o usuário escolher).
CloseApplications=no
ShowLanguageDialog=no
LanguageDetectionMethod=uilanguage

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
brazilianportuguese.BusyTitle=O Fragmo está com algo em andamento
english.BusyTitle=Fragmo has something in progress
brazilianportuguese.BusyText=Há uma captura em edição, uma imagem fixada na tela ou as configurações abertas. Termine ou feche essa janela e clique em "Tentar de novo".%n%nSe escolher fechar à força, o que não foi salvo será perdido.
english.BusyText=There is a capture being edited, an image pinned to the screen or the settings window open. Finish or close it and click "Try again".%n%nIf you force it to close, anything unsaved will be lost.
brazilianportuguese.BusyRetry=Tentar de novo
english.BusyRetry=Try again
brazilianportuguese.BusyForce=Fechar à força
english.BusyForce=Force close
brazilianportuguese.BusyAbort=O Fragmo continua aberto com algo em andamento. Feche essa janela e rode o instalador de novo.
english.BusyAbort=Fragmo is still open with something in progress. Close it and run the installer again.
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

[Code]
// Fechar o Fragmo antes de atualizar ou desinstalar:
//   1. pede para ele fechar normalmente (evento nomeado que o app escuta) e espera;
//   2. se ele não fechar (há captura em edição, imagem fixada ou configurações abertas), avisa o usuário,
//      que escolhe tentar de novo, fechar à força ou cancelar;
//   3. em modo silencioso nunca fecha à força: cancela.
const
  EVENT_MODIFY_STATE = $0002;
  InstanceMutexName = 'Local\Fragmo.SingleInstance';
  CloseEventName = 'Local\Fragmo.CloseRequest';
  GracefulWaitMs = 5000;

function OpenEvent(dwDesiredAccess: DWORD; bInheritHandle: BOOL; lpName: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(hEvent: THandle): BOOL;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(hObject: THandle): BOOL;
  external 'CloseHandle@kernel32.dll stdcall';

function FragmoRunning: Boolean;
begin
  Result := CheckForMutexes(InstanceMutexName);
end;

function RequestClose: Boolean;
var
  Handle: THandle;
  Waited: Integer;
begin
  Handle := OpenEvent(EVENT_MODIFY_STATE, False, CloseEventName);
  if Handle <> 0 then
  begin
    SetEvent(Handle);
    CloseHandle(Handle);
  end;
  Waited := 0;
  while FragmoRunning and (Waited < GracefulWaitMs) do
  begin
    Sleep(200);
    Waited := Waited + 200;
  end;
  Result := not FragmoRunning;
end;

procedure ForceClose;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Fragmo.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

function EnsureFragmoClosed(Silent: Boolean): Boolean;
var
  Choice: Integer;
begin
  Result := True;
  while FragmoRunning do
  begin
    if RequestClose then
      Exit;
    if Silent then
    begin
      Result := False;
      Exit;
    end;
    Choice := TaskDialogMsgBox(CustomMessage('BusyTitle'), CustomMessage('BusyText'), mbConfirmation,
      MB_YESNOCANCEL, [CustomMessage('BusyRetry'), CustomMessage('BusyForce')], 0);
    if Choice = IDNO then
      ForceClose
    else if Choice = IDCANCEL then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if EnsureFragmoClosed(WizardSilent) then
    Result := ''
  else
    Result := CustomMessage('BusyAbort');
end;

function InitializeUninstall(): Boolean;
begin
  Result := EnsureFragmoClosed(UninstallSilent);
end;
