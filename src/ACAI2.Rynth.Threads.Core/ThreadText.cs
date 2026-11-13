using System.Text.RegularExpressions;
namespace ACAI2.Rynth.Threads.Core;
public static class ThreadText
{
    private static readonly Regex PlayerEnvelope=new(@"^(?:\[(?:General(?: Chat)?|Allegiance|Trade|LFG|Fellowship|Tell|Local)\]\s*)|^.*?\b(?:says|say|tells you|thinks|think),\s*""",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
    public static bool Native(string? line,int type=0)=>type is not (2 or 3 or 4 or 17 or 18) && !string.IsNullOrWhiteSpace(line)&&line.Length<=4096&&!PlayerEnvelope.IsMatch(line.Trim());
}
