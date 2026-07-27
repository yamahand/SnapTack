using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SnapTack.Resources;

namespace SnapTack.Models;

/// <summary>
/// スクラップ画像の保存処理 (SPEC-v1.6 2)。ダイアログ保存・即保存・保存後のパスコピーを担う。
/// </summary>
/// <remarks>
/// 付箋 (<see cref="Views.ScrapWindow"/>) とスクラップリスト (<see cref="Views.ScrapListWindow"/>) で
/// 同じ保存処理を重複させていたため、v1.6 でここへ集約した。
/// 保存する画像は常に元画像そのまま (不透明度・枠線は反映しない。SPEC-v1.x 2.1)。
/// </remarks>
public sealed class ImageSaver
{
    // 言語非依存の文字列。日時書式はカルチャに依らずファイル名として安定させる
    private const string AppName = "SnapTack";
    private const string FileNameFormat = "SnapTack_{0:yyyyMMdd_HHmmss}";

    // 同名ファイルがある場合に付ける連番の上限。これを超えたら保存失敗として扱う。
    // 秒単位の名前が 1000 個衝突する状況は異常であり、無限ループを避ける保険
    private const int MaxNameCollisionRetry = 1000;

    private readonly SettingsService _settings;

    public ImageSaver(SettingsService settings) => _settings = settings;

    /// <summary>
    /// <see cref="SaveFileDialog"/> を出して 1 枚を保存する (SPEC-v1.6 2.1)。
    /// 保存した場合のみ true。キャンセル・失敗では false。
    /// </summary>
    /// <param name="owner">ダイアログの親ウィンドウ。</param>
    /// <param name="image">保存する画像 (物理ピクセル等倍)。</param>
    /// <param name="timestamp">既定ファイル名に使う日時。</param>
    public bool SaveWithDialog(Window owner, BitmapSource image, DateTime timestamp)
    {
        var format = _settings.Current.SaveFormat;

        var dialog = new SaveFileDialog
        {
            FileName = string.Format(FileNameFormat, timestamp) + ImageFormatInfo.GetExtension(format),
            DefaultExt = ImageFormatInfo.GetExtension(format),
            Filter = ImageFormatInfo.BuildDialogFilter(),
            FilterIndex = ImageFormatInfo.GetFilterIndex(format), // 既定の選択を設定値に合わせる
            AddExtension = true,
            InitialDirectory = GetDialogInitialDirectory(),
        };
        if (dialog.ShowDialog(owner) != true)
        {
            return false;
        }

        // ユーザーが明示的に付けた拡張子を尊重する。対応外の拡張子ならダイアログで選択された
        // フィルタに従う (SPEC-v1.6 2.1)
        var actualFormat = ImageFormatInfo.FromExtension(Path.GetExtension(dialog.FileName))
            ?? ImageFormatInfo.FromFilterIndex(dialog.FilterIndex);

        // 拡張子と形式を必ず一致させる。ダイアログでフィルタを切り替えつつ拡張子を省いた場合、
        // AddExtension は DefaultExt (設定値の形式) を補ってしまい、選択したフィルタと食い違う。
        // ここで補正しないと「中身は JPEG なのにファイル名は .png」になる
        string path = ImageFormatInfo.EnsureExtension(dialog.FileName, actualFormat);

        if (!TryEncode(image, path, actualFormat))
        {
            ShowSaveFailed();
            return false;
        }

        // 次回のデフォルト保存先として記憶する。永続化失敗は保存自体には影響しないため通知しない
        _settings.Current.LastSaveDirectory = Path.GetDirectoryName(path);
        _settings.Save();

        CopyPathsIfEnabled([path]);
        return true;
    }

