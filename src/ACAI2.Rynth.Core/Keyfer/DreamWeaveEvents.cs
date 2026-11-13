#nullable disable
// Ported from Samm-AI ff44cc3; see origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ACAI2.Protocol
{
    public sealed class DreamWeaveEvent
    {
        public string Server { get; set; } = "DreamWeave";
        public string Character { get; set; } = "";
        public string InstanceId { get; set; } = "";
        public string EventId { get; set; } = "";
        public DateTime ObservedUtc { get; set; }
        public string Phase { get; set; } = "";
        public string Place { get; set; } = "";
        public string Coordinates { get; set; } = "";
        public string Objective { get; set; } = "";
        public string Factions { get; set; } = "";
        public string Champion { get; set; } = "";
        public string FinalBlow { get; set; } = "";
        public string TopDamage { get; set; } = "";
        public string LastPillarBy { get; set; } = "";
        public string MostKills { get; set; } = "";
        public int MostKillCount { get; set; }
        public int Defenders { get; set; }
        public string Countdown { get; set; } = "";
        public int Wave { get; set; }
        public string Remaining { get; set; } = "";
        public string Raw { get; set; } = "";
    }

    public static class DreamWeaveEventParser
    {
        private const string Site = @"(?<place>.+?)\s+\((?<coords>\d+(?:\.\d+)?[NS],\s*\d+(?:\.\d+)?[EW])\)";
        private static Regex Rx(string pattern) => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        private static readonly Regex Warning = Rx(@"^(?:A low rumble can be felt coming from |Something is stirring at |Thunder rolls from the direction of |Birds have scattered from |Word is spreading of strange lights over |The stones underfoot are humming near |The ground will not settle near )" + Site + @".*?Roughly (?<eta>a minute|\d+ minutes?) out\.$");
        private static readonly Regex Started = Rx(@"^(?<objective>Slay the Champion|Defend Against the Waves|Shatter the Pillars) at " + Site + @"\s*-\s*(?<factions>.+?) pour in!$");
        private static readonly Regex Progress = Rx(@"^(?<objective>Slay the Champion) at " + Site + @": (?<champion>.+?) has not yet appeared Wave (?<wave>[1-9]\d*). (?<remaining>\d+m[0-5]\ds) remain\.$");
        private static readonly Regex ChampionWon = Rx(@"^(?<champion>.+?) has been slain at " + Site + @"!");
        private static readonly Regex WavesWon = Rx(@"^The waves at " + Site + @" have been repelled!");
        private static readonly Regex PillarsWon = Rx(@"^The pillars at " + Site + @" have been shattered!");
        private static readonly Regex ChampionFailed = Rx(@"^(?<champion>.+?) still stands at " + Site + @"\s*-\s*time ran out\.");
        private static readonly Regex DefenceFailed = Rx(@"^The defen[cs]e of " + Site + @" has failed\s*-\s*time ran out\.");
        private static readonly Regex Final = Rx(@"!\s*(?<player>.+?) struck the (?:final|killing) blow(?:,|\.)");
        private static readonly Regex Damage = Rx(@"top damage:\s*(?<player>.+?)\.\s*(?:Most kills:|\d+ defenders|$)");
        private static readonly Regex Kills = Rx(@"Most kills:\s*(?<player>.+?)\s*\((?<count>\d+)\)\.");
        private static readonly Regex Defenders = Rx(@"(?<count>\d+) defenders took part\.");
        private static readonly Regex LastPillar = Rx(@"!\s*(?<player>.+?) destroyed the last pillar\.");

        public static IEnumerable<string> NativeLines(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > 12000 ||
                !raw.TrimStart().StartsWith("[World Event] ", StringComparison.OrdinalIgnoreCase)) return Enumerable.Empty<string>();
            return raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static DreamWeaveEvent Parse(string raw, DateTime now)
        {
            string line = (raw ?? "").Trim();
            // A quoted player message or a Discord wrapper is not a native world announcement.
            const string prefix = "[World Event] ";
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || line.Length > 3000) return null;
            string text = line.Substring(prefix.Length);
            var result = new DreamWeaveEvent { Raw = line, ObservedUtc = now };
            Match match = Warning.Match(text);
            if (match.Success) { result.Phase = "Warning"; result.Countdown = match.Groups["eta"].Value; }
            else if ((match = Started.Match(text)).Success)
            { result.Phase = "Started"; result.Objective = match.Groups["objective"].Value; result.Factions = match.Groups["factions"].Value; }
            else if ((match = Progress.Match(text)).Success)
            {
                result.Phase = "Progress"; result.Objective = match.Groups["objective"].Value;
                int wave;
                if (!int.TryParse(match.Groups["wave"].Value, out wave)) return null;
                result.Wave = wave; result.Remaining = match.Groups["remaining"].Value;
            }
            else if ((match = ChampionWon.Match(text)).Success) { result.Phase = "Completed"; result.Objective = "Slay the Champion"; }
            else if ((match = WavesWon.Match(text)).Success) { result.Phase = "Completed"; result.Objective = "Defend Against the Waves"; }
            else if ((match = PillarsWon.Match(text)).Success) { result.Phase = "Completed"; result.Objective = "Shatter the Pillars"; }
            else if ((match = ChampionFailed.Match(text)).Success) { result.Phase = "Failed"; result.Objective = "Slay the Champion"; }
            else if ((match = DefenceFailed.Match(text)).Success) result.Phase = "Failed";
            else return null;
            result.Place = match.Groups["place"].Value;
            result.Coordinates = match.Groups["coords"].Value;
            result.Champion = match.Groups["champion"].Value;
            result.FinalBlow = Final.Match(text).Groups["player"].Value;
            result.TopDamage = Damage.Match(text).Groups["player"].Value;
            result.LastPillarBy = LastPillar.Match(text).Groups["player"].Value;
            var kills = Kills.Match(text);
            result.MostKills = kills.Groups["player"].Value;
            int count;
            if (int.TryParse(kills.Groups["count"].Value, out count)) result.MostKillCount = count;
            if (int.TryParse(Defenders.Match(text).Groups["count"].Value, out count)) result.Defenders = count;
            return result;
        }
    }

    public sealed class DreamWeaveEventTracker
    {
        private readonly Dictionary<string, DreamWeaveEvent> latest = new Dictionary<string, DreamWeaveEvent>(StringComparer.OrdinalIgnoreCase);
        public IEnumerable<DreamWeaveEvent> Snapshot() => latest.Values.ToArray();
        public void Restore(IEnumerable<DreamWeaveEvent> records, DateTime now)
        {
            foreach (var r in (records ?? Enumerable.Empty<DreamWeaveEvent>()).Take(64))
                if (r != null && r.Server == "DreamWeave" && !string.IsNullOrWhiteSpace(r.Place) &&
                    Guid.TryParse(r.EventId, out _) &&
                    new[] { "Warning", "Started", "Progress", "Completed", "Failed" }.Contains(r.Phase) &&
                    now >= r.ObservedUtc.ToUniversalTime() && now - r.ObservedUtc.ToUniversalTime() < TimeSpan.FromMinutes(30))
                { r.ObservedUtc = r.ObservedUtc.ToUniversalTime(); latest[r.Place] = r; }
        }
        public DreamWeaveEvent Observe(string server, string raw, DateTime now)
        {
            if (!string.Equals(server, "DreamWeave", StringComparison.OrdinalIgnoreCase)) return null;
            var next = DreamWeaveEventParser.Parse(raw, now);
            if (next == null) return null;
            DreamWeaveEvent previous;
            if (latest.TryGetValue(next.Place, out previous) && now >= previous.ObservedUtc && now - previous.ObservedUtc < TimeSpan.FromMinutes(30))
            {
                bool ended = previous.Phase == "Completed" || previous.Phase == "Failed";
                if (!(ended && next.Phase == "Warning"))
                {
                    if (ended || previous.Phase == next.Phase || ((previous.Phase == "Started" || previous.Phase == "Progress") && next.Phase == "Warning") ||
                        (previous.Phase == "Progress" && next.Phase == "Started")) return null;
                    next.EventId = previous.EventId;
                    if (next.Objective.Length == 0) next.Objective = previous.Objective;
                    if (next.Factions.Length == 0) next.Factions = previous.Factions;
                }
            }
            if (next.EventId.Length == 0) next.EventId = Guid.NewGuid().ToString("N");
            latest[next.Place] = next;
            if (latest.Count > 64) latest.Remove(latest.OrderBy(x => x.Value.ObservedUtc).First().Key);
            return next;
        }
        public static string Announcement(DreamWeaveEvent e)
        {
            string site = e.Place + " (" + e.Coordinates + ")";
            if (e.Phase == "Warning") return "[DW Event] " + site + ": starts in roughly " + e.Countdown + ".";
            if (e.Phase == "Started") return "[DW Event] ACTIVE: " + e.Objective + " at " + site + ". " + e.Factions + " pour in!";
            if (e.Phase == "Progress") return "[DW Event] ACTIVE: " + e.Objective + " at " + site +
                ". Wave " + e.Wave + "; " + e.Champion + " has not yet appeared. " + e.Remaining + " remain.";
            string text = "[DW Event] " + (e.Phase == "Failed" ? "FAILED" : "COMPLETE") + ": " +
                (e.Objective.Length > 0 ? e.Objective + " at " : "") + site + ".";
            if (e.Champion.Length > 0) text += " " + e.Champion + (e.Phase == "Failed" ? " still stands." : " slain.");
            if (e.Phase == "Failed") text += " Time ran out; a consolation cache remains briefly.";
            if (e.FinalBlow.Length > 0) text += " Final blow: " + e.FinalBlow + ".";
            if (e.TopDamage.Length > 0) text += " Top damage: " + e.TopDamage + ".";
            if (e.LastPillarBy.Length > 0) text += " Last pillar: " + e.LastPillarBy + ".";
            if (e.MostKills.Length > 0) text += " Most kills: " + e.MostKills + " (" + e.MostKillCount + ").";
            if (e.Defenders > 0) text += " " + e.Defenders + " defenders.";
            return text;
        }
    }
}
