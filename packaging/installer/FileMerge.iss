; Inno Setup script for the ordinary installer.
;
; Build it with packaging\installer\Build-Installer.ps1, which publishes the app and passes
; AppVersion, SourceDir, OutputDir and Arch on the ISCC command line.
;
; It installs per user by default, into %LOCALAPPDATA%\Programs, so no administrator rights are
; needed; the first page offers an all-users install into Program Files instead.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #error SourceDir must point at the published app folder
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif
#ifndef Arch
  #define Arch "x64"
#endif

#if Arch == "arm64"
  #define ArchAllowed "arm64"
#else
  #define ArchAllowed "x64compatible"
#endif

#define LangDir AddBackslash(CompilerPath) + "Languages\"

[Setup]
; Never change AppId: Windows uses it to recognise an upgrade of the same app.
AppId={{7C1E6B52-9F0A-4D8E-B3A1-5E2C4D6F8A90}
AppName={cm:AppName}
AppVersion={#AppVersion}
AppVerName={cm:AppName} {#AppVersion}
AppPublisher=hiro5791
AppPublisherURL=https://github.com/hiro5791/FileMerge
AppSupportURL=https://github.com/hiro5791/FileMerge/issues
AppUpdatesURL=https://github.com/hiro5791/FileMerge/releases
DefaultDirName={autopf}\TekuTeku File Merge
DefaultGroupName={cm:AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#ArchAllowed}
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=FileMerge-{#AppVersion}-setup-{#Arch}
SetupIconFile=..\..\src\FileMerge\Assets\app.ico
UninstallDisplayIcon={app}\FileMerge.exe
UninstallDisplayName={cm:AppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=yes
ShowLanguageDialog=auto
LanguageDetectionMethod=uilanguage

; Languages the app speaks that Inno Setup ships a translation for. The unofficial ones
; (Chinese, Arabic, Hindi and so on) are picked up too if their .isl files are dropped into
; the compiler's Languages folder; otherwise those users see the English setup.
[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
#if FileExists(LangDir + "Japanese.isl")
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"
#endif
#if FileExists(LangDir + "ChineseSimplified.isl")
Name: "zhhans"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
#endif
#if FileExists(LangDir + "ChineseTraditional.isl")
Name: "zhhant"; MessagesFile: "compiler:Languages\ChineseTraditional.isl"
#endif
#if FileExists(LangDir + "Korean.isl")
Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl"
#endif
#if FileExists(LangDir + "Spanish.isl")
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
#endif
#if FileExists(LangDir + "BrazilianPortuguese.isl")
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
#endif
#if FileExists(LangDir + "French.isl")
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
#endif
#if FileExists(LangDir + "German.isl")
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
#endif
#if FileExists(LangDir + "Italian.isl")
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"
#endif
#if FileExists(LangDir + "Russian.isl")
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
#endif
#if FileExists(LangDir + "Ukrainian.isl")
Name: "uk"; MessagesFile: "compiler:Languages\Ukrainian.isl"
#endif
#if FileExists(LangDir + "Polish.isl")
Name: "pl"; MessagesFile: "compiler:Languages\Polish.isl"
#endif
#if FileExists(LangDir + "Dutch.isl")
Name: "nl"; MessagesFile: "compiler:Languages\Dutch.isl"
#endif
#if FileExists(LangDir + "Turkish.isl")
Name: "tr"; MessagesFile: "compiler:Languages\Turkish.isl"
#endif
#if FileExists(LangDir + "Arabic.isl")
Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"
#endif
#if FileExists(LangDir + "Hindi.isl")
Name: "hi"; MessagesFile: "compiler:Languages\Hindi.isl"
#endif
#if FileExists(LangDir + "Indonesian.isl")
Name: "id"; MessagesFile: "compiler:Languages\Indonesian.isl"
#endif
#if FileExists(LangDir + "Vietnamese.isl")
Name: "vi"; MessagesFile: "compiler:Languages\Vietnamese.isl"
#endif
#if FileExists(LangDir + "Thai.isl")
Name: "th"; MessagesFile: "compiler:Languages\Thai.isl"
#endif

; The app's own name in the languages that translate it. Everyone else sees the English name,
; as they do in the app itself.
[CustomMessages]
AppName=TekuTeku File Merge
#if FileExists(LangDir + "Japanese.isl")
ja.AppName=てくてくファイル結合
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{cm:AppName}"; Filename: "{app}\FileMerge.exe"
Name: "{autodesktop}\{cm:AppName}"; Filename: "{app}\FileMerge.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\FileMerge.exe"; Description: "{cm:LaunchProgram,{cm:AppName}}"; Flags: nowait postinstall skipifsilent
