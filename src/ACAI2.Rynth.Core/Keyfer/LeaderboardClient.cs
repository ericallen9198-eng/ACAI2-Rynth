#nullable disable
// Adapter port of Samm-AI ff44cc3; no Decal dependency.
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ACAI2.Protocol;

namespace ACAI2.Rynth.Core.Keyfer
{
    // Commands are read-only fixed catalog entries. Dispatch and capture run on the game thread.
    internal sealed class LeaderboardClient : IDisposable
    {
        private sealed class Claim { public LeaderboardJob Job; public LeaderboardRequest Source; }
        private readonly KeyferSettings config;
        private readonly HttpClient http;
        private readonly BoundedQueue<Claim> ready = new BoundedQueue<Claim>();
        private readonly BoundedQueue<string> notices = new BoundedQueue<string>();
        private Claim active;
        private LeaderboardCapture capture;
        private int polling;
        private volatile bool stopped;
        private DateTime nextPoll, nextError, nextDispatch;
        internal LeaderboardClient(KeyferSettings config) { this.config = config; http=config.CreateHttp(); }
        internal void Observe(string raw, DateTime now) { if (!stopped && capture != null) capture.Observe(raw, now); }
        internal void Tick(string instance, string server, string character, Action<string> send, Action<string> local)
        {
            if (stopped || config.SessionToken.IsCancellationRequested || !server.Equals("DreamWeave", StringComparison.OrdinalIgnoreCase)) return;
            string notice; while (notices.TryDequeue(out notice)) { if (config.UtcNow >= nextError) { local(notice); nextError = config.UtcNow.AddMinutes(1); } }
            if (active != null && (active.Source.InstanceId != instance || active.Source.Server != server || active.Source.Character != character)) { active = null; capture = null; }
            if (active != null && capture.Complete(config.UtcNow))
            {
                var result = new LeaderboardResult { Server = server, Character = character, InstanceId = instance, JobId = active.Job.Id, Board = active.Job.Board,
                    ObservedUtc = config.UtcNow.ToString("O"), Lines = capture.Lines, Error = capture.Lines.Length < 2 ? "No recognized native leaderboard output" : "" };
                active = null; capture = null;
                Task.Run(async () => { try { await Post("/v1/keyfer/leaderboards/result", result); } catch { notices.Enqueue("[ACAI2] Leaderboard result sync unavailable; no fresh result assumed."); } });
            }
            Claim claim;
            while (active == null && config.UtcNow >= nextDispatch && ready.TryDequeue(out claim))
            {
                DateTime expires;
                if (claim.Source.InstanceId != instance || claim.Source.Server != server || claim.Source.Character != character ||
                    !DateTime.TryParse(claim.Job.ExpiresUtc, null, DateTimeStyles.RoundtripKind, out expires) || expires <= config.UtcNow ||
                    DreamWeaveLeaderboards.Command(claim.Job.Board) == null) continue;
                active = claim; capture = new LeaderboardCapture(claim.Job.Board, config.UtcNow);
                try { nextDispatch = config.UtcNow.AddSeconds(5); send(DreamWeaveLeaderboards.Command(claim.Job.Board)); }
                catch { active = null; capture = null; notices.Enqueue("[ACAI2] Could not dispatch leaderboard command."); }
            }
            if (active != null || !ready.IsEmpty || config.UtcNow < nextPoll || Interlocked.CompareExchange(ref polling, 1, 0) != 0) return;
            nextPoll = config.UtcNow.AddSeconds(2);
            var source = new LeaderboardRequest { Server = server, Character = character, InstanceId = instance };
            Task.Run(async () =>
            {
                try
                {
                    var body = await Post("/v1/keyfer/leaderboards/poll", source);
                    var job = new KeyferJson().Deserialize<LeaderboardJob>(body);
                    if (!stopped && job != null) ready.Enqueue(new Claim { Job = job, Source = source });
                }
                catch { notices.Enqueue("[ACAI2] Leaderboard Bridge unavailable; commands remain paused."); }
                finally { Interlocked.Exchange(ref polling, 0); }
            });
        }
        private async Task<string> Post(string path, object payload)
        {
            using (var body = new StringContent(new KeyferJson().Serialize(payload), Encoding.UTF8, "application/json"))
            using (var response = await http.PostAsync(config.bridgeUrl.TrimEnd('/') + path, body, config.SessionToken).ConfigureAwait(false))
            { response.EnsureSuccessStatusCode(); return await response.Content.ReadAsStringAsync().ConfigureAwait(false); }
        }
        public void Dispose() { stopped = true; http.Dispose(); }
    }
}
