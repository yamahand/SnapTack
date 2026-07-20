using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace SnapTack.Interop;

/// <summary>
/// RegisterHotKey (Win32) によるグローバルホットキーの登録と WM_HOTKEY の受信。
/// 受信用にメッセージ専用ウィンドウ (HWND_MESSAGE 子) を1つ作成して保持する。
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 1;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private bool _registered;
    private bool _disposed;

    /// <summary>登録中のホットキーが押されたときに UI スレッドで発火する。</summary>
    public event EventHandler? Pressed;

    public GlobalHotkey()
    {
        var parameters = new HwndSourceParameters("SnapTack.HotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = HWND_MESSAGE,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>
    /// ホットキーを登録する。登録済みの場合は差し替える。
    /// 他アプリとの競合などで失敗した場合は false を返す(例外は投げない)。
    /// </summary>
    public bool Register(ModifierKeys modifiers, Key key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Unregister();

        // 設定ファイルの破損・手編集などで無効な組み合わせが流入しても登録しない
        // (修飾キーなしや修飾キー単体の登録は、通常のキー入力をグローバルに乗っ取ってしまう)
        if (modifiers == ModifierKeys.None || !IsValidHotkeyKey(key))
        {
            return false;
        }

        // キーを押しっぱなしにしても連続発火させない
        uint fsModifiers = MOD_NOREPEAT;
        if (modifiers.HasFlag(ModifierKeys.Alt)) fsModifiers |= MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Control)) fsModifiers |= MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift)) fsModifiers |= MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) fsModifiers |= MOD_WIN;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
        {
            return false;
        }
        _registered = RegisterHotKey(_source.Handle, HotkeyId, fsModifiers, vk);
        return _registered;
    }

    /// <summary>ホットキーの本体キーとして有効か (修飾キー・特殊マーカーは不可)。</summary>
    private static bool IsValidHotkeyKey(Key key) =>
        key is not (Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed
            or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin);

    /// <summary>登録を解除する。未登録なら何もしない。</summary>
    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
