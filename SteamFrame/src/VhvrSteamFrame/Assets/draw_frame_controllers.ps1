# Draws frame_controllers.png for the Button guide page (page II of the Steam Frame tab): both Steam Frame controllers
# seen from above (every face button, and the grip button on the handle's neck), a small cropped side view under each
# one that shows its bumper and trigger, and a leader line from every button out to the edge of the picture, where the
# page puts that button's box.
#
# The controller drawings are the reference drawing frame_controllers_reference.svg (next to this script): its shapes
# are copied here as they are, in its own units, and only placed and scaled. Everything else is in the settings tab's
# units (x right, y up, the same numbers FrameTabView uses). The picture covers x = -250..250, y = 165..-240; the boxes
# sit just outside it (x = ±250..±440). Box i of a column is centred at y = 147 - 36.5 * i, in the order of
# FrameButtons.Left / FrameButtons.Right (src/Policies/SteamFrame/FrameButtons.cs). If you change that order or the box
# spacing, change $Anchors and $BoxY here and run this again.
#
# Usage:  powershell -File draw_frame_controllers.ps1 -Out frame_controllers.png [-Preview page_preview.png]
#   -Preview also draws the boxes and the tab's edges, to check the whole page layout without the game.
param([string]$Out, [string]$Preview = '')
Add-Type -AssemblyName System.Drawing

$S = 2.0                         # texture pixels per tab unit (sharp in the headset)
$Left = -250; $Top = 165; $Wt = 500; $Ht = 405
function PX([double]$x) { [float](($x - $Left) * $S) }
function PY([double]$y) { [float](($Top - $y) * $S) }
function PT([double]$x, [double]$y) { New-Object System.Drawing.PointF (PX $x), (PY $y) }

# Box centres (tab y), top to bottom. Same order as FrameButtons.cs.
$BoxY = @(0..9 | ForEach-Object { 147 - 36.5 * $_ })
$BoxEdge = 250

# Where the drawings go. X, Y: where the drawing's (0, 0) lands, in tab units. K: tab units per reference unit.
# M = -1 mirrors it. The side views are mirrored so that each one's bumper and trigger point out towards its boxes.
$Faces = @{
  Left  = @{ X = -132; Y = 76; K = 0.78; M = 1 }
  Right = @{ X = 132;  Y = 76; K = 0.78; M = 1 }
}
$Sides = @{
  Left  = @{ X = -165; Y = -239; K = 0.44; M = -1 }
  Right = @{ X = 165;  Y = -239; K = 0.44; M = 1 }
}
$Crop = @(-130, -190, 260, 190)   # the part of the side view that is shown (reference units)

# The reference drawing's colours (shell, seams, buttons), and the page's own for the leader lines and their dots.
function Color([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
function RefPen([string]$hex, [double]$w) {
  $p = New-Object System.Drawing.Pen (Color $hex), ([float]$w); $p.LineJoin = 'Round'; $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p
}
$shell = @((New-Object System.Drawing.SolidBrush (Color '#262b30')), (RefPen '#8fa3b5' 2.4))
$seam  = @($null, (RefPen '#6c8092' 1.6))
$outer = @((New-Object System.Drawing.SolidBrush (Color '#1d2125')), (RefPen '#8fa3b5' 2))
$inner = @((New-Object System.Drawing.SolidBrush (Color '#262b30')), (RefPen '#8fa3b5' 1.6))
$ledBr = New-Object System.Drawing.SolidBrush (Color '#8fa3b5')

$penLead = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(215, 226, 231, 238)), ([float](0.9 * $S))
$penLead.LineJoin = 'Round'; $penLead.StartCap = 'Round'; $penLead.EndCap = 'Round'
$brDot   = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 179, 71))
$brLine  = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 222, 226, 232))

function NewCanvas([int]$w, [int]$h) {
  $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
  $g.Clear([System.Drawing.Color]::Transparent)
  return @($bmp, $g)
}

# ---- Drawing in reference units (the current transform maps them onto the picture) ----

