namespace SnapTack.Input;

/// <summary>
/// グローバルホットキーの修飾キー。
/// </summary>
/// <remarks>
/// WPF の <c>System.Windows.Input.ModifierKeys</c> の置き換え。名前と値の両方を
/// 元の列挙に一致させてある:
/// <list type="bullet">
///   <item>名前 — <c>JsonStringEnumConverter</c> が "Control, Shift" のような名前で
///   書き出すため、v1.5 以前の settings.json をそのまま読める (SPEC-v1.5 4)</item>
///   <item>値 — UI 層がキャストだけで WPF の列挙へ相互変換できる</item>
/// </list>
/// </remarks>
[Flags]
public enum HotkeyModifier
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}
