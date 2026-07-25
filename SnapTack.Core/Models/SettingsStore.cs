using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnapTack.Models;

/// <summary>
/// 設定の JSON 永続化。
/// ポータブル運用を考慮して exe と同階層を優先し、書き込み不可の場合は
/// %APPDATA%\SnapTack へフォールバックする (SPEC 4.5)。
/// </summary>
public sealed class SettingsStore
{
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _primaryPath;  // exe 同階層
    private readonly string _fallbackPath; // %APPDATA%\SnapTack

    public SettingsStore()
    {
        // 保存先の規則は StorageLocation に集約 (scraps/ と同じ判定を使う)
        _primaryPath = Path.Combine(StorageLocation.PrimaryDirectory, SettingsFileName);
        _fallbackPath = Path.Combine(StorageLocation.FallbackDirectory, SettingsFileName);
    }

    /// <summary>設定を読み込む。ファイルがない・読めない・壊れている場合は既定値を返す。</summary>
    public AppSettings Load()
    {
        foreach (string path in new[] { _primaryPath, _fallbackPath })
        {
            try
            {
                if (File.Exists(path))
                {
                    var settings = Deserialize(File.ReadAllText(path));
                    if (settings is not null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // 壊れた設定ファイルは無視して既定値で継続する
            }
        }
        return new AppSettings();
    }

    /// <summary>
    /// JSON 文字列を <see cref="AppSettings"/> に変換する (テストから直接検証するため internal)。
    /// v1.4 以前のように v1.5 のキーが無い JSON でも、欠けたキーは既定値で補われる (SPEC-v1.5 4)。
    /// </summary>
    internal static AppSettings? Deserialize(string json) =>
        JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);

    /// <summary>設定を保存する。両方の保存先に失敗した場合は false を返す。</summary>
    public bool Save(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);

        try
        {
            File.WriteAllText(_primaryPath, json);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // exe 同階層に書けない (Program Files 配下など) 場合はフォールバックへ
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_fallbackPath)!);
            File.WriteAllText(_fallbackPath, json);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
