using System;
using System.Threading;
using System.Threading.Tasks;
using GameLauncher.Ranking;
using UnityEngine;

/// <summary>
/// 例外が起きてもゲームを止めないスコア送信例です。
/// 同じGameObjectに追加し、SubmitScoreをUIボタンなどから呼んでください。
/// </summary>
public sealed class SafeRankingExample : MonoBehaviour
{
    [SerializeField] private LeaderboardSlot slot = LeaderboardSlot.Slot0;
    [SerializeField] private string rankingTitle = "ハイスコア";
    [SerializeField] private RankingOrder order = RankingOrder.HighScore;
    [SerializeField] private string playerName = "PLAYER";
    [SerializeField] private long score = 1000;
    [SerializeField] private string status = "未送信";

    // UI側はStatusを表示し、IsBusyの間だけ送信ボタンを無効化できます。
    public string Status => status;
    public bool IsBusy { get; private set; }

    private Ranking ranking;
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

    private async Task<Ranking> PrepareAsync(CancellationToken token)
    {
        if (ranking != null) return ranking;
        if (slot != LeaderboardSlot.Slot0 && slot != LeaderboardSlot.Slot1)
            throw new ArgumentOutOfRangeException(nameof(slot), "ランキング番号は0または1です。");
        if (string.IsNullOrWhiteSpace(rankingTitle) || ScalarCount(rankingTitle.Trim()) > 40)
            throw new ArgumentException("ランキング名は1〜40文字にしてください。", nameof(rankingTitle));
        if (order != RankingOrder.HighScore && order != RankingOrder.LowScore)
            throw new ArgumentOutOfRangeException(nameof(order), "並び順を選び直してください。");

        Ranking[] ranks = await RankingApi.SyncLeaderboardsAsync(token);
        Ranking selected = ranks[(int)slot];
        // awaitを外さないでください。設定が失敗したら有効化・送信には進みません。
        await selected.SetAsync(rankingTitle, order, token);
        await selected.EnableAsync(token);
        ranking = selected;
        return ranking;
    }

    [ContextMenu("安全なスコア送信を試す")]
    public async void SubmitScore()
    {
        // async voidはUnityのイベント入口だけに使い、ここで例外を捕まえます。
        if (IsBusy || lifetime.IsCancellationRequested) return;
        IsBusy = true;
        bool submissionStarted = false;
        CancellationToken token = lifetime.Token;
        try
        {
            status = "入力を確認中";
            string name = playerName?.Trim();
            if (string.IsNullOrWhiteSpace(name) || ScalarCount(name) > 24)
                throw new ArgumentException("プレイヤー名は1〜24文字にしてください。", nameof(playerName));
            foreach (char character in name)
                if (char.IsControl(character))
                    throw new ArgumentException("プレイヤー名に改行などは使えません。", nameof(playerName));

            Ranking selected = await PrepareAsync(token);
            token.ThrowIfCancellationRequested();
            status = "スコアを送信中";
            submissionStarted = true;
            ScoreResult result = await selected.InsertAsync(name, score, token);
            // HTTPが成功でも、戻り値が成功とは限らないので確認します。
            if (result == null || !result.Ok || result.Rank < 1)
                throw new RankingApiException("スコア送信の成功を確認できませんでした。");
            status = $"送信完了: {result.Rank}位";
        }
        catch (OperationCanceledException)
        {
            status = submissionStarted ? "送信を中断しました。登録済みの可能性があります。" : "処理を中断しました";
        }
        catch (ArgumentException error)
        {
            // ArgumentOutOfRangeException・ArgumentNullExceptionもここに入ります。
            status = "入力・設定を見直してください";
            Debug.LogWarning(error.Message, this);
        }
        catch (RankingApiException error)
        {
            status = submissionStarted
                ? "送信結果を確認できません。登録済みの可能性があるため自動再送しません。"
                : "ランキングを準備できません。LauncherとServerの接続・設定を確認してください。";
            Debug.LogWarning(error.Message, this);
        }
        catch (Exception error)
        {
            status = submissionStarted ? "送信結果を確認できません。ログを確認してください。" : "予期しないエラーです。ログを確認してください。";
            Debug.LogException(error, this);
        }
        finally
        {
            // エラー時にも送信中の状態を解除し、ゲーム本編は続行します。
            IsBusy = false;
        }
    }

    // Rust側と同じくUnicodeコードポイントで数えます。絵文字のサロゲートペアは1文字。
    private static int ScalarCount(string value)
    {
        int count = 0;
        for (int index = 0; index < value.Length; index++, count++)
        {
            if (!char.IsSurrogate(value[index])) continue;
            if (!char.IsHighSurrogate(value[index]) || index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                throw new ArgumentException("文字列に不正なUnicode文字が含まれています。");
            index++;
        }
        return count;
    }

    private void OnDestroy()
    {
        // シーンを閉じた後まで送信待ちを続けない。Server側の登録取り消しではありません。
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