    /// <summary>
    /// ダイアログを出さずに、設定のフォルダへ日時名で保存する (SPEC-v1.6 2.2)。
    /// 複数件をまとめて保存でき、1 件でも失敗があれば警告を 1 回だけ出す。
    /// </summary>
    /// <param name="images">保存する画像と、ファイル名に使う日時の組。</param>
    public void QuickSave(IEnumerable<(BitmapSource Image, DateTime Timestamp)> images)
    {
        var format = _settings.Current.SaveFormat;
        string? directory = EnsureQuickSaveDirectory();
        if (directory is null)
        {
            ShowSaveFailed();
            return;
        }

        var savedPaths = new List<string>();
        bool anyFailed = false;
        foreach (var (image, timestamp) in images)
        {
            // 上書きせず連番を付ける。ダイアログ無しで既存ファイルを壊さないため (SPEC-v1.6 2.2)
            string? path = BuildUniquePath(directory, timestamp, format);
            if (path is null || !TryEncode(image, path, format))
            {
                anyFailed = true;
                continue;
            }
            savedPaths.Add(path);
        }

        // 成功しても通知は出さない (即保存の意義が失われるため)。失敗時のみ知らせる
        if (anyFailed)
        {
            ShowSaveFailed();
        }
        CopyPathsIfEnabled(savedPaths);
    }

    /// <summary>
    /// 画像をエンコードしてファイルへ書く。失敗したら false (呼び出し側が通知する)。
    /// </summary>
    private bool TryEncode(BitmapSource image, string path, SaveImageFormat format)
    {
        try
        {
            var encoder = ImageFormatInfo.CreateEncoder(format, _settings.Current.JpegQuality);
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = File.Create(path);
            encoder.Save(stream);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ExternalException
                or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            // 不正なパスの手入力・権限不足・書き込み失敗など。スクラップは維持する (SPEC-v1.x 2.1)
            return false;
        }
    }

    /// <summary>
    /// 保存後にファイルパスをクリップボードへコピーする (設定が有効な場合。SPEC-v1.6 2.3)。
    /// 複数件は改行区切り。保存自体は成功しているため、コピー失敗は通知しない。
    /// </summary>
    private void CopyPathsIfEnabled(IReadOnlyList<string> paths)
    {
        if (!_settings.Current.CopyPathAfterSave || paths.Count == 0)
        {
            return;
        }
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, paths));
        }
        catch (ExternalException)
        {
            // 他プロセスがクリップボードをロックしている。保存は済んでいるので黙って諦める
        }
    }

    /// <summary>
    /// 即保存先に衝突しないフルパスを組み立てる。同名があれば <c>_2</c>, <c>_3</c> … を付ける。
    /// 上限まで衝突した場合は null (異常事態。保存失敗として扱う)。
    /// </summary>
    private static string? BuildUniquePath(string directory, DateTime timestamp, SaveImageFormat format)
    {
        string baseName = string.Format(FileNameFormat, timestamp);
        string extension = ImageFormatInfo.GetExtension(format);

        string path = Path.Combine(directory, baseName + extension);
        for (int suffix = 2; File.Exists(path); suffix++)
        {
            if (suffix > MaxNameCollisionRetry)
            {
                return null;
            }
            path = Path.Combine(directory, $"{baseName}_{suffix}{extension}");
        }
        return path;
    }

    /// <summary>
    /// 即保存先のフォルダを用意して返す (SPEC-v1.6 2.2)。未設定なら「ピクチャ」。
    /// 作成できなければ null。
    /// </summary>
    private string? EnsureQuickSaveDirectory()
    {
        string directory = _settings.Current.QuickSaveDirectory is { Length: > 0 } configured
            ? configured
            : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        try
        {
            Directory.CreateDirectory(directory); // 既にあれば何もしない
            return directory;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ArgumentException
                or NotSupportedException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>ダイアログの初期フォルダ。前回保存フォルダが有効ならそれを、なければ「ピクチャ」。</summary>
    private string GetDialogInitialDirectory()
    {
        string? last = _settings.Current.LastSaveDirectory;
        if (!string.IsNullOrEmpty(last) && Directory.Exists(last))
        {
            return last;
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }

    private static void ShowSaveFailed() =>
        MessageBox.Show(Strings.SaveImageFailedMessage, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
}
