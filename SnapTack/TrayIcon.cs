using System.Drawing;
using System.Windows.Forms;

namespace SnapTack;

/// <summary>
/// タスクトレイのアイコンとコンテキストメニュー。
/// アプリ本体への通知はイベントで行い、処理自体は持たない。
/// </summary>
public sealed class TrayIcon : IDisposable
{
    // UI 文字列 (将来の英語化を見据えて集約)
    private const string TooltipText = "SnapTack";
    private const string MenuCaptureTextFormat = "キャプチャ(&C)\t{0}";
    private const string MenuSettingsText = "設定(&S)...";
    private const string MenuExitText = "終了(&X)";

    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _captureItem;

    /// <summary>メニューの「キャプチャ」またはアイコンのダブルクリックで発火する。</summary>
    public event EventHandler? CaptureRequested;

    /// <summary>メニューの「設定」で発火する。</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>メニューの「終了」で発火する。</summary>
    public event EventHandler? ExitRequested;

    public TrayIcon(string hotkeyDisplayText)
    {
        var menu = new ContextMenuStrip();

        _captureItem = new ToolStripMenuItem(string.Format(MenuCaptureTextFormat, hotkeyDisplayText));
        _captureItem.Click += (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(_captureItem);

        var settingsItem = new ToolStripMenuItem(MenuSettingsText);
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settingsItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(MenuExitText, image: null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            // アプリ固有のアイコンは M6 で作成する。それまでは標準アイコンで代用
            Icon = SystemIcons.Application,
            Text = TooltipText,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>ホットキー変更時にメニューの表記を更新する。</summary>
    public void UpdateHotkeyText(string hotkeyDisplayText)
    {
        _captureItem.Text = string.Format(MenuCaptureTextFormat, hotkeyDisplayText);
    }

    public void Dispose()
    {
        // Visible=false にしてから破棄しないとトレイに幽霊アイコンが残る
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}
