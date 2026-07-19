# SnapTack — Claude Code 実装プロンプト

## 使い方

1. リポジトリ直下に `SPEC.md`、`MILESTONES.md`、この内容を `CLAUDE.md` として配置する
2. Claude Code を起動し、下の「開始プロンプト」を投げる
3. 以降はマイルストーン単位で「M2 を実装して」のように進める

---

## CLAUDE.md (リポジトリ直下に配置する内容)

```markdown
# SnapTack 開発ガイド

## プロジェクト概要
画面の範囲キャプチャ→付箋(スクラップ)として画面に貼るWindows常駐ツール。
SETUNA2の後継。詳細仕様は SPEC.md、実装順は MILESTONES.md を必ず参照すること。

## 技術スタック(変更禁止)
- C# / .NET 10 / WPF (`net10.0-windows`)
- トレイ常駐: NotifyIcon (UseWindowsForms=true で WinForms の NotifyIcon を使用可)
- グローバルホットキー: RegisterHotKey (P/Invoke)
- キャプチャ: IScreenCapturer で抽象化。初期実装は Graphics.CopyFromScreen。
  将来 Windows.Graphics.Capture へ差し替えられる構成にする
- 設定画面のみ Fluent テーマ (ThemeMode="System"、WPF0001 警告は NoWarn で抑制)
- 外部 NuGet パッケージは原則追加しない。必要と判断した場合は理由を提示して確認を取る

## アーキテクチャ方針
- ビュー(XAML+コードビハインド)とロジックを分離する。
  小規模アプリなので厳密な MVVM フレームワークは導入しない
- ディレクトリ構成:
  - Interop/   … P/Invoke ラッパー
  - Capture/   … キャプチャ抽象化と実装
  - Views/     … オーバーレイ・付箋・設定の各ウィンドウ
  - Models/    … 設定などのデータクラス
- 付箋ウィンドウは自己完結させ、App 側はリスト管理しない(v2.0のスクラップリスト実装時に再設計)

## 座標系の規約(重要)
- キャプチャ・画像切り出し: 物理ピクセル
- WPF ウィンドウ配置・サイズ: DIP
- 変換は VisualTreeHelper.GetDpi() を使用し、変換箇所には単位をコメントで明記する
- app.manifest で Per-Monitor V2 を宣言する

## コーディング規約
- Nullable 有効、ImplicitUsings 有効
- P/Invoke は LibraryImport ではなく DllImport でよい(シンプルさ優先)
- UI 文字列は日本語。将来の英語化を見据えハードコードは各ビューの先頭付近か定数に集約
- 例外は「ユーザー操作で回復可能」なら MessageBox で通知して継続、それ以外はクラッシュさせない範囲で握りつぶさない

## 作業ルール
- MILESTONES.md の順に、1マイルストーンずつ実装する
- 各マイルストーン完了時に `dotnet build` が警告なしで通ることを確認する
- マイルストーンをまたぐ先回り実装はしない(スコープ外機能の実装禁止)
- 動作確認手順(人間が手でやること)を完了報告に含める
- SPEC.md と矛盾する判断が必要になったら、実装せず選択肢を提示して確認を取る
```

---

## 開始プロンプト (Claude Code に最初に投げるもの)

```
SnapTack という Windows 常駐アプリを新規開発します。

まず SPEC.md と MILESTONES.md を読んでください。

その後、MILESTONES.md の M1(プロジェクト基盤 + トレイ常駐)を実装してください。

要件:
- リポジトリ直下にソリューションを作成(git init 済み想定、.gitignore も作成)
- M1 のチェックリストをすべて満たす
- 完了したら、ビルドが通ることを確認し、私が手動で行う動作確認の手順を箇条書きで提示する

M1 のスコープ外のこと(ホットキー、キャプチャ等)は実装しないでください。
```

---

## マイルストーン進行用プロンプト (M2 以降で使い回す)

```
M1 の動作確認が完了しました。(問題があればここに記載)

MILESTONES.md の M{N} を実装してください。
完了条件は M{N} のチェックリスト全項目 + ビルドが警告なしで通ること。
終わったら手動での動作確認手順を提示してください。
```

---

## 補足: 詰まりやすいポイントの事前情報

Claude Code が迷いやすい箇所の指針。必要に応じてプロンプトに追記して使う。

- **オーバーレイの全画面化**: `WindowStyle=None` + `WindowState=Maximized` でタスクバーごと覆える。手動で座標計算するより確実
- **フリーズ方式**: オーバーレイ表示前に画面全体をキャプチャし、オーバーレイの背景にその画像を表示する。選択確定後は CroppedBitmap で切り出す(実画面を再キャプチャしない)
- **NotifyIcon の破棄漏れ**: 終了時に `Visible=false` → `Dispose()` しないとトレイに幽霊アイコンが残る
- **Clipboard.SetImage**: 他プロセスがクリップボードをロックしていると例外を投げるので try-catch 必須
- **BitmapSource の共有**: ウィンドウ間で使い回す画像は `Freeze()` する
```
