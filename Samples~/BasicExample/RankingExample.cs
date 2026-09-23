using System;
using GameLauncher.Ranking;
using UnityEngine;

/// <summary>
/// InspectorへゲームID・ランキングID・プレイヤー名を入力し、
/// Context Menuから通信を試せる最小サンプルです。
/// </summary>
public sealed class RankingExample : MonoBehaviour
{
    [Header("GameServerで設定した値")]
    [SerializeField] private string gameId = "TestGame";
    [SerializeField] private string leaderboardId = "high_score";

    [Header("送信するテストデータ")]
    [SerializeField] private string playerName = "PLAYER";
    [SerializeField] private long score = 1000;

    [ContextMenu("テストスコアを送信")]
    public async void SubmitTestScore()
    {
        try
        {
            ScoreResult result = await RankingApi.SubmitScoreAsync(
                gameId,
                leaderboardId,
                playerName,
                score);
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
            Leaderboard[] boards = await RankingApi.GetLeaderboardsAsync(gameId);
            foreach (Leaderboard board in boards)
            {
                Debug.Log($"ランキング: {board.Name} ({board.Id})");
                foreach (ScoreEntry entry in board.Entries)
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
