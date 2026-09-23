# GameLauncher Ranking API for Unity

UnityゲームからGameLauncher経由でランキングを利用するためのUPMパッケージです。
ゲーム側はGameServerのIPアドレスやHTTP通信を意識せず、関数を呼ぶだけでスコア送信とランキング取得ができます。

## 必要なもの

- Unity 2022.3以降
- 起動中のGameLauncher
- GameServer管理画面で設定済みのゲームIDとランキングID

通信経路は次のとおりです。

```text
Unityゲーム → GameLauncher (127.0.0.1:50053) → GameServer
```

## Unityへ追加する

1. Unityで `Window > Package Manager` を開きます。
2. 左上の `+` を押します。
3. `Add package from git URL...` を選びます。
4. GitHub repoのURLとバージョンタグを入力します。

```text
https://github.com/mihix9375/GameLauncher-Unity-Ranking.git#v0.1.0
```

開発中の最新版を使う場合は `#v0.1.0` を外せますが、完成したゲームではタグによるバージョン固定を推奨します。

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
                "TestGame",     // meta.jsonのid
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

Leaderboard[] boards = await RankingApi.GetLeaderboardsAsync("TestGame");

foreach (Leaderboard board in boards)
{
    foreach (ScoreEntry entry in board.Entries)
    {
        UnityEngine.Debug.Log($"{entry.Rank}位 {entry.PlayerName}: {entry.Score}");
    }
}
```

## 自分のゲームで変更する場所

- `TestGame` を、配布ZIPの `meta.json` に書いた `id` へ変更します。
- `high_score` を、GameServer管理画面で作ったランキングIDへ変更します。
- `score` には得点、タイム、手数などの整数値を渡します。
- 大きい値と小さい値のどちらを上位にするかはGameServer側で設定します。

## サンプルを読み込む

Package Managerでこのパッケージを選択し、`Samples`欄の`Basic Ranking Example`をImportしてください。

## エラーになるとき

- GameLauncherが起動しているか確認してください。
- GameLauncherのランキングServer API設定を確認してください。
- ゲームIDとランキングIDの大文字・小文字を含め、設定が一致しているか確認してください。
