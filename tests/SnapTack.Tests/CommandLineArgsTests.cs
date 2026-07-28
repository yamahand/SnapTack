using System.Windows;
using SnapTack.Models;
using Xunit;

namespace SnapTack.Tests;

/// <summary>コマンドライン引数の解析 (SPEC-v1.8 2.2 / 2.4 / 2.5) の検証。</summary>
public class CommandLineArgsTests
{
    [Fact]
    public void 引数なしは空になる()
    {
        var args = CommandLineArgs.Parse([]);

        Assert.True(args.IsEmpty);
        Assert.Empty(args.ImagePaths);
        Assert.Equal(CommandLineAction.None, args.Action);
        Assert.Null(args.CaptureRect);
    }

    [Fact]
    public void オプション以外は画像パスとして扱われる()
    {
        var args = CommandLineArgs.Parse([@"C:\pic.jpg", @"D:\shot.png"]);

        Assert.Equal([@"C:\pic.jpg", @"D:\shot.png"], args.ImagePaths);
        Assert.False(args.IsEmpty);
    }

    [Fact]
    public void 画像パスは引数の並び順を保つ()
    {
        var args = CommandLineArgs.Parse([@"C:\b.png", @"C:\a.png", @"C:\c.png"]);

        Assert.Equal([@"C:\b.png", @"C:\a.png", @"C:\c.png"], args.ImagePaths);
    }

    [Theory]
    [InlineData("/C:Capture")]
    [InlineData("/c:capture")]
    [InlineData("/C:CAPTURE")]
    public void Captureオプションは大文字小文字を区別しない(string arg)
    {
        var args = CommandLineArgs.Parse([arg]);

        Assert.Equal(CommandLineAction.Capture, args.Action);
    }

    [Theory]
    [InlineData("/C:Option")]
    [InlineData("/c:option")]
    public void Optionオプションは大文字小文字を区別しない(string arg)
    {
        var args = CommandLineArgs.Parse([arg]);

        Assert.Equal(CommandLineAction.Option, args.Action);
    }

    [Fact]
    public void 未知のオプションは無視される()
    {
        // エラーにせず黙って捨てる。無人実行を止めないため (SPEC-v1.8 2.2)
        var args = CommandLineArgs.Parse(["/Unknown", "/C:Nonsense", "/X:1"]);

        Assert.True(args.IsEmpty);
    }

    [Fact]
    public void 未知のオプションが混ざっても他の引数は生きる()
    {
        var args = CommandLineArgs.Parse(["/Unknown", @"C:\pic.png", "/C:Option"]);

        Assert.Equal([@"C:\pic.png"], args.ImagePaths);
        Assert.Equal(CommandLineAction.Option, args.Action);
    }

    [Fact]
    public void 動作オプションが複数あれば最後が勝つ()
    {
        // 同時に 2 つの画面は開けないため
        var args = CommandLineArgs.Parse(["/C:Capture", "/C:Option"]);

        Assert.Equal(CommandLineAction.Option, args.Action);
    }

    [Fact]
    public void 矩形オプションを解釈できる()
    {
        var args = CommandLineArgs.Parse(["/R:100,200,640,480"]);

        Assert.Equal(new Int32Rect(100, 200, 640, 480), args.CaptureRect);
    }

    [Fact]
    public void 矩形オプションの座標は負値を許す()
    {
        // セカンダリモニタがプライマリの左・上にあると負座標になる (SPEC-v1.8 2.5)
        var args = CommandLineArgs.Parse(["/R:-1920,-100,800,600"]);

        Assert.Equal(new Int32Rect(-1920, -100, 800, 600), args.CaptureRect);
    }

    [Theory]
    [InlineData("/R:100,200,0,480")]    // 幅が 0
    [InlineData("/R:100,200,640,-1")]   // 高さが負
    [InlineData("/R:100,200,640")]      // 要素が足りない
    [InlineData("/R:100,200,640,480,5")] // 要素が多い
    [InlineData("/R:a,b,c,d")]          // 数値でない
    [InlineData("/R:")]                 // 値が空
    public void 不正な矩形オプションは無視される(string arg)
    {
        var args = CommandLineArgs.Parse([arg]);

        Assert.Null(args.CaptureRect);
    }

    [Fact]
    public void 空白だけの引数は無視される()
    {
        var args = CommandLineArgs.Parse(["", "   ", @"C:\pic.png"]);

        Assert.Equal([@"C:\pic.png"], args.ImagePaths);
    }

    [Fact]
    public void オプションと画像パスを同時に指定できる()
    {
        var args = CommandLineArgs.Parse(["/C:Capture", @"C:\pic.png", "/R:0,0,100,100"]);

        Assert.Equal(CommandLineAction.Capture, args.Action);
        Assert.Equal([@"C:\pic.png"], args.ImagePaths);
        Assert.Equal(new Int32Rect(0, 0, 100, 100), args.CaptureRect);
    }

    [Fact]
    public void 引数の直列化と復元で内容が保たれる()
    {
        // パイプ越しの引き渡し (SPEC-v1.8 2.6)。送信側は生の引数を並べるだけで、
        // パースは受信側で 1 回だけ行う
        string[] original = [@"C:\my pic.png", "/C:Option", "/R:1,2,3,4"];

        string payload = CommandLineArgs.SerializeArgs(original);
        var restored = CommandLineArgs.DeserializeArgs(payload);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void 空白を含むパスも直列化で壊れない()
    {
        // スペース区切りにすると壊れるため行区切りにしてある
        string[] original = [@"C:\Program Files\a b c.png"];

        var restored = CommandLineArgs.DeserializeArgs(CommandLineArgs.SerializeArgs(original));

        Assert.Equal(original, restored);
    }

    [Fact]
    public void 直列化と復元を経ても解析結果が同じになる()
    {
        // 初回起動と二重起動で解釈結果が一致すること (SPEC-v1.8 2.1)
        string[] original = [@"C:\pic.png", "/C:Capture", "/R:10,20,30,40"];

        var direct = CommandLineArgs.Parse(original);
        var viaPipe = CommandLineArgs.Parse(
            CommandLineArgs.DeserializeArgs(CommandLineArgs.SerializeArgs(original)));

        Assert.Equal(direct.ImagePaths, viaPipe.ImagePaths);
        Assert.Equal(direct.Action, viaPipe.Action);
        Assert.Equal(direct.CaptureRect, viaPipe.CaptureRect);
    }
}
