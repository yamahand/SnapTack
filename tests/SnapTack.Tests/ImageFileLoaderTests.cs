using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapTack.Models;
using Xunit;

namespace SnapTack.Tests;

/// <summary>外部画像 (ファイル / クリップボード) の読み込み (SPEC-v1.6 3.2/3.3) の検証。</summary>
public class ImageFileLoaderTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "SnapTackLoaderTests", Guid.NewGuid().ToString("N"));

    public ImageFileLoaderTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch (IOException) { }
    }

    /// <summary>テスト用に実際の PNG ファイルを書き出す。</summary>
    private string WritePng(string name, int width = 4, int height = 3)
    {
        int stride = width * 3;
        var pixels = new byte[stride * height];
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null, pixels, stride);

        string path = Path.Combine(_tempDir, name);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    [Theory]
    [InlineData("a.png")]
    [InlineData("a.jpg")]
    [InlineData("a.jpeg")]
    [InlineData("a.bmp")]
    [InlineData("a.gif")]
    [InlineData("a.tif")]
    [InlineData("a.tiff")]
    [InlineData("a.PNG")] // 拡張子の大小は問わない
    [InlineData("a.webp")] // Store の拡張機能パッケージが提供するデコーダで読む
    [InlineData("a.avif")] // 同上 (HEIF デコーダ)
    public void 対応拡張子を受け付ける(string name)
    {
        Assert.True(ImageFileLoader.IsSupportedExtension(name));
    }

    [Theory]
    [InlineData("a.txt")]
    [InlineData("a.exe")]
    [InlineData("a.heic")] // 将来 mac 対応する場合に検討する (現状は対象外)
    [InlineData("a")]
    public void 対応外の拡張子は弾く(string name)
    {
        Assert.False(ImageFileLoader.IsSupportedExtension(name));
    }

    [Fact]
    public void 画像ファイルを読み込める()
    {
        string path = WritePng("ok.png", width: 4, height: 3);

        var image = ImageFileLoader.TryLoadFile(path);

        Assert.NotNull(image);
        Assert.Equal(4, image!.PixelWidth);
        Assert.Equal(3, image.PixelHeight);
    }

    [Fact]
    public void 読み込んだ画像はFreezeされている()
    {
        // ウィンドウ間で使い回すため凍結が必須 (CLAUDE.md のコーディング規約)
        string path = WritePng("frozen.png");

        var image = ImageFileLoader.TryLoadFile(path);

        Assert.NotNull(image);
        Assert.True(image!.IsFrozen);
    }

    [Fact]
    public void 壊れたファイルはnullを返す()
    {
        // 拡張子は対応でも中身が画像でない場合。落とさず読み飛ばせること (SPEC-v1.6 3.2)
        string path = Path.Combine(_tempDir, "broken.png");
        File.WriteAllText(path, "this is not a png");

        Assert.Null(ImageFileLoader.TryLoadFile(path));
    }

    [Fact]
    public void 存在しないファイルはnullを返す()
    {
        Assert.Null(ImageFileLoader.TryLoadFile(Path.Combine(_tempDir, "missing.png")));
    }

    [Theory]
    [InlineData("broken.webp")]
    [InlineData("broken.avif")]
    public void デコードできないwebpやavifでも落ちない(string name)
    {
        // webp/avif のデコーダは Store の拡張機能パッケージ由来で、未導入の環境では読めない。
        // その際 WIC は COMException を投げるため、握って null を返せること (退行防止)。
        // 中身が不正なファイルも同じ経路を通る
        string path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, "RIFF\0\0\0\0WEBPnot a real image"u8.ToArray());

        Assert.Null(ImageFileLoader.TryLoadFile(path));
    }

    [Fact]
    public void 複数ファイルのうち読めたものだけ返す()
    {
        string good1 = WritePng("g1.png");
        string good2 = WritePng("g2.png");
        string broken = Path.Combine(_tempDir, "b.png");
        File.WriteAllText(broken, "not an image");
        string unsupported = Path.Combine(_tempDir, "u.txt");
        File.WriteAllText(unsupported, "text");
        string missing = Path.Combine(_tempDir, "missing.png");

        var images = ImageFileLoader.LoadFiles([good1, broken, unsupported, missing, good2]);

        Assert.Equal(2, images.Count);
    }

    [Fact]
    public void 読める画像が無ければ空を返す()
    {
        // 呼び出し側はこれを見て「1 件も作れなかった」通知を出す (SPEC-v1.6 3.2)
        Assert.Empty(ImageFileLoader.LoadFiles([Path.Combine(_tempDir, "none.txt")]));
    }

    [Fact]
    public void 対応ファイルの有無を判定する()
    {
        // D&D のカーソル表示に使う。実ファイルの存在は見ず拡張子だけで判定する
        Assert.True(ImageFileLoader.ContainsSupportedFile([@"C:\x.txt", @"C:\y.png"]));
        Assert.False(ImageFileLoader.ContainsSupportedFile([@"C:\x.txt", @"C:\y.doc"]));
        Assert.False(ImageFileLoader.ContainsSupportedFile([]));
    }

    // ===== クリップボードのテキストをパスとして解釈する (SPEC-v1.6 3.2) =====

    [Fact]
    public void 引用符付きのパスから引用符を除く()
    {
        // エクスプローラーの「パスのコピー」は引用符付きで入る
        var paths = ImageFileLoader.ParsePathsFromText("\"C:\\pics\\a.png\"").ToList();

        Assert.Equal([@"C:\pics\a.png"], paths);
    }

    [Fact]
    public void 引用符無しのパスもそのまま扱える()
    {
        var paths = ImageFileLoader.ParsePathsFromText(@"C:\pics\a.png").ToList();

        Assert.Equal([@"C:\pics\a.png"], paths);
    }

    [Fact]
    public void 複数行のテキストは各行をパスとして扱う()
    {
        // 複数選択して「パスのコピー」すると改行区切りになる
        string text = "\"C:\\a.png\"\r\n\"C:\\b.jpg\"";

        var paths = ImageFileLoader.ParsePathsFromText(text).ToList();

        Assert.Equal([@"C:\a.png", @"C:\b.jpg"], paths);
    }

    [Fact]
    public void 空行と前後の空白は無視する()
    {
        string text = "  \"C:\\a.png\"  \n\n\t\n C:\\b.jpg \n";

        var paths = ImageFileLoader.ParsePathsFromText(text).ToList();

        Assert.Equal([@"C:\a.png", @"C:\b.jpg"], paths);
    }

    [Fact]
    public void 空文字からはパスを取り出さない()
    {
        Assert.Empty(ImageFileLoader.ParsePathsFromText(""));
        Assert.Empty(ImageFileLoader.ParsePathsFromText("   \r\n  "));
    }
}
