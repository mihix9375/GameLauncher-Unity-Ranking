using System;
using UnityEngine;

namespace GameLauncher.Ranking
{
    /// <summary>ランキングに登録された1件の記録です。</summary>
    [Serializable]
    public sealed class ScoreEntry
    {
        [SerializeField] private int rank;
        [SerializeField] private string player_name;
        [SerializeField] private long score;
        [SerializeField] private long submitted_at;

        public int Rank => rank;
        public string PlayerName => player_name;
        public long Score => score;
        public long SubmittedAt => submitted_at;
    }

    /// <summary>ランキングの設定と上位記録です。</summary>
    [Serializable]
    public sealed class Leaderboard
    {
        [SerializeField] private string id;
        [SerializeField] private string name;
        [SerializeField] private string order;
        [SerializeField] private ScoreEntry[] entries;

        public string Id => id;
        public string Name => name;
        public string Order => order;
        public ScoreEntry[] Entries => entries ?? Array.Empty<ScoreEntry>();
    }

    /// <summary>スコア送信後に返る結果です。</summary>
    [Serializable]
    public sealed class ScoreResult
    {
        [SerializeField] private bool ok;
        [SerializeField] private int rank;

        public bool Ok => ok;
        public int Rank => rank;
    }

    /// <summary>GameLauncherランキングAPIの通信エラーです。</summary>
    public sealed class RankingApiException : Exception
    {
        public RankingApiException(string message) : base(message) { }
        public RankingApiException(string message, Exception innerException) : base(message, innerException) { }
    }

    [Serializable]
    internal sealed class LeaderboardResponse
    {
        [SerializeField] private string game_id;
        [SerializeField] private Leaderboard[] leaderboards;

        public string GameId => game_id;
        public Leaderboard[] Leaderboards => leaderboards ?? Array.Empty<Leaderboard>();
    }

    [Serializable]
    internal sealed class ScoreRequest
    {
        public string player_name;
        public long score;
    }

    [Serializable]
    internal sealed class ErrorResponse
    {
        public string message;
    }
}
