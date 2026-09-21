<#
.SYNOPSIS
    Builds the MSIX package for the Microsoft Store.

.DESCRIPTION
    Publishes a self-contained folder build, lays it out next to the manifest and the tile
    images, then calls makeappx.exe from the Windows SDK.

    For the Store, leave the package unsigned: Partner Center signs it with the certificate
    that belongs to the reserved app identity. Pass -Sign with a certificate only when you
    want to sideload the package for testing.

    Identity values come from Partner Center, under Product identity for the reserved name.

.EXAMPLE
    # Store submission
    pwsh -File packaging\msix\Build-Msix.ps1 `
        -IdentityName 12345Publisher.FileMerge `
        -Publisher 'CN=ABCDEFGH-1234-5678-9012-ABCDEFGHIJKL' `
        -PublisherDisplayName 'Your Publisher Name'

.EXAMPLE
    # Local sideload test
    pwsh -File packaging\msix\Build-Msix.ps1 -Sign -CertificatePath .\test.pfx -CertificatePassword (Read-Host -AsSecureString)
#>
[CmdletBinding()]
param(
    [string] $IdentityName = 'FileMerge',

    [string] $Publisher = 'CN=FileMerge',

    [string] $PublisherDisplayName = 'FileMerge',

    [ValidateSet('x64', 'arm64', 'x86')]
    [string] $Architecture = 'x64',

    [string] $Version,

    [switch] $Sign,

    [string] $CertificatePath,

    [System.Security.SecureString] $CertificatePassword
)

$ErrorActionPreference = 'Stop'

$packagingDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $packagingDir)
$project = Join-Path $repoRoot 'src\FileMerge\FileMerge.csproj'
$artifacts = Join-Path $repoRoot 'artifacts'
$layout = Join-Path $packagingDir 'layout'

if (-not $Version) {
    $props = Get-Content (Join-Path $repoRoot 'Directory.Build.props') -Raw
    $Version = if ($props -match '<Version>([^<]+)</Version>') { $Matches[1] } else { '1.0.0' }
}

# An MSIX version is always four parts and the last one must be 0 for a Store submission.
$parts = $Version.Split('.')
while ($parts.Count -lt 3) { $parts += '0' }
$packageVersion = '{0}.{1}.{2}.0' -f $parts[0], $parts[1], $parts[2]

$runtime = "win-$Architecture"

Write-Output "FileMerge $packageVersion  ($runtime)"

# ---------------------------------------------------------------- locate the Windows SDK

function Find-SdkTool {
    param([string] $Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $roots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin"
    ) | Where-Object { Test-Path $_ }

    foreach ($root in $roots) {
        $found = Get-ChildItem -Path $root -Filter $Name -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\(x64|x86)\\$Name$" } |
            Sort-Object { $_.Directory.Parent.Name } -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    throw "$Name not found. Install the Windows 10/11 SDK, or add it to PATH."
}

$makeappx = Find-SdkTool 'makeappx.exe'
Write-Output "  makeappx: $makeappx"

# ---------------------------------------------------------------- publish

if (Test-Path $layout) { Remove-Item -Recurse -Force $layout }
New-Item -ItemType Directory -Force -Path $layout | Out-Null
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

dotnet publish $project `
    -c Release `
    -r $runtime `
    --self-contained true `
    -p:FileMergePackaged=true `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    -o $layout `
    --nologo `
    -v minimal

if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# ---------------------------------------------------------------- manifest and assets

Copy-Item (Join-Path $packagingDir 'Images') (Join-Path $layout 'Images') -Recurse -Force

$manifest = Get-Content (Join-Path $packagingDir 'AppxManifest.xml') -Raw -Encoding UTF8
$manifest = $manifest.Replace('__IDENTITY_NAME__', $IdentityName)
$manifest = $manifest.Replace('__PUBLISHER__', $Publisher)
$manifest = $manifest.Replace('__PUBLISHER_DISPLAY_NAME__', $PublisherDisplayName)
$manifest = $manifest.Replace('__VERSION__', $packageVersion)
$manifest = $manifest.Replace('__ARCH__', $Architecture)

[System.IO.File]::WriteAllText(
    (Join-Path $layout 'AppxManifest.xml'),
    $manifest,
    [System.Text.UTF8Encoding]::new($false))

# The published .pdb files are useless inside a Store package and only inflate it.
Get-ChildItem -Path $layout -Filter *.pdb -Recurse | Remove-Item -Force

# ---------------------------------------------------------------- pack

$msixPath = Join-Path $artifacts "FileMerge-$packageVersion-$Architecture.msix"
if (Test-Path $msixPath) { Remove-Item $msixPath }

& $makeappx pack /d $layout /p $msixPath /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed" }

$sizeMb = [Math]::Round((Get-Item $msixPath).Length / 1MB, 1)
Write-Output "  $(Split-Path -Leaf $msixPath)  ($sizeMb MB)"

# ---------------------------------------------------------------- optional signing

if ($Sign) {
    if (-not $CertificatePath) { throw "-Sign requires -CertificatePath" }

    $signtool = Find-SdkTool 'signtool.exe'
    $plain = [System.Net.NetworkCredential]::new('', $CertificatePassword).Password

    & $signtool sign /fd SHA256 /a /f $CertificatePath /p $plain $msixPath
    if ($LASTEXITCODE -ne 0) { throw "signtool failed" }

    Write-Output "  signed with $CertificatePath"
}
else {
    Write-Output "  unsigned (correct for a Store submission; Partner Center signs it)"
}

Remove-Item -Recurse -Force $layout
Write-Output "artifacts -> $artifacts"
