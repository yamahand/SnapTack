using System.Globalization;
using SnapTack.Models;
using Xunit;

namespace SnapTack.Tests;

/// <summary>保存ファイル名の規則 (SPEC-v1.6 2.2) の検証。</summary>
public class ImageSaverTests
{
    /// <summary>テスト用の固定日時。西暦 2026-07-27 14:05:09。</summary>
    private static readonly DateTime Timestamp = new(2026, 7, 27, 14, 5, 9);

    private const string Expected = "SnapTack_20260727_140509";

    [Fact]
    public void ファイル名は日時から西暦で組み立てられる()
    {
        Assert.Equal(Expected, ImageSaver.FormatFileName(Timestamp));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    [InlineData("ar-SA")] // ヒジュラ暦
    [InlineData("th-TH")] // 仏暦
    [InlineData("fa-IR")] // イラン暦
    public void ファイル名はカルチャに依存しない(string culture)
    {
        // 退行防止: string.Format をプロバイダ無しで呼ぶと CurrentCulture のカレンダーが
        // 適用され、ar-SA なら SnapTack_14480213_... のように西暦でない名前になる。
        // 即保存はユーザーが名前を確認する機会が無いぶん実害が大きい (PR #20 のレビュー指摘)
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            Assert.Equal(Expected, ImageSaver.FormatFileName(Timestamp));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
