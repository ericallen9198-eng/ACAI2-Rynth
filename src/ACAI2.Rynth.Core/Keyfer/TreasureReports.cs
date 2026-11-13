#nullable disable
// Ported from Samm-AI ff44cc3; see origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ACAI2.Protocol
{
    public sealed class TreasureReport
    {
        public string Id { get; set; } = "";
        public string Server { get; set; } = "";
        public string Character { get; set; } = "";
        public string InstanceId { get; set; } = "";
        public string ObservedUtc { get; set; } = "";
        public string Raw { get; set; } = "";
    }
    public sealed class TreasureSync
    {
        public string InstanceId { get; set; } = "";
        public string Server { get; set; } = "";
        public string Character { get; set; } = "";
        public bool Quiet { get; set; }
        public bool Backlog { get; set; }
        public TreasureReport[] Reports { get; set; } = new TreasureReport[0];
    }
    public sealed class TreasureAction
    {
        public string InstanceId { get; set; } = "";
        public string Server { get; set; } = "";
        public string Character { get; set; } = "";
        public string Round { get; set; } = "";
        public string State { get; set; } = "";
    }
    public sealed class TreasureStanding
    {
        public string Player { get; set; } = "";
        public long Total { get; set; }
        public long PersonalBest { get; set; }
        public int Rank { get; set; }
    }
    public sealed class TreasureReply
    {
        public string[] AcceptedIds { get; set; } = new string[0];
        public string Notice { get; set; } = "";
        public string[] Warnings { get; set; } = new string[0];
        public string Round { get; set; } = "";
        public string ClosesUtc { get; set; } = "";
        public TreasureStanding[] Standings { get; set; } = new TreasureStanding[0];
        public TreasurePrize Prize { get; set; }
    }
    public sealed class TreasurePrize
    {
        public string Id { get; set; } = "";
        public string Round { get; set; } = "";
        public string Winner { get; set; } = "";
        public long Total { get; set; }
        public string State { get; set; } = "";
    }
    public static class TreasureReports
    {
        private static readonly Regex Find = new Regex(@"^Treasure Hunter: \+(?<count>[1-9]\d*) Marae Lassel Doubloons \((?<method>search|corpse)\)$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        public static bool IsOutlier(int amount) => amount > 4;
        public static bool IsReportMessage(string message) => !string.IsNullOrEmpty(message) && message.Length <= 1000 && Find.IsMatch(message);
        public static bool TryParse(string raw, out string player, out int count)
        {
            player = ""; count = 0;
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > 1000 || raw.IndexOf('\n') >= 0 || raw.IndexOf('\r') >= 0) return false;
            var chat = GameChat.Parse(raw);
            if (chat == null || chat.Channel != "Allegiance" || !ValidPlayer(chat.Speaker)) return false;
            var match = Find.Match(chat.Message);
            if (!match.Success) return false;
            // Keep oversized numeric reports recognizable so they are flagged, never scored.
            if (!int.TryParse(match.Groups["count"].Value, out count)) count = int.MaxValue;
            player = chat.Speaker; return true;
        }
        public static string PrizeCommand(string template, string winner)
        {
            // Only an owner-configured bank command with a validated recipient can execute.
            // Model output and chat text can never supply command prefixes or amounts.
            if (!ValidPlayer(winner) || string.IsNullOrWhiteSpace(template) || template != "/b t p 250000 {winner}") return "";
            return template.Replace("{winner}", winner);
        }
        public static bool ValidPlayer(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 64 &&
            !name.Equals("You", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(name, @"\A[\p{L}\p{N}][\p{L}\p{N} .'\-]*\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
}
