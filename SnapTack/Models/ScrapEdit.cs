using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnapTack.Models;

/// <summary>
/// スクラップに適用する変換 (拡大縮小・回転・反転・トリム) を表す不変の値 (SPEC-v1.7 2.1)。
/// </summary>
/// <remarks>
/// **非破壊編集**を採る。元画像は書き換えず、本クラスのパラメータを表示・コピー・保存の
/// 各時点で適用する (SPEC-v1.7 2.1 / 2.8)。これにより拡大→縮小を繰り返しても劣化せず、
/// <see cref="Default"/> へ戻すだけで編集を取り消せる。
///
/// ビューから切り離して純粋な計算にしてあるのは、変換ロジックを単体テストするため
/// (<see cref="ScrapManager"/> を <see cref="Views.IScrapView"/> で抽象化したのと同じ理由)。
/// </remarks>
public sealed record ScrapEdit
{
    /// <summary>拡大縮小率の下限 (%)。これ以上小さいと内容を判別できない (SPEC-v1.7 2.2)。</summary>
    public const int MinScalePercent = 25;

    /// <summary>拡大縮小率の上限 (%)。誤操作で画面を覆い尽くさないための上限 (SPEC-v1.7 2.2)。</summary>
    public const int MaxScalePercent = 400;

    /// <summary>拡大縮小の既定ステップ (%)。<c>Alt+↑ / Alt+↓</c> (SPEC-v1.7 2.2)。</summary>
    public const int ScaleStepPercent = 10;

    /// <summary>拡大縮小の微調整ステップ (%)。<c>Alt+Shift+↑ / Alt+Shift+↓</c> (SPEC-v1.7 2.2)。</summary>
    public const int ScaleFineStepPercent = 1;

    /// <summary>編集なし (すべて既定値)。この値のときは変換処理そのものを通さない。</summary>
    public static readonly ScrapEdit Default = new();

    /// <summary>拡大縮小率 (%)。100 が物理ピクセル等倍 (SPEC-v1.7 2.2)。</summary>
    public int ScalePercent { get; init; } = 100;

    /// <summary>回転角 (度)。0 / 90 / 180 / 270 のいずれか。時計回りが正 (SPEC-v1.7 2.3)。</summary>
    public int RotationDegrees { get; init; }

    /// <summary>水平反転するか (SPEC-v1.7 2.3)。</summary>
    public bool FlipHorizontal { get; init; }

    /// <summary>垂直反転するか (SPEC-v1.7 2.3)。</summary>
    public bool FlipVertical { get; init; }

    /// <summary>
    /// トリム範囲 (**元画像の物理ピクセル座標**)。null なら全体 (SPEC-v1.7 2.4)。
    /// </summary>
    /// <remarks>
    /// 元画像座標で持つことで、トリムを重ねがけしても保持するのは常に 1 つで済み、
    /// 「編集をリセット」で必ず元へ戻せる。
    /// </remarks>
    public Int32Rect? TrimRect { get; init; }

    /// <summary>何も編集されていない (すべて既定値) か。</summary>
    public bool IsDefault =>
        ScalePercent == 100 && RotationDegrees == 0 && !FlipHorizontal && !FlipVertical && TrimRect is null;

    /// <summary>回転で幅と高さが入れ替わるか (90 / 270 度。SPEC-v1.7 2.3)。</summary>
    public bool SwapsAxes => RotationDegrees is 90 or 270;

    // ===== パラメータ操作 (いずれも新しいインスタンスを返す) =====

    /// <summary>拡大縮小率を設定する。範囲外はクランプする (SPEC-v1.7 2.2)。</summary>
    public ScrapEdit WithScale(int percent) =>
        this with { ScalePercent = ClampScale(percent) };

    /// <summary>現在の拡大縮小率に <paramref name="delta"/> % を加える。範囲外はクランプする。</summary>
    public ScrapEdit ScaleBy(int delta) => WithScale(ScalePercent + delta);

    /// <summary>拡大縮小率を有効範囲へ収める。</summary>
    public static int ClampScale(int percent) =>
        Math.Clamp(percent, MinScalePercent, MaxScalePercent);

