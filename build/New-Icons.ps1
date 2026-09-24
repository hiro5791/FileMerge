<#
.SYNOPSIS
    Generates the application icon and the Microsoft Store tile assets from the artwork.

.DESCRIPTION
    Two source images live in artwork\:
      icon.png         the full mark, used for every size of 32 px and up
      icon-small.png   a simplified mark (one footprint, a blank sheet) for 16 and 24 px,
                       where the full one blurs into noise

    Both are cleaned before use: pixels that are almost transparent are made fully transparent
    and pixels that are almost opaque are made fully opaque. The generated source had a faint
    halo just outside the tile and a tile that was about 1% see-through; neither is visible at
    a glance, but both show up as a fringe on dark backgrounds.

    Run it after replacing either image:
        powershell -File build\New-Icons.ps1
#>
[CmdletBinding()]
param(
    [string] $Artwork,
    [string] $SmallArtwork,
    [string] $IconPath,
    [string] $StoreImageDir
)

$ErrorActionPreference = 'Stop'

# $PSScriptRoot is not reliably populated inside param defaults, so the repo root is
# resolved here instead.
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
if (-not $Artwork)       { $Artwork = Join-Path $repoRoot 'artwork\icon.png' }
if (-not $SmallArtwork)  { $SmallArtwork = Join-Path $repoRoot 'artwork\icon-small.png' }
if (-not $IconPath)      { $IconPath = Join-Path $repoRoot 'src\FileMerge\Assets\app.ico' }
if (-not $StoreImageDir) { $StoreImageDir = Join-Path $repoRoot 'packaging\msix\Images' }

Add-Type -AssemblyName System.Drawing

# Touching 1.5 million pixels one at a time is far too slow in PowerShell, so the clean-up
# pass is a few lines of C#.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class ArtworkCleaner
{
    public static void Clean(Bitmap bitmap, int floor, int ceiling)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int length = data.Stride * bitmap.Height;
        var pixels = new byte[length];
        Marshal.Copy(data.Scan0, pixels, 0, length);

        for (int i = 0; i < length; i += 4)
        {
            byte alpha = pixels[i + 3];
            if (alpha <= floor)
            {
                pixels[i] = 0; pixels[i + 1] = 0; pixels[i + 2] = 0; pixels[i + 3] = 0;
            }
            else if (alpha >= ceiling)
            {
                pixels[i + 3] = 255;
            }
        }

        Marshal.Copy(pixels, 0, data.Scan0, length);
        bitmap.UnlockBits(data);
    }
}
'@

<#
    Loads a source image, cleans its alpha, and returns it premultiplied. Scaling a
    premultiplied bitmap keeps the transparent surround from bleeding a dark fringe into the
    tile's edge as it shrinks.
#>
function Import-Artwork {
    param([string] $Path)

    if (-not (Test-Path $Path)) { throw "artwork not found: $Path" }

    $loaded = [System.Drawing.Bitmap]::FromFile($Path)
    $clean = New-Object System.Drawing.Bitmap($loaded.Width, $loaded.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($clean)
    $g.DrawImage($loaded, 0, 0, $loaded.Width, $loaded.Height)
    $g.Dispose()
    $loaded.Dispose()

    [ArtworkCleaner]::Clean($clean, 16, 240)

    $rect = [System.Drawing.Rectangle]::new(0, 0, $clean.Width, $clean.Height)
    $premultiplied = $clean.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppPArgb)
    $clean.Dispose()
    return $premultiplied
}

$MainArt = Import-Artwork $Artwork
$SmallArt = Import-Artwork $SmallArtwork

<#
    The mark at one size. 24 px and below use the simplified artwork; from 32 px up the full
    one holds together.
#>
function New-IconBitmap {
    param([int] $Size)

    $source = if ($Size -le 24) { $SmallArt } else { $MainArt }

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    # Mirroring at the border stops the resampler pulling in black from outside the image.
    $attributes = New-Object System.Drawing.Imaging.ImageAttributes
    $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)

    $target = [System.Drawing.Rectangle]::new(0, 0, $Size, $Size)
    $g.DrawImage($source, $target, 0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)

    $attributes.Dispose()
    $g.Dispose()
    return $bitmap
}

function Save-Png {
    param([System.Drawing.Bitmap] $Bitmap, [string] $Path)

    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

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

<#
    Writes a multi-resolution .ico. 256 px is stored as PNG, which keeps the file small;
    everything smaller is a classic 32-bit DIB. Explorer reads PNG at any size, but not every
    consumer of .ico does, and the small frames are the ones the title bar and taskbar ask for.
#>
function Save-Ico {
    param([int[]] $Sizes, [string] $Path)

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
    Store tiles are the logo centred on a transparent canvas of the exact required size,
    which lets Windows apply its own tile background.
#>
function Save-Tile {
    param([int] $Width, [int] $Height, [double] $Scale, [string] $Path)

    $canvas = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    $g.Clear([System.Drawing.Color]::Transparent)

    $logoSize = [int] ([Math]::Min($Width, $Height) * $Scale)
    $logo = New-IconBitmap -Size $logoSize
    $g.DrawImageUnscaled($logo, [int](($Width - $logoSize) / 2), [int](($Height - $logoSize) / 2))

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

$MainArt.Dispose()
$SmallArt.Dispose()
