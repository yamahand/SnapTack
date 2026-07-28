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

    // ===== 編集 (SPEC-v1.7 2.1) =====

    // 編集でサイズが変わることを確かめるため 4x4 を使う
    private static BitmapSource MakeImage4x4() =>
        BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgr24, null, new byte[4 * 12], 12);

    [Fact]
    public void 既定では編集なしで元画像がそのまま出る()
    {
        var image = MakeImage();

        var item = new ScrapItem(image, new Int32Rect(0, 0, 1, 1));

        Assert.True(item.Edit.IsDefault);
        Assert.Same(image, item.EditedImage); // 変換を挟まない (SPEC-v1.7 6)
    }

    [Fact]
    public void 編集結果はキャッシュされ同じインスタンスを返す()
    {
        // 拡大表示のたびに再サンプリングするとキー連打で重くなるためキャッシュする (SPEC-v1.7 6)
        var item = new ScrapItem(MakeImage4x4(), new Int32Rect(0, 0, 4, 4))
        {
            Edit = ScrapEdit.Default.WithScale(200),
        };

        Assert.Same(item.EditedImage, item.EditedImage);
    }

    [Fact]
    public void 編集を変えるとキャッシュが捨てられる()
    {
        var item = new ScrapItem(MakeImage4x4(), new Int32Rect(0, 0, 4, 4))
        {
            Edit = ScrapEdit.Default.WithScale(200),
        };
        var before = item.EditedImage;

        item.Edit = ScrapEdit.Default.WithScale(50);

        Assert.NotSame(before, item.EditedImage);
        Assert.Equal(2, item.EditedImage.PixelWidth); // 4px の 50%
    }

    [Fact]
    public void 同じ内容の編集を設定してもキャッシュは保たれる()
    {
        // record の値等価で判定するため、別インスタンスでも内容が同じなら再計算しない
        var item = new ScrapItem(MakeImage4x4(), new Int32Rect(0, 0, 4, 4))
        {
            Edit = ScrapEdit.Default.WithScale(200),
        };
        var before = item.EditedImage;

        item.Edit = ScrapEdit.Default.WithScale(200);

        Assert.Same(before, item.EditedImage);
    }

    [Fact]
    public void 編集後のサイズはEditedPixelSizeに反映される()
    {
        var item = new ScrapItem(MakeImage4x4(), new Int32Rect(10, 20, 4, 4));

        Assert.Equal((4, 4), item.EditedPixelSize);

        item.Edit = ScrapEdit.Default.WithScale(200);

        Assert.Equal((8, 8), item.EditedPixelSize);
        // 元の PhysicalRect (キャプチャ元の位置・サイズ) は変わらない (SPEC-v1.7 5)
        Assert.Equal(new Int32Rect(10, 20, 4, 4), item.PhysicalRect);
    }
}
