using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SnapTack.Interop;
using SnapTack.Models;

namespace SnapTack.Views;

/// <summary>
/// 付箋(スクラップ)ウィンドウ。
/// 生成後は自己完結し、App 側でのリスト管理は行わない (v2.0 のスクラップリスト実装時に再設計)。
/// </summary>
public partial class ScrapWindow : Window
{
    // UI 文字列 (将来の英語化を見据えて集約)
    private const string AppName = "SnapTack";
    private const string MenuCopyText = "コピー";
    private const string MenuCopyGestureText = "Ctrl+C";
    private const string MenuCloseText = "閉じる";
    private const string MenuCloseGestureText = "中クリック";
    private const string MenuSavePngText = "PNGで保存...";
    private const string MenuSavePngGestureText = "Ctrl+S";
    private const string ClipboardCopyFailedMessage =
        "クリップボードへのコピーに失敗しました。\n他のアプリがクリップボードを使用中の可能性があります。";
    private const string SavePngFailedMessage =
        "PNG の保存に失敗しました。\n保存先のアクセス権や空き容量を確認してください。";
    private const string SaveFileNameFormat = "SnapTack_{0:yyyyMMdd_HHmmss}";
    private const string SaveFileFilter = "PNG 画像 (*.png)|*.png";
    private const string MenuOpacityText = "不透明度";
    private const string OpacityPresetFormat = "{0}%";
    private const string MenuDiceText = "サイコロ化";
    private const string MenuRestoreText = "元に戻す";
    private const string MenuDiceGestureText = "ダブルクリック";

    // サイコロ (最小化タイル) のサイズ (SPEC-v1.x 2.3)
    private const double DiceSizeDip = 48.0;

    // 不透明度の範囲・ステップ (SPEC-v1.x 2.2)
    private const int OpacityMinPercent = 20;
    private const int OpacityMaxPercent = 100;
    private const int OpacityStepPercent = 10;
    private static readonly int[] OpacityPresets = [100, 75, 50, 25];

    private readonly BitmapSource _image;      // 物理ピクセル (Freeze 済み)
    private readonly Int32Rect _physicalRect;  // キャプチャ元の位置・サイズ (物理px、仮想スクリーン座標)
    private readonly SettingsService _settings;
    private readonly List<MenuItem> _opacityPresetItems = [];

    private int _opacityPercent = OpacityMaxPercent; // 新規付箋は常に 100% (SPEC-v1.x 2.2)
    private bool _isDice;
    private MenuItem? _diceMenuItem;
    private Size _dipSizeBeforeDice; // サイコロ化直前の DIP サイズ (WPF 既定モードでの復元用)

    public ScrapWindow(BitmapSource image, Int32Rect physicalRect, SettingsService settings)
    {
        InitializeComponent();
        _image = image;
        _physicalRect = physicalRect;
        _settings = settings;
        ScrapImage.Source = image;
        DiceBrush.ImageSource = image;
        ContextMenu = BuildContextMenu();
        SetOpacityPercent(_opacityPercent);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // キャプチャ元と同じ位置・等倍サイズで配置する (SPEC 4.4)。
        // 混在 DPI 環境でも正確に重なるよう、物理座標で直接配置する (SPEC-v1.x 2.4)
        var hwnd = new WindowInteropHelper(this).Handle;
        User32.SetWindowPos(hwnd, IntPtr.Zero,
            _physicalRect.X, _physicalRect.Y, _physicalRect.Width, _physicalRect.Height,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE);

        // 配置で確定したキャプチャ元モニタの DPI に合わせて DIP サイズを固定し、
        // WM_DPICHANGED による WPF 側の再配置で位置がずれないよう再固定する
        var dpi = VisualTreeHelper.GetDpi(this);
        Width = _physicalRect.Width / dpi.DpiScaleX;
        Height = _physicalRect.Height / dpi.DpiScaleY;
        User32.SetWindowPos(hwnd, IntPtr.Zero, _physicalRect.X, _physicalRect.Y, 0, 0,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
    }

    /// <summary>
    /// DPI の異なるモニタへ移動したときの挙動 (SPEC-v1.x 2.4)。
    /// 既定は WPF の既定動作 (DIP サイズ維持 = 再スケーリング) に任せ、
    /// KeepPhysicalPixelSize 有効時のみ物理ピクセル等倍を維持する。
    /// </summary>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        if (_isDice)
        {
            // サイコロは 48×48 DIP 固定 (SPEC-v1.x 2.3)
            return;
        }
        if (_settings.Current.KeepPhysicalPixelSize)
        {
            Width = _physicalRect.Width / newDpi.DpiScaleX;
            Height = _physicalRect.Height / newDpi.DpiScaleY;
        }
    }

