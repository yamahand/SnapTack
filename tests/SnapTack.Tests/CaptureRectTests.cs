using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapTack.Capture;
using Xunit;

namespace SnapTack.Tests;

/// <summary>
/// <c>/R:X,Y,W,H</c> による範囲指定キャプチャ (SPEC-v1.8 2.5) の検証。
/// </summary>
/// <remarks>
/// <see cref="CaptureController.CaptureRect"/> はオーバーレイを作らないため、
/// 偽の <see cref="IScreenCapturer"/> を挿せばウィンドウ無しで検証できる。
/// </remarks>
public class CaptureRectTests
{
    /// <summary>指定したモニタ構成を返す偽キャプチャ。切り出し位置の検証用に座標を埋め込む。</summary>
    private sealed class FakeCapturer(params MonitorInfo[] monitors) : IScreenCapturer
    {
        /// <summary>CaptureMonitor が呼ばれた回数。実画面を 2 度読まないことの確認用。</summary>
        public int CaptureCount { get; private set; }

        public IReadOnlyList<MonitorInfo> EnumerateMonitors() => monitors;

        public BitmapSource CaptureMonitor(MonitorInfo monitor)
        {
            CaptureCount++;
            var b = monitor.PhysicalBounds;
            int stride = b.Width * 3;
            var pixels = new byte[stride * b.Height];
            // 画素にモニタ内座標を埋めておくと、どこが切り出されたか判定できる
            for (int y = 0; y < b.Height; y++)
            {
                for (int x = 0; x < b.Width; x++)
                {
                    int i = y * stride + x * 3;
                    pixels[i] = (byte)(x % 256);
                    pixels[i + 1] = (byte)(y % 256);
                    pixels[i + 2] = 128;
                }
            }
            return BitmapSource.Create(b.Width, b.Height, 96, 96,
                PixelFormats.Bgr24, null, pixels, stride);
        }
    }

    private static MonitorInfo Primary(int w = 1920, int h = 1080) =>
        new(@"\\.\DISPLAY1", new Int32Rect(0, 0, w, h), IsPrimary: true);

    /// <summary>キャプチャを実行し、通知された (画像, 矩形) を返す。通知が無ければ null。</summary>
    private static (BitmapSource Image, Int32Rect Rect)? Run(
        FakeCapturer capturer, Int32Rect request)
    {
        var controller = new CaptureController(capturer);
        (BitmapSource, Int32Rect)? result = null;
        controller.SelectionCompleted += (image, rect) => result = (image, rect);
        controller.CaptureRect(request);
        return result;
    }

    [Fact]
    public void 指定矩形がそのまま切り出される()
    {
        var result = Run(new FakeCapturer(Primary()), new Int32Rect(100, 200, 640, 480));

        Assert.NotNull(result);
        Assert.Equal(new Int32Rect(100, 200, 640, 480), result.Value.Rect);
        Assert.Equal(640, result.Value.Image.PixelWidth);
        Assert.Equal(480, result.Value.Image.PixelHeight);
    }

    [Fact]
    public void 切り出した画像は共有できるよう凍結されている()
    {
        // ウィンドウ間で使い回すため (CLAUDE.md のコーディング規約)
        var result = Run(new FakeCapturer(Primary()), new Int32Rect(0, 0, 100, 100));

        Assert.NotNull(result);
        Assert.True(result.Value.Image.IsFrozen);
    }

    [Fact]
    public void 実画面は1度しか読まない()
    {
        // フリーズ方式と同じく再キャプチャしない (CLAUDE.md)
        var capturer = new FakeCapturer(Primary());
        Run(capturer, new Int32Rect(0, 0, 100, 100));

        Assert.Equal(1, capturer.CaptureCount);
    }

    [Fact]
    public void はみ出す矩形はモニタ内へクランプされる()
    {
        // 1920x1080 の右下を超える要求
        var result = Run(new FakeCapturer(Primary()), new Int32Rect(1800, 1000, 400, 400));

        Assert.NotNull(result);
        Assert.Equal(new Int32Rect(1800, 1000, 120, 80), result.Value.Rect);
    }

    [Fact]
    public void モニタ全体を覆う要求は画面サイズに収まる()
    {
        var result = Run(new FakeCapturer(Primary()), new Int32Rect(0, 0, 99999, 99999));

        Assert.NotNull(result);
        Assert.Equal(new Int32Rect(0, 0, 1920, 1080), result.Value.Rect);
    }

    [Fact]
    public void 左上を含むモニタが選ばれる()
    {
        // プライマリの右にセカンダリ (1920,0)-(3840,1080)
        var secondary = new MonitorInfo(@"\\.\DISPLAY2", new Int32Rect(1920, 0, 1920, 1080), false);
        var result = Run(new FakeCapturer(Primary(), secondary), new Int32Rect(2000, 100, 300, 200));

        Assert.NotNull(result);
        Assert.Equal(new Int32Rect(2000, 100, 300, 200), result.Value.Rect);
    }

    [Fact]
    public void 負座標のモニタでも切り出せる()
    {
        // プライマリの左にセカンダリ。仮想スクリーン座標は負になる
        var left = new MonitorInfo(@"\\.\DISPLAY2", new Int32Rect(-1920, 0, 1920, 1080), false);
        var result = Run(new FakeCapturer(Primary(), left), new Int32Rect(-1900, 50, 200, 100));

        Assert.NotNull(result);
        Assert.Equal(new Int32Rect(-1900, 50, 200, 100), result.Value.Rect);
        Assert.Equal(200, result.Value.Image.PixelWidth);
    }

    [Fact]
    public void モニタをまたぐ矩形は左上のモニタ内で切れる()
    {
        // モニタまたぎキャプチャはスコープ外 (SPEC-v1.x 2.4 の判断を維持)
        var secondary = new MonitorInfo(@"\\.\DISPLAY2", new Int32Rect(1920, 0, 1920, 1080), false);
        var result = Run(new FakeCapturer(Primary(), secondary), new Int32Rect(1820, 0, 200, 100));

        Assert.NotNull(result);
        // プライマリの右端 (1920) で打ち切られる
        Assert.Equal(new Int32Rect(1820, 0, 100, 100), result.Value.Rect);
    }

    [Fact]
    public void どのモニタにも重ならない矩形は無視される()
    {
        var result = Run(new FakeCapturer(Primary()), new Int32Rect(5000, 5000, 100, 100));

        Assert.Null(result);
    }

    [Fact]
    public void 画面外から始まる矩形でも交差部分を切り出す()
    {
        // 左上はモニタ外だが右下が画面にかかっている
        var result = Run(new FakeCapturer(Primary()), new Int32Rect(-50, -50, 200, 200));

        Assert.NotNull(result);
        Assert.Equal(new Int32Rect(0, 0, 150, 150), result.Value.Rect);
    }
}
