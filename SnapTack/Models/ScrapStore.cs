using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SnapTack.Models;

/// <summary>
/// スクラップの永続化 (SPEC-v1.5 2.4)。<c>scraps/index.json</c> にメタデータ、
/// <c>scraps/&lt;id&gt;.png</c> に画像本体を保存する。保存先の規則は設定と同じ
/// (exe 同階層 → %APPDATA% フォールバック。<see cref="StorageLocation"/>)。
/// </summary>
public sealed class ScrapStore
{
    // index の形式バージョン。未知 (将来の形式) の場合は解釈せず空で起動する (SPEC-v1.5 2.4)
    private const int CurrentSchemaVersion = 1;
    private const string ScrapsFolderName = "scraps";
    private const string IndexFileName = "index.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // scraps ディレクトリ。書き込みを試みて成功した側を採用する (設定と同じ判定)
    private readonly string _primaryDir;
    private readonly string _fallbackDir;
    private string? _resolvedDir; // 書き込み確定後にキャッシュする

    public ScrapStore()
    {
        _primaryDir = Path.Combine(StorageLocation.PrimaryDirectory, ScrapsFolderName);
        _fallbackDir = Path.Combine(StorageLocation.FallbackDirectory, ScrapsFolderName);
    }

    /// <summary>保存先を明示して初期化する (テスト用)。フォールバックは使わない。</summary>
    internal ScrapStore(string scrapsDirectory)
    {
        _primaryDir = scrapsDirectory;
        _fallbackDir = scrapsDirectory;
    }

    // ===== 読み込み =====

