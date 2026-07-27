**English** | [日本語](#日本語)

Getting images **out of** SnapTack and **into** it both got wider. Save as **PNG, JPEG, or BMP**, or skip the dialog entirely with **quick save**. And you no longer have to capture to make a scrap — **paste from the clipboard or drop image files** you already have.

## Which download do I want?

| File | Description |
|---|---|
| **`SnapTack-v1.6.0-portable-win-x64.zip`** | **Runtime included. Pick this one if you're unsure.** Runs as-is even without .NET installed (~63MB) |
| `SnapTack-v1.6.0-portable-win-x64-fd.zip` | Lightweight build (~0.1MB). Requires [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) to be installed separately |
| `SnapTack-v1.6.0-setup.exe` | Installer (~48MB). Runtime included. Optionally adds a desktop icon and starts SnapTack with Windows |

The portable builds need no installation and touch no registry keys — just extract the zip anywhere and run `SnapTack.exe`.

The installer does not require administrator rights; without them it installs to `%LOCALAPPDATA%\Programs` instead of `Program Files`.

## What's new

### Save as PNG, JPEG, or BMP

`Ctrl+S` now lets you pick the format in the save dialog. JPEG quality is adjustable in settings (default 90).

**PNG remains the default**, so if you never open the settings window, saving behaves exactly as it did in v1.5.

### Quick save — no dialog

Press **`Ctrl+Shift+S`** on a note (or pick **Quick save** from its menu) to write the image straight to a folder of your choice, named by timestamp. No dialog, no typing a filename.

- Set the destination in settings. Left empty, it uses your **Pictures** folder, so it works the first time without any setup
- **Never overwrites.** If a file with the same name exists, `_2`, `_3` … is appended
- In the scrap list, quick save applies to **every selected scrap** at once

Optionally, **the saved file path is copied to the clipboard** after saving (off by default) — handy for pasting into a chat or a terminal.

### Make notes from images you already have

Capturing is no longer the only way to create a scrap.

- **Paste from the clipboard** — from the tray menu, or press **`Ctrl+V`** on a note. Accepts a copied image, copied files, or even text that happens to be an image file path (what Explorer's "Copy as path" gives you)
- **Drag and drop** — drop image files anywhere on the scrap list window

Pasted and dropped scraps join the list and persist like any other. `Ctrl+V` on a note places the new one slightly offset from it, so it reads as "added next to this" rather than replacing it.

Supported formats: PNG, JPEG, BMP, GIF, TIFF — plus **WebP and AVIF** where the matching Windows codec is present.

### New settings

- **Save format** (PNG / JPEG / BMP) and **JPEG quality** (1–100, default 90)
- **Quick save folder** — leave empty to use Pictures
- **Copy the file path to the clipboard after saving** (off by default)

Your existing `settings.json` keeps working as-is — the new keys default to the values above, so nothing changes unless you adjust them.

## Requirements

Windows 10 / 11 (x64)

## If Windows blocks the app

The binaries are not code-signed, so SmartScreen shows a "Windows protected your PC" dialog the first time you run them. Click **More info** and then **Run anyway**.

## Known limitations

- **WebP and AVIF depend on a Windows codec that can be removed.** They ship with Windows 11 by default, but if the codec is missing those files are simply skipped instead of loading. Saving to WebP/AVIF is not offered, as Windows provides only decoders for them
- **Dropping images onto the .exe or the tray icon is not supported yet.** Use the scrap list window, or paste from the clipboard. This is planned alongside command-line support in a future release
- Scraps are always stored as PNG internally, whatever format the original image was
- Selections cannot span multiple monitors — each selection is confined to a single monitor. This is under consideration for v2.0 and later
- Notes already on screen do not switch language when you change the setting. A scrap's language is fixed when it is created
- Images larger than your monitor are pinned at 1:1 without scaling, so they extend past the screen edge. Resizing is planned for v1.7

## License

MIT License. An independent implementation inspired by SETUNA2; it reuses none of SETUNA2's source code.

---

## 日本語

[English](#) | **日本語**

SnapTack から画像を**出す**手段と、**取り込む**手段の両方を広げました。保存形式は **PNG / JPEG / BMP** から選べ、**すぐ保存**ならダイアログすら出ません。さらに、スクラップを作るのにキャプチャは必須でなくなりました — 手元にある画像を**クリップボードから貼り付け**たり、**ドラッグ&ドロップ**したりできます。

## どれをダウンロードすればいい?

| ファイル | 内容 |
|---|---|
| **`SnapTack-v1.6.0-portable-win-x64.zip`** | **ランタイム同梱版。迷ったらこちら。** .NET が入っていない環境でもそのまま動きます (約 63MB) |
| `SnapTack-v1.6.0-portable-win-x64-fd.zip` | 軽量版 (約 0.1MB)。別途 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) のインストールが必要です |
| `SnapTack-v1.6.0-setup.exe` | インストーラー (約 48MB)。ランタイム同梱。デスクトップアイコンの追加や Windows 起動時の自動起動を任意で設定できます |

ポータブル版はインストール不要・レジストリを触りません。zip を好きな場所に展開して `SnapTack.exe` を実行するだけです。

インストーラーは管理者権限を必要としません。権限が無い場合は `Program Files` ではなく `%LOCALAPPDATA%\Programs` にインストールされます。

## 新機能

### PNG / JPEG / BMP で保存

`Ctrl+S` の保存ダイアログで形式を選べるようになりました。JPEG の品質は設定画面から変更できます(既定 90)。

**既定は PNG のまま**なので、設定画面を開かなければ保存の挙動は v1.5 と変わりません。

### すぐ保存 — ダイアログ無し

付箋の上で **`Ctrl+Shift+S`**(またはメニューの**「すぐ保存」**)を押すと、指定フォルダへ日時名でそのまま書き出します。ダイアログもファイル名の入力もありません。

- 保存先は設定画面で指定します。空欄なら**「ピクチャ」**を使うので、何も設定しなくても初回から使えます
- **上書きしません。** 同名のファイルがあれば `_2`, `_3` … と連番が付きます
- スクラップリストでは、**選択中のスクラップすべて**が一括で保存されます

保存後に**ファイルパスをクリップボードへコピー**するオプションもあります(既定オフ)。チャットやターミナルへ貼るときに便利です。

### 手元にある画像から付箋を作る

スクラップを作る手段はキャプチャだけではなくなりました。

- **クリップボードから貼り付け** — トレイメニューから、または付箋の上で **`Ctrl+V`**。コピーされた画像・コピーされたファイル・画像ファイルのパスを表すテキスト(エクスプローラーの「パスのコピー」で得られるもの)のいずれにも対応します
- **ドラッグ&ドロップ** — スクラップリストウィンドウの好きな場所へ画像ファイルをドロップ

貼り付け・ドロップで作ったスクラップも、通常のスクラップと同じようにリストに載り、永続化されます。付箋上の `Ctrl+V` では元の付箋から少しずらした位置に出るので、「置き換わった」のではなく「隣に増えた」と分かります。

対応形式: PNG / JPEG / BMP / GIF / TIFF に加え、対応する Windows のコーデックが入っていれば **WebP / AVIF** も読み込めます。

### 追加された設定

- **保存形式**(PNG / JPEG / BMP)と **JPEG 品質**(1〜100、既定 90)
- **すぐ保存の保存先フォルダ** — 空欄なら「ピクチャ」
- **保存後にファイルパスをクリップボードへコピーする**(既定オフ)

既存の `settings.json` はそのまま使えます。追加キーは上記の既定値で補われるため、自分で変更しない限り挙動は変わりません。

## 動作環境

Windows 10 / 11 (x64)

## 起動時に警告が出る場合

コード署名をしていないため、初回起動時に SmartScreen の「Windows によって PC が保護されました」が表示されます。**詳細情報** → **実行** で起動できます。

## 既知の制限

- **WebP / AVIF は削除可能な Windows のコーデックに依存します。** Windows 11 には標準で付属しますが、コーデックが無い環境ではこれらのファイルは読み込まれず読み飛ばされます。なお WebP / AVIF での保存には対応していません(Windows がデコーダのみを提供しているため)
- **exe やトレイアイコンへの画像のドロップにはまだ対応していません。** スクラップリストウィンドウへのドロップ、またはクリップボードからの貼り付けをご利用ください。コマンドライン対応と併せて今後のリリースで検討します
- スクラップは元画像の形式に関わらず、内部的には常に PNG で保存されます
- 選択範囲は複数モニタをまたげません(1 つの選択は単一モニタ内に収まります)。v2.0 以降で検討します
- 表示中の付箋は、言語設定を変更しても切り替わりません。スクラップの言語は生成時に確定します
- モニタより大きい画像は縮小せず等倍で貼るため、画面からはみ出します。拡大縮小は v1.7 で対応予定です

## ライセンス

MIT License. SETUNA2 に着想を得た独立実装であり、SETUNA2 のソースコードは一切使用していません。
