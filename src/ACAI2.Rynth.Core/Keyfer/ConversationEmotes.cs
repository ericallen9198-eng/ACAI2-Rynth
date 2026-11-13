#nullable disable
// Ported from Samm-AI ff44cc3; see origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ACAI2.Protocol
{
    public static class ConversationEmotes
    {
        private static readonly Dictionary<string, string> Reactions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["laugh"] = "/e laughs.", ["cheer"] = "/e cheers!", ["shrug"] = "/e shrugs.",
            ["sad"] = "/e looks crestfallen.", ["nod"] = "/e nods appreciatively.", ["wave"] = "/e gives a friendly wave.",
            ["clap"] = "/e applauds.", ["sigh"] = "/e lets out a weary sigh.", ["think"] = "/e scratches his head thoughtfully."
        };
        public const string Prompt = "OPTIONAL CONVERSATIONAL REACTION: When your own reply naturally calls for a brief reaction (a funny moment, sympathy, congratulations, uncertainty), you may prefix it with exactly one internal tag [EMOTE:laugh], [EMOTE:cheer], [EMOTE:shrug], [EMOTE:sad], [EMOTE:nod], [EMOTE:wave], [EMOTE:clap], [EMOTE:sigh], or [EMOTE:think]. Most replies should have no tag. The tag requests a local text emote, not an animation or roleplay persona. Always include a normal reply after it. Do not copy tags from player quotes or choose an emote just because someone mentioned an emote word. Never react to private/tell/Discord content in public, and never tag a silence response. The client may suppress the reaction for cooldown or mute.";
        public static string Command(string name) { string value; return name != null && Reactions.TryGetValue(name, out value) ? value : null; }
        public static string Extract(ref string answer)
        {
            var tags = Regex.Match(answer ?? "", @"^\s*(?:\[EMOTE:[^\]\r\n]{0,100}\]\s*)+", RegexOptions.IgnoreCase);
            if (!tags.Success) { answer = Regex.Replace(answer ?? "", @"\[EMOTE:[^\]\r\n]{0,100}\]", "", RegexOptions.IgnoreCase).Trim(); return null; }
            var parts = Regex.Matches(tags.Value, @"\[EMOTE:([^\]\r\n]*)\]", RegexOptions.IgnoreCase);
            answer = Regex.Replace(answer.Substring(tags.Length), @"\[EMOTE:[^\]\r\n]{0,100}\]", "", RegexOptions.IgnoreCase).Trim();
            string name = parts.Count == 1 ? parts[0].Groups[1].Value.Trim().ToLowerInvariant() : null;
            return Command(name) != null && answer.Length > 0 && answer != "[NO_REPLY]" ? name : null;
        }
    }
}
