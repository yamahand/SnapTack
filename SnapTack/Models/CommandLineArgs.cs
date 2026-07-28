using System.Globalization;
using System.Windows;

namespace SnapTack.Models;

/// <summary>
/// コマンドライン引数で要求できる動作 (SPEC-v1.8 2.4)。
/// </summary>
public enum CommandLineAction
{
    /// <summary>指定なし。</summary>
    None,

    /// <summary>キャプチャ (範囲選択オーバーレイ) を開始する (<c>/C:Capture</c>)。</summary>
    Capture,

    /// <summary>設定画面を開く (<c>/C:Option</c>)。</summary>
    Option,
}

/// <summary>
/// コマンドライン引数の解析結果 (SPEC-v1.8 2.2)。
/// </summary>
/// <remarks>
/// UI に依存しない純粋なロジックとして切り出してある。初回起動と二重起動 (パイプ受信) の
/// どちらも同じ型に落としてから処理することで、「常駐中かどうかで挙動が違う」状態を避ける
/// (SPEC-v1.8 2.1)。
/// </remarks>
public sealed class CommandLineArgs
{
    /// <summary>スクラップ化する画像ファイルのパス。順序は引数の並び順を保つ。</summary>
    public IReadOnlyList<string> ImagePaths { get; init; } = [];

    /// <summary><c>/C:</c> で要求された動作。</summary>
    public CommandLineAction Action { get; init; } = CommandLineAction.None;

    /// <summary>
    /// <c>/R:X,Y,W,H</c> で指定された矩形 (物理px、仮想スクリーン座標)。未指定なら null。
    /// </summary>
    public Int32Rect? CaptureRect { get; init; }

    /// <summary>処理すべきものが何も無いか (引数なし / 全て解釈できなかった)。</summary>
    public bool IsEmpty =>
        ImagePaths.Count == 0 && Action == CommandLineAction.None && CaptureRect is null;

    /// <summary>
    /// 引数列を解析する。**解釈できないものは黙って無視する** (SPEC-v1.8 2.2)。
    /// </summary>
    /// <remarks>
    /// エラーを返さないのは、バッチやランチャーからの誤起動で常駐アプリがモーダルダイアログを
    /// 出すと無人実行を止めてしまうため。SnapTack は引数の検証器ではない。
    /// </remarks>
    public static CommandLineArgs Parse(IEnumerable<string> args)
    {
        var imagePaths = new List<string>();
        var action = CommandLineAction.None;
        Int32Rect? captureRect = null;

        foreach (string arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            // オプションは '/' 始まり。それ以外は画像ファイルパスとして扱う。
            // Windows のパスは '/' で始まらないため、この判定で衝突しない
            if (!arg.StartsWith('/'))
            {
                imagePaths.Add(arg);
                continue;
            }

            if (TryParseActionOption(arg) is { } parsedAction)
            {
                // 複数指定された場合は最後の 1 つが勝つ (同時に 2 つの画面は開けないため)
                action = parsedAction;
            }
            else if (TryParseRectOption(arg) is { } parsedRect)
            {
                captureRect = parsedRect;
            }
            // 未知のオプションは無視する (SPEC-v1.8 2.2)
        }

        return new CommandLineArgs
        {
            ImagePaths = imagePaths,
            Action = action,
            CaptureRect = captureRect,
        };
    }

    /// <summary><c>/C:Capture</c> / <c>/C:Option</c> を解釈する。該当しなければ null。</summary>
    private static CommandLineAction? TryParseActionOption(string arg)
    {
        // 大文字小文字は区別しない (SPEC-v1.8 2.4)。SETUNA2 の表記に合わせた綴りで受ける
        if (!arg.StartsWith("/C:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string value = arg[3..];
        return value switch
        {
            _ when value.Equals("Capture", StringComparison.OrdinalIgnoreCase) => CommandLineAction.Capture,
            _ when value.Equals("Option", StringComparison.OrdinalIgnoreCase) => CommandLineAction.Option,
            _ => null,
        };
    }

    /// <summary>
    /// <c>/R:X,Y,W,H</c> を解釈する (物理px)。解釈できなければ null (SPEC-v1.8 2.5)。
    /// </summary>
    private static Int32Rect? TryParseRectOption(string arg)
    {
        if (!arg.StartsWith("/R:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string[] parts = arg[3..].Split(',');
        if (parts.Length != 4)
        {
            return null;
        }

        // X / Y はセカンダリモニタが負座標を持ち得るため負値を許す。W / H は正のみ
        // (SPEC-v1.8 2.5)。カルチャに依存しないよう InvariantCulture で読む
        if (!int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x) ||
            !int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int width) ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int height))
        {
            return null;
        }

        if (width <= 0 || height <= 0)
        {
            return null;
        }

        return new Int32Rect(x, y, width, height);
    }

    /// <summary>
    /// パイプで送るためのテキストへ変換する。1 行 1 引数の UTF-8 テキスト (SPEC-v1.8 2.6)。
    /// </summary>
    /// <remarks>
    /// 引数を再構築せず**受け取った生の引数をそのまま並べる**。パースは受信側で 1 回だけ行い、
    /// 送信側と受信側で解釈がズレないようにするため。
    /// </remarks>
    public static string SerializeArgs(IEnumerable<string> args) =>
        // 改行を含む引数は来ない (コマンドラインの引数として渡らない) ため、
        // 行区切りで十分。JSON にしないのは送るものが文字列配列以上に育つ予定が無いため
        string.Join('\n', args.Where(a => !string.IsNullOrWhiteSpace(a)));

    /// <summary><see cref="SerializeArgs"/> したテキストを引数列へ戻す。</summary>
    public static IReadOnlyList<string> DeserializeArgs(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
