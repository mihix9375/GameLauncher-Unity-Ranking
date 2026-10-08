// APIの回帰テスト専用。Unityや実際のServerへは接続しません。
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
namespace UnityEngine {
    public class SerializeField : Attribute { }
    public static class Debug {
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }
    public static class JsonUtility {
        public static T FromJson<T>(string text) => (T)Read(JsonDocument.Parse(text).RootElement, typeof(T));
        public static string ToJson(object value) => JsonSerializer.Serialize(Write(value));
        private static object Read(JsonElement e, Type type) {
            if (e.ValueKind == JsonValueKind.Null) return null;
            if (type == typeof(string)) return e.GetString();
            if (type == typeof(bool)) return e.GetBoolean();
            if (type == typeof(int)) return e.GetInt32();
            if (type == typeof(long)) return e.GetInt64();
            if (type.IsArray) {
                var a = Array.CreateInstance(type.GetElementType(), e.GetArrayLength()); int i = 0;
                foreach (var item in e.EnumerateArray()) a.SetValue(Read(item, type.GetElementType()), i++);
                return a;
            }
            var obj = Activator.CreateInstance(type);
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (e.TryGetProperty(field.Name, out var child)) field.SetValue(obj, Read(child, field.FieldType));
            return obj;
        }
        private static object Write(object value) {
            if (value == null || value is string || value is bool || value is long || value is int) return value;
            if (value is Array a) { var result = new List<object>(); foreach (var item in a) result.Add(Write(item)); return result; }
            var fields = new Dictionary<string, object>();
            foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) fields[field.Name] = Write(field.GetValue(value));
            return fields;
        }
    }
}
namespace UnityEngine.Networking {
    public class DownloadHandler { public string text; }
    public class DownloadHandlerBuffer : DownloadHandler { }
    public class UploadHandler { }
    public class UploadHandlerRaw : UploadHandler { public byte[] data; public UploadHandlerRaw(byte[] data) { this.data = data; } }
    public class UnityWebRequestAsyncOperation { public bool isDone; }
    public class UnityWebRequest : IDisposable {
        public enum Result { Success, ProtocolError, ConnectionError }
        public const string kHttpVerbPUT = "PUT", kHttpVerbPOST = "POST";
        public string url, method, error; public int timeout; public long responseCode;
        public Result result; public UploadHandler uploadHandler; public DownloadHandler downloadHandler;
        public UnityWebRequest(string url, string method) { this.url = url; this.method = method; }
        public static UnityWebRequest Get(string url) => new UnityWebRequest(url, "GET") { downloadHandler = new DownloadHandlerBuffer() };
        public void SetRequestHeader(string key, string value) { }
        public void Abort() { }
        public void Dispose() { }
        public UnityWebRequestAsyncOperation SendWebRequest() {
            FakeTransport.Calls++;
            var operation = new UnityWebRequestAsyncOperation(); _ = Complete(operation); return operation;
        }
        private async Task Complete(UnityWebRequestAsyncOperation operation) {
            if (method == "PUT" && FakeTransport.WriteGate != null) await FakeTransport.WriteGate.Task;
            (responseCode, downloadHandler.text) = FakeTransport.Respond(this);
            if (method == "GET" && FakeTransport.ReadGate != null) await FakeTransport.ReadGate.Task;
            result = responseCode == 200 ? Result.Success : Result.ProtocolError;
            error = result == Result.Success ? null : "HTTP " + responseCode;
            FakeTransport.OnResponse?.Invoke();
            operation.isDone = true;
        }
    }
}
public static class FakeTransport {
    public static TaskCompletionSource WriteGate;
    public static TaskCompletionSource ReadGate;
    public static Action OnResponse;
    public static int Calls, Submissions;
    public static string Score;
    public static List<(string name, string order, bool enabled)> Boards = new();
    public static Func<UnityEngine.Networking.UnityWebRequest, (long, string)> Override;
    public static (long, string) Respond(UnityEngine.Networking.UnityWebRequest request) {
        if (Override != null) return Override(request);
        if (request.method == "PUT") {
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(((UnityEngine.Networking.UploadHandlerRaw)request.uploadHandler).data));
            Boards.Clear();
            foreach (var board in json.RootElement.GetProperty("leaderboards").EnumerateArray())
                Boards.Add((board.GetProperty("name").GetString(), board.GetProperty("order").GetString(), board.GetProperty("enabled").GetBoolean()));
        }
        if (request.method == "POST") {
            Submissions++;
            if (Boards.Count == 0 || !Boards[0].enabled) return (400, "{\"message\":\"disabled\"}");
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(((UnityEngine.Networking.UploadHandlerRaw)request.uploadHandler).data));
            Score = json.RootElement.GetProperty("score").GetString();
            return (200, "{\"ok\":true,\"rank\":1}");
        }
        var boards = new List<object>();
        for (int i = 0; i < Boards.Count; i++) boards.Add(new { id = i.ToString(), name = Boards[i].name, order = Boards[i].order, enabled = Boards[i].enabled, entries = Array.Empty<object>() });
        return (200, JsonSerializer.Serialize(new { leaderboards = boards }));
    }
}
