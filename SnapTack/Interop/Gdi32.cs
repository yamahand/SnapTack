using System.Runtime.InteropServices;

namespace SnapTack.Interop;

/// <summary>gdi32.dll の P/Invoke。</summary>
internal static class Gdi32
{
    /// <summary>GetHbitmap で取得した HBITMAP の解放に使う。</summary>
    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);
}