# An SVG path: absolute M, L, H, V, C and Z, which is all the reference drawing uses.
function SvgPath([string]$d) {
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $t = @([regex]::Matches($d, '[MLHVCZ]|-?(?:\d+\.?\d*|\.\d+)') | ForEach-Object { $_.Value })
  $i = 0; $cmd = ''; $x = 0.0; $y = 0.0
  while ($i -lt $t.Count) {
    if ($t[$i] -cmatch '^[MLHVCZ]$') {
      $cmd = $t[$i]; $i++
      if ($cmd -ceq 'Z') { $path.CloseFigure() }
      continue
    }
    $n = @($t[$i..([Math]::Min($i + 5, $t.Count - 1))] | ForEach-Object { if ($_ -cmatch '^[MLHVCZ]$') { [float]::NaN } else { [float]$_ } })
    if ($cmd -ceq 'M') { $x = $n[0]; $y = $n[1]; $path.StartFigure(); $i += 2; $cmd = 'L' }
    elseif ($cmd -ceq 'L') { $path.AddLine([float]$x, [float]$y, $n[0], $n[1]); $x = $n[0]; $y = $n[1]; $i += 2 }
    elseif ($cmd -ceq 'H') { $path.AddLine([float]$x, [float]$y, $n[0], [float]$y); $x = $n[0]; $i += 1 }
    elseif ($cmd -ceq 'V') { $path.AddLine([float]$x, [float]$y, [float]$x, $n[0]); $y = $n[0]; $i += 1 }
    elseif ($cmd -ceq 'C') { $path.AddBezier([float]$x, [float]$y, $n[0], $n[1], $n[2], $n[3], $n[4], $n[5]); $x = $n[4]; $y = $n[5]; $i += 6 }
    else { throw "SvgPath: unexpected '$($t[$i])' in $d" }
  }
  return $path
}

function DrawPath($g, $path, $style) {
  if ($style[0]) { $g.FillPath($style[0], $path) }
  if ($style[1]) { $g.DrawPath($style[1], $path) }
}
function DrawShape($g, [string]$d, $style) { DrawPath $g (SvgPath $d) $style }
function DrawCircle($g, [double]$cx, [double]$cy, [double]$r, $style) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddEllipse([float]($cx - $r), [float]($cy - $r), [float](2 * $r), [float](2 * $r))
  DrawPath $g $p $style
}
function DrawRoundRect($g, [double]$x, [double]$y, [double]$w, [double]$h, [double]$r, $style) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = [float](2 * $r)
  $p.AddArc([float]$x, [float]$y, $d, $d, 180, 90)
  $p.AddArc([float]($x + $w - 2 * $r), [float]$y, $d, $d, 270, 90)
  $p.AddArc([float]($x + $w - 2 * $r), [float]($y + $h - 2 * $r), $d, $d, 0, 90)
  $p.AddArc([float]$x, [float]($y + $h - 2 * $r), $d, $d, 90, 90)
  $p.CloseFigure()
  DrawPath $g $p $style
}
function DrawSeam($g, [double]$x1, [double]$y1, [double]$x2, [double]$y2) {
  $g.DrawLine($seam[1], [float]$x1, [float]$y1, [float]$x2, [float]$y2)
}
# A button letter, centred on (cx, cy): 13 px semibold, as in the reference.
function DrawLetter($g, [string]$txt, [double]$cx, [double]$cy) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddString($txt, (New-Object System.Drawing.FontFamily 'Segoe UI Semibold'), 0, [float]13, (New-Object System.Drawing.PointF 0, 0), [System.Drawing.StringFormat]::GenericTypographic)
  $b = $p.GetBounds()
  $mx = New-Object System.Drawing.Drawing2D.Matrix
  $mx.Translate([float]($cx - $b.X - $b.Width / 2), [float]($cy - $b.Y - $b.Height / 2))
  $p.Transform($mx)
  $g.FillPath($ledBr, $p)
}

# Moves the drawing's (0, 0) to where $v says, at its scale and mirroring.
function Place($g, $v) {
  $g.ResetTransform()
  $g.TranslateTransform((PX $v.X), (PY $v.Y))
  $g.ScaleTransform([float]($S * $v.K * $v.M), [float]($S * $v.K))
}

# ---- The reference drawing's parts ----

function ThumbstickTop($g, [double]$x, [double]$y) {
  DrawCircle $g $x $y 31 $outer
  DrawCircle $g $x $y 24.5 $inner
  DrawCircle $g $x $y 20 $outer
}

# Handle, its seam, the lanyard loop and the dual-stage grip button on the inner neck (left controller's orientation).
function TopHandleAssembly($g) {
  DrawRoundRect $g -32.5 295 7 27 3.5 $outer
  DrawShape $g 'M 10 100 L 26 105 C 28 118, 30 132, 30 142 C 30 149, 25 152, 18 151 L 3 148 Z' $outer
  DrawShape $g 'M -98 48 C -82 88, -72 145, -66 225 C -63 275, -48 302, -29 302 C -10 302, 3 275, 4 235 C 5 185, 0 135, 14 95 Z' $shell
  DrawSeam $g -29 106 -29 302
}

