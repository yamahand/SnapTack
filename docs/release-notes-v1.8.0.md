**English** | [日本語](#日本語)

SnapTack can now be driven **from outside the app**. Drop image files straight onto `SnapTack.exe`, pass them on the command line, or grab a fixed region from a script — all handed to the instance that is already running, so it never starts twice.

## Which download do I want?

| File | Description |
|---|---|
| **`SnapTack-v1.8.0-portable-win-x64.zip`** | **Runtime included. Pick this one if you're unsure.** Runs as-is even without .NET installed (~63MB) |
| `SnapTack-v1.8.0-portable-win-x64-fd.zip` | Lightweight build (~0.1MB). Requires [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) to be installed separately |
| `SnapTack-v1.8.0-setup.exe` | Installer (~48MB). Runtime included. Optionally adds a desktop icon and starts SnapTack with Windows |

The portable builds need no installation and touch no registry keys — just extract the zip anywhere and run `SnapTack.exe`.

The installer does not require administrator rights; without them it installs to `%LOCALAPPDATA%\Programs` instead of `Program Files`.

## What's new

### Drop images onto the .exe

The one that was listed as a known limitation in v1.7 is now supported. Drag image files from Explorer onto `SnapTack.exe` (or a shortcut to it) and they appear as notes at the cursor.

If SnapTack is already resident, the files are **handed to the running instance** — you get more notes, not a second tray icon.

### Command line

```powershell
SnapTack.exe "C:\pic.png" "D:\shot.jpg"   # pin two images
SnapTack.exe /R:100,200,640,480           # grab a fixed region, no prompt
```

| Option | What it does |
|---|---|
| `/C:Capture` | Starts a capture (the region-select overlay) |
| `/C:Option` | Opens the settings window |
| `/R:X,Y,W,H` | Captures that exact rectangle with no region selection, in physical pixels |

`/R:` is the one to reach for when scripting: it takes the shot immediately, with no overlay and nothing to click. Coordinates are physical pixels in virtual-screen space, so a monitor placed left of your primary can legitimately use negative X.

Notes made from `/R:` land **exactly on the region they came from**, the same as a normal capture. Notes made from image files land at the cursor, since they have no original position to return to.

### It still never starts twice

Launching `SnapTack.exe` with no arguments while it is already running does nothing at all — **exactly as in v1.7**. Only the arguments changed: with them, the new process passes what you gave it to the resident one and exits immediately.

Options that can't be parsed are ignored rather than reported. An unattended script should never be stopped by a dialog box.

## Requirements

Windows 10 / 11 (x64)

## If Windows blocks the app

The binaries are not code-signed, so SmartScreen shows a "Windows protected your PC" dialog the first time you run them. Click **More info** and then **Run anyway**.

## Upgrading from v1.7

Nothing to do. Your scraps, settings, and trash carry over untouched, and **no settings or storage formats changed in this release** — so unlike the v1.6 → v1.7 step, you can move back to v1.7 freely.

The behaviour of SnapTack with no command-line arguments is unchanged from v1.7 in every respect.

## Known limitations

- **Dropping onto the tray icon is not supported** — only onto the .exe itself. The tray icon cannot accept drops without a workaround that costs more than it is worth, and dropping on the .exe covers the same need
- **`/R:` cannot span multiple monitors.** The rectangle is clipped to the monitor its top-left corner falls on, matching the existing single-monitor rule for selections
- Passing both `/C:Capture` and `/R:` runs only `/R:` — the non-interactive option wins so that scripts don't stall on an overlay
- There is no file-association or "Send to" menu integration; use the .exe drop target or the command line
- Nothing is written to standard output, and the exit code is not meaningful — SnapTack is a GUI app and cannot write to a console
- **Rotation is limited to 90° steps.** Arbitrary angles are not supported
- **Editing is disabled while a note is folded into a tile.** Unfold it first
- Notes cannot be resized by dragging an edge — use the keyboard or the menu
- There is no multi-step undo; **Reset edits** clears all edits at once
- Scraps are always stored as PNG internally, whatever format the original image was
- Selections cannot span multiple monitors — each selection is confined to a single monitor. This is under consideration for v2.0 and later
- Notes already on screen do not switch language when you change the setting. A scrap's language is fixed when it is created

## License

MIT License. An independent implementation inspired by SETUNA2; it reuses none of SETUNA2's source code.

---

## 日本語

[English](#) | **日本語**

SnapTack を **アプリの外から** 操作できるようになりました。画像ファイルを `SnapTack.exe` へ直接ドロップする、コマンドラインで渡す、スクリプトから決まった範囲を切り出す — いずれも起動中のインスタンスへ引き渡されるため、二重起動しません。

## どれをダウンロードすればいい?

| ファイル | 内容 |
|---|---|
| **`SnapTack-v1.8.0-portable-win-x64.zip`** | **ランタイム同梱版。迷ったらこちら。** .NET が入っていない環境でもそのまま動きます (約 63MB) |
| `SnapTack-v1.8.0-portable-win-x64-fd.zip` | 軽量版 (約 0.1MB)。別途 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) のインストールが必要です |
| `SnapTack-v1.8.0-setup.exe` | インストーラー (約 48MB)。ランタイム同梱。デスクトップアイコンの追加や Windows 起動時の自動起動を任意で設定できます |

ポータブル版はインストール不要・レジストリを触りません。zip を好きな場所に展開して `SnapTack.exe` を実行するだけです。

インストーラーは管理者権限を必要としません。権限が無い場合は `Program Files` ではなく `%LOCALAPPDATA%\Programs` にインストールされます。

## 新機能

### exe への画像ドロップ

v1.7 で「既知の制限」に挙げていた項目に対応しました。エクスプローラーから画像ファイルを `SnapTack.exe` (またはそのショートカット) へドロップすると、カーソル位置に付箋として貼られます。

SnapTack が既に常駐している場合、ファイルは **起動中のインスタンスへ引き渡されます**。増えるのは付箋だけで、トレイアイコンが 2 つになることはありません。

### コマンドライン

```powershell
SnapTack.exe "C:\pic.png" "D:\shot.jpg"   # 2 枚まとめて付箋化
SnapTack.exe /R:100,200,640,480           # 決まった範囲を選択なしで切り出す
```

| オプション | 動作 |
|---|---|
| `/C:Capture` | キャプチャ (範囲選択オーバーレイ) を開始 |
| `/C:Option` | 設定画面を開く |
| `/R:X,Y,W,H` | 範囲選択を挟まず、指定した矩形をそのまま切り出す (物理ピクセル) |

スクリプトから使うなら `/R:` が本命です。オーバーレイも出ず、クリックも要らず、その場で切り出します。座標は仮想スクリーン座標の物理ピクセルなので、プライマリの左に置いたモニタでは X が負になることもあります (これは正常です)。

`/R:` で作った付箋は、通常のキャプチャと同じく **切り出した位置にそのまま重なります**。一方、画像ファイルから作った付箋は元の位置を持たないため、カーソル位置に出ます。

### 二重起動しないのは従来どおり

常駐中に引数なしで `SnapTack.exe` を実行しても、**v1.7 と全く同じく何も起こりません**。変わったのは引数を付けた場合だけで、その時は新しいプロセスが引数を常駐側へ渡して即座に終了します。

解釈できないオプションはエラーにせず無視します。無人実行がダイアログで止まらないようにするためです。

## 動作環境

Windows 10 / 11 (x64)

## 起動時に警告が出る場合

コード署名をしていないため、初回起動時に SmartScreen の「Windows によって PC が保護されました」が表示されます。**詳細情報** → **実行** で起動できます。

## v1.7 からの更新について

特に必要な作業はありません。スクラップ・設定・ゴミ箱はそのまま引き継がれます。**本リリースでは設定・保存形式ともに変更していない**ため、v1.6 → v1.7 の時とは違い、v1.7 へ戻すことも自由にできます。

コマンドライン引数を使わない場合の挙動は、あらゆる点で v1.7 と同一です。

## 既知の制限

- **トレイアイコンへのドロップには対応していません** (exe 本体へのドロップのみ)。トレイアイコンは標準では D&D を受け付けず、回避策が実装コストに見合わないためです。exe へのドロップで同じ目的を果たせます
- **`/R:` はモニタをまたげません。** 矩形は左上が乗っているモニタ内へ収められます (選択範囲が単一モニタ内に収まる既存の仕様と揃えています)
- `/C:Capture` と `/R:` を同時に指定した場合、`/R:` だけが実行されます。スクリプトがオーバーレイで止まらないよう、非対話のオプションを優先します
- ファイルの関連付けや「送る」メニューへの登録には対応していません。exe へのドロップかコマンドラインをご利用ください
- 標準出力への出力はなく、終了コードにも意味はありません (GUI アプリのためコンソールへ書き込めません)
- **回転は 90° 単位のみです。** 任意角度の回転には対応していません
- **サイコロ化 (タイルに畳んだ状態) では編集できません。** 元に戻してから操作してください
- 付箋の端をドラッグしてのリサイズには対応していません。キーボードかメニューから操作してください
- 多段の undo はありません。**「編集をリセット」**で全ての編集が一度に解除されます
- スクラップは元画像の形式に関わらず、内部的には常に PNG で保存されます
- 選択範囲は複数モニタをまたげません(1 つの選択は単一モニタ内に収まります)。v2.0 以降で検討します
- 表示中の付箋は、言語設定を変更しても切り替わりません。スクラップの言語は生成時に確定します

## ライセンス

MIT License. SETUNA2 に着想を得た独立実装であり、SETUNA2 のソースコードは一切使用していません。
