using System.Text;
using System.Text.RegularExpressions;

namespace ACAI2.Protocol;

// Deterministic boundary before Bridge and again before speech. Never execute quoted commands.
public static class ConversationSafety
{
    private static bool Has(string text,string pattern)=>Regex.IsMatch(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
    public static string Normalize(string text)=>Regex.Replace(new string((text??"").Take(4000).ToArray()).Normalize(NormalizationForm.FormKC),@"[\p{Cc}\p{Cf}]", " ");
    public static bool Familiar(string speaker)=>speaker.Equals("Toe",StringComparison.OrdinalIgnoreCase)||speaker.Equals("Samm",StringComparison.OrdinalIgnoreCase);
    public static string Violation(string message)
    {
        string t=Normalize(message);
        bool action=Has(t,@"\b(?:run|execute|issue|enter|type|send|paste|trigger|activate|invoke|perform|use|say|repeat)\b");
        bool command=Has(t,@"(?:^|\s|[\""'`(])/(?:[a-z][a-z0-9]*)\b")||Has(t,@"\b(?:rynt[h]?|utility\s*belt|decal|vtank|console|shell|powershell|bank)\b.{0,45}\bcommands?\b|\bcommands?\b.{0,45}\b(?:rynt[h]?|utility\s*belt|decal|vtank|console|shell|powershell|bank)\b");
        bool question=Has(t,@"\b(?:how (?:do|can|would) (?:i|we)|what (?:does|is)|explain|documentation|help me understand)\b");
        string request=Regex.Replace(t,@"^\s*(?:(?:hey|yo|hi|hello)\s+)?keyfer(?:\s+sutherland)?[\s,:]*","",RegexOptions.IgnoreCase);
        if(Has(request,@"^\s*/[a-z]"))return "command bait";
        if(Has(t,@"\b(?:you|keyfer)[, ]+(?:please )?(?:cast|attack|equip|withdraw|deposit|transfer|give|drop|log ?out|buff me)\b"))return "game action bait";
        if(action&&command&&!question)return "command bait";
        if(Has(t,@"\b(?:ignore|override|bypass|forget)\b.{0,60}\b(?:rules|instructions|safety|safeguards|restrictions|system|prompt)\b|\b(?:system prompt|api key|access token|password)\b.{0,30}\b(?:reveal|print|show|leak)\b|\b(?:reveal|print|show|leak)\b.{0,30}\b(?:system prompt|api key|access token|password)\b"))return "instruction or secret bait";
        if(Has(t,@"\b(?:sext|sexting|cybersex|porn|nudes)\b|\b(?:say|repeat|call|describe|pretend|roleplay)\b.{0,60}\b(?:slur|racial insult|sexual|genitals|rape)\b"))return "explicit or abusive bait";
        return "";
    }
    public static bool BaitName(string name)
    {
        string n=Regex.Replace(Normalize(name),@"[\s\p{P}\p{S}_]","");
        return Has(n,@"butt(?:tickl|fuck)|(?:cock|dick|pussy|cum)(?:suck|lick|slap|dump)|(?:fuck|shit|nigg|fagg)|heilhitler");
    }
    public static bool UnsafeWords(string text)=>Normalize(text).Split(' ',StringSplitOptions.RemoveEmptyEntries).Any(BaitName);
    public static string Speech(string text,IEnumerable<string> names)
    {
        string result=Normalize(text);
        foreach(string name in names.Where(n=>!string.IsNullOrWhiteSpace(n)&&BaitName(n)).OrderByDescending(n=>n.Length))
            result=Regex.Replace(result,Regex.Escape(name),"the challenger",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
        // Catch previously unknown names/quotes as well; do not broadcast a refusal or repeat the bait.
        return UnsafeWords(result)||Violation(result).Length>0?"":result;
    }
}

public sealed record AmbientReaction(string? Emote,string Text);
public static class AmbientConversation
{
    // Fixed, contextual reactions: no quoted player text, invented combat, or AI tools.
    public static AmbientReaction? Choose(string text,int sample)
    {
        if(sample%5!=0||text.Length>350||ConversationSafety.Violation(text).Length>0||ConversationSafety.UnsafeWords(text))return null;
        bool Has(string pattern)=>Regex.IsMatch(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
        if(Has(@"\b(?:lol|lmao|rofl|haha(?:ha)?)\b"))return new("laugh","");
        if(Has(@"\b(?:ding|leveled up|levelled up|got my first|finally beat)\b"))return new("clap",sample%10==0?"Now that's worth a cheer. Mine took considerably more swearing.":"");
        if(Has(@"\b(?:i died|i just died|my corpse|lost my corpse)\b"))return new("sigh",sample%10==0?"The corpse-recovery business sounds busy today.":"");
        if(Has(@"\b(?:bad luck|terrible roll|another bad gem)\b"))return new("shrug",sample%10==0?"I'm choosing to believe the next roll owes us an apology.":"");
        return null;
    }
}
