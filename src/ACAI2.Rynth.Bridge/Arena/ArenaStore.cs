using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ACAI2.Bridge;

internal sealed class ArenaStore
{
    private readonly string path;
    internal ArenaStore(string path) { this.path = path; }
    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 2 }.ToString()); db.Open();
        using var cmd = db.CreateCommand(); cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS profiles(name TEXT PRIMARY KEY COLLATE NOCASE, json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS matches(id TEXT PRIMARY KEY, utc TEXT NOT NULL, first TEXT NOT NULL COLLATE NOCASE,
            second TEXT NOT NULL COLLATE NOCASE, winner TEXT NOT NULL COLLATE NOCASE, public INTEGER NOT NULL, json TEXT NOT NULL);
            """; cmd.ExecuteNonQuery(); return db;
    }
    internal ArenaProfile? Profile(string name)
    {
        using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT json FROM profiles WHERE name=$n"; cmd.Parameters.AddWithValue("$n", name);
        return cmd.ExecuteScalar() is string json ? JsonSerializer.Deserialize<ArenaProfile>(json) : null;
    }
    internal void SaveProfile(ArenaProfile p)
    {
        using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "INSERT INTO profiles VALUES($n,$j) ON CONFLICT(name) DO UPDATE SET json=excluded.json";
        cmd.Parameters.AddWithValue("$n", p.Name); cmd.Parameters.AddWithValue("$j", JsonSerializer.Serialize(p)); cmd.ExecuteNonQuery();
    }
    internal void SaveMatch(ArenaMatch match, bool isPublic)
    {
        using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "INSERT INTO matches VALUES($id,$utc,$a,$b,$w,$p,$j)";
        cmd.Parameters.AddWithValue("$id", match.Id); cmd.Parameters.AddWithValue("$utc", match.Utc.ToString("O")); cmd.Parameters.AddWithValue("$a", match.First.Name);
        cmd.Parameters.AddWithValue("$b", match.Second.Name); cmd.Parameters.AddWithValue("$w", match.Winner); cmd.Parameters.AddWithValue("$p", isPublic ? 1 : 0);
        cmd.Parameters.AddWithValue("$j", JsonSerializer.Serialize(match)); cmd.ExecuteNonQuery();
    }
    internal DateTime LastPublicUtc()
    {
        using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT MAX(utc) FROM matches WHERE public=1";
        return DateTime.TryParse(cmd.ExecuteScalar() as string, null, System.Globalization.DateTimeStyles.RoundtripKind, out var value) ? value : DateTime.MinValue;
    }
    internal DateTime LastGeneralUtc()
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        // Older rows did not record a channel; conservatively preserve their existing cooldown.
        cmd.CommandText = "SELECT MAX(utc) FROM matches WHERE public=1 AND (json_extract(json,'$.Channel')='General' OR json_extract(json,'$.Channel') IS NULL)";
        return DateTime.TryParse(cmd.ExecuteScalar() as string, null, System.Globalization.DateTimeStyles.RoundtripKind, out var value) ? value : DateTime.MinValue;
    }
    internal string Stats(string name)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*),COALESCE(SUM(CASE WHEN winner=$n THEN 1 ELSE 0 END),0),COALESCE(SUM(CASE WHEN winner='' THEN 1 ELSE 0 END),0) FROM matches WHERE public=1 AND (first=$n OR second=$n)";
        cmd.Parameters.AddWithValue("$n", name); using var r = cmd.ExecuteReader(); r.Read();
        long count = r.GetInt64(0), wins = r.GetInt64(1), draws = r.GetInt64(2);
        return $"{name}: {wins} exhibition wins, {count - wins - draws} losses, {draws} draws ({count} matches). Private previews excluded.";
    }
}