function LeftControllerTop($g) {
  TopHandleAssembly $g
  DrawCircle $g 0 0 110 $shell
  DrawCircle $g -7 -68 2.2 @($ledBr, $null)
  $st = $g.Save()
  $g.TranslateTransform(-10, -50); $g.RotateTransform(12); $g.TranslateTransform(10, 50)
  DrawRoundRect $g -22 -55 24 10 5 $outer                     # View
  $g.Restore($st)
  $st = $g.Save()
  $g.TranslateTransform(-50, -11); $g.RotateTransform(12)     # D-pad
  DrawShape $g 'M -11.5 -30 C -11.5 -33, -9.5 -35, -6.5 -35 H 6.5 C 9.5 -35, 11.5 -33, 11.5 -30 V -11.5 H 30 C 33 -11.5, 35 -9.5, 35 -6.5 V 6.5 C 35 9.5, 33 11.5, 30 11.5 H 11.5 V 30 C 11.5 33, 9.5 35, 6.5 35 H -6.5 C -9.5 35, -11.5 33, -11.5 30 V 11.5 H -30 C -33 11.5, -35 9.5, -35 6.5 V -6.5 C -35 -9.5, -33 -11.5, -30 -11.5 H -11.5 Z' $outer
  $g.Restore($st)
  ThumbstickTop $g 33 15
  DrawCircle $g -26 48 13 $outer                              # Steam button
}

function RightControllerTop($g) {
  $st = $g.Save(); $g.ScaleTransform(-1, 1); TopHandleAssembly $g; $g.Restore($st)
  DrawCircle $g 0 0 110 $shell
  DrawCircle $g 7 -68 2.2 @($ledBr, $null)
  $st = $g.Save()
  $g.TranslateTransform(10, -50); $g.RotateTransform(-12); $g.TranslateTransform(-10, 50)
  DrawRoundRect $g -2 -55 24 10 5 $outer                      # Menu
  $g.Restore($st)
  ThumbstickTop $g -33 15
  foreach ($b in @(@('Y', 47.8, -35.2), @('B', 77.2, -16.2), @('X', 28.8, -5.8), @('A', 58.2, 13.2))) {
    DrawCircle $g $b[1] $b[2] 13.5 $outer
    DrawLetter $g $b[0] $b[1] $b[2]
  }
  DrawCircle $g 26 48 13 $outer                               # Steam button
}

# The cropped side profile: the ring's edge with the stick on top, the bumper (upper) and trigger (lower) under its
# front, and the top of the handle. Front to the right, as the reference draws the left controller.
function SideProfile($g) {
  $g.SetClip((New-Object System.Drawing.RectangleF $Crop[0], $Crop[1], $Crop[2], $Crop[3]))
  $st = $g.Save()
  $g.TranslateTransform(-38, -110); $g.RotateTransform(-25)  # knurled thumbstick, seen from the side
  DrawShape $g 'M -11 8 L -11 -4 L 11 -4 L 11 8 Z' $outer
  DrawRoundRect $g -24 -14 48 10 4.5 $inner
  foreach ($x in @(-18, -13, -8, -3, 2, 7, 12, 17)) { DrawSeam $g $x -13 $x -5 }
  $g.Restore($st)
  DrawShape $g 'M 70 -122 C 77 -112, 79 -90, 84 -73 C 85 -69, 81 -67, 74 -68 L 53 -74 Z' $outer      # bumper
  DrawShape $g 'M 67 -115 C 72 -102, 74 -86, 77 -71' $seam
  DrawShape $g 'M 54 -72 L 73 -66 L 71 -61 L 52 -66 Z' $inner                                          # hinge
  DrawShape $g 'M 51 -64 L 76 -60 C 79 -59, 79 -54, 77 -47 C 74 -37, 76 -26, 86 -15 C 89 -11, 86 -7, 81 -8 C 70 -11, 61 -22, 46 -27 Z' $outer   # trigger
  DrawShape $g 'M 62 -60 C 62 -44, 65 -28, 79 -11' $seam
  DrawShape $g 'M -94 -64 C -101 -68, -99 -77, -88 -83 C -32 -112, 34 -142, 88 -164 C 95 -167, 100 -161, 97 -154 C 88 -134, 71 -115, 60 -92 C 49 -70, 46 -46, 48 -21 C 48 -15, 43 -13, 38 -17 C 25 -27, 8 -21, 1 -2 C -8 24, -14 82, -16 142 L -84 138 C -87 82, -79 20, -73 -32 C -71 -46, -77 -54, -85 -57 C -89 -59, -91 -62, -94 -64 Z' $shell
  DrawShape $g 'M -96 -71 C -35 -101, 34 -132, 97 -157' $seam     # rim seam
  DrawShape $g 'M -90 -61 C -78 -56, -68 -51, -66 -38' $seam      # rear neck
  DrawShape $g 'M -42 -66 C -41 15, -45 105, -44 191' $seam       # handle seam
  DrawShape $g 'M -42 -8 L -23 -13 C -18 -14, -15 -11, -15 -6 L -16 20 L -43 32 Z' $seam   # grip
  $g.Restore($st)
  $g.ResetClip()
}

