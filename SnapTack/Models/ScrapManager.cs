using System.Windows;
using System.Windows.Media.Imaging;
using SnapTack.Views;

namespace SnapTack.Models;

/// <summary>
/// スクラップのコレクションと付箋ウィンドウのライフサイクルを一元管理する (SPEC-v1.5 3.1)。
/// 状態遷移 (Pinned/Stashed/Trashed)・二重表示防止・上限管理を担う (M14)。
/// </summary>
/// <remarks>
/// 付箋ウィンドウの生成は <see cref="IScrapView"/> ファクトリ越しに行う。実体は
/// <see cref="ScrapWindow"/>(STA・実ウィンドウ)だが、抽象を挟むことで本クラスの
/// コレクション管理・状態遷移ロジックをテスト可能にしている (PR #14 の指摘)。
/// 永続化 (ディスク保存・自動削除) は M16 で足す。
/// </remarks>
public sealed class ScrapManager
{
    private readonly SettingsService _settings;
    private readonly Func<ScrapItem, IScrapView> _viewFactory;
    private readonly ScrapStore? _store; // null なら永続化しない (テスト・一時運用)

    // 全状態のスクラップを追加順に保持する。上限超過時の「古い順」判定に使う
    private readonly List<ScrapItem> _items = [];

    // 表示中の付箋ウィンドウ。1 スクラップにつき最大 1 つ (SPEC-v1.5 2.3)
    private readonly Dictionary<ScrapItem, IScrapView> _views = [];

    /// <summary>本番用。付箋ウィンドウとして <see cref="ScrapWindow"/> を生成し、ディスクへ永続化する。</summary>
    public ScrapManager(SettingsService settings, ScrapStore store)
        : this(settings, item => new ScrapWindow(item, settings), store)
    {
    }

    /// <summary>ビューの生成方法・保存先を差し替えられるコンストラクタ (テスト用)。store は null 可。</summary>
    public ScrapManager(SettingsService settings, Func<ScrapItem, IScrapView> viewFactory, ScrapStore? store = null)
    {
        _settings = settings;
        _viewFactory = viewFactory;
        _store = store;
    }

    /// <summary>管理下の全スクラップ (追加順)。呼び出し側からの変更を防ぐ読み取り専用ビュー。</summary>
    public IReadOnlyList<ScrapItem> Items => _items.AsReadOnly();

    /// <summary>
    /// コレクションまたはいずれかのスクラップの状態が変化したときに発火する。
    /// 開いているスクラップリストが表示を更新するために購読する (M15)。
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>スクラップ (Pinned + Stashed) を追加順に返す。一覧の「スクラップ」タブ用。</summary>
    public IEnumerable<ScrapItem> ActiveScraps =>
        _items.Where(i => i.State is ScrapState.Pinned or ScrapState.Stashed);

    /// <summary>ゴミ箱 (Trashed) を追加順に返す。一覧の「ゴミ箱」タブ用。</summary>
    public IEnumerable<ScrapItem> TrashedScraps =>
        _items.Where(i => i.State == ScrapState.Trashed);

    /// <summary>スクラップが表示中 (付箋ウィンドウが開いている) かどうか。</summary>
    public bool IsShown(ScrapItem item) => _views.ContainsKey(item);

    /// <summary>
    /// キャプチャ結果から新しいスクラップを作り、付箋 (Pinned) として画面に表示する。
    /// 現行のキャプチャ→付箋の体験をそのまま踏襲する。
    /// </summary>
    public ScrapItem Add(BitmapSource image, Int32Rect physicalRect)
    {
        var item = new ScrapItem(image, physicalRect); // 既定で Pinned
        _items.Add(item);
        // 画像は追加時に 1 度だけ書く。以後は不透明度・サイコロ等が変わっても書き直さない (SPEC-v1.5 2.4)
        _store?.SaveImage(item);
        ShowView(item);
        EnforceLimits();
        SaveIndex();
        RaiseChanged();
        return item;
    }

