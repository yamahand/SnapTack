using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using SnapTack.Interop;

namespace SnapTack.Capture;

/// <summary>Graphics.CopyFromScreen による <see cref="IScreenCapturer"/> 実装。</summary>
public sealed class GdiScreenCapturer : IScreenCapturer
{
    public IReadOnlyList<MonitorInfo> EnumerateMonitors()
    {
        // Per-Monitor V2 宣言済みプロセスのため Bounds は物理ピクセルで返る
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length == 0)
        {
            throw new InvalidOperationException("モニタが見つかりません。");
        }
        return screens
            .Select(s => new MonitorInfo(
                s.DeviceName,
                new Int32Rect(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height),
                s.Primary))
            .ToList();
    }

    public BitmapSource CaptureMonitor(MonitorInfo monitor)
    {
        var bounds = monitor.PhysicalBounds;

        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            // 物理ピクセルどうしのコピー
            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bitmap.Size);
        }

        IntPtr hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            // オーバーレイ・付箋間で共有するため Freeze する
            source.Freeze();
            return source;
        }
        finally
        {
            Gdi32.DeleteObject(hBitmap);
        }
    }
}
