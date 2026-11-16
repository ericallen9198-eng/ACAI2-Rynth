using System.Text.RegularExpressions;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using ACAI2.Protocol;

namespace ACAI2.Bridge;

// Public conversation only. Silence is a successful empty answer, never a public warning.
internal sealed class PublicChatPolicy
{
    private sealed record Attempt(DateTime Utc, string Scope, string Channel, string Player, string Question);
    private readonly List<Attempt> recent = new();
    private readonly object sync = new();
    internal sealed record ModerationDecision(bool Blocked, bool Threshold, int Score, string Reason, DateTime Until = default, int Strikes = 0, bool TellNotice = false);
    private readonly string? storageRoot;
    internal PublicChatPolicy(string? storageRoot = null) { this.storageRoot = storageRoot; }
    private sealed class Meter
    {
        public Meter() { }
        public int Score;
        public int Strikes;
        public string Key = "";
        public string Name = "";
        public List<string> Summaries = new();
        public DateTime LastTellNotice;
        public DateTime LastUtc, DecayUtc, Until;
        public string Previous = "";
    }
    private readonly Dictionary<string, Meter> meters = new();
    private static string Key(KeyferAsk ask) => ask.Server.ToLowerInvariant() + "\0" + ask.Character.ToLowerInvariant() + "\0" +
        (ask.Channel == "Discord" ? "discord" : "game") + "\0" + ask.SpeakerId.ToLowerInvariant();
    private string PathFor(string key) => Path.Combine(storageRoot!, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".moderation.json");
    private Meter Get(string key, DateTime now)
    {
        if (meters.TryGetValue(key, out var value)) return value;
        foreach (var old in meters.Where(p => now > p.Value.Until && now - p.Value.LastUtc > TimeSpan.FromMinutes(10)).Select(p => p.Key).ToList()) meters.Remove(old);
        if (meters.Count >= 256) meters.Remove(meters.OrderBy(p => p.Value.LastUtc).First().Key);
        Meter? saved = storageRoot != null && File.Exists(PathFor(key)) ? JsonSerializer.Deserialize<Meter>(File.ReadAllText(PathFor(key)), new JsonSerializerOptions { IncludeFields = true }) : null;
        if (saved != null && saved.Key != key) throw new IOException("Moderation identity mismatch.");
        return meters[key] = saved ?? new Meter { Key = key, DecayUtc = now };
    }
    private void Save(Meter meter)
    {
        if (storageRoot == null) return;
        Directory.CreateDirectory(storageRoot);
        string path = PathFor(meter.Key), temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(meter, new JsonSerializerOptions { IncludeFields = true })); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    internal bool IsTimedOut(KeyferAsk ask, DateTime now)
    {
        lock (sync) return Get(Key(ask), now).Until > now;
    }
    internal static string TimeoutText(DateTime until, DateTime now) => PlayerTimeoutNotice.Format(until, now);
    internal ModerationDecision Gauge(KeyferAsk ask, DateTime now)
    {
        string key = Key(ask);
        string q = Regex.Replace(ask.Question.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
        q = Regex.Replace(q, @"^keyfer\b\s*", "");
        lock (sync)
        {
            var meter = Get(key, now);
            if (now < meter.Until)
            {
                bool tell = ask.Channel == "Tell" && now - meter.LastTellNotice >= TimeSpan.FromMinutes(1);
                if (tell) meter.LastTellNotice = now;
                return new(true, false, meter.Score, "cooldown", meter.Until, meter.Strikes, tell);
            }
            if (meter.Until != default) { meter.Score = 0; meter.Until = default; meter.DecayUtc = now; }
            int decay = (int)Math.Max(0, (now - meter.DecayUtc).TotalSeconds / 90);
            if (decay > 0) { meter.Score = Math.Max(0, meter.Score - decay); meter.DecayUtc = meter.DecayUtc.AddSeconds(decay * 90); }
            int points = 0; string reason = "";
            if (IsRoleplayRequest(ask.Question)) { points = 3; reason = "roleplay bait"; }
            else if (Regex.IsMatch(q, @"\b(?:override|ignore|forget)\b.{0,50}\b(?:instructions|rules|settings)\b|\b(?:from now on|every response|respond to everyone|scream everything)\b|\b(?:play|roleplay|rehearsal)\b.{0,100}\b(?:top|leaderboard|insult|everyone|scream|instructions)\b"))
            { points = 3; reason = "persona or result manipulation"; }
            else if (Regex.IsMatch(q, @"\b(?:you|ur|you re|you are)\s+(?:so\s+)?(?:gay|stupid|idiot|useless|trash|a bitch|a dick|an idiot|a moron)\b|\b(?:shut (?:the fuck )?up|fuck you|you suck|big dick|your dick|your cock)\b"))
            { points = 4; reason = "direct insult or abuse"; }
            else if (q.Length == 0 || Regex.IsMatch(q, @"^(?:the thing|your thing)$"))
            { points = 1; reason = "repeated low-content pings"; }
            meter.Previous = q; meter.LastUtc = now;
            meter.Score = Math.Min(4, meter.Score + points);
            if (meter.Score < 4) return new(false, false, meter.Score, reason);
            meter.Strikes = Math.Min(1000000, meter.Strikes + 1);
            meter.Until = now.AddMinutes(meter.Strikes == 1 ? 5 : meter.Strikes == 2 ? 30 : 1440);
            if (ask.Channel == "Tell") meter.LastTellNotice = now;
            meter.Name = ask.Speaker;
            meter.Summaries.Add(now.ToString("O") + " | " + reason + " | " + Regex.Replace(ask.Question, @"[\p{Cc}\p{Cf}]+", " ").Substring(0, Math.Min(240, Regex.Replace(ask.Question, @"[\p{Cc}\p{Cf}]+", " ").Length)));
            if (meter.Summaries.Count > 20) meter.Summaries.RemoveRange(0, meter.Summaries.Count - 20);
            Save(meter);
            return new(true, true, meter.Score, reason, meter.Until, meter.Strikes, ask.Channel == "Tell");
        }
    }
    internal static bool IsRoleplayRequest(string text) => Regex.IsMatch(text, @"\b(?:roleplay|role play|in a play|act as|play the part|pretend (?:you|to be))\b", RegexOptions.IgnoreCase);
    internal static bool IsPublic(string channel) => channel is "General" or "Allegiance";
    internal static bool IsContinuation(string text) => Regex.IsMatch(text ?? "", @"^\s*(?:(?:(?:hey|yo|hi|hello)\s+)?keyfer(?:\s+sutherland)?[\s,.!?:]+)?(?:please\s+)?(?:continue|go on|tell me more|more details)(?:\s+please)?[.!?]*\s*$", RegexOptions.IgnoreCase);
    private readonly Dictionary<string, DateTime> tellUntil = new(), handoffNoticeUntil = new();
    internal (bool Tell, bool Announce) Handoff(KeyferAsk ask, DateTime now)
    {
        if(ConversationSafety.Familiar(ask.Speaker))return(false,false);
        bool continuation = IsContinuation(ask.Question);
        if (!IsPublic(ask.Channel) || (!continuation && ask.Channel != "General") || (!continuation && !Regex.IsMatch(ask.Question, @"\?|\b(?:what|where|when|who|why|how|can|could|please|tell me|look up|check)\b", RegexOptions.IgnoreCase))) return (false, false);
        string key = Key(ask), scope = ask.Server.ToLowerInvariant() + "\0" + ask.Character.ToLowerInvariant();
        lock (sync)
        {
            foreach (var old in tellUntil.Where(p => p.Value < now).Select(p => p.Key).ToList()) tellUntil.Remove(old);
            foreach (var old in handoffNoticeUntil.Where(p => p.Value < now).Select(p => p.Key).ToList()) handoffNoticeUntil.Remove(old);
            if (!continuation && !tellUntil.ContainsKey(key) && !recent.Any(a => a.Scope == scope && a.Player == ask.SpeakerId.ToLowerInvariant() && now - a.Utc < TimeSpan.FromMinutes(2))) return (false, false);
            tellUntil[key] = now.AddMinutes(5);
            bool announce = !handoffNoticeUntil.ContainsKey(key);
            handoffNoticeUntil[key] = now.AddMinutes(10);
            return (true, announce);
        }
    }
    internal bool StartHandoff(KeyferAsk ask, DateTime now)
    {
        lock (sync)
        {
            string key = Key(ask);
            tellUntil[key] = now.AddMinutes(5);
            bool announce = !handoffNoticeUntil.TryGetValue(key, out var until) || until <= now;
            if (announce) handoffNoticeUntil[key] = now.AddMinutes(10);
            return announce;
        }
    }
    internal bool TryReserve(KeyferAsk ask, DateTime now)
    {
        if (!IsPublic(ask.Channel)) return true;
        string scope = ask.Server.ToLowerInvariant() + "\0" + ask.Character.ToLowerInvariant();
        string player = ask.SpeakerId.ToLowerInvariant();
        string question = Regex.Replace(ask.Question.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
        lock (sync)
        {
            recent.RemoveAll(a => now - a.Utc >= TimeSpan.FromMinutes(2));
            var own = recent.Where(a => a.Scope == scope && a.Player == player).ToList();
            var channel = recent.Where(a => a.Scope == scope && a.Channel == ask.Channel && now - a.Utc < TimeSpan.FromMinutes(1)).ToList();
            if (own.Any(a => a.Question == question || now - a.Utc < TimeSpan.FromSeconds(20)) || own.Count >= 3 ||
                channel.Count >= 6 || channel.Any(a => now - a.Utc < TimeSpan.FromSeconds(6))) return false;
            recent.Add(new Attempt(now, scope, ask.Channel, player, question));
            return true;
        }
    }
    internal static string Limit(string text)
    {
        text = Regex.Replace(text, @"[\p{Cc}\p{Cf}]+", " ").Trim();
        if (text.Length <= 200) return text;
        // Keep complete sentences only. Full long answers use the typed Tell route instead.
        var ends = Regex.Matches(text, @"[.!?](?=\s|$)").Cast<Match>().Where(m => m.Index < 200).ToArray();
        return ends.Length > 0 ? text.Substring(0, ends.Last().Index + 1).Trim() : "Let's take this to tells for the full answer.";
    }
}
