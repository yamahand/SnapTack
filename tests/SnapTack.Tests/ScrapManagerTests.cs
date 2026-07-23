using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapTack.Models;
using SnapTack.Views;
using Xunit;

namespace SnapTack.Tests;

/// <summary>スクラップの状態遷移・二重表示防止・上限管理 (SPEC-v1.5 2.1/2.3/2.4) の検証。</summary>
public class ScrapManagerTests
{
    /// <summary>実ウィンドウの代わりに使うテスト用ビュー。表示状態と発火した意図を記録する。</summary>
    private sealed class FakeView : IScrapView
    {
        public ScrapItem Item { get; }
        public bool IsOpen { get; private set; } = true;
        public int ActivateCount { get; private set; }

        public event EventHandler? TrashRequested;
        public event EventHandler? StashRequested;
        public event EventHandler? Closed;

        public FakeView(ScrapItem item) => Item = item;

        public void Show() => IsOpen = true;
        public bool Activate() { ActivateCount++; return true; }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Closed?.Invoke(this, EventArgs.Empty);
        }

        // 以下はユーザー操作の模擬
        public void UserTrash() => TrashRequested?.Invoke(this, EventArgs.Empty);
        public void UserStash() => StashRequested?.Invoke(this, EventArgs.Empty);
    }

    private static BitmapSource MakeImage() =>
        BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgr24, null, new byte[] { 0, 0, 0 }, 3);

    /// <summary>生成した FakeView を後から参照できる Manager を組み立てる。</summary>
    private static (ScrapManager Manager, Dictionary<ScrapItem, FakeView> Views) NewManager(AppSettings? settings = null)
    {
        var views = new Dictionary<ScrapItem, FakeView>();
        var service = new SettingsService(settings ?? new AppSettings());
        var manager = new ScrapManager(service, item =>
        {
            var view = new FakeView(item);
            views[item] = view;
            return view;
        });
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
}
