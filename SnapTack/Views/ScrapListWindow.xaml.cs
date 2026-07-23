using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SnapTack.Models;
using SnapTack.Resources;

namespace SnapTack.Views;

/// <summary>
/// スクラップの一覧ウィンドウ (SPEC-v1.5 2.2)。サムネイルで内容を判別し、
/// スクラップ/ゴミ箱を切り替えて表示・復元・削除などの操作を行う。
/// </summary>
public partial class ScrapListWindow : Window
{
    // 言語非依存の文字列
    private const string AppName = "SnapTack";
    private const string SaveFileNameFormat = "SnapTack_{0:yyyyMMdd_HHmmss}.png";

    private readonly ScrapManager _manager;
    private readonly SettingsService _settings;

    public ScrapListWindow(ScrapManager manager, SettingsService settings)
    {
        InitializeComponent();
        _manager = manager;
        _settings = settings;

        Title = Strings.ScrapListTitle;
        ScrapsTab.Content = Strings.ScrapsTabText;
        TrashTab.Content = Strings.TrashTabText;
        GridLayoutButton.Content = Strings.LayoutGridText;
        ListLayoutButton.Content = Strings.LayoutListText;
        ItemsList.ContextMenu = BuildContextMenu();

        // 保存済みのレイアウトを反映 (Checked が発火して ApplyLayout が走る)
        if (_settings.Current.ScrapListLayout == ScrapListLayout.List)
        {
            ListLayoutButton.IsChecked = true;
        }
        else
        {
            GridLayoutButton.IsChecked = true;
        }

        // リストを開いている間の増減・状態変化を反映する
        _manager.Changed += OnManagerChanged;
        Closed += (_, _) => _manager.Changed -= OnManagerChanged;

        Refresh();
    }

    private bool IsTrashTab => TrashTab.IsChecked == true;

    private void OnManagerChanged(object? sender, EventArgs e)
    {
        // Changed は UI スレッドで発火する (Manager 操作はすべて UI スレッド) が、念のため委譲
        Dispatcher.Invoke(Refresh);
    }

