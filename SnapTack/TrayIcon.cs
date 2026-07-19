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
    private readonly Icon _icon;

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

        _icon = LoadAppIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = TooltipText,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>WPF リソースの .ico からトレイ用サイズ (DPI 追従) のアイコンを読み込む。</summary>
    private static Icon LoadAppIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/SnapTack.ico"))
            ?? throw new InvalidOperationException("アプリアイコンのリソースが見つかりません。");
        using var stream = resource.Stream;
        return new Icon(stream, SystemInformation.SmallIconSize);
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
        _icon.Dispose();
    }
}
