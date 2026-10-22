$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot "assets\Metron.png"
$outputPath = Join-Path $repoRoot "assets\Metron.ico"

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Icon source PNG was not found: $sourcePath"
}

Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Bitmap]::FromFile($sourcePath)
$sourceWidth = $source.Width
$sourceHeight = $source.Height
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = [System.Collections.Generic.List[byte[]]]::new()

try {
    if ($source.Width -ne $source.Height) {
        throw "The source logo must be square to preserve its full image in a square icon."
    }

    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new(
            $size,
            $size,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
        )
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            $imageAttributes = [System.Drawing.Imaging.ImageAttributes]::new()
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $imageAttributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
                $graphics.DrawImage(
                    $source,
                    [System.Drawing.Rectangle]::new(0, 0, $size, $size),
                    0,
                    0,
                    $sourceWidth,
                    $sourceHeight,
                    [System.Drawing.GraphicsUnit]::Pixel,
                    $imageAttributes
                )
            }
            finally {
                $imageAttributes.Dispose()
                $graphics.Dispose()
            }

            $pixelBytes = [byte[]]::new($size * $size * 4)
            $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
            $maskBytes = [byte[]]::new($maskStride * $size)

            for ($y = 0; $y -lt $size; $y++) {
                for ($x = 0; $x -lt $size; $x++) {
                    $color = $bitmap.GetPixel($x, $y)
                    $pixelOffset = (($size - 1 - $y) * $size + $x) * 4
                    $pixelBytes[$pixelOffset] = $color.B
                    $pixelBytes[$pixelOffset + 1] = $color.G
                    $pixelBytes[$pixelOffset + 2] = $color.R
                    $pixelBytes[$pixelOffset + 3] = $color.A

                    if ($color.A -eq 0) {
                        $maskOffset = ($size - 1 - $y) * $maskStride + [int][Math]::Floor($x / 8.0)
                        $maskBytes[$maskOffset] = $maskBytes[$maskOffset] -bor (0x80 -shr ($x % 8))
                    }
                }
            }

            $imageStream = [System.IO.MemoryStream]::new()
            try {
                $writer = [System.IO.BinaryWriter]::new($imageStream)
                try {
                    $imageSize = $pixelBytes.Length + $maskBytes.Length
                    $writer.Write([UInt32]40)
                    $writer.Write([Int32]$size)
                    $writer.Write([Int32]($size * 2))
                    $writer.Write([UInt16]1)
                    $writer.Write([UInt16]32)
                    $writer.Write([UInt32]0)
                    $writer.Write([UInt32]$imageSize)
                    $writer.Write([Int32]0)
                    $writer.Write([Int32]0)
                    $writer.Write([UInt32]0)
                    $writer.Write([UInt32]0)
                    $writer.Write($pixelBytes)
                    $writer.Write($maskBytes)
                    $images.Add($imageStream.ToArray())
                }
                finally {
                    $writer.Dispose()
                }
            }
            finally {
                $imageStream.Dispose()
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }
}
finally {
    $source.Dispose()
}

$outputStream = [System.IO.File]::Create($outputPath)
try {
    $writer = [System.IO.BinaryWriter]::new($outputStream)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$sizes.Count)

        $offset = 6 + (16 * $sizes.Count)
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $size = $sizes[$index]
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$images[$index].Length)
            $writer.Write([UInt32]$offset)
            $offset += $images[$index].Length
        }

        foreach ($image in $images) {
            $writer.Write($image)
        }
    }
    finally {
        $writer.Dispose()
    }
}
finally {
    $outputStream.Dispose()
}

Write-Output "Created $outputPath from $sourcePath (${sourceWidth}x${sourceHeight} PNG; sizes: $($sizes -join ', '))."
