using System;
using System.Threading;
using System.Threading.Tasks;
using GameLauncher.Ranking;

static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS: " + name); }
static async Task<T> Throws<T>(Func<Task> action) where T : Exception {
    try { await action(); } catch (T error) { return error; }
    throw new Exception("Expected " + typeof(T).Name);
}
Environment.SetEnvironmentVariable("GAMELAUNCHER_SESSION_TOKEN", "test-only");
#if UNITY_EDITOR
await Throws<RankingApiException>(() => RankingApi.SyncLeaderboardsAsync());
await Throws<RankingApiException>(() => RankingApi.SyncLeaderboardsAsync(new[] { new LeaderboardDefinition("Score") }));
await Throws<RankingApiException>(() => RankingApi.SubmitScoreAsync(LeaderboardSlot.Slot0, "A", 1L));
await Throws<ArgumentException>(() => RankingApi.SubmitScoreAsync(LeaderboardSlot.Slot0, new string('A', 25), 1L));
Check(FakeTransport.Calls == 0, "Editor never sends production requests, but validates arguments");
#else
Ranking[] ranks = await RankingApi.SyncLeaderboardsAsync();
int before = FakeTransport.Calls;
await Throws<ArgumentNullException>(() => ranks[0].SetAsync(null));
await Throws<ArgumentException>(() => ranks[0].SetAsync(new string('A', 41)));
await Throws<ArgumentException>(() => ranks[0].SetAsync("Bad\nTitle"));
await Throws<ArgumentOutOfRangeException>(() => ranks[0].SetAsync("Score", (RankingOrder)123));
await Throws<ArgumentException>(() => RankingApi.SyncLeaderboardsAsync(new[] { new LeaderboardDefinition("Score", (RankingOrder)123) }));
await Throws<ArgumentException>(() => ranks[0].InsertAsync(new string('A', 25), 1L));
await Throws<ArgumentException>(() => ranks[0].InsertAsync("A\nB", 1L));
await Throws<ArgumentException>(() => ranks[0].InsertAsync("\ud800", 1L));
Check(FakeTransport.Calls == before, "invalid inputs rejected before any requests");
await ranks[0].SetAsync(string.Concat(System.Linq.Enumerable.Repeat("😀", 40)));
await ranks[0].EnableAsync();
await ranks[0].InsertAsync(string.Concat(System.Linq.Enumerable.Repeat("😀", 24)), "1e1000");
Check(FakeTransport.Score == "1e1000", "Unicode length and exact score transport");

FakeTransport.Boards.Clear();
FakeTransport.Submissions = 0;
FakeTransport.WriteGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
Task set = ranks[0].SetAsync("Score");
Task enable = ranks[0].EnableAsync();
Task<ScoreResult> insert = ranks[0].InsertAsync("A", 1L);
Check(FakeTransport.Submissions == 0, "insert waits for pending configuration");
FakeTransport.WriteGate.SetResult();
await Task.WhenAll(set, enable, insert);
FakeTransport.WriteGate = null;
Check(insert.Result.Ok && FakeTransport.Submissions == 1, "set enable insert completes exactly once");

FakeTransport.ReadGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
Task get = ranks[0].GetAsync();
int snapshotCalls = FakeTransport.Calls;
Task update = ranks[0].SetAsync("Updated");
Check(FakeTransport.Calls == snapshotCalls, "snapshot update waits for earlier read");
FakeTransport.ReadGate.SetResult();
await get;
FakeTransport.ReadGate = null;
await update;
Check(ranks[0].Title == "Updated", "late read cannot overwrite newer settings");

foreach (string body in new[] { "{}", "{\"ok\":false,\"rank\":1}", "{\"ok\":true,\"rank\":0}", "null", "not-json" }) {
    int calls = FakeTransport.Calls;
    FakeTransport.Override = _ => (200, body);
    await Throws<RankingApiException>(() => ranks[0].InsertAsync("A", 1L));
    Check(FakeTransport.Calls == calls + 1, "invalid success rejected without retry: " + body);
}
foreach (string body in new[] { "{}", "{\"leaderboards\":null}", "{\"leaderboards\":[null]}", "{\"leaderboards\":[{\"id\":\"9\",\"name\":\"Score\",\"entries\":[]}]}", "{\"leaderboards\":[{\"id\":\"0\",\"name\":\"Score\",\"order\":\"bad\",\"entries\":[]}]}", "{\"leaderboards\":[{\"id\":\"0\",\"name\":\"Score\",\"order\":\"high_score\",\"entries\":[{\"rank\":1,\"player_name\":\"A\",\"score\":\"NaN\"}]}]}" }) {
    FakeTransport.Override = _ => (200, body);
    await Throws<RankingApiException>(() => RankingApi.GetLeaderboardsAsync());
}
Check(true, "malformed list responses rejected");
FakeTransport.Override = _ => (200, "{\"leaderboards\":[]}");
await Throws<RankingApiException>(() => RankingApi.SyncLeaderboardsAsync(new[] { new LeaderboardDefinition("Score") }));
Check(true, "missing synchronization result rejected");
FakeTransport.Override = _ => (200, "{\"leaderboards\":[{\"id\":\"0\",\"name\":\"Wrong\",\"order\":\"high_score\",\"enabled\":true,\"entries\":[]},{\"id\":\"1\",\"name\":\"ランキング2\",\"order\":\"high_score\",\"enabled\":false,\"entries\":[]}]}");
await Throws<RankingApiException>(() => RankingApi.SyncLeaderboardsAsync(new[] { new LeaderboardDefinition("Score") }));
Check(true, "settings not reflected in successful response rejected");
FakeTransport.Override = _ => (500, "{\"message\":\"storage failed\"}");
RankingApiException failure = await Throws<RankingApiException>(() => ranks[0].InsertAsync("A", 1L));
Check(failure.HttpStatusCode == 500 && failure.Message == "storage failed", "status and reason preserved");
FakeTransport.Override = null;
using (var cancelAtCompletion = new CancellationTokenSource()) {
    FakeTransport.OnResponse = () => cancelAtCompletion.Cancel();
    await Throws<OperationCanceledException>(() => RankingApi.GetLeaderboardsAsync(cancelAtCompletion.Token));
    FakeTransport.OnResponse = null;
    Check(true, "cancellation at completion is not lost");
}
using (var cancel = new CancellationTokenSource()) {
    cancel.Cancel(); int calls = FakeTransport.Calls;
    await Throws<OperationCanceledException>(() => ranks[0].InsertAsync("A", 1L, cancel.Token));
    await Throws<OperationCanceledException>(() => RankingApi.GetLeaderboardsAsync(cancel.Token));
    Check(FakeTransport.Calls == calls, "cancelled requests never sent");
}
Check((await ranks[0].InsertAsync("A", 1L)).Ok, "locks released after failures and cancellation");
#endif
