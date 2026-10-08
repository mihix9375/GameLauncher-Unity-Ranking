# GameLauncher Ranking API for Unity

UnityゲームからGameLauncher経由でランキングを利用するためのUPMパッケージです。
ゲーム側はGameServerのIPアドレスやHTTP通信を意識せず、関数を呼ぶだけでスコア送信とランキング取得ができます。

## 特徴

- `await`できる関数でランキングの作成・取得・スコア送信が可能
- HTTP、JSON、URLエンコード、エラー応答をパッケージ内で処理
- ランキングはゲームごとに最大2つ
- 得点、タイム、手数などの大きな整数・小数・指数表記に対応
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

## 大きなスコアと指数表記

v0.3.0の機能です。GameLauncher v0.7.0・GameServer v0.6.0と組み合わせてください。通信・保存の`score`は数値文字列となり、`ScoreEntry.Score`も`long`から`RankingScore`へ変更しています。旧版との混在はできません。旧`leaderboards.json`の整数値はServerが引き続き読み込めます。

```csharp
using System.Numerics;
using GameLauncher.Ranking;

// これまでの整数もそのまま送信できます。
await ranks[0].InsertAsync("PLAYER", 1000L);

// 大きな整数を正確に送る場合。先にdoubleへ変換しないでください。
await ranks[0].InsertAsync("PLAYER", BigInteger.Parse("100000000000000000000000000001"));

// 浮動小数点数・10進小数にも対応します。
await ranks[0].InsertAsync("PLAYER", 1.25e100);
await ranks[0].InsertAsync("PLAYER", 123.456m);

// doubleの範囲を超える値も数値文字列またはRankingScoreで送れます。
await ranks[0].InsertAsync("PLAYER", "1.23456789e1000");
await ranks[0].InsertAsync("PLAYER", RankingScore.Parse("1e-1000"));

ScoreEntry[] entries = await ranks[0].GetAsync();
foreach (ScoreEntry entry in entries)
{
    // 表示だけが自動で指数表記になります（例: 1.235e1000）。
    UnityEngine.Debug.Log(entry.Score.ToString());
    // 計算・保存に使う場合は、丸めていない値を使ってください。
    UnityEngine.Debug.Log(entry.Score.RawValue);
}
```

有効数字は最大1024桁、正規化した10進指数は-10000〜10000です。負の値・0も扱えます。順位比較は正確な10進値で行い、表示の丸めは順位に影響しません。絶対値が`1e9`以上、または0以外で`1e-4`未満の値は、4有効桁の指数表示へ自動変換します。Launcherも同じルールで表示し、スコアにポインタを合わせると丸めていない数値文字列を確認できます。

`double`自体は約15〜17桁の精度なので、変換前に失われた桁は復元できません。大きな整数は`BigInteger`、正確な10進値は`decimal`または数値文字列を使ってください。カンマ・単位・全角数字・NaN・Infinityは指定できません。不正な形式は`ArgumentException`、範囲外はその派生型`ArgumentOutOfRangeException`になります（型変換時に同期的に例外が出る場合もあるため、呼び出し全体をtry内に置きます）。

`RankingScore`は数値比較（`==`・`<`・`>`など）が可能です。整数なら`ToBigInteger()`で正確に取り出せます。小数を`ToBigInteger()`へ渡すと`InvalidOperationException`、doubleの範囲外を`ToDouble()`へ渡すと`OverflowException`になります。`ToDouble()`は範囲内でもdoubleの精度へ丸められます。`ToString()`は表示用なので、再送には`RawValue`を使ってください。

`LeaderboardAutoSync`をシーン内のGameObjectへ追加すれば、Inspectorで設定した配列をゲーム起動時に自動同期できます。

## Unityへ追加する

1. Unityで `Window > Package Manager` を開きます。
2. 左上の `+` を押します。
3. `Add package from git URL...` を選びます。
4. GitHub repoのURLとバージョンタグを入力します。

```text
https://github.com/mihix9375/GameLauncher-Unity-Ranking.git#v0.3.1
```

開発中の最新版を使う場合は`#v0.3.1`を`#main`へ置き換えられますが、完成したゲームではタグによるバージョン固定を推奨します。

`Packages/manifest.json`へ直接追加する場合は次のように記述します。

