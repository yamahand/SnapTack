using System.IO;

namespace SnapTack.Images;

/// <summary>
/// PNG の読み書き。<see cref="Models.ScrapStore"/> が画像を永続化するために使う。
/// 実装は UI 層が持つ (WPF なら PngBitmapEncoder / BitmapImage)。
/// </summary>
public interface IImageCodec
{
    /// <summary>
    /// 画像を PNG としてストリームへ書き出す。失敗時は例外を投げる
    /// (呼び出し側が IO 例外として扱い、保存失敗を false で返す)。
    /// </summary>
    void EncodePng(ICapturedImage image, Stream destination);

    /// <summary>
    /// PNG ファイルを読み込む。読み込み後はファイルを掴み続けないこと
    /// (掴んだままだとスクラップ削除時にファイルを消せなくなる)。
    /// </summary>
    ICapturedImage DecodePng(string path);
}
