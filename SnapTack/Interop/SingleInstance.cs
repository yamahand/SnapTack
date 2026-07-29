using System.IO;
using System.IO.Pipes;
using System.Text;

namespace SnapTack.Interop;

/// <summary>
/// 二重起動の判定 (Mutex) と、二重起動側から常駐側への引数引き渡し (名前付きパイプ)
/// を担う (SPEC-v1.8 2.6)。
/// </summary>
/// <remarks>
/// <para>
/// IPC に名前付きパイプを選んだのは、P/Invoke を増やさずに可変長の引数列を送れるため。
/// <c>WM_COPYDATA</c> は受信用の隠しウィンドウと構造体マーシャリングが要り、この用途には重い。
/// </para>
/// <para>
/// 既定の ACL で作るため、接続できるのは**同一ユーザーのプロセス**に限られる。
/// 受け取った文字列は画像パスとオプションとしてのみ解釈し、実行や展開はしない。
/// </para>
/// </remarks>
public sealed class SingleInstance : IDisposable
{
    // 同一ユーザーセッション内で一意。Mutex 名と対になる
    private const string MutexName = "SnapTack_SingleInstanceMutex";
    private const string PipeName = "SnapTack_CommandLinePipe";

    // 常駐側が終了処理中でパイプが閉じている競合もあるため、待ち続けない。
    // 接続できなければ黙って諦める (SPEC-v1.8 2.6)
    private const int ConnectTimeoutMs = 3000;

    private readonly Mutex _mutex;
    private CancellationTokenSource? _listenerCts;

    /// <summary>このプロセスが最初のインスタンスか (= 常駐すべきか)。</summary>
    public bool IsFirstInstance { get; }

    /// <summary>
    /// 他プロセスから引数を受け取ったときに発火する。**任意のスレッドから呼ばれる**ため、
    /// 購読側で UI スレッドへディスパッチすること (SPEC-v1.8 2.6)。
    /// </summary>
    public event Action<IReadOnlyList<string>>? ArgumentsReceived;

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        IsFirstInstance = createdNew;
    }

    /// <summary>
    /// 常駐側として引数の待ち受けを開始する。最初のインスタンスでなければ何もしない。
    /// </summary>
    /// <remarks>
    /// 待ち受けはバックグラウンドで回す。起動をブロックしないため
    /// (起動速度優先の設計方針。SPEC-v1.8 4)。
    /// </remarks>
    public void StartListening()
    {
        if (!IsFirstInstance || _listenerCts is not null)
        {
            return;
        }

        _listenerCts = new CancellationTokenSource();

        // Task.Run で UI スレッドから切り離す。OnStartup から素の fire-and-forget で呼ぶと
        // 継続が UI スレッドのキューに積まれ、起動処理 (復元・ウィンドウ生成) が終わるまで
        // 待ち受けが始まらない。実測で 7 秒ほど遅れ、その間の引き渡しが取りこぼされた
        _ = Task.Run(() => ListenLoopAsync(_listenerCts.Token));
    }

    /// <summary>
    /// 既に起動しているインスタンスへ引数を送る。送れたら true。
    /// </summary>
    /// <remarks>
    /// 失敗しても例外は投げず false を返す。二重起動側は黙って終了するだけで、
    /// ダイアログを出す価値が無いため (SPEC-v1.8 2.6)。
    /// </remarks>
    public static bool TrySendArguments(IEnumerable<string> args)
    {
        string payload = Models.CommandLineArgs.SerializeArgs(args);
        if (payload.Length == 0)
        {
            return false;
        }

        try
        {
            using var client = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.Out, PipeOptions.None);
            client.Connect(ConnectTimeoutMs);

            // 受信側が Encoding.UTF8 で読むため、BOM を書かない UTF8Encoding を使う
            using var writer = new StreamWriter(client, new UTF8Encoding(false));
            writer.Write(payload);
            writer.Flush();
            return true;
        }
        catch (Exception ex) when (
            ex is TimeoutException or IOException or UnauthorizedAccessException
                or ObjectDisposedException or InvalidOperationException)
        {
            // 常駐側が終了中・パイプ未作成など。黙って諦める
            return false;
        }
    }

    /// <summary>
    /// 接続を 1 件ずつ受け付け続ける。引数の引き渡しは頻度が低く同時実行の必要が無いため、
    /// 単一の待ち受けループで足りる。
    /// </summary>
    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            string payload;
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8);
                payload = await reader.ReadToEndAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 終了時の正常な打ち切り
                return;
            }
            catch (Exception)
            {
                // 送信側が途中で切れた等。次の接続を待ち直す。
                // **ここで抜けると以降の引き渡しが一切効かなくなる**ため、
                // 種類を問わず握って継続する (待ち受けの停止は Dispose だけが行う)
                continue;
            }

            // 通知は try の外で行う。購読側 (UI スレッドへのディスパッチ) が投げた例外で
            // 待ち受けループごと死ぬと、以降 2 度と引数を受け取れなくなるため
            var received = Models.CommandLineArgs.DeserializeArgs(payload);
            if (received.Count > 0)
            {
                try
                {
                    ArgumentsReceived?.Invoke(received);
                }
                catch (Exception)
                {
                    // 購読側の失敗はこの 1 件を捨てるだけに留める
                }
            }
        }
    }

    public void Dispose()
    {
        _listenerCts?.Cancel();
        _listenerCts?.Dispose();
        _listenerCts = null;

        if (IsFirstInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex の解放はスレッドアフィニティを持ち、取得したスレッド以外から
                // 呼ぶと投げる。Dispose が UI スレッド以外から呼ばれた場合がこれに当たる。
                // どちらにせよ直後の Dispose とプロセス終了でハンドルは解放されるため、
                // ここで落とす理由が無い
            }
        }
        _mutex.Dispose();
    }
}
