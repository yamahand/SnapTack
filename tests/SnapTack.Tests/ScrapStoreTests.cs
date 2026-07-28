using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapTack.Models;
using Xunit;

namespace SnapTack.Tests;

/// <summary>index.json と画像の読み書き (SPEC-v1.5 2.4) の検証。</summary>
public sealed class ScrapStoreTests : IDisposable
{
    private readonly string _dir;

    public ScrapStoreTests()
    {
        // テストごとに独立した一時ディレクトリを使う
        _dir = Path.Combine(Path.GetTempPath(), "SnapTackScrapStoreTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private ScrapStore NewStore() => new(_dir);

    private static ScrapItem NewScrap(ScrapState state = ScrapState.Pinned)
    {
        var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgr24, null,
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, 6);
        return new ScrapItem(image, new Int32Rect(10, 20, 2, 2)) { State = state };
    }

    private string IndexPath => Path.Combine(_dir, "index.json");

    [Fact]
    public void 保存して読み直すとメタデータが一致する()
    {
        var store = NewStore();
        var item = NewScrap();
        item.OpacityPercent = 75;
        item.IsDice = true;
        item.WindowPosition = new Point(100, 200);

        Assert.True(store.SaveImage(item));
        Assert.True(store.SaveIndex(new[] { item }));

        var loaded = NewStore().Load(out bool needsRewrite);

        Assert.False(needsRewrite);
        var one = Assert.Single(loaded);
        Assert.Equal(item.Id, one.Id);
        Assert.Equal(item.PhysicalRect, one.PhysicalRect);
        Assert.Equal(75, one.OpacityPercent);
        Assert.True(one.IsDice);
        Assert.Equal(new Point(100, 200), one.WindowPosition);
        // 画像は遅延読み込み。参照するとファイルから読める
        Assert.False(one.IsImageLoaded);
        Assert.NotNull(one.Image);
        Assert.True(one.IsImageLoaded);
    }

    [Fact]
    public void ディレクトリが無ければ空を返す()
    {
        var store = new ScrapStore(Path.Combine(_dir, "does-not-exist"));

        var loaded = store.Load(out bool needsRewrite);

        Assert.Empty(loaded);
        Assert.False(needsRewrite);
    }

    [Fact]
    public void 壊れたindexは空として読む()
    {
        File.WriteAllText(IndexPath, "{ this is not valid json");

        var loaded = NewStore().Load(out _);

        Assert.Empty(loaded);
    }

    [Fact]
    public void 未知のスキーマは空として読む()
    {
        // 将来の形式。無理に解釈せず空で起動する (既存データを壊さない)
        File.WriteAllText(IndexPath, """{ "SchemaVersion": 999, "Scraps": [] }""");

        var loaded = NewStore().Load(out _);

        Assert.Empty(loaded);
    }

    // ===== 編集パラメータの永続化 (SPEC-v1.7 5) =====

    [Fact]
    public void 編集パラメータを保存して読み直すと一致する()
    {
        var store = NewStore();
        var item = NewScrap();
        item.Edit = new ScrapEdit
        {
            ScalePercent = 200,
            RotationDegrees = 90,
            FlipHorizontal = true,
            TrimRect = new Int32Rect(0, 0, 1, 2),
        };

        Assert.True(store.SaveImage(item));
        Assert.True(store.SaveIndex(new[] { item }));

        var one = Assert.Single(NewStore().Load(out _));

        Assert.Equal(200, one.Edit.ScalePercent);
        Assert.Equal(90, one.Edit.RotationDegrees);
        Assert.True(one.Edit.FlipHorizontal);
        Assert.False(one.Edit.FlipVertical);
        Assert.Equal(new Int32Rect(0, 0, 1, 2), one.Edit.TrimRect);
    }

    [Fact]
    public void v1のindexは編集なしとして読める()
    {
        // v1.6 以前が書いた index。編集パラメータのキーが無くても既定値で補って読む
        var store = NewStore();
        var item = NewScrap();
        store.SaveImage(item);
        File.WriteAllText(IndexPath, $$"""
            {
              "SchemaVersion": 1,
              "Scraps": [
                {
                  "Id": "{{item.Id}}",
                  "CapturedAt": "2026-01-01T00:00:00+00:00",
                  "State": "Pinned",
                  "PhysicalRect": { "X": 10, "Y": 20, "Width": 2, "Height": 2 },
                  "OpacityPercent": 100
                }
              ]
            }
            """);

        var loaded = NewStore().Load(out bool needsRewrite);

        var one = Assert.Single(loaded);
        // 「編集していないスクラップ」として復元され、v1.6 以前と同じ見た目になる
        Assert.True(one.Edit.IsDefault);
        // 旧形式から読んだので現行形式へ書き直す対象になる
        Assert.True(needsRewrite);
    }

    [Fact]
    public void 壊れた編集パラメータは正規化して読む()
    {
        // 手編集・別バージョン由来の不正値。落とさず既定へ倒す
        var store = NewStore();
        var item = NewScrap();
        store.SaveImage(item);
        File.WriteAllText(IndexPath, $$"""
            {
              "SchemaVersion": 2,
              "Scraps": [
                {
                  "Id": "{{item.Id}}",
                  "CapturedAt": "2026-01-01T00:00:00+00:00",
                  "State": "Pinned",
                  "PhysicalRect": { "X": 10, "Y": 20, "Width": 2, "Height": 2 },
                  "OpacityPercent": 100,
                  "ScalePercent": 5000,
                  "RotationDegrees": 45,
                  "TrimRect": { "X": 0, "Y": 0, "Width": 0, "Height": 0 }
                }
              ]
            }
            """);

        var one = Assert.Single(NewStore().Load(out _));

        Assert.Equal(ScrapEdit.MaxScalePercent, one.Edit.ScalePercent); // クランプされる
        Assert.Equal(0, one.Edit.RotationDegrees);                     // 90 の倍数でないので 0
        Assert.Null(one.Edit.TrimRect);                                // 幅 0 は成立しない
    }

    [Fact]
    public void 画像が欠けているエントリは読み飛ばし書き直しを要求する()
    {
        var store = NewStore();
        var withImage = NewScrap();
        var noImage = NewScrap();
        store.SaveImage(withImage); // withImage の画像だけ書く
        store.SaveIndex(new[] { withImage, noImage });

        var loaded = NewStore().Load(out bool needsRewrite);

        Assert.True(needsRewrite);
        var one = Assert.Single(loaded);
        Assert.Equal(withImage.Id, one.Id);
    }

    [Fact]
    public void 書き込み不可なディレクトリではSaveがfalseを返す()
    {
        // ディレクトリを作れない状況を作る: 同名のファイルを置いておくと CreateDirectory が失敗する
        string blocked = Path.Combine(_dir, "blocked");
        File.WriteAllText(blocked, "not a directory");
        var store = new ScrapStore(blocked);

        Assert.False(store.SaveImage(NewScrap()));
        Assert.False(store.SaveIndex(new[] { NewScrap() }));
    }

    [Fact]
    public void DeleteImageで画像ファイルが消える()
    {
        var store = NewStore();
        var item = NewScrap();
        store.SaveImage(item);
        string imagePath = Path.Combine(_dir, item.Id + ".png");
        Assert.True(File.Exists(imagePath));

        store.DeleteImage(item.Id);

        Assert.False(File.Exists(imagePath));
    }
}