# ---- Leader lines ----

# Where each leader line ends, in box order (top to bottom; must match FrameButtons.cs): the button's name, which
# drawing the point is on ('face' = that side's controller from above, 'side' = its side view), and the point in that
# drawing's reference units. Any further pairs are bend points (same units, in order from the box), to pass between
# other buttons. A line runs from its box out to x = ±222, through the bend points, to the button.
$Anchors = @{
  Right = @(
    @('Menu', 'face', 19, -53), @('Y', 'face', 58, -42), @('B', 'face', 90, -14),
    @('X', 'face', 41, -11, 87, 5.4), @('A', 'face', 62, 26),
    @('Stick click', 'face', -33, 15, 5, 62), @('Stick', 'face', -33, 46, -15, 95), @('Grip', 'face', -24, 128),
    @('Bumper', 'side', 81, -92), @('Trigger', 'side', 76, -33))
  Left = @(
    @('View', 'face', -20, -53), @('D-pad up', 'face', -45, -40), @('D-pad left', 'face', -82, -17),
    @('D-pad down', 'face', -57, 21), @('D-pad right', 'face', -20, -3, -36, 30),
    @('Stick click', 'face', 33, 15, -5, 62), @('Stick', 'face', 33, 46, 15, 95), @('Grip', 'face', 24, 128),
    @('Bumper', 'side', 81, -92), @('Trigger', 'side', 76, -33))
}

function ToTab([string]$side, [string]$view, [double]$x, [double]$y) {
  $v = if ($view -eq 'face') { $Faces[$side] } else { $Sides[$side] }
  @(($v.X + $v.K * $v.M * $x), ($v.Y - $v.K * $y))
}

function Leaders($g) {
  $g.ResetTransform()
  foreach ($side in @('Left', 'Right')) {
    $sign = if ($side -eq 'Left') { -1 } else { 1 }
    $list = $Anchors[$side]
    for ($i = 0; $i -lt $list.Count; $i++) {
      $a = $list[$i]
      $pts = New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
      $pts.Add((PT ($sign * $BoxEdge) $BoxY[$i])); $pts.Add((PT ($sign * 222) $BoxY[$i]))
      for ($j = 4; $j + 1 -lt $a.Count; $j += 2) { $q = ToTab $side $a[1] $a[$j] $a[$j + 1]; $pts.Add((PT $q[0] $q[1])) }
      $end = ToTab $side $a[1] $a[2] $a[3]
      $pts.Add((PT $end[0] $end[1]))
      $g.DrawLines($penLead, $pts.ToArray())
      $r = 2.6 * $S
      $g.FillEllipse($brDot, [float]((PX $end[0]) - $r), [float]((PY $end[1]) - $r), [float](2 * $r), [float](2 * $r))
    }
  }
}

$c = NewCanvas ([int]($Wt * $S)) ([int]($Ht * $S)); $bmp = $c[0]; $g = $c[1]
Place $g $Sides.Left; SideProfile $g
Place $g $Sides.Right; SideProfile $g
Place $g $Faces.Left; LeftControllerTop $g
Place $g $Faces.Right; RightControllerTop $g
Leaders $g
$g.Dispose()
if ($Out) { $bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png); "saved $Out" }

