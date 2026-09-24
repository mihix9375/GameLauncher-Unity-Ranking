# GameLauncher Ranking API for Unity

UnityゲームからGameLauncher経由でランキングを利用するためのUPMパッケージです。
ゲーム側はGameServerのIPアドレスやHTTP通信を意識せず、関数を呼ぶだけでスコア送信とランキング取得ができます。

## 特徴

- `await`できる2つの関数だけでランキングを利用可能
- HTTP、JSON、URLエンコード、エラー応答をパッケージ内で処理
- ランキングはゲームごとに最大2つ
- 得点、タイム、手数などの符号付き64bit整数に対応
- Unity EditorでのAPI呼び出し時に接続先や失敗原因を自動診断
- Unity 2022.3 LTS以降に対応

## 必要なもの

- Unity 2022.3以降
- Windows向けUnityゲーム
- 起動中のGameLauncher
- GameServer管理画面で設定済みのゲームIDとランキングID

通信経路は次のとおりです。

```text
Unityゲーム → GameLauncher (127.0.0.1:50053) → GameServer
```

GameLauncherがGameServerの接続先を管理するため、Unityプロジェクトへ部内ServerのIPアドレスを書く必要はありません。

## 最初にServer側を設定する

1. GameServer管理画面を開きます。
2. 対象ゲームの「ランキング」を開きます。
3. ランキングID、表示名、並び順を設定します。

たとえば通常のハイスコアなら次のように設定します。

| 項目 | 設定例 |
|---|---|
| ランキングID | `high_score` |
| 表示名 | `ハイスコア` |
| 並び順 | `high_score` |

タイムや手数のように小さい値を上位にする場合は、並び順を`low_score`にします。

## Unityへ追加する

1. Unityで `Window > Package Manager` を開きます。
2. 左上の `+` を押します。
3. `Add package from git URL...` を選びます。
4. GitHub repoのURLとバージョンタグを入力します。

```text
https://github.com/mihix9375/GameLauncher-Unity-Ranking.git#main
```

この修正版のReleaseタグを作成した後は、完成したゲームでは`#main`をそのタグへ置き換えてバージョンを固定することを推奨します。

`Packages/manifest.json`へ直接追加する場合は次のように記述します。

```json
{
  "dependencies": {
    "com.mihix.gamelauncher-ranking": "https://github.com/mihix9375/GameLauncher-Unity-Ranking.git#main"
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
            ScoreResult result = await RankingApi.SubmitScoreAsync(
                "high_score",   // GameServerで設定したランキングID
                playerName,
                score);

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
        Leaderboard[] boards = await RankingApi.GetLeaderboardsAsync();

        foreach (Leaderboard board in boards)
        {
            foreach (ScoreEntry entry in board.Entries)
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

### `RankingApi.SubmitScoreAsync`

```csharp
Task<ScoreResult> SubmitScoreAsync(
    string leaderboardId,
    string playerName,
    long score,
    CancellationToken cancellationToken = default)
```

送信成功時は`ScoreResult.Rank`から現在順位を取得できます。

### `RankingApi.GetLeaderboardsAsync`

```csharp
Task<Leaderboard[]> GetLeaderboardsAsync(
    CancellationToken cancellationToken = default)
```

各`Leaderboard`の`Entries`には、上位記録の順位、プレイヤー名、スコア、投稿時刻が入ります。

ゲームIDはGameLauncherが起動時に自動設定します。互換性のため、先頭に`string gameId`を指定する従来のオーバーロードも利用できますが、Launcher経由の通信では実際に起動中のゲームIDが優先されます。

## Unity Editorの診断ログ

Unity EditorのPlay Modeで`GetLeaderboardsAsync`または`SubmitScoreAsync`を呼んでも、本番通信は送信しません。ゲームIDを省略するAPIではGameLauncherからの起動が必要だと案内し、ゲームIDを指定する従来のAPIでは引数をローカル診断します。

- `gameId`、`leaderboardId`、プレイヤー名が空でないか検証
- 呼び出した処理、ゲームID、ランキングIDをConsoleへ表示
- 本番通信にはビルド後、GameLauncherからゲームを起動する必要があることを案内

診断後は`RankingApiException`を送出するため、通常の`try/catch`で処理できます。診断処理は`UNITY_EDITOR`のときだけコンパイルされるため、配布するゲームでは通常どおりGameLauncherへ通信します。Editorで案内ログだけを止めたい場合は、APIを呼ぶ前に次のように指定します。

```csharp
RankingApi.EnableEditorDiagnostics = false;
```

## 自分のゲームで変更する場所

- `high_score` を、GameServer管理画面で作ったランキングIDへ変更します。
- `score` には得点、タイム、手数などの整数値を渡します。
- 大きい値と小さい値のどちらを上位にするかはGameServer側で設定します。

ランキングIDは、大文字・小文字も含めてServer側の設定と同じ値を使用してください。ゲームIDはGameLauncherが自動で判定します。

## サンプルを読み込む

Package Managerでこのパッケージを選択し、`Samples`欄の`Basic Ranking Example`をImportしてください。

## エラーになるとき

- GameLauncherが起動しているか確認してください。
- GameLauncherのランキングServer API設定を確認してください。
- ランキングIDの大文字・小文字を含め、設定が一致しているか確認してください。
- ゲームを単体起動せず、GameLauncherの起動ボタンから実行してください。
- GameServerとGameLauncherのWindows Firewall設定を確認してください。

`RankingApiException`の`Message`には、Launcherへの接続失敗やServerから返されたエラー理由が入ります。ゲーム本編は通信失敗でも続行できるよう、ランキング処理を`try/catch`で囲んでください。

## 関連リポジトリ

- [GameLauncher](https://github.com/mihix9375/GameLauncher)
- [GameServer](https://github.com/mihix9375/GameServer)
