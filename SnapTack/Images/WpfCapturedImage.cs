using System.Windows.Media.Imaging;

namespace SnapTack.Images;

/// <summary>
/// WPF の <see cref="BitmapSource"/> を <see cref="ICapturedImage"/> として Core へ渡すラッパー。
/// </summary>
/// <remarks>
/// Core は画像をピクセル単位では扱わず持ち回すだけなので、包むだけで足りる。
/// 表示・クリップボード・PNG 保存には WPF の型が必要なため、
/// <see cref="CapturedImageExtensions.ToBitmapSource"/> で取り出して使う。
/// </remarks>
internal sealed class WpfCapturedImage : ICapturedImage
{
    /// <summary>包んでいる画像。ウィンドウ間で使い回すため Freeze 済みであること (CLAUDE.md)。</summary>
    public BitmapSource Source { get; }

    public int PixelWidth => Source.PixelWidth;

    public int PixelHeight => Source.PixelHeight;

    public WpfCapturedImage(BitmapSource source)
    {
        Source = source;
    }
}

/// <summary>Core の画像抽象から WPF の画像を取り出すヘルパー。</summary>
internal static class CapturedImageExtensions
{
    /// <summary>
    /// <see cref="ICapturedImage"/> を <see cref="BitmapSource"/> に戻す。
    /// WPF 版で生成される画像は必ず <see cref="WpfCapturedImage"/> なので、
    /// それ以外が来るのはプログラミングエラー (実装の取り違え) として例外にする。
    /// </summary>
    public static BitmapSource ToBitmapSource(this ICapturedImage image) =>
        image is WpfCapturedImage wpf
            ? wpf.Source
            : throw new ArgumentException(
                $"WPF 版では {nameof(WpfCapturedImage)} 以外の画像実装は扱えません。", nameof(image));
}
