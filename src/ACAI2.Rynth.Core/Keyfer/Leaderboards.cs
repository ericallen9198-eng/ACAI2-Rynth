#nullable disable
// Ported from Samm-AI ff44cc3; see origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ACAI2.Protocol
{
    public static class DreamWeaveLeaderboards
    {
        public static readonly string[] Ids = { "dps", "defense", "wave", "speed", "speed winners", "level", "bank", "cap", "stamp", "thread", "knot", "deaths", "title", "1v1", "2v2", "ffa", "tugak", "bg" };
        public static string Command(string id) => Ids.Contains(id) ? "/top " + id : null;
        public static string Description(string id)
        {
            switch (id)
            {
                case "dps": return "Attack arena: most damage in the 60-second trial";
                case "defense": return "Defense arena: longest survival";
                case "wave": return "Wave gauntlet: highest wave, ties by fastest full clear; lines include clear time";
                case "speed": return "Speed trial: fastest current-season dungeon run";
                case "speed winners": return "Winners of recent speed-trial seasons";
                case "level": return "Highest character level";
                case "bank": return "Most banked pyreals; one account entry under its highest-level character, pooled account balance";
                case "cap": return "Lifetime Class Ability Points earned; spending does not reduce rank";
                case "stamp": return "Most quest stamps earned by one character";
                case "thread": return "Thread-Guide: highest guide rung, ties by lifetime Thread clears; no-rung clears rank below rung holders";
                case "knot": return "Best Knot: highest level, ties by fastest fight-start-to-kill time";
                case "deaths": return "Most deaths, excluding characters who hide death counts";
                case "title": return "Most character titles earned";
                case "ffa": return "Free-for-all PvP arena ladder, ranked by rating";
                case "1v1": case "2v2": case "tugak": case "bg": return "PvP arena rating ladder";
                default: return "";
            }
        }
        public static string Knowledge => string.Join("\n", Ids.Select(id => Command(id) + ": " + Description(id)));
        public static string[] Requested(string question)
        {
            string q = question.ToLowerInvariant();
            var exact = Regex.Match(q, @"/top\s+(speed\s+winners|dps|defense|wave|speed|level|bank|cap|stamp|thread|knot|deaths|title|1v1|2v2|ffa|tugak|bg)\b");
            if (exact.Success) return new[] { Regex.Replace(exact.Groups[1].Value, @"\s+", " ") };
            if (!Regex.IsMatch(q, @"\b(?:top|leaderboards?|rankings?|who.?s (?:first|leading|best)|who is (?:first|leading|best))\b")) return new string[0];
            var found = Ids.Where(id => Regex.IsMatch(q, @"\b" + Regex.Escape(id) + @"\b")).ToList();
            if (q.Contains("class ability") || q.Contains("class points")) found.Add("cap");
            if (q.Contains("survival")) found.Add("defense");
            if (Regex.IsMatch(q, @"\bfree[\s-]*for[\s-]*all\b")) found.Add("ffa");
            if (found.Contains("speed winners")) found.Remove("speed");
            return found.Count > 0 ? found.Distinct().ToArray() : Ids.ToArray();
        }
        public static bool HeaderMatches(string id, string line)
        {
            if (Command(id) == null || !line.StartsWith("===") || !Regex.IsMatch(line, @"leaderboard|ladder|season.*winners|speed.*winners", RegexOptions.IgnoreCase)) return false;
            string pattern = id == "cap" ? @"class ability points|class points" : id == "stamp" ? @"quest stamps?|stamps?" :
                id == "ffa" ? @"\bffa\b|free[\s-]*for[\s-]*all" : id == "level" ? @"levels?" : id == "deaths" ? @"deaths?" : id == "title" ? @"titles?" : id == "thread" ? @"thread" : id == "dps" ? @"dps|attack arena" : id == "bank" ? @"bank|pyreals" :
                id == "defense" ? @"defen[cs]e|survival" : id == "speed winners" ? @"speed.*winners|season.*winners" : @"\b" + Regex.Escape(id) + @"\b";
            if (id == "speed" && Regex.IsMatch(line, "winners", RegexOptions.IgnoreCase)) return false;
            return Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase);
        }
    }
    public sealed class LeaderboardRequest
    {
        public string Server { get; set; }
        public string Character { get; set; }
        public string InstanceId { get; set; }
    }
    public sealed class LeaderboardJob
    {
        public string Id { get; set; }
        public string Board { get; set; }
        public string ExpiresUtc { get; set; }
    }
    public sealed class LeaderboardResult
    {
        public string Server { get; set; }
        public string Character { get; set; }
        public string InstanceId { get; set; }
        public string JobId { get; set; }
        public string Board { get; set; }
        public string ObservedUtc { get; set; }
        public string[] Lines { get; set; }
        public string Error { get; set; }
    }
    public sealed class LeaderboardCapture
    {
        private readonly string board;
        private readonly DateTime started;
        private DateTime last;
        private readonly List<string> lines = new List<string>();
        public LeaderboardCapture(string board, DateTime started) { this.board = board; this.started = started; }
        public string[] Lines => lines.ToArray();
        public bool Complete(DateTime now) => now - started >= TimeSpan.FromSeconds(12) || lines.Count >= 21 ||
            (lines.Count >= 2 && now - last >= TimeSpan.FromSeconds(2));
        public void Observe(string raw, DateTime now)
        {
            if (raw == null || raw.Length > 16000 || GameChat.Parse(raw) != null ||
                Regex.IsMatch(raw, "(?:says|say|tells you),\\s*\"", RegexOptions.IgnoreCase)) return;
            foreach (string part in raw.Replace("\r", "").Split('\n'))
            {
                string text = Regex.Replace(part.Trim(), @"^\d{2}:\d{2}:\d{2}\s+", "");
                if (text.Length > 600 || lines.Count >= 21) continue;
                if (lines.Count == 0) { if (DreamWeaveLeaderboards.HeaderMatches(board, text)) { lines.Add(text); last = now; } continue; }
                var rank = Regex.Match(text, @"^(?<rank>\d{1,2})\.\s+.+$");
                if (rank.Success && int.TryParse(rank.Groups["rank"].Value, out int n) && n == lines.Count)
                { lines.Add(text); last = now; }
            }
        }
    }
}
