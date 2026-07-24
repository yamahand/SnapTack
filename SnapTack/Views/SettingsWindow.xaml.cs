using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapTack.Models;
using SnapTack.Resources;

namespace SnapTack.Views;

/// <summary>
/// 設定画面 (Fluent テーマ)。v1.5 でスクラップリスト関連の項目を追加した (SPEC-v1.5 2.5)。
/// 閉じた後に <see cref="Result"/> を参照する (保存時のみ非 null)。
/// </summary>
public partial class SettingsWindow : Window
{
    // 数値入力欄は 0-9 のみ受け付ける (符号・小数を弾く)
    private static readonly Regex NonDigit = new("[^0-9]", RegexOptions.Compiled);

    private readonly AppSettings _current;
    private ModifierKeys _modifiers;
    private Key _key;
    private ModifierKeys _scrapListModifiers;
    private Key _scrapListKey;

    /// <summary>保存された新しい設定。キャンセル時は null。</summary>
    public AppSettings? Result { get; private set; }

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        _current = current;

        Title = Strings.WindowTitle;
        HotkeyLabel.Text = Strings.HotkeyLabelText;
        HotkeyHint.Text = Strings.HotkeyHintText;
        ScrapListHotkeyLabel.Text = Strings.ScrapListHotkeyLabelText;
        MaxScrapsLabel.Text = Strings.MaxScrapsLabelText;
        MaxTrashedScrapsLabel.Text = Strings.MaxTrashedScrapsLabelText;
        TrashRetentionLabel.Text = Strings.TrashRetentionLabelText;
        RestoreOnStartupCheck.Content = Strings.RestoreOnStartupText;
        LanguageLabel.Text = Strings.LanguageLabelText;
        SaveButton.Content = Strings.SaveButtonText;
        CancelButton.Content = Strings.CancelButtonText;

        InitializeLanguageBox(current.Language);

        _modifiers = current.HotkeyModifiers;
        _key = current.HotkeyKey;
        UpdateHotkeyBoxText();

        _scrapListModifiers = current.ScrapListHotkeyModifiers;
        _scrapListKey = current.ScrapListHotkeyKey;
        UpdateScrapListHotkeyBoxText();

