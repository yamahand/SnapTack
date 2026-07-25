using System.IO;
using SnapTack.Images;

namespace SnapTack.Tests;

/// <summary>
/// テスト用の画像。Core は画像のピクセルに触れないため、寸法だけ持つ空の実装で足りる。
/// </summary>
internal sealed class FakeImage : ICapturedImage
{
    public int PixelWidth { get; }
    public int PixelHeight { get; }

    public FakeImage(int pixelWidth = 1, int pixelHeight = 1)
    {
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
    }
}

/// <summary>
/// テスト用のコーデック。PNG として妥当なバイト列は作らず、1 バイト書くだけ。
/// </summary>
/// <remarks>
/// ScrapStore の関心事は「画像ファイルが存在するか」「index と対応が取れているか」なので、
/// 実際の PNG エンコードは要らない。逆に実 PNG を要求すると Core のテストが
/// 画像コーデック (= プラットフォーム依存) を必要としてしまう。
/// WPF の PngBitmapEncoder / BitmapImage による実装の検証は WPF 側の責務。
/// </remarks>
internal sealed class FakeImageCodec : IImageCodec
{
    /// <summary>デコード回数。遅延読み込みが 1 回だけ走ることの確認に使える。</summary>
    public int DecodeCount { get; private set; }

    public void EncodePng(ICapturedImage image, Stream destination)
    {
        destination.WriteByte(1);
    }

    public ICapturedImage DecodePng(string path)
    {
        DecodeCount++;
        return new FakeImage();
    }
}