    /// <summary>コンテキストメニュー: コピー / PNG保存 / 閉じる (SPEC 4.4 + SPEC-v1.x 2.1)。</summary>
    private ContextMenu BuildContextMenu()
    {
        var copyItem = new MenuItem { Header = MenuCopyText, InputGestureText = MenuCopyGestureText };
        copyItem.Click += (_, _) => CopyToClipboard();

        var savePngItem = new MenuItem { Header = MenuSavePngText, InputGestureText = MenuSavePngGestureText };
        savePngItem.Click += (_, _) => SaveAsPng();

        // 不透明度プリセット。現在値の項目にチェックを付ける (SPEC-v1.x 2.2)
        var opacityItem = new MenuItem { Header = MenuOpacityText };
        foreach (int percent in OpacityPresets)
        {
            var presetItem = new MenuItem { Header = string.Format(OpacityPresetFormat, percent), Tag = percent };
            presetItem.Click += (_, _) => SetOpacityPercent((int)presetItem.Tag);
            _opacityPresetItems.Add(presetItem);
            opacityItem.Items.Add(presetItem);
        }

        // サイコロ化 ⇔ 元に戻す (状態に応じて表記切替、SPEC-v1.x 2.3)
        _diceMenuItem = new MenuItem { Header = MenuDiceText, InputGestureText = MenuDiceGestureText };
        _diceMenuItem.Click += (_, _) => ToggleDice();

        var closeItem = new MenuItem { Header = MenuCloseText, InputGestureText = MenuCloseGestureText };
        closeItem.Click += (_, _) => Close();

        var menu = new ContextMenu();
        menu.Items.Add(copyItem);
        menu.Items.Add(savePngItem);
        menu.Items.Add(opacityItem);
        menu.Items.Add(_diceMenuItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(closeItem);
        return menu;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        SetOpacityPercent(NextOpacityPercent(_opacityPercent, e.Delta > 0 ? +1 : -1));
        e.Handled = true;
    }

    /// <summary>
    /// ホイール 1 ステップ後の不透明度を返す。
    /// 10% の倍数以外 (プリセット 75% / 25% 適用後) は最初の 1 ステップを ±5% として倍数へスナップする (SPEC-v1.x 2.2)。
    /// </summary>
    private static int NextOpacityPercent(int current, int direction)
    {
        int remainder = current % OpacityStepPercent;
        int next = remainder == 0
            ? current + direction * OpacityStepPercent
            : direction > 0 ? current + (OpacityStepPercent - remainder) : current - remainder;
        return Math.Clamp(next, OpacityMinPercent, OpacityMaxPercent);
    }

    private void SetOpacityPercent(int percent)
    {
        _opacityPercent = percent;
        // ウィンドウ全体に適用する。コピー / PNG 保存は _image を使うため影響を受けない (SPEC-v1.x 2.2)
        Opacity = percent / 100.0;
        foreach (var item in _opacityPresetItems)
        {
            item.IsChecked = (int)item.Tag == percent;
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // ダブルクリック判定はシステムのダブルクリック距離内に限られるため、
        // ドラッグ移動後のクリックは ClickCount=1 に戻り、移動と干渉しない (SPEC-v1.x 4)
        if (e.ClickCount == 2)
        {
            ToggleDice();
            return;
        }
        DragMove();
    }

    /// <summary>サイコロ (48×48 DIP タイル) ⇔ 元サイズをトグルする (SPEC-v1.x 2.3)。</summary>
    private void ToggleDice()
    {
        _isDice = !_isDice;
        if (_isDice)
        {
            // 左上位置は維持したままタイル化する
            _dipSizeBeforeDice = new Size(Width, Height);
            ScrapImage.Visibility = Visibility.Collapsed;
            DiceThumb.Visibility = Visibility.Visible;
            Width = DiceSizeDip;
            Height = DiceSizeDip;
        }
        else
        {
            ScrapImage.Visibility = Visibility.Visible;
            DiceThumb.Visibility = Visibility.Collapsed;
            if (_settings.Current.KeepPhysicalPixelSize)
            {
                // 現在のモニタ DPI で物理ピクセル等倍になるサイズへ戻す
                var dpi = VisualTreeHelper.GetDpi(this);
                Width = _physicalRect.Width / dpi.DpiScaleX;
                Height = _physicalRect.Height / dpi.DpiScaleY;
            }
            else
            {
                // WPF 既定モード: サイコロ化直前の DIP サイズへ戻す
                Width = _dipSizeBeforeDice.Width;
                Height = _dipSizeBeforeDice.Height;
            }
            ClampIntoCurrentMonitor();
        }
        if (_diceMenuItem is not null)
        {
            _diceMenuItem.Header = _isDice ? MenuRestoreText : MenuDiceText;
        }
    }

    /// <summary>付箋が画面外へはみ出す場合、表示中のモニタ内に収まるよう位置をクランプする (SPEC-v1.x 2.3)。</summary>
    private void ClampIntoCurrentMonitor()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }
        var bounds = System.Windows.Forms.Screen.FromHandle(hwnd).Bounds; // 物理px
        var dpi = VisualTreeHelper.GetDpi(this);
        double monitorLeft = bounds.X / dpi.DpiScaleX;
        double monitorTop = bounds.Y / dpi.DpiScaleY;
        double monitorRight = (bounds.X + bounds.Width) / dpi.DpiScaleX;
        double monitorBottom = (bounds.Y + bounds.Height) / dpi.DpiScaleY;
        Left = Math.Max(monitorLeft, Math.Min(Left, monitorRight - Width));
        Top = Math.Max(monitorTop, Math.Min(Top, monitorBottom - Height));
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            Close();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopyToClipboard();
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SaveAsPng();
            e.Handled = true;
        }
    }

    /// <summary>キャプチャ画像を物理ピクセル等倍の PNG として保存する (SPEC-v1.x 2.1)。</summary>
    private void SaveAsPng()
    {
        var dialog = new SaveFileDialog
        {
            FileName = string.Format(SaveFileNameFormat, DateTime.Now),
            DefaultExt = ".png",
            Filter = SaveFileFilter,
            InitialDirectory = GetInitialSaveDirectory(),
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_image));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExternalException)
        {
            // 失敗しても付箋は維持する
            MessageBox.Show(SavePngFailedMessage, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 次回のデフォルト保存先として記憶する。永続化失敗は保存自体には影響しないため通知しない
        _settings.Current.LastSaveDirectory = Path.GetDirectoryName(dialog.FileName);
        _settings.Save();
    }

    /// <summary>前回保存フォルダが有効ならそれを、なければ「ピクチャ」を返す。</summary>
    private string GetInitialSaveDirectory()
    {
        string? last = _settings.Current.LastSaveDirectory;
        if (!string.IsNullOrEmpty(last) && Directory.Exists(last))
        {
            return last;
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }

    private void CopyToClipboard()
    {
        try
        {
            Clipboard.SetImage(_image);
        }
        catch (ExternalException)
        {
            // 他プロセスがクリップボードをロックしていると失敗する
            MessageBox.Show(ClipboardCopyFailedMessage, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
