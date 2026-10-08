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
            return await Task.FromException<Leaderboard[]>(EditorDiagnostics.CreateEditorOnlyException("ランキング取得", null));
#else
            using (UnityWebRequest request = UnityWebRequest.Get($"{NormalizedBaseUrl()}/v1/leaderboards"))
            {
                Authenticate(request);
                string json = await SendAsync(request, cancellationToken);
                try
                {
                    LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(json);
                    return ValidateResponse(response);
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
                    ValidateTitle(definition.DisplayName, $"{nameof(leaderboards)}[{index}].DisplayName");
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
            if (title != null) ValidateTitle(title, nameof(title));
            if (order.HasValue) OrderValue(order.Value);
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
            return await Task.FromException<Leaderboard[]>(EditorDiagnostics.CreateEditorOnlyException("ランキング設定の同期", null));
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
                    Leaderboard[] result = ValidateResponse(response);
                    if (result.Length != definitions.Length)
                        throw new RankingApiException("ランキング同期応答の件数が不正です。");
                    foreach (Leaderboard board in result)
                    {
                        SyncLeaderboardDefinition requested = definitions[(int)board.Slot];
                        if (board.Name != requested.name || board.Order != requested.order || board.Enabled != requested.enabled)
                            throw new RankingApiException("ランキング設定が応答に反映されていません。登録済みの可能性があるため、自動再送しないでください。");
                    }
                    return result;
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
            if (order != RankingOrder.HighScore && order != RankingOrder.LowScore)
                throw new ArgumentOutOfRangeException(nameof(order), "定義済みの並び順を指定してください。");
            return order == RankingOrder.LowScore ? "low_score" : "high_score";
        }

        /// <summary>現在のゲームの指定ランキングへスコアを送信します。</summary>
        public static async Task<ScoreResult> SubmitScoreAsync(
            LeaderboardSlot slot,
            string playerName,
            long score,
            CancellationToken cancellationToken = default)
        {
            return await SubmitScoreAsync(slot, playerName, (RankingScore)score, cancellationToken);
        }

        public static async Task<ScoreResult> SubmitScoreAsync(
            LeaderboardSlot slot,
            string playerName,
            RankingScore score,
            CancellationToken cancellationToken = default)
        {
            ValidateSlot(slot);
            ValidateText(playerName, nameof(playerName), 24);
#if UNITY_EDITOR
            return await Task.FromException<ScoreResult>(EditorDiagnostics.CreateEditorOnlyException("スコア送信", ((int)slot).ToString()));
#else
            // 設定・有効化が完了する前に送信しない。再送は行わない。
            await ConfigurationLock.WaitAsync(cancellationToken);
            try
            {
                string url = $"{NormalizedBaseUrl()}/v1/leaderboards/" +
                             $"{(int)slot}/scores";
                string json = JsonUtility.ToJson(new ScoreRequest {
                    player_name = playerName.Trim(),
                    score = score.RawValue,
                });
                using (UnityWebRequest request = JsonRequest(url, UnityWebRequest.kHttpVerbPOST, json))
                {
                    Authenticate(request);
                    string responseJson = await SendAsync(request, cancellationToken);
                    try
                    {
                        ScoreResult result = JsonUtility.FromJson<ScoreResult>(responseJson);
                        if (result == null || !result.Ok || result.Rank < 1)
                            throw new RankingApiException("スコア送信の成功を確認できませんでした。登録済みの可能性があるため、自動再送しないでください。");
                        return result;
                    }
                    catch (RankingApiException) { throw; }
                    catch (Exception error)
                    {
                        throw new RankingApiException("スコア送信応答を読み取れませんでした。", error);
                    }
                }
            }
            finally { ConfigurationLock.Release(); }
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
            cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
            if (request.result == UnityWebRequest.Result.Success)
            {
                return request.downloadHandler?.text ?? string.Empty;
            }
            if (request.responseCode > 0)
                throw new RankingApiException(ReadError(request), request.responseCode);
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

        internal static void ValidateTitle(string value, string parameterName)
            => ValidateText(value, parameterName, 40);

        private static void ValidateText(string value, string parameterName, int maximum)
        {
            if (value == null) throw new ArgumentNullException(parameterName);
            RequireValue(value, parameterName);
            value = value.Trim();
            int count = 0;
            for (int index = 0; index < value.Length; index++)
            {
                char ch = value[index];
                if (char.IsControl(ch)) throw new ArgumentException("制御文字は使えません。", parameterName);
                if (char.IsHighSurrogate(ch))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                        throw new ArgumentException("不正なUnicode文字です。", parameterName);
                    index++;
                }
                else if (char.IsLowSurrogate(ch)) throw new ArgumentException("不正なUnicode文字です。", parameterName);
                count++;
            }
            if (count > maximum) throw new ArgumentException($"{maximum}文字以内で指定してください。", parameterName);
        }

        private static Leaderboard[] ValidateResponse(LeaderboardResponse response)
        {
            Leaderboard[] boards = response?.Leaderboards;
            if (boards == null || boards.Length > 2) throw new RankingApiException("ランキング応答が不正です。");
            int slots = 0;
            foreach (Leaderboard board in boards)
            {
                if (board == null || !board.HasEntries) throw new RankingApiException("ランキング応答が不正です。");
                int bit = 1 << (int)board.Slot;
                if ((slots & bit) != 0) throw new RankingApiException("ランキング応答のスロットが重複しています。");
                slots |= bit;
                ValidateTitle(board.Name, "response.name");
                if (board.Order != "high_score" && board.Order != "low_score")
                    throw new RankingApiException("ランキング応答の並び順が不正です。");
                if (board.Entries.Length > 10) throw new RankingApiException("ランキング応答の件数が不正です。");
                for (int index = 0; index < board.Entries.Length; index++)
                {
                    ScoreEntry entry = board.Entries[index];
                    if (entry == null || entry.Rank != index + 1 || entry.SubmittedAt < 0)
                        throw new RankingApiException("順位の応答が不正です。");
                    ValidateText(entry.PlayerName, "response.player_name", 24);
                    RankingScore.Parse(entry.RawScore);
                }
            }
            return boards;
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
