using System;
using System.Numerics;
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
        [SerializeField] private string score;
        [SerializeField] private long submitted_at;

        public int Rank => rank;
        public string PlayerName => player_name;
        public RankingScore Score => RankingScore.Parse(score);
        public string RawScore => score;
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

        public LeaderboardSlot Slot => id == "0" ? LeaderboardSlot.Slot0 :
            id == "1" ? LeaderboardSlot.Slot1 : throw new RankingApiException("ランキング応答のスロットが不正です。");
        internal bool HasEntries => entries != null;
        public string Name => name;
        public string Order => order;
        public bool Enabled => enabled;
        public ScoreEntry[] Entries => entries ?? Array.Empty<ScoreEntry>();
    }

    /// <summary>ランキング0または1を直感的に操作するためのオブジェクトです。</summary>
    public sealed class Ranking
    {
        private readonly SemaphoreSlim snapshotLock = new SemaphoreSlim(1, 1);
        private async Task<Leaderboard> UpdateSnapshotAsync(Func<Task<Leaderboard>> operation, CancellationToken cancellationToken)
        {
            await snapshotLock.WaitAsync(cancellationToken);
            try { Snapshot = await operation(); return Snapshot; }
            finally { snapshotLock.Release(); }
        }
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
            RankingApi.ValidateTitle(title, nameof(title));
            await UpdateSnapshotAsync(() => RankingApi.SetRankingAsync(Slot, title, order, null, cancellationToken), cancellationToken);
        }

        public Task<ScoreResult> InsertAsync(
            string playerName,
            long score,
            CancellationToken cancellationToken = default)
        {
            return InsertAsync(playerName, (RankingScore)score, cancellationToken);
        }

        public async Task<ScoreResult> InsertAsync(string playerName, RankingScore score, CancellationToken cancellationToken = default)
        {
            await snapshotLock.WaitAsync(cancellationToken);
            try { return await RankingApi.SubmitScoreAsync(Slot, playerName, score, cancellationToken); }
            finally { snapshotLock.Release(); }
        }
        public Task<ScoreResult> InsertAsync(string playerName, BigInteger score, CancellationToken cancellationToken = default)
            => InsertAsync(playerName, (RankingScore)score, cancellationToken);
        public Task<ScoreResult> InsertAsync(string playerName, double score, CancellationToken cancellationToken = default)
            => InsertAsync(playerName, (RankingScore)score, cancellationToken);
        public Task<ScoreResult> InsertAsync(string playerName, decimal score, CancellationToken cancellationToken = default)
            => InsertAsync(playerName, (RankingScore)score, cancellationToken);
        public Task<ScoreResult> InsertAsync(string playerName, string score, CancellationToken cancellationToken = default)
            => InsertAsync(playerName, RankingScore.Parse(score), cancellationToken);

        /// <summary>このランキング枠の最新順位表を取得します。</summary>
        public async Task<ScoreEntry[]> GetAsync(CancellationToken cancellationToken = default)
        {
            Leaderboard result = await UpdateSnapshotAsync(() => RankingApi.GetRankingAsync(Slot, cancellationToken), cancellationToken);
            return result?.Entries ?? Array.Empty<ScoreEntry>();
        }

        public async Task EnableAsync(CancellationToken cancellationToken = default)
        {
            await UpdateSnapshotAsync(() => RankingApi.SetRankingAsync(Slot, null, null, true, cancellationToken), cancellationToken);
        }

        public async Task DisableAsync(CancellationToken cancellationToken = default)
        {
            await UpdateSnapshotAsync(() => RankingApi.SetRankingAsync(Slot, null, null, false, cancellationToken), cancellationToken);
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
        /// <summary>HTTPエラーの応答コード。接続失敗・不正な成功応答などではnullです。</summary>
        public long? HttpStatusCode { get; }
        public RankingApiException(string message) : base(message) { }
        public RankingApiException(string message, Exception innerException) : base(message, innerException) { }
        public RankingApiException(string message, long httpStatusCode) : base(message)
        { HttpStatusCode = httpStatusCode; }
    }

    [Serializable]
    internal sealed class LeaderboardResponse
    {
        [SerializeField] private Leaderboard[] leaderboards;

        public Leaderboard[] Leaderboards => leaderboards;
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
        public string score;
    }

    [Serializable]
    internal sealed class ErrorResponse
    {
        public string message;
    }
}
