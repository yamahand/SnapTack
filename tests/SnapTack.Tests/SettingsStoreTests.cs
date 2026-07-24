using System.Windows.Input;
using SnapTack.Models;
using Xunit;

namespace SnapTack.Tests;

/// <summary>
/// 設定 JSON の読み込み契約の検証。特に v1.4 以前の settings.json との後方互換 (SPEC-v1.5 4) を担保する。
/// </summary>
public class SettingsStoreTests
{
    [Fact]
    public void v14の設定JSONを読むとv15のキーは既定値で補われる()
    {
        // v1.4 時点の settings.json (v1.5 のキーを一切含まない)。列挙は文字列で書かれる
        const string v14Json = """
            {
              "HotkeyModifiers": "Control, Alt",
              "HotkeyKey": "S",
              "LastSaveDirectory": "C:\\Users\\me\\Pictures",
              "Language": "Japanese"
            }
            """;

        var settings = SettingsStore.Deserialize(v14Json);

        Assert.NotNull(settings);

        // 既存キーはそのまま読める
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Alt, settings!.HotkeyModifiers);
        Assert.Equal(Key.S, settings.HotkeyKey);
        Assert.Equal(@"C:\Users\me\Pictures", settings.LastSaveDirectory);
        Assert.Equal(AppLanguage.Japanese, settings.Language);

        // v1.5 で追加したキーは既定値になる (SPEC-v1.5 2.5)
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Shift, settings.ScrapListHotkeyModifiers);
        Assert.Equal(Key.L, settings.ScrapListHotkeyKey);
        Assert.Equal(200, settings.MaxScraps);
        Assert.Equal(50, settings.MaxTrashedScraps);
        Assert.Equal(30, settings.TrashRetentionDays);
        Assert.True(settings.RestoreScrapsOnStartup);
        Assert.Equal(ScrapListLayout.Grid, settings.ScrapListLayout);
    }

    [Fact]
    public void v15のキーを含むJSONは書いた値がそのまま読める()
    {
        const string v15Json = """
            {
              "ScrapListHotkeyModifiers": "Control, Alt",
              "ScrapListHotkeyKey": "K",
              "MaxScraps": 42,
              "MaxTrashedScraps": 7,
              "TrashRetentionDays": 0,
              "RestoreScrapsOnStartup": false,
              "ScrapListLayout": "List"
            }
            """;

        var settings = SettingsStore.Deserialize(v15Json);

        Assert.NotNull(settings);
        Assert.Equal(ModifierKeys.Control | ModifierKeys.Alt, settings!.ScrapListHotkeyModifiers);
        Assert.Equal(Key.K, settings.ScrapListHotkeyKey);
        Assert.Equal(42, settings.MaxScraps);
        Assert.Equal(7, settings.MaxTrashedScraps);
        Assert.Equal(0, settings.TrashRetentionDays);
        Assert.False(settings.RestoreScrapsOnStartup);
        Assert.Equal(ScrapListLayout.List, settings.ScrapListLayout);
    }

    [Fact]
    public void 空のJSONオブジェクトはすべて既定値になる()
    {
        var settings = SettingsStore.Deserialize("{}");
        var defaults = new AppSettings();

        Assert.NotNull(settings);
        Assert.Equal(defaults.HotkeyModifiers, settings!.HotkeyModifiers);
        Assert.Equal(defaults.HotkeyKey, settings.HotkeyKey);
        Assert.Equal(defaults.MaxScraps, settings.MaxScraps);
        Assert.Equal(defaults.MaxTrashedScraps, settings.MaxTrashedScraps);
        Assert.Equal(defaults.TrashRetentionDays, settings.TrashRetentionDays);
        Assert.Equal(defaults.RestoreScrapsOnStartup, settings.RestoreScrapsOnStartup);
    }
}
