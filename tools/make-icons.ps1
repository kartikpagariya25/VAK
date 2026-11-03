# Generates the VAK .ico files from the same geometry as Assets/Brand/vak-mark.xaml.
param([string]$OutDir = (Join-Path $PSScriptRoot '..\src\VAK.App\Assets\Brand'))
Add-Type -AssemblyName System.Drawing

function New-MarkPng([int]$s, [string]$variant) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $k = $s / 100.0
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 30 * $k
    $path.AddArc(0, 0, 2*$r, 2*$r, 180, 90); $path.AddArc($s-2*$r, 0, 2*$r, 2*$r, 270, 90)
    $path.AddArc($s-2*$r, $s-2*$r, 2*$r, 2*$r, 0, 90); $path.AddArc(0, $s-2*$r, 2*$r, 2*$r, 90, 90); $path.CloseFigure()
    switch ($variant) {
        'color' { $bg = [System.Drawing.Color]::FromArgb(47,124,246); $notch = [System.Drawing.Color]::White; $eye = [System.Drawing.Color]::FromArgb(11,11,12) }
        'tray-dark' { $bg = [System.Drawing.Color]::White; $notch = [System.Drawing.Color]::FromArgb(11,11,12); $eye = [System.Drawing.Color]::White }
        'tray-light' { $bg = [System.Drawing.Color]::FromArgb(11,11,12); $notch = [System.Drawing.Color]::White; $eye = [System.Drawing.Color]::FromArgb(11,11,12) }
    }
    $g.SetClip($path)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $bg), 0, 0, $s, $s)
    $np = New-Object System.Drawing.Drawing2D.GraphicsPath
    $np.AddLine(22*$k, -2*$k, 78*$k, -2*$k)
    $np.AddLine(78*$k, -2*$k, 78*$k, 34*$k)
    $np.AddArc(38*$k, 14*$k, 40*$k, 40*$k, 0, 90)
    $np.AddLine(58*$k, 54*$k, 42*$k, 54*$k)
    $np.AddArc(22*$k, 14*$k, 40*$k, 40*$k, 90, 90)
    $np.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $notch), $np)
    $eb = New-Object System.Drawing.SolidBrush $eye
    $g.FillEllipse($eb, (33*$k), (22*$k), (12*$k), (12*$k)); $g.FillEllipse($eb, (55*$k), (22*$k), (12*$k), (12*$k))
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream; $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    return ,$ms.ToArray()
}

function Write-Ico([string]$file, [string]$variant, [int[]]$sizes) {
    $pngs = $sizes | ForEach-Object { ,(New-MarkPng $_ $variant) }
    $fs = [System.IO.File]::Create($file); $w = New-Object System.IO.BinaryWriter $fs
    $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]; $d = if ($s -ge 256) { 0 } else { $s }
        $w.Write([byte]$d); $w.Write([byte]$d); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
        $offset += $pngs[$i].Length
    }
    foreach ($p in $pngs) { $w.Write($p) }
    $w.Close(); $fs.Close()
}

Write-Ico (Join-Path $OutDir 'vak.ico') 'color' @(16,32,48,256)
Write-Ico (Join-Path $OutDir 'tray-dark.ico') 'tray-dark' @(16,24,32)
Write-Ico (Join-Path $OutDir 'tray-light.ico') 'tray-light' @(16,24,32)
Write-Host "icons written to $OutDir"
