using System.Windows;
using System.Windows.Media.Imaging;

namespace SnapTack.Models;

/// <summary>
/// スクラップ 1 件のモデル。キャプチャ画像とそのメタデータ・表示状態を保持する。
/// v1.5 では付箋を「画面上の存在」から「管理対象のデータ」へ格上げする土台になる (SPEC-v1.5 1)。
/// </summary>
/// <remarks>
/// 状態管理は M14、ディスク保存 (Id と画像ファイルの対応) は M16 で追加した。
/// 画像は遅延読み込みに対応する。復元直後の <see cref="ScrapState.Stashed"/> /
/// <see cref="ScrapState.Trashed"/> は画像を持たず、<see cref="Image"/> 初回参照時に
/// ローダー経由でディスクから読む (SPEC-v1.5 3.3)。
/// </remarks>
public sealed class ScrapItem
{
    // 画像のローダー。null になるのは「読み込み済み」を意味する
    private Func<BitmapSource>? _imageLoader;
    private BitmapSource? _image;

    private ScrapEdit _edit = ScrapEdit.Default;
    private BitmapSource? _editedImage; // 変換結果のキャッシュ。Edit の変更で捨てる

    /// <summary>スクラップの一意な ID。画像ファイル名 (&lt;id&gt;.png) と対応する。</summary>
    public Guid Id { get; }

    /// <summary>キャプチャ元の位置・サイズ (物理px、仮想スクリーン座標)。</summary>
    public Int32Rect PhysicalRect { get; }

    /// <summary>キャプチャ日時。</summary>
    public DateTimeOffset CapturedAt { get; }

    /// <summary>現在の状態 (SPEC-v1.5 2.1)。遷移は <see cref="ScrapManager"/> が行う。</summary>
    public ScrapState State { get; set; } = ScrapState.Pinned;

    /// <summary>
    /// ゴミ箱へ移した日時。<see cref="ScrapState.Trashed"/> 以外では null。
    /// 自動削除と、ゴミ箱内での古い順削除の判定に使う (SPEC-v1.5 2.4)。
    /// </summary>
    public DateTimeOffset? TrashedAt { get; set; }

    /// <summary>不透明度 (%)。既定は 100 (SPEC-v1.x 2.2)。</summary>
    public int OpacityPercent { get; set; } = 100;

    /// <summary>サイコロ (最小化タイル) 状態か (SPEC-v1.x 2.3)。</summary>
    public bool IsDice { get; set; }

    /// <summary>
    /// 適用中の編集 (拡大縮小・回転・反転・トリム。SPEC-v1.7 2.1)。既定は編集なし。
    /// 設定すると <see cref="EditedImage"/> のキャッシュを捨てる。
    /// </summary>
    public ScrapEdit Edit
    {
        get => _edit;
        set
        {
            if (_edit == value)
            {
                return; // record の値等価。同じ内容ならキャッシュを保つ
            }
            _edit = value;
            _editedImage = null;
        }
    }

    /// <summary>
    /// 編集を適用した画像 (SPEC-v1.7 2.8)。表示・コピー・保存・サムネイルはすべてこれを使う。
    /// 編集が無ければ <see cref="Image"/> と同一のインスタンスを返す。
    /// </summary>
    /// <remarks>
    /// 変換のたびに再サンプリングするとキー連打で目に見えて重くなるため、
    /// <see cref="Edit"/> が変わるまでキャッシュする (SPEC-v1.7 6)。
    /// </remarks>
    public BitmapSource EditedImage => _editedImage ??= _edit.Apply(Image);

    /// <summary>編集適用後の物理ピクセルサイズ。画像を生成せずに付箋のサイズを決めるのに使う。</summary>
    public (int Width, int Height) EditedPixelSize =>
        _edit.GetResultSize(PhysicalRect.Width, PhysicalRect.Height);

    /// <summary>
    /// 最後に表示していた位置 (物理px)。未移動なら null で <see cref="PhysicalRect"/> の位置を使う。
    /// ドラッグのたびには保存せず、ウィンドウを閉じる時とアプリ終了時に書き戻す (SPEC-v1.5 2.4)。
    /// </summary>
    public Point? WindowPosition { get; set; }

    /// <summary>
    /// 画像 (物理ピクセル、Freeze 済み)。遅延読み込みのため初回参照時にローダーを実行する。
    /// </summary>
    public BitmapSource Image
    {
        get
        {
            if (_image is null && _imageLoader is { } loader)
            {
                _image = loader();
                _imageLoader = null; // 二度目以降は読み直さない
            }
            return _image!;
        }
    }

    /// <summary>画像がまだ読み込まれていない (遅延ローダーが未実行) か。</summary>
    public bool IsImageLoaded => _image is not null;

    /// <summary>新規キャプチャから作る。画像は既にメモリ上にある。</summary>
    public ScrapItem(BitmapSource image, Int32Rect physicalRect)
        : this(Guid.NewGuid(), physicalRect, DateTimeOffset.Now)
    {
        _image = image;
    }

    /// <summary>ID・日時を指定して作る (復元用)。画像は <see cref="SetImageLoader"/> で後付けする。</summary>
    public ScrapItem(Guid id, Int32Rect physicalRect, DateTimeOffset capturedAt)
    {
        Id = id;
        PhysicalRect = physicalRect;
        CapturedAt = capturedAt;
    }

    /// <summary>復元時に画像の遅延ローダーを設定する。<see cref="Image"/> 初回参照でディスクから読む。</summary>
    public void SetImageLoader(Func<BitmapSource> loader) => _imageLoader = loader;
}
