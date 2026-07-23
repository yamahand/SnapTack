using System.Windows;
using System.Windows.Media.Imaging;
using SnapTack.Views;

namespace SnapTack.Models;

/// <summary>
/// スクラップのコレクションと付箋ウィンドウのライフサイクルを一元管理する (SPEC-v1.5 3.1)。
/// これまで <see cref="ScrapWindow"/> は自己完結し App 側で参照を持たなかったが、
/// スクラップリストからの一括操作に中央管理が要るため本クラスに集約する。
/// </summary>
/// <remarks>
/// M13 は「挙動を変えないリファクタリング」。付箋を閉じたらこれまで同様データも破棄する。
/// 状態 (Trashed 等) の保持は M14、永続化は M16 で足す (MILESTONES-v1.5)。
/// </remarks>
public sealed class ScrapManager
{
    private readonly SettingsService _settings;

    // スクラップと、それを表示している付箋ウィンドウの対応。
    // 1 スクラップにつきウィンドウは最大 1 つ (SPEC-v1.5 2.3。二重表示防止は M14 で本格対応)
    private readonly Dictionary<ScrapItem, ScrapWindow> _windows = [];

    public ScrapManager(SettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// キャプチャ結果から新しいスクラップを作り、付箋として画面に表示する。
    /// 現行のキャプチャ→付箋の体験をそのまま踏襲する。
    /// </summary>
    public ScrapItem Add(BitmapSource image, Int32Rect physicalRect)
    {
        var item = new ScrapItem(image, physicalRect);
        ShowWindow(item);
        return item;
    }

    /// <summary>スクラップの付箋ウィンドウを生成して表示する。</summary>
    private void ShowWindow(ScrapItem item)
    {
        var window = new ScrapWindow(item, _settings);
        // Closed で対応を解除する。M13 では参照が消えるだけ = 従来どおりデータも失われる
        window.Closed += (_, _) => _windows.Remove(item);
        _windows[item] = window;
        window.Show();
    }
}
