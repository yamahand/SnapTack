using System.IO;

namespace SnapTack.Models;

/// <summary>
/// 設定・スクラップの保存先ディレクトリの決定を共通化する (SPEC 4.5 / SPEC-v1.5 2.4)。
/// ポータブル運用のため exe 同階層を優先し、書き込み不可なら %APPDATA%\SnapTack へフォールバックする。
/// </summary>
internal static class StorageLocation
{
    private const string AppDataFolderName = "SnapTack";

    /// <summary>exe 同階層のディレクトリ。single-file publish でも exe の場所を指す。</summary>
    public static string PrimaryDirectory =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    /// <summary>%APPDATA%\SnapTack。exe 同階層に書けない (Program Files 配下など) 場合の代替。</summary>
    public static string FallbackDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDataFolderName);
}
