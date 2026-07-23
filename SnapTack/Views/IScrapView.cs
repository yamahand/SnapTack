using SnapTack.Models;

namespace SnapTack.Views;

/// <summary>
/// 付箋ウィンドウを <see cref="ScrapManager"/> から扱うための抽象。
/// 実体は <see cref="ScrapWindow"/>(STA・実ウィンドウ)だが、この抽象を挟むことで
/// Manager のコレクション管理・状態遷移ロジックをテスト可能にする (PR #14 の指摘)。
/// </summary>
public interface IScrapView
{
    /// <summary>表示しているスクラップ。</summary>
    ScrapItem Item { get; }

    /// <summary>ユーザーが「閉じる」(中クリック・メニュー) を要求した。ゴミ箱へ移す意図。</summary>
    event EventHandler? TrashRequested;

    /// <summary>ユーザーが「リストに隠す」を要求した。Stashed へ移す意図。</summary>
    event EventHandler? StashRequested;

    /// <summary>ウィンドウが閉じられた(プログラム由来を含む全ての閉じ)。</summary>
    event EventHandler? Closed;

    /// <summary>ウィンドウを表示する。</summary>
    void Show();

    /// <summary>ウィンドウを閉じる(Manager からの明示的な破棄)。</summary>
    void Close();

    /// <summary>ウィンドウを最前面へ持ってくる。戻り値は <see cref="System.Windows.Window.Activate"/> に準ずる。</summary>
    bool Activate();
}
