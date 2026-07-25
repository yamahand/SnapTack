namespace SnapTack.Images;

/// <summary>
/// キャプチャ画像の抽象。実体は UI 層のビットマップ (WPF なら <c>BitmapSource</c>) を包んだもの。
/// </summary>
/// <remarks>
/// Core はピクセルに触れず、画像を「持ち回して保存する」だけなので寸法しか公開しない。
/// 表示・クリップボード・PNG 保存はいずれも UI 層の仕事で、そこで実装型へ戻して使う。
/// </remarks>
public interface ICapturedImage
{
    /// <summary>幅 (物理px)。</summary>
    int PixelWidth { get; }

    /// <summary>高さ (物理px)。</summary>
    int PixelHeight { get; }
}