    /// <summary>スクラップを画面に表示する (Pinned へ)。閉じていたウィンドウを開き直す用途も兼ねる。</summary>
    public void Show(ScrapItem item)
    {
        SetState(item, ScrapState.Pinned);
        ShowView(item);
        EnforceLimits();
        SaveIndex();
        RaiseChanged();
    }

    /// <summary>スクラップをリストに隠す (Stashed へ)。ウィンドウは閉じるがデータは残す (SPEC-v1.5 2.3)。</summary>
    public void Stash(ScrapItem item)
    {
        SetState(item, ScrapState.Stashed);
        CloseView(item);
        // Trashed → Stashed はアクティブ (Pinned+Stashed) を増やすため上限を超え得る。
        // Pinned → Stashed では合計が変わらないが、区別せず一律にチェックする
        EnforceLimits();
        SaveIndex();
        RaiseChanged();
    }

    /// <summary>スクラップをゴミ箱へ移す (Trashed へ)。破棄せず復元できる状態にする (SPEC-v1.5 2.3)。</summary>
    public void Trash(ScrapItem item)
    {
        SetState(item, ScrapState.Trashed);
        CloseView(item);
        EnforceLimits();
        SaveIndex();
        RaiseChanged();
    }

    /// <summary>スクラップを完全に削除する (ゴミ箱内での「完全に削除」。SPEC-v1.5 2.2)。</summary>
    public void Delete(ScrapItem item)
    {
        Remove(item);
        SaveIndex();
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>現在のスクラップ一覧を index.json へ書き出す (永続化が有効な場合のみ)。</summary>
    private void SaveIndex() => _store?.SaveIndex(_items);

    /// <summary>
    /// 状態を遷移させ、<see cref="ScrapItem.TrashedAt"/> の不変条件を保つ。
    /// Trashed へ入る時のみ日時を打ち、Trashed から離れる時は必ず null に戻す
    /// (Stashed など Trashed 以外では null であるべき。SPEC-v1.5 2.4 の自動削除判定が誤らないよう)。
    /// </summary>
    private static void SetState(ScrapItem item, ScrapState next)
    {
        item.TrashedAt = next == ScrapState.Trashed ? DateTimeOffset.Now : null;
        item.State = next;
    }

    /// <summary>ゴミ箱・退避中のスクラップを画面に戻す (Pinned へ)。<see cref="Show"/> と同義。</summary>
    public void Restore(ScrapItem item) => Show(item);

    /// <summary>ビューを表示する。既に表示中なら再生成せず最前面へ (SPEC-v1.5 2.3)。</summary>
    private void ShowView(ScrapItem item)
    {
        if (_views.TryGetValue(item, out var existing))
        {
            existing.Activate();
            return;
        }
        var view = _viewFactory(item);
        view.TrashRequested += (_, _) => Trash(item);
        view.StashRequested += (_, _) => Stash(item);
        // どの閉じ方 (ユーザー操作・Manager からの明示閉じ) でも対応を解除する
        view.Closed += (_, _) => _views.Remove(item);
        _views[item] = view;
        view.Show();
    }

    /// <summary>表示中ならビューを閉じる。<see cref="IScrapView.Closed"/> 経由で _views からも外れる。</summary>
    private void CloseView(ScrapItem item)
    {
        if (_views.TryGetValue(item, out var view))
        {
            view.Close();
        }
    }

    /// <summary>スクラップを完全に破棄する (コレクションと画像ファイルを消し、開いていれば閉じる)。</summary>
    private void Remove(ScrapItem item)
    {
        CloseView(item);
        _items.Remove(item);
        _store?.DeleteImage(item.Id); // 削除時は画像ファイルも消す (SPEC-v1.5 2.4)
    }

    /// <summary>
    /// 上限を超えた分を古いものから削除する (SPEC-v1.5 2.4)。
    /// Pinned は削除対象にしない。削除候補が Stashed だけで足りない場合は上限を超えて保持する。
    /// </summary>
    private void EnforceLimits()
    {
        // アクティブ (Pinned + Stashed): 超過分を古い Stashed から削除。Pinned は消さない
        int activeCount = _items.Count(i => i.State is ScrapState.Pinned or ScrapState.Stashed);
        int activeOver = activeCount - _settings.Current.MaxScraps;
        if (activeOver > 0)
        {
            foreach (var item in _items
                .Where(i => i.State == ScrapState.Stashed)
                .OrderBy(i => i.CapturedAt)
                .Take(activeOver)
                .ToList())
            {
                Remove(item);
            }
        }

        // ゴミ箱: 超過分を古い順 (TrashedAt) から削除
        var trashed = _items.Where(i => i.State == ScrapState.Trashed).ToList();
        int trashOver = trashed.Count - _settings.Current.MaxTrashedScraps;
        if (trashOver > 0)
        {
            foreach (var item in trashed
                .OrderBy(i => i.TrashedAt ?? i.CapturedAt)
                .Take(trashOver)
                .ToList())
            {
                Remove(item);
            }
        }
    }

    // ===== 起動時の復元・終了時の保存 (M16) =====

    /// <summary>
    /// ディスクからスクラップを読み込み、設定に応じて Pinned を画面へ復元する (SPEC-v1.5 2.4)。
    /// 起動時に 1 度だけ呼ぶ。ゴミ箱の期限切れ削除もここで行う。
    /// </summary>
    public void RestoreFromDisk()
    {
        if (_store is null)
        {
            return;
        }
        var restored = _store.Load(out bool indexNeedsRewrite);
        _items.Clear();
        _items.AddRange(restored);

        // 期限切れのゴミ箱を消す。この時点で index を書き直す (画像欠落分の除去も兼ねる)
        bool removedExpired = PurgeExpiredTrash();
        if (indexNeedsRewrite || removedExpired)
        {
            SaveIndex();
        }

        // Pinned のみ画面へ復元する。Stashed/Trashed の画像は一覧を開いた時に遅延読み込みされる
        if (_settings.Current.RestoreScrapsOnStartup)
        {
            foreach (var item in _items.Where(i => i.State == ScrapState.Pinned).ToList())
            {
                ShowView(item);
            }
        }
        RaiseChanged();
    }

    /// <summary>
    /// 保持日数を過ぎたゴミ箱を削除する (SPEC-v1.5 2.4)。TrashRetentionDays が 0 なら無期限保持。
    /// 起動時とスクラップリストを開いた時に呼ぶ。削除があれば true。
    /// </summary>
    public bool PurgeExpiredTrash()
    {
        int days = _settings.Current.TrashRetentionDays;
        if (days <= 0)
        {
            return false; // 0 (以下) は無期限保持 = 自動削除しない
        }
        var threshold = DateTimeOffset.Now - TimeSpan.FromDays(days);
        var expired = _items
            .Where(i => i.State == ScrapState.Trashed && i.TrashedAt is { } t && t < threshold)
            .ToList();
        foreach (var item in expired)
        {
            Remove(item);
        }
        return expired.Count > 0;
    }

    /// <summary>スクラップリストを開いた時に呼ぶ。期限切れゴミ箱を掃除し、変化があれば通知する。</summary>
    public void OnScrapListOpened()
    {
        if (PurgeExpiredTrash())
        {
            SaveIndex();
            RaiseChanged();
        }
    }

    /// <summary>
    /// 全スクラップの現在の状態 (特に表示位置) を index へ保存する。アプリ終了時に呼ぶ (SPEC-v1.5 2.6)。
    /// 表示中のウィンドウには最新位置を書き戻させてから保存する。
    /// </summary>
    public void SaveAll()
    {
        foreach (var view in _views.Values)
        {
            view.SaveStateToItem();
        }
        SaveIndex();
    }
}
