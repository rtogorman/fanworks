param(
    [string]$Source = "src/DuctSizer.WinForms/Assets/DuctSizerFanIcon.png",
    [string]$PngOutput = "src/DuctSizer.WinForms/Assets/DuctSizerMechanicalIcon.png",
    [string]$IcoOutput = "src/DuctSizer.WinForms/Assets/DuctSizerMechanicalIcon.ico"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing.Common

function New-RoundedPath([System.Drawing.RectangleF]$Bounds, [single]$Radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = [Math]::Min($Radius * 2, [Math]::Min($Bounds.Width, $Bounds.Height))
    $arc = [System.Drawing.RectangleF]::new($Bounds.X, $Bounds.Y, $diameter, $diameter)
    $path.AddArc($arc, 180, 90)
    $arc.X = $Bounds.Right - $diameter
    $path.AddArc($arc, 270, 90)
    $arc.Y = $Bounds.Bottom - $diameter
    $path.AddArc($arc, 0, 90)
    $arc.X = $Bounds.Left
    $path.AddArc($arc, 90, 90)
    $path.CloseFigure()
    return $path
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

$sourcePath = (Resolve-Path $Source).Path
$sourceImage = [System.Drawing.Image]::FromFile($sourcePath)
$canvas = [System.Drawing.Bitmap]::new(1024, 1024, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($canvas)
try {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $shadowBounds = [System.Drawing.RectangleF]::new(38, 52, 948, 948)
    $shadowPath = New-RoundedPath $shadowBounds 178
    $shadowBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(78, 0, 12, 24))
    $g.FillPath($shadowBrush, $shadowPath)

    $housingBounds = [System.Drawing.RectangleF]::new(38, 32, 948, 948)
    $housingPath = New-RoundedPath $housingBounds 178
    $housingBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        $housingBounds,
        [System.Drawing.Color]::FromArgb(15, 79, 118),
        [System.Drawing.Color]::FromArgb(4, 23, 42),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillPath($housingBrush, $housingPath)
    $housingBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 82, 222, 230), 20)
    $g.DrawPath($housingBorder, $housingPath)

    $innerBounds = [System.Drawing.RectangleF]::new(72, 66, 880, 880)
    $innerPath = New-RoundedPath $innerBounds 148
    $innerBorder = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(115, 177, 237, 241), 6)
    $g.DrawPath($innerBorder, $innerPath)

    # Horizontal duct casing and flanged ends give the symbol a specific airside identity.
    $ductBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Rectangle]::new(88, 367, 848, 290),
        [System.Drawing.Color]::FromArgb(235, 18, 70, 104),
        [System.Drawing.Color]::FromArgb(235, 7, 35, 62),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($ductBrush, 88, 367, 848, 290)
    $ductPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(210, 83, 218, 225), 8)
    $g.DrawRectangle($ductPen, 88, 367, 848, 290)
    foreach ($x in @(88, 126, 898, 936)) {
        $g.DrawLine($ductPen, $x, 334, $x, 690)
    }

    # Flow chevrons remain visible at small sizes and distinguish the mark from a generic fan.
    $flowPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(220, 75, 225, 232), 18)
    $flowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $flowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    foreach ($x in @(158, 794)) {
        $g.DrawLine($flowPen, $x, 462, $x + 42, 512)
        $g.DrawLine($flowPen, $x + 42, 512, $x, 562)
    }

    # Mechanical radial vents and fasteners add detail around the retained fan artwork.
    $center = [System.Drawing.PointF]::new(512, 512)
    $ventPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(175, 123, 224, 232), 9)
    $ventPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $ventPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    for ($i = 0; $i -lt 16; $i++) {
        $angle = (($i * 22.5) - 90) * [Math]::PI / 180
        $p1 = [System.Drawing.PointF]::new(
            [single]($center.X + [Math]::Cos($angle) * 386),
            [single]($center.Y + [Math]::Sin($angle) * 386))
        $p2 = [System.Drawing.PointF]::new(
            [single]($center.X + [Math]::Cos($angle) * 420),
            [single]($center.Y + [Math]::Sin($angle) * 420))
        $g.DrawLine($ventPen, $p1, $p2)
    }

    $boltBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 161, 231, 235))
    $boltPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 4, 38, 63), 7)
    foreach ($point in @(
        [System.Drawing.PointF]::new(154, 154), [System.Drawing.PointF]::new(870, 154),
        [System.Drawing.PointF]::new(154, 870), [System.Drawing.PointF]::new(870, 870))) {
        $g.FillEllipse($boltBrush, $point.X - 18, $point.Y - 18, 36, 36)
        $g.DrawEllipse($boltPen, $point.X - 18, $point.Y - 18, 36, 36)
        $g.DrawLine($boltPen, $point.X - 9, $point.Y, $point.X + 9, $point.Y)
    }

    $g.DrawImage($sourceImage, [System.Drawing.Rectangle]::new(206, 206, 612, 612))

    $highlightPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(92, 255, 255, 255), 8)
    $g.DrawArc($highlightPen, 68, 62, 886, 886, 205, 118)

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
