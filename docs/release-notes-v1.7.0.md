**English** | [日本語](#日本語)

Scraps are no longer fixed the moment you capture them. **Scale, rotate, flip, and trim** a note after it is on screen — and because editing is **non-destructive**, the original is always one click away.

## Which download do I want?

| File | Description |
|---|---|
| **`SnapTack-v1.7.0-portable-win-x64.zip`** | **Runtime included. Pick this one if you're unsure.** Runs as-is even without .NET installed (~63MB) |
| `SnapTack-v1.7.0-portable-win-x64-fd.zip` | Lightweight build (~0.1MB). Requires [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) to be installed separately |
| `SnapTack-v1.7.0-setup.exe` | Installer (~48MB). Runtime included. Optionally adds a desktop icon and starts SnapTack with Windows |

The portable builds need no installation and touch no registry keys — just extract the zip anywhere and run `SnapTack.exe`.

The installer does not require administrator rights; without them it installs to `%LOCALAPPDATA%\Programs` instead of `Program Files`.

## What's new

### Resize a note

That oversized screenshot no longer has to stay oversized.

- **`Alt+↑` / `Alt+↓`** scales in 10% steps, from **25% up to 400%**
- **`Alt+Shift+↑` / `Alt+Shift+↓`** nudges by 1% when you want it exact
- Or pick a preset from the right-click menu (400% / 200% / 100% / 75% / 50% / 25%)

The mouse wheel still changes opacity — scaling deliberately does not take it over.

### Rotate, flip, and trim

- **`R`** rotates 90° right, **`Shift+R`** rotates left. Horizontal and vertical flips are in the menu
- **`T`** starts trimming: drag out the part you want to keep, press **`Enter`** to apply, **`Esc`** to cancel. The note dims outside your selection, the same way the capture overlay does

Trimming can be repeated — trim, look at it, trim again.

### Editing never destroys the original

Every edit is stored as a **set of parameters alongside the untouched original**, not baked into the image. That means:

- **Scaling up and back down does not degrade the image.** It is always rendered from the original in a single step
- **Reset edits** restores the original exactly, however far you went. Trimmed too much? One click back
- Copying and saving give you **what you see on screen** — the edited result, at the size shown

Opacity and the note border still stay out of the saved image, as before.

### Move notes by keyboard

Arrow keys nudge a note **1px** at a time, **`Shift`+arrows** by **50px**, with the position shown as a number while you move. Useful for lining notes up against something behind them.

### Edits survive restarts

Edited notes come back edited. Because edits are deliberate work rather than a quick adjustment, they are written to disk promptly rather than only at exit.

## Requirements

Windows 10 / 11 (x64)

## If Windows blocks the app

The binaries are not code-signed, so SmartScreen shows a "Windows protected your PC" dialog the first time you run them. Click **More info** and then **Run anyway**.

## Upgrading from v1.6 or earlier

Your existing scraps, settings, and trash carry over untouched. **Notes you never edit look and behave exactly as they did in v1.6** — 1:1 physical pixels, unscaled.

One caveat if you plan to move back: this release upgrades the scrap index format. **v1.6 and earlier cannot read a v1.7 index and will start with an empty list.** Your image files are not deleted, so nothing is permanently lost, but the list itself would need rebuilding.

## Known limitations

- **Rotation is limited to 90° steps.** Arbitrary angles are not supported
- **Editing is disabled while a note is folded into a tile.** Unfold it first
- Notes cannot be resized by dragging an edge — use the keyboard or the menu
- There is no multi-step undo; **Reset edits** clears all edits at once
- Scraps are always stored as PNG internally, whatever format the original image was
- Selections cannot span multiple monitors — each selection is confined to a single monitor. This is under consideration for v2.0 and later
- Notes already on screen do not switch language when you change the setting. A scrap's language is fixed when it is created
- **Dropping images onto the .exe or the tray icon is still not supported.** Use the scrap list window, or paste from the clipboard. This is planned alongside command-line support in a future release

## License

MIT License. An independent implementation inspired by SETUNA2; it reuses none of SETUNA2's source code.

---

## 日本語

[English](#) | **日本語**

スクラップはキャプチャした瞬間に確定するものではなくなりました。貼った後から**拡大縮小・回転・反転・トリム**ができます。しかも**非破壊編集**なので、元の状態にはいつでもワンクリックで戻せます。

## どれをダウンロードすればいい?

| ファイル | 内容 |
|---|---|
| **`SnapTack-v1.7.0-portable-win-x64.zip`** | **ランタイム同梱版。迷ったらこちら。** .NET が入っていない環境でもそのまま動きます (約 63MB) |
| `SnapTack-v1.7.0-portable-win-x64-fd.zip` | 軽量版 (約 0.1MB)。別途 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) のインストールが必要です |
| `SnapTack-v1.7.0-setup.exe` | インストーラー (約 48MB)。ランタイム同梱。デスクトップアイコンの追加や Windows 起動時の自動起動を任意で設定できます |

ポータブル版はインストール不要・レジストリを触りません。zip を好きな場所に展開して `SnapTack.exe` を実行するだけです。

インストーラーは管理者権限を必要としません。権限が無い場合は `Program Files` ではなく `%LOCALAPPDATA%\Programs` にインストールされます。

## 新機能

### 付箋の拡大縮小

大きすぎるスクリーンショットを、大きいまま我慢する必要はなくなりました。

- **`Alt+↑` / `Alt+↓`** で 10% ずつ、**25%〜400%** の範囲で拡大縮小
- **`Alt+Shift+↑` / `Alt+Shift+↓`** なら 1% ずつの微調整
- 右クリックメニューのプリセット (400% / 200% / 100% / 75% / 50% / 25%) からも選べます

ホイールは従来どおり不透明度の変更です。拡大縮小には**あえて割り当てていません**。

### 回転・反転・トリム

- **`R`** で右 90° 回転、**`Shift+R`** で左 90° 回転。左右・上下の反転はメニューから
- **`T`** でトリム開始。残したい範囲をドラッグして **`Enter`** で確定、**`Esc`** で取消。選択範囲の外は暗く表示されます (キャプチャ時のオーバーレイと同じ見た目)

トリムは何度でも重ねられます。切って、見て、また切る、という使い方ができます。

### 編集しても元画像は失われない

編集内容は画像に焼き込まず、**元画像はそのままに、変換パラメータとして**保持しています。そのため:

- **拡大してから縮小しても劣化しません。** 常に元画像から 1 回で描画されるためです
- **「編集をリセット」**でどれだけ編集していても元通りになります。トリムしすぎてもワンクリックで戻せます
- コピーと保存では**画面に見えているとおり**の、編集後・表示サイズの画像が出力されます

不透明度と枠線が保存画像に反映されないのは従来どおりです。

### キーボードでの移動

方向キーで **1px**、**`Shift`+方向キー**で **50px** ずつ移動でき、移動中は座標が数値で表示されます。背面にあるものと位置を合わせたいときに便利です。

### 編集内容は再起動後も残る

編集した付箋は編集された状態で復元されます。編集は「ちょっとした調整」ではなくやり直しの利かない作業なので、終了時だけでなく**編集のたびに速やかに**ディスクへ書き込みます。

## 動作環境

Windows 10 / 11 (x64)

## 起動時に警告が出る場合

コード署名をしていないため、初回起動時に SmartScreen の「Windows によって PC が保護されました」が表示されます。**詳細情報** → **実行** で起動できます。

## v1.6 以前からの更新について

既存のスクラップ・設定・ゴミ箱はそのまま引き継がれます。**編集していない付箋の見た目と挙動は v1.6 と完全に同じ**です (物理ピクセル等倍・拡大縮小なし)。

1 点だけ注意があります。本リリースでスクラップの index 形式を更新したため、**v1.6 以前では v1.7 の index を読めず、スクラップ一覧が空の状態で起動します。** 画像ファイル自体は削除されないのでデータが失われるわけではありませんが、一覧は作り直しになります。バージョンを戻す予定がある場合はご注意ください。

## 既知の制限

- **回転は 90° 単位のみです。** 任意角度の回転には対応していません
- **サイコロ化 (タイルに畳んだ状態) では編集できません。** 元に戻してから操作してください
- 付箋の端をドラッグしてのリサイズには対応していません。キーボードかメニューから操作してください
- 多段の undo はありません。**「編集をリセット」**で全ての編集が一度に解除されます
- スクラップは元画像の形式に関わらず、内部的には常に PNG で保存されます
- 選択範囲は複数モニタをまたげません(1 つの選択は単一モニタ内に収まります)。v2.0 以降で検討します
- 表示中の付箋は、言語設定を変更しても切り替わりません。スクラップの言語は生成時に確定します
- **exe やトレイアイコンへの画像のドロップには引き続き未対応です。** スクラップリストウィンドウへのドロップ、またはクリップボードからの貼り付けをご利用ください。コマンドライン対応と併せて今後のリリースで検討します

## ライセンス

MIT License. SETUNA2 に着想を得た独立実装であり、SETUNA2 のソースコードは一切使用していません。
