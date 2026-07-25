namespace SnapTack.Primitives;

/// <summary>
/// DIP (デバイス非依存ピクセル) の矩形 (CLAUDE.md「座標系の規約」)。
/// UI 層で作った選択範囲を物理ピクセルへ変換する際の入力に使う。
/// </summary>
/// <remarks>
/// WPF の <c>Rect</c> の置き換え。必要な <see cref="Right"/> / <see cref="Bottom"/> だけを
/// 引き継いでいる。負の幅・高さは検証しない (呼び出し側が正規化済みの矩形を渡す前提。
/// WPF の Rect はコンストラクタで弾いていたが、その検証は UI 層の Rect 生成時点で効いている)。
/// </remarks>
/// <param name="X">左端 (DIP)。</param>
/// <param name="Y">上端 (DIP)。</param>
/// <param name="Width">幅 (DIP)。</param>
/// <param name="Height">高さ (DIP)。</param>
public readonly record struct DipRect(double X, double Y, double Width, double Height)
{
    /// <summary>右端 (X + Width)。</summary>
    public double Right => X + Width;

    /// <summary>下端 (Y + Height)。</summary>
    public double Bottom => Y + Height;
}
