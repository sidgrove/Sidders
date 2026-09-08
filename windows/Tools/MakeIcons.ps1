<#
.SYNOPSIS
  Renders the app and tray icons in the Sidgrove identity.

.DESCRIPTION
  The Sidgrove mark — a thick rounded 45° stroke and a dot, "/." — in white on a
  brand-blue rounded tile, redrawn from the site's favicon.svg geometry. The recording
  variant swaps the dot for the muted rose so the tray shows state at a glance.

  Output:
    src/Murmur.App/Assets/app.ico        16 · 24 · 32 · 48 · 64 · 128 · 256
    src/Murmur.App/Assets/tray.ico       16 · 24 · 32   (idle)
    src/Murmur.App/Assets/tray-rec.ico   16 · 24 · 32   (recording)
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root   = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'src\Murmur.App\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

$brand       = [System.Drawing.Color]::FromArgb(0x68, 0x74, 0xB4)
$brandStrong = [System.Drawing.Color]::FromArgb(0x3D, 0x47, 0x85)
$white       = [System.Drawing.Color]::White
$rose        = [System.Drawing.Color]::FromArgb(0xFF, 0x9F, 0xBC)   # rose-mid on blue reads as "live"

function Render([int]$size, [bool]$recording) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Tile: brand gradient, radius ~22% like the app's 10px-on-44px logo box.
    $radius = [Math]::Max(2, $size * 0.22)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point $size, $size), $brand, $brandStrong
    $g.FillPath($brush, $path)

    # The mark, in the favicon's 100-unit space scaled to fill ~76% of the tile.
    $u = $size / 100.0
    $g.TranslateTransform(50 * $u, 50 * $u)
    $g.ScaleTransform(0.76, 0.76)
    $g.TranslateTransform(-48.1 * $u, -49.15 * $u)

    # Diagonal: a rounded rect 86.4 x 19 centred at (39.5, 49.15), rotated -44.6°.
    $state = $g.Save()
    $g.TranslateTransform(39.5 * $u, 49.15 * $u)
    $g.RotateTransform(-44.6)
    $rw = 86.4 * $u; $rh = 19 * $u; $rr = 6.5 * $u
    $rp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $rp.AddArc(-$rw/2, -$rh/2, 2*$rr, 2*$rr, 180, 90)
    $rp.AddArc($rw/2 - 2*$rr, -$rh/2, 2*$rr, 2*$rr, 270, 90)
    $rp.AddArc($rw/2 - 2*$rr, $rh/2 - 2*$rr, 2*$rr, 2*$rr, 0, 90)
    $rp.AddArc(-$rw/2, $rh/2 - 2*$rr, 2*$rr, 2*$rr, 90, 90)
    $rp.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $white), $rp)
    $g.Restore($state)

    # The dot at (82.3, 72.3) r 11.8 — rose while recording.
    $dot = if ($recording) { $rose } else { $white }
    $r = 11.8 * $u
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $dot), (82.3 * $u - $r), (72.3 * $u - $r), 2*$r, 2*$r)

    $g.Dispose()
    return $bmp
}

function Write-Ico([string]$path, [int[]]$sizes, [bool]$recording) {
    $images = foreach ($s in $sizes) {
        $bmp = Render $s $recording
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        ,@{ Size = $s; Bytes = $ms.ToArray() }
    }

    $fs = [System.IO.File]::Create($path)
    $w = New-Object System.IO.BinaryWriter $fs
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($img in $images) {
        $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }
        $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32)
        $w.Write([uint32]$img.Bytes.Length); $w.Write([uint32]$offset)
        $offset += $img.Bytes.Length
    }
    foreach ($img in $images) { $w.Write($img.Bytes) }
    $w.Dispose(); $fs.Dispose()
    Write-Host "wrote $path ($($images.Count) sizes)"
}

Write-Ico (Join-Path $assets 'app.ico')      @(16, 24, 32, 48, 64, 128, 256) $false
Write-Ico (Join-Path $assets 'tray.ico')     @(16, 24, 32) $false
Write-Ico (Join-Path $assets 'tray-rec.ico') @(16, 24, 32) $true
