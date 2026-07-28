using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapTack.Interop;
using SnapTack.Models;
using SnapTack.Resources;

namespace SnapTack.Views;

/// <summary>
/// 付箋(スクラップ)ウィンドウ。<see cref="ScrapItem"/> を表示するビュー。
/// 生成・破棄のライフサイクルは <see cref="Models.ScrapManager"/> が管理する (SPEC-v1.5 3.1)。
/// </summary>
public partial class ScrapWindow : Window, IScrapView
{
    /// <summary>ユーザーが「閉じる」(中クリック・メニュー) を要求した。ゴミ箱へ移す意図 (SPEC-v1.5 2.3)。</summary>
    public event EventHandler? TrashRequested;

    /// <summary>ユーザーが「リストに隠す」を要求した。Stashed へ移す意図 (SPEC-v1.5 2.3)。</summary>
    public event EventHandler? StashRequested;

    /// <summary>
    /// ユーザーが付箋上で Ctrl+V を押した (SPEC-v1.6 3.5)。引数は新しい付箋を出す
    /// 左上位置 (物理px)。実際の生成は <see cref="Models.ScrapManager"/> が行う。
    /// </summary>
    public event EventHandler<Point>? PasteRequested;

    /// <summary>
    /// ユーザーが編集を適用した (SPEC-v1.7 5)。永続化は <see cref="Models.ScrapManager"/> が行う。
    /// </summary>
    public event EventHandler? EditApplied;

    // 言語非依存の文字列。翻訳対象は Resources/Strings.resx を参照
    private const string AppName = "SnapTack";
    private const string OpacityPresetFormat = "{0}%";

    // サイコロ (最小化タイル) のサイズ (SPEC-v1.x 2.3)
    private const double DiceSizeDip = 48.0;

    // Ctrl+V で作る付箋を元の付箋からずらす量 (物理px)。重ねると置き換えたように
    // 見えるため、少しずらして「隣に増えた」と分かるようにする (SPEC-v1.6 3.5)
    private const int PasteOffsetPx = 24;

    // 不透明度の範囲・ステップ (SPEC-v1.x 2.2) は OpacityLevel が持つ
    private const int OpacityMaxPercent = OpacityLevel.MaxPercent;
    private static readonly int[] OpacityPresets = [100, 75, 50, 25];

    // 拡大縮小プリセット (SPEC-v1.7 2.2)。不透明度と同じくチェック表示で現在値を示す
    private static readonly int[] ScalePresets = [400, 200, 100, 75, 50, 25];

    // キー移動の刻み (物理px、SPEC-v1.7 2.5)。設定化はせず定数で始める (SPEC-v1.7 3)
    private const int MoveStepPx = 1;
    private const int MoveStepLargePx = 50;

    // 移動値表示を消すまでの時間。常時表示すると画像を隠すため (SPEC-v1.7 2.5)
    private static readonly TimeSpan PositionReadoutDuration = TimeSpan.FromSeconds(1);

    // 移動値の表示形式。言語非依存なので const のままでよい (CLAUDE.md の i18n 方針)
    private const string PositionFormat = "{0}, {1}";

    // トリムの極小選択をキャンセル扱いにするしきい値 (DIP)。誤クリック対策 (SPEC-v1.7 2.4)
    private const double MinTrimSizeDip = 4.0;

    private readonly BitmapSource _image;      // 物理ピクセル (Freeze 済み)。編集前の元画像
    private readonly Int32Rect _physicalRect;  // キャプチャ元の位置・サイズ (物理px、仮想スクリーン座標)
    private readonly ImageSaver _saver;        // 保存処理 (形式選択・即保存。SPEC-v1.6 2)
    private readonly List<MenuItem> _opacityPresetItems = [];
    private readonly List<MenuItem> _scalePresetItems = [];

    /// <summary>このウィンドウが表示しているスクラップ。<see cref="Models.ScrapManager"/> が識別に使う。</summary>
    public ScrapItem Item { get; }

    // Manager が明示的に閉じる時だけ true。OS/ユーザー由来の閉じ (Alt+F4 等) と区別する
    private bool _closingByManager;

    private int _opacityPercent = OpacityMaxPercent; // 新規付箋は常に 100% (SPEC-v1.x 2.2)
    private bool _isDice;
    private MenuItem? _diceMenuItem;
    private Size _dipSizeBeforeDice; // サイコロ化直前の DIP サイズ (復元用)

