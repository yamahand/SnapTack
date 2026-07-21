using System.Windows;
using System.Windows.Media.Imaging;
using SnapTack.Views;

namespace SnapTack.Capture;

/// <summary>
/// キャプチャ起動フロー(フリーズ → 範囲選択オーバーレイ → 選択結果)を管理する。
/// マルチモニタ化 (v1.3) で複数オーバーレイを扱えるよう、モーダルではなくイベント駆動にしている。
/// </summary>
public sealed class CaptureController
{
    private readonly IScreenCapturer _capturer;
    private readonly List<OverlayWindow> _overlays = [];
    private bool _closingAll;

    /// <summary>オーバーレイ表示中かどうか。表示中の再キャプチャ要求は無視される (SPEC 4.3)。</summary>
    public bool IsActive { get; private set; }

    /// <summary>選択確定時に発火する。引数は切り出した画像と物理ピクセル矩形。</summary>
    public event Action<BitmapSource, Int32Rect>? SelectionCompleted;

    public CaptureController(IScreenCapturer capturer)
    {
        _capturer = capturer;
    }

    /// <summary>
    /// キャプチャを開始する。実行中なら何もしない。
    /// キャプチャ失敗時は例外を投げる(呼び出し側で通知して継続する)。
    /// </summary>
    public void Start()
    {
        if (IsActive)
        {
            return;
        }

        // ホットキー押下の瞬間に全モニタをフリーズさせる (SPEC 4.2 / SPEC-v1.x 2.4)。
        // オーバーレイ生成前にすべてキャプチャし、途中失敗時は何も表示せず例外を伝播させる
        var monitors = _capturer.EnumerateMonitors();
        var screenshots = monitors.Select(m => (Monitor: m, Screenshot: _capturer.CaptureMonitor(m))).ToList();

        IsActive = true;
        OverlayWindow? cursorOverlay = null;
        var cursorPosition = System.Windows.Forms.Cursor.Position; // 物理px (仮想スクリーン座標)
        foreach (var (monitor, screenshot) in screenshots)
        {
            var overlay = new OverlayWindow(screenshot, monitor);
            overlay.Closed += OnOverlayClosed;
            _overlays.Add(overlay);
            var b = monitor.PhysicalBounds;
            if (cursorPosition.X >= b.X && cursorPosition.X < b.X + b.Width &&
                cursorPosition.Y >= b.Y && cursorPosition.Y < b.Y + b.Height)
            {
                cursorOverlay = overlay;
            }
        }
        foreach (var overlay in _overlays)
        {
            overlay.Show();
        }
        // Esc を受け取れるよう、カーソルのあるモニタのオーバーレイへフォーカスを与える
        (cursorOverlay ?? _overlays[0]).Activate();
    }

    /// <summary>
    /// どれか1つのオーバーレイが閉じたら(確定・キャンセルとも)残りも全て閉じ、
    /// 確定していれば結果を通知する。
    /// </summary>
    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        if (_closingAll)
        {
            return;
        }
        _closingAll = true;

        var closed = (OverlayWindow)sender!;
        foreach (var overlay in _overlays)
        {
            if (!ReferenceEquals(overlay, closed))
            {
                overlay.Close();
            }
        }
        _overlays.Clear();
        _closingAll = false;
        IsActive = false;

        if (closed.ResultImage is { } image)
        {
            SelectionCompleted?.Invoke(image, closed.ResultPhysicalRect);
        }
    }
}
