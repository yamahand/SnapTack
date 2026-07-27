using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapTack.Models;
using SnapTack.Views;
using Xunit;

namespace SnapTack.Tests;

/// <summary>スクラップの状態遷移・二重表示防止・上限管理・永続化 (SPEC-v1.5 2.1/2.3/2.4) の検証。</summary>
public class ScrapManagerTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "SnapTackManagerTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch (IOException) { }
    }

    private ScrapStore NewStore() => new(_tempDir);

    /// <summary>実ウィンドウの代わりに使うテスト用ビュー。表示状態と発火した意図を記録する。</summary>
    private sealed class FakeView : IScrapView
    {
        public ScrapItem Item { get; }
        public bool IsOpen { get; private set; } = true;
        public int ActivateCount { get; private set; }
        public int SaveStateCount { get; private set; }

        public event EventHandler? TrashRequested;
        public event EventHandler? StashRequested;
        public event EventHandler<Point>? PasteRequested;
        public event EventHandler? Closed;

        public FakeView(ScrapItem item) => Item = item;

        public void Show() => IsOpen = true;
        public bool Activate() { ActivateCount++; return true; }
        public void SaveStateToItem() => SaveStateCount++;

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }

        // 以下はユーザー操作の模擬
        public void UserTrash() => TrashRequested?.Invoke(this, EventArgs.Empty);
        public void UserStash() => StashRequested?.Invoke(this, EventArgs.Empty);
        public void UserPaste(Point position) => PasteRequested?.Invoke(this, position);
    }

    private static BitmapSource MakeImage() =>
        BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr24, null, new byte[] { 0, 0, 0 }, 3);

    /// <summary>生成した FakeView を後から参照できる Manager を組み立てる。</summary>
    private static (ScrapManager Manager, Dictionary<ScrapItem, FakeView> Views) NewManager(
        AppSettings? settings = null, ScrapStore? store = null)
    {
        var views = new Dictionary<ScrapItem, FakeView>();
        var service = new SettingsService(settings ?? new AppSettings());
        var manager = new ScrapManager(service, item =>
        {
            var view = new FakeView(item);
            views[item] = view;
            return view;
        }, store);
        return (manager, views);
    }

    private static ScrapItem Add(ScrapManager m) => m.Add(MakeImage(), new Int32Rect(0, 0, 10, 10));

    [Fact]
    public void 追加したスクラップはPinnedで表示される()
    {
        var (m, views) = NewManager();

        var item = Add(m);

        Assert.Equal(ScrapState.Pinned, item.State);
        Assert.True(views[item].IsOpen);
        Assert.Contains(item, m.Items);
    }

    [Fact]
    public void ActiveScrapsとTrashedScrapsが状態で振り分けられる()
    {
        var (m, views) = NewManager();
        var pinned = Add(m);
        var stashed = Add(m);
        var trashed = Add(m);
        views[stashed].UserStash();
        views[trashed].UserTrash();

        Assert.Equal(new[] { pinned, stashed }, m.ActiveScraps.ToArray());
        Assert.Equal(new[] { trashed }, m.TrashedScraps.ToArray());
    }

    [Fact]
    public void Deleteでコレクションから完全に消える()
    {
        var (m, views) = NewManager();
        var item = Add(m);
        views[item].UserTrash();

        m.Delete(item);

        Assert.DoesNotContain(item, m.Items);
        Assert.Empty(m.TrashedScraps);
    }

    [Fact]
    public void 状態変更でChangedが発火する()
    {
        var (m, views) = NewManager();
        int changes = 0;
        m.Changed += (_, _) => changes++;

        var item = Add(m);   // +1
        views[item].UserStash(); // +1
        m.Show(item);        // +1
        m.Trash(item);       // +1
        m.Delete(item);      // +1

        Assert.Equal(5, changes);
    }

    [Fact]
    public void IsShownは表示中だけtrueを返す()
    {
        var (m, views) = NewManager();
        var item = Add(m);
        Assert.True(m.IsShown(item));

        views[item].UserStash();
        Assert.False(m.IsShown(item));
    }

    [Fact]
    public void 閉じる要求でTrashedになりウィンドウが閉じる()
    {
        var (m, views) = NewManager();
        var item = Add(m);

        views[item].UserTrash();

        Assert.Equal(ScrapState.Trashed, item.State);
        Assert.NotNull(item.TrashedAt);
        Assert.False(views[item].IsOpen);
        Assert.Contains(item, m.Items); // 破棄せず残る
    }

    [Fact]
    public void 隠す要求でStashedになりウィンドウが閉じる()
    {
        var (m, views) = NewManager();
        var item = Add(m);

        views[item].UserStash();

        Assert.Equal(ScrapState.Stashed, item.State);
        Assert.Null(item.TrashedAt);
        Assert.False(views[item].IsOpen);
        Assert.Contains(item, m.Items);
    }

    [Fact]
    public void Trashedから復元するとPinnedに戻り再表示される()
    {
        var (m, views) = NewManager();
        var item = Add(m);
        views[item].UserTrash();

        m.Restore(item);

        Assert.Equal(ScrapState.Pinned, item.State);
        Assert.Null(item.TrashedAt);
        Assert.True(views[item].IsOpen);
    }

    [Fact]
    public void Trashedから隠すとTrashedAtがクリアされる()
    {
        // TrashedAt は Trashed のときのみ非 null という不変条件を保つ (SPEC-v1.5 2.4)
        var (m, views) = NewManager();
        var item = Add(m);
        views[item].UserTrash();
        Assert.NotNull(item.TrashedAt);

        m.Stash(item);

        Assert.Equal(ScrapState.Stashed, item.State);
        Assert.Null(item.TrashedAt);
    }

    [Fact]
    public void 表示中のスクラップを再表示しても新規生成せずアクティブ化する()
    {
        var (m, views) = NewManager();
        var item = Add(m);
        var view = views[item];

        m.Show(item); // 既に Pinned で表示中

        Assert.Same(view, views[item]); // ファクトリが再度呼ばれていない
        Assert.Equal(1, view.ActivateCount);
    }

    [Fact]
    public void Stashを再表示すると新しいウィンドウで開く()
    {
        var (m, views) = NewManager();
        var item = Add(m);
        views[item].UserStash();

        m.Show(item);

        Assert.Equal(ScrapState.Pinned, item.State);
        Assert.True(views[item].IsOpen);
    }

    [Fact]
    public void アクティブ上限を超えると古いStashedから削除される()
    {
        var settings = new AppSettings { MaxScraps = 2 };
        var (m, views) = NewManager(settings);

        var a = Add(m);
        var b = Add(m);
        // a, b を隠して 2 件 (上限ちょうど)
        views[a].UserStash();
        views[b].UserStash();
        // 3 件目を追加すると上限超過。最古の Stashed = a が削除される
        var c = Add(m);

        Assert.DoesNotContain(a, m.Items);
        Assert.Contains(b, m.Items);
        Assert.Contains(c, m.Items);
    }

    [Fact]
    public void Trashedから隠すとアクティブ上限チェックが走る()
    {
        // Trashed → Stashed は Pinned+Stashed を +1 するため、上限超過なら削除が走るべき。
        // 途中の各 Add はアクティブが上限を超えないよう組み、削除は最後の Stash でのみ起きるようにする
        var settings = new AppSettings { MaxScraps = 2 };
        var (m, views) = NewManager(settings);

        var a = Add(m);            // a: Pinned  (アクティブ 1)
        var b = Add(m);            // b: Pinned  (アクティブ 2、上限ちょうど)
        views[b].UserTrash();      // b: Trashed (アクティブ 1)
        views[a].UserStash();      // a: Stashed (アクティブ 1、以後の削除候補)
        var c = Add(m);            // c: Pinned  (アクティブ 2 = a,c。上限ちょうどで削除は起きない)

        m.Stash(b);                // b: Trashed → Stashed。アクティブ 3 → 最古の Stashed = a を削除

        Assert.DoesNotContain(a, m.Items);
        Assert.Equal(ScrapState.Stashed, b.State);
        Assert.Null(b.TrashedAt);
        Assert.Contains(c, m.Items);
    }

    [Fact]
    public void Itemsは読み取り専用で外部から変更できない()
    {
        var (m, _) = NewManager();
        Add(m);

        // 内部 List へキャストして変更できないこと (AsReadOnly のラッパーが返る)
        Assert.IsNotType<List<ScrapItem>>(m.Items);
    }

    [Fact]
    public void Pinnedは上限を超えても削除されない()
    {
        var settings = new AppSettings { MaxScraps = 1 };
        var (m, _) = NewManager(settings);

        // 全て Pinned のまま上限 (1) を超えて追加する
        var a = Add(m);
        var b = Add(m);
        var c = Add(m);

        // Pinned は削除対象外。削除候補 (Stashed) が無いため上限を超えて保持する
        Assert.Contains(a, m.Items);
        Assert.Contains(b, m.Items);
        Assert.Contains(c, m.Items);
    }

    [Fact]
    public void ゴミ箱上限を超えると古いものから削除される()
    {
        var settings = new AppSettings { MaxTrashedScraps = 2 };
        var (m, views) = NewManager(settings);

        var a = Add(m);
        var b = Add(m);
        var c = Add(m);
        // a→b→c の順にゴミ箱へ。TrashedAt の古い順は a < b < c
        views[a].UserTrash();
        views[b].UserTrash();
        views[c].UserTrash();

        // 上限 2 を超えた分 (最古の a) が削除される
        Assert.DoesNotContain(a, m.Items);
        Assert.Contains(b, m.Items);
        Assert.Contains(c, m.Items);
    }

    // ===== 外部画像からの作成 (M19。SPEC-v1.6 3.4) =====

    /// <summary>指定サイズの画像を作る (物理ピクセル)。</summary>
    private static BitmapSource MakeImage(int width, int height)
    {
        int stride = width * 3;
        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr24, null,
            new byte[stride * height], stride);
    }

    [Fact]
    public void 外部画像はPinnedで表示され管理下に入る()
    {
        // 作成経路が増えても中央管理を崩さないこと (SPEC-v1.5 3.1 / SPEC-v1.6 3.1)
        var (m, views) = NewManager();

        var item = m.AddExternal(MakeImage(20, 10));

        Assert.Equal(ScrapState.Pinned, item.State);
        Assert.True(views[item].IsOpen);
        Assert.Contains(item, m.Items);
    }

    [Fact]
    public void 外部画像のサイズは元画像の物理ピクセルと一致する()
    {
        // 等倍表示の原則を維持し、大きい画像でも縮小しない (SPEC-v1.6 3.4)
        var (m, _) = NewManager();

        var item = m.AddExternal(MakeImage(123, 45));

        Assert.Equal(123, item.PhysicalRect.Width);
        Assert.Equal(45, item.PhysicalRect.Height);
    }

    [Fact]
    public void 外部画像も永続化され別Managerで復元される()
    {
        var store = NewStore();
        var (m, _) = NewManager(store: store);
        var added = m.AddExternal(MakeImage(8, 6));

        var (m2, _) = NewManager(store: NewStore());
        m2.RestoreFromDisk();

        var one = Assert.Single(m2.Items);
        Assert.Equal(added.Id, one.Id);
        Assert.Equal(8, one.PhysicalRect.Width);
        Assert.Equal(6, one.PhysicalRect.Height);
    }

    [Fact]
    public void 位置を指定した外部画像はその位置に配置される()
    {
        // 付箋上の Ctrl+V で「元の付箋からずらして出す」ための経路 (SPEC-v1.6 3.5)。
        // プライマリモニタ内の座標を使い、クランプが働かない条件で確認する
        var (m, _) = NewManager();

        var item = m.AddExternalAt(MakeImage(10, 10), 100, 80);

        Assert.Equal(100, item.PhysicalRect.X);
        Assert.Equal(80, item.PhysicalRect.Y);
    }

    [Fact]
    public void 位置指定でも画面外へはみ出さない()
    {
        // 極端な座標を渡してもモニタ内へクランプされること
        var (m, _) = NewManager();

        var item = m.AddExternalAt(MakeImage(10, 10), 999_999, 999_999);

        var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        Assert.InRange(item.PhysicalRect.X, bounds.Left, bounds.Right);
        Assert.InRange(item.PhysicalRect.Y, bounds.Top, bounds.Bottom);
    }

    [Fact]
    public void 付箋のCtrl_V要求がManager経由の生成につながる()
    {
        // ビューは位置を通知するだけで、生成は必ず Manager が行う (SPEC-v1.5 3.1)。
        // 実際に何が貼られるかはクリップボードの中身次第 (環境依存) なので、
        // 配線が繋がっていて「貼るものが無くても落ちない」ことだけを見る。
        //
        // WPF の Clipboard は OLE 経由で STA を要求する。xunit の既定は MTA で、
        // STA 属性を足すには外部パッケージ (xunit.stafact) が要り
        // 「外部 NuGet は原則追加しない」に反するため、STA スレッドを自前で立てる
        var (m, views) = NewManager();
        var item = Add(m);
        int before = m.Items.Count;

        Exception? captured = null;
        var thread = new Thread(() =>
        {
            captured = Record.Exception(() => views[item].UserPaste(new Point(50, 50)));
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(captured);
        // クリップボードに画像があれば増える。無ければ変わらない。どちらでも減りはしない
        Assert.True(m.Items.Count >= before);
    }

    [Fact]
    public void 外部画像も上限管理の対象になる()
    {
        // キャプチャ由来と同じ経路を通ること (Add 経由なので EnforceLimits が効く)
        var (m, views) = NewManager(settings: new AppSettings { MaxScraps = 2 });
        var old = m.AddExternal(MakeImage(4, 4));
        m.Stash(old); // 削除候補 (Stashed) にしておく
        m.AddExternal(MakeImage(4, 4));
        m.AddExternal(MakeImage(4, 4));

        Assert.DoesNotContain(old, m.Items);
    }

    // ===== 永続化 (M16) =====

    [Fact]
    public void 追加したスクラップが別Managerで復元される()
    {
        var store = NewStore();
        var (m, _) = NewManager(store: store);
        var added = Add(m);
        m.Stash(added); // Stashed で保存されること

        // 別 Manager (再起動相当) で読み直す
        var (m2, _) = NewManager(store: NewStore());
        m2.RestoreFromDisk();

        var one = Assert.Single(m2.Items);
        Assert.Equal(added.Id, one.Id);
        Assert.Equal(ScrapState.Stashed, one.State);
    }

    [Fact]
    public void Deleteすると復元されない()
    {
        var store = NewStore();
        var (m, views) = NewManager(store: store);
        var keep = Add(m);
        var gone = Add(m);
        views[gone].UserTrash();
        m.Delete(gone);

        var (m2, _) = NewManager(store: NewStore());
        m2.RestoreFromDisk();

        var one = Assert.Single(m2.Items);
        Assert.Equal(keep.Id, one.Id);
    }

    [Fact]
    public void 復元時にRestoreScrapsOnStartupがfalseならPinnedを表示しない()
    {
        var store = NewStore();
        var (m, _) = NewManager(store: store);
        Add(m); // Pinned で保存

        var settings = new AppSettings { RestoreScrapsOnStartup = false };
        var (m2, views2) = NewManager(settings, NewStore());
        m2.RestoreFromDisk();

        // データは復元されるが、画面 (ビュー) は開かれない
        Assert.Single(m2.Items);
        Assert.Empty(views2);
    }

    [Fact]
    public void 期限切れのゴミ箱は起動時に削除される()
    {
        var store = NewStore();
        var (m, views) = NewManager(settings: new AppSettings { TrashRetentionDays = 30 }, store: store);
        var item = Add(m);
        views[item].UserTrash();
        // 保持期間を過ぎた過去日時に偽装する
        item.TrashedAt = DateTimeOffset.Now - TimeSpan.FromDays(31);
        m.SaveAll(); // 偽装した TrashedAt を index に反映

        var (m2, _) = NewManager(settings: new AppSettings { TrashRetentionDays = 30 }, store: NewStore());
        m2.RestoreFromDisk();

        Assert.Empty(m2.Items);
    }

    [Fact]
    public void 復元時に上限を超えていればトリミングされる()
    {
        // セッション間で MaxScraps を下げた状況を模す。まず上限ゆるめで 3 件 Stashed を保存
        var store = NewStore();
        var (m, views) = NewManager(settings: new AppSettings { MaxScraps = 10 }, store: store);
        var a = Add(m);
        var b = Add(m);
        var c = Add(m);
        views[a].UserStash();
        views[b].UserStash();
        views[c].UserStash();

        // 上限 2 で復元 → 最古の Stashed 1 件 (a) がトリミングされる
        var (m2, _) = NewManager(settings: new AppSettings { MaxScraps = 2 }, store: NewStore());
        m2.RestoreFromDisk();

        Assert.Equal(2, m2.Items.Count);
        Assert.DoesNotContain(m2.Items, i => i.Id == a.Id);
    }

    [Fact]
    public void 保持日数0なら期限切れ削除しない()
    {
        var store = NewStore();
        var (m, views) = NewManager(settings: new AppSettings { TrashRetentionDays = 0 }, store: store);
        var item = Add(m);
        views[item].UserTrash();
        item.TrashedAt = DateTimeOffset.Now - TimeSpan.FromDays(9999);
        m.SaveAll();

        var (m2, _) = NewManager(settings: new AppSettings { TrashRetentionDays = 0 }, store: NewStore());
        m2.RestoreFromDisk();

        Assert.Single(m2.Items); // 無期限保持
    }
}
