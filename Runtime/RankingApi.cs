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
            string url = $"{NormalizedBaseUrl()}/v1/games/{UnityWebRequest.EscapeURL(gameId)}/leaderboards";

#if UNITY_EDITOR
            EditorDiagnostics.RequestStarted("ランキング取得", gameId, null, url);
#endif

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                string json = await SendAsync(request, cancellationToken);
                try
                {
                    LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(json);
                    Leaderboard[] leaderboards = response?.Leaderboards ?? Array.Empty<Leaderboard>();
#if UNITY_EDITOR
                    EditorDiagnostics.RequestSucceeded("ランキング取得", $"{leaderboards.Length}件のランキングを受信しました。");
#endif
                    return leaderboards;
                }
                catch (Exception error)
                {
#if UNITY_EDITOR
                    EditorDiagnostics.InvalidResponse("ランキング取得", json, error);
#endif
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

#if UNITY_EDITOR
            EditorDiagnostics.RequestStarted("スコア送信", gameId, leaderboardId, url);
#endif

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
#if UNITY_EDITOR
                    EditorDiagnostics.RequestSucceeded("スコア送信", $"送信に成功しました。現在順位: {result.Rank}位");
#endif
                    return result;
                }
                catch (RankingApiException)
                {
#if UNITY_EDITOR
                    EditorDiagnostics.InvalidResponse("スコア送信", responseJson, null);
#endif
                    throw;
                }
                catch (Exception error)
                {
#if UNITY_EDITOR
                    EditorDiagnostics.InvalidResponse("スコア送信", responseJson, error);
#endif
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

#if UNITY_EDITOR
            EditorDiagnostics.RequestFailed(request);
#endif
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

            internal static void RequestStarted(
                string operation,
                string gameId,
                string leaderboardId,
                string url)
            {
                if (!EnableEditorDiagnostics) return;

                string leaderboard = string.IsNullOrWhiteSpace(leaderboardId)
                    ? string.Empty
                    : $" / leaderboardId: {leaderboardId}";
                Debug.Log($"{Prefix} {operation}を開始します。gameId: {gameId}{leaderboard}\n接続先: {url}");
            }

            internal static void RequestSucceeded(string operation, string detail)
            {
                if (!EnableEditorDiagnostics) return;
                Debug.Log($"{Prefix} {operation} 成功: {detail}");
            }

            internal static void InvalidParameter(string parameterName)
            {
                if (!EnableEditorDiagnostics) return;
                Debug.LogError(
                    $"{Prefix} 引数 {parameterName} が空です。" +
                    "gameIdはmeta.jsonのid、leaderboardIdはGameServer管理画面の設定と完全に一致させてください。");
            }

            internal static void RequestFailed(UnityWebRequest request)
            {
                if (!EnableEditorDiagnostics) return;

                string advice;
                if (request.responseCode == 0)
                {
                    advice = "GameLauncherが起動しているか、ランキングAPIが127.0.0.1:50053で待ち受けているか確認してください。";
                }
                else if (request.responseCode == 404)
                {
                    advice = "gameIdとleaderboardIdが、meta.jsonおよびGameServer管理画面の設定と一致しているか確認してください。";
                }
                else if (request.responseCode >= 500)
                {
                    advice = "GameLauncherからGameServerへ接続できているか、GameServerのログとランキング設定を確認してください。";
                }
                else
                {
                    advice = "Consoleの応答本文とGameServer管理画面のランキング設定を確認してください。";
                }

                string body = Shorten(request.downloadHandler?.text);
                Debug.LogError(
                    $"{Prefix} 通信に失敗しました。\n" +
                    $"URL: {request.url}\n" +
                    $"HTTP: {request.responseCode} / Result: {request.result} / Error: {request.error}\n" +
                    $"応答: {(string.IsNullOrWhiteSpace(body) ? "(なし)" : body)}\n" +
                    $"確認ポイント: {advice}");
            }

            internal static void InvalidResponse(string operation, string response, Exception error)
            {
                if (!EnableEditorDiagnostics) return;

                Debug.LogError(
                    $"{Prefix} {operation}の応答を解析できませんでした。\n" +
                    $"応答: {(string.IsNullOrWhiteSpace(response) ? "(空)" : Shorten(response))}\n" +
                    $"例外: {(error == null ? "(なし)" : error.Message)}");
            }

            private static string Shorten(string value)
            {
                if (string.IsNullOrWhiteSpace(value)) return value;
                const int maxLength = 500;
                return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
            }
        }
#endif
    }
}
