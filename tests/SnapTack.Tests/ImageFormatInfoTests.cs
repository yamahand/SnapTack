using System.Globalization;
using System.Windows.Media.Imaging;
using SnapTack.Models;
using Xunit;

namespace SnapTack.Tests;

/// <summary>保存形式ごとの拡張子・エンコーダ・ダイアログフィルタ (SPEC-v1.6 2.1) の検証。</summary>
public class ImageFormatInfoTests
{
    [Theory]
    [InlineData(SaveImageFormat.Png, ".png")]
    [InlineData(SaveImageFormat.Jpeg, ".jpg")]
    [InlineData(SaveImageFormat.Bmp, ".bmp")]
    public void 形式ごとの既定拡張子を返す(SaveImageFormat format, string expected)
    {
        Assert.Equal(expected, ImageFormatInfo.GetExtension(format));
    }

    [Theory]
    [InlineData(".png", SaveImageFormat.Png)]
    [InlineData(".jpg", SaveImageFormat.Jpeg)]
    [InlineData(".jpeg", SaveImageFormat.Jpeg)] // JPEG は 2 つの拡張子を受け付ける
    [InlineData(".bmp", SaveImageFormat.Bmp)]
    [InlineData(".PNG", SaveImageFormat.Png)]   // 大文字で手入力されても判定できること
    public void 拡張子から形式を判定する(string extension, SaveImageFormat expected)
    {
        Assert.Equal(expected, ImageFormatInfo.FromExtension(extension));
    }

    [Theory]
    [InlineData(".gif")]
    [InlineData(".txt")]
    [InlineData("")]
    [InlineData(null)]
    public void 対応外の拡張子はnullを返す(string? extension)
    {
        // null を返すことで、呼び出し側がフィルタの選択へフォールバックできる (SPEC-v1.6 2.1)
        Assert.Null(ImageFormatInfo.FromExtension(extension));
    }

    [Fact]
    public void JPEGエンコーダに品質が反映される()
    {
        var encoder = Assert.IsType<JpegBitmapEncoder>(
            ImageFormatInfo.CreateEncoder(SaveImageFormat.Jpeg, 55));

        Assert.Equal(55, encoder.QualityLevel);
    }

    [Theory]
    [InlineData(0, 1)]     // 設定ファイルを手で書き換えられた場合の下限
    [InlineData(150, 100)] // 同上限
    public void JPEG品質は範囲外でもクランプされる(int input, int expected)
    {
        var encoder = Assert.IsType<JpegBitmapEncoder>(
            ImageFormatInfo.CreateEncoder(SaveImageFormat.Jpeg, input));

        Assert.Equal(expected, encoder.QualityLevel);
    }

    [Fact]
    public void 形式ごとに対応するエンコーダを作る()
    {
        Assert.IsType<PngBitmapEncoder>(ImageFormatInfo.CreateEncoder(SaveImageFormat.Png, 90));
        Assert.IsType<BmpBitmapEncoder>(ImageFormatInfo.CreateEncoder(SaveImageFormat.Bmp, 90));
    }

    [Fact]
    public void ダイアログフィルタが表示名とパターンの対で並ぶ()
    {
        // SaveFileDialog.Filter は "表示名|パターン" の繰り返し。要素数が奇数だと例外になる
        var parts = ImageFormatInfo.BuildDialogFilter().Split('|');

        Assert.Equal(ImageFormatInfo.All.Length * 2, parts.Length);
        Assert.All(parts, p => Assert.NotEmpty(p));
    }

    [Fact]
    public void フィルタのパターンが形式の並び順と一致する()
    {
        // FilterIndex から形式へ戻す変換 (FromFilterIndex) が成立する前提を固定する
        var parts = ImageFormatInfo.BuildDialogFilter().Split('|');

        Assert.Equal("*.png", parts[1]);
        Assert.Equal("*.jpg;*.jpeg", parts[3]);
        Assert.Equal("*.bmp", parts[5]);
    }

    [Fact]
    public void フィルタ番号と形式が相互に変換できる()
    {
        // ダイアログの FilterIndex は 1 始まり
        foreach (var format in ImageFormatInfo.All)
        {
            int index = ImageFormatInfo.GetFilterIndex(format);
            Assert.InRange(index, 1, ImageFormatInfo.All.Length);
            Assert.Equal(format, ImageFormatInfo.FromFilterIndex(index));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void 範囲外のフィルタ番号はPNGへフォールバックする(int index)
    {
        Assert.Equal(SaveImageFormat.Png, ImageFormatInfo.FromFilterIndex(index));
    }

    [Fact]
    public void 拡張子が形式と食い違えば差し替える()
    {
        Assert.Equal(@"C:\a\b.jpg", ImageFormatInfo.EnsureExtension(@"C:\a\b.png", SaveImageFormat.Jpeg));
    }

    [Fact]
    public void 拡張子が形式と一致していればそのまま返す()
    {
        // .jpeg も JPEG なので .jpg へ書き換えない (ユーザーの指定を尊重する)
        Assert.Equal(@"C:\a\b.jpeg", ImageFormatInfo.EnsureExtension(@"C:\a\b.jpeg", SaveImageFormat.Jpeg));
    }

    [Fact]
    public void 拡張子が無ければ形式の拡張子を付ける()
    {
        // ダイアログでフィルタだけ切り替えて拡張子を省いた場合の補正 (中身と名前の食い違い防止)
        Assert.Equal(@"C:\a\b.jpg", ImageFormatInfo.EnsureExtension(@"C:\a\b", SaveImageFormat.Jpeg));
    }

    [Fact]
    public void ファイル名にドットを含んでも最後の拡張子だけ扱う()
    {
        // "2026.07.27_shot" のような名前でも、名前本体を壊さないこと
        Assert.Equal(
            @"C:\a\2026.07.27_shot.png",
            ImageFormatInfo.EnsureExtension(@"C:\a\2026.07.27_shot.jpg", SaveImageFormat.Png));
    }

    [Fact]
    public void フィルタは現在のUI言語で解決される()
    {
        // 翻訳済みフィルタを使うため、言語切替が効くことを確認する
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ja");
            string ja = ImageFormatInfo.BuildDialogFilter();
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            string en = ImageFormatInfo.BuildDialogFilter();

            Assert.NotEqual(en, ja);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
