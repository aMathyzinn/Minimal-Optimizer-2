; Inno Setup Script for Minimal Optimizer V2
; Open Source PC Optimization Tool
; Developer: aMathyzin (aMathyzin Studio)
; GitHub: https://github.com/amathyzinn/Minimal-Optimizer-2.0
; Website: https://amathyzin.com.br
; YouTube: https://youtube.com/@aMathyzin
; License: Open Source (GPL-3.0)

#define MyAppName "Minimal Optimizer"
#define MyAppVersion "2.0.1"
#define MyAppPublisher "aMathyzin Studio"
#define MyAppURL "https://amathyzin.com.br"
#define MyAppExeName "MinimalOptimizer2.exe"
#define MyAppAssocName MyAppName + " File"
#define MyAppAssocExt ".minimal"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt
#define MyAppDescription "Minimalist PC Optimizer focused on improving system performance with safe and reversible features"
#define MyAppCopyright "Copyright (C) 2025-2026 aMathyzin Studio"
#define MyAppContact "https://github.com/amathyzinn/Minimal-Optimizer-2.0"
#define BuildOutputDir "..\\bin\\Release\\net8.0-windows\\win-x64\\publish"

[Setup]
; Application Identity
AppId={{4D38F09C-58BA-49B1-8B53-47B8E0D6DCEA}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppContact}
AppUpdatesURL={#MyAppURL}
AppContact={#MyAppContact}
AppComments={#MyAppDescription}
AppCopyright={#MyAppCopyright}

; Version Information (appears in file properties)
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppDescription}
VersionInfoTextVersion={#MyAppVersion}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

; Installation Directories
DefaultDirName={autopf}\Minimal Optimizer V2
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

; Architecture Requirements
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Security and Permissions
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Compression and Output
Compression=lzma2/max
SolidCompression=yes
OutputDir=..\dist
OutputBaseFilename=MinimalOptimizer_v{#MyAppVersion}_Setup
SetupIconFile=icon.ico

; Interface Configuration
WizardStyle=modern dark windows11
WizardSizePercent=120,100
DisableProgramGroupPage=yes
DisableWelcomePage=no
ChangesAssociations=yes

; Custom Wizard Images
; Imagem lateral do wizard (164x314 pixels recomendado para modern style)
WizardImageFile=wizard-image.bmp
WizardSmallImageFile=wizard-small.png

; License and Information
LicenseFile=..\LICENCE.txt
InfoBeforeFile=..\README.txt

; Uninstall Configuration
UninstallDisplayName={#MyAppName} V2
UninstallFilesDir={app}\uninstall
CreateUninstallRegKey=yes

; Code Signing (opcional - adicione quando tiver certificado)
; SignTool=signtool
; SignedUninstaller=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
english.WelcomeLabel2=This will install [name/ver] on your computer.%n%nThis is an open-source PC optimization tool developed by aMathyzin Studio.%n%nAll optimizations are safe, auditable and reversible.%n%nGitHub: https://github.com/amathyzinn/Minimal-Optimizer-2.0%nWebsite: https://amathyzin.com.br%nYouTube: https://youtube.com/@aMathyzin
brazilianportuguese.WelcomeLabel2=Este instalador vai instalar o [name/ver] em seu computador.%n%nEsta é uma ferramenta open-source de otimização de PC desenvolvida por aMathyzin Studio.%n%nTodas as otimizações são seguras, auditáveis e reversíveis.%n%nGitHub: https://github.com/amathyzinn/Minimal-Optimizer-2.0%nWebsite: https://amathyzin.com.br%nYouTube: https://youtube.com/@aMathyzin

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#BuildOutputDir}\\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion signonce
Source: "{#BuildOutputDir}\\MinimalOptimizer2.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutputDir}\\MinimalOptimizer2.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutputDir}\\MinimalOptimizer2.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion

Source: "{#BuildOutputDir}\\SharpVectors.Converters.Wpf.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutputDir}\\SharpVectors.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutputDir}\\SharpVectors.Css.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutputDir}\\SharpVectors.Dom.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutputDir}\\SharpVectors.Model.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutputDir}\\SharpVectors.Rendering.Wpf.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutputDir}\\SharpVectors.Runtime.Wpf.dll"; DestDir: "{app}"; Flags: ignoreversion

Source: "{#BuildOutputDir}\\System.Diagnostics.EventLog.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutputDir}\\System.Diagnostics.EventLog.Messages.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutputDir}\\System.Management.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutputDir}\\System.ServiceProcess.ServiceController.dll"; DestDir: "{app}"; Flags: ignoreversion

; Documentation
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "..\LICENCE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; File Association
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocExt}\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppAssocKey}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}"; ValueType: string; ValueName: "URL"; ValueData: "{#MyAppURL}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; Application Information in Registry
Root: HKA; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "Website"; ValueData: "{#MyAppURL}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "GitHub"; ValueData: "{#MyAppContact}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "Developer"; ValueData: "Matheus Fernandes"; Flags: uninsdeletekey

[Icons]
; Start Menu Icons
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppDescription}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"; Comment: "Uninstall {#MyAppName}"
Name: "{group}\Visit Website"; Filename: "{#MyAppURL}"; Comment: "Visit the official website"
Name: "{group}\GitHub Repository"; Filename: "{#MyAppContact}"; Comment: "View source code on GitHub"
Name: "{group}\YouTube Channel"; Filename: "https://youtube.com/@aMathyzin"; Comment: "Subscribe to aMathyzin channel"

; Desktop Icon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{#MyAppDescription}"

; Quick Launch Icon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon; Comment: "{#MyAppDescription}"

[Run]
; Launch after installation (shellexec permite que o UAC solicite elevação)
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent shellexec

; Open website (optional)
; Filename: "{#MyAppURL}"; Description: "Visit the official website"; Flags: postinstall shellexec skipifsilent unchecked

[UninstallDelete]
; Clean up any generated files
Type: filesandordirs; Name: "{app}\Logs"
Type: filesandordirs; Name: "{app}\Temp"

[Code]
procedure InitializeWizard();
begin
end;

function IsDotNetDesktopRuntimeInstalled(): Boolean;
var
  FindRec: TFindRec;
begin
  // Verifica se o dotnet.exe existe e se há runtimes do WindowsDesktop 8.x instalados
  Result := FileExists(ExpandConstant('{pf}\dotnet\dotnet.exe')) and 
            FindFirst(ExpandConstant('{pf}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

function InitializeSetup(): Boolean;
var
  Resp: Integer;
  ErrorCode: Integer;
begin
  Result := True;

  if not IsDotNetDesktopRuntimeInstalled() then
  begin
    Resp := MsgBox('O .NET Desktop Runtime 8.0 não foi encontrado no sistema. ' +
      'Este aplicativo requer o runtime para executar. Deseja abrir a página oficial de download?',
      mbInformation, MB_YESNO);
    if Resp = IDYES then
    begin
      ShellExec('', 'https://dotnet.microsoft.com/download/dotnet/8.0/runtime', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    Result := False; // aborta até que o usuário instale o runtime
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
  end;
end;
