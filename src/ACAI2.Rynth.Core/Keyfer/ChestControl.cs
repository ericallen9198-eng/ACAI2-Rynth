using System.Text.Json;
using System.Text.RegularExpressions;
using ACAI2.Protocol;
namespace ACAI2.Rynth.Core.Keyfer;

// A fixed owner-only command catalog, separate from model output and speech generation.
public static class ChestControl
{
    public static bool TryRequest(string raw,out ChatObservation chat,out string verb)
    {
        chat=GameChat.Parse(raw);verb="";
        if(chat==null||chat.Channel is not ("Tell" or "Allegiance")||
            !(chat.Speaker.Equals("Toe",StringComparison.OrdinalIgnoreCase)||chat.Speaker.Equals("Samm",StringComparison.OrdinalIgnoreCase)))return false;
        var m=Regex.Match(chat.Message,@"^\s*Keyfer[ ,]*\s+(pull keys|stop pulling|pull status)[.!]?\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        if(!m.Success)return false;
        verb=m.Groups[1].Value.ToLowerInvariant();return true;
    }
    public static (string? Command,string Reply) Decide(string verb,string? json)
    {
        if(verb=="stop pulling")return ("/ra itemtool chest stop","I've requested that chest pulling stop.");
        try
        {
            if(json==null||json.Length>32768)throw new JsonException();
            using var doc=JsonDocument.Parse(json);var chest=doc.RootElement.GetProperty("chest");
            bool running=chest.GetProperty("running").GetBoolean();
            if(verb=="pull status")return (null,(running?"Pulling: ":"Stopped: ")+Clean(chest.GetProperty("status").GetString()));
            if(verb!="pull keys")return (null,"Unknown chest request.");
            if(running)return (null,"I'm already pulling keys.");
            if(string.IsNullOrWhiteSpace(chest.GetProperty("name").GetString())||string.IsNullOrWhiteSpace(chest.GetProperty("key").GetString()))
                return (null,"Select my key and chest using the Keyfer Chest panel first.");
            return ("/ra itemtool chest start","I've requested chest pulling with my configured key, chest and current loot profile.");
        }
        catch(Exception ex) when(ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {return (null,"I can't read the Rynth chest looter status right now; check RynthAi and the Item Tool.");}
    }
    private static string Clean(string? text)=>new string((text??"Status unavailable.").Where(c=>!char.IsControl(c)).Take(170).ToArray());
}
