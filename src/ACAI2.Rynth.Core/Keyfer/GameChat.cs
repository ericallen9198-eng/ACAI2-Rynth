#nullable disable
// Ported from Samm-AI ff44cc3; see origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ACAI2.Protocol
{
    // No Decal dependency: parsing and the reply whitelist can be tested separately.
    public static class GameChat
    {
        private static readonly Regex Chat = new Regex(
            @"^(?:\[(?<channel>[^\]]+)\]\s*)?(?:(?:<Tell:IIDString:[^:]+:(?<speaker>[^>]+)>.*?<\\Tell>)|(?<speaker2>You|[^,]+?))\s+(?<verb>say|says|think|thinks|tells you),\s+""(?<message>.*)""$",
            RegexOptions.IgnoreCase);

        public static ChatObservation Parse(string raw)
        {
            var match = Chat.Match(raw ?? "");
            if (!match.Success) return null;
            string channel = match.Groups["channel"].Success ? match.Groups["channel"].Value : "Local";
            // Only the outer envelope identifies a tell. Body text can contain pasted NPC/player tells.
            if (!match.Groups["channel"].Success && match.Groups["verb"].Value.Equals("tells you", StringComparison.OrdinalIgnoreCase)) channel = "Tell";
            if (channel.Equals("General Chat", StringComparison.OrdinalIgnoreCase)) channel = "General";
            if (channel.Equals("General", StringComparison.OrdinalIgnoreCase)) channel = "General";
            if (channel.Equals("Allegiance", StringComparison.OrdinalIgnoreCase)) channel = "Allegiance";
            if (channel.Equals("Tell", StringComparison.OrdinalIgnoreCase)) channel = "Tell";
            return new ChatObservation
            {
                Channel = channel,
                Speaker = (match.Groups["speaker"].Success ? match.Groups["speaker"].Value : match.Groups["speaker2"].Value).Trim(),
                Message = match.Groups["message"].Value.Trim()
            };
        }

        public static bool ShouldReply(ChatObservation chat, string self, bool publicReplies)
        {
            if (chat == null || string.IsNullOrWhiteSpace(chat.Message) ||
                chat.Speaker.Equals("You", StringComparison.OrdinalIgnoreCase) ||
                chat.Speaker.Equals(self, StringComparison.OrdinalIgnoreCase)) return false;
            if (chat.Channel.Equals("Tell", StringComparison.OrdinalIgnoreCase)) return true;
            return publicReplies && (chat.Channel.Equals("General", StringComparison.OrdinalIgnoreCase) ||
                chat.Channel.Equals("Allegiance", StringComparison.OrdinalIgnoreCase)) &&
                IsAddressed(chat.Message);
        }

        public static bool IsFollowUp(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || message.Length > 500 || Parse(message) != null) return false;
            if (Regex.IsMatch(message, @"^\s*(?:please\s+)?(?:continue|go on|tell me more|more details)(?:\s+please)?[.!?]*\s*$", RegexOptions.IgnoreCase)) return true;
            return message.Contains("?") || Regex.IsMatch(message,
                @"^\s*(?:(?:from\s+)?(?:what|which|when|where|who|why|how)\b|(?:do|does|did|can|could|would|is|are)\b|(?:yes|yeah|yep|no|nah|go ahead|do that|please)\b)", RegexOptions.IgnoreCase);
        }

        public static bool IsAddressed(string message)
        {
            var forwarded = Parse(message);
            if (forwarded != null && Regex.IsMatch(forwarded.Speaker, @"^keyfer(?:\s+sutherland)?$", RegexOptions.IgnoreCase)) return false;
            return Regex.IsMatch(message ?? "", @"^\s*(?:(?:hey|yo|hi|hello)\s+)?keyfer\b|\bkeyfer\s*[?.!,]*\s*$", RegexOptions.IgnoreCase);
        }

        public static bool TryQuietControl(string message, out bool quiet)
        {
            quiet = false;
            if (Parse(message) != null) return false;
            if (!Regex.IsMatch(message ?? "", @"^\s*(?:(?:hey|yo|hi|hello)\s+)?keyfer\b", RegexOptions.IgnoreCase)) return false;
            // Resume must precede quiet matching: "stop being quiet" is a resume request.
            if (Regex.IsMatch(message, @"\b(?:you\s+can\s+talk\s+again|talk\s+again|start\s+talking\s+again|resume|wake\s+up|quit\s+being\s+quiet|stop\s+being\s+quiet|speak\s+up)\b", RegexOptions.IgnoreCase)) return true;
            quiet = Regex.IsMatch(message, @"\b(?:quiet\s+down|be\s+quiet|hush|stop\s+(?:chiming|jumping)\s+in|chill(?:\s+out)?|quit\s+talking|stop\s+talking|give\s+it\s+a\s+rest|pipe\s+down)\b", RegexOptions.IgnoreCase);
            return quiet;
        }

        public static bool CanControlQuiet(ChatObservation chat, IEnumerable<string> owners, IEnumerable<string> excluded)
        {
            if (chat == null || excluded.Any(x => string.Equals(x, chat.Speaker, StringComparison.OrdinalIgnoreCase))) return false;
            return chat.Channel == "Allegiance" || owners.Any(x => string.Equals(x, chat.Speaker, StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryGeneralQuietControl(ChatObservation chat, out bool quiet)
        {
            quiet = false;
            return chat != null && (string.Equals(chat.Speaker, "Samm", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(chat.Speaker, "Toe", StringComparison.OrdinalIgnoreCase)) &&
                (TryQuietControl(chat.Message, out quiet) || (chat.Channel == "Tell" && TryQuietControl("Keyfer " + chat.Message, out quiet)));
        }

        public static IEnumerable<string> ReplyCommands(string channel, string speaker, string answer)
        {
            string prefix;
            if (channel == "Tell")
            {
                if (string.IsNullOrWhiteSpace(speaker) || !Regex.IsMatch(speaker, @"^[\p{L}\p{N} .'\-]+$")) yield break;
                prefix = "/t " + speaker + ", ";
            }
            else if (channel == "Allegiance") prefix = "/a ";
            else if (channel == "General") prefix = "/cg ";
            else yield break;
            string text = Regex.Replace(answer ?? "", @"[\p{Cc}\p{Cf}]+", " ").Trim();
            // Model output is always appended to a fixed speech command, never executed.
            int size = Math.Max(32, 220 - prefix.Length);
            while (text.Length > 0)
            {
                int take = Math.Min(size, text.Length);
                if (take < text.Length)
                {
                    int space = text.LastIndexOf(' ', take - 1, take);
                    if (space > take / 2) take = space;
                }
                yield return prefix + text.Substring(0, take);
                text = text.Substring(take).TrimStart();
            }
        }
    }
}
