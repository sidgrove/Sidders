<#
.SYNOPSIS
  Renders the app and tray icons from the design tokens' palette.

.DESCRIPTION
  Draws a cassette motif — a black chassis, a silver panel, two hub windows and the record
  lamp — at every size Windows asks for, and packs them into .ico containers. Run it once
  after changing the design; the outputs are committed.

  Output:
    src/Murmur.App/Assets/app.ico        16 · 24 · 32 · 48 · 64 · 128 · 256
    src/Murmur.App/Assets/tray.ico       16 · 24 · 32   (idle)
    src/Murmur.App/Assets/tray-rec.ico   16 · 24 · 32   (record lamp lit)
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root   = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'src\Murmur.App\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

# Tokens.Colors, black face — the icon reads on light and dark taskbars alike.
$chassis   = [System.Drawing.Color]::FromArgb(0x12, 0x11, 0x10)
$panel     = [System.Drawing.Color]::FromArgb(0xB8, 0xB4, 0xAD)
$highlight = [System.Drawing.Color]::FromArgb(0xC9, 0xC5, 0xBE)
$deck      = [System.Drawing.Color]::FromArgb(0x38, 0x35, 0x2F)
$seam      = [System.Drawing.Color]::FromArgb(0x6B, 0x68, 0x62)
$record    = [System.Drawing.Color]::FromArgb(0xC8, 0x34, 0x2A)
$recordOff = [System.Drawing.Color]::FromArgb(0x4A, 0x27, 0x24)
$ink       = [System.Drawing.Color]::FromArgb(0x1C, 0x1A, 0x17)

function Render([int]$size, [bool]$lit) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    $u = $size / 32.0            # one unit at 32 px

    # Chassis: rounded square.
    $radius = [Math]::Max(1, 4 * $u)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = New-Object System.Drawing.RectangleF 0, 0, $size, $size
    $d = $radius * 2
    $path.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $path.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $path.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $chassis), $path)

    # Silver panel inset.
    $inset = 3 * $u
    $pr = New-Object System.Drawing.RectangleF $inset, (7 * $u), ($size - 2 * $inset), ($size - 10 * $u)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $panel), $pr)
    $g.DrawLine((New-Object System.Drawing.Pen $highlight, ([Math]::Max(1, $u))), $pr.X, $pr.Y, $pr.Right, $pr.Y)
    $g.DrawRectangle((New-Object System.Drawing.Pen $seam, ([Math]::Max(1, $u * 0.8))), $pr.X, $pr.Y, $pr.Width, $pr.Height)

    # Deck window with two hubs.
    $wr = New-Object System.Drawing.RectangleF ($inset + 3 * $u), (10 * $u), ($size - 2 * $inset - 6 * $u), (11 * $u)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $deck), $wr)
    $hub = 5 * $u
    $hy = $wr.Y + ($wr.Height - $hub) / 2
    foreach ($hx in @(($wr.X + 2.5 * $u), ($wr.Right - 2.5 * $u - $hub))) {
        $g.FillEllipse((New-Object System.Drawing.SolidBrush $panel), $hx, $hy, $hub, $hub)
        $g.FillEllipse((New-Object System.Drawing.SolidBrush $deck), ($hx + 1.6 * $u), ($hy + 1.6 * $u), ($hub - 3.2 * $u), ($hub - 3.2 * $u))
    }

    # The record lamp, top-left on the chassis.
    $lamp = 4 * $u
    $lc = if ($lit) { $record } else { $recordOff }
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $lc), (3.5 * $u), (2 * $u), $lamp, $lamp)
    if ($lit -and $size -ge 24) {
        $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(115, 255, 255, 255))), (4.3 * $u), (2.7 * $u), ($lamp * 0.3), ($lamp * 0.3))
    }

    # Transport keys along the bottom of the panel.
    if ($size -ge 24) {
        $ky = $pr.Bottom - 4.5 * $u
        for ($i = 0; $i -lt 3; $i++) {
            $kx = $wr.X + $i * 4.4 * $u
            $g.FillRectangle((New-Object System.Drawing.SolidBrush $ink), $kx, $ky, (3.4 * $u), (2.4 * $u))
        }
    }

    $g.Dispose()
    return $bmp
}

function Write-Ico([string]$path, [int[]]$sizes, [bool]$lit) {
    $images = foreach ($s in $sizes) {
        $bmp = Render $s $lit
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
