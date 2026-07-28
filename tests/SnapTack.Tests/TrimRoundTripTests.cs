using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapTack.Models;
using Xunit;

namespace SnapTack.Tests;

/// <summary>
/// トリムの逆変換 (表示座標 → 元画像座標) を実ピクセルで検証する。
/// 座標計算だけでは符号や軸の取り違えに気付けないため、
/// 「見た目で選んだ範囲の画素」と「トリム結果の画素」が一致することを確かめる。
/// </summary>
public sealed class TrimRoundTripTests
{
    // 各画素を一意に識別できるよう、位置から色を作る 8x6 の画像
    private const int W = 8;
    private const int H = 6;

    private static BitmapSource NewDistinctImage()
    {
        int stride = W * 3;
        var pixels = new byte[stride * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int i = y * stride + x * 3;
                pixels[i] = (byte)(x * 10 + 1);      // B
                pixels[i + 1] = (byte)(y * 10 + 1);  // G
                pixels[i + 2] = 200;                 // R
            }
        }
        var bmp = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgr24, null, pixels, stride);
        bmp.Freeze();
        return bmp;
    }

    private static byte[] GetPixels(BitmapSource src)
    {
        int stride = src.PixelWidth * 3;
        var buf = new byte[stride * src.PixelHeight];
        var converted = new FormatConvertedBitmap(src, PixelFormats.Bgr24, null, 0);
        converted.CopyPixels(buf, stride, 0);
        return buf;
    }

    /// <summary>
    /// 「表示状態の画像を displayRect で単純に切り出したもの」と
    /// 「WithTrimFromDisplay を通して元画像から切り出したもの」が一致するか。
    /// </summary>
    private static void AssertTrimMatchesVisualCrop(ScrapEdit baseEdit, Int32Rect displayRect)
    {
        var source = NewDistinctImage();

        // 期待値: 現在の見た目を作り、そこから素直に切り出す
        var displayed = baseEdit.Apply(source);
        var expected = new CroppedBitmap(displayed, displayRect);

        // 実測: 逆変換を通してトリムを取り込み、改めて適用する
        var trimmed = baseEdit.WithTrimFromDisplay(displayRect, new Size(W, H));
        var actual = trimmed.Apply(source);

        Assert.Equal(expected.PixelWidth, actual.PixelWidth);
        Assert.Equal(expected.PixelHeight, actual.PixelHeight);
        Assert.Equal(GetPixels(expected), GetPixels(actual));
    }

    [Fact]
    public void 編集なし() =>
        AssertTrimMatchesVisualCrop(ScrapEdit.Default, new Int32Rect(2, 1, 4, 3));

    [Fact]
    public void 回転90度() =>
        AssertTrimMatchesVisualCrop(ScrapEdit.Default.RotateBy(90), new Int32Rect(1, 2, 3, 4));

    [Fact]
    public void 回転180度() =>
        AssertTrimMatchesVisualCrop(ScrapEdit.Default.RotateBy(180), new Int32Rect(2, 1, 4, 3));

    [Fact]
    public void 回転270度() =>
        AssertTrimMatchesVisualCrop(ScrapEdit.Default.RotateBy(270), new Int32Rect(1, 2, 3, 4));

    [Fact]
    public void 水平反転() =>
        AssertTrimMatchesVisualCrop(ScrapEdit.Default.ToggleFlipHorizontal(), new Int32Rect(2, 1, 4, 3));

    [Fact]
    public void 垂直反転() =>
        AssertTrimMatchesVisualCrop(ScrapEdit.Default.ToggleFlipVertical(), new Int32Rect(2, 1, 4, 3));

    [Fact]
    public void 水平垂直反転() =>
        AssertTrimMatchesVisualCrop(
            ScrapEdit.Default.ToggleFlipHorizontal().ToggleFlipVertical(), new Int32Rect(2, 1, 4, 3));

    [Fact]
    public void 回転90度と水平反転() =>
        AssertTrimMatchesVisualCrop(
            ScrapEdit.Default.RotateBy(90).ToggleFlipHorizontal(), new Int32Rect(1, 2, 3, 4));

    [Fact]
    public void 回転270度と垂直反転() =>
        AssertTrimMatchesVisualCrop(
            ScrapEdit.Default.RotateBy(270).ToggleFlipVertical(), new Int32Rect(1, 2, 3, 4));

    [Fact]
    public void トリム済みからの重ねがけ()
    {
        var first = ScrapEdit.Default.WithTrimFromDisplay(new Int32Rect(1, 1, 6, 4), new Size(W, H));
        AssertTrimMatchesVisualCrop(first, new Int32Rect(1, 1, 3, 2));
    }

    [Fact]
    public void トリム済みで回転してからの重ねがけ()
    {
        var first = ScrapEdit.Default
            .WithTrimFromDisplay(new Int32Rect(1, 1, 6, 4), new Size(W, H))
            .RotateBy(90);
        AssertTrimMatchesVisualCrop(first, new Int32Rect(1, 1, 2, 3));
    }
}
