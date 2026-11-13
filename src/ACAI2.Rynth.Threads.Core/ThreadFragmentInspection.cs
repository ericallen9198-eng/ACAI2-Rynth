#nullable disable
// Adapted from the original ACAI2 Thread journal; see origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ACAI2.Rynth.Threads.Core
{
    public sealed class ThreadFragmentLevel
    {
        public string Type { get; set; } = "";
        public int Level { get; set; }
        public string Evidence { get; set; } = "";
    }
    public static class ThreadFragmentInspection
    {
        private static readonly Regex Name = new Regex(@"^(?<type>Raw Fragment|Guide Fragment)(?:\s*\(Level\s*:?[ ]*(?<level>\d+)\))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Description = new Regex(@"^(?:(?:Thread(?: Gem)?|Dungeon|Fragment|Starting)\s+)?Level\s*[:=]?\s*(?<level>\d+)\b|\b(?:Raw Fragment|Guide Fragment)\s*\(?Level\s*:?\s*(?<level>\d+)\b|\bLevel\s+(?<level>\d+)\s+(?:Raw Fragment|Guide Fragment)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
        public static string FragmentType(string name)
        { var match = Name.Match((name ?? "").Trim()); return match.Success ? match.Groups["type"].Value : ""; }
        public static bool TryUseWith(byte[] bytes, out int source, out int target)
        { source = target = 0; if (bytes == null || bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != 0xF7B1 || BitConverter.ToUInt32(bytes, 8) != 0x35) return false; source = BitConverter.ToInt32(bytes, 12); target = BitConverter.ToInt32(bytes, 16); return source != 0 && target != 0; }
        public static ThreadFragmentLevel Read(string itemName, IEnumerable<string> descriptions, int? itemLevel)
        {
            var name = Name.Match((itemName ?? "").Trim());
            if (!name.Success) return null;
            int level;
            if (name.Groups["level"].Success && int.TryParse(name.Groups["level"].Value, out level) && level > 0)
                return new ThreadFragmentLevel { Type = name.Groups["type"].Value, Level = level, Evidence = "Item name: " + itemName };
            var matches = (descriptions ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x))
                .SelectMany(x => Description.Matches(x).Cast<Match>()).Select(x => new { Match = x, Text = x.Groups["level"].Value }).ToArray();
            var values = matches.Select(x => int.TryParse(x.Text, out level) && level > 0 ? level : 0).Where(x => x > 0).Distinct().ToArray();
            if (values.Length == 1) return new ThreadFragmentLevel { Type = name.Groups["type"].Value, Level = values[0], Evidence = "Item description: " + matches.First(x => int.TryParse(x.Text, out int matched) && matched == values[0]).Match.Value };
            if (values.Length > 1) return null; // Ambiguous descriptions are not resolved by guessing.
            if (itemLevel.HasValue && itemLevel > 0)
                return new ThreadFragmentLevel { Type = name.Groups["type"].Value, Level = itemLevel.Value, Evidence = "Identified item Level property: " + itemLevel.Value };
            return null;
        }
    }
}
