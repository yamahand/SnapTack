using System.Windows.Input;

namespace SnapTack.Models;

/// <summary>アプリ設定。JSON で永続化する (SPEC 4.5)。</summary>
public class AppSettings
{
    /// <summary>キャプチャホットキーの修飾キー。既定は Ctrl+Shift (SPEC 4.2)。</summary>
    public ModifierKeys HotkeyModifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Shift;

    /// <summary>キャプチャホットキーの本体キー。既定は Z。</summary>
    public Key HotkeyKey { get; set; } = Key.Z;

    /// <summary>"Ctrl+Shift+Z" 形式の表示文字列を返す。</summary>
    public string GetHotkeyDisplayText() => FormatHotkey(HotkeyModifiers, HotkeyKey);

    /// <summary>修飾キーとキーの組み合わせを "Ctrl+Alt+Shift+Win+キー" 形式で整形する。</summary>
    public static string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>(5);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
