# 開発ドキュメント

SnapTack の開発向け資料。利用者向けの説明はリポジトリ直下の [README](../README.md) を参照。

## 仕様

| ファイル | 内容 |
|---|---|
| [SPEC.md](SPEC.md) | v1.0 の仕様 (基本方針・座標系・設定) |
| [SPEC-v1.x.md](SPEC-v1.x.md) | v1.1〜v1.4 の追補。PNG 保存 / 不透明度 / サイコロ化 / マルチディスプレイ / 多言語 |
| [SPEC-v1.5.md](SPEC-v1.5.md) | v1.5 の追補。スクラップリスト / ゴミ箱 / 永続化 |

追補は SPEC.md に統合せず並置する(版ごとの差分を追えるようにするため)。

## 実装計画

| ファイル | 内容 |
|---|---|
| [MILESTONES.md](MILESTONES.md) | M1〜M6 (v1.0 まで) |
| [MILESTONES-v1.x.md](MILESTONES-v1.x.md) | M7〜M12 (v1.1〜v1.4) |
| [MILESTONES-v1.5.md](MILESTONES-v1.5.md) | M13〜M17 (v1.5) |
| [MILESTONE-M11.md](MILESTONE-M11.md) | M11 (CI 整備) の詳細チェックリスト |

## 開発基盤

| ファイル | 内容 |
|---|---|
| [CI.md](CI.md) | CI / リリース自動化の方針 |

## 関連

- [../CLAUDE.md](../CLAUDE.md) — 開発ガイド (コーディング規約・落とし穴・ビルド手順)。Claude Code が自動で読むためルートに置いている
