using SnapTack.Models;
using Xunit;

namespace SnapTack.Tests;

/// <summary>不透明度の刻み (SPEC-v1.x 2.2) の検証。</summary>
public class OpacityLevelTests
{
    private const int Down = -1;
    private const int Up = 1;

    [Fact]
    public void 下げると10刻みで下がる()
    {
        Assert.Equal(90, OpacityLevel.Next(100, Down));
    }

    [Fact]
    public void 上げると10刻みで上がる()
    {
        Assert.Equal(60, OpacityLevel.Next(50, Up));
    }

    [Fact]
    public void 下限20より下がらない()
    {
        Assert.Equal(20, OpacityLevel.Next(20, Down));
    }

    [Fact]
    public void 上限100より上がらない()
    {
        Assert.Equal(100, OpacityLevel.Next(100, Up));
    }

    [Fact]
    public void 下限直上から下げると下限にクランプされる()
    {
        Assert.Equal(20, OpacityLevel.Next(25, Down));
    }

    [Fact]
    public void プリセット75から下げると70へスナップする()
    {
        // 倍数でない値は直近の倍数へスナップするため、結果的に -5 になる
        Assert.Equal(70, OpacityLevel.Next(75, Down));
    }

    [Fact]
    public void プリセット25から上げると30へスナップする()
    {
        Assert.Equal(30, OpacityLevel.Next(25, Up));
    }

    [Fact]
    public void プリセット75から上げると80へスナップする()
    {
        Assert.Equal(80, OpacityLevel.Next(75, Up));
    }

    [Fact]
    public void 複数ノッチは1ステップずつ適用される()
    {
        // OnMouseWheel と同じく 1 ステップずつ進める。75 → 70 → 60 → 50
        int percent = 75;
        for (int i = 0; i < 3; i++)
        {
            percent = OpacityLevel.Next(percent, Down);
        }
        Assert.Equal(50, percent);
    }

    [Fact]
    public void 複数ノッチで下限を越えてもクランプされる()
    {
        int percent = 100;
        for (int i = 0; i < 20; i++)
        {
            percent = OpacityLevel.Next(percent, Down);
        }
        Assert.Equal(20, percent);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(75)]
    [InlineData(50)]
    [InlineData(25)]
    [InlineData(20)]
    public void 結果は常に範囲内に収まる(int start)
    {
        foreach (int direction in new[] { Down, Up })
        {
            int result = OpacityLevel.Next(start, direction);
            Assert.InRange(result, OpacityLevel.MinPercent, OpacityLevel.MaxPercent);
        }
    }
}
