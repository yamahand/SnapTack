using System.Drawing;
using System.Windows.Forms;
using SnapTack.Resources;

namespace SnapTack;

/// <summary>
/// タスクトレイのアイコンとコンテキストメニュー。
/// アプリ本体への通知はイベントで行い、処理自体は持たない。
/// </summary>
public sealed class TrayIcon : IDisposable
{
    // 製品名は翻訳しないため const のまま
    private const string TooltipText = "SnapTack";

    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;

    /// <summary>メニューの「キャプチャ」またはアイコンのダブルクリックで発火する。</summary>
    public event EventHandler? CaptureRequested;

    /// <summary>メニューの「スクラップリスト」で発火する。</summary>
    public event EventHandler? ScrapListRequested;

    /// <summary>メニューの「設定」で発火する。</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>メニューの「終了」で発火する。</summary>
    public event EventHandler? ExitRequested;

    public TrayIcon(string hotkeyDisplayText)
    {
        _icon = LoadAppIcon();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = TooltipText,
            ContextMenuStrip = BuildMenu(hotkeyDisplayText),
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>現在の UI 言語でコンテキストメニューを構築する。</summary>
    private ContextMenuStrip BuildMenu(string hotkeyDisplayText)
    {
        var menu = new ContextMenuStrip();

        var captureItem = new ToolStripMenuItem(string.Format(Strings.MenuCaptureTextFormat, hotkeyDisplayText));
        captureItem.Click += (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(captureItem);

        var scrapListItem = new ToolStripMenuItem(Strings.MenuScrapListText);
        scrapListItem.Click += (_, _) => ScrapListRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(scrapListItem);

        var settingsItem = new ToolStripMenuItem(Strings.MenuSettingsText);
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settingsItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.MenuExitText, image: null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        return menu;
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

    /// <summary>
    /// メニューを現在の UI 言語・ホットキー表記で作り直す。
    /// 設定の保存後に呼ぶことで、言語変更が再起動なしで反映される。
    /// </summary>
    public void RebuildMenu(string hotkeyDisplayText)
    {
        var old = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = BuildMenu(hotkeyDisplayText);
        // 差し替え後に破棄する。表示中のメニューを破棄しないよう順序を守ること
        old?.Dispose();
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
