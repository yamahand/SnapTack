namespace SnapTack.Models;

/// <summary>
/// 付箋の不透明度の刻みを計算する (SPEC-v1.x 2.2)。
/// UI に依存しない純粋計算のため、ここへ切り出してテスト可能にしている。
/// </summary>
public static class OpacityLevel
{
    public const int MinPercent = 20;
    public const int MaxPercent = 100;
    public const int StepPercent = 10;

    /// <summary>
    /// ホイール 1 ステップ後の不透明度を返す。
    /// 現在値が 10% の倍数のときは ±10%。倍数でない (プリセット 75% / 25% 適用後) ときは
    /// 直近の倍数へスナップする (75% / 25% の場合は結果的に ±5% になる、SPEC-v1.x 2.2)。
    /// </summary>
    public static int Next(int current, int direction)
    {
        int remainder = current % StepPercent;
        int next = remainder == 0
            ? current + direction * StepPercent
            : direction > 0 ? current + (StepPercent - remainder) : current - remainder;
        return Math.Clamp(next, MinPercent, MaxPercent);
    }
}
