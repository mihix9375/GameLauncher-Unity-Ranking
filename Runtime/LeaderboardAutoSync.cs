using UnityEngine;

namespace GameLauncher.Ranking
{
    /// <summary>
    /// シーン開始時にInspectorの配列をLauncherへ同期します。
    /// 配列の0番がSlot0、1番がSlot1です。公開後は順番を入れ替えないでください。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class LeaderboardAutoSync : MonoBehaviour
    {
        [SerializeField] private LeaderboardDefinition[] leaderboards = new LeaderboardDefinition[0];

        public LeaderboardDefinition[] Leaderboards => leaderboards;

        private async void Awake()
        {
            // コンポーネントを追加しただけで既存設定を空にしない。
            if (leaderboards == null || leaderboards.Length == 0) return;
            try
            {
                await RankingApi.SyncLeaderboardsAsync(leaderboards);
            }
            catch (RankingApiException error)
            {
#if !UNITY_EDITOR
                Debug.LogError($"ランキング設定を同期できませんでした: {error.Message}", this);
#endif
            }
        }
    }
}