    private void OnTabChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }
        Refresh();
    }

    private void OnLayoutChanged(object sender, RoutedEventArgs e)
    {
        var layout = ListLayoutButton.IsChecked == true ? ScrapListLayout.List : ScrapListLayout.Grid;
        ApplyLayout(layout);

        // 設定を永続化する (保存失敗は表示の切替自体には影響しないため通知しない)
        if (IsLoaded && _settings.Current.ScrapListLayout != layout)
        {
            _settings.Current.ScrapListLayout = layout;
            _settings.Save();
        }
    }

    /// <summary>レイアウトに応じて ItemsPanel と ItemTemplate を差し替える。</summary>
    private void ApplyLayout(ScrapListLayout layout)
    {
        bool grid = layout == ScrapListLayout.Grid;
        ItemsList.ItemsPanel = (ItemsPanelTemplate)Resources[grid ? "GridPanel" : "ListPanel"];
        ItemsList.ItemTemplate = (DataTemplate)Resources[grid ? "GridItemTemplate" : "ListItemTemplate"];
    }

    /// <summary>現在のタブの内容でリストを再構築する。</summary>
    private void Refresh()
    {
        var source = IsTrashTab ? _manager.TrashedScraps : _manager.ActiveScraps;
        // 新しい順に見せる (最近のものを上/先頭に)
        var items = source.Reverse().Select(i => new ScrapListItem(i)).ToList();
        ItemsList.ItemsSource = items;

        bool empty = items.Count == 0;
        EmptyText.Text = IsTrashTab ? Strings.TrashEmptyText : Strings.ScrapsEmptyText;
        EmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    private IReadOnlyList<ScrapItem> SelectedScraps() =>
        ItemsList.SelectedItems.Cast<ScrapListItem>().Select(x => x.Item).ToList();

    // ===== コンテキストメニュー =====

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        menu.Opened += OnContextMenuOpened;
        return menu;
    }

    /// <summary>開くたびに現在のタブに応じた項目で組み直す (スクラップ/ゴミ箱で内容が変わるため)。</summary>
    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        var menu = (ContextMenu)sender;
        menu.Items.Clear();

        if (SelectedScraps().Count == 0)
        {
            e.Handled = true;
            menu.IsOpen = false;
            return;
        }

        if (IsTrashTab)
        {
            AddMenuItem(menu, Strings.ListMenuRestoreText, ShowSelected);
            menu.Items.Add(new Separator());
            AddMenuItem(menu, Strings.MenuCopyText, CopySelected);
            AddMenuItem(menu, Strings.MenuSavePngText, SaveSelectedAsPng);
            menu.Items.Add(new Separator());
            AddMenuItem(menu, Strings.ListMenuDeleteText, DeleteSelected);
        }
        else
        {
            AddMenuItem(menu, Strings.ListMenuShowText, ShowSelected);
            AddMenuItem(menu, Strings.ListMenuHideText, HideSelected);
            menu.Items.Add(new Separator());
            AddMenuItem(menu, Strings.MenuCopyText, CopySelected);
            AddMenuItem(menu, Strings.MenuSavePngText, SaveSelectedAsPng);
            menu.Items.Add(new Separator());
            AddMenuItem(menu, Strings.ListMenuTrashText, TrashSelected);
        }
    }

    private static void AddMenuItem(ContextMenu menu, string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    // ===== 操作 =====

    private void OnListMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 項目上でのダブルクリックのみ反応する (空白部では選択が無い)
        if (SelectedScraps().Count > 0)
        {
            ShowSelected();
        }
    }

    private void OnListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ShowSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            if (IsTrashTab)
            {
                DeleteSelected();
            }
            else
            {
                TrashSelected();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopySelected();
            e.Handled = true;
        }
    }

    /// <summary>選択スクラップを画面に表示 (Pinned へ) し、最前面へ持ってくる。</summary>
    private void ShowSelected()
    {
        foreach (var item in SelectedScraps())
        {
            _manager.Show(item);
        }
        // Show → Changed → Refresh でタブ内容が変わるため、このウィンドウは背面に回る。
        // ユーザーは付箋を見たいはずなので、ここでリストを前面に戻さない
    }

    private void HideSelected()
    {
        foreach (var item in SelectedScraps())
        {
            _manager.Stash(item);
        }
    }

    private void TrashSelected()
    {
        foreach (var item in SelectedScraps())
        {
            _manager.Trash(item);
        }
    }

    private void DeleteSelected()
    {
        var targets = SelectedScraps();
        if (targets.Count == 0)
        {
            return;
        }
        // 完全削除は取り消せないため確認する (SPEC-v1.5 2.2)
        var result = MessageBox.Show(
            string.Format(Strings.DeleteConfirmFormat, targets.Count),
            AppName, MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (result != MessageBoxResult.OK)
        {
            return;
        }
        foreach (var item in targets)
        {
            _manager.Delete(item);
        }
    }

    /// <summary>選択スクラップの画像をクリップボードへ。複数選択時は先頭の 1 枚 (クリップボードは 1 枚のみ)。</summary>
    private void CopySelected()
    {
        var first = SelectedScraps().FirstOrDefault();
        if (first is null)
        {
            return;
        }
        try
        {
            Clipboard.SetImage(first.Image);
        }
        catch (ExternalException)
        {
            MessageBox.Show(Strings.ClipboardCopyFailedMessage, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>選択スクラップを PNG 保存する。複数選択時は先頭の 1 枚 (付箋側と同じ挙動)。</summary>
    private void SaveSelectedAsPng()
    {
        var first = SelectedScraps().FirstOrDefault();
        if (first is null)
        {
            return;
        }
        var dialog = new SaveFileDialog
        {
            FileName = string.Format(SaveFileNameFormat, first.CapturedAt.LocalDateTime),
            DefaultExt = ".png",
            Filter = Strings.SaveFileFilter,
            InitialDirectory = GetInitialSaveDirectory(),
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        string? savedDirectory;
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(first.Image));
            using (var stream = File.Create(dialog.FileName))
            {
                encoder.Save(stream);
            }
            savedDirectory = Path.GetDirectoryName(dialog.FileName);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ExternalException
                or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            MessageBox.Show(Strings.SavePngFailedMessage, AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.Current.LastSaveDirectory = savedDirectory;
        _settings.Save();
    }

    private string GetInitialSaveDirectory()
    {
        string? last = _settings.Current.LastSaveDirectory;
        if (!string.IsNullOrEmpty(last) && Directory.Exists(last))
        {
            return last;
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }
}
