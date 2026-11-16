using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ACAI2.Bridge;

internal sealed record ArenaSkill(string Name, double Base, double Buffed, string Training);
internal sealed record ArenaAbility(string Name, string Class, int Rank, string Description);
internal sealed class ArenaProfile
{
    public int ParserRevision { get; set; }
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime RetrievedUtc { get; set; }
    public int Level { get; set; }
    public double Health { get; set; }
    public Dictionary<string, double> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, double> Vitals { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ArenaSkill> Skills { get; set; } = new();
    public List<ArenaAbility> Abilities { get; set; } = new();
    public void Validate(string expected)
    {
        if (string.IsNullOrEmpty(Name) || !Name.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Sheet name does not match " + expected + ".");
        if (!ACAI2.Protocol.ArenaCommands.ValidName(Name) || Level < 1 || Level > 100000 || Health < 1 || Health > 10000000 || !double.IsFinite(Health) ||
            Skills == null || Skills.Count == 0 || Skills.Count > 100 || Skills.Any(x => x == null || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 80 ||
                x.Training == null || x.Base < 0 || x.Base > 100000 || x.Buffed < 0 || x.Buffed > 100000 || !double.IsFinite(x.Buffed) || !double.IsFinite(x.Base)) ||
            Skills.Select(x => x.Name.ToLowerInvariant()).Distinct().Count() != Skills.Count ||
            Abilities == null || Abilities.Count > 300 || Abilities.Any(x => x == null || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 100 ||
                x.Class == null || x.Class.Length > 50 || x.Rank < 0 || x.Rank > 100)) throw new InvalidDataException("Sheet lacks valid level, health, or skill data; no build was guessed.");
    }
}

internal static class ArenaSheetParser
{
    internal const int Revision = 2;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    internal static string Plain(string html)
    {
        html = Regex.Replace(html, @"<(script|style)\b[^>]*>.*?</\1\s*>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline, Timeout);
        html = Regex.Replace(html, @"</(?:tr|p|div|h[1-6]|li|section|dt|dd)>|<br\s*/?>", "\n", RegexOptions.IgnoreCase, Timeout);
        html = Regex.Replace(html, @"</(?:td|th|label)>", "\t", RegexOptions.IgnoreCase, Timeout);
        html = Regex.Replace(html, "<[^>]*>", "", RegexOptions.None, Timeout);
        return WebUtility.HtmlDecode(html).Replace('\u00a0', ' ');
    }
    internal static ArenaProfile Parse(string body, string expected, string url, DateTime now)
    {
        if (body.Length > 2000000) throw new InvalidDataException("Sheet response too large.");
        // Explicit structured fixtures/exports. Arbitrary application script JSON is not treated as a profile.
        if (body.TrimStart().StartsWith("{"))
        {
            var profile = JsonSerializer.Deserialize<ArenaProfile>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Empty sheet.");
            profile.ParserRevision = Revision; profile.Url = url; profile.RetrievedUtc = now; profile.Validate(expected); return profile;
        }
        var p = new ArenaProfile { ParserRevision = Revision, Url = url, RetrievedUtc = now };
        string plain = Plain(body);
        var lines = plain.Split('\n').Select(x => Regex.Replace(x, @"[ \r]+", " ").Trim()).Where(x => x.Length > 0).ToArray();
        bool named = Regex.Matches(body, @"<h[12]\b[^>]*>(.*?)</h[12]>", RegexOptions.IgnoreCase | RegexOptions.Singleline, Timeout)
            .Cast<Match>().Any(x => Plain(x.Groups[1].Value).Trim().Equals(expected, StringComparison.OrdinalIgnoreCase));
        string section = "";
        foreach (string line in lines)
        {
            var cells = Regex.Split(line, @"\t+|\s*:\s*").Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            if (cells.Length == 0) continue;
            string label = cells[0];
            if ((label.Equals("Name", StringComparison.OrdinalIgnoreCase) || label.Equals("Character", StringComparison.OrdinalIgnoreCase)) && cells.Length > 1)
            { p.Name = cells[1]; continue; }
            if (new[] { "Skills", "Attributes", "Vitals", "Class Abilities", "Abilities" }.Contains(line, StringComparer.OrdinalIgnoreCase)) { section = line; continue; }
            if (label.Equals("Level", StringComparison.OrdinalIgnoreCase) && cells.Length > 1 && Number(cells[1], out double level)) p.Level = (int)level;
            if (label.Equals("Health", StringComparison.OrdinalIgnoreCase) && cells.Length > 1 && Number(cells[cells.Length - 1], out double hp)) { p.Health = hp; p.Vitals[label] = hp; }
            if (section.Equals("Attributes", StringComparison.OrdinalIgnoreCase) && cells.Length > 1 && Number(cells[cells.Length - 1], out double attr)) p.Attributes[label] = attr;
            if (section.Equals("Vitals", StringComparison.OrdinalIgnoreCase) && cells.Length > 1 && Number(cells[cells.Length - 1], out double vital)) p.Vitals[label] = vital;
            if (section.Equals("Skills", StringComparison.OrdinalIgnoreCase) && cells.Length >= 2 && Number(cells[cells.Length - 1], out double skill))
            {
                var nums = cells.Skip(1).Where(x => Number(x, out _)).Select(x => { Number(x, out double v); return v; }).ToArray();
                string training = cells.FirstOrDefault(x => Regex.IsMatch(x, @"^(?:specialized|trained|untrained)$", RegexOptions.IgnoreCase)) ?? "Unknown";
                p.Skills.Add(new ArenaSkill(label, nums.FirstOrDefault(), skill, training));
            }
            if ((section.Equals("Class Abilities", StringComparison.OrdinalIgnoreCase) || section.Equals("Abilities", StringComparison.OrdinalIgnoreCase)) && cells.Length >= 3)
            {
                var rank = Regex.Match(cells[2], @"^(\d+)(?:/\d+)?$");
                if (rank.Success && int.TryParse(rank.Groups[1].Value, out int parsedRank)) p.Abilities.Add(new ArenaAbility(cells[0], cells[1], parsedRank, string.Join(" ", cells.Skip(3))));
            }
        }
        ReadVisibleText(p, plain, expected);
        ReadAbilityBlocks(p, plain);
        if (p.Name.Length == 0 && named) p.Name = expected;
        p.Validate(expected); return p;
    }
    internal static readonly string[] Classes = { "Archer", "Archmage", "Berserker", "BloodMage", "Rogue", "Spellsword", "Vanguard", "VoidSummon" };
    private static void ReadVisibleText(ArenaProfile p, string text, string expected)
    {
        string section = "", training = "Unknown";
        foreach (string raw in text.Split('\n'))
        {
            string line = Regex.Replace(raw, @"\s+", " ").Trim();
            line = Regex.Replace(line, @"^(?:#{1,6}\s+|[-*]\s+)", "");
            if (line.Equals(expected, StringComparison.OrdinalIgnoreCase)) p.Name = line;
            var level = Regex.Match(line, @"^Level\s*(\d+)(?:\s*Generated.*)?$", RegexOptions.IgnoreCase);
            if (level.Success && int.TryParse(level.Groups[1].Value, out int parsedLevel)) p.Level = parsedLevel;
            if (new[] { "Skills", "Attributes", "Vitals", "Class abilities", "Equipped", "Ranks" }.Contains(line, StringComparer.OrdinalIgnoreCase))
            { section = line.ToLowerInvariant(); continue; }
            if (line == "Specialized" || line == "Trained" || line == "Untrained") { training = line; continue; }
            var stat = Regex.Match(line, @"^(?<name>[\p{L}][\p{L} '\-]+?)\s*(?<base>[0-9,]+)\s*\((?<buff>[0-9,]+)\)$", RegexOptions.None, Timeout);
            if (!stat.Success) continue;
            string label = stat.Groups["name"].Value.Trim(); Number(stat.Groups["base"].Value, out double baseValue); Number(stat.Groups["buff"].Value, out double buffed);
            if (section == "skills")
            {
                p.Skills.RemoveAll(x => x.Name.Equals(label, StringComparison.OrdinalIgnoreCase));
                p.Skills.Add(new ArenaSkill(label, baseValue, buffed, training));
            }
            else if (section == "attributes") p.Attributes[label] = buffed;
            else if (section == "vitals") { p.Vitals[label] = buffed; if (label.Equals("Health", StringComparison.OrdinalIgnoreCase)) p.Health = buffed; }
        }
    }
    private static void ReadAbilityBlocks(ArenaProfile p, string plain)
    {
        var lines = plain.Split('\n').Select(x => Regex.Replace(Regex.Replace(x, @"^\s*(?:#{1,6}\s+|[-*]\s+)", ""), @"\s+", " ").Trim()).ToArray();
        bool inside = false; string tree = ""; var block = new System.Text.StringBuilder(); bool sawRanks = false;
        void Flush()
        {
            if (tree.Length == 0) { block.Clear(); return; }
            string text = block.ToString();
            foreach (Match m in Regex.Matches(text, @"(?<name>[\p{L}][\p{L}0-9 '&+():\-\s]*?)\s*(?<rank>\d+)\s*/\s*(?<max>\d+)\s*(?:T(?:ier)?\s*\d+)?", RegexOptions.IgnoreCase, Timeout))
            {
                string name = Regex.Replace(m.Groups["name"].Value, @"\s+", " ").Trim();
                if (name.Length <= 100 && int.TryParse(m.Groups["rank"].Value, out int rank) && rank <= 100 &&
                    !p.Abilities.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && x.Class.Equals(tree, StringComparison.OrdinalIgnoreCase)))
                    p.Abilities.Add(new ArenaAbility(name, tree, rank, "Published name/rank; fictional arena adaptation."));
            }
            block.Clear();
        }
        foreach (string line in lines)
        {
            if (Regex.IsMatch(line, @"^class\s+abilities\s*:?$", RegexOptions.IgnoreCase)) { Flush(); inside = true; tree = ""; continue; }
            if (!inside) continue;
            if (Regex.IsMatch(line, @"^(?:Equipped|Equipment|Attributes|Vitals|Skills|Ranks)\s*:?$", RegexOptions.IgnoreCase)) { Flush(); inside = false; tree = ""; continue; }
            string normalized = Regex.Replace(line, @"\s+", "");
            string? known = Classes.FirstOrDefault(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (known != null) { Flush(); tree = known; continue; }
            if (Regex.IsMatch(line, @"\d+\s*/\s*\d+", RegexOptions.None, Timeout)) sawRanks = true;
            block.Append(line).Append(' ');
        }
        Flush();
        if (sawRanks && p.Abilities.Count == 0) throw new InvalidDataException("Class abilities are present but could not be read; refusing a basic-stats-only match. Refresh the sheet after checking its class headings.");
    }
    private static bool Number(string s, out double value) => double.TryParse(s.Replace(",", "").Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
}
