namespace SnapTack.Models;

/// <summary>
/// アプリ全体で共有する現在の設定と、その保存手段。
/// 付箋ウィンドウなど App 以外からも設定の参照・更新を行うために用いる。
/// </summary>
public sealed class SettingsService
{
    private readonly SettingsStore _store;

    /// <summary>現在有効な設定。設定画面での保存時は <see cref="Replace"/> で差し替わる。</summary>
    public AppSettings Current { get; private set; }

    public SettingsService(SettingsStore store)
    {
        _store = store;
        Current = store.Load();
    }

    /// <summary>現在の設定を永続化する。失敗時は false(呼び出し側で通知の要否を判断する)。</summary>
    public bool Save() => _store.Save(Current);

    /// <summary>設定を差し替えて永続化する。</summary>
    public bool Replace(AppSettings settings)
    {
        Current = settings;
        return Save();
    }
}
