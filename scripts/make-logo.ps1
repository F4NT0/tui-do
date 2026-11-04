Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$lines = Get-Content -Encoding UTF8 (Join-Path $root "src\TuiDo\Cli\Logo.cs") |
    Where-Object { $_ -match '^\s+"[^"]+",\s*$' } |
    ForEach-Object { ($_ -replace '^\s+"', '') -replace '",\s*$', '' }

$font = New-Object System.Drawing.Font("Consolas", 40, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$fmt = [System.Drawing.StringFormat]::GenericTypographic
$fmt.FormatFlags = $fmt.FormatFlags -bor [System.Drawing.StringFormatFlags]::MeasureTrailingSpaces
$g0 = [System.Drawing.Graphics]::FromImage((New-Object System.Drawing.Bitmap 1, 1))
$w = [int]($g0.MeasureString($lines[0], $font, 5000, $fmt).Width) + 80
$lh = [int]($font.Height * 0.98)
$bmp = New-Object System.Drawing.Bitmap $w, ($lh * $lines.Count + 80)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0, 175, 255))
for ($i = 0; $i -lt $lines.Count; $i++) { $g.DrawString($lines[$i], $font, $brush, 40, 40 + $i * $lh, $fmt) }
New-Item -ItemType Directory -Force (Join-Path $root "images") | Out-Null
$bmp.Save((Join-Path $root "images\logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
