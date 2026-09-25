<#
.SYNOPSIS
    Captures 1920x1080 Store screenshots of the app in every language, light and dark.

.DESCRIPTION
    For each language the app is started with that language and theme and a few sample files,
    the window is sized so its visible frame is exactly 1920x1080, brought to the front, and
    captured. The user's settings file is restored afterwards.

    Output: <OutDir>\<code>-light.png and <OutDir>\<code>-dark.png, where <code> is the app's
    language code (en, ja, zh-Hans, ...).

.EXAMPLE
    ./build/Capture-StoreScreenshots.ps1 -Files 'F:\dev\test1\csv\a.csv','F:\dev\test1\csv\b.csv'
#>
[CmdletBinding()]
param(
    [string] $Exe,
    [string[]] $Files = @(),
    [string] $OutDir,
    [string[]] $Languages
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if (-not $Exe) { $Exe = Join-Path $repoRoot 'src\FileMerge\bin\Debug\net10.0-windows\FileMerge.exe' }
if (-not $OutDir) { $OutDir = Join-Path $repoRoot 'artifacts\store-listing\images' }
if (-not $Languages) {
    $Languages = Get-ChildItem (Join-Path $repoRoot 'src\FileMerge\Localization\Strings\*.json') |
        ForEach-Object { $_.BaseName }
}
if (-not (Test-Path $Exe)) { throw "Build the app first: $Exe not found." }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$sig = @'
using System;
using System.Runtime.InteropServices;
public static class StoreShotWin {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool repaint);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
}
'@
if (-not ('StoreShotWin' -as [type])) { Add-Type -TypeDefinition $sig }

$settingsDir = Join-Path $env:LOCALAPPDATA 'FileMerge'
$settingsPath = Join-Path $settingsDir 'FileMerge.settings.json'
New-Item -ItemType Directory -Force -Path $settingsDir | Out-Null
$backup = if (Test-Path $settingsPath) { [IO.File]::ReadAllBytes($settingsPath) } else { $null }

function Get-Shot([string] $language, [string] $theme, [string] $outPath) {
    Get-Process -Name 'FileMerge' -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 400

    $json = '{ "Language": "' + $language + '", "Theme": "' + $theme + '", "ExistingFile": "Ask" }'
    [IO.File]::WriteAllText($settingsPath, $json, [Text.UTF8Encoding]::new($false))

    $arguments = @($Files | ForEach-Object { '"' + $_ + '"' })
    $p = if ($arguments.Count) { Start-Process -FilePath $Exe -ArgumentList $arguments -PassThru }
         else { Start-Process -FilePath $Exe -PassThru }

    $deadline = (Get-Date).AddSeconds(15)
    while ($p.MainWindowHandle -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200; $p.Refresh()
    }
    Start-Sleep -Seconds 2
    $h = $p.MainWindowHandle

    # The window rect includes an invisible resize border; the DWM frame bounds are what is seen.
    $outer = New-Object StoreShotWin+RECT; [void][StoreShotWin]::GetWindowRect($h, [ref] $outer)
    $frame = New-Object StoreShotWin+RECT; [void][StoreShotWin]::DwmGetWindowAttribute($h, 9, [ref] $frame, 16)
    $left = $frame.Left - $outer.Left; $top = $frame.Top - $outer.Top
    $right = $outer.Right - $frame.Right; $bottom = $outer.Bottom - $frame.Bottom
    [void][StoreShotWin]::MoveWindow($h, -$left, -$top, 1920 + $left + $right, 1080 + $top + $bottom, $true)

    # Topmost, so nothing else on the desktop ends up in the capture.
    [void][StoreShotWin]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 0x0003)
    [void][StoreShotWin]::SetForegroundWindow($h)
    Start-Sleep -Seconds 2

    [void][StoreShotWin]::DwmGetWindowAttribute($h, 9, [ref] $frame, 16)
    $w = $frame.Right - $frame.Left; $hh = $frame.Bottom - $frame.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $hh)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($frame.Left, $frame.Top, 0, 0, (New-Object System.Drawing.Size($w, $hh)))
    $g.Dispose()
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    $null = $p.CloseMainWindow(); Start-Sleep -Milliseconds 800
    if (-not $p.HasExited) { $p | Stop-Process -Force }
    Write-Output ("{0,-8} {1,-5} {2}x{3}" -f $language, $theme, $w, $hh)
}

try {
    foreach ($language in $Languages) {
        foreach ($theme in 'Light', 'Dark') {
            Get-Shot $language $theme (Join-Path $OutDir ("{0}-{1}.png" -f $language, $theme.ToLowerInvariant()))
        }
    }
}
finally {
    if ($null -ne $backup) { [IO.File]::WriteAllBytes($settingsPath, $backup) }
}
