# Offline texture preparation only; no runtime dependency. Preserve source originals.
param([Parameter(Mandatory=$true)][string]$Source,
      [Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$sourcePath=(Resolve-Path -LiteralPath $Source).Path
$destinationPath=[IO.Path]::GetFullPath($Destination)
if($sourcePath -eq $destinationPath){throw 'Source and destination must differ.'}
$inputImage=[Drawing.Bitmap]::new($sourcePath)
$outputImage=[Drawing.Bitmap]::new(1024,1024,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics=[Drawing.Graphics]::FromImage($outputImage)
try {
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $scale=[Math]::Min(1024.0/$inputImage.Width,1024.0/$inputImage.Height)
    $width=[int][Math]::Round($inputImage.Width*$scale)
    $height=[int][Math]::Round($inputImage.Height*$scale)
    $x=[int][Math]::Floor((1024-$width)/2)
    $y=1024-$height
    $graphics.DrawImage($inputImage,[Drawing.Rectangle]::new($x,$y,$width,$height),0,0,$inputImage.Width,$inputImage.Height,[Drawing.GraphicsUnit]::Pixel)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destinationPath))|Out-Null
    $outputImage.Save($destinationPath,[Drawing.Imaging.ImageFormat]::Png)
} finally {$graphics.Dispose();$outputImage.Dispose();$inputImage.Dispose()}
