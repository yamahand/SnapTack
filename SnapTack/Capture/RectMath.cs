using System.Windows;

namespace SnapTack.Capture;

/// <summary>
/// 範囲選択の座標変換 (DIP ↔ 物理px) とクランプ (SPEC-v1.x 2.4)。
/// ウィンドウ状態に依存する値は引数で受け取り、純粋計算としてテスト可能にしている。
/// </summary>
public static class RectMath
{
    /// <summary>
    /// DIP の矩形をスクリーンショットの物理ピクセル矩形へ変換する。
    /// </summary>
    /// <param name="dipRect">変換する矩形 (DIP)。</param>
    /// <param name="actualWidth">オーバーレイウィンドウの幅 (DIP)。</param>
    /// <param name="actualHeight">オーバーレイウィンドウの高さ (DIP)。</param>
    /// <param name="pixelWidth">スクリーンショットの幅 (物理px)。</param>
    /// <param name="pixelHeight">スクリーンショットの高さ (物理px)。</param>
    public static Int32Rect ToPhysicalRect(
        Rect dipRect, double actualWidth, double actualHeight, int pixelWidth, int pixelHeight)
    {
        // 0 以下だと除算で Infinity / NaN となり、int キャスト時に分かりにくい例外になるため
        // ここで弾く (ウィンドウ表示前に呼ばれた場合など)
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(actualWidth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(actualHeight, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(pixelHeight);

        // フリーズ画像はウィンドウ全面に Stretch=Fill で表示しているため、
        // ウィンドウ全体 (DIP) → 画像全体 (物理px) の比率で変換する。
        // オーバーレイが画面全体を覆っているとき、この比率は DPI スケールと一致する。
        // 万一ウィンドウサイズが画面と一致しない場合でも、見た目どおりの範囲が切り出される
        double scaleX = pixelWidth / actualWidth;
        double scaleY = pixelHeight / actualHeight;
        // DIP → 物理px (端は四捨五入)
        int x = (int)Math.Round(dipRect.X * scaleX);
        int y = (int)Math.Round(dipRect.Y * scaleY);
        int right = (int)Math.Round(dipRect.Right * scaleX);
        int bottom = (int)Math.Round(dipRect.Bottom * scaleY);
        return new Int32Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }

    /// <summary>矩形をスクリーンショットの範囲内 (物理px) に収める。</summary>
    /// <param name="rect">クランプする矩形 (物理px)。</param>
    /// <param name="pixelWidth">スクリーンショットの幅 (物理px)。</param>
    /// <param name="pixelHeight">スクリーンショットの高さ (物理px)。</param>
    public static Int32Rect ClampToScreenshot(Int32Rect rect, int pixelWidth, int pixelHeight)
    {
        // 負だと Math.Clamp が max < min で例外になるため、意図を明示して弾く
        ArgumentOutOfRangeException.ThrowIfNegative(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(pixelHeight);

        int x = Math.Clamp(rect.X, 0, pixelWidth);
        int y = Math.Clamp(rect.Y, 0, pixelHeight);
        int right = Math.Clamp(rect.X + rect.Width, x, pixelWidth);
        int bottom = Math.Clamp(rect.Y + rect.Height, y, pixelHeight);
        return new Int32Rect(x, y, right - x, bottom - y);
    }
}
