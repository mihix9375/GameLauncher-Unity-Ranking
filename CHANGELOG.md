# Changelog

## [0.1.1] - 2026-09-24

### Added

- Unity EditorでAPIを呼んだときに、接続先、HTTP状態、応答、確認ポイントをConsoleへ表示する診断ログ
- `RankingApi.EnableEditorDiagnostics`によるEditor診断ログのON/OFF

## [0.1.0] - 2026-09-23

### Added

- ランキング一覧を取得する `RankingApi.GetLeaderboardsAsync`
- スコアを送信する `RankingApi.SubmitScoreAsync`
- Unity Package ManagerからImportできる初心者向けサンプル
