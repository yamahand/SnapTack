using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapTack.Models;
using Xunit;

namespace SnapTack.Tests;

/// <summary>
/// スクラップの編集 (拡大縮小・回転・反転・トリム。SPEC-v1.7 2) の検証。
/// ビューから切り離した純粋な計算なのでウィンドウ無しでテストできる。
/// </summary>
public sealed class ScrapEditTests
{
    /// <summary>指定サイズの単色画像を作る (サイズ計算の検証用)。</summary>
    private static BitmapSource NewImage(int width, int height)
    {
        int stride = width * 3;
        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null,
            new byte[stride * height], stride);
    }

    // ===== 既定値 =====

    [Fact]
    public void 既定は編集なし()
    {
        var edit = ScrapEdit.Default;

        Assert.True(edit.IsDefault);
        Assert.Equal(100, edit.ScalePercent);
        Assert.Equal(0, edit.RotationDegrees);
        Assert.False(edit.FlipHorizontal);
        Assert.False(edit.FlipVertical);
        Assert.Null(edit.TrimRect);
    }

    [Fact]
    public void 編集なしなら元画像をそのまま返す()
    {
        // 変換を挟まないことが v1.6 以前との互換性の中核 (SPEC-v1.7 6)
        var source = NewImage(10, 8);

        var result = ScrapEdit.Default.Apply(source);

        Assert.Same(source, result);
    }

    // ===== 拡大縮小 (SPEC-v1.7 2.2) =====

    [Theory]
    [InlineData(200, 200)]
    [InlineData(25, 25)]
    [InlineData(400, 400)]
    [InlineData(10, ScrapEdit.MinScalePercent)]  // 下限へクランプ
    [InlineData(1000, ScrapEdit.MaxScalePercent)] // 上限へクランプ
    public void 拡大縮小率は範囲内へクランプされる(int input, int expected)
    {
        Assert.Equal(expected, ScrapEdit.Default.WithScale(input).ScalePercent);
    }

    [Fact]
    public void 拡大縮小の加算も範囲内へクランプされる()
    {
        var atMax = ScrapEdit.Default.WithScale(ScrapEdit.MaxScalePercent);

        // 上限に張り付いた状態でさらに拡大しても値は変わらない (キー連打の想定)
        Assert.Equal(ScrapEdit.MaxScalePercent, atMax.ScaleBy(10).ScalePercent);
        Assert.Equal(ScrapEdit.MaxScalePercent - 10, atMax.ScaleBy(-10).ScalePercent);
    }

    [Fact]
    public void 拡大縮小すると結果サイズが変わる()
    {
        var edit = ScrapEdit.Default.WithScale(200);

        Assert.Equal((20, 16), edit.GetResultSize(10, 8));
    }

    [Fact]
    public void 拡大縮小した画像は実際にリサイズされる()
    {
        var result = ScrapEdit.Default.WithScale(200).Apply(NewImage(10, 8));

        Assert.Equal(20, result.PixelWidth);
        Assert.Equal(16, result.PixelHeight);
        Assert.True(result.IsFrozen); // 使い回すため Freeze する (CLAUDE.md の規約)
    }

    [Fact]
    public void 縮小しても1pxを下回らない()
    {
        // 0 サイズのウィンドウは作れないため、下限を 1px に保つ
        var edit = ScrapEdit.Default.WithScale(ScrapEdit.MinScalePercent);

        var (width, height) = edit.GetResultSize(2, 2);

        Assert.True(width >= 1);
        Assert.True(height >= 1);
    }

    // ===== 回転・反転 (SPEC-v1.7 2.3) =====

    [Theory]
    [InlineData(90, 90)]
    [InlineData(180, 180)]
    [InlineData(360, 0)]   // 一周して戻る
    [InlineData(450, 90)]  // 360 を超えても正規化される
    public void 回転は0から359へ正規化される(int degrees, int expected)
    {
        Assert.Equal(expected, ScrapEdit.Default.RotateBy(degrees).RotationDegrees);
    }

    [Fact]
    public void 反時計回りの回転も正の角度に正規化される()
    {
        // C# の % は負を返すため、明示的に +360 して正規化している
        Assert.Equal(270, ScrapEdit.Default.RotateBy(-90).RotationDegrees);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(90, true)]
    [InlineData(180, false)]
    [InlineData(270, true)]
    public void 回転90度と270度で軸が入れ替わる(int degrees, bool swaps)
    {
        Assert.Equal(swaps, ScrapEdit.Default.RotateBy(degrees).SwapsAxes);
    }

    [Fact]
    public void 回転90度で幅と高さが入れ替わる()
    {
        var edit = ScrapEdit.Default.RotateBy(90);

        Assert.Equal((8, 10), edit.GetResultSize(10, 8));

        var result = edit.Apply(NewImage(10, 8));
        Assert.Equal(8, result.PixelWidth);
        Assert.Equal(10, result.PixelHeight);
    }

    [Fact]
    public void 反転しても画像サイズは変わらない()
    {
        var edit = ScrapEdit.Default.ToggleFlipHorizontal().ToggleFlipVertical();

        Assert.Equal((10, 8), edit.GetResultSize(10, 8));

        var result = edit.Apply(NewImage(10, 8));
        Assert.Equal(10, result.PixelWidth);
        Assert.Equal(8, result.PixelHeight);
    }

    [Fact]
    public void 反転はトグルできる()
    {
        var flipped = ScrapEdit.Default.ToggleFlipHorizontal();
        Assert.True(flipped.FlipHorizontal);
        Assert.False(flipped.ToggleFlipHorizontal().FlipHorizontal);
    }

    // ===== トリム (SPEC-v1.7 2.4) =====

    [Fact]
    public void 編集なしのトリムは表示座標がそのまま元画像座標になる()
    {
        var edit = ScrapEdit.Default.WithTrimFromDisplay(
            new Int32Rect(2, 3, 4, 5), new Size(10, 8));

        Assert.Equal(new Int32Rect(2, 3, 4, 5), edit.TrimRect);
    }

    [Fact]
    public void 拡大中のトリムはスケールを戻して元画像座標になる()
    {
        // 200% 表示で (4,6)-8x10 を選んだ = 元画像では (2,3)-4x5
        var edit = ScrapEdit.Default.WithScale(200)
            .WithTrimFromDisplay(new Int32Rect(4, 6, 8, 10), new Size(10, 8));

        Assert.Equal(new Int32Rect(2, 3, 4, 5), edit.TrimRect);
    }

    [Fact]
    public void トリム後は結果サイズがトリム範囲になる()
    {
        var edit = ScrapEdit.Default.WithTrimFromDisplay(
            new Int32Rect(2, 3, 4, 5), new Size(10, 8));

        Assert.Equal((4, 5), edit.GetResultSize(10, 8));

        var result = edit.Apply(NewImage(10, 8));
        Assert.Equal(4, result.PixelWidth);
        Assert.Equal(5, result.PixelHeight);
    }

    [Fact]
    public void トリムを重ねがけしてもパラメータは1つに保たれる()
    {
        var source = new Size(20, 20);
        // 1 回目: (5,5)-10x10 を切り出す
        var first = ScrapEdit.Default.WithTrimFromDisplay(new Int32Rect(5, 5, 10, 10), source);
        // 2 回目: その結果の (2,2)-4x4 を切り出す = 元画像では (7,7)-4x4
        var second = first.WithTrimFromDisplay(new Int32Rect(2, 2, 4, 4), source);

        Assert.Equal(new Int32Rect(7, 7, 4, 4), second.TrimRect);
        Assert.Equal((4, 4), second.GetResultSize(20, 20));
    }

    [Fact]
    public void トリムは元画像の範囲外へはみ出さない()
    {
        // 画像より大きい範囲を選んでも、切り出せるのは画像の内側だけ
        var edit = ScrapEdit.Default.WithTrimFromDisplay(
            new Int32Rect(5, 5, 100, 100), new Size(10, 8));

        Assert.Equal(new Int32Rect(5, 5, 5, 3), edit.TrimRect);
    }

    [Fact]
    public void 回転180度中のトリムは元画像座標へ逆変換される()
    {
        // 180 度回転した見た目の左上 (0,0)-2x2 は、元画像では右下 (8,6)-2x2
        var edit = ScrapEdit.Default.RotateBy(180)
            .WithTrimFromDisplay(new Int32Rect(0, 0, 2, 2), new Size(10, 8));

        Assert.Equal(new Int32Rect(8, 6, 2, 2), edit.TrimRect);
    }

    [Fact]
    public void 水平反転中のトリムは元画像座標へ逆変換される()
    {
        // 左右反転した見た目の左上 (0,0)-2x2 は、元画像では右上 (8,0)-2x2
        var edit = ScrapEdit.Default.ToggleFlipHorizontal()
            .WithTrimFromDisplay(new Int32Rect(0, 0, 2, 2), new Size(10, 8));

        Assert.Equal(new Int32Rect(8, 0, 2, 2), edit.TrimRect);
    }

    [Fact]
    public void 回転90度中のトリムは元画像座標へ逆変換される()
    {
        // 元画像 10x8 を時計回り 90 度 → 見た目は 8x10。
        // 見た目の左上 (0,0)-2x3 は、元画像では左下側の (0,6)-3x2 に対応する
        var edit = ScrapEdit.Default.RotateBy(90)
            .WithTrimFromDisplay(new Int32Rect(0, 0, 2, 3), new Size(10, 8));

        Assert.Equal(new Int32Rect(0, 6, 3, 2), edit.TrimRect);
    }

    [Fact]
    public void 成立しないトリムは無視される()
    {
        // 画像の外だけを選んだ場合。トリムとして成立しないので元の編集を保つ
        var before = ScrapEdit.Default;

        var after = before.WithTrimFromDisplay(new Int32Rect(50, 50, 10, 10), new Size(10, 8));

        Assert.Null(after.TrimRect);
    }

    // ===== 組み合わせ =====

    [Fact]
    public void トリムと回転と拡大を組み合わせた結果サイズが正しい()
    {
        // 適用順は トリム → 回転 → 反転 → 拡大縮小 (SPEC-v1.7 2.7)。
        // 20x20 を (0,0)-10x4 でトリム → 90 度回転で 4x10 → 200% で 8x20
        var edit = new ScrapEdit
        {
            TrimRect = new Int32Rect(0, 0, 10, 4),
            RotationDegrees = 90,
            ScalePercent = 200,
        };

        Assert.Equal((8, 20), edit.GetResultSize(20, 20));

        var result = edit.Apply(NewImage(20, 20));
        Assert.Equal(8, result.PixelWidth);
        Assert.Equal(20, result.PixelHeight);
    }

    [Fact]
    public void 編集が入るとIsDefaultがfalseになる()
    {
        Assert.False(ScrapEdit.Default.WithScale(200).IsDefault);
        Assert.False(ScrapEdit.Default.RotateBy(90).IsDefault);
        Assert.False(ScrapEdit.Default.ToggleFlipHorizontal().IsDefault);
        Assert.False(ScrapEdit.Default.ToggleFlipVertical().IsDefault);
        Assert.False((ScrapEdit.Default with { TrimRect = new Int32Rect(0, 0, 1, 1) }).IsDefault);
    }

    [Fact]
    public void リセットすると既定へ戻る()
    {
        // 「編集をリセット」はトリムし過ぎた場合の唯一の復旧手段 (SPEC-v1.7 2.6)
        var edited = new ScrapEdit
        {
            ScalePercent = 200,
            RotationDegrees = 270,
            FlipHorizontal = true,
            TrimRect = new Int32Rect(1, 1, 2, 2),
        };

        Assert.False(edited.IsDefault);
        Assert.True(ScrapEdit.Default.IsDefault);
        // record の値等価により、既定と同じ内容なら等しいと判定される
        Assert.Equal(ScrapEdit.Default, edited with
        {
            ScalePercent = 100,
            RotationDegrees = 0,
            FlipHorizontal = false,
            TrimRect = null,
        });
    }
}
