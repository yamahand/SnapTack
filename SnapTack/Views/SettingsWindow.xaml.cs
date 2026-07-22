using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnapTack.Models;
using SnapTack.Resources;

namespace SnapTack.Views;

/// <summary>
/// 設定画面 (Fluent テーマ)。v1.0 の設定項目はホットキーの変更のみ (SPEC 4.5)。
/// 閉じた後に <see cref="Result"/> を参照する (保存時のみ非 null)。
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _current;
    private ModifierKeys _modifiers;
    private Key _key;

    /// <summary>保存された新しい設定。キャンセル時は null。</summary>
    public AppSettings? Result { get; private set; }

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        _current = current;

        Title = Strings.WindowTitle;
        HotkeyLabel.Text = Strings.HotkeyLabelText;
        HotkeyHint.Text = Strings.HotkeyHintText;
        LanguageLabel.Text = Strings.LanguageLabelText;
        SaveButton.Content = Strings.SaveButtonText;
        CancelButton.Content = Strings.CancelButtonText;

        InitializeLanguageBox(current.Language);

        _modifiers = current.HotkeyModifiers;
        _key = current.HotkeyKey;
        UpdateHotkeyBoxText();
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
        // Alt 併用時は SystemKey 側、IME 有効時は ImeProcessedKey 側に実キーが入る
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };

        // 修飾キーなしの Enter / Esc / Tab は取り込まず、通常のダイアログ操作
        // (保存 / キャンセル / フォーカス移動) として既定処理に流す
        if (Keyboard.Modifiers == ModifierKeys.None && key is Key.Enter or Key.Escape or Key.Tab)
        {
            return;
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
            return;
        }

        _modifiers = Keyboard.Modifiers;
        _key = key;
        UpdateHotkeyBoxText();
    }

    private void OnHotkeyBoxPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        // IME 経由の文字入力もブロックする
        e.Handled = true;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // 修飾キーなしの登録は通常のキー入力を乗っ取ってしまうため許可しない
        if (_modifiers == ModifierKeys.None)
        {
            MessageBox.Show(this, Strings.ModifierRequiredMessage, Strings.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 画面にない設定項目 (LastSaveDirectory 等) を落とさないよう、現在値のコピーへ上書きする
        var result = _current.Clone();
        result.HotkeyModifiers = _modifiers;
        result.HotkeyKey = _key;
        result.Language = SelectedLanguage;
        Result = result;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        // モードレス表示 (Show) では IsCancel だけでは閉じないため明示的に Close する
        Close();
    }

    private void UpdateHotkeyBoxText()
    {
        HotkeyBox.Text = AppSettings.FormatHotkey(_modifiers, _key);
    }
}