    /// <summary>
    /// 時計回りに回転を加える (<paramref name="degrees"/> は 90 の倍数。負値で反時計回り)。
    /// </summary>
    public ScrapEdit RotateBy(int degrees)
    {
        // 負値でも 0〜359 に収める (C# の % は負を返すため +360 してから再度剰余を取る)
        int normalized = ((RotationDegrees + degrees) % 360 + 360) % 360;
        return this with { RotationDegrees = normalized };
    }

    /// <summary>水平反転をトグルする。</summary>
    public ScrapEdit ToggleFlipHorizontal() => this with { FlipHorizontal = !FlipHorizontal };

    /// <summary>垂直反転をトグルする。</summary>
    public ScrapEdit ToggleFlipVertical() => this with { FlipVertical = !FlipVertical };

    /// <summary>
    /// **現在の見た目**の座標系で指定された範囲を、元画像座標のトリムとして取り込む
    /// (SPEC-v1.7 2.4)。既存のトリムがあれば合成し、保持するのは常に 1 つに保つ。
    /// </summary>
    /// <param name="displayRect">
    /// 変換適用後の画像 (= 画面に見えているもの) の**物理ピクセル**座標での選択範囲。
    /// スケール適用後の座標であること。
    /// </param>
    /// <param name="sourceSize">元画像の物理ピクセルサイズ。</param>
    /// <remarks>
    /// ユーザーは「見えているもの」を選ぶのであって元画像の座標を意識しない。
    /// そのため表示座標 → 元画像座標の逆変換をここで行う。
    /// </remarks>
    public ScrapEdit WithTrimFromDisplay(Int32Rect displayRect, Size sourceSize)
    {
        // 1. スケールを取り除く (表示座標 → 回転・反転適用後の座標)
        double scale = ScalePercent / 100.0;
        double x = displayRect.X / scale;
        double y = displayRect.Y / scale;
        double w = displayRect.Width / scale;
        double h = displayRect.Height / scale;

        // 2. 現在のトリム後・回転前のサイズ (= 逆変換の基準になる矩形の大きさ)
        var trimmed = TrimRect ?? new Int32Rect(0, 0, (int)sourceSize.Width, (int)sourceSize.Height);
        double baseW = SwapsAxes ? trimmed.Height : trimmed.Width;
        double baseH = SwapsAxes ? trimmed.Width : trimmed.Height;

        // 3. 反転を戻す (回転後の座標系で行う。適用順が回転→反転のため、逆順で解く)
        if (FlipHorizontal)
        {
            x = baseW - x - w;
        }
        if (FlipVertical)
        {
            y = baseH - y - h;
        }

        // 4. 回転を戻す。時計回り θ の逆変換を、回転後の矩形サイズ (baseW × baseH) 上で行う
        double ux, uy, uw, uh;
        switch (RotationDegrees)
        {
            case 90:
                // 時計回り 90: (px, py) → (baseW - py - ph, px)。これを解いて元へ戻す
                ux = y;
                uy = baseW - x - w;
                uw = h;
                uh = w;
                break;
            case 180:
                ux = baseW - x - w;
                uy = baseH - y - h;
                uw = w;
                uh = h;
                break;
            case 270:
                ux = baseH - y - h;
                uy = x;
                uw = h;
                uh = w;
                break;
            default:
                ux = x;
                uy = y;
                uw = w;
                uh = h;
                break;
        }

        // 5. 既存トリムの原点を足して元画像座標にし、元画像の範囲内へ収める
        var result = ClampToSource(
            new Int32Rect(
                trimmed.X + (int)Math.Round(ux),
                trimmed.Y + (int)Math.Round(uy),
                (int)Math.Round(uw),
                (int)Math.Round(uh)),
            trimmed);

        return result is null ? this : this with { TrimRect = result };
    }

