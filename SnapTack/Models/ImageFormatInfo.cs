using System.IO;
using System.Windows.Media.Imaging;
using SnapTack.Resources;

namespace SnapTack.Models;

/// <summary>保存形式 (SPEC-v1.6 2.1)。</summary>
public enum SaveImageFormat
{
    /// <summary>可逆・既定。v1.5 までの唯一の形式。</summary>
    Png,
    /// <summary>非可逆。品質を指定できる。</summary>
    Jpeg,
    /// <summary>無圧縮。SETUNA2 互換のため用意する。</summary>
    Bmp,
}

/// <summary>
/// 保存形式ごとの差分 (拡張子・エンコーダ・ダイアログのフィルタ) を集約する (SPEC-v1.6 2.1)。
/// </summary>
/// <remarks>
/// エンコーダはいずれも <c>System.Windows.Media.Imaging</c> の標準実装で、外部 NuGet は使わない。
/// 形式が増えたときに触る箇所をこの 1 ファイルに閉じ込める意図で分離している。
/// </remarks>
public static class ImageFormatInfo
{
    /// <summary>ダイアログのフィルタと同じ並び。設定画面のコンボボックスもこの順で並べる。</summary>
    public static readonly SaveImageFormat[] All = [SaveImageFormat.Png, SaveImageFormat.Jpeg, SaveImageFormat.Bmp];

    /// <summary>形式に対応する既定の拡張子 (先頭のドットを含む)。</summary>
    public static string GetExtension(SaveImageFormat format) => format switch
    {
        SaveImageFormat.Jpeg => ".jpg",
        SaveImageFormat.Bmp => ".bmp",
        _ => ".png",
    };

    /// <summary>
    /// 拡張子から形式を判定する。対応外なら null。
    /// ユーザーがダイアログで手入力した拡張子を尊重するために使う (SPEC-v1.6 2.1)。
    /// </summary>
    public static SaveImageFormat? FromExtension(string? extension) =>
        extension?.ToLowerInvariant() switch
        {
            ".png" => SaveImageFormat.Png,
            ".jpg" or ".jpeg" => SaveImageFormat.Jpeg,
            ".bmp" => SaveImageFormat.Bmp,
            _ => null,
        };

    /// <summary>形式に対応するエンコーダを作る。JPEG のみ品質を反映する。</summary>
    public static BitmapEncoder CreateEncoder(SaveImageFormat format, int jpegQuality) => format switch
    {
        // 設定ファイルを手で書き換えられても落ちないよう、ここでも範囲をクランプする
        SaveImageFormat.Jpeg => new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpegQuality, 1, 100) },
        SaveImageFormat.Bmp => new BmpBitmapEncoder(),
        _ => new PngBitmapEncoder(),
    };

    /// <summary>
    /// <see cref="Microsoft.Win32.SaveFileDialog"/> 用のフィルタ文字列を <see cref="All"/> の順で作る。
    /// </summary>
    public static string BuildDialogFilter() =>
        string.Join("|", All.Select(f => string.Format(GetFilterFormat(f), GetFilterPattern(f))));

    /// <summary>
    /// ダイアログの <c>FilterIndex</c> (1 始まり) を形式から求める。
    /// 既定の選択を設定値に合わせるために使う。
    /// </summary>
    public static int GetFilterIndex(SaveImageFormat format) => Array.IndexOf(All, format) + 1;

    /// <summary>ダイアログの <c>FilterIndex</c> (1 始まり) から形式へ戻す。範囲外は PNG。</summary>
    public static SaveImageFormat FromFilterIndex(int filterIndex) =>
        filterIndex >= 1 && filterIndex <= All.Length ? All[filterIndex - 1] : SaveImageFormat.Png;

    /// <summary>設定画面のコンボボックスに出す表示名 (翻訳対象ではないので形式名そのまま)。</summary>
    public static string GetDisplayName(SaveImageFormat format) => format switch
    {
        SaveImageFormat.Jpeg => "JPEG",
        SaveImageFormat.Bmp => "BMP",
        _ => "PNG",
    };

    /// <summary>"PNG image (*.png)|{0}" のような、翻訳済みのフィルタ書式を返す。</summary>
    private static string GetFilterFormat(SaveImageFormat format) => format switch
    {
        SaveImageFormat.Jpeg => Strings.SaveFilterJpeg,
        SaveImageFormat.Bmp => Strings.SaveFilterBmp,
        _ => Strings.SaveFilterPng,
    };

    /// <summary>フィルタのパターン部分。JPEG は .jpeg も受け付ける。</summary>
    private static string GetFilterPattern(SaveImageFormat format) => format switch
    {
        SaveImageFormat.Jpeg => "*.jpg;*.jpeg",
        SaveImageFormat.Bmp => "*.bmp",
        _ => "*.png",
    };

    /// <summary>
    /// パスの拡張子が形式と一致しなければ、既定の拡張子へ差し替えたパスを返す。
    /// 即保存 (ダイアログを経ない) でファイル名を組み立てる際に使う。
    /// </summary>
    public static string EnsureExtension(string path, SaveImageFormat format)
    {
        string extension = Path.GetExtension(path);
        return FromExtension(extension) == format ? path : Path.ChangeExtension(path, GetExtension(format));
    }
}
