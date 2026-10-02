# Changelog

## [0.2.0] - 2026-10-02

### Breaking Changes

- v0.1.xと後方互換性はありません
- `gameId`および自由文字列のランキングIDを受け取るAPIを削除
- ランキング指定を`LeaderboardSlot.Slot0` / `Slot1`の固定2枠へ変更
- GameLauncherが発行する起動セッショントークンを必須化

### Added

- `SyncLeaderboardsAsync()`から常に2つの`Ranking`操作オブジェクトを取得するAPIを追加
- `Ranking.SetAsync`、`InsertAsync`、`EnableAsync`、`DisableAsync`を追加
- 最新順位表を取得する`Ranking.GetAsync`を追加
- Inspectorから起動時に同期できる`LeaderboardAutoSync`を追加

## [0.1.3] - 2026-09-24

### Added

- GameLauncherから起動中のゲームIDを自動取得するAPIオーバーロード
- ランキングIDだけでスコア送信できる初心者向けAPI

## [0.1.2] - 2026-09-24

### Changed

- Unity Editorではランキングの本番通信を送信せず、引数のローカル診断後にビルド版での確認方法を案内

## [0.1.1] - 2026-09-24

### Added

- Unity EditorでAPIを呼んだときに、接続先、HTTP状態、応答、確認ポイントをConsoleへ表示する診断ログ
- `RankingApi.EnableEditorDiagnostics`によるEditor診断ログのON/OFF

## [0.1.0] - 2026-09-23

### Added

- ランキング一覧を取得する `RankingApi.GetLeaderboardsAsync`
- スコアを送信する `RankingApi.SubmitScoreAsync`
- Unity Package ManagerからImportできる初心者向けサンプル
