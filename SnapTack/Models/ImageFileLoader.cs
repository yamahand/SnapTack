using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SnapTack.Models;

/// <summary>
/// 外部画像 (ファイル / クリップボード) の読み込み (SPEC-v1.6 3)。
/// キャプチャ以外の経路でスクラップを作るための入力側を担う。
/// </summary>
/// <remarks>
/// ここでは画像を読むだけで、スクラップ化はしない。生成は必ず
/// <see cref="ScrapManager"/> を経由させる (中央管理を崩さない。SPEC-v1.5 3.1)。
/// </remarks>
public static class ImageFileLoader
{
    /// <summary>
    /// 読み込みに対応する拡張子 (SPEC-v1.6 3.2)。WPF (WIC) のデコーダが扱える範囲。
    /// </summary>
    /// <remarks>
    /// webp / avif は OS 標準ではなく Store の拡張機能パッケージ
    /// (Microsoft.WebpImageExtension / Microsoft.HEIFImageExtension) が提供するデコーダで読む。
    /// これらは削除可能なため、**未導入の環境では読めない**。その場合も
    /// <see cref="TryLoadFile"/> が null を返して読み飛ばされるだけで、アプリは落ちない
    /// (SPEC-v1.6 3.2 の「読み込みに失敗したファイルは読み飛ばす」に乗る)。
    /// heic は同じ HEIF デコーダで読めるが、Windows 版では需要が薄いため入れていない。
    /// </remarks>
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".avif",
        };

    /// <summary>パスの拡張子が対応形式かどうか。ファイルの存在は見ない。</summary>
    public static bool IsSupportedExtension(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// 画像ファイルを読み込む。デコードできなければ null (呼び出し側が読み飛ばす)。
    /// </summary>
    public static BitmapSource? TryLoadFile(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad; // ファイルを掴みっぱなしにしない
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze(); // ウィンドウ間で使い回すため (CLAUDE.md のコーディング規約)
            return image;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or NotSupportedException
                or ArgumentException or UriFormatException or FileFormatException
                or COMException or System.Security.SecurityException)
        {
            // 壊れたファイル・デコード不可・権限不足など。読み飛ばす (SPEC-v1.6 3.2)。
            // COMException は WIC が直接投げてくる。対応デコーダが未導入の webp/avif や、
            // 拡張子は合っているが中身が壊れているファイルがこれに当たる
            // (実測: 0xC00D36BE = ファイルが形式仕様に準拠していない)
            return null;
        }
    }

    /// <summary>
    /// 与えられたパス群のうち、対応拡張子で実際に読めたものだけを読み込む。
    /// ドロップされたファイル群の処理に使う (SPEC-v1.6 3.3)。
    /// </summary>
    public static IReadOnlyList<BitmapSource> LoadFiles(IEnumerable<string> paths)
    {
        var images = new List<BitmapSource>();
        foreach (string path in paths)
        {
            if (!IsSupportedExtension(path) || !File.Exists(path))
            {
                continue;
            }
            if (TryLoadFile(path) is { } image)
            {
                images.Add(image);
            }
        }
        return images;
    }

    /// <summary>
    /// 与えられたパス群に、読み込み対象になり得るものが 1 つでも含まれるか。
    /// D&amp;D のカーソル表示 (受け入れ可否) の判定に使う。実際の読み込みは行わない。
    /// </summary>
    public static bool ContainsSupportedFile(IEnumerable<string> paths) =>
        paths.Any(IsSupportedExtension);

    /// <summary>
    /// クリップボードの内容から画像を取り出す (SPEC-v1.6 3.2)。
    /// 画像 → ファイルドロップ → テキスト (パスとして解釈) の順に判定し、最初に成立したものを返す。
    /// 何も取り出せなければ空。
    /// </summary>
    public static IReadOnlyList<BitmapSource> LoadFromClipboard()
    {
        try
        {
            // 1. 画像そのもの (スクリーンショットや他アプリからのコピー)
            if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } image)
            {
                image.Freeze();
                return [image];
            }

            // 2. エクスプローラーでファイルをコピーした場合
            if (Clipboard.ContainsFileDropList())
            {
                var dropped = Clipboard.GetFileDropList().Cast<string?>()
                    .Where(p => p is not null).Select(p => p!);
                var images = LoadFiles(dropped);
                if (images.Count > 0)
                {
                    return images;
                }
            }

            // 3. テキストをパスとして解釈する (「パスのコピー」で得られる形式)
            if (Clipboard.ContainsText())
            {
                return LoadFiles(ParsePathsFromText(Clipboard.GetText()));
            }
        }
        catch (Exception ex) when (ex is ExternalException or OutOfMemoryException)
        {
            // 他プロセスのクリップボードロック、または巨大データのデコード失敗。
            // 「貼るものが無かった」と同じ扱いにする (SPEC-v1.6 3.2)
        }
        return [];
    }

    /// <summary>
    /// テキストを 1 行 1 パスとして解釈する。前後の空白と、パスを囲む二重引用符を除く。
    /// エクスプローラーの「パスのコピー」は複数選択で改行区切り・各行が引用符付きになる。
    /// </summary>
    /// <remarks>クリップボードを介さず検証できるよう internal で公開している (テスト用)。</remarks>
    internal static IEnumerable<string> ParsePathsFromText(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Trim('"'))
            .Where(line => line.Length > 0);
}
