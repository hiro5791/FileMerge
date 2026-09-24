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


# Green, for walking: てくてく is the sound of someone going along on foot.
$TileLight = [System.Drawing.Color]::FromArgb(255, 72, 199, 142)
$TileDark  = [System.Drawing.Color]::FromArgb(255, 22, 150, 110)
$Ink       = [System.Drawing.Color]::White

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
    One footprint: a sole and, when there is room for them, three toes. Drawn around its own
    centre and tilted, so a pair of them reads as a step toward the upper right.
#>
function Draw-Foot {
    param($G, $Brush, [float] $Cx, [float] $Cy, [float] $Unit, [float] $Angle, [bool] $Toes)

    $state = $G.Save()
    $G.TranslateTransform($Cx, $Cy)
    $G.RotateTransform($Angle)

    $soleW = [float] (0.11 * $Unit)
    $soleH = [float] (0.16 * $Unit)
    $G.FillEllipse($Brush, [float] (-$soleW / 2), [float] (-$soleH / 2), $soleW, $soleH)

    if ($Toes) {
        $r = [float] (0.022 * $Unit)
        foreach ($toe in @(@(-0.045, -0.125), @(0.0, -0.142), @(0.045, -0.125))) {
            $tx = [float] ($toe[0] * $Unit - $r)
            $ty = [float] ($toe[1] * $Unit - $r)
            $G.FillEllipse($Brush, $tx, $ty, [float] (2 * $r), [float] (2 * $r))
        }
    }

    $G.Restore($state)
}

<#
    Draws the mark: footprints walking up to a sheet of paper, for てくてく and for the file
    they arrive at. Small sizes drop detail rather than shrink it into noise: the toes go
    below 48 px, and below 24 px a single sole stands in for the pair.
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
        $radius = [float] [Math]::Max(2.0, $s * 0.22)
        $shape = New-RoundedPath -X 0 -Y 0 -W $s -H $s -R $radius
        $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            [System.Drawing.PointF]::new(0, 0),
            [System.Drawing.PointF]::new($s, $s),
            $TileLight,
            $TileDark)
        $g.FillPath($brush, $shape)
        $brush.Dispose()
        $shape.Dispose()
    }

    $white = New-Object System.Drawing.SolidBrush($Ink)

    if ($Size -lt 24) {
        # One sole and a larger sheet: the most a 16 px square can say.
        Draw-Foot -G $g -Brush $white -Cx ([float] (0.30 * $s)) -Cy ([float] (0.64 * $s)) -Unit ([float] (1.5 * $s)) -Angle 20 -Toes $false
        $sheet = New-RoundedPath -X ([float] (0.50 * $s)) -Y ([float] (0.16 * $s)) -W ([float] (0.36 * $s)) -H ([float] (0.50 * $s)) -R ([float] [Math]::Max(1.0, 0.06 * $s))
        $g.FillPath($white, $sheet)
        $sheet.Dispose()
    }
    else {
        $toes = $Size -ge 48
        Draw-Foot -G $g -Brush $white -Cx ([float] (0.25 * $s)) -Cy ([float] (0.73 * $s)) -Unit $s -Angle 20 -Toes $toes
        Draw-Foot -G $g -Brush $white -Cx ([float] (0.41 * $s)) -Cy ([float] (0.53 * $s)) -Unit $s -Angle 20 -Toes $toes

        $sheet = New-RoundedPath -X ([float] (0.56 * $s)) -Y ([float] (0.19 * $s)) -W ([float] (0.27 * $s)) -H ([float] (0.36 * $s)) -R ([float] [Math]::Max(1.0, 0.04 * $s))
        $g.FillPath($white, $sheet)
        $sheet.Dispose()

        $pen = New-Object System.Drawing.Pen($TileDark, [float] [Math]::Max(1.0, 0.03 * $s))
        $rows = if ($Size -ge 48) { @(0.29, 0.36, 0.43) } else { @(0.31, 0.41) }
        foreach ($row in $rows) {
            $y = [float] ($row * $s)
            $g.DrawLine($pen, [float] (0.61 * $s), $y, [float] (0.78 * $s), $y)
        }
        $pen.Dispose()
    }

    $white.Dispose()
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
function ConvertTo-PngBytes {
    param([System.Drawing.Bitmap] $Bitmap)

    $stream = New-Object System.IO.MemoryStream
    $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    return , $bytes
}

<#
    An .ico image entry in the old format: a BITMAPINFOHEADER whose height is doubled, the
    BGRA pixels bottom row first, then a 1-bit AND mask. With 32-bit colour the alpha channel
    does the real work, so the mask only marks the fully transparent pixels.
#>
function ConvertTo-DibBytes {
    param([System.Drawing.Bitmap] $Bitmap)

    $w = $Bitmap.Width
    $h = $Bitmap.Height
    $maskStride = [int] ([Math]::Ceiling($w / 32.0) * 4)

    $out = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($out)

    $writer.Write([uint32] 40)          # header size
    $writer.Write([int32] $w)
    $writer.Write([int32] ($h * 2))     # colour rows plus mask rows
    $writer.Write([uint16] 1)           # planes
    $writer.Write([uint16] 32)          # bits per pixel
    $writer.Write([uint32] 0)           # BI_RGB
    $writer.Write([uint32] (($w * $h * 4) + ($maskStride * $h)))
    $writer.Write([int32] 0)
    $writer.Write([int32] 0)
    $writer.Write([uint32] 0)
    $writer.Write([uint32] 0)

    for ($y = $h - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $Bitmap.GetPixel($x, $y)
            $writer.Write([byte] $c.B)
            $writer.Write([byte] $c.G)
            $writer.Write([byte] $c.R)
            $writer.Write([byte] $c.A)
        }
    }

    for ($y = $h - 1; $y -ge 0; $y--) {
        $row = New-Object byte[] $maskStride
        for ($x = 0; $x -lt $w; $x++) {
            if ($Bitmap.GetPixel($x, $y).A -eq 0) {
                $row[[int][Math]::Floor($x / 8)] = $row[[int][Math]::Floor($x / 8)] -bor (0x80 -shr ($x % 8))
            }
        }
        $writer.Write($row)
    }

    $writer.Flush()
    $bytes = $out.ToArray()
    $writer.Dispose()
    $out.Dispose()
    return , $bytes
}

function Save-Ico {
    param([int[]] $Sizes, [string] $Path)

    # 256 px is stored as PNG, which keeps the file small; everything smaller is a classic
    # 32-bit DIB. Explorer reads PNG at any size, but not every consumer of .ico does, and the
    # small frames are exactly the ones the title bar and taskbar ask for.
    $frames = foreach ($size in $Sizes) {
        $bitmap = New-IconBitmap -Size $size
        $bytes = if ($size -ge 256) { ConvertTo-PngBytes $bitmap } else { ConvertTo-DibBytes $bitmap }
        $bitmap.Dispose()
        [pscustomobject]@{ Size = $size; Bytes = $bytes }
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
