namespace SnapTack.Models;

/// <summary>
/// スクラップの状態 (SPEC-v1.5 2.1)。3 状態で確定 (SPEC-v1.5 6)。
/// </summary>
public enum ScrapState
{
    /// <summary>画面に貼られている。リストにも表示する。</summary>
    Pinned,

    /// <summary>リストにのみ存在する(画面から退避)。「隠す」で遷移する。</summary>
    Stashed,

    /// <summary>閉じた(ゴミ箱)。中クリック・「閉じる」で遷移する。破棄はまだしない。</summary>
    Trashed,
}
