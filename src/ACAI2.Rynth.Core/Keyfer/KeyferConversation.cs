#nullable disable
// Adapter port of Samm-AI ff44cc3; no Decal dependency.
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ACAI2.Protocol;

namespace ACAI2.Rynth.Core.Keyfer
{
    // Game API access belongs to PluginCore's render callback, never an async worker.
    internal sealed class KeyferConversation : IDisposable
    {
        private readonly KeyferSettings config;
        private readonly HttpClient http;
        private readonly SemaphoreSlim requests = new SemaphoreSlim(1, 1);
        private readonly BoundedQueue<Delivery> deliveries = new BoundedQueue<Delivery>();
        private int pending;
        private volatile bool stopped;
        private bool quiet;
        private QuietState state = new QuietState();
        private string stateIdentity;
        private string threadSpeaker;
        private string threadChannel;
        private DateTime threadUntil;
        private DreamWeaveEventTracker events = new DreamWeaveEventTracker();
        private string eventRoot = "";
        private readonly TreasureClient treasure;
        private readonly LeaderboardClient leaderboards;
        private readonly ConcurrentDictionary<string, DateTime> playerTimeouts = new ConcurrentDictionary<string, DateTime>();
        private readonly ConcurrentDictionary<string, DateTime> nextTimeoutNotice = new ConcurrentDictionary<string, DateTime>();
        private static string TimeoutKey(string server, string character, string speaker) => server + "::" + character + "::" + speaker.ToLowerInvariant();
        private bool TimedOut(string server, string character, string speaker, out DateTime until) =>
            playerTimeouts.TryGetValue(TimeoutKey(server, character, speaker), out until) && until > config.UtcNow;
        private DateTime lastTreasureErrorUtc;
        private readonly System.Collections.Generic.HashSet<string> publicNames = new(StringComparer.OrdinalIgnoreCase);
        private DateTime nextAmbientUtc;
        private DateTime arenaQuietUntil;
        private int ambientSample;
        private volatile bool timeoutStateDirty;
        private void SaveState(string server,string character)
        {
            timeoutStateDirty=false;
            foreach(var old in playerTimeouts.Where(x=>x.Value<=config.UtcNow).Select(x=>x.Key).ToArray())playerTimeouts.TryRemove(old,out _);
            foreach(var old in playerTimeouts.OrderByDescending(x=>x.Value).Skip(256).Select(x=>x.Key).ToArray())playerTimeouts.TryRemove(old,out _);
            string prefix=server+"::"+character+"::";
            state.Timeouts=playerTimeouts.Where(x=>x.Key.StartsWith(prefix,StringComparison.Ordinal)&&x.Value>config.UtcNow)
                .Take(256).ToDictionary(x=>x.Key.Substring(prefix.Length),x=>x.Value,StringComparer.OrdinalIgnoreCase);
            state.Strikes=state.Strikes.Where(x=>config.UtcNow-x.Value.LastUtc<TimeSpan.FromDays(30)).OrderByDescending(x=>x.Value.LastUtc).Take(256).ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
            string path=StatePath(server,character);Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path+".tmp",new KeyferJson().Serialize(state));File.Move(path+".tmp",path,true);
        }
        private void ObserveName(string name)
        { if(publicNames.Count<128)publicNames.Add(name); }
        private void TryAmbient(ChatObservation chat,string server,string character)
        {
            if(!config.keyferAmbientReactions||!config.keyferPublicReplies||!state.EmotesEnabled||quiet||pending>0||
                !arenaSpeech.IsEmpty||!speech.IsEmpty||!deliveries.IsEmpty||!eventSpeech.IsEmpty||config.UtcNow<threadUntil||
                config.UtcNow<nextAmbientUtc||config.UtcNow<nextEmoteUtc||config.UtcNow<arenaQuietUntil||
                state.Excluded.Any(x=>x.Equals(chat.Speaker,StringComparison.OrdinalIgnoreCase))||
                ConversationSafety.BaitName(chat.Speaker)||GameChat.IsAddressed(chat.Message)||ArenaCommands.Parse(chat.Message)!=null)return;
            var reaction=AmbientConversation.Choose(chat.Message,++ambientSample);
            if(reaction==null)return;
            nextAmbientUtc=config.UtcNow.AddMinutes(5);
            // Area chat remains local, allegiance remains allegiance. Never cross-post a tell.
            if(reaction.Text.Length>0)deliveries.Enqueue(new Delivery{Server=server,Character=character,Channel=chat.Channel,Speaker=chat.Speaker,Text=reaction.Text,Emote=reaction.Emote,Ambient=true,EmoteExpires=config.UtcNow.AddSeconds(10)});
            else speech.Enqueue(new Delivery{Server=server,Character=character,Channel=chat.Channel,Speaker=chat.Speaker,Text=ConversationEmotes.Command(reaction.Emote),EmoteOnly=true,Ambient=true,EmoteExpires=config.UtcNow.AddSeconds(10)});
        }

