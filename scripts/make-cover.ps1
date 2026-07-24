<#
.SYNOPSIS
  itch.io 用のカバー画像 (630x500) を生成する。

.DESCRIPTION
  アプリアイコン (黄色い付箋 + 赤い画鋲) のモチーフを踏襲し、
  ダーク背景に重なった付箋とロゴタイポを描画する。

  ImageMagick 等の外部ツールには依存せず、WPF (PresentationCore /
  PresentationFramework) の DrawingVisual で描いて PNG に焼く。
  CLAUDE.md の「外部 NuGet は原則追加しない」方針に合わせている。

.EXAMPLE
  pwsh scripts/make-cover.ps1
  pwsh scripts/make-cover.ps1 -OutputPath docs/images/cover.png
#>
[CmdletBinding()]
param(
    [string]$OutputPath = "docs/images/cover.png",
    [int]$Width = 630,
    [int]$Height = 500
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

# ---- パレット (アイコンの黄色 / 赤ピンに合わせる) ----
$bgTop     = '#FF1B1D23'  # 背景グラデーション上
$bgBottom  = '#FF0E0F13'  # 背景グラデーション下
$noteFill  = '#FFFFD44F'  # 付箋の本体 (アイコンと同じ黄色)
$noteEdge  = '#FFE0A81E'  # 付箋の枠線
$noteFold  = '#FFE8BE3A'  # 右下の折り返し
$lineColor = '#FFE0B831'  # 付箋の中のテキスト行
$pinOuter  = '#FFD93B36'  # 画鋲の外側
$pinEdge   = '#FFA82420'  # 画鋲の縁
$pinShine  = '#FFF9D9D7'  # 画鋲のハイライト
$titleFg   = '#FFF5F6F8'
$tagFg     = '#FF9BA3B4'
$accentFg  = '#FFFFD44F'

function ToBrush([string]$argb) {
    $c = [System.Windows.Media.ColorConverter]::ConvertFromString($argb)
    $b = New-Object System.Windows.Media.SolidColorBrush($c)
    $b.Freeze()
    return $b
}

function ToPen([string]$argb, [double]$thickness) {
    $p = New-Object System.Windows.Media.Pen((ToBrush $argb), $thickness)
    $p.Freeze()
    return $p
}

# 角丸の付箋を 1 枚描く。回転と影付き。
function Draw-Note {
    param(
        [System.Windows.Media.DrawingContext]$Dc,
        [double]$X, [double]$Y,
        [double]$W, [double]$H,
        [double]$Angle,
        [double]$Opacity = 1.0,
        [int]$Lines = 3,
        [bool]$WithPin = $false
    )

    $Dc.PushOpacity($Opacity)

    # 中心を軸に回転させる。
    # New-Object の括弧内で算術式を書くと引数が配列扱いになるため、中心座標は先に確定させる
    $cx = [double]($X + $W / 2)
    $cy = [double]($Y + $H / 2)
    $rotate = New-Object System.Windows.Media.RotateTransform -ArgumentList $Angle, $cx, $cy
    $Dc.PushTransform($rotate)

    $rect = New-Object System.Windows.Rect($X, $Y, $W, $H)

    # 影 (少しずらした暗い矩形でフェイク。BlurEffect は DrawingVisual に乗せにくいため)
    $shadow = New-Object System.Windows.Rect(($X + 6), ($Y + 8), $W, $H)
    $Dc.DrawRoundedRectangle((ToBrush '#55000000'), $null, $shadow, 10, 10)

    # 本体
    $Dc.DrawRoundedRectangle((ToBrush $noteFill), (ToPen $noteEdge 2.0), $rect, 10, 10)

    # 中のテキスト行
    $lineH = 7.0
    $marginX = $W * 0.14
    $startY = $Y + $H * 0.38
    $gap = $H * 0.16
    for ($i = 0; $i -lt $Lines; $i++) {
        # 最後の行だけ短くして文章らしく見せる
        $lw = if ($i -eq $Lines - 1) { ($W - $marginX * 2) * 0.6 } else { $W - $marginX * 2 }
        $lr = New-Object System.Windows.Rect(($X + $marginX), ($startY + $gap * $i), $lw, $lineH)
        $Dc.DrawRoundedRectangle((ToBrush $lineColor), $null, $lr, 3, 3)
    }

    # 右下の折り返し (アイコンと同じ意匠)
    $foldSize = [Math]::Min($W, $H) * 0.28
    $fig = New-Object System.Windows.Media.PathFigure
    $fig.StartPoint = New-Object System.Windows.Point(($X + $W), ($Y + $H - $foldSize))
    $seg1 = New-Object System.Windows.Media.LineSegment((New-Object System.Windows.Point(($X + $W - $foldSize), ($Y + $H))), $false)
    $seg2 = New-Object System.Windows.Media.LineSegment((New-Object System.Windows.Point(($X + $W), ($Y + $H))), $false)
    $fig.Segments.Add($seg1) | Out-Null
    $fig.Segments.Add($seg2) | Out-Null
    $fig.IsClosed = $true
    $geo = New-Object System.Windows.Media.PathGeometry
    $geo.Figures.Add($fig) | Out-Null
    $Dc.DrawGeometry((ToBrush $noteFold), $null, $geo)

    # 画鋲 (主役の付箋のみ)
    if ($WithPin) {
        # 上辺ちょうどに置くと「刺さっている」感が出ないため、少し内側に下げる
        $pinR = [Math]::Min($W, $H) * 0.115
        $pinC = New-Object System.Windows.Point(($X + $W / 2), ($Y + $pinR * 0.9))
        $Dc.DrawEllipse((ToBrush $pinOuter), (ToPen $pinEdge 2.5), $pinC, $pinR, $pinR)
        $shineC = New-Object System.Windows.Point(($pinC.X - $pinR * 0.3), ($pinC.Y - $pinR * 0.3))
        $Dc.DrawEllipse((ToBrush $pinShine), $null, $shineC, ($pinR * 0.3), ($pinR * 0.3))
    }

    $Dc.Pop()  # transform
    $Dc.Pop()  # opacity
}

function New-Text {
    param([string]$Text, [double]$Size, [string]$Color, [string]$Family = 'Segoe UI', [string]$Weight = 'Bold')

    $tf = New-Object System.Windows.Media.Typeface(
        (New-Object System.Windows.Media.FontFamily($Family)),
        [System.Windows.FontStyles]::Normal,
        ([System.Windows.FontWeights]::$Weight),
        [System.Windows.FontStretches]::Normal)

    $ft = New-Object System.Windows.Media.FormattedText(
        $Text,
        [System.Globalization.CultureInfo]::GetCultureInfo('en-US'),
        [System.Windows.FlowDirection]::LeftToRight,
        $tf, $Size, (ToBrush $Color),
        1.0)
    return $ft
}

# ---- 描画 ----
$visual = New-Object System.Windows.Media.DrawingVisual
$dc = $visual.RenderOpen()

# 背景 (縦グラデーション)
$grad = New-Object System.Windows.Media.LinearGradientBrush
$grad.StartPoint = New-Object System.Windows.Point(0, 0)
$grad.EndPoint = New-Object System.Windows.Point(0, 1)
$grad.GradientStops.Add((New-Object System.Windows.Media.GradientStop(([System.Windows.Media.ColorConverter]::ConvertFromString($bgTop)), 0.0))) | Out-Null
$grad.GradientStops.Add((New-Object System.Windows.Media.GradientStop(([System.Windows.Media.ColorConverter]::ConvertFromString($bgBottom)), 1.0))) | Out-Null
$grad.Freeze()
$dc.DrawRectangle($grad, $null, (New-Object System.Windows.Rect(0, 0, $Width, $Height)))

# 背景に薄い水平ライン (キャプチャ対象の「画面」を示唆)
for ($y = 60; $y -lt $Height; $y += 42) {
    $lr = New-Object System.Windows.Rect(0, $y, $Width, 1)
    $dc.DrawRectangle((ToBrush '#10FFFFFF'), $null, $lr)
}

# 付箋を 3 枚、奥から手前へ重ねる
Draw-Note -Dc $dc -X 336 -Y 258 -W 150 -H 130 -Angle 9  -Opacity 0.5  -Lines 2
Draw-Note -Dc $dc -X 442 -Y 122 -W 158 -H 138 -Angle -8 -Opacity 0.72 -Lines 3
Draw-Note -Dc $dc -X 348 -Y 168 -W 196 -H 176 -Angle 3  -Opacity 1.0  -Lines 3 -WithPin $true

# ロゴタイポ
$title = New-Text -Text 'SnapTack' -Size 62 -Color $titleFg
$dc.DrawText($title, (New-Object System.Windows.Point(48, 150)))

# アクセントの下線
$dc.DrawRectangle((ToBrush $accentFg), $null, (New-Object System.Windows.Rect(52, 228, 96, 5)))

# タグライン (2 行)
$tag1 = New-Text -Text 'Pin screen captures to' -Size 21 -Color $tagFg -Weight 'Regular'
$dc.DrawText($tag1, (New-Object System.Windows.Point(50, 256)))
$tag2 = New-Text -Text 'your desktop as sticky notes' -Size 21 -Color $tagFg -Weight 'Regular'
$dc.DrawText($tag2, (New-Object System.Windows.Point(50, 284)))

# フッター: 対応 OS と無料表記
$foot = New-Text -Text 'Windows 10 / 11   ·   Free & open source' -Size 15 -Color '#FF6E7687' -Weight 'Regular'
$dc.DrawText($foot, (New-Object System.Windows.Point(50, 400)))

$dc.Close()

# ---- PNG に焼く ----
$rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($Width, $Height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
$rtb.Render($visual)

$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb)) | Out-Null

$full = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath }
        else { Join-Path (Split-Path -Parent $PSScriptRoot) $OutputPath }
$dir = Split-Path -Parent $full
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$fs = [System.IO.File]::Create($full)
try { $encoder.Save($fs) } finally { $fs.Close() }

Write-Host "カバー画像を生成しました: $full ($Width x $Height)"
