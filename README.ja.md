# SnapTack

[English](README.md) | **日本語**

[![CI](https://github.com/yamahand/SnapTack/actions/workflows/ci.yml/badge.svg)](https://github.com/yamahand/SnapTack/actions/workflows/ci.yml)

画面の任意の範囲をキャプチャして、そのまま「付箋」として画面に貼っておける Windows 用常駐ツールです。
SETUNA2(開発終了したフリーソフト)の後継を目指しています。

資料の一部・エラーメッセージ・参考画像などを一瞬で切り取って画面に貼り、見比べたり他アプリへコピーしたりできます。

![スクリーンショット](docs/images/screenshot.png)

## 特徴

- **Ctrl+Shift+Z** で画面がその瞬間の映像でフリーズ → ドラッグで範囲選択 → 選択範囲がその場に「付箋」として残る
- 付箋は常に最前面。ドラッグで移動、`Ctrl+C` でコピー、`Ctrl+S` で PNG 保存、中クリックで閉じる
- ホイールで不透明度を変更、ダブルクリックで小さなタイル (サイコロ) に畳んで場所を節約
- マルチディスプレイ対応。キャプチャは物理ピクセル等倍で、125% / 150% などの DPI スケーリングが混在していても位置ズレしない (Per-Monitor V2 対応)
- タスクトレイ常駐。ホットキーは設定画面から変更可能
- 日本語 / 英語に対応。既定では Windows の表示言語に従い、設定画面から手動で切り替えも可能
- ポータブル運用可能 (設定は exe と同じフォルダの `settings.json` に保存。書き込めない場合は `%APPDATA%\SnapTack`)

## 動作環境

- Windows 10 / 11 (x64)

## インストール

### ポータブル版

[Releases](../../releases) から zip をダウンロードし、好きな場所に展開して `SnapTack.exe` を起動するだけです。zip は 2 種類あります。

| ファイル | 内容 |
|---|---|
| `SnapTack-vX.X.X-portable-win-x64.zip` | **ランタイム同梱版。迷ったらこちら。** .NET が入っていない環境でもそのまま動きます (その分サイズが大きめです) |
| `SnapTack-vX.X.X-portable-win-x64-fd.zip` | 軽量版。別途 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) のインストールが必要です |

### インストーラー版

1. [Releases](../../releases) から `SnapTack-vX.X.X-setup.exe` をダウンロードして実行

### 起動時に警告が出る場合

コード署名をしていないため、初回起動時に SmartScreen の「Windows によって PC が保護されました」が表示されます。
**詳細情報** → **実行** で起動できます。

## 使い方

| 操作 | 動作 |
|---|---|
| `Ctrl+Shift+Z` (変更可) / トレイアイコンのダブルクリック | キャプチャ開始 |
| 左ドラッグ | 範囲選択 (離すと確定し、その場に付箋が残る) |
| `Esc` / 右クリック | キャプチャのキャンセル |

### 付箋の操作

| 操作 | 動作 |
|---|---|
| 左ドラッグ | 移動 |
| `Ctrl+C` | 画像をクリップボードへコピー |
| `Ctrl+S` | PNG ファイルとして保存 |
| ホイール | 不透明度の変更 (20〜100%) |
| ダブルクリック | サイコロ化 (小さなタイルに畳む) / 元に戻す |
| 中クリック | 閉じる |
| 右クリック | メニュー (コピー / PNG 保存 / 不透明度 / サイコロ化 / 閉じる) |

付箋は何枚でも同時に貼れます。全部閉じてもアプリはトレイに常駐し続けます。終了はトレイメニューの「終了」から。

## ビルド方法

.NET 10 SDK が必要です。

```powershell
dotnet build SnapTack.slnx

# ポータブル版 (single-file) の作成
# ランタイム同梱版と軽量版の zip が artifacts/ に 2 つ出力される
pwsh scripts/publish.ps1

# インストーラーの作成 (Inno Setup 6 が必要。publish 実行後に)
# artifacts/publish (ランタイム同梱版) の exe を取り込む
iscc installer\SnapTack.iss
```

バージョン番号は `Directory.Build.props` の 1 箇所だけで定義しています。
上記コマンドはその値を使います。リリース時は Git タグが正となり、CI から各所へ渡されます。

```powershell
# テストの実行
dotnet test SnapTack.slnx
```

### リリース手順

リリースは自動化されています。`v*` タグを push するとポータブル版 2 種とインストーラーがビルドされ、
**ドラフト**の GitHub Release に添付されます。

```powershell
# 1. Directory.Build.props の <Version> を更新してコミットする
# 2. タグを打って push する (これがリリースワークフローの起動条件)
git tag v1.4.0
git push origin v1.4.0
```

その後 [Releases](../../releases) を開き、添付ファイルを確認してリリースノートを書き、
ドラフトを手動で公開します。

## ライセンス

[MIT License](LICENSE)

SETUNA2 にインスパイアされた独立実装であり、オリジナルのコードは含みません。
