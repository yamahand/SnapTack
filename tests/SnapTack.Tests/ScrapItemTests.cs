using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapTack.Models;
using Xunit;

namespace SnapTack.Tests;

/// <summary>スクラップ 1 件のモデル (SPEC-v1.5 3.2) の検証。</summary>
public class ScrapItemTests
{
    // 1x1 の最小画像。ScrapItem はメタデータ保持のみでピクセルには触れないため内容は問わない
    private static BitmapSource MakeImage() =>
        BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr24, null, new byte[] { 0, 0, 0 }, 3);

    [Fact]
    public void 引数2つのコンストラクタはGUIDを採番する()
    {
        var rect = new Int32Rect(10, 20, 30, 40);

        var a = new ScrapItem(MakeImage(), rect);
        var b = new ScrapItem(MakeImage(), rect);

        Assert.NotEqual(Guid.Empty, a.Id);
        Assert.NotEqual(a.Id, b.Id); // 生成のたびに一意
    }

    [Fact]
    public void 引数2つのコンストラクタは画像と矩形を保持する()
    {
        var image = MakeImage();
        var rect = new Int32Rect(10, 20, 30, 40);

        var item = new ScrapItem(image, rect);

        Assert.Same(image, item.Image);
        Assert.Equal(rect, item.PhysicalRect);
    }

    [Fact]
    public void 復元用コンストラクタは渡した値をそのまま保持する()
    {
        // M16 の復元で Id / CapturedAt を指定して再構築するための経路。画像は遅延ローダーで後付け
        var id = Guid.NewGuid();
        var rect = new Int32Rect(1, 2, 3, 4);
        var capturedAt = new DateTimeOffset(2026, 7, 23, 10, 0, 0, TimeSpan.FromHours(9));

        var item = new ScrapItem(id, rect, capturedAt);

        Assert.Equal(id, item.Id);
        Assert.Equal(rect, item.PhysicalRect);
        Assert.Equal(capturedAt, item.CapturedAt);
        Assert.False(item.IsImageLoaded);
    }

    [Fact]
    public void 遅延ローダーはImage初回参照でだけ実行される()
    {
        var image = MakeImage();
        int calls = 0;
        var item = new ScrapItem(Guid.NewGuid(), new Int32Rect(0, 0, 1, 1), DateTimeOffset.Now);
        item.SetImageLoader(() => { calls++; return image; });

        Assert.False(item.IsImageLoaded);
        var first = item.Image;
        var second = item.Image;

        Assert.Same(image, first);
        Assert.Same(image, second);
        Assert.Equal(1, calls); // 初回のみロード
        Assert.True(item.IsImageLoaded);
    }
}
