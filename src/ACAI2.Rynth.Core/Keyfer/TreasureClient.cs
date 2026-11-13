#nullable disable
// Adapter port of Samm-AI ff44cc3; no Decal dependency.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using ACAI2.Protocol;

namespace ACAI2.Rynth.Core.Keyfer
{
    // Capture is durable before transmission. All game actions happen on the render thread.
    internal sealed class TreasureClient : IDisposable
    {
        private readonly KeyferSettings config;
        private readonly HttpClient http;
        private readonly object gate = new object();
        private readonly BoundedQueue<string> local = new BoundedQueue<string>();
        private readonly BoundedQueue<string> remarks = new BoundedQueue<string>();
        private readonly BoundedQueue<TreasurePrize> claimed = new BoundedQueue<TreasurePrize>();
        private readonly List<TreasureReport> outbox = new List<TreasureReport>();
        private List<PrizeJournal> journal = new List<PrizeJournal>();
        private string identity = "", root = "";
        private DateTime nextPoll;
        private TreasureReply latest;
        private DateTime latestUtc;
        private int polling;
        private bool stopped, warnedCommand;
        internal TreasureClient(KeyferSettings config) { this.config = config; http=config.CreateHttp(); }
        private void Load(string server, string character)
        {
            string next = server + "::" + character;
            if (identity == next) return;
            if (polling != 0) throw new InvalidOperationException("Wait for treasure sync before changing identities.");
            root = Path.Combine(config.DataRoot,"Servers", IdentityPaths.SafeSegment(server), "Characters", IdentityPaths.SafeSegment(character), "Treasure");
            Directory.CreateDirectory(root);
            outbox.Clear();
            var serializer = new KeyferJson { MaxJsonLength = 8 * 1024 * 1024 };
            string pending = Path.Combine(root, "outbox.json"), prizes = Path.Combine(root, "prizes.json");
            if (File.Exists(pending)) outbox.AddRange(serializer.Deserialize<List<TreasureReport>>(File.ReadAllText(pending)) ?? new List<TreasureReport>());
            journal = File.Exists(prizes) ? serializer.Deserialize<List<PrizeJournal>>(File.ReadAllText(prizes)) ?? new List<PrizeJournal>() : new List<PrizeJournal>();
            foreach (var entry in journal.Where(j => j.State == "claiming"))
            { entry.State = "uncertain"; entry.Attempted = true; }
            Save("prizes.json", journal);
            // Recover a capture interrupted between append and outbox replacement. Replays keep their original IDs.
            var known = new HashSet<string>(outbox.Select(r => r.Id));
            foreach (string log in Directory.GetFiles(root, "*.jsonl").Where(f => Regex.IsMatch(Path.GetFileName(f), @"^\d{4}-\d{2}-\d{2}\.jsonl$" )).OrderBy(f => f).Reverse().Take(3))
                foreach (string line in File.ReadLines(log))
                {
                    try
                    {
                        var report = serializer.Deserialize<TreasureReport>(line);
                        if (report != null && known.Add(report.Id)) outbox.Add(report);
                    }
                    catch { local.Enqueue("An incomplete local treasure-log record was skipped during recovery."); }
                }
            Save("outbox.json", outbox);
            identity = next; warnedCommand = false;
        }
        private void Save(string file, object value)
        {
            if(stopped||config.SessionToken.IsCancellationRequested)return;
            string path = Path.Combine(root, file), temp = path + ".new";
            File.WriteAllText(temp, new KeyferJson { MaxJsonLength = 8 * 1024 * 1024 }.Serialize(value));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
        internal void Capture(string raw, string instance, string server, string character)
        {
            if (stopped || config.SessionToken.IsCancellationRequested || !config.treasureEnabled || !server.Equals("DreamWeave", StringComparison.OrdinalIgnoreCase)) return;
            lock (gate)
            {
                Load(server, character);
                var attempt = journal.LastOrDefault(j => !string.IsNullOrEmpty(j.SubmittedUtc));
                if (attempt != null && DateTime.TryParse(attempt.SubmittedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime submitted) &&
                    config.UtcNow - submitted.ToUniversalTime() < TimeSpan.FromSeconds(30) && GameChat.Parse(raw) == null &&
                    !string.IsNullOrWhiteSpace(raw) && raw.Length <= 4000 && Regex.IsMatch(raw, @"\b(bank|pyreals|transfer(?:red)?)\b", RegexOptions.IgnoreCase))
                    File.AppendAllText(Path.Combine(root, "bank-replies.jsonl"), new KeyferJson().Serialize(new {
                        Server = server, Character = character, InstanceId = instance, Round = attempt.Round, Utc = config.UtcNow.ToString("O"), Raw = raw,
                        Verification = "candidate bank feedback, not parsed payment confirmation" }) + Environment.NewLine);
            }
            if (!TreasureReports.TryParse(raw, out string player, out int count)) return;
            lock (gate)
            {
                Load(server, character);
                var report = new TreasureReport { Id = Guid.NewGuid().ToString("N"), Server = server, Character = character, InstanceId = instance, ObservedUtc = config.UtcNow.ToString("O"), Raw = raw };
                // No content-based deduplication: identical +2 reports are distinct finds.
                File.AppendAllText(Path.Combine(root, config.UtcNow.ToString("yyyy-MM-dd") + ".jsonl"), new KeyferJson().Serialize(report) + Environment.NewLine);
                outbox.Add(report); Save("outbox.json", outbox);
            }
        }
        internal void Tick(string instance, string server, string character, bool quiet, Action<string> send, Action<string> show, Action<string> say)
        {
            if (stopped || config.SessionToken.IsCancellationRequested || !config.treasureEnabled || !server.Equals("DreamWeave", StringComparison.OrdinalIgnoreCase)) return;
            lock (gate) Load(server, character);
            while (local.TryDequeue(out string message)) show("[ACAI2 Treasure] " + message);
            while (remarks.TryDequeue(out string remark)) if (!quiet) say(remark);
            while (claimed.TryDequeue(out TreasurePrize prize))
            {
                lock (gate)
                {
                    var entry = journal.Single(j => j.Round == prize.Round);
                    if (entry.Attempted) continue;
                    // Save before invoking the game parser. A crash in this interval is ambiguous, never auto-retry.
                    entry.Attempted = true; entry.State = "uncertain"; entry.SubmittedUtc = config.UtcNow.ToString("O"); Save("prizes.json", journal);
                    string command = TreasureReports.PrizeCommand(config.treasurePrizeCommand, prize.Winner);
                    if (command.Length == 0) { show("[ACAI2 Treasure] Prize command invalid. Payout held for review."); continue; }
                    try { send(command); entry.State = "submitted"; }
                    catch { show("[ACAI2 Treasure] Prize dispatch uncertain; automatic retry suppressed."); }
                    Save("prizes.json", journal);
                    show("[ACAI2 Treasure] " + prize.Round + ": " + prize.Winner + ", prize command " + entry.State + ". This is not bank confirmation.");
                }
            }
            lock (gate)
            {
                foreach (var entry in journal.Where(j => !j.Announced && !quiet && j.State != "claiming"))
                {
                    // Announce winner, never claim a bank transfer succeeded without server confirmation.
                    say("Treasure round " + entry.Round + ": " + entry.Winner + " takes the crown with " + entry.Total + " recorded doubloons! One MMD prize " +
                        (entry.State == "submitted" ? "transfer requested." : "transfer pending."));
                    entry.Announced = true; Save("prizes.json", journal);
                }
            }
            if (config.UtcNow < nextPoll || Interlocked.CompareExchange(ref polling, 1, 0) != 0) return;
            nextPoll = config.UtcNow.AddSeconds(5);
            Task.Run(async () =>
            {
                try
                {
                    TreasureReport[] reports; bool backlog;
                    lock (gate) { reports = outbox.Take(64).ToArray(); backlog = outbox.Count > reports.Length; }
                    var reply = await Post<TreasureReply>("sync", new TreasureSync { InstanceId = instance, Server = server, Character = character, Quiet = quiet, Backlog = backlog, Reports = reports }).ConfigureAwait(false);
                    lock (gate)
                    {
                        latest = reply; latestUtc = config.UtcNow;
                        var accepted = new HashSet<string>(reply.AcceptedIds ?? new string[0]);
                        outbox.RemoveAll(r => accepted.Contains(r.Id)); Save("outbox.json", outbox);
                    }
                    foreach (string warning in reply.Warnings ?? new string[0]) local.Enqueue(warning);
                    if (!string.IsNullOrWhiteSpace(reply.Notice)) remarks.Enqueue(reply.Notice);
                    if (reply.Prize != null && !stopped && !config.SessionToken.IsCancellationRequested)
                    {
                        bool attempt;
                        lock (gate)
                        {
                            var entry = journal.FirstOrDefault(j => j.Round == reply.Prize.Round);
                            if (entry == null)
                            {
                                entry = new PrizeJournal { Round = reply.Prize.Round, Winner = reply.Prize.Winner, Total = reply.Prize.Total };
                                journal.Add(entry); Save("prizes.json", journal);
                            }
                            attempt = !entry.Attempted;
                            if (attempt) { entry.State = "claiming"; Save("prizes.json", journal); }
                        }
                        string command = TreasureReports.PrizeCommand(config.treasurePrizeCommand, reply.Prize.Winner);
                        if (command.Length == 0)
                        {
                            lock (gate) { journal.Single(j => j.Round == reply.Prize.Round).State = "pending"; Save("prizes.json", journal); }
                            if (!warnedCommand) { warnedCommand = true; local.Enqueue("Winner recorded. Automatic payment needs the exact one-MMD bank command configured; no money command was sent."); }
                        }
                        else if (attempt)
                        {
                            try
                            {
                                var response = await Post<ClaimResult>("claim", new TreasureAction { InstanceId = instance, Server = server, Character = character, Round = reply.Prize.Round }).ConfigureAwait(false);
                                if (response.claimed) claimed.Enqueue(reply.Prize);
                                else
                                {
                                    lock (gate) { var entry = journal.Single(j => j.Round == reply.Prize.Round); entry.Attempted = true; entry.State = "uncertain"; entry.Reported = true; Save("prizes.json", journal); }
                                    local.Enqueue("Prize already claimed or dispatch uncertain; no automatic duplicate transfer.");
                                }
                            }
                            catch
                            {
                                lock (gate) { var entry = journal.Single(j => j.Round == reply.Prize.Round); entry.Attempted = true; entry.State = "uncertain"; Save("prizes.json", journal); }
                                throw;
                            }
                        }
                    }
                    PrizeJournal[] results;
                    lock (gate) results = journal.Where(j => j.Attempted && !j.Reported).ToArray();
                    foreach (var entry in results)
                    {
                        await Post<object>("result", new TreasureAction { InstanceId = instance, Server = server, Character = character, Round = entry.Round, State = entry.State }).ConfigureAwait(false);
                        lock (gate) { entry.Reported = true; Save("prizes.json", journal); }
                    }
                }
                catch (Exception ex) { local.Enqueue("Sync pending: " + ex.GetType().Name + ". Captured reports remain in the local outbox."); nextPoll = config.UtcNow.AddSeconds(30); }
                finally { Interlocked.Exchange(ref polling, 0); }
            });
        }
        private async Task<T> Post<T>(string action, object payload)
        {
            string json = new KeyferJson().Serialize(payload);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await http.PostAsync(config.bridgeUrl.TrimEnd('/') + "/v1/keyfer/treasure/" + action, content, config.SessionToken).ConfigureAwait(false))
            {
                config.SessionToken.ThrowIfCancellationRequested();
                response.EnsureSuccessStatusCode();
                return new KeyferJson().Deserialize<T>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
        }
        internal string Status(bool standings)
        {
            lock (gate)
            {
                string prefix = "Treasure " + (config.treasureEnabled ? "ON" : "OFF") + " | pending reports " + outbox.Count + " | payout command " +
                    (TreasureReports.PrizeCommand(config.treasurePrizeCommand, "Test") == "" ? "DISABLED" : "one MMD") + " | cutoff 6pm Eastern";
                if (!standings) return prefix + " | last prize " + (journal.Count == 0 ? "none" : journal.Last().Round + " " + journal.Last().Winner + " " + journal.Last().State);
                if (latest == null || config.UtcNow - latestUtc > TimeSpan.FromSeconds(60)) return prefix + " | awaiting fresh Bridge standings";
                return "Recorded round " + latest.Round + ": " + (latest.Standings == null || latest.Standings.Length == 0 ? "no finds yet" :
                    string.Join("; ", latest.Standings.Select(s => "#" + s.Rank + " " + s.Player + " " + s.Total + " (best " + s.PersonalBest + ")")));
            }
        }
        public void Dispose() { stopped = true; http.Dispose(); }
        public sealed class ClaimResult { public bool claimed { get; set; } }
        public sealed class PrizeJournal
        {
            public string Round { get; set; } = "";
            public string Winner { get; set; } = "";
            public long Total { get; set; }
            public string SubmittedUtc { get; set; } = "";
            public bool Attempted { get; set; }
            public bool Reported { get; set; }
            public bool Announced { get; set; }
            public string State { get; set; } = "pending";
        }
    }
}
