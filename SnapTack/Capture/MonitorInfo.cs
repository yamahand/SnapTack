using System.Windows;

namespace SnapTack.Capture;

/// <summary>
/// 1つのモニタの情報。
/// </summary>
/// <param name="DeviceName">デバイス名 (例: \\.\DISPLAY1)。</param>
/// <param name="PhysicalBounds">
/// 仮想スクリーン座標での物理ピクセル矩形。原点 (0,0) はプライマリモニタの左上で、
/// プライマリの左・上に別モニタが配置されている場合、座標は負になり得る。
/// </param>
/// <param name="IsPrimary">プライマリモニタかどうか。</param>
public sealed record MonitorInfo(string DeviceName, Int32Rect PhysicalBounds, bool IsPrimary);