if ($Preview) {
  # The whole page as the tab lays it out: panel edges, boxes, reset button, chord notes.
  $PS = 1.5; $pw = 960; $ph = 560   # tab x -480..480, y 280..-280
  $pv = New-Object System.Drawing.Bitmap ([int]($pw*$PS)), ([int]($ph*$PS))
  $pg = [System.Drawing.Graphics]::FromImage($pv); $pg.SmoothingMode = 'AntiAlias'; $pg.TextRenderingHint = 'AntiAliasGridFit'
  $pg.Clear([System.Drawing.Color]::FromArgb(255, 30, 32, 36))
  function QX([double]$x) { [float](($x + 480) * $PS) }
  function QY([double]$y) { [float]((280 - $y) * $PS) }
  $edge = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 200, 60, 60)), 1
  $pg.DrawRectangle($edge, (QX -450), (QY 252), [float](900*$PS), [float]((252+252)*$PS))
  $safe = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 90, 160, 90)), 1
  $safe.DashStyle = 'Dash'
  $pg.DrawRectangle($safe, (QX -440), (QY 165), [float](880*$PS), [float](405*$PS))
  $pg.DrawImage($bmp, (QX $Left), (QY $Top), [float]($Wt*$PS), [float]($Ht*$PS))
  $boxBr = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 70, 62, 50))
  $fixBr = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 40, 44, 50))
  # Text at the page's sizes (TMP sizes are tab units per em): names 12, actions and notes 13, Reset 14.
  function PreviewFont([double]$size) { New-Object System.Drawing.Font 'Arial', ([float]($size*$PS)), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel) }
  $f12 = PreviewFont 12; $f13 = PreviewFont 13; $f14 = PreviewFont 14
  $fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'
  $dim = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 170, 170, 170))
  $sample = @{
    Left = @('Wrist map', 'Scroll up / zoom in', 'Quick actions', 'Scroll down / zoom out', 'Inventory', 'Sprint', 'Move', 'Grab', 'Laser right-click', 'Use / attack + Laser click')
    Right = @('Menu', 'Sprint (hold)', 'Quick switch', 'Dodge', 'Jump', 'Middle click (ping) + Crouch', 'Turn', 'Grab', 'Laser right-click', 'Use / attack + Laser click')
  }
  foreach ($side in @('Left', 'Right')) {
    $x0 = if ($side -eq 'Left') { -440 } else { 250 }
    for ($i = 0; $i -lt 10; $i++) {
      $y = $BoxY[$i]
      $fixed = $Anchors[$side][$i][0] -match '^(Trigger|Grip|Stick)$'
      $pg.FillRectangle($(if ($fixed) { $fixBr } else { $boxBr }), (QX $x0), (QY ($y + 16.5)), [float](190*$PS), [float](33*$PS))
      $name = $Anchors[$side][$i][0]; if ($fixed) { $name += ' (fixed)' }
      $pg.DrawString($name, $f12, $dim, (New-Object System.Drawing.RectangleF (QX $x0), (QY ($y + 16)), ([float](190*$PS)), ([float](16*$PS))), $fmt)
      $pg.DrawString($sample[$side][$i], $f13, $brLine, (New-Object System.Drawing.RectangleF (QX $x0), (QY ($y + 1)), ([float](190*$PS)), ([float](16*$PS))), $fmt)
    }
  }
  $pg.FillRectangle($boxBr, (QX -440), (QY -207), [float](190*$PS), [float](30*$PS))
  $pg.DrawString('Reset to default', $f14, $brLine, (New-Object System.Drawing.RectangleF (QX -440), (QY -213), ([float](190*$PS)), ([float](20*$PS))), $fmt)
  $pg.DrawString('Click a box to change that button.', $f12, $dim, (New-Object System.Drawing.RectangleF (QX 250), (QY -210), ([float](190*$PS)), ([float](36*$PS))), $fmt)
  # The two-button combinations, where FrameGuideLayout puts them (NotesWidth wide from NotesTopY down).
  $notesW = 210; $notesTop = -45
  $notes = "Grip + Trigger: Add map pin / Quick-move item`nGrip + other Trigger: Split stack`nGrip + A: Remove build piece`nGrip + Stick tilt (right): Flip piece / Rotate piece"
  $pg.DrawString($notes, $f13, $dim, (New-Object System.Drawing.RectangleF (QX (-$notesW / 2)), (QY $notesTop), ([float]($notesW*$PS)), ([float](($notesTop + 240)*$PS))), $fmt)
  $pg.Dispose()
  $pv.Save($Preview, [System.Drawing.Imaging.ImageFormat]::Png); "saved $Preview"
}
$bmp.Dispose()
