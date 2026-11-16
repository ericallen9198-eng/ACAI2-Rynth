using System.Security.Cryptography;
using ACAI2.Protocol;

namespace ACAI2.Bridge;

internal sealed class ArenaService : IDisposable
{
    private readonly ArenaStore store;
    private readonly IArenaSheets sheets;
    private readonly IArenaCommentary? commentary;
    private readonly bool generalEnabled, allegianceEnabled;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, DateTime> requests = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> commands = new(StringComparer.OrdinalIgnoreCase);
    private string activeId = "", activeInstance = "", activeChannel = "", activeRequester = "";
    private DateTime activeUntil;
    private string lastCommentary = "no match yet";
    internal ArenaService(ArenaStore store, IArenaSheets? sheets = null, bool? generalEnabled = null, bool? allegianceEnabled = null, IArenaCommentary? commentary = null)
    {
        this.commentary = commentary;
        this.store = store; this.sheets = sheets ?? new ArenaSheets(store);
        this.generalEnabled = generalEnabled ?? string.Equals(Environment.GetEnvironmentVariable("ACAI2_ARENA_GENERAL_ENABLED"), "true", StringComparison.OrdinalIgnoreCase);
        this.allegianceEnabled = allegianceEnabled ?? !string.Equals(Environment.GetEnvironmentVariable("ACAI2_ARENA_ALLEGIANCE_ENABLED"), "false", StringComparison.OrdinalIgnoreCase);
    }
    internal async Task<KeyferAnswer?> HandleAsync(KeyferAsk ask, DateTime now)
    {
        string? command = ArenaCommands.Parse(ask.Question);
        if (command == null) return null;
        KeyferAnswer Tell(string text) => new() { Ok = true, ReplyChannel = "Tell", Answer = text };
        if (ask.Channel == "Discord") return new KeyferAnswer { Ok = true, Answer = "Arena commands currently use the registered game client or the Bridge's local preview command." };
        if (!await gate.WaitAsync(0)) return Tell("");
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            foreach (string old in commands.Where(x => x.Value <= now).Select(x => x.Key).ToArray()) commands.Remove(old);
            if (commands.Count >= 256 || commands.TryGetValue(ask.SpeakerId, out var until) && now < until) return Tell("");
            commands[ask.SpeakerId] = now.AddSeconds(3);
            if (command.Equals("help", StringComparison.OrdinalIgnoreCase))
                return Tell("Use !arena First Name vs Second Name for a sheet-based simulated exhibition. Tells are private previews. !sheet <url> registers your own sheet. !arena build <name> lists loaded abilities; !arena refresh <name> reloads them. !arena status or !arena stats [name]. No prizes or real combat.");
            if (command.Equals("status", StringComparison.OrdinalIgnoreCase))
                return Tell($"Arena Allegiance {(allegianceEnabled ? "ON" : "OFF")}; General {(generalEnabled ? "ON" : "OFF for testing")}. " + (now < activeUntil ? "One public exhibition is active." : "No active exhibition.") + " Last narration: " + lastCommentary + ".");
            if (command.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                if (!ask.Speaker.Equals("Samm", StringComparison.OrdinalIgnoreCase) && !ask.Speaker.Equals("Toe", StringComparison.OrdinalIgnoreCase)) return Tell("Only Samm or Toe can cancel the public exhibition.");
                string cancelled = activeId; activeId = ""; activeUntil = default;
                return new KeyferAnswer { Ok = true, ReplyChannel = "Tell", Answer = "Pending arena commentary cancelled. The game cooldown remains.", ArenaCancelId = cancelled };
            }
            if (command.Equals("stats", StringComparison.OrdinalIgnoreCase) || command.StartsWith("stats ", StringComparison.OrdinalIgnoreCase))
            {
                string name = command.Length > 6 ? command.Substring(6).Trim() : ask.Speaker;
                return ArenaCommands.ValidName(name) ? Tell(store.Stats(name)) : Tell("Use !arena stats <character name>.");
            }
            string key = ask.SpeakerId;
            if (requests.TryGetValue(key, out var next) && now < next)
            {
                var global = ask.Channel == "General" ? store.LastGeneralUtc().AddMinutes(10).AddSeconds(40) : DateTime.MinValue;
                return Tell(ask.Channel == "General" ? ArenaCooldown.Notice(global > next ? global : next, now) : "Arena request cooldown: retry in " + Remaining(next, now) + ".");
            }
            foreach (string old in requests.Where(x => x.Value < now).Select(x => x.Key).ToArray()) requests.Remove(old);
            if (requests.Count >= 256) return Tell("Arena request capacity reached. Try again later.");
            requests[key] = now.AddSeconds(15);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            if (command.StartsWith("sheet ", StringComparison.OrdinalIgnoreCase))
            {
                var p = await sheets.RegisterAsync(ask.Speaker, command.Substring(6).Trim(), timeout.Token);
                return Tell($"Sheet saved for {p.Name}, level {p.Level}. Name matching is not proof of account ownership. Purchased abilities get fictional arena adaptations, not server formulas.");
            }
            bool refreshBuild = command.StartsWith("refresh ", StringComparison.OrdinalIgnoreCase);
            if (refreshBuild || command.StartsWith("build ", StringComparison.OrdinalIgnoreCase))
            {
                string name = command.Substring(refreshBuild ? 8 : 6).Trim();
                if (!ArenaCommands.ValidName(name)) return Tell("Use !arena build <name> or !arena refresh <name>.");
                var profile = await sheets.FindAsync(name, timeout.Token, refreshBuild);
                string abilities = profile.Abilities.Count == 0 ? "No purchased abilities read from this sheet." :
                    string.Join("; ", profile.Abilities.Where(x => x.Rank > 0).GroupBy(x => x.Class).Select(g => g.Key + ": " + string.Join(", ", g.Select(x => x.Name + " " + x.Rank))));
                return Tell($"{profile.Name}, level {profile.Level}: {profile.Abilities.Count(x => x.Rank > 0)} purchased abilities. " + abilities);
            }
            bool preview = command.StartsWith("preview ", StringComparison.OrdinalIgnoreCase) || ask.Channel == "Tell";
            if (command.StartsWith("preview ", StringComparison.OrdinalIgnoreCase)) command = command.Substring(8).Trim();
            if (!ArenaCommands.Names(command, out string first, out string second)) return Tell("Use !arena First Name vs Second Name. Multiword names are supported. Add preview for private results.");
            if (!preview)
            {
                if (ask.Channel == "General" && !generalEnabled) return Tell("General arena hosting is disabled during testing. Request the match in Allegiance chat with /a !arena First Name vs Second Name.");
                if (ask.Channel == "Allegiance" && !allegianceEnabled) return Tell("Allegiance arena hosting is disabled. Use !arena preview First Name vs Second Name for private results.");
                if (now < activeUntil) return Tell(ask.Channel == "General" ? ArenaCooldown.Notice(store.LastGeneralUtc().AddMinutes(10).AddSeconds(40), now) :
                    "One public exhibition is already active. Wait for its conclusion (up to " + Remaining(activeUntil, now) + ").");
                DateTime generalNext = store.LastGeneralUtc().AddMinutes(10).AddSeconds(40);
                if (ask.Channel == "General" && now < generalNext) return Tell(ArenaCooldown.Notice(generalNext, now));
            }
            // Fetch sequentially to avoid a burst at the public website. Never use stale data after a failed refresh.
            var a = await sheets.FindAsync(first, timeout.Token);
            var b = await sheets.FindAsync(second, timeout.Token);
            DateTime completed = now.Add(System.Diagnostics.Stopwatch.GetElapsedTime(started));
            var match = ArenaEngine.Simulate(a, b, RandomNumberGenerator.GetInt32(int.MaxValue), completed);
            if (commentary != null) match.Updates = await commentary.NarrateAsync(match, timeout.Token);
            completed = now.Add(System.Diagnostics.Stopwatch.GetElapsedTime(started)); match.Utc = completed;
            lastCommentary = match.CommentaryMode == "ai" ? "AI" : "descriptive fallback";
            match.Channel = preview ? "Tell" : ask.Channel;
            if (!preview && ask.Channel == "General")
                match.Updates[match.Updates.Length - 1] = "SIM RESULT: " + (match.Winner.Length == 0 ? "Draw" : match.Winner + " wins") + $"! Remaining HP {match.FirstHealth:0}/{match.SecondHealth:0}.";
            store.SaveMatch(match, !preview);
            if (preview)
                return Tell("PRIVATE SIM PREVIEW (not ranked):\n" + string.Join("\n", match.Updates) +
                    $"\nSeed {match.Seed}; {match.Model}. Unmodeled abilities: {match.Warnings.Length}. Ability effects are fictional arena adaptations for entertainment.");
            requests[key] = ask.Channel == "General" ? completed.AddMinutes(5) : completed.AddSeconds(3);
            activeId = match.Id; activeUntil = completed.AddSeconds(40);
            activeInstance = ask.InstanceId; activeChannel = ask.Channel; activeRequester = key;
            return new KeyferAnswer { Ok = true, ArenaMatchId = match.Id, ArenaUpdates = match.Updates, ArenaChannel = ask.Channel,
                ArenaNextGeneralUtc = ask.Channel == "General" ? completed.AddMinutes(10).AddSeconds(40).ToString("O") : null };
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or ArgumentException or OperationCanceledException or System.Text.Json.JsonException or System.Text.RegularExpressions.RegexMatchTimeoutException or IOException or Microsoft.Data.Sqlite.SqliteException or UnauthorizedAccessException)
        {
            string reason = ex is OperationCanceledException ? "website request timed out." :
                ex is Microsoft.Data.Sqlite.SqliteException or UnauthorizedAccessException || ex is IOException && ex is not InvalidDataException ? "local arena storage is unavailable; no public commentary was queued." : ex.Message;
            return Tell("Arena could not load a verified sheet or finish the request: " + reason);
        }
        finally { gate.Release(); }
    }
    internal static string Remaining(DateTime until, DateTime now) => ArenaCooldown.Remaining(until, now);
    internal async Task<bool> CompleteAsync(string instance, string id, DateTime now)
    {
        await gate.WaitAsync();
        try
        {
            if (string.IsNullOrEmpty(id) || id != activeId || instance != activeInstance || activeChannel != "Allegiance") return false;
            activeUntil = now; activeId = ""; requests.Remove(activeRequester); return true;
        }
        finally { gate.Release(); }
    }
    public void Dispose() { (sheets as IDisposable)?.Dispose(); gate.Dispose(); }
}
