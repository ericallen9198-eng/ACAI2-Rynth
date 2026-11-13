#nullable disable
// Ported from Samm-AI ff44cc3; see origin/ACAI2-SOURCE.md.
using System.Collections.Generic;
using System;

namespace ACAI2.Protocol
{
    public static class PlayerTimeoutNotice
    {
        public static string Format(DateTime until, DateTime now)
        {
            int minutes = (int)Math.Ceiling(Math.Max(0, (until - now).TotalMinutes));
            return "I'm sorry, you're on a reply timeout: " + minutes / 60 + " hours " + minutes % 60 + " minutes remaining. I'll be happy to respond when it ends.";
        }
    }
    public sealed class ChatObservation
    {
        public string InstanceId { get; set; }
        public string Server { get; set; }
        public string Character { get; set; }
        public string Channel { get; set; }
        public string Speaker { get; set; }
        public string Message { get; set; }
    }

    public sealed class KeyferAsk
    {
        public string InstanceId { get; set; }
        public string Server { get; set; }
        public string Character { get; set; }
        public string Channel { get; set; }
        // Stable Discord user ID or in-game character name, never a display name alone.
        public string SpeakerId { get; set; }
        public string Speaker { get; set; }
        public string Question { get; set; }
        public string RoomContext { get; set; }
    }

    public sealed class ArenaCompletion
    {
        public string InstanceId { get; set; }
        public string Server { get; set; }
        public string Character { get; set; }
        public string MatchId { get; set; }
    }

    public sealed class KeyferAnswer
    {
        public bool Ok { get; set; }
        public string Answer { get; set; }
        public string Error { get; set; }
        public string Notice { get; set; }
        public string AllegianceNotice { get; set; }
        public string TimeoutUntilUtc { get; set; }
        public string TimedOutPlayer { get; set; }
        public string ReplyChannel { get; set; }
        public string GeneralNotice { get; set; }
        public string NoticeChannel { get; set; }
        public string ArenaMatchId { get; set; }
        public string[] ArenaUpdates { get; set; }
        public string ArenaChannel { get; set; }
        public string ArenaCancelId { get; set; }
        public string ArenaNextGeneralUtc { get; set; }
        public string ConversationEmote { get; set; }
    }
}
