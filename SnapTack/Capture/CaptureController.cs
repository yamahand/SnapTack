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
    /// 範囲選択を挟まず、指定矩形 (物理px、仮想スクリーン座標) を切り出して
    /// <see cref="SelectionCompleted"/> を発火する (SPEC-v1.8 2.5。<c>/R:</c> オプション)。
    /// </summary>
    /// <remarks>
    /// 矩形が複数モニタにまたがる場合は、**左上位置のあるモニタ内へクランプ**する。
    /// モニタまたぎキャプチャは混在 DPI の正規化方針が未決のためスコープ外
    /// (SPEC-v1.x 2.4 の判断を維持)。クランプの結果が空になる場合は何もしない。
    /// キャプチャ失敗時は例外を投げる(呼び出し側で通知して継続する)。
    /// </remarks>
    public void CaptureRect(Int32Rect physicalRect)
    {
        // オーバーレイ表示中に横から切り出すと、フリーズ画像とオーバーレイの対応が
        // 崩れて紛らわしいため、通常のキャプチャ要求と同じく無視する (SPEC 4.3)
        if (IsActive)
        {
            return;
        }

        var monitors = _capturer.EnumerateMonitors();
        var monitor = FindMonitorFor(monitors, physicalRect);
        if (monitor is null)
        {
            return;
        }

        var bounds = monitor.PhysicalBounds;
        // モニタ原点からの相対座標へ直してからクランプする (切り出しは画像内座標のため)
        var relative = new Int32Rect(
            physicalRect.X - bounds.X, physicalRect.Y - bounds.Y,
            physicalRect.Width, physicalRect.Height);
        var clamped = RectMath.ClampToScreenshot(relative, bounds.Width, bounds.Height);
        if (clamped.Width <= 0 || clamped.Height <= 0)
        {
            return;
        }

        var screenshot = _capturer.CaptureMonitor(monitor);
        var cropped = new CroppedBitmap(screenshot, clamped);
        cropped.Freeze(); // ウィンドウ間で使い回すため (CLAUDE.md のコーディング規約)

        // 通知する矩形は仮想スクリーン座標へ戻す。スクラップは指定位置にそのまま重なる
        SelectionCompleted?.Invoke(cropped, new Int32Rect(
            clamped.X + bounds.X, clamped.Y + bounds.Y, clamped.Width, clamped.Height));
    }

    /// <summary>
    /// 指定矩形を担当するモニタを選ぶ。左上位置を含むモニタを優先し、
    /// どのモニタにも含まれなければ矩形と交差するモニタを使う。
    /// </summary>
    private static MonitorInfo? FindMonitorFor(IReadOnlyList<MonitorInfo> monitors, Int32Rect rect)
    {
        foreach (var monitor in monitors)
        {
            var b = monitor.PhysicalBounds;
            if (rect.X >= b.X && rect.X < b.X + b.Width &&
                rect.Y >= b.Y && rect.Y < b.Y + b.Height)
            {
                return monitor;
            }
        }

        // 左上がモニタ外 (モニタ間の隙間や画面外) でも、矩形の一部が映っていれば
        // その分だけ切り出す。交差が無ければ諦める
        foreach (var monitor in monitors)
        {
            var b = monitor.PhysicalBounds;
            if (rect.X < b.X + b.Width && rect.X + rect.Width > b.X &&
                rect.Y < b.Y + b.Height && rect.Y + rect.Height > b.Y)
            {
                return monitor;
            }
        }
        return null;
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
