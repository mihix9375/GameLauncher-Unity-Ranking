using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace GameLauncher.Ranking
{
    /// <summary>
    /// GameLauncher経由でランキングを利用する入口です。
    /// 通常はBaseUrlを変更せず、GetLeaderboardsAsyncとSubmitScoreAsyncを呼ぶだけで使えます。
    /// </summary>
    public static class RankingApi
    {
        public const string DefaultBaseUrl = "http://127.0.0.1:50053";

        /// <summary>
        /// Unity EditorでAPIを呼んだときに、接続先や失敗原因の診断ログを表示するかどうかです。
        /// 診断処理そのものはUNITY_EDITORのときだけコンパイルされ、製品ビルドには入りません。
        /// </summary>
        public static bool EnableEditorDiagnostics { get; set; } = true;

        /// <summary>接続先です。特別な構成でない限り変更しないでください。</summary>
        public static string BaseUrl { get; set; } = DefaultBaseUrl;

        /// <summary>通信を待つ最大秒数です。</summary>
        public static int TimeoutSeconds { get; set; } = 5;

        /// <summary>
        /// 指定したゲームのランキング一覧を取得します。
        /// gameIdには配布ZIPのmeta.jsonと同じidを指定します。
        /// </summary>
        public static async Task<Leaderboard[]> GetLeaderboardsAsync(
            string gameId,
            CancellationToken cancellationToken = default)
        {
            RequireValue(gameId, nameof(gameId));
#if UNITY_EDITOR
            if (EditorDiagnostics.BlockProductionRequests)
            {
                throw EditorDiagnostics.CreateEditorOnlyException("ランキング取得", gameId, null);
            }
#endif
            string url = $"{NormalizedBaseUrl()}/v1/games/{UnityWebRequest.EscapeURL(gameId)}/leaderboards";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                string json = await SendAsync(request, cancellationToken);
                try
                {
                    LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(json);
                    Leaderboard[] leaderboards = response?.Leaderboards ?? Array.Empty<Leaderboard>();
                    return leaderboards;
                }
                catch (Exception error)
                {
                    throw new RankingApiException("ランキング応答を読み取れませんでした。", error);
                }
            }
        }

        /// <summary>
        /// 指定したランキングへ1件のスコアを送信します。
        /// leaderboardIdにはGameServer管理画面で設定したIDを指定します。
        /// </summary>
        public static async Task<ScoreResult> SubmitScoreAsync(
            string gameId,
            string leaderboardId,
            string playerName,
            long score,
            CancellationToken cancellationToken = default)
        {
            RequireValue(gameId, nameof(gameId));
            RequireValue(leaderboardId, nameof(leaderboardId));
            RequireValue(playerName, nameof(playerName));
#if UNITY_EDITOR
            if (EditorDiagnostics.BlockProductionRequests)
            {
                throw EditorDiagnostics.CreateEditorOnlyException("スコア送信", gameId, leaderboardId);
            }
#endif

            string url = $"{NormalizedBaseUrl()}/v1/games/{UnityWebRequest.EscapeURL(gameId)}" +
                         $"/leaderboards/{UnityWebRequest.EscapeURL(leaderboardId)}/scores";
            string json = JsonUtility.ToJson(new ScoreRequest {
                player_name = playerName.Trim(),
                score = score,
            });

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                string responseJson = await SendAsync(request, cancellationToken);
                try
                {
                    ScoreResult result = JsonUtility.FromJson<ScoreResult>(responseJson);
                    if (result == null)
                    {
                        throw new RankingApiException("スコア送信応答が空です。");
                    }
                    return result;
                }
                catch (RankingApiException)
                {
                    throw;
                }
                catch (Exception error)
                {
                    throw new RankingApiException("スコア送信応答を読み取れませんでした。", error);
                }
            }
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
                catch (Exception)
                {
                    // JSON以外の本文は、そのまま下でエラーとして表示します。
                }
                return body;
            }

            return string.IsNullOrWhiteSpace(request.error)
                ? "GameLauncherのランキングAPIに接続できません。GameLauncherが起動しているか確認してください。"
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
            if (string.IsNullOrWhiteSpace(value))
            {
#if UNITY_EDITOR
                EditorDiagnostics.InvalidParameter(parameterName);
#endif
                throw new ArgumentException("空文字は指定できません。", parameterName);
            }
        }

#if UNITY_EDITOR
        private static class EditorDiagnostics
        {
            private const string Prefix = "[GameLauncher Ranking 診断]";

            // constにすると、この後の本番通信用コードがコンパイラーから到達不能と判定されます。
            // プロパティにして、Editorでは必ず本番通信を止めつつ警告を発生させないようにします。
            internal static bool BlockProductionRequests => true;

            internal static RankingApiException CreateEditorOnlyException(
                string operation,
                string gameId,
                string leaderboardId)
            {
                string leaderboard = string.IsNullOrWhiteSpace(leaderboardId)
                    ? string.Empty
                    : $" / leaderboardId: {leaderboardId}";
                string message =
                    $"{operation}の引数を確認しました。gameId: {gameId}{leaderboard}\n" +
                    "Unity Editorから本番ランキング通信は行いません。実通信はゲームをビルドし、GameLauncherから起動して確認してください。";

                if (EnableEditorDiagnostics)
                {
                    Debug.LogWarning($"{Prefix} {message}");
                }

                return new RankingApiException(message);
            }

            internal static void InvalidParameter(string parameterName)
            {
                if (!EnableEditorDiagnostics) return;
                Debug.LogError(
                    $"{Prefix} 引数 {parameterName} が空です。" +
                    "gameIdはmeta.jsonのid、leaderboardIdはGameServer管理画面の設定と完全に一致させてください。");
            }
        }
#endif
    }
}
