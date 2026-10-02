using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GameLauncher.Ranking
{
    /// <summary>GameLauncher経由でランキングを利用する入口です。ゲームIDは指定しません。</summary>
    public static class RankingApi
    {
        public const string DefaultBaseUrl = "http://127.0.0.1:50053";
        private const string SessionTokenVariable = "GAMELAUNCHER_SESSION_TOKEN";
        private static readonly SemaphoreSlim ConfigurationLock = new SemaphoreSlim(1, 1);

        public static bool EnableEditorDiagnostics { get; set; } = true;
        public static string BaseUrl { get; set; } = DefaultBaseUrl;
        public static int TimeoutSeconds { get; set; } = 5;

        /// <summary>現在のゲームに登録されたランキングを取得します。</summary>
        public static async Task<Leaderboard[]> GetLeaderboardsAsync(
            CancellationToken cancellationToken = default)
        {
#if UNITY_EDITOR
            throw EditorDiagnostics.CreateEditorOnlyException("ランキング取得", null);
#else
            using (UnityWebRequest request = UnityWebRequest.Get($"{NormalizedBaseUrl()}/v1/leaderboards"))
            {
                Authenticate(request);
                string json = await SendAsync(request, cancellationToken);
                try
                {
                    LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(json);
                    return response?.Leaderboards ?? Array.Empty<Leaderboard>();
                }
                catch (Exception error)
                {
                    throw new RankingApiException("ランキング応答を読み取れませんでした。", error);
                }
            }
#endif
        }

        /// <summary>ランキング0・1を操作するための2つのオブジェクトを取得します。</summary>
        public static async Task<Ranking[]> SyncLeaderboardsAsync(
            CancellationToken cancellationToken = default)
        {
            return BuildRankings(await GetLeaderboardsAsync(cancellationToken));
        }

        /// <summary>配列の0番・1番を有効なランキングとしてLauncherと同期します。</summary>
        public static async Task<Ranking[]> SyncLeaderboardsAsync(
            LeaderboardDefinition[] leaderboards,
            CancellationToken cancellationToken = default)
        {
            if (leaderboards == null) throw new ArgumentNullException(nameof(leaderboards));
            if (leaderboards.Length > 2)
            {
                throw new ArgumentException("ランキングは最大2つです。", nameof(leaderboards));
            }
            SyncLeaderboardDefinition[] definitions = DefaultDefinitions();
            for (int index = 0; index < 2; index++)
            {
                if (index < leaderboards.Length)
                {
                    LeaderboardDefinition definition = leaderboards[index]
                        ?? throw new ArgumentException($"{index}番のランキング設定が空です。", nameof(leaderboards));
                    RequireValue(definition.DisplayName, $"{nameof(leaderboards)}[{index}].DisplayName");
                    definitions[index].name = definition.DisplayName.Trim();
                    definitions[index].order = OrderValue(definition.Order);
                    definitions[index].enabled = true;
                }
            }
            await ConfigurationLock.WaitAsync(cancellationToken);
            try
            {
                return BuildRankings(await SendDefinitionsAsync(definitions, cancellationToken));
            }
            finally
            {
                ConfigurationLock.Release();
            }
        }

        internal static async Task<Leaderboard> SetRankingAsync(
            LeaderboardSlot slot,
            string title,
            RankingOrder? order,
            bool? enabled,
            CancellationToken cancellationToken)
        {
            ValidateSlot(slot);
            if (title != null) RequireValue(title, nameof(title));
            await ConfigurationLock.WaitAsync(cancellationToken);
            try
            {
                Leaderboard[] current = await GetLeaderboardsAsync(cancellationToken);
                SyncLeaderboardDefinition[] definitions = DefaultDefinitions();
                foreach (Leaderboard board in current)
                {
                    int index = (int)board.Slot;
                    definitions[index].name = string.IsNullOrWhiteSpace(board.Name)
                        ? definitions[index].name
                        : board.Name;
                    definitions[index].order = board.Order == "low_score" ? "low_score" : "high_score";
                    definitions[index].enabled = board.Enabled;
                }

                int target = (int)slot;
                if (title != null) definitions[target].name = title.Trim();
                if (order.HasValue) definitions[target].order = OrderValue(order.Value);
                if (enabled.HasValue) definitions[target].enabled = enabled.Value;
                Leaderboard[] updated = await SendDefinitionsAsync(definitions, cancellationToken);
                foreach (Leaderboard board in updated)
                {
                    if (board.Slot == slot) return board;
                }
                throw new RankingApiException($"ランキング{target}の同期結果を取得できませんでした。");
            }
            finally
            {
                ConfigurationLock.Release();
            }
        }

        internal static async Task<Leaderboard> GetRankingAsync(
            LeaderboardSlot slot,
            CancellationToken cancellationToken)
        {
            ValidateSlot(slot);
            Leaderboard[] leaderboards = await GetLeaderboardsAsync(cancellationToken);
            foreach (Leaderboard leaderboard in leaderboards)
            {
                if (leaderboard.Slot == slot) return leaderboard;
            }
            return null;
        }

        private static async Task<Leaderboard[]> SendDefinitionsAsync(
            SyncLeaderboardDefinition[] definitions,
            CancellationToken cancellationToken)
        {
#if UNITY_EDITOR
            throw EditorDiagnostics.CreateEditorOnlyException("ランキング設定の同期", null);
#else
            string json = JsonUtility.ToJson(new SyncLeaderboardsRequest { leaderboards = definitions });
            using (UnityWebRequest request = JsonRequest(
                $"{NormalizedBaseUrl()}/v1/leaderboards", UnityWebRequest.kHttpVerbPUT, json))
            {
                Authenticate(request);
                string responseJson = await SendAsync(request, cancellationToken);
                try
                {
                    LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(responseJson);
                    return response?.Leaderboards ?? Array.Empty<Leaderboard>();
                }
                catch (Exception error)
                {
                    throw new RankingApiException("ランキング同期応答を読み取れませんでした。", error);
                }
            }
#endif
        }

        private static SyncLeaderboardDefinition[] DefaultDefinitions()
        {
            return new[] {
                new SyncLeaderboardDefinition { name = "ランキング1", order = "high_score", enabled = false },
                new SyncLeaderboardDefinition { name = "ランキング2", order = "high_score", enabled = false },
            };
        }

        private static Ranking[] BuildRankings(Leaderboard[] leaderboards)
        {
            Leaderboard first = null;
            Leaderboard second = null;
            foreach (Leaderboard board in leaderboards ?? Array.Empty<Leaderboard>())
            {
                if (board.Slot == LeaderboardSlot.Slot0) first = board;
                else if (board.Slot == LeaderboardSlot.Slot1) second = board;
            }
            return new[] {
                new Ranking(LeaderboardSlot.Slot0, first),
                new Ranking(LeaderboardSlot.Slot1, second),
            };
        }

        private static string OrderValue(RankingOrder order)
        {
            return order == RankingOrder.LowScore ? "low_score" : "high_score";
        }

        /// <summary>現在のゲームの指定ランキングへスコアを送信します。</summary>
        public static async Task<ScoreResult> SubmitScoreAsync(
            LeaderboardSlot slot,
            string playerName,
            long score,
            CancellationToken cancellationToken = default)
        {
            ValidateSlot(slot);
            RequireValue(playerName, nameof(playerName));
#if UNITY_EDITOR
            throw EditorDiagnostics.CreateEditorOnlyException("スコア送信", ((int)slot).ToString());
#else
            string url = $"{NormalizedBaseUrl()}/v1/leaderboards/" +
                         $"{(int)slot}/scores";
            string json = JsonUtility.ToJson(new ScoreRequest {
                player_name = playerName.Trim(),
                score = score,
            });
            using (UnityWebRequest request = JsonRequest(url, UnityWebRequest.kHttpVerbPOST, json))
            {
                Authenticate(request);
                string responseJson = await SendAsync(request, cancellationToken);
                try
                {
                    ScoreResult result = JsonUtility.FromJson<ScoreResult>(responseJson);
                    if (result == null) throw new RankingApiException("スコア送信応答が空です。");
                    return result;
                }
                catch (RankingApiException) { throw; }
                catch (Exception error)
                {
                    throw new RankingApiException("スコア送信応答を読み取れませんでした。", error);
                }
            }
#endif
        }

        private static UnityWebRequest JsonRequest(string url, string method, string json)
        {
            UnityWebRequest request = new UnityWebRequest(url, method);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private static void ValidateSlot(LeaderboardSlot slot)
        {
            if (slot != LeaderboardSlot.Slot0 && slot != LeaderboardSlot.Slot1)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "ランキング番号は0または1です。");
            }
        }

        private static void Authenticate(UnityWebRequest request)
        {
            string token = Environment.GetEnvironmentVariable(SessionTokenVariable);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new RankingApiException(
                    "GameLauncherのゲームセッションを確認できません。ゲームをGameLauncherから起動してください。");
            }
            request.SetRequestHeader("Authorization", $"Bearer {token.Trim()}");
        }

        private static async Task<string> SendAsync(
            UnityWebRequest request,
            CancellationToken cancellationToken)
        {
            request.timeout = Math.Max(1, TimeoutSeconds);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }
            if (request.result == UnityWebRequest.Result.Success)
            {
                return request.downloadHandler?.text ?? string.Empty;
            }
            throw new RankingApiException(ReadError(request));
        }

        private static string ReadError(UnityWebRequest request)
        {
            string body = request.downloadHandler?.text;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    ErrorResponse response = JsonUtility.FromJson<ErrorResponse>(body);
                    if (!string.IsNullOrWhiteSpace(response?.message)) return response.message;
                }
                catch (Exception) { }
                return body;
            }
            return string.IsNullOrWhiteSpace(request.error)
                ? "GameLauncherのランキングAPIに接続できません。"
                : request.error;
        }

        private static string NormalizedBaseUrl()
        {
            return string.IsNullOrWhiteSpace(BaseUrl)
                ? DefaultBaseUrl
                : BaseUrl.Trim().TrimEnd('/');
        }

        private static void RequireValue(string value, string parameterName)
        {
            if (!string.IsNullOrWhiteSpace(value)) return;
#if UNITY_EDITOR
            EditorDiagnostics.InvalidParameter(parameterName);
#endif
            throw new ArgumentException("空文字は指定できません。", parameterName);
        }

#if UNITY_EDITOR
        private static class EditorDiagnostics
        {
            private const string Prefix = "[GameLauncher Ranking 診断]";

            internal static RankingApiException CreateEditorOnlyException(
                string operation,
                string slot)
            {
                string leaderboard = string.IsNullOrWhiteSpace(slot)
                    ? string.Empty
                    : $" / slot: {slot}";
                string message =
                    $"{operation}の引数を確認しました{leaderboard}。\n" +
                    "Unity Editorから本番ランキング通信は行いません。ゲームをビルドし、GameLauncherから起動して確認してください。";
                if (EnableEditorDiagnostics) Debug.LogWarning($"{Prefix} {message}");
                return new RankingApiException(message);
            }

            internal static void InvalidParameter(string parameterName)
            {
                if (!EnableEditorDiagnostics) return;
                Debug.LogError($"{Prefix} 引数 {parameterName} が空です。");
            }
        }
#endif
    }
}
