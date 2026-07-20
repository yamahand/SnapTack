using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using SnapTack.Interop;

namespace SnapTack.Capture;

/// <summary>Graphics.CopyFromScreen による <see cref="IScreenCapturer"/> 実装。</summary>
public sealed class GdiScreenCapturer : IScreenCapturer
{
    public BitmapSource CapturePrimaryScreen()
    {
        // Per-Monitor V2 宣言済みプロセスのため Bounds は物理ピクセルで返る
        var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
            ?? throw new InvalidOperationException("プライマリモニタが見つかりません。");

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
