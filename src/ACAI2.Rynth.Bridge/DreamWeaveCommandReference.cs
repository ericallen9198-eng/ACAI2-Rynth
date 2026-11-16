using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ACAI2.Bridge;

internal static class DreamWeaveCommandReference
{
    private sealed class Section
    {
        public Section() { }
        public string Topic { get; set; } = "";
        public string[] Commands { get; set; } = Array.Empty<string>();
        public string[] Keywords { get; set; } = Array.Empty<string>();
        public string Text { get; set; } = "";
    }
    private static readonly Section[] sections = Load();
    private static Section[] Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("dreamweave-commands-reference.json"));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        return JsonSerializer.Deserialize<Section[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
    internal static string Find(string question)
    {
        string q = question.ToLowerInvariant();
        var matching = sections.Where(s => s.Commands.Any(c => Regex.IsMatch(q, @"(?<![\p{L}\p{N}])/?" + Regex.Escape(c.TrimStart('/')) + @"\b")) ||
            s.Keywords.Any(k => Regex.IsMatch(q, @"\b" + Regex.Escape(k) + @"\b"))).Take(4).ToArray();
        return string.Join("\n\n", matching.Select(s => s.Text));
    }
    internal static string Index => string.Join("; ", sections.Select(s => string.Join(", ", s.Commands) + " (" + s.Topic + ")"));
}
