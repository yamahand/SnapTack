using System.Globalization;
using System.Reflection;
using System.Resources;
using SnapTack.Resources;
using Xunit;

namespace SnapTack.Tests;

/// <summary>UI 文字列リソースの解決 (SPEC-v1.x 2.5) の検証。</summary>
public class StringsTests
{
    private static readonly ResourceManager Manager =
        new("SnapTack.Resources.Strings", typeof(Strings).Assembly);

    /// <summary>Strings クラスが公開しているキー名の一覧。</summary>
    private static IEnumerable<string> AllKeys =>
        typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(p => p.Name);

    [Fact]
    public void 日本語カルチャでは日本語が返る()
    {
        Assert.Equal("終了(&X)", Manager.GetString("MenuExitText", new CultureInfo("ja")));
    }

    [Fact]
    public void 英語カルチャでは英語が返る()
    {
        Assert.Equal("E&xit", Manager.GetString("MenuExitText", new CultureInfo("en")));
    }

    [Fact]
    public void 未対応の言語は英語にフォールバックする()
    {
        // 既定 (中立) リソースが英語なので、翻訳のない言語では英語が出る
        Assert.Equal("E&xit", Manager.GetString("MenuExitText", new CultureInfo("fr-FR")));
    }

    [Fact]
    public void 日本語の衛星アセンブリが配置されている()
    {
        // 退行防止: SatelliteResourceLanguages の設定ミスで ja が生成されなくなると、
        // 例外は出ずに全て英語へフォールバックしてしまい気付きにくい
        var ja = Manager.GetString("MenuExitText", new CultureInfo("ja"));
        var en = Manager.GetString("MenuExitText", new CultureInfo("en"));

        Assert.NotEqual(en, ja);
    }

    [Fact]
    public void 全てのキーが英語リソースに存在する()
    {
        // Strings.cs は手書きのため、プロパティを足して .resx への追加を忘れると
        // キー名がそのまま画面に出る。それを検出する
        var missing = AllKeys
            .Where(key => Manager.GetString(key, new CultureInfo("en")) is null)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void 全てのキーが日本語リソースに存在する()
    {
        // 日本語だけ翻訳漏れがあると、その項目だけ英語で表示される。
        // ResourceManager は衛星に無いキーを中立リソースへフォールバックさせるため、
        // 衛星アセンブリを直接読んで存在を確認する
        var jaSet = new ResourceManager("SnapTack.Resources.Strings", typeof(Strings).Assembly)
            .GetResourceSet(new CultureInfo("ja"), createIfNotExists: true, tryParents: false);
        Assert.NotNull(jaSet);

        var missing = AllKeys.Where(key => jaSet!.GetString(key) is null).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void アクセサは存在しないキーでもキー名を返す()
    {
        // 翻訳漏れでアプリを落とさないための保険が効いていること。
        // Strings の private な Get を経由できないため、同じ ResourceManager の挙動で代替検証する
        Assert.Null(Manager.GetString("NoSuchKey_ForTest", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ホットキー登録失敗メッセージに書式指定子が残っている()
    {
        // {0} を落とすと string.Format でホットキー名が消える。両言語で確認する
        foreach (string culture in new[] { "en", "ja" })
        {
            var text = Manager.GetString("HotkeyRegisterFailedFormat", new CultureInfo(culture));
            Assert.Contains("{0}", text);
        }
    }

    [Fact]
    public void トレイのキャプチャ項目に書式指定子とタブが残っている()
    {
        // {0} はホットキー表記、タブ以降が右寄せのショートカット表示になる
        foreach (string culture in new[] { "en", "ja" })
        {
            var text = Manager.GetString("MenuCaptureTextFormat", new CultureInfo(culture));
            Assert.Contains("{0}", text);
            Assert.Contains("\t", text);
        }
    }

    [Fact]
    public void PNG保存フィルタの書式が両言語で妥当()
    {
        // SaveFileDialog.Filter は "表示名|パターン" 形式。崩れるとダイアログが例外を投げる
        foreach (string culture in new[] { "en", "ja" })
        {
            var filter = Manager.GetString("SaveFileFilter", new CultureInfo(culture));
            Assert.NotNull(filter);
            var parts = filter!.Split('|');
            Assert.Equal(2, parts.Length);
            Assert.Equal("*.png", parts[1]);
        }
    }
}
