using System.IO;
using System.Windows.Media.Imaging;

namespace SnapTack.Images;

/// <summary>
/// WPF の PNG エンコーダ・デコーダによる <see cref="IImageCodec"/> 実装。
/// スクラップ画像 (<c>scraps/&lt;id&gt;.png</c>) の読み書きに使う。
/// </summary>
internal sealed class WpfImageCodec : IImageCodec
{
    public void EncodePng(ICapturedImage image, Stream destination)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image.ToBitmapSource()));
        encoder.Save(destination);
    }

    public ICapturedImage DecodePng(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad; // ファイルを掴みっぱなしにしない
        image.UriSource = new Uri(path);
        image.EndInit();
        // ウィンドウ間で使い回すため Freeze する (CLAUDE.md)
        image.Freeze();
        return new WpfCapturedImage(image);
    }
}
