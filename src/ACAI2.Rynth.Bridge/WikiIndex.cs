using System.Text.Json;
using System.Text.RegularExpressions;

namespace ACAI2.Bridge;

// Navigation only. Answers must still come from a live, successfully retrieved wiki page.
internal static class WikiIndex
{
    internal sealed class Entry
    {
        public string Title { get; set; } = "";
        public string[] Aliases { get; set; } = Array.Empty<string>();
    }
    private static readonly Entry[] Entries = Load();
    private static readonly HashSet<string> Generic = new(new[] { "quest", "quests", "bonus", "class", "abilities", "ability", "time", "game", "xp", "experience", "system", "systems", "overview", "notes", "commands", "rewards" });
    private static Entry[] Load()
    {
        using var stream = typeof(WikiIndex).Assembly.GetManifestResourceStream("ACAI2.Bridge.dreamweave-wiki-index.json")!;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.GetProperty("Entries").Deserialize<Entry[]>()!;
    }
    internal static string Normalize(string text) => " " + Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}0-9]+", " ").Trim() + " ";
    internal static IEnumerable<string> Find(string query)
    {
        string normalized = Normalize(query);
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        return Entries.Select(e => new { e.Title, Score = e.Aliases.Append(e.Title).Max(a => Score(normalized, words, a)) })
            .Where(e => e.Score > 0).OrderByDescending(e => e.Score).ThenBy(e => e.Title.Length).Take(2).Select(e => e.Title);
    }
    private static double Score(string query, HashSet<string> words, string alias)
    {
        string normalized = Normalize(alias);
        var terms = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0 || (terms.Length == 1 && Generic.Contains(terms[0]))) return 0;
        if (query.Contains(normalized, StringComparison.Ordinal)) return 10 + Math.Min(terms.Length, 8);
        int matched = terms.Count(words.Contains);
        return matched >= 2 && matched >= terms.Length * .65 ? (double)matched / terms.Length : 0;
    }
}
