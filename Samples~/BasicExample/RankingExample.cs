using System;
using GameLauncher.Ranking;
using UnityEngine;

/// <summary>
/// ランキング配列の取得・設定・スコア送信を試せる最小サンプルです。
/// </summary>
public sealed class RankingExample : MonoBehaviour
{
    [Header("使用するランキング枠")]
    [SerializeField] private LeaderboardSlot leaderboardSlot = LeaderboardSlot.Slot0;

    [Header("送信するテストデータ")]
    [SerializeField] private string playerName = "PLAYER";
    [SerializeField] private long score = 1000;

    [ContextMenu("ランキング設定を同期")]
    public async void SyncLeaderboards()
    {
        try
        {
            Ranking[] ranks = await RankingApi.SyncLeaderboardsAsync();
            Ranking rank = ranks[(int)leaderboardSlot];
            await rank.SetAsync("ハイスコア", RankingOrder.HighScore);
            await rank.EnableAsync();
            Debug.Log($"{rank.Title}を有効にしました。");
        }
        catch (Exception error)
        {
            Debug.LogError($"ランキングを作成できませんでした: {error.Message}");
        }
    }

    [ContextMenu("テストスコアを送信")]
    public async void SubmitTestScore()
    {
        try
        {
            Ranking[] ranks = await RankingApi.SyncLeaderboardsAsync();
            ScoreResult result = await ranks[(int)leaderboardSlot].InsertAsync(playerName, score);
            Debug.Log($"スコアを送信しました。現在 {result.Rank} 位です。");
        }
        catch (Exception error)
        {
            Debug.LogError($"スコアを送信できませんでした: {error.Message}");
        }
    }

    [ContextMenu("ランキングを取得")]
    public async void LoadLeaderboards()
    {
        try
        {
            Ranking[] ranks = await RankingApi.SyncLeaderboardsAsync();
            foreach (Ranking rank in ranks)
            {
                if (!rank.Enabled) continue;
                Debug.Log($"ランキング: {rank.Title} ({rank.Slot})");
                ScoreEntry[] entries = await rank.GetAsync();
                foreach (ScoreEntry entry in entries)
                {
                    Debug.Log($"{entry.Rank}位 {entry.PlayerName}: {entry.Score}");
                }
            }
        }
        catch (Exception error)
        {
            Debug.LogError($"ランキングを取得できませんでした: {error.Message}");
        }
    }
}
