using System.Globalization;

namespace SnapTack.Models;

/// <summary>
/// <see cref="AppLanguage"/> を UI カルチャへ反映する。
/// 反映後に生成される UI が新しい言語になる (既存の付箋は生成時の言語のまま)。
/// </summary>
public static class LanguageService
{
    /// <summary>
    /// 起動時点の UI カルチャ (= Windows の「表示言語」)。Auto の戻り先として使う。
    /// <see cref="CultureInfo.InstalledUICulture"/> は OS の<em>インストール</em>言語で固定のため、
    /// 表示言語だけを切り替えている環境では「OS の表示言語に従う」仕様とズレる。
    /// Apply で CurrentUICulture を上書きする前に確定させる必要があるので static 初期化で捕まえる。
    /// </summary>
    private static readonly CultureInfo StartupUICulture = CultureInfo.CurrentUICulture;

    /// <summary>設定の言語を現在のスレッドと以降の新規スレッドへ適用する。</summary>
    public static void Apply(AppLanguage language)
    {
        // Auto は起動時の UI カルチャへ戻す。English/Japanese から Auto へ戻す場合に
        // 上書き済みの CurrentUICulture を参照しないよう、スナップショットを使う
        var culture = ToCulture(language) ?? StartupUICulture;

        // DefaultThreadCurrentUICulture だけでは実行中のスレッドに効かないため両方に設定する
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    /// <summary>言語に対応する UI カルチャを返す。Auto は null (呼び出し側で起動時の値を使う)。</summary>
    private static CultureInfo? ToCulture(AppLanguage language) => language switch
    {
        AppLanguage.English => new CultureInfo("en"),
        AppLanguage.Japanese => new CultureInfo("ja"),
        _ => null,
    };
}