    // 編集関連 (SPEC-v1.7)。編集メニューはサイコロ中に無効化するため参照を持つ
    private MenuItem? _scaleMenuItem;
    private MenuItem? _rotateFlipMenuItem;
    private MenuItem? _trimMenuItem;
    private MenuItem? _resetEditMenuItem;
    private MenuItem? _flipHorizontalItem;
    private MenuItem? _flipVerticalItem;

    // トリムモード (SPEC-v1.7 2.4)。true の間は移動・サイコロ化・不透明度変更を受け付けない
    private bool _isTrimming;
    private Point? _trimStartDip;
    private Rect _trimSelectionDip;

    // 移動値表示を消すタイマー (SPEC-v1.7 2.5)
    private System.Windows.Threading.DispatcherTimer? _positionReadoutTimer;

    // 左ボタン押下位置 (DIP)。しきい値を超えて動いたら初めて DragMove を開始する。
    // これにより「移動を伴わないクリック」のみがダブルクリック判定に残る (SPEC-v1.x 4)
    private Point? _leftButtonDownDip;

    public ScrapWindow(ScrapItem item, SettingsService settings)
    {
        InitializeComponent();
        Item = item;
        _image = item.Image;
        _physicalRect = item.PhysicalRect;
        _saver = new ImageSaver(settings);
        ContextMenu = BuildContextMenu();
        // 復元されたスクラップは保存済みの不透明度を引き継ぐ (新規は既定 100%)
        _opacityPercent = item.OpacityPercent;
        SetOpacityPercent(_opacityPercent);
        // 編集済みで復元された場合もここで反映される (編集なしなら元画像がそのまま入る)
        RefreshEditedImage();
    }

    /// <summary>
    /// 編集適用後の画像を表示へ反映し、メニューのチェック状態を更新する (SPEC-v1.7 2.8)。
    /// ウィンドウサイズの追従は <see cref="ApplyEdit"/> が行う (復元時は配置側が持つため分けている)。
    /// </summary>
    private void RefreshEditedImage()
    {
        var edited = Item.EditedImage;
        ScrapImage.Source = edited;
        DiceBrush.ImageSource = edited; // サムネイルも編集結果を出す (SPEC-v1.7 2.8)
        UpdateEditMenuState();
    }

    /// <summary>
    /// Manager からの明示的な閉じ。フラグを立ててから閉じることで、
    /// <see cref="OnClosing"/> がこれを「ユーザー由来の閉じ」と誤認しないようにする。
    /// </summary>
    void IScrapView.Close()
    {
        _closingByManager = true;
        Close();
    }

