using System.Windows;
using SnapTack.Capture;
using Xunit;

namespace SnapTack.Tests;

/// <summary>DIP ↔ 物理px 変換とクランプ (SPEC-v1.x 2.4) の検証。</summary>
public class RectMathTests
{
    // DPI 100%: 1920x1080 DIP のウィンドウに 1920x1080 の画像
    private const double Width100 = 1920, Height100 = 1080;
    private const int Pixel100W = 1920, Pixel100H = 1080;

    // DPI 150%: 1280x720 DIP のウィンドウに 1920x1080 の画像
    private const double Width150 = 1280, Height150 = 720;

    [Fact]
    public void DPI100では座標がそのまま変換される()
    {
        var result = RectMath.ToPhysicalRect(
            new Rect(100, 50, 300, 200), Width100, Height100, Pixel100W, Pixel100H);

        Assert.Equal(new Int32Rect(100, 50, 300, 200), result);
    }

    [Fact]
    public void DPI150では1_5倍に変換される()
    {
        // 1920/1280 = 1.5 倍
        var result = RectMath.ToPhysicalRect(
            new Rect(100, 50, 300, 200), Width150, Height150, Pixel100W, Pixel100H);

        // x=150, y=75, right=(400*1.5)=600, bottom=(250*1.5)=375
        Assert.Equal(new Int32Rect(150, 75, 450, 300), result);
    }

    [Fact]
    public void 端数座標は四捨五入される()
    {
        // x=10.4*1.5=15.6→16, right=(10.4+5.2)*1.5=23.4→23, width=23-16=7
        var result = RectMath.ToPhysicalRect(
            new Rect(10.4, 10.4, 5.2, 5.2), Width150, Height150, Pixel100W, Pixel100H);

        Assert.Equal(16, result.X);
        Assert.Equal(16, result.Y);
        Assert.Equal(7, result.Width);
        Assert.Equal(7, result.Height);
    }

    [Fact]
    public void 幅と高さは負にならない()
    {
        // 空の矩形でも Math.Max(0, ...) により負にならない
        var result = RectMath.ToPhysicalRect(
            new Rect(100, 100, 0, 0), Width100, Height100, Pixel100W, Pixel100H);

        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
    }

    [Fact]
    public void クランプは範囲内の矩形を変更しない()
    {
        var rect = new Int32Rect(100, 100, 200, 200);

        var result = RectMath.ClampToScreenshot(rect, Pixel100W, Pixel100H);

        Assert.Equal(rect, result);
    }

    [Fact]
    public void 右下へはみ出した矩形は画像内に収まる()
    {
        var result = RectMath.ClampToScreenshot(
            new Int32Rect(1800, 1000, 500, 500), Pixel100W, Pixel100H);

        Assert.Equal(new Int32Rect(1800, 1000, 120, 80), result);
    }

    [Fact]
    public void 左上へはみ出した矩形は原点側にクランプされる()
    {
        // x,y は 0 へクランプされ、right/bottom は元のまま (-50+200=150)
        var result = RectMath.ClampToScreenshot(
            new Int32Rect(-50, -50, 200, 200), Pixel100W, Pixel100H);

        Assert.Equal(new Int32Rect(0, 0, 150, 150), result);
    }

    [Fact]
    public void 完全に範囲外の矩形は幅ゼロになる()
    {
        var result = RectMath.ClampToScreenshot(
            new Int32Rect(5000, 5000, 100, 100), Pixel100W, Pixel100H);

        // x,y は画像サイズへクランプされ、幅・高さは 0 になる (負にはならない)
        Assert.Equal(Pixel100W, result.X);
        Assert.Equal(Pixel100H, result.Y);
        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
    }

    [Fact]
    public void クランプ後の幅と高さは負にならない()
    {
        var result = RectMath.ClampToScreenshot(
            new Int32Rect(-500, -500, 100, 100), Pixel100W, Pixel100H);

        Assert.True(result.Width >= 0);
        Assert.True(result.Height >= 0);
    }

    [Fact]
    public void 変換とクランプを通しても画像範囲内に収まる()
    {
        // ウィンドウ全面をドラッグした場合、丸め誤差が出ても画像内に収まること
        var physical = RectMath.ToPhysicalRect(
            new Rect(0, 0, Width150, Height150), Width150, Height150, Pixel100W, Pixel100H);
        var clamped = RectMath.ClampToScreenshot(physical, Pixel100W, Pixel100H);

        Assert.True(clamped.X + clamped.Width <= Pixel100W);
        Assert.True(clamped.Y + clamped.Height <= Pixel100H);
    }
}
