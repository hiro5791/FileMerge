<#
.SYNOPSIS
    Generates the application icon and the Microsoft Store tile assets.

.DESCRIPTION
    The artwork is drawn in code rather than checked in as binaries, so the icon can be
    regenerated at any size and the repository stays free of opaque image blobs.

    Run it after changing the palette or the mark:
        pwsh -File build\New-Icons.ps1
#>
[CmdletBinding()]
param(
    [string] $IconPath,
    [string] $StoreImageDir
)

$ErrorActionPreference = 'Stop'

# $PSScriptRoot is not reliably populated inside param defaults, so the repo root is
# resolved here instead.
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
if (-not $IconPath)      { $IconPath = Join-Path $repoRoot 'src\FileMerge\Assets\app.ico' }
if (-not $StoreImageDir) { $StoreImageDir = Join-Path $repoRoot 'packaging\msix\Images' }

Add-Type -AssemblyName System.Drawing

$Accent      = [System.Drawing.Color]::FromArgb(255, 15, 108, 189)
$AccentLight = [System.Drawing.Color]::FromArgb(255, 71, 158, 245)
$Ink         = [System.Drawing.Color]::White

function New-RoundedPath {
    param([float] $X, [float] $Y, [float] $W, [float] $H, [float] $R)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $R * 2
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

<#
    Draws the mark: two strands entering from the left that converge in the middle and
    leave as one. It stays legible down to 16 px because it is only three strokes.
#>
function New-IconBitmap {
    param(
        [int] $Size,
        [switch] $Transparent
    )

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [float] $Size

    if (-not $Transparent) {
        $radius = [Math]::Max(2.0, $s * 0.22)
        $shape = New-RoundedPath -X 0 -Y 0 -W $s -H $s -R $radius
        $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF(0, 0)),
            (New-Object System.Drawing.PointF($s, $s)),
            $AccentLight,
            $Accent)
        $g.FillPath($brush, $shape)
        $brush.Dispose()
        $shape.Dispose()
    }

    $stroke = [Math]::Max(1.0, $s * 0.085)
    $pen = New-Object System.Drawing.Pen($Ink, $stroke)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $left   = $s * 0.24
    $mid    = $s * 0.52
    $right  = $s * 0.78
    $top    = $s * 0.28
    $bottom = $s * 0.72
    $center = $s * 0.50

    # The comma binds tighter than + in PowerShell, so every coordinate is computed first.
    $joinX = [float] ($center + $s * 0.10)
    $tailX = [float] ($center + $s * 0.02)

    $junction = [System.Drawing.PointF]::new($joinX, $center)

    # Upper strand, lower strand, then the single merged strand leaving to the right.
    $g.DrawLines($pen, @(
        [System.Drawing.PointF]::new($left, $top),
        [System.Drawing.PointF]::new($mid, $top),
        $junction
    ))
    $g.DrawLines($pen, @(
        [System.Drawing.PointF]::new($left, $bottom),
        [System.Drawing.PointF]::new($mid, $bottom),
        $junction
    ))
    $g.DrawLine($pen,
        [System.Drawing.PointF]::new($tailX, $center),
        [System.Drawing.PointF]::new($right, $center))

    $pen.Dispose()
    $g.Dispose()
    return $bitmap
}

function Save-Png {
    param([System.Drawing.Bitmap] $Bitmap, [string] $Path)

    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

<#
    Writes a multi-resolution .ico. Each frame is stored as PNG, which Windows has
    accepted inside .ico since Vista and which keeps the file small at 256 px.
#>
function Save-Ico {
    param([int[]] $Sizes, [string] $Path)

    $frames = foreach ($size in $Sizes) {
        $bitmap = New-IconBitmap -Size $size
        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
        [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
        $stream.Dispose()
    }

    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

    $out = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($out)

    $writer.Write([uint16] 0)              # reserved
    $writer.Write([uint16] 1)              # type: icon
    $writer.Write([uint16] $frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $dim = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
        $writer.Write([byte] $dim)         # width
        $writer.Write([byte] $dim)         # height
        $writer.Write([byte] 0)            # palette entries
        $writer.Write([byte] 0)            # reserved
        $writer.Write([uint16] 1)          # colour planes
        $writer.Write([uint16] 32)         # bits per pixel
        $writer.Write([uint32] $frame.Bytes.Length)
        $writer.Write([uint32] $offset)
        $offset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) {
        $writer.Write($frame.Bytes)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($Path, $out.ToArray())
    $writer.Dispose()
    $out.Dispose()
}

<#
    Store tiles are a logo centred on a transparent canvas of the exact required size,
    which lets Windows apply its own tile background.
#>
function Save-Tile {
    param([int] $Width, [int] $Height, [double] $Scale, [string] $Path)

    $canvas = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $logoSize = [int] ([Math]::Min($Width, $Height) * $Scale)
    $logo = New-IconBitmap -Size $logoSize
    $g.DrawImage($logo, [int](($Width - $logoSize) / 2), [int](($Height - $logoSize) / 2), $logoSize, $logoSize)

    $logo.Dispose()
    $g.Dispose()
    Save-Png -Bitmap $canvas -Path $Path
    $canvas.Dispose()
}

# ---------------------------------------------------------------- Application icon

$IconPath = [System.IO.Path]::GetFullPath($IconPath)
Save-Ico -Sizes @(16, 24, 32, 48, 64, 128, 256) -Path $IconPath
Write-Output "icon  -> $IconPath"

# ---------------------------------------------------------------- Store assets

$StoreImageDir = [System.IO.Path]::GetFullPath($StoreImageDir)

$tiles = @(
    @{ File = 'Square44x44Logo.png';                 W = 44;  H = 44;  S = 0.86 },
    @{ File = 'Square44x44Logo.targetsize-24.png';   W = 24;  H = 24;  S = 1.00 },
    @{ File = 'Square44x44Logo.targetsize-32.png';   W = 32;  H = 32;  S = 1.00 },
    @{ File = 'Square44x44Logo.targetsize-48.png';   W = 48;  H = 48;  S = 1.00 },
    @{ File = 'Square44x44Logo.targetsize-256.png';  W = 256; H = 256; S = 1.00 },
    @{ File = 'Square71x71Logo.png';                 W = 71;  H = 71;  S = 0.66 },
    @{ File = 'Square150x150Logo.png';               W = 150; H = 150; S = 0.60 },
    @{ File = 'Square310x310Logo.png';               W = 310; H = 310; S = 0.50 },
    @{ File = 'Wide310x150Logo.png';                 W = 310; H = 150; S = 0.62 },
    @{ File = 'StoreLogo.png';                       W = 50;  H = 50;  S = 0.88 },
    @{ File = 'SplashScreen.png';                    W = 620; H = 300; S = 0.42 }
)

foreach ($tile in $tiles) {
    $path = Join-Path $StoreImageDir $tile.File
    Save-Tile -Width $tile.W -Height $tile.H -Scale $tile.S -Path $path
}

Write-Output "tiles -> $StoreImageDir ($($tiles.Count) files)"
