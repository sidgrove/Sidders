<#
.SYNOPSIS
  Renders the app and tray icons: a simple microphone on a brand-blue tile.

.DESCRIPTION
  The same mark the app draws in its header — a filled capsule, a cradle, a stem and a base
  — in white on the brand tile. The recording variant lights the capsule rose.

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
$rose        = [System.Drawing.Color]::FromArgb(0xFF, 0xB1, 0xC6)

function Render([int]$size, [bool]$recording) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Tile.
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

    # The mark occupies the middle 62% of the tile.
    $s = $size * 0.62
    $ox = ($size - $s) / 2
    $oy = ($size - $s) / 2
    $cx = $ox + $s / 2
    $stroke = [Math]::Max(1.0, $s * 0.09)
    $pen = New-Object System.Drawing.Pen $white, $stroke
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'

    # Capsule.
    $headW = $s * 0.34; $headH = $s * 0.56
    $hp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $hr = $headW / 2
    $hx = $cx - $hr; $hy = $oy + $s * 0.04
    $hp.AddArc($hx, $hy, $headW, $headW, 180, 180)
    $hp.AddArc($hx, $hy + $headH - $headW, $headW, $headW, 0, 180)
    $hp.CloseFigure()
    $headColour = if ($recording) { $rose } else { $white }
    $g.FillPath((New-Object System.Drawing.SolidBrush $headColour), $hp)

    # Cradle: the lower half of a circle around the head.
    $cr = $s * 0.30
    $ccy = $oy + $s * 0.44
    $g.DrawArc($pen, ($cx - $cr), ($ccy - $cr), (2 * $cr), (2 * $cr), 0, 180)

    # Stem and base.
    $g.DrawLine($pen, $cx, ($ccy + $cr), $cx, ($oy + $s * 0.90))
    $g.DrawLine($pen, ($cx - $s * 0.18), ($oy + $s * 0.92), ($cx + $s * 0.18), ($oy + $s * 0.92))

    $g.Dispose()
    return $bmp
}

function Write-Ico([string]$path, [int[]]$sizes, [bool]$recording) {
    $images = foreach ($sz in $sizes) {
        $bmp = Render $sz $recording
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        ,@{ Size = $sz; Bytes = $ms.ToArray() }
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
