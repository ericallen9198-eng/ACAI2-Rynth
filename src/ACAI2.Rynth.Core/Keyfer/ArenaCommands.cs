#nullable disable
// Ported from Samm-AI ff44cc3; see origin/ACAI2-SOURCE.md.
using System;
using System.Text.RegularExpressions;

namespace ACAI2.Protocol
{
    public static class ArenaCooldown
    {
        public static string Remaining(DateTime until, DateTime now)
        {
            long seconds = (long)Math.Ceiling(Math.Max(0, (until - now).TotalSeconds));
            return seconds / 60 + "m " + seconds % 60 + "s";
        }
        public static string Notice(DateTime until, DateTime now) => "Next general match available in " + Remaining(until, now) + ".";
    }

    // Deliberate commands only: pasted speech and incidental arena mentions never start a match.
    public static class ArenaCommands
    {
        public static string Parse(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || message.Length > 500 || GameChat.Parse(message) != null) return null;
            bool addressed = Regex.IsMatch(message, @"^(?:hey\s+)?keyfer(?:\s+sutherland)?[\s,:]+", RegexOptions.IgnoreCase);
            string text = Regex.Replace(message.Trim(), @"^(?:hey\s+)?keyfer(?:\s+sutherland)?[\s,:]+", "", RegexOptions.IgnoreCase);
            var match = Regex.Match(text, @"^(?:!arena|!duel|arena)\b(?:\s+(.*))?$", RegexOptions.IgnoreCase);
            if (match.Success && (text.StartsWith("!", StringComparison.Ordinal) || addressed)) return (match.Groups[1].Value.Trim().Length == 0 ? "help" : match.Groups[1].Value.Trim());
            match = Regex.Match(text, @"^!sheet\s+(\S+)\s*$", RegexOptions.IgnoreCase);
            return match.Success ? "sheet " + match.Groups[1].Value : null;
        }
        public static bool Names(string text, out string first, out string second)
        {
            first = second = "";
            var parts = Regex.Split(text ?? "", @"\s+vs\.?\s+", RegexOptions.IgnoreCase);
            if (parts.Length != 2) return false;
            first = parts[0].Trim().Trim('"'); second = parts[1].Trim().Trim('"');
            return ValidName(first) && ValidName(second) && !first.Equals(second, StringComparison.OrdinalIgnoreCase);
        }
        public static bool ValidName(string name) => name != null && name.Length <= 50 && Regex.IsMatch(name, @"^[\p{L}\p{N}][\p{L}\p{N} .'\-]*$");
    }
}
