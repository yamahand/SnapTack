# SnapTack 開発ガイド

## プロジェクト概要

画面の範囲キャプチャを「付箋(スクラップ)」として画面に貼る Windows 常駐ツール。SETUNA2 の後継を目指す。

開発ドキュメントは `docs/` に集約している(ルートに置くのは README と本書のみ)。

- 仕様: `docs/SPEC.md` (v1.0) + `docs/SPEC-v1.x.md` (v1.1 以降の追補)
- 実装順: `docs/MILESTONES.md` (M1〜M6) + `docs/MILESTONES-v1.x.md` (M7 以降)
- CI 方針: `docs/CI.md`

## 技術スタック(変更禁止)

- C# / .NET 10 / WPF (`net10.0-windows`)
- トレイ常駐: WinForms の `NotifyIcon` (`UseWindowsForms=true`)
- グローバルホットキー: `RegisterHotKey` (P/Invoke)
- キャプチャ: `IScreenCapturer` で抽象化。実装は `Graphics.CopyFromScreen`
- 設定画面のみ Fluent テーマ (`ThemeMode="System"`、WPF0001 は `NoWarn` で抑制)
- **外部 NuGet パッケージは原則追加しない**。必要と判断したら理由を提示して確認を取る

## ディレクトリ構成

```
SnapTack/
  Interop/    P/Invoke ラッパー
  Capture/    キャプチャ抽象化と実装
  Views/      オーバーレイ・付箋・設定の各ウィンドウ
  Models/     設定などのデータクラス
  Resources/  UI 文字列 (.resx) とアクセサ
tests/SnapTack.Tests/   xUnit
```

ビューとロジックは分離するが、小規模アプリのため厳密な MVVM フレームワークは導入しない。

## 重要な設計判断

### 付箋ウィンドウは自己完結させる

`ScrapWindow` は生成後に App 側が参照を保持しない。全部閉じても `ShutdownMode=OnExplicitShutdown` で常駐は続く。

この設計は v2.0 のスクラップリスト実装時に再設計する予定。**それまでは App から付箋を一括操作する機能を足さないこと**(言語切替の即時反映を表示中の付箋に及ぼさないのもこの理由)。

### キャプチャのフリーズ方式

オーバーレイ表示**前**に画面全体をキャプチャし、その画像をオーバーレイの背景に表示する。選択確定後は `CroppedBitmap` で切り出す(**実画面を再キャプチャしない**)。マルチディスプレイ対応 (v1.3) 以降はモニタごとに個別のフリーズ画像とオーバーレイを持つ。

### 座標系の規約

- キャプチャ・画像切り出し: **物理ピクセル**
- WPF のウィンドウ配置・サイズ: **DIP**
- 変換は `VisualTreeHelper.GetDpi()`。変換箇所には単位をコメントで明記する
- `app.manifest` で Per-Monitor V2 を宣言済み
- 混在 DPI 環境で正確に重ねるため、オーバーレイと付箋は `SetWindowPos` で**物理座標で直接配置**している。DIP 配置はフォールバック

## UI 文字列 (i18n)

**UI に出る文字列は必ず `SnapTack/Resources/Strings.resx` に追加する。ハードコード禁止。**

- `Strings.resx` = 英語(既定 / フォールバック)、`Strings.ja.resx` = 日本語(衛星アセンブリ)
- `Resources/Strings.cs` は**手書きのアクセサ**。VS デザイナ生成ではないので、キーを足したらこのファイルにもプロパティを追加する
  - 手書きにした理由: 生成ファイルはデザイナを開かないと更新されず、CI とローカルで差分が出るため
  - キーが無い場合は例外を投げずキー名を返す(翻訳漏れでアプリを落とさない)
  - **`StringsTests` が「`Strings.cs` の全プロパティが両方の .resx に存在するか」を検証している**ので、追加漏れはテストで落ちる
- 言語非依存の文字列(製品名 `SnapTack`、`"{0} × {1}"`、日付書式など)は `const` のままでよい

### 言語の適用

`LanguageService.Apply()` が UI カルチャを設定する。**UI を生成する前に呼ぶこと**(トレイメニューは生成時に文字列が確定する)。

`Auto` の戻り先は**起動時の `CurrentUICulture` のスナップショット**。`InstalledUICulture` は OS の*インストール*言語で固定のため使ってはいけない(表示言語だけを変更している環境で仕様とズレる。PR #9 で実際に混入した)。

### 配布時の注意

`SatelliteResourceLanguages` と `IncludeAllContentForSelfExtract` を csproj で設定済み。**これを外すと single-file publish で `ja` フォルダが exe と別に出力され、`SnapTack.exe` のみを配置するインストーラー経由で日本語表示が失われる。**

## コーディング規約

- Nullable 有効、ImplicitUsings 有効
- P/Invoke は `LibraryImport` ではなく `DllImport` でよい(シンプルさ優先)
- コメントは日本語。**なぜそうしたか**を書く(何をしているかはコードを読めば分かる)
- 例外は「ユーザー操作で回復可能」なら MessageBox で通知して継続。握りつぶさない
- `Clipboard.SetImage` は他プロセスのロックで例外を投げるため try-catch 必須
- ウィンドウ間で使い回す `BitmapSource` は `Freeze()` する
- `NotifyIcon` は終了時に `Visible=false` → `Dispose()`(順序を守らないと幽霊アイコンが残る)

## ビルド・テスト

```powershell
dotnet build SnapTack.slnx
dotnet test SnapTack.slnx
```

### CI と同条件で検証する

CI は **Release 構成 + 警告をエラー扱い**でビルドする。`Directory.Build.props` が `GITHUB_ACTIONS` を見て `TreatWarningsAsErrors` を切り替えているため、ローカルでも再現できる:

```powershell
$env:GITHUB_ACTIONS="true"; dotnet build SnapTack.slnx -c Release
$env:GITHUB_ACTIONS="true"; dotnet test SnapTack.slnx -c Release --no-build
```

push 前にこれを通しておくと CI で落ちない。

### 配布物のビルド

```powershell
pwsh scripts/publish.ps1              # ポータブル版 2 種 (artifacts/)
iscc installer\SnapTack.iss           # インストーラー (要 Inno Setup 6、publish 後に実行)
```

Inno Setup はローカルに無いことが多い。その場合 `.iss` の変更は**リリースワークフローが初めての検証機会**になるので、変更したら報告する。

## バージョン管理

**`Directory.Build.props` の `<Version>` が唯一の定義箇所**。`SnapTack.csproj` と `installer/SnapTack.iss` に直接書かないこと。

リリース時は Git タグが正で、CI から `-p:Version=` / `/DMyAppVersion=` で上書きされる。

`<Description>` は exe の FileDescription になりビルド時に固定されるため、UI 言語には追従しない(英語で持つ)。

## リリース手順

```powershell
# 1. Directory.Build.props の <Version> を更新してコミット
# 2. タグを push すると CI がドラフト Release を作る
git tag v1.4.0
git push origin v1.4.0
# 3. Releases でリリースノートを書いて手動で公開
```

## 作業ルール

- マイルストーン単位で実装し、完了ごとにビルドとテストが通ることを確認する
- **スコープ外の先回り実装はしない**
- SPEC と矛盾する判断が必要になったら、実装せず選択肢を提示して確認を取る
- 完了報告には**人間が手で行う動作確認手順**を含める
- コミットやプッシュは指示があるまで行わない
