using System.Windows.Media.Imaging;

namespace SnapTack.Capture;

/// <summary>
/// 画面キャプチャの抽象化。
/// 初期実装は GDI (CopyFromScreen)。将来 Windows.Graphics.Capture へ差し替え可能にする。
/// </summary>
public interface IScreenCapturer
{
    /// <summary>
    /// プライマリモニタ全体を物理ピクセル解像度でキャプチャし、Freeze 済みの画像を返す。
    /// 失敗時は例外を投げる(呼び出し側で通知して継続する)。
    /// </summary>
    BitmapSource CapturePrimaryScreen();
}
