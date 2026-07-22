using System.Globalization;

namespace SnapTack.Models;

/// <summary>
/// <see cref="AppLanguage"/> を UI カルチャへ反映する。
/// 反映後に生成される UI が新しい言語になる (既存の付箋は生成時の言語のまま)。
/// </summary>
public static class LanguageService
{
    /// <summary>設定の言語を現在のスレッドと以降の新規スレッドへ適用する。</summary>
    public static void Apply(AppLanguage language)
    {
        var culture = ToCulture(language);

        // Auto は OS 既定の UI カルチャに任せる。起動時は何もしなくてよいが、
        // 別言語から Auto へ戻す場合は明示的に OS 既定へ戻す必要がある
        culture ??= CultureInfo.InstalledUICulture;

        // DefaultThreadCurrentUICulture だけでは実行中のスレッドに効かないため両方に設定する
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    /// <summary>言語に対応する UI カルチャを返す。Auto は null (OS 既定に従う)。</summary>
    private static CultureInfo? ToCulture(AppLanguage language) => language switch
    {
        AppLanguage.English => new CultureInfo("en"),
        AppLanguage.Japanese => new CultureInfo("ja"),
        _ => null,
    };
}