        public KeyferConversation(KeyferSettings config) { this.config = config; http=config.CreateHttp(); treasure = new TreasureClient(config); leaderboards = new LeaderboardClient(config); }

        public bool Enabled(string server, string character)
        {
            return config.keyferEnabled && string.Equals(server, config.keyferServer, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(character, config.keyferCharacter, StringComparison.OrdinalIgnoreCase);
        }

        private string StatePath(string server, string character)
        {
            using (var sha = SHA256.Create())
            {
                string key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(server + "\n" + character))).Replace("-", "");
                return Path.Combine(config.DataRoot,"KeyferState", key + ".json");
            }
        }

        private void LoadState(string server, string character)
        {
            string identity = server + "::" + character;
            if (stateIdentity == identity) return;
            stateIdentity = identity;
            events = new DreamWeaveEventTracker();
            eventRoot = Path.Combine(config.DataRoot,"Servers", IdentityPaths.SafeSegment(server),
                "Characters", IdentityPaths.SafeSegment(character), "WorldEvents");
            try
            {
                string eventState = Path.Combine(eventRoot, "state.json");
                if (File.Exists(eventState)) events.Restore(new KeyferJson().Deserialize<DreamWeaveEvent[]>(File.ReadAllText(eventState))
                    .Where(r => r != null && string.Equals(r.Character, character, StringComparison.OrdinalIgnoreCase)), config.UtcNow);
            }
            catch { /* Invalid event state must not disable conversations. */ }
            string path = StatePath(server, character);
            state = File.Exists(path) ? new KeyferJson().Deserialize<QuietState>(File.ReadAllText(path)) : new QuietState();
            if (state == null) state = new QuietState();
            state.Server = server;
            state.Character = character;
            if (state.Excluded == null) state.Excluded = new System.Collections.Generic.List<string> { "Wooty" };
            if (state.Owners == null) state.Owners = new System.Collections.Generic.List<string> { "Samm" };
            state.Timeouts ??= new(); state.Strikes ??= new();
            foreach(var entry in state.Timeouts.Where(x=>x.Value>config.UtcNow).Take(256))playerTimeouts[TimeoutKey(server,character,entry.Key)]=entry.Value;
            publicNames.Clear();nextAmbientUtc=config.UtcNow.AddMinutes(1);
            quiet = state.Quiet;
            threadUntil = DateTime.MinValue;
        }

        public string SetQuiet(string server, string character, bool value)
        {
            LoadState(server, character);
            string path = StatePath(server, character);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            state.Quiet = value;
            File.WriteAllText(path, new KeyferJson().Serialize(state));
            quiet = value;
            threadUntil = DateTime.MinValue;
            return value ? "I'll keep listening, but only answer tells or messages addressed to me." : "Alright, I'm talking again.";
        }

        public string SetEmotes(string server, string character, bool enabled)
        {
            LoadState(server, character); state.EmotesEnabled = enabled;
            string path = StatePath(server, character); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, new KeyferJson().Serialize(state));
            return "Conversational local emotes " + (enabled ? "ON" : "OFF") + ".";
        }

        public string EditList(string server, string character, string list, string operation, string name)
        {
            LoadState(server, character);
            var names = list == "owners" ? state.Owners : state.Excluded;
            if (operation == "list") return list + ": " + string.Join(", ", names);
            if (string.IsNullOrWhiteSpace(name)) return "Specify a character name.";
            if (operation == "add" && !names.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase))) names.Add(name);
            else if (operation == "remove") names.RemoveAll(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
            else if (operation != "add") return "Use add, remove, or list.";
            string path = StatePath(server, character);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, new KeyferJson().Serialize(state));
            return list + ": " + string.Join(", ", names);
        }

        public string TreasureStatus(bool standings) => treasure.Status(standings);

        public string Status(string server, string character)
        {
            if (!Enabled(server, character)) return "Keyfer replies disabled for this client.";
            LoadState(server, character);
            return "Keyfer enabled | public quiet " + (quiet ? "ON" : "OFF") + " | General muted " + (state.GeneralQuiet ? "YES" : "NO") +
                " | DW event alerts " + (config.keyferWorldEvents && server.Equals("DreamWeave", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF") + " | pending " + pending;
        }

        public void Capture(string raw, string instance, string server, string character)
        {
            if (stopped || config.SessionToken.IsCancellationRequested || !Enabled(server, character)) return;
            if(instance!=config.InstanceId)return;
            LoadState(server, character);
            leaderboards.Observe(raw, config.UtcNow);
            if (config.keyferWorldEvents && string.Equals(server, "DreamWeave", StringComparison.OrdinalIgnoreCase))
                foreach (string line in DreamWeaveEventParser.NativeLines(raw))
                {
                    var observed = events.Observe(server, line, config.UtcNow);
                    if (observed == null) continue;
                    observed.Character = character; observed.InstanceId = instance;
                    try
                    {
                        Directory.CreateDirectory(eventRoot);
                        var serializer = new KeyferJson();
                        File.AppendAllText(Path.Combine(eventRoot, config.UtcNow.ToString("yyyy-MM-dd") + ".jsonl"), serializer.Serialize(observed) + Environment.NewLine);
                        File.WriteAllText(Path.Combine(eventRoot, "state.json"), serializer.Serialize(events.Snapshot().ToArray()));
                    }
                    catch
                    {
                        deliveries.Enqueue(new Delivery { Server = server, Character = character, LocalOnly = true, Text = "[ACAI2] World-event history could not be saved." });
                    }
                    if (!quiet) deliveries.Enqueue(new Delivery { Server = server, Character = character,
                        Channel = "Allegiance", Text = DreamWeaveEventTracker.Announcement(observed), WorldEvent = true });
                    Task.Run(async () =>
                    {
                        try { await PostAsync("/v1/keyfer/world-event", observed).ConfigureAwait(false); }
                        catch { deliveries.Enqueue(new Delivery { Server = server, Character = character, LocalOnly = true, Text = "[ACAI2] World-event Bridge update unavailable; local alerts still work." }); }
                    });
                }
            if (config.treasureEnabled) treasure.Capture(raw, instance, server, character);
            ChatObservation chat = GameChat.Parse(raw);
            if (chat == null || chat.Speaker.Equals("You", StringComparison.OrdinalIgnoreCase) ||
                chat.Speaker.Equals(character, StringComparison.OrdinalIgnoreCase)) return;
            chat.InstanceId = instance;
            chat.Server = server;
            chat.Character = character;
            if (chat.Channel != "Tell" && chat.Channel != "Allegiance" && chat.Channel != "General" && chat.Channel != "Local") return;
            ObserveName(chat.Speaker);
            string arenaText=ArenaCommands.Parse(chat.Message);
            if(arenaText!=null&&ArenaCommands.Names(arenaText.StartsWith("preview ",StringComparison.OrdinalIgnoreCase)?arenaText.Substring(8):arenaText,out var first,out var second)){ObserveName(first);ObserveName(second);}

            DateTime existingTimeout;
            if (TimedOut(server, character, chat.Speaker, out existingTimeout))
            {
                string key = TimeoutKey(server, character, chat.Speaker); DateTime next;
                if (chat.Channel == "Tell" && (!nextTimeoutNotice.TryGetValue(key, out next) || config.UtcNow >= next))
                {
                    nextTimeoutNotice[key] = config.UtcNow.AddMinutes(1);
                    deliveries.Enqueue(new Delivery { Server = server, Character = character, Channel = "Tell", Speaker = chat.Speaker,
                        Text = PlayerTimeoutNotice.Format(existingTimeout, config.UtcNow), TimeoutExplanation = true });
                }
                return;
            }
            bool addressed=chat.Channel=="Tell"||GameChat.IsAddressed(chat.Message);
            string violation=addressed?ConversationSafety.Violation(chat.Message):"";
            if(violation.Length>0)
            {
                string key=chat.Speaker.ToLowerInvariant();
                state.Strikes.TryGetValue(key,out var strike);
                int count=strike!=null&&config.UtcNow-strike.LastUtc<TimeSpan.FromDays(30)?Math.Min(3,strike.Count+1):1;
                state.Strikes[key]=new SafetyStrike{Count=count,LastUtc=config.UtcNow};
                var until=config.UtcNow.AddMinutes(count==1?5:count==2?30:1440);
                playerTimeouts[TimeoutKey(server,character,chat.Speaker)]=until;
                try{SaveState(server,character);}catch{timeoutStateDirty=true;deliveries.Enqueue(new Delivery{Server=server,Character=character,LocalOnly=true,Text="[ACAI2] Timeout active for this session; state could not be saved."});}
                nextTimeoutNotice[TimeoutKey(server,character,chat.Speaker)]=config.UtcNow.AddMinutes(1);
                deliveries.Enqueue(new Delivery{Server=server,Character=character,Channel="Tell",Speaker=chat.Speaker,Text="I'm sitting this conversation out. "+PlayerTimeoutNotice.Format(until,config.UtcNow),TimeoutExplanation=true});
                return;
            }
            bool requestedQuiet;
            if (GameChat.TryGeneralQuietControl(chat, out requestedQuiet))
            {
                state.GeneralQuiet = requestedQuiet;
                string path = StatePath(server, character);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, new KeyferJson().Serialize(state));
                threadUntil = DateTime.MinValue;
                deliveries.Enqueue(new Delivery { Server = server, Character = character,
                    Channel = chat.Channel == "Tell" ? "Tell" : "Allegiance", Speaker = chat.Speaker, Control = true,
                    Text = requestedQuiet ? "General replies off. I'll still answer here and in tells." : "General replies back on." });
                return;
            }
            if (GameChat.TryQuietControl(chat.Message, out requestedQuiet))
            {
                bool authorized = GameChat.CanControlQuiet(chat, state.Owners.Concat(config.keyferControllers ?? new string[0]), state.Excluded);
                string answer = authorized ? SetQuiet(server, character, requestedQuiet) : "You are not authorized to change my quiet mode.";
                deliveries.Enqueue(new Delivery { Server = server, Character = character, Channel = chat.Channel,
                    Speaker = chat.Speaker, Text = answer, Control = true });
                return;
            }
            if(!addressed&&ConversationSafety.Violation(chat.Message).Length>0)return;
            if(chat.Channel=="Local"||chat.Channel=="Allegiance")TryAmbient(chat,server,character);
            if(chat.Channel=="Local"&&!ConversationSafety.Familiar(chat.Speaker))return;
            bool arenaCommand = arenaText != null;
            bool direct = (chat.Channel=="Local"&&ConversationSafety.Familiar(chat.Speaker)&&GameChat.IsAddressed(chat.Message)&&config.keyferPublicReplies) || GameChat.ShouldReply(chat, character, config.keyferPublicReplies) ||
                (arenaCommand && (chat.Channel == "Tell" || config.keyferPublicReplies));
            bool continuation = !quiet && config.keyferPublicReplies && chat.Channel == threadChannel &&
                chat.Speaker == threadSpeaker && config.UtcNow < threadUntil && GameChat.IsFollowUp(chat.Message);
            bool ask = (direct || continuation) && !(state.GeneralQuiet && chat.Channel == "General" && !ConversationSafety.Familiar(chat.Speaker));
            bool observe = chat.Channel == "Allegiance" || chat.Channel == "General";
            if (!ask && !observe) return;
            if(ask&&arenaCommand)arenaQuietUntil=config.UtcNow.AddSeconds(45);
            if (Interlocked.Increment(ref pending) > 16)
            {
                Interlocked.Decrement(ref pending);
                if (ask) deliveries.Enqueue(new Delivery { Server = server, Character = character, LocalOnly = true,
                    Text = "[ACAI2] Keyfer is busy; incoming question skipped." });
                return;
            }
            Task.Run(async () =>
            {
                bool acquired=false;
                try
                { await requests.WaitAsync(config.SessionToken).ConfigureAwait(false); acquired=true; }
                catch(OperationCanceledException){Interlocked.Decrement(ref pending);return;}
                try
                {
                    if (stopped || config.SessionToken.IsCancellationRequested) return;
                    DateTime queuedTimeout;
                    if (TimedOut(server, character, chat.Speaker, out queuedTimeout)) return;
                    if (observe) await PostAsync("/v1/keyfer/chat", chat).ConfigureAwait(false);
                    if (!ask) return;
                    // Legacy Bridge handoff reads Tell history before answering. Give owner public
                    // dialogue its own audience-scoped key so that reply can safely stay public.
                    bool publicOwnerDialog=ConversationSafety.Familiar(chat.Speaker)&&chat.Channel!="Tell"&&!arenaCommand;
                    string speakerId=publicOwnerDialog?"public-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chat.Channel+"\n"+chat.Speaker.ToLowerInvariant()))).ToLowerInvariant():chat.Speaker.ToLowerInvariant();
                    var result = await PostAsync("/v1/keyfer/ask", new KeyferAsk
                    {
                        InstanceId = instance, Server = server, Character = character,
                        Channel = chat.Channel=="Local"?(arenaCommand?"Tell":"Allegiance"):chat.Channel,
                        SpeakerId = speakerId, Speaker = chat.Speaker, Question = chat.Message
                    }).ConfigureAwait(false);
                    var reply = new KeyferJson().Deserialize<KeyferAnswer>(result);
                    DateTime timeoutUntil;
                    if (reply != null && !string.IsNullOrWhiteSpace(reply.TimedOutPlayer) && DateTime.TryParse(reply.TimeoutUntilUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out timeoutUntil))
                    {
                        string key = TimeoutKey(server, character, reply.TimedOutPlayer);
                        playerTimeouts[key] = playerTimeouts.TryGetValue(key,out var currentUntil)&&currentUntil>timeoutUntil?currentUntil:timeoutUntil;
                        timeoutStateDirty=true;
                        if (chat.Channel == "Tell" && !string.IsNullOrWhiteSpace(reply.Answer)) nextTimeoutNotice[key] = config.UtcNow.AddMinutes(1);
                    }
                    if (reply != null && reply.Ok && !string.IsNullOrWhiteSpace(reply.ArenaCancelId))
                        cancelledArena[reply.ArenaCancelId] = config.UtcNow.AddMinutes(2);
                    if (reply != null && reply.Ok && reply.ArenaUpdates != null && reply.ArenaUpdates.Length <= 5 &&
                        !string.IsNullOrWhiteSpace(reply.ArenaMatchId) && reply.ArenaChannel == chat.Channel && (reply.ArenaChannel == "General" || reply.ArenaChannel == "Allegiance"))
                    {
                        DateTime generalNext;
                        DateTime.TryParse(reply.ArenaNextGeneralUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out generalNext);
                        for (int arenaLine = 0; arenaLine < reply.ArenaUpdates.Length; arenaLine++)
                        {
                            string line = reply.ArenaUpdates[arenaLine];
                            if (!string.IsNullOrWhiteSpace(line) && line.Length <= 180)
                                deliveries.Enqueue(new Delivery { Server = server, Character = character, Channel = reply.ArenaChannel,
                                    Speaker = chat.Speaker, Text = line, Direct = true, ArenaId = reply.ArenaMatchId,
                                    ArenaExpires = config.UtcNow.AddSeconds(40), ArenaFinal = arenaLine == reply.ArenaUpdates.Length - 1, GeneralNextUtc = generalNext });
                        }
                    }
                    if (reply != null && !string.IsNullOrWhiteSpace(reply.Notice))
                        deliveries.Enqueue(new Delivery { Server = server, Character = character, LocalOnly = true, Text = reply.Notice });
                    if (reply != null && reply.Ok && !string.IsNullOrWhiteSpace(reply.AllegianceNotice))
                        deliveries.Enqueue(new Delivery { Server = server, Character = character, Channel = "Allegiance",
                            Text = reply.AllegianceNotice });
                    bool stayHere=publicOwnerDialog;
                    if (reply != null && reply.Ok && !string.IsNullOrWhiteSpace(reply.GeneralNotice) && !(stayHere&&reply.ReplyChannel=="Tell"))
                        deliveries.Enqueue(new Delivery { Server = server, Character = character, Channel = reply.NoticeChannel == "Allegiance" ? "Allegiance" : "General", Speaker = chat.Speaker,
                            Text = reply.GeneralNotice, Direct = true });
                    deliveries.Enqueue(new Delivery { Server = server, Character = character, Channel = reply != null && reply.Ok && reply.ReplyChannel == "Tell" && !stayHere ? "Tell" : chat.Channel,
                        Speaker = chat.Speaker, Text = reply != null && reply.Ok ? reply.Answer : "[ACAI2] Keyfer: " + (reply == null ? "Empty Bridge response." : reply.Error),
                        LocalOnly = reply == null || !reply.Ok, Direct = direct,
                        Emote = reply != null && reply.Ok ? reply.ConversationEmote : null,
                        TimeoutExplanation = reply != null && !string.IsNullOrWhiteSpace(reply.TimeoutUntilUtc) && chat.Channel == "Tell" });
                }
                catch (Exception ex)
                {
                    deliveries.Enqueue(new Delivery { Server = server, Character = character, LocalOnly = true,
                        Text = "[ACAI2] Keyfer request failed: " + ex.GetType().Name });
                }
                finally { if(acquired)requests.Release(); Interlocked.Decrement(ref pending); }
            });
        }

        private async Task<string> PostAsync(string path, object payload)
        {
            var json = new KeyferJson().Serialize(payload);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await http.PostAsync(config.bridgeUrl.TrimEnd('/') + path, content, config.SessionToken).ConfigureAwait(false))
            {
                string body = await response.Content.ReadAsStringAsync(config.SessionToken).ConfigureAwait(false);
                config.SessionToken.ThrowIfCancellationRequested();
                if (!path.EndsWith("/ask") && !response.IsSuccessStatusCode) throw new HttpRequestException("Observation rejected.");
                return body;
            }
        }

        private readonly BoundedQueue<Delivery> speech = new BoundedQueue<Delivery>();
        private readonly BoundedQueue<Delivery> eventSpeech = new BoundedQueue<Delivery>();
        private DateTime nextSpeechUtc;
        private DateTime nextArenaUtc;
        private DateTime nextEmoteUtc;
        private readonly BoundedQueue<Delivery> arenaSpeech = new BoundedQueue<Delivery>();
        private readonly ConcurrentDictionary<string, DateTime> cancelledArena = new ConcurrentDictionary<string, DateTime>();
        public void Drain(string server, string character, string instance, Action<string> send, Action<string> local)
        {
            if (stopped || config.SessionToken.IsCancellationRequested || instance!=config.InstanceId || !Enabled(server,character)) return;
            if (Enabled(server, character))
            {
                LoadState(server, character);
                if(timeoutStateDirty){try{SaveState(server,character);}catch{timeoutStateDirty=false;local("[ACAI2] Timeout state could not be saved; current-session timeouts remain active.");}}
                leaderboards.Tick(instance, server, character, send, local);
                try { treasure.Tick(instance, server, character, quiet, send, local, text => deliveries.Enqueue(new Delivery {
                    Server = server, Character = character, Channel = "Allegiance", Text = text, WorldEvent = true })); }
                catch (Exception ex)
                {
                    if (config.UtcNow - lastTreasureErrorUtc >= TimeSpan.FromSeconds(30))
                    { lastTreasureErrorUtc = config.UtcNow; local("[ACAI2 Treasure] Local persistence error: " + ex.GetType().Name); }
                }
            }
            foreach (var expired in cancelledArena.Where(x => x.Value < config.UtcNow).Select(x => x.Key).ToArray()) { DateTime ignored; cancelledArena.TryRemove(expired, out ignored); }
            Delivery item;
            while (deliveries.TryDequeue(out item))
            {
                if (config.SessionToken.IsCancellationRequested || item.Server != server || item.Character != character) continue;
                if (item.LocalOnly) { local(item.Text); continue; }
                foreach (string command in GameChat.ReplyCommands(item.Channel, item.Speaker, ConversationSafety.Speech(item.Text,publicNames)))
                    (!string.IsNullOrEmpty(item.ArenaId) ? arenaSpeech : item.WorldEvent ? eventSpeech : speech).Enqueue(new Delivery { Server = server, Character = character, Channel = item.Channel,
                        Speaker = item.Speaker, Text = command, Control = item.Control, Direct = item.Direct, WorldEvent = item.WorldEvent, TimeoutExplanation = item.TimeoutExplanation, ArenaId = item.ArenaId, ArenaExpires = item.ArenaExpires, ArenaFinal = item.ArenaFinal, GeneralNextUtc = item.GeneralNextUtc, Emote = item.Emote, Ambient=item.Ambient, EmoteExpires=item.EmoteExpires });
            }
            if (config.UtcNow < nextSpeechUtc) return;
            while ((config.UtcNow >= nextArenaUtc && arenaSpeech.TryDequeue(out item)) || eventSpeech.TryDequeue(out item) || speech.TryDequeue(out item))
            {
                DateTime until;
                bool playerBlocked = !string.IsNullOrEmpty(item.Speaker) && playerTimeouts.TryGetValue(server + "::" + character + "::" + item.Speaker.ToLowerInvariant(), out until) && until > config.UtcNow;
                if ((item.Ambient && (!config.keyferAmbientReactions||!state.EmotesEnabled||quiet||config.UtcNow>item.EmoteExpires||config.UtcNow<arenaQuietUntil||pending>0)) ||
                    (item.EmoteOnly && (!state.EmotesEnabled || config.UtcNow > item.EmoteExpires)) ||
                    (!string.IsNullOrEmpty(item.ArenaId) && (config.UtcNow > item.ArenaExpires || cancelledArena.ContainsKey(item.ArenaId))) ||
                    (!item.Control && !item.TimeoutExplanation && playerBlocked) || item.Server != server || item.Character != character ||
                    (state.GeneralQuiet && item.Channel == "General" && !ConversationSafety.Familiar(item.Speaker??"")) ||
                    (quiet && item.Channel != "Tell" && !item.Control && !item.Direct)) continue;
                string outgoing = item.Text;
                if (item.ArenaFinal && item.Channel == "General" && item.GeneralNextUtc != DateTime.MinValue)
                    outgoing += " " + ArenaCooldown.Notice(item.GeneralNextUtc, config.UtcNow);
                send(outgoing);
                if(item.EmoteOnly)nextEmoteUtc=config.UtcNow.AddSeconds(90);
                if (item.ArenaFinal && item.Channel == "Allegiance")
                {
                    string completedId = item.ArenaId;
                    Task.Run(async () =>
                    {
                        try { await PostAsync("/v1/keyfer/arena/complete", new ArenaCompletion { InstanceId = instance, Server = server, Character = character, MatchId = completedId }).ConfigureAwait(false); }
                        catch { /* The 40-second active window is the fallback if this receipt cannot reach Bridge. */ }
                    });
                }
                if (!item.EmoteOnly && state.EmotesEnabled && ConversationEmotes.Command(item.Emote) != null &&
                    (item.Channel == "General" || item.Channel == "Allegiance" || item.Channel == "Local") && config.UtcNow >= nextEmoteUtc)
                {
                    nextEmoteUtc = config.UtcNow.AddSeconds(90);
                    speech.Enqueue(new Delivery { Server = server, Character = character, Channel = item.Channel, Speaker = item.Speaker,
                        Text = ConversationEmotes.Command(item.Emote), EmoteOnly = true, EmoteExpires = config.UtcNow.AddSeconds(12), Direct = item.Direct, Ambient=item.Ambient });
                }
                if (!string.IsNullOrEmpty(item.ArenaId)) nextArenaUtc = config.UtcNow.AddSeconds(4);
                if (!item.Ambient && !item.EmoteOnly && string.IsNullOrEmpty(item.ArenaId) && !item.WorldEvent && (item.Channel == "Allegiance" || item.Channel == "General" || item.Channel == "Local"))
                {
                    threadSpeaker = item.Speaker;
                    threadChannel = item.Channel;
                    threadUntil = item.Control || quiet ? DateTime.MinValue : config.UtcNow.AddSeconds(45);
                }
                nextSpeechUtc = config.UtcNow.AddMilliseconds(1500);
                break;
            }
        }

        public void Dispose() { stopped = true; treasure.Dispose(); leaderboards.Dispose(); http.Dispose(); }
        public sealed class QuietState
        {
            public string Server { get; set; } = "";
            public string Character { get; set; } = "";
            public bool Quiet { get; set; }
            public bool EmotesEnabled { get; set; } = true;
            public bool GeneralQuiet { get; set; }
            public System.Collections.Generic.Dictionary<string,DateTime> Timeouts {get;set;}=new();
            public System.Collections.Generic.Dictionary<string,SafetyStrike> Strikes {get;set;}=new();
            public System.Collections.Generic.List<string> Excluded { get; set; } = new System.Collections.Generic.List<string> { "Wooty" };
            public System.Collections.Generic.List<string> Owners { get; set; } = new System.Collections.Generic.List<string> { "Samm" };
        }
        public sealed class SafetyStrike {public int Count {get;set;} public DateTime LastUtc {get;set;} }
        private sealed class Delivery
        {
            public string Server;
            public string Character;
            public string Channel;
            public string Speaker;
            public string Text;
            public bool LocalOnly;
            public bool Control;
            public bool Direct;
            public bool WorldEvent;
            public bool TimeoutExplanation;
            public string ArenaId;
            public DateTime ArenaExpires;
            public bool ArenaFinal;
            public DateTime GeneralNextUtc;
            public string Emote;
            public bool EmoteOnly;
            public bool Ambient;
            public DateTime EmoteExpires;
        }
    }
}
