# SnapTack CI 整備方針

## 1. 目的

1. **壊れたコードが main に入らないようにする** — push / PR ごとに Release 構成でビルドし、警告ゼロを強制する
2. **リリース作業を自動化する** — タグを打つだけでポータブル版 2 種 + インストーラーがビルドされ、GitHub Release に添付される
3. **バージョン番号の一元管理** — 現状 3 箇所 (csproj / publish.ps1 / SnapTack.iss) に散っている `1.3.0` を 1 箇所にまとめる

## 2. 現状の問題点

| 問題 | 内容 |
|---|---|
| バージョンの重複 | `SnapTack.csproj` の `<Version>`、`scripts/publish.ps1` の `$Version` 既定値、`installer/SnapTack.iss` の `MyAppVersion` に同じ値がハードコードされており、更新漏れが起きる |
| 警告の見逃し | ローカルでのみビルドしているため、警告の混入に気付けない |
| リリースが手作業 | publish → iscc → zip を手元で実行し、手動でアップロードしている。環境差による再現性の問題もある |
| テストがない | 純粋なロジック (不透明度の刻み、DIP↔物理px 変換、矩形クランプ) が UI クラスの private に埋まっており、検証手段がない |

## 3. 対応方針

### 3.1 バージョンの一元化

リポジトリ直下に `Directory.Build.props` を置き、`<Version>` をそこだけで定義する。

- `SnapTack.csproj` からは `<Version>` `<Product>` `<Description>` を削除する (props 側へ移動)
- `scripts/publish.ps1` は `-Version` 引数必須にはせず、省略時は csproj/props の既定値を使う。CI からは明示的に渡す
- `installer/SnapTack.iss` は `#ifndef` でガードし、`iscc /DMyAppVersion=1.4.0` で外から上書きできるようにする

リリース時のバージョンは **Git タグを正 (single source of truth)** とする。
`v1.4.0` というタグを打つと、CI が `1.4.0` を各所へ渡してビルドする。
`Directory.Build.props` の値は、ローカルビルド時の既定値という位置付け。

### 3.2 CI ワークフロー (`.github/workflows/ci.yml`)

- トリガー: `main` への push / PR、手動実行
- ランナー: `windows-latest` (WPF のため必須)
- 手順: restore → build (Release) → test
- **警告をエラー扱いにする**: `Directory.Build.props` で `ContinuousIntegrationBuild` が true のときのみ `TreatWarningsAsErrors` を有効にする。ローカル開発は従来どおり警告のまま進められる

### 3.3 リリースワークフロー (`.github/workflows/release.yml`)

- トリガー: `v*` タグの push、または手動実行 (バージョン入力)
- 手順:
  1. タグ名からバージョンを算出 (`v1.4.0` → `1.4.0`)、書式を検証
  2. `scripts/publish.ps1 -Version <ver>` でポータブル版 2 種を生成
  3. Inno Setup を導入し、`iscc /DMyAppVersion=<ver>` でインストーラーを生成
  4. `artifacts/*.zip` と `artifacts/*setup.exe` を GitHub Release に添付 (ドラフトとして作成し、リリースノートを書いてから公開する)

### 3.4 テストプロジェクト

`tests/SnapTack.Tests/` を追加し、xUnit で純粋ロジックを検証する。

テスト可能にするため、以下を UI クラスから純粋な static クラスへ切り出す:

| 切り出し先 (案) | 現在の場所 | 対象 |
|---|---|---|
| `SnapTack/Models/OpacityLevel.cs` | `ScrapWindow.NextOpacityPercent` | ホイール 1 ステップ後の不透明度計算 |
| `SnapTack/Capture/RectMath.cs` | `OverlayWindow.ToPhysicalRect` / `ClampToScreenshot` | DIP↔物理px 変換、矩形クランプ |

**この切り出しは挙動を変えないリファクタリングとして行うこと**。ロジックの改変は禁止。

テスト観点の例:
- 不透明度: 100→90、20 で下限クランプ、75 から下げると 70 (倍数スナップ)、複数ノッチ、上限
- 矩形変換: 等倍 (DPI 100%) / 150% / 端数のある座標、画像範囲外の入力がクランプされること、幅ゼロにならないこと

## 4. スコープ外 (将来検討)

- **コード署名**: SmartScreen 警告の解消には証明書が必要でコストがかかる。当面は README に回避手順を書く方針
- **依存パッケージの自動更新 (Dependabot)**: 現状 NuGet 依存がほぼないため優先度低
- **itch.io への自動アップロード (butler)**: リリース頻度が上がったら検討する