    /// <summary>
    /// トリム矩形を親矩形 (既存トリム or 元画像) の内側へ収める。
    /// 幅・高さが 0 以下になる場合は null (トリムとして成立しない)。
    /// </summary>
    private static Int32Rect? ClampToSource(Int32Rect rect, Int32Rect bounds)
    {
        int left = Math.Max(rect.X, bounds.X);
        int top = Math.Max(rect.Y, bounds.Y);
        int right = Math.Min(rect.X + rect.Width, bounds.X + bounds.Width);
        int bottom = Math.Min(rect.Y + rect.Height, bounds.Y + bounds.Height);
        if (right <= left || bottom <= top)
        {
            return null;
        }
        return new Int32Rect(left, top, right - left, bottom - top);
    }

    // ===== 適用 =====

    /// <summary>
    /// 元画像に本変換を適用した画像を返す (SPEC-v1.7 2.7)。
    /// 適用順は **トリム → 回転 → 反転 → 拡大縮小** で固定する。
    /// </summary>
    /// <remarks>
    /// トリムを先に置くのは <see cref="TrimRect"/> を元画像座標で保持しているため。
    /// 拡大縮小を最後に置くのは、再サンプリングを 1 回だけにして劣化と計算量を抑えるため。
    /// 戻り値は <c>Freeze()</c> 済みで、ウィンドウ・サムネイル・出力で使い回せる (CLAUDE.md の規約)。
    /// </remarks>
    public BitmapSource Apply(BitmapSource source)
    {
        // 編集されていなければ元画像をそのまま返す。v1.6 以前と完全に同じ経路を通す
        // (変換を挟まないことが互換性の中核。SPEC-v1.7 6)
        if (IsDefault)
        {
            return source;
        }

        BitmapSource result = source;

        // 1. トリム。元画像座標なので最初に適用する
        if (TrimRect is { } trim)
        {
            var safe = ClampToSource(trim, new Int32Rect(0, 0, result.PixelWidth, result.PixelHeight));
            if (safe is { } rect)
            {
                result = new CroppedBitmap(result, rect);
            }
        }

        // 2. 回転 → 3. 反転。TransformedBitmap 1 回で合成できるため同時に組む。
        // TransformGroup は Children の順に適用されるため、**回転を先に入れる**こと。
        // 逆変換 (WithTrimFromDisplay) は「反転を解いてから回転を解く」前提で書かれており、
        // ここで順序を入れ替えると回転と反転を併用した時だけトリム位置がずれる (SPEC-v1.7 2.7)。
        // 反転は負のスケール (ScaleTransform) で表現する
        var transform = new TransformGroup();
        if (RotationDegrees != 0)
        {
            transform.Children.Add(new RotateTransform(RotationDegrees));
        }
        if (FlipHorizontal || FlipVertical)
        {
            transform.Children.Add(new ScaleTransform(
                FlipHorizontal ? -1 : 1,
                FlipVertical ? -1 : 1));
        }
        if (transform.Children.Count > 0)
        {
            result = new TransformedBitmap(result, transform);
        }

        // 4. 拡大縮小。最後に 1 度だけ再サンプリングする。
        // NearestNeighbor は表示側 (ScrapWindow.xaml) の指定で効くため、ここでは行わない
        if (ScalePercent != 100)
        {
            double scale = ScalePercent / 100.0;
            result = new TransformedBitmap(result, new ScaleTransform(scale, scale));
        }

        result.Freeze(); // 複数のウィンドウで使い回すため (CLAUDE.md の規約)
        return result;
    }

    /// <summary>
    /// 元画像サイズに本変換を適用した後の**物理ピクセル**サイズを返す。
    /// 画像を実際に生成せずに付箋のサイズを決めたい場合に使う。
    /// </summary>
    public (int Width, int Height) GetResultSize(int sourceWidth, int sourceHeight)
    {
        int w = TrimRect?.Width ?? sourceWidth;
        int h = TrimRect?.Height ?? sourceHeight;
        if (SwapsAxes)
        {
            (w, h) = (h, w);
        }
        double scale = ScalePercent / 100.0;
        // 1px を下回らないようにする (0 サイズのウィンドウは作れない)
        return (Math.Max(1, (int)Math.Round(w * scale)), Math.Max(1, (int)Math.Round(h * scale)));
    }
}
