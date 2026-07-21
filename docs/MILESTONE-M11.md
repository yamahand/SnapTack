# M11: CI 整備 (Claude Code 用)

詳細方針は `docs/CI.md` を参照。M11 完了時点でリリース作業が自動化される。

## チェックリスト

### M11-1: バージョンの一元化

- [x] リポジトリ直下に `Directory.Build.props` を追加 (提供済みファイルを使用)
- [x] `SnapTack/SnapTack.csproj` から `<Version>` `<Product>` `<Description>` を削除
- [x] `installer/SnapTack.iss` の `MyAppVersion` を `#ifndef` でガードし、`/DMyAppVersion=` で上書き可能にする
- [x] `scripts/publish.ps1` の `-Version` 既定値の扱いを整理 (省略時は props の値を使い、`-p:Version` を渡さない)
- [x] ローカルで `pwsh scripts/publish.ps1 -Version 1.3.0` と `iscc /DMyAppVersion=1.3.0 installer\SnapTack.iss` が通ることを確認 (どちらも省略時は props の既定値にフォールバックすることも確認済み)

### M11-2: テスト対象の切り出し (挙動を変えないリファクタリング)

- [x] `ScrapWindow.NextOpacityPercent` を `SnapTack/Models/OpacityLevel.cs` の public static メソッドへ移動
- [x] `OverlayWindow.ToPhysicalRect` / `ClampToScreenshot` を `SnapTack/Capture/RectMath.cs` の public static メソッドへ移動 (ウィンドウ状態に依存する部分は引数で受け取る)
- [x] 呼び出し元を新クラス経由に置き換える。**ロジックは一切変更しないこと**
- [x] ビルドが警告なしで通ることを確認

### M11-3: テストプロジェクト

- [x] `tests/SnapTack.Tests/SnapTack.Tests.csproj` を追加 (xUnit、`net10.0-windows`、本体プロジェクトを ProjectReference)
- [x] `SnapTack.slnx` にテストプロジェクトを追加
- [x] `OpacityLevel` のテスト: 100→90 / 20 で下限クランプ / 100 で上限クランプ / 75 から下げて 70 / 25 から上げて 30 / 複数ノッチ
- [x] `RectMath` のテスト: DPI 100% と 150% での変換 / 端数座標の丸め / 画像範囲外入力のクランプ / 幅・高さが負にならないこと
- [x] `dotnet test SnapTack.slnx` が全て通ることを確認

### M11-4: ワークフロー

- [x] `.github/workflows/ci.yml` を追加 (提供済みファイルを使用)
- [x] `.github/workflows/release.yml` を追加 (提供済みファイルを使用)
- [x] `.gitignore` に CI 由来の除外が必要なものがないか確認

### M11-5: ドキュメント

- [x] `docs/CI.md` を追加
- [x] README (en/ja 両方) の Building セクションに、リリース手順が「タグを打つ → CI がドラフト Release を作る → 内容を確認して公開」である旨を追記
- [x] README の Installation セクションに SmartScreen 警告の回避手順を追記 (未署名のため初回起動時に警告が出る。「詳細情報」→「実行」)
- [x] README に CI バッジを追加

## 完了条件

- `dotnet build SnapTack.slnx -c Release` が警告なしで通る
- `dotnet test SnapTack.slnx` が全て通る
- ワークフローファイルの YAML が妥当である

---

## Claude Code へのプロンプト

```
CI を整備します。docs/CI.md (新規追加するファイル、内容は別途渡します) の方針に沿って、
MILESTONE-M11.md の M11-1 から M11-5 を順に実装してください。

前提:
- Directory.Build.props / .github/workflows/ci.yml / .github/workflows/release.yml は
  すでに用意したものをリポジトリに配置済みです。これらは原則そのまま使い、
  修正が必要と判断した場合は理由を説明してから変更してください。
- M11-2 のロジック切り出しは「挙動を変えないリファクタリング」です。
  計算式・境界条件を一切変更しないでください。変更が必要に見えたら、実装せず報告してください。

各サブマイルストーン完了ごとにビルドとテストが通ることを確認し、
最後に私が手動で確認すべきこと(タグを打ってのリリース試験の手順など)を提示してください。
```