    /// <summary>
    /// OS/ユーザー由来の閉じ (Alt+F4、タスク一覧からの閉じ等) をゴミ箱行きへ委譲する (SPEC-v1.5 2.3)。
    /// これらは意図イベントを経由しないため、放置すると State が Pinned のまま表示だけ消え、
    /// 永続化 (M16) 時に「Pinned なのに窓が無い」不整合になる。Manager 由来の閉じはそのまま通す。
    /// ただしアプリ終了中は委譲するとシャットダウンをブロックするため、そのまま閉じる (SPEC-v1.5 2.6)。
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_closingByManager && !App.IsShuttingDown)
        {
            e.Cancel = true;
            TrashRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        // 実際に閉じる直前に現在の表示状態を Item へ書き戻す。位置はここ (閉じる時) と
        // アプリ終了時にだけ保存する。ドラッグのたびには保存しない (SPEC-v1.5 2.4)
        SaveStateToItem();
        base.OnClosing(e);
    }

    /// <summary>現在の不透明度・サイコロ状態・表示位置 (物理px) を <see cref="Item"/> へ書き戻す。</summary>
    public void SaveStateToItem()
    {
        Item.OpacityPercent = _opacityPercent;
        Item.IsDice = _isDice;
        if (TryGetPhysicalPosition(out var position))
        {
            Item.WindowPosition = position;
        }
    }

    /// <summary>ウィンドウの現在の左上位置を物理px (仮想スクリーン座標) で取得する。</summary>
    private bool TryGetPhysicalPosition(out Point position)
    {
        position = default;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !User32.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }
        position = new Point(rect.Left, rect.Top);
        return true;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // 復元されたスクラップは前回の表示位置を優先し、なければキャプチャ元と同じ位置に置く。
        // 混在 DPI 環境でも正確に重なるよう、物理座標で直接配置する (SPEC 4.4 / SPEC-v1.x 2.4)
        int posX = Item.WindowPosition is { } p ? (int)p.X : _physicalRect.X;
        int posY = Item.WindowPosition is { } q ? (int)q.Y : _physicalRect.Y;

        // 編集済みで復元された場合は編集後のサイズで出す。編集なしならキャプチャ元と同じ
        // (等倍は「不変条件」ではなく「既定値」になった。SPEC-v1.7 1)
        var (sizeX, sizeY) = Item.EditedPixelSize;

        var hwnd = new WindowInteropHelper(this).Handle;
        bool placed = User32.SetWindowPos(hwnd, IntPtr.Zero,
            posX, posY, sizeX, sizeY,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE);

        var dpi = VisualTreeHelper.GetDpi(this);
        if (placed)
        {
            // 配置で確定したキャプチャ元モニタの DPI に合わせて DIP サイズを固定し、
            // WM_DPICHANGED による WPF 側の再配置で位置がずれないよう再固定する
            Width = sizeX / dpi.DpiScaleX;
            Height = sizeY / dpi.DpiScaleY;
            User32.SetWindowPos(hwnd, IntPtr.Zero, posX, posY, 0, 0,
                User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
        }
        else
        {
            // 物理座標での配置に失敗した場合は WPF の DIP 配置へフォールバックする。
            // 混在 DPI では厳密には重ならないが、付箋を確実に画面へ出すことを優先する
            Left = posX / dpi.DpiScaleX;
            Top = posY / dpi.DpiScaleY;
            Width = sizeX / dpi.DpiScaleX;
            Height = sizeY / dpi.DpiScaleY;
        }

        // 保存済みのモニタ構成と変わり画面外に出る場合は、表示中のモニタ内へクランプする
        // (サイコロ復元と同じ処理。SPEC-v1.5 2.4)
        if (Item.WindowPosition is not null)
        {
            ClampIntoCurrentMonitor();
        }

        // サイコロ状態で保存されていれば、その姿で復元する
        if (Item.IsDice && !_isDice)
        {
            ToggleDice();
        }
    }

    /// <summary>
    /// コンテキストメニュー: コピー / 保存 / すぐ保存 / 隠す / 閉じる
    /// (SPEC 4.4 + SPEC-v1.5 2.3 + SPEC-v1.6 2.2)。
    /// </summary>
    private ContextMenu BuildContextMenu()
    {
        var copyItem = new MenuItem { Header = Strings.MenuCopyText, InputGestureText = Strings.MenuCopyGestureText };
        copyItem.Click += (_, _) => CopyToClipboard();

        var savePngItem = new MenuItem { Header = Strings.MenuSavePngText, InputGestureText = Strings.MenuSavePngGestureText };
        savePngItem.Click += (_, _) => SaveWithDialog();

        // ダイアログ無しで設定フォルダへ日時名保存する (SPEC-v1.6 2.2)
        var quickSaveItem = new MenuItem { Header = Strings.MenuQuickSaveText, InputGestureText = Strings.MenuQuickSaveGestureText };
        quickSaveItem.Click += (_, _) => QuickSave();

        // クリップボードの画像を新しい付箋にする (SPEC-v1.6 3.5)。
        // キーだけだと気付かれにくいのでメニューにも出す
        var pasteItem = new MenuItem { Header = Strings.MenuPasteScrapText, InputGestureText = Strings.MenuPasteScrapGestureText };
        pasteItem.Click += (_, _) => RequestPaste();

        // 不透明度プリセット。現在値の項目にチェックを付ける (SPEC-v1.x 2.2)
        var opacityItem = new MenuItem { Header = Strings.MenuOpacityText };
        foreach (int percent in OpacityPresets)
        {
            var presetItem = new MenuItem { Header = string.Format(OpacityPresetFormat, percent), Tag = percent };
            presetItem.Click += (_, _) => SetOpacityPercent((int)presetItem.Tag);
            _opacityPresetItems.Add(presetItem);
            opacityItem.Items.Add(presetItem);
        }

        // ===== 編集 (SPEC-v1.7 2) =====

        // 拡大縮小プリセット。現在値の項目にチェックを付ける (不透明度と同じ形式。SPEC-v1.7 2.2)
        _scaleMenuItem = new MenuItem { Header = Strings.MenuScaleText };
        foreach (int percent in ScalePresets)
        {
            var presetItem = new MenuItem { Header = string.Format(OpacityPresetFormat, percent), Tag = percent };
            presetItem.Click += (_, _) => SetScalePercent((int)presetItem.Tag);
            _scalePresetItems.Add(presetItem);
            _scaleMenuItem.Items.Add(presetItem);
        }

        // 回転・反転 (SPEC-v1.7 2.3)。反転はキーを割り当てずメニューのみ
        var rotateRightItem = new MenuItem
        {
            Header = Strings.MenuRotateRightText,
            InputGestureText = Strings.MenuRotateRightGestureText,
        };
        rotateRightItem.Click += (_, _) => RotateBy(90);
        var rotateLeftItem = new MenuItem
        {
            Header = Strings.MenuRotateLeftText,
            InputGestureText = Strings.MenuRotateLeftGestureText,
        };
        rotateLeftItem.Click += (_, _) => RotateBy(-90);
        _flipHorizontalItem = new MenuItem { Header = Strings.MenuFlipHorizontalText };
        _flipHorizontalItem.Click += (_, _) => ApplyEdit(Item.Edit.ToggleFlipHorizontal());
        _flipVerticalItem = new MenuItem { Header = Strings.MenuFlipVerticalText };
        _flipVerticalItem.Click += (_, _) => ApplyEdit(Item.Edit.ToggleFlipVertical());

        _rotateFlipMenuItem = new MenuItem { Header = Strings.MenuRotateFlipText };
        _rotateFlipMenuItem.Items.Add(rotateRightItem);
        _rotateFlipMenuItem.Items.Add(rotateLeftItem);
        _rotateFlipMenuItem.Items.Add(new Separator());
        _rotateFlipMenuItem.Items.Add(_flipHorizontalItem);
        _rotateFlipMenuItem.Items.Add(_flipVerticalItem);

        // トリム (SPEC-v1.7 2.4)。モードに入るだけで、確定は Enter
        _trimMenuItem = new MenuItem { Header = Strings.MenuTrimText, InputGestureText = Strings.MenuTrimGestureText };
        _trimMenuItem.Click += (_, _) => BeginTrim();

        // 編集をリセット (SPEC-v1.7 2.6)。トリムし過ぎた場合の唯一の復旧手段でもある
        _resetEditMenuItem = new MenuItem { Header = Strings.MenuResetEditText };
        _resetEditMenuItem.Click += (_, _) => ApplyEdit(ScrapEdit.Default);

        // サイコロ化 ⇔ 元に戻す (状態に応じて表記切替、SPEC-v1.x 2.3)
        _diceMenuItem = new MenuItem { Header = Strings.MenuDiceText, InputGestureText = Strings.MenuDiceGestureText };
        _diceMenuItem.Click += (_, _) => ToggleDice();

        // リストに隠す (Stashed へ)。データは残し、ウィンドウだけ閉じる (SPEC-v1.5 2.3)
        var stashItem = new MenuItem { Header = Strings.MenuStashText };
        stashItem.Click += (_, _) => StashRequested?.Invoke(this, EventArgs.Empty);

        // 閉じる = ゴミ箱へ。破棄ではなく Trashed へ移す (SPEC-v1.5 2.3)。
        // 実際にウィンドウを閉じるのは Manager 側 (状態を確定してから閉じる)
        var closeItem = new MenuItem { Header = Strings.MenuCloseText, InputGestureText = Strings.MenuCloseGestureText };
        closeItem.Click += (_, _) => TrashRequested?.Invoke(this, EventArgs.Empty);

        var menu = new ContextMenu();
        menu.Items.Add(copyItem);
        menu.Items.Add(pasteItem);
        menu.Items.Add(savePngItem);
        menu.Items.Add(quickSaveItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_scaleMenuItem);
        menu.Items.Add(_rotateFlipMenuItem);
        menu.Items.Add(_trimMenuItem);
        menu.Items.Add(_resetEditMenuItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(opacityItem);
        menu.Items.Add(_diceMenuItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(stashItem);
        menu.Items.Add(closeItem);
        return menu;
    }

    /// <summary>
    /// 編集メニューのチェック・有効状態を現在の <see cref="ScrapItem.Edit"/> に合わせる。
    /// サイコロ中は編集できないため淡色表示にする (SPEC-v1.7 2.10)。
    /// </summary>
    private void UpdateEditMenuState()
    {
        var edit = Item.Edit;
        foreach (var item in _scalePresetItems)
        {
            item.IsChecked = (int)item.Tag == edit.ScalePercent;
        }
        if (_flipHorizontalItem is not null)
        {
            _flipHorizontalItem.IsChecked = edit.FlipHorizontal;
        }
        if (_flipVerticalItem is not null)
        {
            _flipVerticalItem.IsChecked = edit.FlipVertical;
        }
        // サイコロ中の編集は 48×48 のタイルに対して意味を持たず UI も破綻する (SPEC-v1.7 2.10)
        bool canEdit = !_isDice;
        if (_scaleMenuItem is not null) _scaleMenuItem.IsEnabled = canEdit;
        if (_rotateFlipMenuItem is not null) _rotateFlipMenuItem.IsEnabled = canEdit;
        if (_trimMenuItem is not null) _trimMenuItem.IsEnabled = canEdit;
        // 編集が無いならリセットする対象も無い (SPEC-v1.7 2.6)
        if (_resetEditMenuItem is not null) _resetEditMenuItem.IsEnabled = canEdit && !edit.IsDefault;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_isTrimming)
        {
            return; // トリムモード中は不透明度変更を受け付けない (SPEC-v1.7 2.4)
        }
        // 複数ノッチ入力 (Delta = ±240 等) はノッチ数ぶんステップを適用する。
        // 1 ステップずつ進めることで、倍数へのスナップも各ステップで正しく効く
        int notches = e.Delta / Mouse.MouseWheelDeltaForOneLine;
        int direction = Math.Sign(notches);
        int percent = _opacityPercent;
        for (int i = 0; i < Math.Abs(notches); i++)
        {
            percent = OpacityLevel.Next(percent, direction);
        }
        SetOpacityPercent(percent);
        e.Handled = true;
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

    // ===== 編集 (SPEC-v1.7 2) =====

    /// <summary>
    /// 編集を差し替えて表示へ反映する (SPEC-v1.7 2.1)。
    /// 左上位置は維持したまま、編集後のサイズへウィンドウを合わせる。
    /// </summary>
    /// <remarks>
    /// 非破壊なので元画像には触れない。<see cref="ScrapItem.EditedImage"/> のキャッシュは
    /// <see cref="ScrapItem.Edit"/> の setter が捨てるため、ここでは意識しなくてよい。
    /// </remarks>
    private void ApplyEdit(ScrapEdit edit)
    {
        // サイコロ中は編集できない (SPEC-v1.7 2.10)。キー入力からの呼び出しに対する保険
        if (_isDice || _isTrimming)
        {
            return;
        }
        if (Item.Edit == edit)
        {
            return; // 値等価。上限に張り付いた状態でのキー連打などで無駄な再描画をしない
        }

        Item.Edit = edit;
        RefreshEditedImage();

        // 編集後の物理サイズへ合わせる。90/270 度回転では幅と高さが入れ替わる (SPEC-v1.7 2.3)
        var (pixelWidth, pixelHeight) = Item.EditedPixelSize;
        var dpi = VisualTreeHelper.GetDpi(this);
        Width = pixelWidth / dpi.DpiScaleX;
        Height = pixelHeight / dpi.DpiScaleY;

        // サイズが変わって画面外へはみ出す場合はモニタ内へ寄せる (サイコロ復元と同じ扱い)
        ClampIntoCurrentMonitor();

        // 編集はやり直しの利かない意図的な操作なので、閉じるまで待たずに永続化を要求する
        // (異常終了で失わないため。SPEC-v1.7 5)
        EditApplied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>拡大縮小率を設定する (範囲外はクランプ。SPEC-v1.7 2.2)。</summary>
    private void SetScalePercent(int percent) => ApplyEdit(Item.Edit.WithScale(percent));

    /// <summary>現在の拡大縮小率に加算する。<c>Alt+↑ / Alt+↓</c> から呼ぶ。</summary>
    private void ScaleBy(int delta) => ApplyEdit(Item.Edit.ScaleBy(delta));

    /// <summary>時計回りに回転する (負値で反時計回り。SPEC-v1.7 2.3)。</summary>
    private void RotateBy(int degrees) => ApplyEdit(Item.Edit.RotateBy(degrees));

    // ===== トリム (SPEC-v1.7 2.4) =====

    /// <summary>
    /// トリムモードに入る。通常時の左ドラッグは移動なので、モードを分けて干渉を避ける。
    /// </summary>
    private void BeginTrim()
    {
        if (_isDice || _isTrimming)
        {
            return;
        }
        _isTrimming = true;
        _trimStartDip = null;
        _trimSelectionDip = Rect.Empty;
        TrimHint.Text = Strings.TrimHintText;
        TrimSelection.Visibility = Visibility.Collapsed;
        UpdateTrimMask();
        TrimLayer.Visibility = Visibility.Visible;
        // キー入力 (Enter / Esc) を受け取るためフォーカスを確実に持たせる
        Activate();
        Focus();
    }

    /// <summary>トリムモードを抜ける (確定・取消の共通処理)。</summary>
    private void EndTrim()
    {
        _isTrimming = false;
        _trimStartDip = null;
        _trimSelectionDip = Rect.Empty;
        TrimLayer.Visibility = Visibility.Collapsed;
        TrimSelection.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 選択範囲を確定してトリムを適用する (SPEC-v1.7 2.4)。
    /// 範囲が極小・未選択ならキャンセル扱いにする (SPEC 4.3 と同じ判断)。
    /// </summary>
    private void CommitTrim()
    {
        var selection = _trimSelectionDip;
        bool valid = selection.Width >= MinTrimSizeDip && selection.Height >= MinTrimSizeDip;
        // 先にモードを抜ける。ApplyEdit は _isTrimming 中を弾くため順序が要る
        EndTrim();
        if (!valid)
        {
            return;
        }

        // DIP の選択範囲を「編集適用後の画像」の物理ピクセル座標へ直す。
        // ScrapEdit 側がここからさらに元画像座標へ逆変換する (SPEC-v1.7 2.4)
        var dpi = VisualTreeHelper.GetDpi(this);
        var displayRect = new Int32Rect(
            (int)Math.Round(selection.X * dpi.DpiScaleX),
            (int)Math.Round(selection.Y * dpi.DpiScaleY),
            (int)Math.Round(selection.Width * dpi.DpiScaleX),
            (int)Math.Round(selection.Height * dpi.DpiScaleY));

        var sourceSize = new Size(_image.PixelWidth, _image.PixelHeight);
        ApplyEdit(Item.Edit.WithTrimFromDisplay(displayRect, sourceSize));
    }

    /// <summary>
    /// 暗幕のくり抜きと選択枠を現在の選択範囲に合わせる。
    /// キャプチャオーバーレイと同じ見た目にして、操作を学び直させない (SPEC-v1.7 2.4)。
    /// </summary>
    private void UpdateTrimMask()
    {
        // 全体を不透明 (= 暗幕が出る) にし、選択範囲だけ透明 (= 明るく抜ける) にする
        var geometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
        geometry.Children.Add(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        if (!_trimSelectionDip.IsEmpty)
        {
            geometry.Children.Add(new RectangleGeometry(_trimSelectionDip));
        }
        TrimMaskBrush.Drawing = new GeometryDrawing(Brushes.White, null, geometry);

        if (_trimSelectionDip.IsEmpty)
        {
            TrimSelection.Visibility = Visibility.Collapsed;
            return;
        }
        TrimSelection.Visibility = Visibility.Visible;
        TrimSelection.Margin = new Thickness(_trimSelectionDip.X, _trimSelectionDip.Y, 0, 0);
        TrimSelection.Width = _trimSelectionDip.Width;
        TrimSelection.Height = _trimSelectionDip.Height;
    }

    // ===== キー移動と移動値表示 (SPEC-v1.7 2.5) =====

    /// <summary>
    /// 付箋を物理px 単位で移動する。DIP で動かすと高 DPI で「1px 押しても動かない」が起きるため、
    /// <c>SetWindowPos</c> で物理座標を直接動かす (SPEC-v1.7 2.5)。
    /// </summary>
    private void MoveByPhysical(int deltaX, int deltaY)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !User32.GetWindowRect(hwnd, out var rect))
        {
            return;
        }
        int x = rect.Left + deltaX;
        int y = rect.Top + deltaY;
        User32.SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0,
            User32.SWP_NOZORDER | User32.SWP_NOACTIVATE | User32.SWP_NOSIZE);
        ShowPositionReadout(x, y);
    }

    /// <summary>
    /// 現在位置 (物理px) を付箋上に数値表示する。一定時間で消す (SPEC-v1.7 2.5)。
    /// </summary>
    private void ShowPositionReadout(int x, int y)
    {
        PositionText.Text = string.Format(PositionFormat, x, y);
        PositionReadout.Visibility = Visibility.Visible;

        // 操作のたびにタイマーを張り直すことで、連続移動中は表示が消えない
        _positionReadoutTimer ??= CreatePositionReadoutTimer();
        _positionReadoutTimer.Stop();
        _positionReadoutTimer.Start();
    }

    private System.Windows.Threading.DispatcherTimer CreatePositionReadoutTimer()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = PositionReadoutDuration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            PositionReadout.Visibility = Visibility.Collapsed;
        };
        return timer;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isTrimming)
        {
            // トリムモード中の左ドラッグは範囲選択。移動・サイコロ化は受け付けない
            _trimStartDip = e.GetPosition(this);
            _trimSelectionDip = Rect.Empty;
            UpdateTrimMask();
            CaptureMouse(); // 付箋の外まで動かしても選択を続けられるようにする
            e.Handled = true;
            return;
        }
        if (e.ClickCount == 2)
        {
            // ダブルクリックでサイコロ化 ⇔ 元に戻す。
            // 移動開始前に押下位置をクリアし、後続の Up が誤ってドラッグ扱いされないようにする
            _leftButtonDownDip = null;
            ToggleDice();
            return;
        }
        // まだ移動しない。しきい値を超えたら OnMouseMove で DragMove を開始する
        _leftButtonDownDip = e.GetPosition(this);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isTrimming)
        {
            if (_trimStartDip is not { } trimStart || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }
            // 選択範囲は付箋の内側へ収める (画像の外は切り出せない)
            var trimCurrent = e.GetPosition(this);
            double x = Math.Clamp(trimCurrent.X, 0, ActualWidth);
            double y = Math.Clamp(trimCurrent.Y, 0, ActualHeight);
            _trimSelectionDip = new Rect(
                Math.Min(trimStart.X, x),
                Math.Min(trimStart.Y, y),
                Math.Abs(x - trimStart.X),
                Math.Abs(y - trimStart.Y));
            UpdateTrimMask();
            return;
        }

        if (_leftButtonDownDip is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }
        var current = e.GetPosition(this);
        if (Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        // しきい値を超えたのでドラッグ移動を開始する。DragMove はボタンが離れるまでブロックする
        _leftButtonDownDip = null;
        DragMove();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isTrimming)
        {
            // ドラッグ終了。範囲は残したまま Enter 待ちにする (SETUNA2 と同じ「選択 → Enter」)
            _trimStartDip = null;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }
        // 移動を伴わなかったクリック。ダブルクリック待ちのため状態だけ解除する
        _leftButtonDownDip = null;
    }

    /// <summary>サイコロ (48×48 DIP タイル) ⇔ 元サイズをトグルする (SPEC-v1.x 2.3)。</summary>
    private void ToggleDice()
    {
        if (_isTrimming)
        {
            return; // トリムモード中はサイコロ化を受け付けない (SPEC-v1.7 2.4)
        }
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
            // サイコロ化直前の DIP サイズへ戻す
            Width = _dipSizeBeforeDice.Width;
            Height = _dipSizeBeforeDice.Height;
            ClampIntoCurrentMonitor();
        }
        if (_diceMenuItem is not null)
        {
            _diceMenuItem.Header = _isDice ? Strings.MenuRestoreText : Strings.MenuDiceText;
        }
        UpdateEditMenuState(); // サイコロ中は編集メニューを淡色にする (SPEC-v1.7 2.10)
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
        if (_isTrimming)
        {
            // トリムモード中の右クリックは取消 (キャプチャオーバーレイと同じ。SPEC-v1.7 2.4)。
            // 中クリックで閉じるのもこの間は受け付けない (誤操作で編集中の付箋を失わないため)
            if (e.ChangedButton == MouseButton.Right)
            {
                EndTrim();
                e.Handled = true;
            }
            return;
        }
        if (e.ChangedButton == MouseButton.Middle)
        {
            // 中クリックで閉じる = ゴミ箱へ。破棄せず Trashed へ移す (SPEC-v1.5 2.3)
            TrashRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// トリムモード中は右クリックを「取消」に使うため、コンテキストメニューを出さない
    /// (SPEC-v1.7 2.4)。
    /// </summary>
    protected override void OnContextMenuOpening(ContextMenuEventArgs e)
    {
        if (_isTrimming)
        {
            e.Handled = true;
            return;
        }
        // 開く直前に状態を反映する。サイコロ化・編集の変化がメニューへ確実に載る
        UpdateEditMenuState();
        base.OnContextMenuOpening(e);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Alt を伴うキーは「システムキー」として届き、e.Key は Key.System になる。
        // 実際のキーは e.SystemKey にあるため、ここで解きほぐしてから判定する。
        // これを忘れると Alt+↑ / Alt+↓ (拡大縮小) が一切反応しない
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // トリムモード中は Enter / Esc だけを受け付け、他の操作は素通しさせない (SPEC-v1.7 2.4)
        if (_isTrimming)
        {
            if (key == Key.Enter)
            {
                CommitTrim();
                e.Handled = true;
            }
            else if (key == Key.Escape)
            {
                EndTrim();
                e.Handled = true;
            }
            return;
        }

        var modifiers = Keyboard.Modifiers;

        if (key == Key.C && modifiers == ModifierKeys.Control)
        {
            CopyToClipboard();
            e.Handled = true;
        }
        else if (key == Key.S && modifiers == ModifierKeys.Control)
        {
            SaveWithDialog();
            e.Handled = true;
        }
        else if (key == Key.S && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            // 即保存は付箋ローカルのキーに割り当てる。グローバル登録を増やさず、
            // かつ「どの付箋を保存するか」が一意に決まる (SPEC-v1.6 2.2)
            QuickSave();
            e.Handled = true;
        }
        else if (key == Key.V && modifiers == ModifierKeys.Control)
        {
            RequestPaste();
            e.Handled = true;
        }
        else if (key == Key.R && modifiers == ModifierKeys.None)
        {
            RotateBy(90); // 右 90 度回転 (SETUNA2 の R に合わせる。SPEC-v1.7 2.3)
            e.Handled = true;
        }
        else if (key == Key.R && modifiers == ModifierKeys.Shift)
        {
            RotateBy(-90);
            e.Handled = true;
        }
        else if (key == Key.T && modifiers == ModifierKeys.None)
        {
            BeginTrim();
            e.Handled = true;
        }
        else if (IsArrowKey(key))
        {
            // Alt+方向キーは拡大縮小、修飾なし / Shift は移動。修飾キーの完全一致で
            // 分岐することで両者が競合しない (SPEC-v1.7 2.9)
            HandleArrowKey(key, modifiers, e);
        }
    }

    private static bool IsArrowKey(Key key) => key is Key.Up or Key.Down or Key.Left or Key.Right;

    /// <summary>
    /// 方向キーを拡大縮小 (Alt 併用) またはキー移動 (修飾なし / Shift) として処理する
    /// (SPEC-v1.7 2.2 / 2.5)。
    /// </summary>
    private void HandleArrowKey(Key key, ModifierKeys modifiers, KeyEventArgs e)
    {
        // Alt+↑↓ = 拡大縮小 10%、Alt+Shift+↑↓ = 1% の微調整 (SPEC-v1.7 2.2)
        if (modifiers == ModifierKeys.Alt || modifiers == (ModifierKeys.Alt | ModifierKeys.Shift))
        {
            if (key is not (Key.Up or Key.Down))
            {
                return; // Alt+←→ は割り当てなし (SETUNA2 は透明度だがホイールと重複するため見送る)
            }
            int step = modifiers.HasFlag(ModifierKeys.Shift)
                ? ScrapEdit.ScaleFineStepPercent
                : ScrapEdit.ScaleStepPercent;
            ScaleBy(key == Key.Up ? step : -step);
            e.Handled = true;
            return;
        }

        // 修飾なし = 1px、Shift = 50px のキー移動 (SPEC-v1.7 2.5)
        if (modifiers is not (ModifierKeys.None or ModifierKeys.Shift))
        {
            return;
        }
        int distance = modifiers == ModifierKeys.Shift ? MoveStepLargePx : MoveStepPx;
        var (dx, dy) = key switch
        {
            Key.Up => (0, -distance),
            Key.Down => (0, distance),
            Key.Left => (-distance, 0),
            _ => (distance, 0),
        };
        MoveByPhysical(dx, dy);
        e.Handled = true;
    }

    /// <summary>
    /// クリップボードの画像を新しい付箋にするよう要求する (SPEC-v1.6 3.5)。
    /// この付箋から少しずらした位置に出すことで「隣に増えた」と分かるようにする。
    /// </summary>
    private void RequestPaste()
    {
        // 現在位置が取れなければキャプチャ元の位置を基準にする (復元直後など)
        var origin = TryGetPhysicalPosition(out var position)
            ? position
            : new Point(_physicalRect.X, _physicalRect.Y);
        PasteRequested?.Invoke(this,
            new Point(origin.X + PasteOffsetPx, origin.Y + PasteOffsetPx));
    }

    /// <summary>
    /// スクラップ画像を保存する。形式はダイアログで選べる (SPEC-v1.6 2.1)。
    /// 編集済みなら編集結果を出力する (SPEC-v1.7 2.8)。
    /// </summary>
    private void SaveWithDialog() => _saver.SaveWithDialog(this, Item.EditedImage, DateTime.Now);

    /// <summary>ダイアログを出さず、設定フォルダへ日時名で保存する (SPEC-v1.6 2.2)。</summary>
    private void QuickSave() => _saver.QuickSave([(Item.EditedImage, DateTime.Now)]);

    /// <summary>
    /// 画像をクリップボードへコピーする。編集済みなら編集結果を出す (SPEC-v1.7 2.8)。
    /// 不透明度と枠線は従来どおり反映しない (SPEC-v1.x 2.2)。
    /// </summary>
    private void CopyToClipboard()
    {
        try
        {
            Clipboard.SetImage(Item.EditedImage);
        }
        catch (ExternalException)
        {
            // 他プロセスがクリップボードをロックしていると失敗する
            MessageBox.Show(Strings.ClipboardCopyFailedMessage, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
