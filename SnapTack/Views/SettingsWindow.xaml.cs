using System.Windows;
using System.Windows.Input;
using SnapTack.Models;

namespace SnapTack.Views;

/// <summary>
/// 設定画面 (Fluent テーマ)。v1.0 の設定項目はホットキーの変更のみ (SPEC 4.5)。
/// 閉じた後に <see cref="Result"/> を参照する (保存時のみ非 null)。
/// </summary>
public partial class SettingsWindow : Window
{
    // UI 文字列 (将来の英語化を見据えて集約)
    private const string WindowTitle = "SnapTack 設定";
    private const string HotkeyLabelText = "キャプチャホットキー";
    private const string HotkeyHintText = "ボックスを選択してキーを押すと変更されます。修飾キー (Ctrl / Shift / Alt / Win) を1つ以上含めてください。";
    private const string SaveButtonText = "保存";
    private const string CancelButtonText = "キャンセル";
    private const string ModifierRequiredMessage = "修飾キー (Ctrl / Shift / Alt / Win) を1つ以上含めてください。";

    private ModifierKeys _modifiers;
    private Key _key;

    /// <summary>保存された新しい設定。キャンセル時は null。</summary>
    public AppSettings? Result { get; private set; }

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();

        Title = WindowTitle;
        HotkeyLabel.Text = HotkeyLabelText;
        HotkeyHint.Text = HotkeyHintText;
        SaveButton.Content = SaveButtonText;
        CancelButton.Content = CancelButtonText;

        _modifiers = current.HotkeyModifiers;
        _key = current.HotkeyKey;
        UpdateHotkeyBoxText();
    }

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
            MessageBox.Show(this, ModifierRequiredMessage, WindowTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new AppSettings
        {
            HotkeyModifiers = _modifiers,
            HotkeyKey = _key,
        };
        Close();
    }

    private void UpdateHotkeyBoxText()
    {
        HotkeyBox.Text = AppSettings.FormatHotkey(_modifiers, _key);
    }
}
