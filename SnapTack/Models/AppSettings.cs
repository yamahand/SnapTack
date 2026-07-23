using System.Windows.Input;

namespace SnapTack.Models;

/// <summary>UI 言語。</summary>
public enum AppLanguage
{
    /// <summary>OS の表示言語に従う (対応言語になければ英語)。</summary>
    Auto,
    English,
    Japanese,
}

/// <summary>スクラップリストの一覧レイアウト。</summary>
public enum ScrapListLayout
{
    /// <summary>サムネイルのグリッド (内容を判別しやすい)。</summary>
    Grid,
    /// <summary>サムネイル付きの縦リスト (日時・サイズを併記)。</summary>
    List,
}

/// <summary>アプリ設定。JSON で永続化する (SPEC 4.5)。</summary>
public class AppSettings
{
    /// <summary>キャプチャホットキーの修飾キー。既定は Ctrl+Shift (SPEC 4.2)。</summary>
    public ModifierKeys HotkeyModifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Shift;

    /// <summary>キャプチャホットキーの本体キー。既定は Z。</summary>
    public Key HotkeyKey { get; set; } = Key.Z;

    /// <summary>スクラップリストを開くホットキーの修飾キー。既定は Ctrl+Shift (SPEC-v1.5 2.5)。</summary>
    public ModifierKeys ScrapListHotkeyModifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Shift;

    /// <summary>スクラップリストを開くホットキーの本体キー。既定は L。</summary>
    public Key ScrapListHotkeyKey { get; set; } = Key.L;

    /// <summary>PNG 保存の前回フォルダ。未保存なら null で「ピクチャ」を使う (SPEC-v1.x 2.1)。</summary>
    public string? LastSaveDirectory { get; set; }

    /// <summary>UI 言語。既定は Auto (OS の表示言語に従う)。</summary>
    public AppLanguage Language { get; set; } = AppLanguage.Auto;

    /// <summary>スクラップの最大保持数 (Pinned + Stashed)。既定 200 (SPEC-v1.5 2.4)。</summary>
    public int MaxScraps { get; set; } = 200;

    /// <summary>ゴミ箱 (Trashed) の最大保持数。既定 50 (SPEC-v1.5 2.4)。</summary>
    public int MaxTrashedScraps { get; set; } = 50;

    /// <summary>スクラップリストの一覧レイアウト。既定はサムネイルのグリッド。</summary>
    public ScrapListLayout ScrapListLayout { get; set; } = ScrapListLayout.Grid;

    /// <summary>浅いコピーを返す。設定画面が一部項目だけ書き換える際に使う。</summary>
    public AppSettings Clone() => (AppSettings)MemberwiseClone();

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
