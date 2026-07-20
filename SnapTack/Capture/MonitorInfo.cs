using System.Windows;

namespace SnapTack.Capture;

/// <summary>
/// 1つのモニタの情報。
/// </summary>
/// <param name="DeviceName">デバイス名 (例: \\.\DISPLAY1)。</param>
/// <param name="PhysicalBounds">仮想スクリーン座標での物理ピクセル矩形。プライマリ左上が (0,0)。</param>
/// <param name="IsPrimary">プライマリモニタかどうか。</param>
public sealed record MonitorInfo(string DeviceName, Int32Rect PhysicalBounds, bool IsPrimary);
