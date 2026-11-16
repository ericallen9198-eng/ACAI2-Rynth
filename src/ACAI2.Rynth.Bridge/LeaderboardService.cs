using System.Globalization;
using System.Text.Json;
using ACAI2.Protocol;

namespace ACAI2.Bridge;

internal sealed class LeaderboardService
{
    private sealed class State
    {
        public State() { }
        public string Server { get; set; } = "";
        public string Character { get; set; } = "";
        public Dictionary<string, LeaderboardResult> Latest { get; set; } = new();
        public Dictionary<string, DateTime> Attempts { get; set; } = new();
    }
    private readonly KeyferOptions options;
    private readonly string path;
    private readonly object sync = new();
    private readonly List<string> pending = new();
    private State? state;
    private LeaderboardJob? active;
    private string activeInstance = "";
    private readonly TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    internal LeaderboardService(KeyferOptions options)
    {
        this.options = options;
        path = Path.Combine(options.DataRoot, "Servers", IdentityPaths.SafeSegment(options.Server.ToLowerInvariant()), "Keyfer", "Leaderboards", IdentityPaths.SafeSegment(options.Character.ToLowerInvariant()) + ".json");
    }
    private State Load()
    {
        if (state != null) return state;
        var saved = File.Exists(path) ? JsonSerializer.Deserialize<State>(File.ReadAllText(path)) : null;
        if (saved != null && (!options.Matches(saved.Server, saved.Character) || saved.Latest == null || saved.Attempts == null)) throw new IOException("Leaderboard identity mismatch.");
        return state = saved ?? new State { Server = options.Server, Character = options.Character };
    }
    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(Load())); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private void Queue(string board, bool priority)
    {
        if (DreamWeaveLeaderboards.Command(board) == null || active?.Board == board) return;
        if (pending.Contains(board)) { if (!priority) return; pending.Remove(board); }
        if (priority) pending.Insert(0, board); else pending.Add(board);
    }
    internal LeaderboardJob? Poll(string instance, DateTime now)
    {
        lock (sync)
        {
            if (active != null && DateTime.Parse(active.ExpiresUtc, null, DateTimeStyles.RoundtripKind) <= now) { active = null; activeInstance = ""; }
            if (active != null) return null;
            var saved = Load();
            DateTime today = TimeZoneInfo.ConvertTimeFromUtc(now, zone).Date;
            foreach (string id in DreamWeaveLeaderboards.Ids)
            {
                bool todaySuccess = saved.Latest.TryGetValue(id, out var latest) && TimeZoneInfo.ConvertTimeFromUtc(DateTime.Parse(latest.ObservedUtc, null, DateTimeStyles.RoundtripKind), zone).Date == today;
                if (!todaySuccess && (!saved.Attempts.TryGetValue(id, out var attempt) || now - attempt >= TimeSpan.FromMinutes(15))) Queue(id, false);
            }
            if (pending.Count == 0) return null;
            string board = pending[0]; pending.RemoveAt(0);
            // Persist dispatch time; a restart cannot cause an immediate command storm.
            saved.Attempts[board] = now; Save();
            active = new LeaderboardJob { Id = Guid.NewGuid().ToString("N"), Board = board, ExpiresUtc = now.AddSeconds(30).ToString("O") };
            activeInstance = instance;
            return active;
        }
    }
    internal void Receive(LeaderboardResult result, DateTime now)
    {
        lock (sync)
        {
            if (active == null || active.Id != result.JobId || active.Board != result.Board || activeInstance != result.InstanceId || !options.Matches(result.Server, result.Character) ||
                DateTime.Parse(active.ExpiresUtc, null, DateTimeStyles.RoundtripKind) < now) throw new ArgumentException("Leaderboard job is stale or mismatched.");
            if (!DateTime.TryParse(result.ObservedUtc, null, DateTimeStyles.RoundtripKind, out var observed) || observed.Kind != DateTimeKind.Utc || Math.Abs((now - observed).TotalSeconds) > 60)
                throw new ArgumentException("Invalid leaderboard timestamp.");
            if (string.IsNullOrEmpty(result.Error))
            {
                if (result.Lines == null || result.Lines.Length < 2 || result.Lines.Length > 21 || result.Lines.Any(l => l == null || l.Length > 600)) throw new ArgumentException("Invalid leaderboard output.");
                var parser = new LeaderboardCapture(result.Board, now);
                foreach (string line in result.Lines) parser.Observe(line, now);
                if (!parser.Lines.SequenceEqual(result.Lines)) throw new ArgumentException("Unrecognized native leaderboard output.");
                Load().Latest[result.Board] = result; Save();
                Console.WriteLine("[LEADERBOARDS] Captured " + DreamWeaveLeaderboards.Command(result.Board) + ": " + (result.Lines.Length - 1) + " ranked rows at " + result.ObservedUtc);
            }
            else Console.WriteLine("[LEADERBOARDS] No recognized output for " + DreamWeaveLeaderboards.Command(result.Board) + "; keeping previous snapshot.");
            // Failed/unrecognized captures do not replace good snapshots or invent an empty board.
            active = null; activeInstance = "";
        }
    }
    internal void Request(string[] boards, DateTime now)
    {
        lock (sync)
            foreach (string id in boards.Reverse())
                if (!Load().Attempts.TryGetValue(id, out var attempted) || now - attempted >= TimeSpan.FromSeconds(30)) Queue(id, true);
    }
    internal DateTime? Captured(string board)
    {
        lock (sync) return Load().Latest.TryGetValue(board, out var value) ? DateTime.Parse(value.ObservedUtc, null, DateTimeStyles.RoundtripKind) : null;
    }
    internal string Context(string[] requested, DateTime now)
    {
        lock (sync)
        {
            var saved = Load();
            var ids = requested.Length == 0 ? DreamWeaveLeaderboards.Ids : requested;
            return JsonSerializer.Serialize(ids.Select(id => new { command = DreamWeaveLeaderboards.Command(id), meaning = DreamWeaveLeaderboards.Description(id),
                observation = saved.Latest.TryGetValue(id, out var result) ? new { utc = result.ObservedUtc, lines = result.Lines, stale = now - DateTime.Parse(result.ObservedUtc, null, DateTimeStyles.RoundtripKind) > TimeSpan.FromDays(1) } : null,
                refreshPending = pending.Contains(id) || active?.Board == id }));
        }
    }
}
