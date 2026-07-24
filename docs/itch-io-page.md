# itch.io 公開用ページ設定

itch.io のプロジェクトページ (https://itch.io/game/new) に入力する内容をまとめたもの。
バージョンを上げたときは「アップロードするファイル」と本文の更新履歴を差し替える。

## プロジェクト設定 (Create new project の各欄)

| 欄 | 値 |
|---|---|
| Title | SnapTack |
| Project URL | `snaptack` (https://<ユーザー名>.itch.io/snaptack) |
| Short description | Pin screen captures to your desktop as sticky notes. A Windows tray utility inspired by SETUNA2. |
| Classification | **Tools** (Games ではない) |
| Kind of project | **Downloadable** |
| Release status | Released |
| Pricing | **$0 or donate** (無料 + 任意の寄付)。下記の前提あり |
| Suggested donation | $3 程度 (任意) |

#### 寄付を有効にする前に:支払い先の設定が必要

支払い先 (Stripe / PayPal) が未接続だと、itch.io が次の警告を出す:

> You don't have payment configured. If you set a minimum price above 0 no one will be able to download your project.

この状態で最低価格を 0 より上にすると**誰もダウンロードできなくなる**ため、
寄付を受け付けるなら先に **Account → Payment settings** で Stripe か PayPal を接続する。
接続には本人確認や税務情報の入力が必要で、itch.io アカウント側の作業になる。

- 支払い先を接続 → Pricing を **`$0 or donate`** にする (最低価格は 0 のまま)
- 支払い先が未接続のうちは **`No payments` (無料のみ)** で公開し、後から寄付を足す

**最低価格は 0 のままにすること。** 0 より上げると無料ダウンロードができなくなる。

### Platforms

**Windows** のみチェック。各ファイルのアップロード後に、ファイル単位でも Windows を指定する。

### Genre / Tags

Genre は "Other"。Tags には以下を設定する (itch.io のタグは小文字・英語が基本):

```
screenshot, sticky-notes, utility, productivity, annotation
```

**タグは盛らない。** itch.io は「実際に当てはまるタグだけを付ける」方針で、
関連性の低いタグは検索の精度を下げるだけで発見されやすくはならない。5 個前後が目安。

意図的に外したもの:

| 外したタグ | 理由 |
|---|---|
| `windows` | **プラットフォームはタグに書かない** (Platforms 欄で指定済み。itch.io が明記している) |
| `screen-capture` | `screenshot` と重複。どちらか一方でよい |
| `open-source` / `dotnet` | 実装や配布形態はユーザーの検索語ではない。本文で伝わる |
| `setuna` | 一般的な検索語ではない。本文で言及すれば足りる |
| `tray` | 検索されにくい |

### Community / Comments

Comments を有効にする (不具合報告の受け皿。GitHub Issues への導線も本文に置く)。

## アップロードするファイル

GitHub Releases の v1.5.0 と同じ 3 種を上げる。各ファイルに Windows タグを付ける。

**表示順は下記のとおりにする** (Uploads でドラッグして並べ替える)。
軽量版 (0.14MB) を上に置くと、サイズの小ささにつられて .NET 未導入の人が
選んでしまい「起動しない」問い合わせにつながるため、最後に置く。

| 順 | ファイル | itch.io 上の表示名 | 備考 |
|---|---|---|---|
| 1 | `SnapTack-v1.5.0-portable-win-x64.zip` | Portable (runtime included) — recommended | 約 63MB。**"This file will be downloaded automatically" を有効化** |
| 2 | `SnapTack-v1.5.0-setup.exe` | Installer | 約 48MB |
| 3 | `SnapTack-v1.5.0-portable-win-x64-fd.zip` | Portable (lightweight, needs .NET 10) | 約 0.14MB |

ファイルは GitHub Releases から取得できる:

```powershell
gh release download v1.5.0 --dir artifacts/itch
```

## カバー画像 / スクリーンショット

- **Cover image (必須)**: 630×500 px → `docs/images/cover.png` を使う。
  `scripts/make-cover.ps1` で生成しており、色や配置はスクリプト冒頭の変数で調整できる
- **Screenshots**: `docs/images/screenshot.png` をそのまま使える

## ページ本文 (Description)

> **Description 欄は Markdown 非対応。** itch.io のプロジェクト説明はリッチテキスト
> エディタ (以前の Markdown 方式を置き換えたもの) で、`##` や `|` の表をそのまま
> 貼っても変換されず、文字列として表示される。
>
> **貼り付けるときはエディタの `<>` ボタンで HTML モードに切り替え、
> `itch-io-description.html` の中身を貼ること。**
> itch.io は「HTML で書き始めたら以降も HTML モードで編集する」ことを推奨している
> (リッチテキスト側で触るとマークアップが書き換えられ、タグが除去されることがある)。
>
> なお Markdown が使えるのはコミュニティ投稿・devlog・コメント欄で、説明文は対象外。

以下は文面の原稿 (可読性のため Markdown で保持)。実際に貼るのは
`docs/itch-io-description.html` の方。

---

Pin any part of your screen to your desktop as a sticky note.

SnapTack is a Windows tray utility that captures a region of your screen and leaves it floating on top of your other windows. Clip a document, an error message, or a reference image in an instant, keep it visible while you work, and copy it into another app when you need it.

It aims to be a successor to SETUNA2, a freeware tool that is no longer developed.

## How it works

1. Press **Ctrl+Shift+Z** — the screen freezes
2. Drag to select any region
3. Release — the selection stays on your desktop as a sticky note

Notes float above everything else. Drag to move them, scroll to fade them, double-click to fold them into a small tile when they're in the way.

## Features

- **Instant region capture** with a global hotkey, at 1:1 physical pixels
- **Notes stay on top** — drag to move, `Ctrl+C` to copy, `Ctrl+S` to save as PNG, middle-click to close
- **Scrap list** (`Ctrl+Shift+L`) — browse every capture as a thumbnail, show or hide notes, and recover closed ones from the trash
- **Scraps persist across restarts** — pinned notes come back where you left them
- **Adjustable opacity** with the mouse wheel, and a fold-to-tile mode to save space
- **Multi-monitor aware** — no positional drift even with mixed DPI scaling like 125% / 150% (Per-Monitor V2)
- **English and Japanese** — follows your Windows display language, switchable in settings
- **Portable** — no installation required, and it touches no registry keys

## Downloads

| File | Description |
|---|---|
| **Portable (runtime included)** | **Pick this one if you're unsure.** Runs as-is even without .NET installed |
| Installer | Same build, with an installer. Optionally starts with Windows |
| Portable (lightweight) | Tiny download. Requires the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |

## Requirements

Windows 10 / 11 (x64)

## If Windows blocks the app

The binaries are not code-signed, so SmartScreen shows a "Windows protected your PC" dialog the first time you run them. Click **More info** and then **Run anyway**.

## Free and open source

SnapTack is free and released under the MIT License. The source code is on [GitHub](https://github.com/yamahand/SnapTack) — issues and pull requests are welcome.

If you find it useful, a donation is appreciated but never required.

An independent implementation inspired by SETUNA2; it reuses none of SETUNA2's source code.

---

## 日本語版 (本文の後半に併記する場合)

画面の好きな範囲を切り取って、そのままデスクトップに貼り付けられます。

SnapTack は、画面の範囲キャプチャを「付箋」として常に最前面に表示する Windows 常駐ツールです。資料の一部、エラーメッセージ、参考画像などを一瞬で切り取り、作業中ずっと見える場所に置いておけます。

開発が終了したフリーソフト SETUNA2 の後継を目指しています。

### 使い方

1. **Ctrl+Shift+Z** を押すと画面がフリーズします
2. ドラッグで範囲を選択
3. 離すと、その範囲が付箋としてデスクトップに残ります

### 主な機能

- **ホットキーで即座に範囲キャプチャ**(物理ピクセル等倍)
- **付箋は常に最前面** — ドラッグで移動、`Ctrl+C` でコピー、`Ctrl+S` で PNG 保存、中クリックで閉じる
- **スクラップリスト**(`Ctrl+Shift+L`)— すべてのキャプチャをサムネイルで一覧。閉じた付箋はゴミ箱から復元できます
- **再起動をまたいでスクラップを保持** — 貼り付け済みの付箋は次回起動時に復元されます
- **ホイールで不透明度を変更**、ダブルクリックで小さなタイルに畳んで場所を節約
- **マルチディスプレイ対応** — 125% / 150% など DPI スケーリングが混在しても位置ズレしません
- **日本語 / 英語対応** — Windows の表示言語に追従、設定画面から切替も可能
- **ポータブル運用可能** — インストール不要、レジストリを汚しません

### 動作環境

Windows 10 / 11 (x64)

### 起動時に警告が出る場合

コード署名をしていないため、初回起動時に SmartScreen の警告が表示されます。**詳細情報** → **実行** で起動できます。

### 無料・オープンソース

MIT License で公開しています。ソースコードは [GitHub](https://github.com/yamahand/SnapTack) にあります。

SETUNA2 に着想を得た独立実装であり、SETUNA2 のソースコードは一切流用していません。
