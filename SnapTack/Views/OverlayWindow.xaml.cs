using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapTack.Capture;
using SnapTack.Interop;

namespace SnapTack.Views;

/// <summary>
/// 範囲選択オーバーレイ。1つのモニタ全面を覆う (モニタごとに1枚生成される)。
/// フリーズ画像を表示し、左ドラッグで選択・確定、Esc / 右クリックでキャンセルする。
/// Closed 後に <see cref="ResultImage"/> を参照する(キャンセル時は null)。
/// </summary>
public partial class OverlayWindow : Window
{
    // UI 文字列 (将来の英語化を見据えて集約)
    private const string SizeLabelFormat = "{0} × {1}";

    // これ未満 (物理px) のドラッグは誤クリックとしてキャンセル扱い (SPEC 4.3)
    private const int MinSelectionPhysicalPx = 5;

    // サイズラベルと選択枠の間隔 (DIP)
    private const double LabelMargin = 4.0;

    private readonly BitmapSource _screenshot; // 物理ピクセル
    private readonly MonitorInfo _monitor;     // このオーバーレイが覆うモニタ
    private Point _dragStartDip;
    private bool _dragging;

    /// <summary>確定した選択範囲の画像 (Freeze 済み)。キャンセル時は null。</summary>
    public BitmapSource? ResultImage { get; private set; }

    /// <summary>確定した選択範囲 (物理ピクセル、仮想スクリーン座標)。</summary>
    public Int32Rect ResultPhysicalRect { get; private set; }

