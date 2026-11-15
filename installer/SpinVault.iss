; Instalador do SpinVault (Inno Setup 6)
; A versão pode ser passada pela linha de comando: ISCC /DAppVersion=1.1.0 SpinVault.iss
#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
#define AppName "SpinVault"
#define AppExe "SpinVault.exe"

[Setup]
AppId={{B6F2D0C4-7A1E-4E58-9C3B-5D2A8E41F7A9}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Bruno
DefaultDirName={autopf}\SpinVault
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\Assets\app.ico
OutputDir=..\dist
OutputBaseFilename=SpinVault-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"

[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir {#AppName}"; Flags: nowait postinstall skipifsilent

; Os dados do usuário (%LocalAppData%\ColecaoMidias) NÃO são apagados ao desinstalar.
