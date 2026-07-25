using System.Globalization;
using SnapTack.Models;
using Xunit;

namespace SnapTack.Tests;

/// <summary>UI 言語の解決 (SPEC-v1.x 2.5) の検証。</summary>
public class LanguageServiceTests
{
    // 「OS のインストール言語は日本語だが、表示言語は英語」という環境を想定した起動時カルチャ。
    // Auto がこちらへ戻ることを確認するために使う
    private static readonly CultureInfo StartupEnUs = new("en-US");

    [Fact]
    public void Englishは英語カルチャになる()
    {
        Assert.Equal("en", LanguageService.Resolve(AppLanguage.English, StartupEnUs).Name);
    }

    [Fact]
    public void Japaneseは日本語カルチャになる()
    {
        Assert.Equal("ja", LanguageService.Resolve(AppLanguage.Japanese, StartupEnUs).Name);
    }

    [Fact]
    public void Autoは起動時の表示言語へ戻る()
    {
        // 退行防止: 当初は InstalledUICulture (OS のインストール言語) を返していたため、
        // 表示言語だけを変更している環境で「表示言語に従う」仕様とズレていた (PR #9 のレビュー指摘)
        Assert.Equal("en-US", LanguageService.Resolve(AppLanguage.Auto, StartupEnUs).Name);
    }

    [Fact]
    public void Autoはインストール言語を参照しない()
    {
        // 起動時カルチャと InstalledUICulture が異なる場合に、前者が選ばれること。
        // 両者がたまたま一致する環境ではこの退行を検出できないため、異なる値を明示的に渡す
        var installed = CultureInfo.InstalledUICulture;
        var startup = installed.Name == "ja-JP" ? new CultureInfo("en-US") : new CultureInfo("ja-JP");

        var resolved = LanguageService.Resolve(AppLanguage.Auto, startup);

        Assert.Equal(startup.Name, resolved.Name);
        Assert.NotEqual(installed.Name, resolved.Name);
    }

    [Theory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Japanese)]
    public void 明示指定は起動時カルチャに影響されない(AppLanguage language)
    {
        // Auto 以外は起動時カルチャが何であっても同じ結果になる
        var viaEn = LanguageService.Resolve(language, new CultureInfo("en-US"));
        var viaJa = LanguageService.Resolve(language, new CultureInfo("ja-JP"));

        Assert.Equal(viaEn.Name, viaJa.Name);
    }

    [Fact]
    public void 設定の既定はAuto()
    {
        // 既存の settings.json に Language キーが無い場合もこの既定値が使われる
        Assert.Equal(AppLanguage.Auto, new AppSettings().Language);
    }

    [Fact]
    public void Cloneで言語設定が引き継がれる()
    {
        // 設定画面は Clone したうえで一部項目だけ書き換えるため、
        // 言語が欠落しないことを確認する
        var original = new AppSettings { Language = AppLanguage.Japanese };

        Assert.Equal(AppLanguage.Japanese, original.Clone().Language);
    }
}
