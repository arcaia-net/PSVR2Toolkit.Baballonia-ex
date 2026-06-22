# PSVR2Toolkit.Baballonia docs

作成日: 2026-06-22

この `docs` フォルダは、既存の調査結果をもとに、`PSVR2Toolkit.Baballonia` をフォークして実装作業へ進むための資料をまとめる場所です。

## 既存調査

- [codex-survey-results.md](codex-survey-results.md): ビルド可否、Baballonia 側のモジュール読み込み、現状の不足点、推奨修正フローの調査結果。
- [claude-survey-results.md](claude-survey-results.md): 実装構成、既知問題、修正優先度、修正手順案の調査結果。

## 実装準備資料

- [requirements-definition.md](requirements-definition.md): 要件定義。目的、対象範囲、機能要件、非機能要件、受け入れ条件。
- [fork-to-implementation-plan.md](fork-to-implementation-plan.md): フォーク、開発環境準備、実装順序、成果物の作り方。
- [test-validation-plan.md](test-validation-plan.md): 単体テスト、統合テスト、実機検証、リリース前チェック。
- [open-questions-and-risks.md](open-questions-and-risks.md): 未確定事項、リスク、実装前に決める判断。

## 現時点の結論

このリポジトリはビルド可能な試作モジュールですが、そのまま Baballonia に安定同梱できる状態ではありません。最低限、provider 判定、停止時例外処理、初回フレーム取得前の ready 判定、モジュール配置手順、実機フレーム仕様の確認が必要です。
