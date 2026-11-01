<#
.SYNOPSIS
  Renders the wintty application icon (assets/logo/wintty-icon.svg geometry) to a multi-size .ico and a PNG.
.DESCRIPTION
  Keep the geometry here in step with the SVG. Each size is drawn directly at its pixel size with
  anti-aliasing, so small sizes stay crisp. Output goes to src/Wintty.App/Assets by default.
#>
param(
  [string] $OutDirectory = (Join-Path $PSScriptRoot '..\src\Wintty.App\Assets')
)

Add-Type -AssemblyName System.Drawing

function New-RoundedRect([float] $x, [float] $y, [float] $w, [float] $h, [float] $r) {
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = 2 * $r
  $path.AddArc($x, $y, $d, $d, 180, 90)
  $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
  $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
  $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
  $path.CloseFigure()
  return $path
}

function Get-IconPng([int] $size) {
  $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bitmap)
  $g.SmoothingMode = 'AntiAlias'
  $g.PixelOffsetMode = 'HighQuality'
  $g.Clear([System.Drawing.Color]::Transparent)
  $g.ScaleTransform($size / 256.0, $size / 256.0)

  $background = New-RoundedRect 0 0 256 256 56
  $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#1a1d24'))), $background)

  $g.TranslateTransform(12, -4)
  $pen = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#4fd1b5')), 18
  $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
  $points = @(36, 92, 66, 172, 96, 116, 126, 172, 156, 92)
  $g.DrawLines($pen, [System.Drawing.PointF[]]@(
    for ($i = 0; $i -lt $points.Count; $i += 2) { New-Object System.Drawing.PointF $points[$i], $points[$i + 1] }))

  $cursor = New-RoundedRect 172 164 38 16 4
  $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#ffb454'))), $cursor)
  $g.Dispose()

  $stream = New-Object System.IO.MemoryStream
  $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
  $bitmap.Dispose()
  return , $stream.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($size in $sizes) { , (Get-IconPng $size) }

New-Item -ItemType Directory -Force $OutDirectory | Out-Null

# ICO container: header, one directory entry per image, then the PNG payloads.
$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $stream
$writer.Write([uint16] 0); $writer.Write([uint16] 1); $writer.Write([uint16] $sizes.Count)
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
  $writer.Write([byte] $dimension); $writer.Write([byte] $dimension)
  $writer.Write([byte] 0); $writer.Write([byte] 0)
  $writer.Write([uint16] 1); $writer.Write([uint16] 32)
  $writer.Write([uint32] $images[$i].Length); $writer.Write([uint32] $offset)
  $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write($image) }
$writer.Flush()

[System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'wintty.ico'), $stream.ToArray())
[System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'wintty-256.png'), $images[-1])
Write-Host "Wrote wintty.ico and wintty-256.png to $OutDirectory"
