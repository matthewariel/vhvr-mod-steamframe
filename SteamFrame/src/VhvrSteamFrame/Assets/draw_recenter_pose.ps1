param([string]$Out)
Add-Type -AssemblyName System.Drawing
$W = 640; $H = 360
$bmp = New-Object System.Drawing.Bitmap $W, $H, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAliasGridFit'
$g.Clear([System.Drawing.Color]::Transparent)

$line   = [System.Drawing.Color]::FromArgb(255, 225, 225, 225)
$dim    = [System.Drawing.Color]::FromArgb(255, 150, 150, 150)
$accent = [System.Drawing.Color]::FromArgb(255, 255, 179, 71)
$hand   = [System.Drawing.Color]::FromArgb(150, 255, 179, 71)
$visor  = [System.Drawing.Color]::FromArgb(255, 70, 70, 78)

$pen      = New-Object System.Drawing.Pen $line, 4
$penThin  = New-Object System.Drawing.Pen $dim, 2
$penAcc   = New-Object System.Drawing.Pen $accent, 4
$penDash  = New-Object System.Drawing.Pen $accent, 3
$penDash.DashStyle = 'Dash'
$brHand   = New-Object System.Drawing.SolidBrush $hand
$brVisor  = New-Object System.Drawing.SolidBrush $visor
$brText   = New-Object System.Drawing.SolidBrush $line
$brDim    = New-Object System.Drawing.SolidBrush $dim
$brAcc    = New-Object System.Drawing.SolidBrush $accent
$font     = New-Object System.Drawing.Font 'Segoe UI', 17, ([System.Drawing.FontStyle]::Bold)
$fontSm   = New-Object System.Drawing.Font 'Segoe UI', 14, ([System.Drawing.FontStyle]::Bold)

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = 2 * $r
  $p.AddArc($x, $y, $d, $d, 180, 90)
  $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
  $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
  $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
  $p.CloseFigure()
  return $p
}

# Open hand seen from the front: palm + four fingers + thumb pointing inward ($dir = +1 thumb to the right, -1 to the left).
function Hand([float]$cx, [float]$cy, [int]$dir) {
  $palm = RoundRect ($cx - 30) ($cy - 10) 60 62 16
  $g.FillPath($brHand, $palm); $g.DrawPath($penAcc, $palm)
  for ($i = 0; $i -lt 4; $i++) {
    $fx = $cx - 27 + $i * 14.5
    $len = @(34, 42, 40, 32)[$i]
    if ($dir -lt 0) { $len = @(32, 40, 42, 34)[$i] }
    $f = RoundRect $fx ($cy - 8 - $len) 12 ($len + 6) 6
    $g.FillPath($brHand, $f); $g.DrawPath($penAcc, $f)
  }
  $tx = if ($dir -gt 0) { $cx + 18 } else { $cx - 40 }
  $t = RoundRect $tx ($cy + 22) 22 13 6
  $g.FillPath($brHand, $t); $g.DrawPath($penAcc, $t)
}

function Caption([string]$s, [float]$cx, [float]$y) {
  $size = $g.MeasureString($s, $fontSm)
  $g.DrawString($s, $fontSm, $brDim, $cx - $size.Width / 2, $y)
}

# ---------- FRONT view ----------
$fx = 165; $fy = 190
$g.DrawEllipse($pen, $fx - 95, $fy - 110, 190, 220)               # head
$visorPath = RoundRect ($fx - 88) ($fy - 42) 176 58 18            # headset
$g.FillPath($brVisor, $visorPath); $g.DrawPath($pen, $visorPath)
Hand ($fx - 55) ($fy - 8) 1                                       # left hand over left eye (thumb inward)
Hand ($fx + 55) ($fy - 8) -1                                      # right hand over right eye
Caption 'FRONT' $fx 8

# ---------- SIDE view ----------
$sx = 455; $sy = 190
$g.DrawEllipse($pen, $sx - 95, $sy - 110, 190, 220)               # head
$hs = RoundRect ($sx + 60) ($sy - 46) 46 52 12                    # headset front
$g.FillPath($brVisor, $hs); $g.DrawPath($pen, $hs)
# hand in profile: palm edge facing the face, fingers up
$hp = RoundRect ($sx + 150) ($sy - 92) 22 112 10
$g.FillPath($brHand, $hp); $g.DrawPath($penAcc, $hp)
$th = RoundRect ($sx + 134) ($sy - 6) 24 13 6
$g.FillPath($brHand, $th); $g.DrawPath($penAcc, $th)
# distance arrow at eye level
$ay = $sy - 20
$g.DrawLine($penDash, $sx + 108, $ay, $sx + 148, $ay)
$g.FillPolygon($brAcc, [System.Drawing.PointF[]]@(
  (New-Object System.Drawing.PointF ($sx + 148), $ay),
  (New-Object System.Drawing.PointF ($sx + 138), ($ay - 6)),
  (New-Object System.Drawing.PointF ($sx + 138), ($ay + 6))))
$g.FillPolygon($brAcc, [System.Drawing.PointF[]]@(
  (New-Object System.Drawing.PointF ($sx + 108), $ay),
  (New-Object System.Drawing.PointF ($sx + 118), ($ay - 6)),
  (New-Object System.Drawing.PointF ($sx + 118), ($ay + 6))))
$lbl = '10 cm'
$sz = $g.MeasureString($lbl, $fontSm)
$g.DrawString($lbl, $fontSm, $brAcc, $sx + 126 - $sz.Width / 2, $ay - 34)
Caption 'SIDE' $sx 8

$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"saved $Out"
