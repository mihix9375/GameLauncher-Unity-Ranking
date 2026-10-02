using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GameLauncher.Ranking
{
    /// <summary>ランキングで大きい値・小さい値のどちらを上位にするかを指定します。</summary>
    public enum RankingOrder
    {
        HighScore,
        LowScore,
    }

    /// <summary>ゲームが利用できる2つのランキング枠です。</summary>
    public enum LeaderboardSlot
    {
        Slot0 = 0,
        Slot1 = 1,
    }

    /// <summary>Launcherと同期するランキング1枠分の設定です。</summary>
    [Serializable]
    public sealed class LeaderboardDefinition
    {
        [SerializeField] private string displayName = "ランキング";
        [SerializeField] private RankingOrder order = RankingOrder.HighScore;

        public string DisplayName => displayName;
        public RankingOrder Order => order;

        public LeaderboardDefinition(string displayName, RankingOrder order = RankingOrder.HighScore)
        {
            this.displayName = displayName;
            this.order = order;
        }
    }

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
        [SerializeField] private bool enabled;
        [SerializeField] private ScoreEntry[] entries;

        public LeaderboardSlot Slot => id == "1" ? LeaderboardSlot.Slot1 : LeaderboardSlot.Slot0;
        public string Name => name;
        public string Order => order;
        public bool Enabled => enabled;
        public ScoreEntry[] Entries => entries ?? Array.Empty<ScoreEntry>();
    }

    /// <summary>ランキング0または1を直感的に操作するためのオブジェクトです。</summary>
    public sealed class Ranking
    {
        public LeaderboardSlot Slot { get; }
        public string Title => Snapshot?.Name ?? string.Empty;
        public RankingOrder Order => Snapshot?.Order == "low_score" ? RankingOrder.LowScore : RankingOrder.HighScore;
        public bool Enabled => Snapshot?.Enabled ?? false;
        public ScoreEntry[] Entries => Snapshot?.Entries ?? Array.Empty<ScoreEntry>();

        internal Leaderboard Snapshot { get; private set; }

        internal Ranking(LeaderboardSlot slot, Leaderboard snapshot)
        {
            Slot = slot;
            Snapshot = snapshot;
        }

        public async Task SetAsync(
            string title,
            RankingOrder order = RankingOrder.HighScore,
            CancellationToken cancellationToken = default)
        {
            Snapshot = await RankingApi.SetRankingAsync(Slot, title, order, null, cancellationToken);
        }

        public Task<ScoreResult> InsertAsync(
            string playerName,
            long score,
            CancellationToken cancellationToken = default)
        {
            return RankingApi.SubmitScoreAsync(Slot, playerName, score, cancellationToken);
        }

        /// <summary>このランキング枠の最新順位表を取得します。</summary>
        public async Task<ScoreEntry[]> GetAsync(CancellationToken cancellationToken = default)
        {
            Snapshot = await RankingApi.GetRankingAsync(Slot, cancellationToken);
            return Entries;
        }

        public async Task EnableAsync(CancellationToken cancellationToken = default)
        {
            Snapshot = await RankingApi.SetRankingAsync(Slot, null, null, true, cancellationToken);
        }

        public async Task DisableAsync(CancellationToken cancellationToken = default)
        {
            Snapshot = await RankingApi.SetRankingAsync(Slot, null, null, false, cancellationToken);
        }
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
        [SerializeField] private Leaderboard[] leaderboards;

        public Leaderboard[] Leaderboards => leaderboards ?? Array.Empty<Leaderboard>();
    }

    [Serializable]
    internal sealed class SyncLeaderboardDefinition
    {
        public string name;
        public string order;
        public bool enabled;
    }

    [Serializable]
    internal sealed class SyncLeaderboardsRequest
    {
        public SyncLeaderboardDefinition[] leaderboards;
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
