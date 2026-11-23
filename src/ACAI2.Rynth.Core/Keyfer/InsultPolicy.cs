using System.Text;
using System.Text.RegularExpressions;
namespace ACAI2.Protocol;

// Conservative direct-address patterns, shared by the native client and Bridge.
// Never classify a player's name or quoted/reported speech as their own insult.
public static class InsultPolicy
{
    private const string Words=@"(?:idiot|moron|stupid|useless|trash|loser|pathetic|dumb|worthless|dumbass|asshole|bastard|bitch|dickhead|shithead|fuckwit|piece of (?:shit|crap))";
    private const string Alias=@"queefer";
    public static bool Exempt(string speaker,string channel)=>channel!="Discord"&&ConversationSafety.Familiar(speaker);
    private static bool Match(string s,string pattern)=>Regex.IsMatch(s,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
    public static bool Directed(string? message,string channel,bool recentAddress=false)
    {
        if(string.IsNullOrWhiteSpace(message)||message.Length>4000)return false;
        string text=Regex.Replace(message.Normalize(NormalizationForm.FormKC),@"\p{Cf}","");
        text=Regex.Replace(text,"\"[^\"]*\"|“[^”]*”|`[^`]*`|(?<!\\w)'[^'\\r\\n]*'(?!\\w)"," ");
        text=Regex.Replace(text,@"(?m)^\s*>.*$","");
        string q=Regex.Replace(text.ToLowerInvariant(),@"[^\p{L}\p{N}]+"," ").Trim();
        // Direct derogatory vocative or explicit first-person renaming, as in the reported example.
        if((recentAddress||channel is "Tell" or "Discord")&&Match(q,@"^(?:(?:ok|okay|hey|yo|listen|thanks|thank you)\s+)?"+Alias+@"(?:\s+(?:you suck|shut up))?$"))return true;
        if(Match(q,@"\bi\s+(?:(?:will|shall)\s+)?(?:call|refer to)\s+you\s+(?:as\s+)?"+Alias+@"\b")||Match(q,@"^(?:keyfer\s+)?(?:please\s+)?(?:call yourself|your name is)\s+"+Alias+@"\b"))return true;
        bool addressed=recentAddress||channel is "Tell" or "Discord"||GameChat.IsAddressed(text)||Match(q,@"^keyfer\b");
        if(!addressed)return false;
        foreach(string sentence in Regex.Split(text,@"[.!?\r\n]+"))
        {
            string s=Regex.Replace(sentence.ToLowerInvariant(),@"[^\p{L}\p{N}]+"," ").Trim();
            s=Regex.Replace(s,@"^(?:(?:hey|yo|listen|ok|okay|well)\s+)?keyfer(?:\s+sutherland)?\s*","");
            s=Regex.Replace(s,@"\s+keyfer(?:\s+sutherland)?$","");
            if(Match(s,@"^(?:you(?: re| are)?|are you|is|you really are|you are just|you re just)\s+(?:(?:a|an|so|such|fucking|bloody)\s+){0,3}"+Words+@"\b"))return true;
            if(Match(s,@"^(?:(?:a|an|fucking|bloody)\s+){0,2}"+Words+@"(?:\s+bot)?$"))return true;
            if(Match(s,@"^(?:fuck you|fuck off|you suck|sucks|shut (?:the fuck )?up)\b"))return true;
            if(Match(s,@"^(?:you(?: re| are)?\s+)?"+Alias+@"\b"))return true;
        }
        return false;
    }
    public static string Notice(int choice)=>(((choice%3)+3)%3) switch
    {
        0=>"I don't like being spoken to that way. I'm taking 24 hours away from your messages.",
        1=>"I'm here for a good time, and that isn't it. You won't get another answer from me for 24 hours.",
        _=>"Call me Keyfer and keep it civil. I don't like the insults, so I'm leaving your messages alone for 24 hours."
    };
}
