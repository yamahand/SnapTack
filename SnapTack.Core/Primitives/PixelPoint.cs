namespace SnapTack.Primitives;

/// <summary>
/// 物理ピクセルの座標 (CLAUDE.md「座標系の規約」)。付箋の表示位置の保存に使う。
/// </summary>
/// <remarks>
/// WPF の <c>Point</c> の置き換え。整数ではなく double で持つのは、
/// <c>scraps/index.json</c> の WindowPosition が double で書かれているため。
/// int にすると保存済みデータのスキーマが変わってしまう (SPEC-v1.5 2.4 の後方互換)。
/// 値の出自は GetWindowRect (整数) なので、実際に端数が入ることはない。
/// </remarks>
/// <param name="X">左端 (物理px、仮想スクリーン座標)。</param>
/// <param name="Y">上端 (物理px、仮想スクリーン座標)。</param>
public readonly record struct PixelPoint(double X, double Y);
