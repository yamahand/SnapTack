using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using SnapTack.Capture;
using SnapTack.Interop;
using SnapTack.Models;
using SnapTack.Resources;
using SnapTack.Views;

namespace SnapTack;

/// <summary>
/// アプリケーションのエントリポイント。
/// ウィンドウは表示せず、タスクトレイに常駐する (ShutdownMode=OnExplicitShutdown)。
/// </summary>
public partial class App : Application
{
    // 製品名は翻訳しない。翻訳対象の文字列は Resources/Strings.resx を参照
    private const string AppName = "SnapTack";

    // 二重起動防止用の Mutex 名 (同一ユーザーセッション内で一意)
    private const string MutexName = "SnapTack_SingleInstanceMutex";

    private readonly SettingsService _settings = new(new SettingsStore());
    private readonly CaptureController _capture = new(new GdiScreenCapturer());
    private ScrapManager? _scraps;

    /// <summary>
    /// アプリ終了処理中かどうか。付箋ウィンドウは終了時の閉じをゴミ箱行きへ委譲せず
    /// そのまま閉じる必要があるため参照する (SPEC-v1.5 2.6。終了 = 破棄ではなく状態保存)。
    /// </summary>
    public static bool IsShuttingDown { get; private set; }

    private Mutex? _mutex;
    private TrayIcon? _trayIcon;
    private GlobalHotkey? _hotkey;          // キャプチャ用
    private GlobalHotkey? _scrapListHotkey; // スクラップリスト用 (M15)
    private SettingsWindow? _settingsWindow;
    private ScrapListWindow? _scrapListWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 二重起動は何も表示せず即終了する (SPEC 4.1)
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // UI を作る前に言語を確定させる。トレイメニューは生成時に文字列が確定するため順序が重要
        LanguageService.Apply(_settings.Current.Language);

        // 付箋の生成・管理は ScrapManager に集約する (SPEC-v1.5 3.1)。
        // 全部閉じても OnExplicitShutdown なので常駐は継続する
        _scraps = new ScrapManager(_settings, new ScrapStore());
        _capture.SelectionCompleted += (image, physicalRect) => _scraps.Add(image, physicalRect);
        // 保存済みスクラップを復元する (Pinned を画面へ、期限切れゴミ箱を掃除)。SPEC-v1.5 2.4
        _scraps.RestoreFromDisk();

        _trayIcon = new TrayIcon(_settings.Current.GetHotkeyDisplayText());
        _trayIcon.CaptureRequested += OnCaptureRequested;
        _trayIcon.ScrapListRequested += OnScrapListRequested;
        _trayIcon.SettingsRequested += OnSettingsRequested;
        _trayIcon.ExitRequested += (_, _) => ShutdownApp();

        // OS のログオフ・シャットダウンでも終了中フラグを立てておく (付箋が閉じをブロックしないよう)
        SessionEnding += (_, _) => IsShuttingDown = true;

        // キャプチャ用とスクラップリスト用の 2 つのホットキーを同時登録する (M15)
        _hotkey = new GlobalHotkey();
        _hotkey.Pressed += OnCaptureRequested;
        _scrapListHotkey = new GlobalHotkey();
        _scrapListHotkey.Pressed += OnScrapListRequested;
        RegisterHotkeysOrWarn();
    }

    /// <summary>終了中フラグを立ててからシャットダウンする。付箋の閉じがゴミ箱行きへ委譲されないようにする。</summary>
    private void ShutdownApp()
    {
        IsShuttingDown = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 表示中だった付箋の最終位置などを保存する。各ウィンドウは閉じる際に Item へ
        // 書き戻し済みなので、ここでは index を確定させる (SPEC-v1.5 2.6)
        _scraps?.SaveAll();

        _hotkey?.Dispose();
        _hotkey = null;

        _scrapListHotkey?.Dispose();
        _scrapListHotkey = null;

        _trayIcon?.Dispose();
        _trayIcon = null;

        if (_mutex is not null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            _mutex = null;
        }

        base.OnExit(e);
    }

    /// <summary>現在の設定で 2 つのホットキーを登録し、失敗したものだけ警告して継続する (SPEC 4.2)。</summary>
    private void RegisterHotkeysOrWarn()
    {
        // 失敗時の案内はホットキーの用途ごとに変える (キャプチャ / スクラップリスト)
        RegisterHotkeyOrWarn(_hotkey, _settings.Current.HotkeyModifiers, _settings.Current.HotkeyKey,
            _settings.Current.GetHotkeyDisplayText(), Strings.HotkeyFallbackCaptureText);
        RegisterHotkeyOrWarn(_scrapListHotkey,
            _settings.Current.ScrapListHotkeyModifiers, _settings.Current.ScrapListHotkeyKey,
            AppSettings.FormatHotkey(_settings.Current.ScrapListHotkeyModifiers, _settings.Current.ScrapListHotkeyKey),
            Strings.HotkeyFallbackScrapListText);
    }

    /// <summary>指定ホットキーを登録し、失敗したら用途に応じた案内で警告して継続する。</summary>
    private static void RegisterHotkeyOrWarn(GlobalHotkey? hotkey, ModifierKeys modifiers, Key key,
        string displayText, string fallbackText)
    {
        if (hotkey is null)
        {
            return;
        }
        if (!hotkey.Register(modifiers, key))
        {
            MessageBox.Show(
                string.Format(Strings.HotkeyRegisterFailedFormat, displayText, fallbackText),
                AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCaptureRequested(object? sender, EventArgs e)
    {
        // オーバーレイ表示中の再要求は CaptureController 側で無視される (SPEC 4.3)
        try
        {
            _capture.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or ExternalException)
        {
            MessageBox.Show(Strings.CaptureFailedMessage, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        // 設定中は現在のホットキーを解除し、同じ組み合わせも入力欄で押せるようにする
        _hotkey?.Unregister();

        var window = new SettingsWindow(_settings.Current);
        _settingsWindow = window;
        window.Closed += (_, _) =>
        {
            _settingsWindow = null;
            if (window.Result is { } newSettings)
            {
                if (!_settings.Replace(newSettings))
                {
                    // 永続化に失敗しても、このセッション中は新しい設定で動かす。
                    // 言語の反映も含めて先に通知しておく (メッセージ自体は変更前の言語で出る)
                    MessageBox.Show(Strings.SettingsSaveFailedMessage, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                // 言語を反映してからメニューを作り直す。既存の付箋は生成時の言語で確定するため
                // 表示中のものには反映しない (ScrapManager で中央管理後もこの挙動は維持。SPEC-v1.5 3.1)
                LanguageService.Apply(_settings.Current.Language);
                _trayIcon?.RebuildMenu(_settings.Current.GetHotkeyDisplayText());
            }
            // 保存・キャンセルどちらでも、現在の設定で即時再登録する
            RegisterHotkeysOrWarn();
        };
        window.Show();
    }

    /// <summary>スクラップリストを開く。既に開いていれば新規生成せずアクティブ化する (SPEC-v1.5 2.2)。</summary>
    private void OnScrapListRequested(object? sender, EventArgs e)
    {
        if (_scrapListWindow is not null)
        {
            _scrapListWindow.Activate();
            return;
        }
        if (_scraps is null)
        {
            return;
        }
        // 長時間起動しっぱなしでもゴミ箱が肥大しないよう、開くたびに期限切れを掃除する (SPEC-v1.5 6)
        _scraps.OnScrapListOpened();

        var window = new ScrapListWindow(_scraps, _settings);
        _scrapListWindow = window;
        window.Closed += (_, _) => _scrapListWindow = null;
        window.Show();
    }
}
