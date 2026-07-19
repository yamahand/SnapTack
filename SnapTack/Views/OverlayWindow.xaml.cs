using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnapTack.Views;

/// <summary>
/// 範囲選択オーバーレイ。
/// フリーズ画像を全画面に表示し、左ドラッグで選択・確定、Esc / 右クリックでキャンセルする。
/// ShowDialog() 後に <see cref="ResultImage"/> を参照する(キャンセル時は null)。
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
    private Point _dragStartDip;
    private bool _dragging;

    /// <summary>確定した選択範囲の画像 (Freeze 済み)。キャンセル時は null。</summary>
    public BitmapSource? ResultImage { get; private set; }

    /// <summary>確定した選択範囲 (物理ピクセル、プライマリモニタ左上原点)。</summary>
    public Int32Rect ResultPhysicalRect { get; private set; }

    public OverlayWindow(BitmapSource screenshot)
    {
        InitializeComponent();
        _screenshot = screenshot;
        FrozenImage.Source = screenshot;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Esc を受け取れるように前面化してフォーカスを取る
        Activate();
        Focus();
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
            UpdateSelectionVisual(new Rect(_dragStartDip, e.GetPosition(this)));
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

        // DIP → 物理ピクセルへ変換して切り出す
        var physicalRect = ToPhysicalRect(new Rect(_dragStartDip, e.GetPosition(this)));
        physicalRect = ClampToScreenshot(physicalRect);
        if (physicalRect.Width < MinSelectionPhysicalPx || physicalRect.Height < MinSelectionPhysicalPx)
        {
            // 誤クリック扱い
            Cancel();
            return;
        }

        var cropped = new CroppedBitmap(_screenshot, physicalRect);
        cropped.Freeze(); // 付箋ウィンドウ等へ渡すため
        ResultImage = cropped;
        ResultPhysicalRect = physicalRect;
        Close();
    }

    private void Cancel()
    {
        ResultImage = null;
        Close();
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

    /// <summary>DIP の矩形を物理ピクセルの矩形へ変換する。</summary>
    private Int32Rect ToPhysicalRect(Rect dipRect)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        // DIP → 物理px (端は四捨五入)
        int x = (int)Math.Round(dipRect.X * dpi.DpiScaleX);
        int y = (int)Math.Round(dipRect.Y * dpi.DpiScaleY);
        int right = (int)Math.Round(dipRect.Right * dpi.DpiScaleX);
        int bottom = (int)Math.Round(dipRect.Bottom * dpi.DpiScaleY);
        return new Int32Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }

    /// <summary>矩形をスクリーンショットの範囲内 (物理px) に収める。</summary>
    private Int32Rect ClampToScreenshot(Int32Rect rect)
    {
        int x = Math.Clamp(rect.X, 0, _screenshot.PixelWidth);
        int y = Math.Clamp(rect.Y, 0, _screenshot.PixelHeight);
        int right = Math.Clamp(rect.X + rect.Width, x, _screenshot.PixelWidth);
        int bottom = Math.Clamp(rect.Y + rect.Height, y, _screenshot.PixelHeight);
        return new Int32Rect(x, y, right - x, bottom - y);
    }
}
