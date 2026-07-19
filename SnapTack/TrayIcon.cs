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
    private const string MenuCaptureText = "キャプチャ(&C)\tCtrl+Shift+Z";
    private const string MenuSettingsText = "設定(&S)...";
    private const string MenuExitText = "終了(&X)";

    private readonly NotifyIcon _notifyIcon;

    /// <summary>メニューの「キャプチャ」またはアイコンのダブルクリックで発火する。</summary>
    public event EventHandler? CaptureRequested;

    /// <summary>メニューの「終了」で発火する。</summary>
    public event EventHandler? ExitRequested;

    public TrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(MenuCaptureText, image: null, (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty));

        // 設定画面は M5 で実装するため仮・無効 (MILESTONES M1)
        var settingsItem = new ToolStripMenuItem(MenuSettingsText) { Enabled = false };
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

    public void Dispose()
    {
        // Visible=false にしてから破棄しないとトレイに幽霊アイコンが残る
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}
