using System.Runtime.CompilerServices;

// テストから internal メンバーを検証するため
// (LanguageService.Resolve、SettingsStore.Deserialize、
//  ScrapStore / SettingsService のテスト用コンストラクタ)
[assembly: InternalsVisibleTo("SnapTack.Core.Tests")]
