using System.Runtime.InteropServices;

namespace SnapTack.Interop;

/// <summary>user32.dll の P/Invoke。</summary>
internal static class User32
{
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>
    /// ウィンドウを物理ピクセル座標で配置する。
    /// 混在 DPI 環境では WPF の Left/Top (DIP) 経由の配置が不正確になるため、こちらを使う。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
