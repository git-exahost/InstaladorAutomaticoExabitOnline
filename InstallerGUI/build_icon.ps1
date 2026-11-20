Add-Type -AssemblyName System.Drawing

$sourcePath = "InstallerGUI\Image\icon.png"
$outputPath = "InstallerGUI\install.ico"

if (-not (Test-Path $sourcePath)) {
    Write-Error "Arquivo de origem não encontrado: $sourcePath"
    exit 1
}

Write-Host "Carregando imagem de origem: $sourcePath"
$src = [System.Drawing.Bitmap]::FromFile((Resolve-Path $sourcePath).Path)
Write-Host ("Dimensoes: " + $src.Width + "x" + $src.Height + " | Formato: " + $src.PixelFormat)

$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$imagesData = @()

foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($src, 0, 0, $sz, $sz)
    $g.Dispose()

    if ($sz -eq 256) {
        # PNG format for 256x256
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $rawBytes = $ms.ToArray()
        $ms.Dispose()

        $imagesData += @{
            Width = 0 # 0 represents 256 in ICO header
            Height = 0
            ColorCount = 0
            Planes = 1
            BitCount = 32
            Bytes = $rawBytes
        }
    } else {
        # DIB format (32bpp BGRA + AND mask)
        $ms = New-Object System.IO.MemoryStream
        $bw = New-Object System.IO.BinaryWriter($ms)

        # BITMAPINFOHEADER (40 bytes)
        $bw.Write([uint32]40)              # biSize
        $bw.Write([int32]$sz)              # biWidth
        $bw.Write([int32]($sz * 2))        # biHeight (XOR height + AND height)
        $bw.Write([uint16]1)               # biPlanes
        $bw.Write([uint16]32)              # biBitCount
        $bw.Write([uint32]0)               # biCompression = BI_RGB
        $bw.Write([uint32]($sz * $sz * 4)) # biSizeImage
        $bw.Write([int32]0)                # biXPelsPerMeter
        $bw.Write([int32]0)                # biYPelsPerMeter
        $bw.Write([uint32]0)               # biClrUsed
        $bw.Write([uint32]0)               # biClrImportant

        # XOR mask (bottom-to-top rows, BGRA)
        for ($y = $sz - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $sz; $x++) {
                $pixel = $bmp.GetPixel($x, $y)
                $bw.Write([byte]$pixel.B)
                $bw.Write([byte]$pixel.G)
                $bw.Write([byte]$pixel.R)
                $bw.Write([byte]$pixel.A)
            }
        }

        # AND mask (1-bit per pixel, padded to 32-bit boundary)
        $andRowBytes = [Math]::Ceiling($sz / 32.0) * 4
        for ($y = $sz - 1; $y -ge 0; $y--) {
            $rowBits = New-Object byte[] $andRowBytes
            for ($x = 0; $x -lt $sz; $x++) {
                $pixel = $bmp.GetPixel($x, $y)
                if ($pixel.A -eq 0) {
                    $byteIdx = [int][Math]::Floor($x / 8.0)
                    $bitIdx = 7 - ($x % 8)
                    $rowBits[$byteIdx] = $rowBits[$byteIdx] -bor (1 -shl $bitIdx)
                }
            }
            $bw.Write($rowBits)
        }

        $bw.Flush()
        $rawBytes = $ms.ToArray()
        $bw.Close()
        $ms.Dispose()

        $imagesData += @{
            Width = [byte]$sz
            Height = [byte]$sz
            ColorCount = 0
            Planes = 1
            BitCount = 32
            Bytes = $rawBytes
        }
    }
    $bmp.Dispose()
}

$src.Dispose()

# Now write complete ICO file
$outStream = New-Object System.IO.FileStream($outputPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$outWriter = New-Object System.IO.BinaryWriter($outStream)

# ICONDIR header (6 bytes)
$outWriter.Write([uint16]0)                  # idReserved
$outWriter.Write([uint16]1)                  # idType (1 = Icon)
$outWriter.Write([uint16]$imagesData.Count)  # idCount

$currentOffset = 6 + ($imagesData.Count * 16)

# ICONDIRENTRY (16 bytes per image)
foreach ($img in $imagesData) {
    $outWriter.Write([byte]$img.Width)
    $outWriter.Write([byte]$img.Height)
    $outWriter.Write([byte]$img.ColorCount)
    $outWriter.Write([byte]0)                # bReserved
    $outWriter.Write([uint16]$img.Planes)
    $outWriter.Write([uint16]$img.BitCount)
    $outWriter.Write([uint32]$img.Bytes.Length)
    $outWriter.Write([uint32]$currentOffset)
    $currentOffset += $img.Bytes.Length
}

# Image Data
foreach ($img in $imagesData) {
    $outWriter.Write($img.Bytes)
}

$outWriter.Flush()
$outWriter.Close()
$outStream.Dispose()

Write-Host ("[ok] Arquivo ICO gerado com sucesso: " + $outputPath + " (" + (Get-Item $outputPath).Length + " bytes)")
