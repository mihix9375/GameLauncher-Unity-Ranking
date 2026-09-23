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
            string url = $"{NormalizedBaseUrl()}/v1/games/{UnityWebRequest.EscapeURL(gameId)}/leaderboards";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
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
                    return result ?? throw new RankingApiException("スコア送信応答が空です。");
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
                throw new ArgumentException("空文字は指定できません。", parameterName);
            }
        }
    }
}
