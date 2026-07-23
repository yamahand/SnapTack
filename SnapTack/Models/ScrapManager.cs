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

    // 全状態のスクラップを追加順に保持する。上限超過時の「古い順」判定に使う
    private readonly List<ScrapItem> _items = [];

    // 表示中の付箋ウィンドウ。1 スクラップにつき最大 1 つ (SPEC-v1.5 2.3)
    private readonly Dictionary<ScrapItem, IScrapView> _views = [];

    /// <summary>本番用。付箋ウィンドウとして <see cref="ScrapWindow"/> を生成する。</summary>
    public ScrapManager(SettingsService settings)
        : this(settings, item => new ScrapWindow(item, settings))
    {
    }

    /// <summary>ビューの生成方法を差し替えられるコンストラクタ (テスト用)。</summary>
    public ScrapManager(SettingsService settings, Func<ScrapItem, IScrapView> viewFactory)
    {
        _settings = settings;
        _viewFactory = viewFactory;
    }

    /// <summary>管理下の全スクラップ (追加順)。呼び出し側からの変更を防ぐ読み取り専用ビュー。</summary>
    public IReadOnlyList<ScrapItem> Items => _items.AsReadOnly();

    /// <summary>
    /// キャプチャ結果から新しいスクラップを作り、付箋 (Pinned) として画面に表示する。
    /// 現行のキャプチャ→付箋の体験をそのまま踏襲する。
    /// </summary>
    public ScrapItem Add(BitmapSource image, Int32Rect physicalRect)
    {
        var item = new ScrapItem(image, physicalRect); // 既定で Pinned
        _items.Add(item);
        ShowView(item);
        EnforceLimits();
        return item;
    }

    /// <summary>スクラップを画面に表示する (Pinned へ)。閉じていたウィンドウを開き直す用途も兼ねる。</summary>
    public void Show(ScrapItem item)
    {
        SetState(item, ScrapState.Pinned);
        ShowView(item);
        EnforceLimits();
    }

    /// <summary>スクラップをリストに隠す (Stashed へ)。ウィンドウは閉じるがデータは残す (SPEC-v1.5 2.3)。</summary>
    public void Stash(ScrapItem item)
    {
        SetState(item, ScrapState.Stashed);
        CloseView(item);
        // Trashed → Stashed はアクティブ (Pinned+Stashed) を増やすため上限を超え得る。
        // Pinned → Stashed では合計が変わらないが、区別せず一律にチェックする
        EnforceLimits();
    }

    /// <summary>スクラップをゴミ箱へ移す (Trashed へ)。破棄せず復元できる状態にする (SPEC-v1.5 2.3)。</summary>
    public void Trash(ScrapItem item)
    {
        SetState(item, ScrapState.Trashed);
        CloseView(item);
        EnforceLimits();
    }

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

    /// <summary>スクラップを完全に破棄する (コレクションから除去し、開いていれば閉じる)。</summary>
    private void Remove(ScrapItem item)
    {
        CloseView(item);
        _items.Remove(item);
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
}