```json
{
  "dependencies": {
    "com.mihix.gamelauncher-ranking": "https://github.com/mihix9375/GameLauncher-Unity-Ranking.git#v0.3.1"
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

### 使う型と戻り値

| 型 | 用途・主な値 |
|---|---|
| `Ranking[]` | 操作用の2枠。`ranks[0]`と`ranks[1]`を使います |
| `Ranking` | 1枠を操作するオブジェクト。`Title`は`string`、`Order`は`RankingOrder`、`Enabled`は`bool`、`Entries`は`ScoreEntry[]` |
| `LeaderboardSlot` | `Slot0`・`Slot1`だけを指定する列挙型 |
| `RankingOrder` | `HighScore`は大きい値が上位、`LowScore`は小さい値が上位 |
| `LeaderboardDefinition` | `SyncLeaderboardsAsync(定義配列)`へ渡す設定。表示名と並び順を持ちます |
| `Leaderboard` | 取得結果のスナップショット。`Order`はenumではなく`"high_score"`・`"low_score"`の文字列です |
| `ScoreResult` | 送信結果。`Ok`は`bool`、`Rank`は`int`。`Ok == true`かつ`Rank >= 1`を確認します |
| `ScoreEntry` | 1件の記録。`PlayerName`は`string`、`Score`は`RankingScore`、`RawScore`は`string`、`SubmittedAt`は`long`、`Rank`は`int` |
| `RankingScore` | 正確なスコア型。`RawValue`は丸めていない文字列、`ToString()`は自動指数表示。整数は`ToBigInteger()`で取り出せます |
| `Task` / `Task<T>` | 非同期処理。`await`で完了を待ち、戻り値・例外を受け取ります |
| `CancellationToken` | 処理を中断したいときに渡します。Server側の処理の取り消しは保証しません |
| `RankingApiException` | 通信・認証・Serverの拒否・Editor診断などの例外。理由は`Message`に入ります |

`InsertAsync`には`long`・`BigInteger`・`double`・`decimal`・数値文字列・`RankingScore`を渡せます。小数の秒数を使う場合も、秒／ミリ秒などの単位をゲームごとに統一してください。`SubmittedAt`はUNIX秒です。

`Entries`は取得時点のデータで、自動更新されません。`InsertAsync`も`Entries`を更新しないため、最新順位表が必要なら`await rank.GetAsync()`を呼びます。返るのは上位10件で、Serverの保持件数は最大100件です。送信後の順位が返っても、保持範囲外のスコアが残るとは限りません。

APIはUnityのメインスレッドから呼んでください。通信を速くする目的で`Task.Run`へ入れないでください。

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

スコア引数には上記の各数値型を受け付けるオーバーロードがあります。低レベルAPIの`RankingApi.SubmitScoreAsync`を直接使う場合は`long`または`RankingScore`を渡します。

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

`RankingScore`の純粋な数値テストは、Unityを起動せずに.NET 9 SDKで実行できます。

```text
dotnet run --project Tests~/ScoreTests.csproj
dotnet run --project Tests~/ApiTests.csproj
dotnet run --project Tests~/ApiTests.csproj -p:EditorTests=true
```

`ApiTests`はUnityの通信・JSON部分をテスト用に置き換えた回帰テストです。入力検証、呼び出し順、応答の検証、キャンセルを確認し、実際のLauncher・Serverには通信しません。Unityプレイヤーでの実機確認とは別のテストです。

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

### 呼び出し順と例外の受け取り方

```csharp
try
{
    Ranking[] ranks = await RankingApi.SyncLeaderboardsAsync();
    await ranks[0].SetAsync("ハイスコア", RankingOrder.HighScore);
    await ranks[0].EnableAsync();
    ScoreResult result = await ranks[0].InsertAsync("PLAYER", 1000L);
    if (!result.Ok || result.Rank < 1)
        throw new RankingApiException("送信成功を確認できませんでした。");
}
catch (OperationCanceledException)
{
    // キャンセルは通常の中断。送信開始後なら登録済みの可能性があります。
}
catch (ArgumentException error)
{
    // 入力値・スロットなどを修正します。同じ値のまま再試行しません。
    UnityEngine.Debug.LogWarning(error.Message);
}
catch (RankingApiException error)
{
    // 接続・認証・Serverの拒否など。ゲーム本編を止めず、理由を記録します。
    UnityEngine.Debug.LogWarning(error.Message);
}
catch (Exception error)
{
    // 想定外の不具合はスタックトレースも残します。
    UnityEngine.Debug.LogException(error);
}
```

上のコードでは`using System;`と`using GameLauncher.Ranking;`を追加してください。`ArgumentNullException`・`ArgumentOutOfRangeException`は`ArgumentException`の派生型なので、このcatchで扱えます。

各呼び出しの`await`を省略すると、`insert`が設定・有効化より先に走る可能性があります。Taskを待たずに呼び出すと、周囲の`try/catch`でも非同期の例外を受け取れません。`async void`はUnityイベントの入口だけで使い、その中で例外を捕まえます。それ以外は`async Task`を返し、呼び出し元が`await`してください。

### 原因別の一般的な対処

| 原因 | 例外・結果 | 対処 |
|---|---|---|
| 空文字・空白のタイトル／名前、0・1以外のスロット | `ArgumentException`またはその派生型 | 入力・設定を直す |
| 名前が25文字以上、制御文字を含む、タイトルが41文字以上 | 現版では主に`RankingApiException` | 名前は1〜24文字、タイトルは1〜40文字。送信前に検証する |
| Unity Editorから呼んだ | `RankingApiException` | 本番通信は行わない仕様。ビルドしてLauncherから起動する |
| セッショントークンなし・認証拒否 | `RankingApiException` | exeを単体起動せず、Launcherから起動し直す |
| ランキング未同期・無効 | `RankingApiException` | `SetAsync`・`EnableAsync`の完了を待つ |
| 接続切断・タイムアウト・保存失敗 | `RankingApiException` | ログと接続を確認。スコアは自動再送しない |
| 処理中にキャンセルを検出 | `OperationCanceledException` | 中断扱い。Serverへの登録取消しとは考えない |
| 成功応答でも`Ok == false`・順位が不正 | `RankingApiException` | 登録済みの可能性を考慮し、自動再送しない |
| それ以外 | `Exception` | スタックトレースを保存し、送信中UIを`finally`で解除する |

取得処理は、接続が復旧してから手動で再試行できます。自動再試行する場合でも回数上限と待ち時間を設けてください。**スコア送信はタイムアウトしてもServer側では登録済みの可能性があり、同じスコアを自動再送すると重複します。** 現APIには送信IDによる重複防止がありません。取得結果に自分のスコアが見当たらなくても、上位10件以外は返らないため「未登録」の証明にはなりません。

`RankingApiException.HttpStatusCode`にはHTTPエラーのコードが入ります。接続失敗・Editor診断・不正な成功応答などでは`null`です。保存失敗は500、不正な入力は400または422などで返ります。ステータスや`Message`だけでスコアの自動再送を決めないでください。

### v0.3.1の修正と注意点

同じ`Ranking`オブジェクトで取得・設定・有効化・投稿が重なっても直列に処理し、遅れて届いた取得結果で新しい設定を上書きしません。同期応答で設定が実際に反映されているかも検証します。通信完了のタイミングでキャンセルされた場合も`OperationCanceledException`として扱います（Server側で登録済みの可能性は残ります）。

GameServer v0.7.0・GameLauncher v0.8.0と合わせて利用してください。GameServer v0.6.0には、不正な設定の拒否や保存失敗でメモリ上のランキングが変わる問題があります。v0.7.0では、変更候補を一時ファイルへ保存して置換に成功した後だけメモリを更新し、設定・投稿・削除の失敗時は元のデータを維持します。

v0.3.1では、`SetAsync(null)`・長すぎるタイトル／プレイヤー名・制御文字・不正なUnicode・未定義の`RankingOrder`を送信前に拒否します。文字数はServerと同じUnicode文字数で数えます。設定処理とスコア送信は同じロックで直列化しますが、成功・例外を確認するため、必ず`await`でset → enable → insertの順に完了を待ってください。不正な成功応答も`RankingApiException`として通知します。Editorでは本番通信を送りません。

`Samples~/BasicExample/SafeRankingExample.cs`には、入力検証、`set → enable → insert`の順次実行、連打防止、原因別catch、破棄時キャンセル、送信結果の確認、`finally`によるUI復帰をまとめた例があります。`Status`と`IsBusy`をゲーム内UIに表示できます。サンプルはServerの不具合そのものを修正するものではありません。

- GameLauncherが起動しているか確認してください。
- GameLauncherのランキングServer API設定を確認してください。
- 使用している`LeaderboardSlot`が同期した配列位置と一致しているか確認してください。
- ゲームを単体起動せず、GameLauncherの起動ボタンから実行してください。
- GameServerとGameLauncherのWindows Firewall設定を確認してください。

`RankingApiException`の`Message`には、Launcherへの接続失敗やServerから返されたエラー理由が入ります。ゲーム本編は通信失敗でも続行できるよう、ランキング処理を`try/catch`で囲んでください。

## 関連リポジトリ

- [GameLauncher](https://github.com/mihix9375/GameLauncher)
- [GameServer](https://github.com/mihix9375/GameServer)
