param(
    [string]$Source = "src/DuctSizer.WinForms/Assets/DuctSizerCentrifugalIconSource.png",
    [string]$PngOutput = "src/DuctSizer.WinForms/Assets/DuctSizerCentrifugalIcon.png",
    [string]$IcoOutput = "src/DuctSizer.WinForms/Assets/DuctSizerCentrifugalIcon.ico"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing.Common

function Get-OpaqueBounds([System.Drawing.Bitmap]$Bitmap) {
    $left = $Bitmap.Width
    $top = $Bitmap.Height
    $right = -1
    $bottom = -1
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            if ($Bitmap.GetPixel($x, $y).A -le 8) { continue }
            $left = [Math]::Min($left, $x)
            $top = [Math]::Min($top, $y)
            $right = [Math]::Max($right, $x)
            $bottom = [Math]::Max($bottom, $y)
        }
    }
    if ($right -lt $left -or $bottom -lt $top) {
        return [System.Drawing.Rectangle]::new(0, 0, $Bitmap.Width, $Bitmap.Height)
    }
    return [System.Drawing.Rectangle]::FromLTRB($left, $top, $right + 1, $bottom + 1)
}

function Save-MultiSizeIcon([System.Drawing.Image]$Image, [string]$Path) {
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
    $payloads = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($Image, 0, 0, $size, $size)
            $stream = [System.IO.MemoryStream]::new()
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $payloads.Add($stream.ToArray())
            $stream.Dispose()
        }
        finally {
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }

    $file = [System.IO.File]::Create((Join-Path (Get-Location) $Path))
    $writer = [System.IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$sizes.Count)
        $offset = 6 + (16 * $sizes.Count)
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $size = $sizes[$i]
            $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
            $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$payloads[$i].Length)
            $writer.Write([uint32]$offset)
            $offset += $payloads[$i].Length
        }
        foreach ($payload in $payloads) { $writer.Write($payload) }
    }
    finally {
        $writer.Dispose()
        $file.Dispose()
    }
}

$sourcePath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Source))
if (-not [System.IO.File]::Exists($sourcePath)) { throw "Icon source was not found: $sourcePath" }
$sourceImage = [System.Drawing.Bitmap]::FromFile($sourcePath)
$canvas = [System.Drawing.Bitmap]::new(1024, 1024, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($canvas)
try {
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $sourceBounds = Get-OpaqueBounds $sourceImage
    $available = 948.0
    $scale = [Math]::Min($available / $sourceBounds.Width, $available / $sourceBounds.Height)
    $drawWidth = [Math]::Max(1, [int][Math]::Round($sourceBounds.Width * $scale))
    $drawHeight = [Math]::Max(1, [int][Math]::Round($sourceBounds.Height * $scale))
    $destination = [System.Drawing.Rectangle]::new(
        [int][Math]::Round((1024 - $drawWidth) / 2.0),
        [int][Math]::Round((1024 - $drawHeight) / 2.0),
        $drawWidth,
        $drawHeight)
    $g.DrawImage($sourceImage, $destination, $sourceBounds, [System.Drawing.GraphicsUnit]::Pixel)

    $outputDirectory = Split-Path -Parent $PngOutput
    if ($outputDirectory) { [System.IO.Directory]::CreateDirectory((Join-Path (Get-Location) $outputDirectory)) | Out-Null }
    $canvas.Save((Join-Path (Get-Location) $PngOutput), [System.Drawing.Imaging.ImageFormat]::Png)
    Save-MultiSizeIcon $canvas $IcoOutput
}
finally {
    $g.Dispose()
    $canvas.Dispose()
    $sourceImage.Dispose()
}

Write-Output "Generated $PngOutput and $IcoOutput"