    /// <summary>
    /// index を読み、復元した <see cref="ScrapItem"/> の一覧を返す。画像は遅延ローダーで後付けする。
    /// 壊れている・未知スキーマ・ディレクトリ無しの場合は空を返す (落とさない。SPEC-v1.5 2.4)。
    /// 画像ファイルが欠けているエントリは読み飛ばす。
    /// </summary>
    /// <param name="indexNeedsRewrite">画像欠落などで index を書き直すべきなら true。</param>
    public IReadOnlyList<ScrapItem> Load(out bool indexNeedsRewrite)
    {
        indexNeedsRewrite = false;

        string dir = ExistingDirectory();
        string indexPath = Path.Combine(dir, IndexFileName);
        if (!File.Exists(indexPath))
        {
            return [];
        }
        // 実データを読んだディレクトリを以後の書き込み・削除でも使う。primary が存在するが
        // 書き込み不可で fallback から読んだ場合でも、DeleteImage が正しい側を指すようにする
        _resolvedDir = dir;

        ScrapIndex? index;
        try
        {
            index = JsonSerializer.Deserialize<ScrapIndex>(File.ReadAllText(indexPath), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // 壊れた index は無視して空で起動する。次の保存まではファイルに触れない
            return [];
        }

        // 未知スキーマ (将来の形式) は無理に解釈しない。既存データを壊さないよう空で起動する
        if (index is null || index.SchemaVersion != CurrentSchemaVersion || index.Scraps is null)
        {
            return [];
        }

        var items = new List<ScrapItem>(index.Scraps.Count);
        foreach (var entry in index.Scraps)
        {
            if (!TryBuildItem(dir, entry, out var item))
            {
                // 画像が欠けている等。読み飛ばして index を書き直す対象にする
                indexNeedsRewrite = true;
                continue;
            }
            items.Add(item);
        }
        return items;
    }

    /// <summary>index エントリ 1 件から <see cref="ScrapItem"/> を組み立てる。画像欠落なら false。</summary>
    private bool TryBuildItem(string dir, ScrapEntry entry, out ScrapItem item)
    {
        item = null!;
        if (!Guid.TryParse(entry.Id, out var id))
        {
            return false;
        }
        string imagePath = Path.Combine(dir, id + ".png");
        if (!File.Exists(imagePath))
        {
            return false;
        }

        var rect = new Int32Rect(entry.PhysicalRect.X, entry.PhysicalRect.Y,
            entry.PhysicalRect.Width, entry.PhysicalRect.Height);
        item = new ScrapItem(id, rect, entry.CapturedAt)
        {
            State = entry.State,
            TrashedAt = entry.TrashedAt,
            OpacityPercent = entry.OpacityPercent,
            IsDice = entry.IsDice,
            WindowPosition = entry.WindowPosition is { } wp ? new Point(wp.X, wp.Y) : null,
        };
        // 画像は初回参照時に読む (遅延読み込み。SPEC-v1.5 3.3)
        item.SetImageLoader(() => LoadImage(imagePath));
        return true;
    }

    private static BitmapSource LoadImage(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad; // ファイルを掴みっぱなしにしない
        image.UriSource = new Uri(path);
        image.EndInit();
        image.Freeze();
        return image;
    }

    // ===== 書き込み =====

    /// <summary>index.json をスクラップ一覧の内容で書き直す。失敗しても落とさない (false を返す)。</summary>
    public bool SaveIndex(IEnumerable<ScrapItem> scraps)
    {
        var index = new ScrapIndex
        {
            SchemaVersion = CurrentSchemaVersion,
            Scraps = scraps.Select(ToEntry).ToList(),
        };
        string json = JsonSerializer.Serialize(index, JsonOptions);

        string? dir = EnsureWritableDirectory();
        if (dir is null)
        {
            return false;
        }
        try
        {
            File.WriteAllText(Path.Combine(dir, IndexFileName), json);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>スクラップの画像を &lt;id&gt;.png として保存する (追加時に 1 度だけ)。失敗で false。</summary>
    public bool SaveImage(ScrapItem item)
    {
        string? dir = EnsureWritableDirectory();
        if (dir is null)
        {
            return false;
        }
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(item.Image));
            using var stream = File.Create(Path.Combine(dir, item.Id + ".png"));
            encoder.Save(stream);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>スクラップの画像ファイルを削除する。無ければ何もしない。</summary>
    public void DeleteImage(Guid id)
    {
        string dir = _resolvedDir ?? ExistingDirectory();
        try
        {
            string path = Path.Combine(dir, id + ".png");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 画像が消せなくても致命的ではない。次回起動時に index から外れていれば孤児として残るだけ
        }
    }

    // ===== ディレクトリ解決 =====

    /// <summary>
    /// 読み込み用: 実データ (index.json) を持つ scraps ディレクトリを返す (primary 優先)。
    /// index がどちらにも無ければ、存在するディレクトリ → primary の順で返す。
    /// </summary>
    private string ExistingDirectory()
    {
        if (_resolvedDir is not null)
        {
            return _resolvedDir;
        }
        // index.json を持つ側を優先する (primary が空でも fallback に実データがあれば拾う)
        if (File.Exists(Path.Combine(_primaryDir, IndexFileName)))
        {
            return _primaryDir;
        }
        if (File.Exists(Path.Combine(_fallbackDir, IndexFileName)))
        {
            return _fallbackDir;
        }
        if (Directory.Exists(_primaryDir))
        {
            return _primaryDir;
        }
        if (Directory.Exists(_fallbackDir))
        {
            return _fallbackDir;
        }
        return _primaryDir;
    }

    /// <summary>書き込み用: 書ける scraps ディレクトリを作成して返す。両方失敗なら null。</summary>
    private string? EnsureWritableDirectory()
    {
        if (_resolvedDir is not null)
        {
            return _resolvedDir;
        }
        foreach (string dir in new[] { _primaryDir, _fallbackDir })
        {
            try
            {
                Directory.CreateDirectory(dir);
                _resolvedDir = dir; // 以後は同じ場所を使う (画像と index を揃える)
                return dir;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 次の候補へ
            }
        }
        return null;
    }

    // ===== DTO =====

    private static ScrapEntry ToEntry(ScrapItem item) => new()
    {
        Id = item.Id.ToString(),
        CapturedAt = item.CapturedAt,
        State = item.State,
        TrashedAt = item.TrashedAt,
        PhysicalRect = new RectDto
        {
            X = item.PhysicalRect.X,
            Y = item.PhysicalRect.Y,
            Width = item.PhysicalRect.Width,
            Height = item.PhysicalRect.Height,
        },
        OpacityPercent = item.OpacityPercent,
        IsDice = item.IsDice,
        WindowPosition = item.WindowPosition is { } p ? new PointDto { X = p.X, Y = p.Y } : null,
    };

    private sealed class ScrapIndex
    {
        public int SchemaVersion { get; set; }
        public List<ScrapEntry>? Scraps { get; set; }
    }

    private sealed class ScrapEntry
    {
        public string Id { get; set; } = "";
        public DateTimeOffset CapturedAt { get; set; }
        public ScrapState State { get; set; }
        public DateTimeOffset? TrashedAt { get; set; }
        public RectDto PhysicalRect { get; set; } = new();
        public int OpacityPercent { get; set; } = 100;
        public bool IsDice { get; set; }
        public PointDto? WindowPosition { get; set; }
    }

    private sealed class RectDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    private sealed class PointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }
}
