using System.Windows.Input;

namespace SnapTack.Input;

/// <summary>
/// Core のプラットフォーム非依存なホットキー表現と、WPF の
/// <see cref="ModifierKeys"/> / <see cref="Key"/> を相互変換する。
/// </summary>
/// <remarks>
/// 修飾キーは値が一致するよう <see cref="HotkeyModifier"/> を定義してあるためキャストで済む。
/// 本体キーはキー名の文字列で往復させる (理由は <see cref="Models.AppSettings.HotkeyKey"/> のコメント)。
/// </remarks>
internal static class HotkeyKeyMap
{
    /// <summary>修飾キーを WPF の列挙へ。</summary>
    public static ModifierKeys ToWpf(this HotkeyModifier modifiers) => (ModifierKeys)(int)modifiers;

    /// <summary>修飾キーを Core の列挙へ。</summary>
    public static HotkeyModifier ToCore(this ModifierKeys modifiers) => (HotkeyModifier)(int)modifiers;

    /// <summary>
    /// キー名を WPF の <see cref="Key"/> へ。設定ファイルの手編集などで未知の名前が入っていた場合は
    /// <see cref="Key.None"/> を返す。None は登録できないため、呼び出し側の登録失敗の警告に乗る
    /// (設定全体を既定値へ巻き戻さないことを優先する)。
    /// </summary>
    public static Key ToWpfKey(string? name) =>
        Enum.TryParse(name, ignoreCase: false, out Key key) ? key : Key.None;

    /// <summary>WPF の <see cref="Key"/> をキー名へ。</summary>
    public static string ToCoreName(this Key key) => key.ToString();
}
