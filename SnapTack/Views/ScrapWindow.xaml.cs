using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
    private const string ClipboardCopyFailedMessage =
        "クリップボードへのコピーに失敗しました。\n他のアプリがクリップボードを使用中の可能性があります。";

    private readonly BitmapSource _image;      // 物理ピクセル (Freeze 済み)
    private readonly Int32Rect _physicalRect;  // キャプチャ元の位置・サイズ (物理px、プライマリモニタ左上原点)

    public ScrapWindow(BitmapSource image, Int32Rect physicalRect)
    {
        InitializeComponent();
        _image = image;
        _physicalRect = physicalRect;
        ScrapImage.Source = image;
        ContextMenu = BuildContextMenu();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // 物理px → DIP に変換し、キャプチャ元と同じ位置・等倍サイズで配置する (SPEC 4.4)
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = _physicalRect.X / dpi.DpiScaleX;
        Top = _physicalRect.Y / dpi.DpiScaleY;
        Width = _physicalRect.Width / dpi.DpiScaleX;
        Height = _physicalRect.Height / dpi.DpiScaleY;
    }

    /// <summary>コンテキストメニュー: コピー / 閉じる の2項目+セパレータのみ (SPEC 4.4)。</summary>
    private ContextMenu BuildContextMenu()
    {
        var copyItem = new MenuItem { Header = MenuCopyText, InputGestureText = MenuCopyGestureText };
        copyItem.Click += (_, _) => CopyToClipboard();

        var closeItem = new MenuItem { Header = MenuCloseText, InputGestureText = MenuCloseGestureText };
        closeItem.Click += (_, _) => Close();

        var menu = new ContextMenu();
        menu.Items.Add(copyItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(closeItem);
        return menu;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
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
