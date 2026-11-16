using System.Globalization;
using System.Text.Json;
using ACAI2.Protocol;
using Microsoft.Data.Sqlite;

namespace ACAI2.Bridge;

// Counts and prize selection are deterministic. Language-model output is never a ledger operation.
internal sealed class TreasureLedger
{
    private readonly KeyferOptions options;
    private readonly TimeZoneInfo zone;
    private readonly object gate = new();
    internal TreasureLedger(KeyferOptions options)
    {
        this.options = options;
        zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    }
    private SqliteConnection Open()
    {
        string path = Path.Combine(options.DataRoot, "Servers", IdentityPaths.SafeSegment(options.Server.ToLowerInvariant()), "Keyfer", "treasure.sqlite");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 2 }.ToString()); db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS treasure_reports(id TEXT PRIMARY KEY,owner TEXT NOT NULL,player TEXT NOT NULL,name TEXT NOT NULL,amount INTEGER NOT NULL,utc TEXT NOT NULL,round TEXT NOT NULL,raw TEXT NOT NULL,server TEXT NOT NULL,character TEXT NOT NULL,instance TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS treasure_outliers(id TEXT PRIMARY KEY,owner TEXT NOT NULL,player TEXT NOT NULL,name TEXT NOT NULL,utc TEXT NOT NULL,round TEXT NOT NULL,raw TEXT NOT NULL,server TEXT NOT NULL,character TEXT NOT NULL,instance TEXT NOT NULL,reason TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS treasure_late(id TEXT PRIMARY KEY,owner TEXT NOT NULL,raw TEXT NOT NULL,reason TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS treasure_round ON treasure_reports(owner,round,player);
            CREATE TABLE IF NOT EXISTS treasure_rounds(owner TEXT NOT NULL,round TEXT NOT NULL,winner TEXT NOT NULL,total INTEGER NOT NULL,state TEXT NOT NULL,instance TEXT NOT NULL DEFAULT '',updated TEXT NOT NULL,PRIMARY KEY(owner,round));
            CREATE TABLE IF NOT EXISTS treasure_settings(owner TEXT NOT NULL,key TEXT NOT NULL,value TEXT NOT NULL,PRIMARY KEY(owner,key));
            """;
        cmd.ExecuteNonQuery(); return db;
    }
    private string Owner => options.Character.ToLowerInvariant();
    internal string Round(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        return (local.Hour >= 18 ? local.Date.AddDays(1) : local.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
    internal DateTime CloseUtc(string round) => TimeZoneInfo.ConvertTimeToUtc(DateTime.ParseExact(round, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddHours(18), zone);
    private static SqliteCommand Command(SqliteConnection db, SqliteTransaction? tx, string sql, params (string, object)[] args)
    {
        var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        return cmd;
    }
    private List<TreasureStanding> Standings(SqliteConnection db, string round, SqliteTransaction? tx = null)
    {
        using var cmd = Command(db, tx, """
            WITH totals AS (SELECT player,MAX(name) name,SUM(amount) total,MAX(utc) reached FROM treasure_reports WHERE owner=$owner AND round=$round GROUP BY player),
            best AS (SELECT player,MAX(total) personal FROM (SELECT player,round,SUM(amount) total FROM treasure_reports WHERE owner=$owner GROUP BY player,round) GROUP BY player)
            SELECT t.name,t.total,b.personal FROM totals t JOIN best b ON t.player=b.player ORDER BY t.total DESC,t.reached ASC,t.player ASC LIMIT 100
            """, ("$owner", Owner), ("$round", round));
        var rows = new List<TreasureStanding>(); using var reader = cmd.ExecuteReader();
        while (reader.Read()) rows.Add(new TreasureStanding { Player = reader.GetString(0), Total = reader.GetInt64(1), PersonalBest = reader.GetInt64(2), Rank = rows.Count + 1 });
        return rows;
    }
    internal List<TreasureStanding> Standings(DateTime now) { lock (gate) { using var db = Open(); return Standings(db, Round(now)); } }
    internal string Context(DateTime now)
    {
        lock (gate)
        {
            using var db = Open();
            using var past = Command(db, null, "SELECT round,winner,total,state FROM treasure_rounds WHERE owner=$owner ORDER BY round DESC LIMIT 3", ("$owner", Owner));
            var awards = new List<object>(); using var reader = past.ExecuteReader();
            while (reader.Read()) awards.Add(new { round = reader.GetString(0), winner = reader.GetString(1), total = reader.GetInt64(2), payoutState = reader.GetString(3) });
            return JsonSerializer.Serialize(new { basis = "Observed allegiance Treasure Hunter meta reports, not verified inventory. PersonalBest means best recorded 6pm-to-6pm round, never an inferred hunt streak.",
                round = Round(now), closesUtc = CloseUtc(Round(now)), timezone = zone.Id, standings = Standings(db, Round(now)).Take(10), recentAwards = awards });
        }
    }
    internal string[] RecentBanter()
    {
        lock (gate)
        {
            using var db = Open();
            using var cmd = Command(db, null, "SELECT value FROM treasure_settings WHERE owner=$owner AND key='banter-text'", ("$owner", Owner));
            try { return JsonSerializer.Deserialize<string[]>(cmd.ExecuteScalar() as string ?? "[]") ?? Array.Empty<string>(); }
            catch (JsonException) { return Array.Empty<string>(); }
        }
    }
    internal void SaveBanter(string text)
    {
        lock (gate)
        {
            var recent = RecentBanter().Append(text).TakeLast(6).ToArray();
            using var db = Open();
            using var cmd = Command(db, null, "INSERT INTO treasure_settings VALUES($owner,'banter-text',$value) ON CONFLICT(owner,key) DO UPDATE SET value=$value",
                ("$owner", Owner), ("$value", JsonSerializer.Serialize(recent))); cmd.ExecuteNonQuery();
        }
    }
    internal bool Claim(TreasureAction action, DateTime now)
    {
        lock (gate)
        {
            using var db = Open();
            using (var source = Command(db, null, "SELECT value FROM treasure_settings WHERE owner=$owner AND key='source'", ("$owner", Owner)))
            {
                string[] lease = (source.ExecuteScalar() as string ?? "").Split('|');
                if (lease.Length != 2 || lease[0] != action.InstanceId || !DateTime.TryParse(lease[1], out var seen) || now - seen.ToUniversalTime() > TimeSpan.FromSeconds(35)) return false;
            }
            using var cmd = Command(db, null, "UPDATE treasure_rounds SET state='dispatching',instance=$instance,updated=$now WHERE owner=$owner AND round=$round AND state='pending'",
                ("$owner", Owner), ("$round", action.Round), ("$instance", action.InstanceId), ("$now", now.ToString("O")));
            return cmd.ExecuteNonQuery() == 1;
        }
    }
    internal void Result(TreasureAction action, DateTime now)
    {
        if (action.State != "submitted" && action.State != "uncertain") throw new ArgumentException("Invalid prize dispatch result.");
        lock (gate)
        {
            using var db = Open();
            using var cmd = Command(db, null, "UPDATE treasure_rounds SET state=$state,updated=$now WHERE owner=$owner AND round=$round AND instance=$instance AND state='dispatching'",
                ("$owner", Owner), ("$round", action.Round), ("$instance", action.InstanceId), ("$state", action.State), ("$now", now.ToString("O")));
            cmd.ExecuteNonQuery();
        }
    }
    internal TreasureReply Accept(TreasureSync sync, DateTime now)
    {
        if ((sync.Reports?.Length ?? 0) > 64) throw new ArgumentException("Too many treasure observations.");
        var reports = new List<(TreasureReport Report, DateTime Time, string Player, int Count)>();
        foreach (var r in sync.Reports ?? Array.Empty<TreasureReport>())
        {
            if (r == null || !Guid.TryParse(r.Id, out _) || !DateTime.TryParse(r.ObservedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time) ||
                !r.ObservedUtc.EndsWith('Z') || time > now.AddSeconds(30) || !TreasureReports.TryParse(r.Raw, out string player, out int count))
                throw new ArgumentException("Invalid or stale treasure report.");
            if ((!string.IsNullOrEmpty(r.Server) && !r.Server.Equals(sync.Server, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(r.Character) && !r.Character.Equals(sync.Character, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Treasure report identity differs from capture source.");
            reports.Add((r, time.ToUniversalTime(), player, count));
        }
        lock (gate)
        {
            using var db = Open(); using var tx = db.BeginTransaction();
            // One active capture source per Keyfer role; multiple logged-in clients must not count the same chat twice.
            using (var lease = Command(db, tx, "SELECT value FROM treasure_settings WHERE owner=$owner AND key='source'", ("$owner", Owner)))
            {
                string[] existing = (lease.ExecuteScalar() as string ?? "").Split('|');
                if (existing.Length == 2 && existing[0] != sync.InstanceId && DateTime.TryParse(existing[1], out var seen) && now - seen.ToUniversalTime() < TimeSpan.FromSeconds(35))
                    throw new ArgumentException("Another Keyfer client currently owns treasure capture.");
            }
            using (var lease = Command(db, tx, "INSERT INTO treasure_settings VALUES($owner,'source',$source) ON CONFLICT(owner,key) DO UPDATE SET value=$source", ("$owner", Owner), ("$source", sync.InstanceId + "|" + now.ToString("O")))) lease.ExecuteNonQuery();
            var accepted = new List<string>(); var warnings = new List<string>(); bool changed = false; string lastPlayer = ""; string current = Round(now);
            foreach (var (r, time, player, count) in reports.OrderBy(r => r.Time))
            {
                using (var seen = Command(db, tx, "SELECT COUNT(*) FROM treasure_reports WHERE id=$id", ("$id", r.Id.ToLowerInvariant())))
                    if ((long)seen.ExecuteScalar()! > 0) { accepted.Add(r.Id); continue; }
                string round = Round(time);
                if (TreasureReports.IsOutlier(count))
                {
                    using var flag = Command(db, tx, "INSERT OR IGNORE INTO treasure_outliers VALUES($id,$owner,$player,$name,$utc,$round,$raw,$server,$character,$instance,$reason)",
                        ("$id", r.Id.ToLowerInvariant()), ("$owner", Owner), ("$player", player.ToLowerInvariant()), ("$name", player),
                        ("$utc", time.ToString("O")), ("$round", round), ("$raw", r.Raw), ("$server", options.Server), ("$character", options.Character),
                        ("$instance", string.IsNullOrEmpty(r.InstanceId) ? sync.InstanceId : r.InstanceId), ("$reason", "single report exceeds 4 doubloons"));
                    if (flag.ExecuteNonQuery() > 0) warnings.Add(player + ": Treasure Hunter report over 4 doubloons flagged as an outlier and ignored.");
                    accepted.Add(r.Id);
                    continue;
                }
                // Reports arriving after a round has closed cannot revise a winner already selected.
                using var closed = Command(db, tx, "SELECT COUNT(*) FROM treasure_rounds WHERE owner=$owner AND round=$round", ("$owner", Owner), ("$round", round));
                if ((long)closed.ExecuteScalar()! > 0 || time < now.AddDays(-2))
                {
                    using var archive = Command(db, tx, "INSERT OR IGNORE INTO treasure_late VALUES($id,$owner,$raw,$reason)",
                        ("$id", r.Id), ("$owner", Owner), ("$raw", r.Raw), ("$reason", "round closed or report older than two days"));
                    if (archive.ExecuteNonQuery() > 0) warnings.Add("An old or already-closed-round report was archived without changing a prize winner.");
                    accepted.Add(r.Id); continue;
                }
                using var add = Command(db, tx, "INSERT OR IGNORE INTO treasure_reports VALUES($id,$owner,$player,$name,$amount,$utc,$round,$raw,$server,$character,$instance)",
                    ("$id", r.Id.ToLowerInvariant()), ("$owner", Owner), ("$player", player.ToLowerInvariant()), ("$name", player), ("$amount", count),
                    ("$utc", time.ToString("O")), ("$round", round), ("$raw", r.Raw), ("$server", options.Server), ("$character", options.Character), ("$instance", string.IsNullOrEmpty(r.InstanceId) ? sync.InstanceId : r.InstanceId));
                if (add.ExecuteNonQuery() > 0 && round == current) { changed = true; lastPlayer = player; }
                accepted.Add(r.Id);
            }
            // Close every recorded due round, even after an offline period. Never invent an empty day's winner.
            var due = new List<string>();
            using (var rounds = Command(db, tx, "SELECT DISTINCT round FROM treasure_reports WHERE owner=$owner AND round<$current ORDER BY round", ("$owner", Owner), ("$current", current)))
            using (var reader = rounds.ExecuteReader()) while (reader.Read()) due.Add(reader.GetString(0));
            foreach (string round in due)
            {
                if (sync.Backlog || now < CloseUtc(round).AddSeconds(10)) continue;
                var winner = Standings(db, round, tx).FirstOrDefault(); if (winner == null) continue;
                using var select = Command(db, tx, "INSERT OR IGNORE INTO treasure_rounds(owner,round,winner,total,state,updated) VALUES($owner,$round,$winner,$total,'pending',$now)",
                    ("$owner", Owner), ("$round", round), ("$winner", winner.Player), ("$total", winner.Total), ("$now", now.ToString("O"))); select.ExecuteNonQuery();
            }
            string notice = "";
            if (changed && !sync.Quiet)
            {
                using var last = Command(db, tx, "SELECT value FROM treasure_settings WHERE owner=$owner AND key='banter'", ("$owner", Owner));
                string value = last.ExecuteScalar() as string ?? "";
                if (!DateTime.TryParse(value, out var before) || now - before.ToUniversalTime() >= TimeSpan.FromMinutes(3))
                {
                    var lead = Standings(db, current, tx).FirstOrDefault();
                    if (lead != null)
                    {
                        var finder = Standings(db, current, tx).FirstOrDefault(s => s.Player.Equals(lastPlayer, StringComparison.OrdinalIgnoreCase));
                        notice = $"{lead.Player} leads the recorded round with {lead.Total} doubloons.";
                        if (finder != null) notice += $" Latest reporting hunter: {finder.Player}, rank {finder.Rank}, total {finder.Total}.";
                        using var save = Command(db, tx, "INSERT INTO treasure_settings VALUES($owner,'banter',$now) ON CONFLICT(owner,key) DO UPDATE SET value=$now", ("$owner", Owner), ("$now", now.ToString("O"))); save.ExecuteNonQuery();
                    }
                }
            }
            TreasurePrize? prize = null;
            using (var next = Command(db, tx, "SELECT round,winner,total,state FROM treasure_rounds WHERE owner=$owner AND state='pending' ORDER BY round LIMIT 1", ("$owner", Owner)))
            using (var reader = next.ExecuteReader()) if (reader.Read()) prize = new TreasurePrize { Id = reader.GetString(0), Round = reader.GetString(0), Winner = reader.GetString(1), Total = reader.GetInt64(2), State = reader.GetString(3) };
            var standings = Standings(db, current, tx).Take(10).ToArray();
            tx.Commit(); return new TreasureReply { Round = current, ClosesUtc = CloseUtc(current).ToString("O"), Standings = standings, AcceptedIds = accepted.ToArray(), Notice = notice, Warnings = warnings.Distinct().ToArray(), Prize = prize! };
        }
    }
}
