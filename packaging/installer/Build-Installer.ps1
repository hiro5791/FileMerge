<#
.SYNOPSIS
    Builds the ordinary Windows installer (setup.exe) with Inno Setup.

.DESCRIPTION
    Publishes a self-contained folder build of the app, so it needs no .NET on the target
    machine, then compiles FileMerge.iss around it. Unlike the portable single-file .exe, the
    installed app is a plain folder: it starts faster and never unpacks anything to %TEMP%.

    Writes artifacts\FileMerge-<version>-setup-<arch>.exe.

    Requires Inno Setup 6. Install it once with:
        winget install --id JRSoftware.InnoSetup -e

    The setup.exe is unsigned, so Windows SmartScreen warns about it the first time it is run.
    Sign it with a code-signing certificate before wide distribution.

.EXAMPLE
    powershell -File packaging\installer\Build-Installer.ps1
    powershell -File packaging\installer\Build-Installer.ps1 -Architecture arm64 -Version 1.2.0
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string] $Architecture = 'x64',

    [string] $Version
)

$ErrorActionPreference = 'Stop'

$installerDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $installerDir)
$project = Join-Path $repoRoot 'src\FileMerge\FileMerge.csproj'
$artifacts = Join-Path $repoRoot 'artifacts'
$staging = Join-Path $artifacts "staging\installer-$Architecture"

if (-not $Version) {
    $props = Get-Content (Join-Path $repoRoot 'Directory.Build.props') -Raw
    $Version = if ($props -match '<Version>([^<]+)</Version>') { $Matches[1] } else { '1.0.0' }
}

Write-Output "FileMerge $Version installer ($Architecture)"

# ---------------------------------------------------------------- locate Inno Setup

$iscc = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | ForEach-Object Source),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 not found. Install it with: winget install --id JRSoftware.InnoSetup -e"
}

Write-Output "  iscc: $iscc"

# ---------------------------------------------------------------- publish

if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

dotnet publish $project `
    -c Release `
    -r "win-$Architecture" `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:EnableCompressionInSingleFile=false `
    -p:Version=$Version `
    -o $staging `
    --nologo `
    -v minimal

if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# Debug symbols are of no use to someone installing the app.
Get-ChildItem -Path $staging -Filter *.pdb -Recurse | Remove-Item -Force

# ---------------------------------------------------------------- compile

& $iscc `
    "/DAppVersion=$Version" `
    "/DSourceDir=$staging" `
    "/DOutputDir=$artifacts" `
    "/DArch=$Architecture" `
    /Q `
    (Join-Path $installerDir 'FileMerge.iss')

if ($LASTEXITCODE -ne 0) { throw "iscc failed" }

Remove-Item -Recurse -Force $staging

$setup = Join-Path $artifacts "FileMerge-$Version-setup-$Architecture.exe"
$sizeMb = [Math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Output "  $(Split-Path -Leaf $setup)  ($sizeMb MB)"
