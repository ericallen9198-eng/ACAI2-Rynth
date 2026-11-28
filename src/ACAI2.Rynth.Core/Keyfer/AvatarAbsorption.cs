using System.Globalization;
using System.Text.RegularExpressions;

namespace ACAI2.Protocol;

public sealed record AvatarAbsorption(string Owner, int Level, long AddedXp);
public static class AvatarAbsorptionParser
{
    public const string Kind = "AvatarAbsorption";
    public const string Speaker = "Server announcement";
    private static readonly Regex Notice = new(
        @"^(?<owner>[^\r\n]{1,128})'s Avatar \(level (?<level>\d{1,6})\) has been absorbed into this allegiance's Avatar, adding (?<xp>(?:\d{1,3}(?:,\d{3})+|\d+)) Avatar XP\.$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static AvatarAbsorption? Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 400 || raw.Any(char.IsControl) || GameChat.Parse(raw) != null) return null;
        var m = Notice.Match(raw);
        if (!m.Success || !int.TryParse(m.Groups["level"].Value, out int level) ||
            !long.TryParse(m.Groups["xp"].Value, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out long xp)) return null;
        return new(m.Groups["owner"].Value, level, xp);
    }
}
