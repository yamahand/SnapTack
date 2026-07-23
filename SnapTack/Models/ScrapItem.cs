using System.Windows;
using System.Windows.Media.Imaging;

namespace SnapTack.Models;

/// <summary>
/// スクラップ 1 件のモデル。キャプチャ画像とそのメタデータを保持する。
/// v1.5 では付箋を「画面上の存在」から「管理対象のデータ」へ格上げする土台になる (SPEC-v1.5 1)。
/// </summary>
/// <remarks>
/// M13 の時点では状態 (Pinned/Stashed/Trashed) や永続化は持たない。
/// 状態管理は M14、ディスク保存は M16 で足す (MILESTONES-v1.5)。
/// </remarks>
public sealed class ScrapItem
{
    /// <summary>スクラップの一意な ID。将来 (M16) の画像ファイル名と対応させる。</summary>
    public Guid Id { get; }

    /// <summary>キャプチャ画像 (物理ピクセル、Freeze 済み)。</summary>
    public BitmapSource Image { get; }

    /// <summary>キャプチャ元の位置・サイズ (物理px、仮想スクリーン座標)。</summary>
    public Int32Rect PhysicalRect { get; }

    /// <summary>キャプチャ日時。</summary>
    public DateTimeOffset CapturedAt { get; }

    public ScrapItem(BitmapSource image, Int32Rect physicalRect)
        : this(Guid.NewGuid(), image, physicalRect, DateTimeOffset.Now)
    {
    }

    public ScrapItem(Guid id, BitmapSource image, Int32Rect physicalRect, DateTimeOffset capturedAt)
    {
        Id = id;
        Image = image;
        PhysicalRect = physicalRect;
        CapturedAt = capturedAt;
    }
}
