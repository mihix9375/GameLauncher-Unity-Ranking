# GameLauncher Ranking API for Unity

UnityゲームからGameLauncher経由でランキングを利用するためのUPMパッケージです。
ゲーム側はGameServerのIPアドレスやHTTP通信を意識せず、関数を呼ぶだけでスコア送信とランキング取得ができます。

## 特徴

- `await`できる関数でランキングの作成・取得・スコア送信が可能
- HTTP、JSON、URLエンコード、エラー応答をパッケージ内で処理
- ランキングはゲームごとに最大2つ
- 得点、タイム、手数などの符号付き64bit整数に対応
- Unity EditorでのAPI呼び出し時に接続先や失敗原因を自動診断
- Unity 2022.3 LTS以降に対応

## 必要なもの

- Unity 2022.3以降
- Windows向けUnityゲーム
- 起動中のGameLauncher
- GameLauncherから起動されたゲーム

通信経路は次のとおりです。

```text
Unityゲーム → GameLauncher (127.0.0.1:50053) → GameServer
```

GameLauncherがGameServerの接続先を管理するため、Unityプロジェクトへ部内ServerのIPアドレスを書く必要はありません。

> [!IMPORTANT]
> **v0.2.0はv0.1.xと後方互換性がありません。**
> `gameId`や文字列のランキングIDを渡す旧APIは削除されています。v0.1.xを利用しているゲームは、下記の「v0.1.xからの移行」を確認してコードを変更してください。GameLauncherとGameServerもv0.2.0対応版が必要です。

## ランキングをゲームから作成する

ゲームの初期化時などに一度呼びます。0番・1番の固定スロットへ設定を同期するため、繰り返し呼んでもランキングが重複しません。

```csharp
var ranks = await RankingApi.SyncLeaderboardsAsync();
await ranks[0].SetAsync("ハイスコア", RankingOrder.HighScore);
await ranks[0].EnableAsync();

await ranks[1].SetAsync("クリアタイム", RankingOrder.LowScore);
await ranks[1].EnableAsync();
```

タイムのように小さい値を上位にする場合は`RankingOrder.LowScore`を指定します。`ranks[0]`と`ranks[1]`が固定された2つのランキング枠です。

`LeaderboardAutoSync`をシーン内のGameObjectへ追加すれば、Inspectorで設定した配列をゲーム起動時に自動同期できます。

## Unityへ追加する

1. Unityで `Window > Package Manager` を開きます。
2. 左上の `+` を押します。
3. `Add package from git URL...` を選びます。
4. GitHub repoのURLとバージョンタグを入力します。

```text
https://github.com/mihix9375/GameLauncher-Unity-Ranking.git#v0.2.0
```

開発中の最新版を使う場合は`#v0.2.0`を`#main`へ置き換えられますが、完成したゲームではタグによるバージョン固定を推奨します。

`Packages/manifest.json`へ直接追加する場合は次のように記述します。

```json
{
  "dependencies": {
    "com.mihix.gamelauncher-ranking": "https://github.com/mihix9375/GameLauncher-Unity-Ranking.git#v0.2.0"
  }
}
```

## スコアを送信する

```csharp
using GameLauncher.Ranking;
using UnityEngine;

public class GameClear : MonoBehaviour
{
    public async void SendClearScore(long score, string playerName)
    {
        try
        {
            Ranking[] ranks = await RankingApi.SyncLeaderboardsAsync();
            ScoreResult result = await ranks[0].InsertAsync(playerName, score);

            // 必要なときにServer上の最新順位表を再取得できます。
            ScoreEntry[] entries = await ranks[0].GetAsync();

            Debug.Log($"現在 {result.Rank} 位です");
        }
        catch (RankingApiException error)
        {
            Debug.LogError(error.Message);
        }
    }
}
```

## ランキングを取得する

```csharp
using GameLauncher.Ranking;

public async void ShowRanking()
{
    try
    {
        Ranking[] ranks = await RankingApi.SyncLeaderboardsAsync();

        foreach (Ranking rank in ranks)
        {
            if (!rank.Enabled) continue;
            foreach (ScoreEntry entry in rank.Entries)
            {
                UnityEngine.Debug.Log(
                    $"{entry.Rank}位 {entry.PlayerName}: {entry.Score}");
            }
        }
    }
    catch (RankingApiException error)
    {
        UnityEngine.Debug.LogError(error.Message);
    }
}
```

## API一覧

### `RankingApi.SyncLeaderboardsAsync`

```csharp
Task<Ranking[]> SyncLeaderboardsAsync(
    CancellationToken cancellationToken = default)
```

常に2件の`Ranking`操作オブジェクトを返します。`SetAsync`、`InsertAsync`、`GetAsync`、`EnableAsync`、`DisableAsync`で各枠を操作できます。無効化しても設定とスコアは削除されません。

### `Ranking.InsertAsync`

