using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using SnapTack.Capture;
using SnapTack.Interop;
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
    private const string HotkeyRegisterFailedMessage =
        "ホットキー (Ctrl+Shift+Z) の登録に失敗しました。\n" +
        "他のアプリと競合している可能性があります。\n" +
        "トレイメニューからのキャプチャは引き続き使用できます。";
    private const string CaptureFailedMessage = "画面のキャプチャに失敗しました。";

    // 二重起動防止用の Mutex 名 (同一ユーザーセッション内で一意)
    private const string MutexName = "SnapTack_SingleInstanceMutex";

    // デフォルトホットキー: Ctrl+Shift+Z (SPEC 4.2)。M5 で設定から変更可能にする
    private const ModifierKeys DefaultHotkeyModifiers = ModifierKeys.Control | ModifierKeys.Shift;
    private const Key DefaultHotkeyKey = Key.Z;

    private readonly IScreenCapturer _screenCapturer = new GdiScreenCapturer();

    private Mutex? _mutex;
    private TrayIcon? _trayIcon;
    private GlobalHotkey? _hotkey;
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

        _trayIcon = new TrayIcon();
        _trayIcon.CaptureRequested += OnCaptureRequested;
        _trayIcon.ExitRequested += (_, _) => Shutdown();

        _hotkey = new GlobalHotkey();
        _hotkey.Pressed += OnCaptureRequested;
        if (!_hotkey.Register(DefaultHotkeyModifiers, DefaultHotkeyKey))
        {
            // 登録失敗はトレイメニューから操作できるため警告のみで継続する (SPEC 4.2)
            MessageBox.Show(HotkeyRegisterFailedMessage, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
            new ScrapWindow(image, overlay.ResultPhysicalRect).Show();
        }
    }
}
