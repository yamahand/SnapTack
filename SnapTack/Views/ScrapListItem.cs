using System.Windows.Media.Imaging;
using SnapTack.Models;
using SnapTack.Resources;

namespace SnapTack.Views;

/// <summary>
/// スクラップ 1 件を一覧に表示するための表示用ラッパー (SPEC-v1.5 2.2)。
/// サムネイルは <see cref="BitmapImage.DecodePixelWidth"/> でデコード時に縮小し、
/// 元画像をそのまま多数並べてメモリを圧迫しないようにする (SPEC-v1.5 3.3)。
/// </summary>
public sealed class ScrapListItem
{
    // サムネイルのデコード幅 (物理px)。96 DIP 表示に対し高 DPI でも足りるよう少し大きめ
    private const int ThumbnailDecodeWidth = 192;

    // 日付書式・サイズ書式は言語非依存なので const (CLAUDE.md)
    private const string SizeFormat = "{0} × {1}";

    public ScrapItem Item { get; }

    /// <summary>縮小デコード済みのサムネイル (Freeze 済み)。</summary>
    public BitmapSource Thumbnail { get; }

    /// <summary>キャプチャ日時 (現在のカルチャの短い日時書式)。</summary>
    public string CapturedAtText => Item.CapturedAt.LocalDateTime.ToString("g");

    /// <summary>サイズ (物理px)。</summary>
    public string SizeText => string.Format(SizeFormat, Item.PhysicalRect.Width, Item.PhysicalRect.Height);

    /// <summary>状態の短い表記 (Pinned / Stashed の区別)。Trashed では空。</summary>
    public string StateText => Item.State switch
    {
        ScrapState.Pinned => Strings.StatePinnedText,
        ScrapState.Stashed => Strings.StateStashedText,
        _ => string.Empty,
    };

    public ScrapListItem(ScrapItem item)
    {
        Item = item;
        Thumbnail = CreateThumbnail(item.Image);
    }

    /// <summary>元画像を縮小デコードしてサムネイルを作る。中央クロップは表示側の UniformToFill で行う。</summary>
    private static BitmapSource CreateThumbnail(BitmapSource source)
    {
        // 既に十分小さければそのまま使う
        if (source.PixelWidth <= ThumbnailDecodeWidth)
        {
            return source;
        }
        // DecodePixelWidth はストリームからのデコード時のみ効くため、一度 PNG にエンコードして
        // 縮小デコードし直す。元画像を保持したままメモリを食わないようにする (SPEC-v1.5 3.3)
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new System.IO.MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;

        var thumb = new BitmapImage();
        thumb.BeginInit();
        thumb.CacheOption = BitmapCacheOption.OnLoad;
        thumb.DecodePixelWidth = ThumbnailDecodeWidth;
        thumb.StreamSource = stream;
        thumb.EndInit();
        thumb.Freeze();
        return thumb;
    }
}