```csharp
Task<ScoreResult> InsertAsync(
    string playerName,
    long score,
    CancellationToken cancellationToken = default)
```

送信成功時は`ScoreResult.Rank`から現在順位を取得できます。

### `Ranking.GetAsync`

```csharp
Task<ScoreEntry[]> GetAsync(
    CancellationToken cancellationToken = default)
```

そのランキング枠の最新順位表を取得します。同時に`Title`、`Order`、`Enabled`、`Entries`も最新状態へ更新されます。

### `Ranking.SetAsync` / `EnableAsync` / `DisableAsync`

```csharp
Task SetAsync(string title, RankingOrder order, CancellationToken cancellationToken = default)
Task EnableAsync(CancellationToken cancellationToken = default)
Task DisableAsync(CancellationToken cancellationToken = default)
```

`SetAsync`で表示名と並び順を設定します。`EnableAsync`と`DisableAsync`は表示・スコア受付を切り替えます。無効化しても保存済みスコアは残ります。

ゲームIDを指定するAPIはありません。GameLauncherが起動ごとに発行するセッショントークンから対象ゲームを安全に特定します。

## v0.1.xからの移行

v0.2.0では、別ゲームのランキングを書き換えられないよう通信先を起動セッションへ固定しました。この変更に伴い、次の旧APIは利用できません。

```csharp
// v0.1.xのコード。この形式はv0.2.0ではコンパイルできません。
await RankingApi.GetLeaderboardsAsync(gameId);
await RankingApi.SubmitScoreAsync(gameId, leaderboardId, playerName, score);
await RankingApi.SubmitScoreAsync(leaderboardId, playerName, score);
```

次のように、Launcherから現在のゲームに割り当てられた2枠を取得して操作します。

```csharp
Ranking[] ranks = await RankingApi.SyncLeaderboardsAsync();
await ranks[0].SetAsync("ハイスコア", RankingOrder.HighScore);
await ranks[0].EnableAsync();
ScoreResult result = await ranks[0].InsertAsync(playerName, score);
ScoreEntry[] entries = await ranks[0].GetAsync();
```

- 以前のランキングIDは、用途に応じて`ranks[0]`または`ranks[1]`へ置き換えます。
- `gameId`は渡しません。GameLauncherがセッショントークンから安全に判定します。
- Unity Editorでは本番通信を行いません。Windows向けにビルドし、GameLauncherから起動して確認します。
- v0.1.xのまま公開済みのゲームは、依存タグを更新しない限りそのまま利用できます。

## 動作確認

Unity 2022.3.62f3でWindowsプレイヤーをビルドし、ランキング2枠の設定、取得、スコア送信、無効化、再有効化、保存済みスコアの保持を確認しています。

## Unity Editorの診断ログ

Unity EditorのPlay Modeで各APIを呼んでも、本番通信は送信しません。引数をローカル診断し、GameLauncherから起動した製品ビルドで確認するよう案内します。

- ランキング番号、表示名、プレイヤー名が正しいか検証
- 呼び出した処理とランキング番号をConsoleへ表示
- 本番通信にはビルド後、GameLauncherからゲームを起動する必要があることを案内

診断後は`RankingApiException`を送出するため、通常の`try/catch`で処理できます。診断処理は`UNITY_EDITOR`のときだけコンパイルされるため、配布するゲームでは通常どおりGameLauncherへ通信します。Editorで案内ログだけを止めたい場合は、APIを呼ぶ前に次のように指定します。

```csharp
RankingApi.EnableEditorDiagnostics = false;
```

## 自分のゲームで変更する場所

- `ranks[0]`または`ranks[1]`を、使用するランキング枠に合わせます。
- `score` には得点、タイム、手数などの整数値を渡します。
- 大きい値と小さい値のどちらを上位にするかは`SetAsync`またはGameServer管理画面で設定します。

ゲームIDと自由入力のランキングIDは使いません。ゲームIDはGameLauncherが起動セッションから判定し、ランキングは0・1の固定スロットで扱います。

## サンプルを読み込む

Package Managerでこのパッケージを選択し、`Samples`欄の`Basic Ranking Example`をImportしてください。

## エラーになるとき

- GameLauncherが起動しているか確認してください。
- GameLauncherのランキングServer API設定を確認してください。
- 使用している`LeaderboardSlot`が同期した配列位置と一致しているか確認してください。
- ゲームを単体起動せず、GameLauncherの起動ボタンから実行してください。
- GameServerとGameLauncherのWindows Firewall設定を確認してください。

`RankingApiException`の`Message`には、Launcherへの接続失敗やServerから返されたエラー理由が入ります。ゲーム本編は通信失敗でも続行できるよう、ランキング処理を`try/catch`で囲んでください。

## 関連リポジトリ

- [GameLauncher](https://github.com/mihix9375/GameLauncher)
- [GameServer](https://github.com/mihix9375/GameServer)
