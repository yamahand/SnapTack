namespace SnapTack.Primitives;

/// <summary>
/// 物理ピクセルの矩形 (CLAUDE.md「座標系の規約」)。キャプチャ範囲・モニタ範囲に使う。
/// </summary>
/// <remarks>
/// WPF の <c>Int32Rect</c> の置き換え。Core を net10.0 に保つため WPF 型は持ち込まない。
/// <c>record struct</c> にしているのは、テストや状態比較で値等価が要るため
/// (Int32Rect も値等価だったので挙動を引き継ぐ)。
/// </remarks>
/// <param name="X">左端 (物理px)。仮想スクリーン座標では負になり得る。</param>
/// <param name="Y">上端 (物理px)。仮想スクリーン座標では負になり得る。</param>
/// <param name="Width">幅 (物理px)。</param>
/// <param name="Height">高さ (物理px)。</param>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);
