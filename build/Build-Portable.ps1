<#
.SYNOPSIS
    Builds the portable releases: a single self-contained .exe and a small framework-dependent one.

.DESCRIPTION
    Produces, under artifacts\:
      FileMerge-<version>-win-<arch>.exe           one file, no .NET needed, no installation
      FileMerge-<version>-win-<arch>.zip           the same file zipped for GitHub Releases
      FileMerge-<version>-win-<arch>-netdep.zip    ~1 MB, needs the .NET Desktop Runtime

    Both zips carry packaging\portable\portable.txt next to the executable, so a copy that is
    unzipped and run leaves nothing in the user profile: settings are kept beside the .exe.

    The single-file build extracts its native WPF components to %TEMP% on first run. That is
    how WPF single-file works; it needs no administrator rights and happens only once.

.EXAMPLE
    pwsh -File build\Build-Portable.ps1
    pwsh -File build\Build-Portable.ps1 -Runtime win-arm64
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64', 'win-x86')]
    [string] $Runtime = 'win-x64',

    [string] $Version,

    [switch] $SkipFrameworkDependent
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
$project = Join-Path $repoRoot 'src\FileMerge\FileMerge.csproj'
$artifacts = Join-Path $repoRoot 'artifacts'
$staging = Join-Path $artifacts 'staging'

if (-not $Version) {
    $props = Get-Content (Join-Path $repoRoot 'Directory.Build.props') -Raw
    $Version = if ($props -match '<Version>([^<]+)</Version>') { $Matches[1] } else { '1.0.0' }
}

Write-Output "FileMerge $Version  ($Runtime)"

if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

# ---------------------------------------------------------------- self-contained single file

$selfContained = Join-Path $staging 'self-contained'

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -o $selfContained `
    --nologo `
    -v minimal

if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# Files that ship next to the .exe in the portable zips only; the installer and the Store
# package must not carry portable.txt. Download sites such as Vector refuse an archive without
# a user document, hence the readmes. Text is written as UTF-8 with BOM and CRLF so Notepad on
# any supported Windows shows it correctly, whatever line endings the checkout has.
$portableDir = Join-Path $repoRoot 'packaging\portable'
$extras = Join-Path $staging 'extras'
New-Item -ItemType Directory -Force -Path $extras | Out-Null
$extraFiles = [ordered]@{
    'portable.txt'  = Join-Path $portableDir 'portable.txt'
    'readme.txt'    = Join-Path $portableDir 'readme.txt'
    'readme_ja.txt' = Join-Path $portableDir 'readme_ja.txt'
    'LICENSE.txt'   = Join-Path $repoRoot 'LICENSE'
}
foreach ($name in $extraFiles.Keys) {
    $text = [IO.File]::ReadAllText($extraFiles[$name], [Text.Encoding]::UTF8) -replace "`r?`n", "`r`n"
    [IO.File]::WriteAllText((Join-Path $extras $name), $text, [Text.UTF8Encoding]::new($true))
}
$extraPaths = @($extraFiles.Keys | ForEach-Object { Join-Path $extras $_ })

$exeName = "FileMerge-$Version-$Runtime.exe"
$exePath = Join-Path $artifacts $exeName
Copy-Item (Join-Path $selfContained 'FileMerge.exe') $exePath -Force

$zipPath = Join-Path $artifacts "FileMerge-$Version-$Runtime.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath }
Compress-Archive -Path (@($exePath) + $extraPaths) -DestinationPath $zipPath

$sizeMb = [Math]::Round((Get-Item $exePath).Length / 1MB, 1)
Write-Output "  $exeName  ($sizeMb MB, no .NET required)"

# ---------------------------------------------------------------- framework-dependent

if (-not $SkipFrameworkDependent) {
    $frameworkDependent = Join-Path $staging 'framework-dependent'

    dotnet publish $project `
        -c Release `
        -r $Runtime `
        --self-contained false `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=false `
        -p:Version=$Version `
        -o $frameworkDependent `
        --nologo `
        -v minimal

    if ($LASTEXITCODE -ne 0) { throw "publish failed" }

    $netdepZip = Join-Path $artifacts "FileMerge-$Version-$Runtime-netdep.zip"
    if (Test-Path $netdepZip) { Remove-Item $netdepZip }
    Copy-Item $extraPaths $frameworkDependent
    Compress-Archive -Path (Join-Path $frameworkDependent '*') -DestinationPath $netdepZip

    $netdepMb = [Math]::Round((Get-Item $netdepZip).Length / 1MB, 2)
    Write-Output "  $(Split-Path -Leaf $netdepZip)  ($netdepMb MB, needs .NET Desktop Runtime 10)"
}

Remove-Item -Recurse -Force $staging
Write-Output "artifacts -> $artifacts"