        // 数値は現在の UI カルチャで整形する (桁区切りは付けない)
        MaxScrapsBox.Text = current.MaxScraps.ToString(CultureInfo.CurrentCulture);
        MaxTrashedScrapsBox.Text = current.MaxTrashedScraps.ToString(CultureInfo.CurrentCulture);
        TrashRetentionBox.Text = current.TrashRetentionDays.ToString(CultureInfo.CurrentCulture);
        RestoreOnStartupCheck.IsChecked = current.RestoreScrapsOnStartup;
    }

    /// <summary>言語コンボボックスに選択肢を並べ、現在の設定を選択状態にする。</summary>
    private void InitializeLanguageBox(AppLanguage current)
    {
        // 言語名 (English / 日本語) は自言語表記のままにするため、リソース側でも翻訳していない
        (AppLanguage Value, string Text)[] items =
        [
            (AppLanguage.Auto, Strings.LanguageAutoText),
            (AppLanguage.English, Strings.LanguageEnglishText),
            (AppLanguage.Japanese, Strings.LanguageJapaneseText),
        ];

        foreach (var (value, text) in items)
        {
            var item = new ComboBoxItem { Content = text, Tag = value };
            LanguageBox.Items.Add(item);
            if (value == current)
            {
                LanguageBox.SelectedItem = item;
            }
        }

        // 設定ファイルが未知の値を持っていた場合に無選択のままにしない
        LanguageBox.SelectedIndex = LanguageBox.SelectedIndex < 0 ? 0 : LanguageBox.SelectedIndex;
    }

    /// <summary>コンボボックスで選択中の言語を返す。</summary>
    private AppLanguage SelectedLanguage =>
        LanguageBox.SelectedItem is ComboBoxItem { Tag: AppLanguage language } ? language : AppLanguage.Auto;

    private void OnHotkeyBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (TryCaptureHotkey(e, out var modifiers, out var key))
        {
            _modifiers = modifiers;
            _key = key;
            UpdateHotkeyBoxText();
        }
    }

    private void OnScrapListHotkeyBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (TryCaptureHotkey(e, out var modifiers, out var key))
        {
            _scrapListModifiers = modifiers;
            _scrapListKey = key;
            UpdateScrapListHotkeyBoxText();
        }
    }

    /// <summary>
    /// ホットキー入力欄のキー押下を解釈する。本体キーが確定したときだけ true を返す。
    /// キャプチャ用とスクラップリスト用の 2 つの入力欄で共有する。
    /// </summary>
    private bool TryCaptureHotkey(KeyEventArgs e, out ModifierKeys modifiers, out Key key)
    {
        modifiers = ModifierKeys.None;

        // Alt 併用時は SystemKey 側、IME 有効時は ImeProcessedKey 側に実キーが入る
        key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };

        // 修飾キーなしの Enter / Esc / Tab は取り込まず、通常のダイアログ操作
        // (保存 / キャンセル / フォーカス移動) として既定処理に流す
        if (Keyboard.Modifiers == ModifierKeys.None && key is Key.Enter or Key.Escape or Key.Tab)
        {
            return false;
        }

        // 上記以外はホットキーの取り込み専用にするため、既定のキー処理は止める
        e.Handled = true;

        // 修飾キー単体は無視して、本体キーが押されるのを待つ
        if (key is Key.None or Key.System
            or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin)
        {
            return false;
        }

        modifiers = Keyboard.Modifiers;
        return true;
    }

    private void OnHotkeyBoxPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        // IME 経由の文字入力もブロックする
        e.Handled = true;
    }

    private void OnNumberBoxPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        // 数字以外の入力を弾く (貼り付けは PreviewTextInput を通らないため保存時にも検証する)
        e.Handled = NonDigit.IsMatch(e.Text);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // 修飾キーなしの登録は通常のキー入力を乗っ取ってしまうため許可しない
        // (キャプチャ用・スクラップリスト用のどちらも修飾必須)
        if (_modifiers == ModifierKeys.None || _scrapListModifiers == ModifierKeys.None)
        {
            MessageBox.Show(this, Strings.ModifierRequiredMessage, Strings.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 2 つのホットキーが同一だと片方の RegisterHotkey が失敗し、汎用の登録失敗警告しか
        // 出ずに原因が分かりにくい。保存時にここで弾いて専用メッセージで知らせる
        if (_modifiers == _scrapListModifiers && _key == _scrapListKey)
        {
            MessageBox.Show(this, Strings.HotkeyConflictMessage, Strings.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 画面にない設定項目 (LastSaveDirectory 等) を落とさないよう、現在値のコピーへ上書きする
        var result = _current.Clone();
        result.HotkeyModifiers = _modifiers;
        result.HotkeyKey = _key;
        result.ScrapListHotkeyModifiers = _scrapListModifiers;
        result.ScrapListHotkeyKey = _scrapListKey;
        result.Language = SelectedLanguage;

        // 数値欄は空・非数字でも落ちないよう既定値へフォールバックし、下限でクランプする。
        // 上限を 0 にすると全消えするため、保持数は最低 1 を保証する。
        // なお上限を下げても既存の超過分はここでは削除しない。トリミングは ScrapManager が
        // スクラップ操作時 (追加・状態変更) に行うため、次に 1 枚追加した時点で自然に収束する。
        // 設定変更だけでユーザーが貼った付箋を消さないための意図的な挙動 (SPEC-v1.5 2.4)
        result.MaxScraps = Math.Max(1, ParseOrDefault(MaxScrapsBox.Text, _current.MaxScraps));
        result.MaxTrashedScraps = Math.Max(0, ParseOrDefault(MaxTrashedScrapsBox.Text, _current.MaxTrashedScraps));
        result.TrashRetentionDays = Math.Max(0, ParseOrDefault(TrashRetentionBox.Text, _current.TrashRetentionDays));
        result.RestoreScrapsOnStartup = RestoreOnStartupCheck.IsChecked == true;

        Result = result;
        Close();
    }

    /// <summary>数値欄のテキストを整数に変換する。空・非数字なら現在値を返す。</summary>
    private static int ParseOrDefault(string text, int fallback) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value) ? value : fallback;

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        // モードレス表示 (Show) では IsCancel だけでは閉じないため明示的に Close する
        Close();
    }

    private void UpdateHotkeyBoxText()
    {
        HotkeyBox.Text = AppSettings.FormatHotkey(_modifiers, _key);
    }

    private void UpdateScrapListHotkeyBoxText()
    {
        ScrapListHotkeyBox.Text = AppSettings.FormatHotkey(_scrapListModifiers, _scrapListKey);
    }
}
