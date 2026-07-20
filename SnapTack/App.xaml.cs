using System.Runtime.InteropServices;
using System.Windows;
using SnapTack.Capture;
using SnapTack.Interop;
using SnapTack.Models;
using SnapTack.Views;

namespace SnapTack;

/// <summary>
/// アプリケーションのエントリポイント。
/// ウィンドウは表示せず、タスクトレイに常駐する (ShutdownMode=OnExplicitShutdown)。
/// </summary>
public partial class App : Application
{
    // UI 文字列 (将来の英語化を見据えて集約)
    private const string AppName = "SnapTack";
    private const string HotkeyRegisterFailedFormat =
        "ホットキー ({0}) の登録に失敗しました。\n" +
        "他のアプリと競合している可能性があります。\n" +
        "トレイメニューからのキャプチャは引き続き使用できます。";
    private const string CaptureFailedMessage = "画面のキャプチャに失敗しました。";
    private const string SettingsSaveFailedMessage = "設定ファイルの保存に失敗しました。設定は次回起動時に元に戻ります。";

    // 二重起動防止用の Mutex 名 (同一ユーザーセッション内で一意)
    private const string MutexName = "SnapTack_SingleInstanceMutex";

    private readonly IScreenCapturer _screenCapturer = new GdiScreenCapturer();
    private readonly SettingsService _settings = new(new SettingsStore());

    private Mutex? _mutex;
    private TrayIcon? _trayIcon;
    private GlobalHotkey? _hotkey;
    private SettingsWindow? _settingsWindow;
    private bool _capturing;

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

        _trayIcon = new TrayIcon(_settings.Current.GetHotkeyDisplayText());
        _trayIcon.CaptureRequested += OnCaptureRequested;
        _trayIcon.SettingsRequested += OnSettingsRequested;
        _trayIcon.ExitRequested += (_, _) => Shutdown();

        _hotkey = new GlobalHotkey();
        _hotkey.Pressed += OnCaptureRequested;
        RegisterHotkeyOrWarn();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _hotkey = null;

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

    /// <summary>現在の設定でホットキーを登録し、失敗したら警告して継続する (SPEC 4.2)。</summary>
    private void RegisterHotkeyOrWarn()
    {
        if (_hotkey is null)
        {
            return;
        }
        if (!_hotkey.Register(_settings.Current.HotkeyModifiers, _settings.Current.HotkeyKey))
        {
            MessageBox.Show(
                string.Format(HotkeyRegisterFailedFormat, _settings.Current.GetHotkeyDisplayText()),
                AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCaptureRequested(object? sender, EventArgs e)
    {
        // オーバーレイ表示中の再キャプチャ要求は無視する (SPEC 4.3)
        if (_capturing)
        {
            return;
        }
        _capturing = true;
        try
        {
            StartCapture();
        }
        finally
        {
            _capturing = false;
        }
    }

    private void StartCapture()
    {
        // ホットキー押下の瞬間に画面全体をフリーズさせる (SPEC 4.2)
        System.Windows.Media.Imaging.BitmapSource screenshot;
        try
        {
            screenshot = _screenCapturer.CapturePrimaryScreen();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or ExternalException)
        {
            MessageBox.Show(CaptureFailedMessage, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var overlay = new OverlayWindow(screenshot);
        overlay.ShowDialog();
        if (overlay.ResultImage is { } image)
        {
            // 付箋は自己完結させ、App 側で参照は保持しない (全部閉じても OnExplicitShutdown なので継続する)
            new ScrapWindow(image, overlay.ResultPhysicalRect, _settings).Show();
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
                    MessageBox.Show(SettingsSaveFailedMessage, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                _trayIcon?.UpdateHotkeyText(_settings.Current.GetHotkeyDisplayText());
            }
            // 保存・キャンセルどちらでも、現在の設定で即時再登録する
            RegisterHotkeyOrWarn();
        };
        window.Show();
    }
}
