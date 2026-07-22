using System.Globalization;
using System.Resources;

namespace SnapTack.Resources;

/// <summary>
/// UI 文字列のリソースアクセサ。既定は英語 (Strings.resx)、日本語は衛星アセンブリ (Strings.ja.resx)。
/// 参照時の <see cref="CultureInfo.CurrentUICulture"/> で解決されるため、
/// <see cref="Models.LanguageService"/> でカルチャを切り替えると以降の取得結果が変わる。
/// </summary>
/// <remarks>
/// Visual Studio のデザイナ生成 (Strings.Designer.cs) は使わず手書きしている。
/// 生成ファイルはデザイナを開かないと更新されず、CI とローカルで差分が出やすいため。
/// キー名は i18n 化前に各ファイルへ集約されていた const 名をそのまま引き継いでいる。
/// </remarks>
internal static class Strings
{
    private static readonly ResourceManager Manager =
        new("SnapTack.Resources.Strings", typeof(Strings).Assembly);

    /// <summary>
    /// キーに対応する文字列を返す。キーが存在しない場合は例外を投げず、キー名自体を返す。
    /// 翻訳漏れでアプリを落とさないための保険 (画面には英字キーが出るので気付ける)。
    /// </summary>
    private static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    // ===== トレイメニュー (TrayIcon.cs) =====
    public static string MenuCaptureTextFormat => Get(nameof(MenuCaptureTextFormat));
    public static string MenuSettingsText => Get(nameof(MenuSettingsText));
    public static string MenuExitText => Get(nameof(MenuExitText));

    // ===== 付箋のコンテキストメニュー (ScrapWindow.xaml.cs) =====
    public static string MenuCopyText => Get(nameof(MenuCopyText));
    public static string MenuCopyGestureText => Get(nameof(MenuCopyGestureText));
    public static string MenuCloseText => Get(nameof(MenuCloseText));
    public static string MenuCloseGestureText => Get(nameof(MenuCloseGestureText));
    public static string MenuSavePngText => Get(nameof(MenuSavePngText));
    public static string MenuSavePngGestureText => Get(nameof(MenuSavePngGestureText));
    public static string MenuOpacityText => Get(nameof(MenuOpacityText));
    public static string MenuDiceText => Get(nameof(MenuDiceText));
    public static string MenuRestoreText => Get(nameof(MenuRestoreText));
    public static string MenuDiceGestureText => Get(nameof(MenuDiceGestureText));
    public static string SaveFileFilter => Get(nameof(SaveFileFilter));
    public static string ClipboardCopyFailedMessage => Get(nameof(ClipboardCopyFailedMessage));
    public static string SavePngFailedMessage => Get(nameof(SavePngFailedMessage));

    // ===== 設定画面 (SettingsWindow.xaml.cs) =====
    public static string WindowTitle => Get(nameof(WindowTitle));
    public static string HotkeyLabelText => Get(nameof(HotkeyLabelText));
    public static string HotkeyHintText => Get(nameof(HotkeyHintText));
    public static string SaveButtonText => Get(nameof(SaveButtonText));
    public static string CancelButtonText => Get(nameof(CancelButtonText));
    public static string ModifierRequiredMessage => Get(nameof(ModifierRequiredMessage));
    public static string LanguageLabelText => Get(nameof(LanguageLabelText));
    public static string LanguageAutoText => Get(nameof(LanguageAutoText));
    public static string LanguageEnglishText => Get(nameof(LanguageEnglishText));
    public static string LanguageJapaneseText => Get(nameof(LanguageJapaneseText));

    // ===== アプリ全体のエラー (App.xaml.cs) =====
    public static string HotkeyRegisterFailedFormat => Get(nameof(HotkeyRegisterFailedFormat));
    public static string CaptureFailedMessage => Get(nameof(CaptureFailedMessage));
    public static string SettingsSaveFailedMessage => Get(nameof(SettingsSaveFailedMessage));
}
