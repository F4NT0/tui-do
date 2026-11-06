# Writes copies of images/*.png (except the logo) with rounded corners to images/rounded, because GitHub READMEs ignore CSS border-radius.
param([int]$Radius = 28)

Add-Type -AssemblyName System.Drawing

$source = Join-Path $PSScriptRoot '..\images'
$target = Join-Path $source 'rounded'
New-Item -ItemType Directory -Force -Path $target | Out-Null

foreach ($file in Get-ChildItem $source -Filter *.png | Where-Object Name -ne 'logo.png') {
    $image = [System.Drawing.Image]::FromFile($file.FullName)
    $bitmap = New-Object System.Drawing.Bitmap $image.Width, $image.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = 'AntiAlias'
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $d = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($image.Width - $d - 1, 0, $d, $d, 270, 90)
    $path.AddArc($image.Width - $d - 1, $image.Height - $d - 1, $d, $d, 0, 90)
    $path.AddArc(0, $image.Height - $d - 1, $d, $d, 90, 90)
    $path.CloseFigure()

    $graphics.SetClip($path)
    $graphics.DrawImage($image, 0, 0, $image.Width, $image.Height)
    $bitmap.Save((Join-Path $target $file.Name), [System.Drawing.Imaging.ImageFormat]::Png)

    $graphics.Dispose(); $bitmap.Dispose(); $image.Dispose(); $path.Dispose()
}