    public OverlayWindow(BitmapSource screenshot, MonitorInfo monitor)
    {
        InitializeComponent();
        _screenshot = screenshot;
        _monitor = monitor;
        FrozenImage.Source = screenshot;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // 混在 DPI 環境でも正確にモニタ全面を覆うため、物理座標で直接配置する
        var hwnd = new WindowInteropHelper(this).Handle;
        var bounds = _monitor.PhysicalBounds;
        bool placed = User32.SetWindowPos(hwnd, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE);

        var dpi = VisualTreeHelper.GetDpi(this);
        if (placed)
        {
            // 配置で確定したモニタ DPI に合わせて DIP サイズを固定し、
            // WM_DPICHANGED による WPF 側の再配置で位置がずれないよう再固定する
            Width = bounds.Width / dpi.DpiScaleX;
            Height = bounds.Height / dpi.DpiScaleY;
            User32.SetWindowPos(hwnd, IntPtr.Zero, bounds.X, bounds.Y, 0, 0,
                User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
        }
        else
        {
            // 物理座標での配置に失敗した場合は WPF の DIP 配置へフォールバックし、
            // オーバーレイがモニタ全面を覆えず操作不能になるのを防ぐ
            Left = bounds.X / dpi.DpiScaleX;
            Top = bounds.Y / dpi.DpiScaleY;
            Width = bounds.Width / dpi.DpiScaleX;
            Height = bounds.Height / dpi.DpiScaleY;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 初期のアクティブ化 (どのモニタのオーバーレイをフォーカスするか) は CaptureController が
        // 全オーバーレイ表示後に一元的に行う。ここで Activate() すると Loaded の発火順によって
        // 別モニタのオーバーレイがフォーカスを奪い得るため、キーボードフォーカスの取得だけ行う
        Focus();
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        // マウスのあるモニタのオーバーレイが Esc を受け取れるよう、フォーカスを追従させる
        if (!IsActive)
        {
            Activate();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 選択前は全面を薄暗くする (SelectionGeometry は空)
        FullGeometry.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cancel();
        }
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        Cancel();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartDip = e.GetPosition(this);
        _dragging = true;
        CaptureMouse();

        SelectionBorder.Visibility = Visibility.Visible;
        SizeLabel.Visibility = Visibility.Visible;
        UpdateSelectionVisual(new Rect(_dragStartDip, _dragStartDip));
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging)
        {
            // マウスキャプチャ中はカーソルが他モニタへ出ても座標が届くため、
            // ウィンドウ内へクランプして見た目を確定結果と一致させる (SPEC-v1.x 2.4)
            UpdateSelectionVisual(CurrentSelectionDipRect(e.GetPosition(this)));
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        ReleaseMouseCapture();

        // ドラッグ中と同じクランプ済み矩形から切り出す。ClampToScreenshot は
        // 物理ピクセルへの丸め誤差でわずかに範囲外へ出た場合の保険 (SPEC-v1.x 2.4)
        var localRect = ToPhysicalRect(CurrentSelectionDipRect(e.GetPosition(this)));
        localRect = ClampToScreenshot(localRect);
        if (localRect.Width < MinSelectionPhysicalPx || localRect.Height < MinSelectionPhysicalPx)
        {
            // 誤クリック扱い
            Cancel();
            return;
        }

        var cropped = new CroppedBitmap(_screenshot, localRect);
        cropped.Freeze(); // 付箋ウィンドウ等へ渡すため
        ResultImage = cropped;
        // モニタ内ローカル座標 → 仮想スクリーン座標
        ResultPhysicalRect = new Int32Rect(
            _monitor.PhysicalBounds.X + localRect.X,
            _monitor.PhysicalBounds.Y + localRect.Y,
            localRect.Width,
            localRect.Height);
        Close();
    }

    private void Cancel()
    {
        ResultImage = null;
        Close();
    }

    /// <summary>
    /// ドラッグ開始点から現在のカーソル位置までの選択矩形 (DIP) を返す。
    /// 現在位置はこのオーバーレイ (= 選択中モニタ) の範囲内へクランプする。
    /// </summary>
    private Rect CurrentSelectionDipRect(Point currentDip)
    {
        double x = Math.Clamp(currentDip.X, 0, ActualWidth);
        double y = Math.Clamp(currentDip.Y, 0, ActualHeight);
        return new Rect(_dragStartDip, new Point(x, y));
    }

    /// <summary>ドラッグ中の選択枠・くり抜き・サイズラベルを更新する。引数は DIP。</summary>
    private void UpdateSelectionVisual(Rect dipRect)
    {
        SelectionGeometry.Rect = dipRect;

        Canvas.SetLeft(SelectionBorder, dipRect.X);
        Canvas.SetTop(SelectionBorder, dipRect.Y);
        SelectionBorder.Width = dipRect.Width;
        SelectionBorder.Height = dipRect.Height;

        // ラベルは物理ピクセル表記 (SPEC 4.3)
        var physicalRect = ToPhysicalRect(dipRect);
        SizeText.Text = string.Format(SizeLabelFormat, physicalRect.Width, physicalRect.Height);

        // ラベルは選択範囲の左上外側。画面上端にかかる場合は内側に出す
        SizeLabel.UpdateLayout();
        double labelX = Math.Clamp(dipRect.X, 0, Math.Max(0, ActualWidth - SizeLabel.ActualWidth));
        double labelY = dipRect.Y - SizeLabel.ActualHeight - LabelMargin;
        if (labelY < 0)
        {
            labelY = dipRect.Y + LabelMargin;
        }
        Canvas.SetLeft(SizeLabel, labelX);
        Canvas.SetTop(SizeLabel, labelY);
    }

    /// <summary>DIP の矩形をスクリーンショットの物理ピクセル矩形へ変換する。</summary>
    private Int32Rect ToPhysicalRect(Rect dipRect) =>
        RectMath.ToPhysicalRect(
            dipRect, ActualWidth, ActualHeight, _screenshot.PixelWidth, _screenshot.PixelHeight);

    /// <summary>矩形をスクリーンショットの範囲内 (物理px) に収める。</summary>
    private Int32Rect ClampToScreenshot(Int32Rect rect) =>
        RectMath.ClampToScreenshot(rect, _screenshot.PixelWidth, _screenshot.PixelHeight);
}
