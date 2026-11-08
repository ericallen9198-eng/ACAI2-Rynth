using System.Text.RegularExpressions;

namespace ACAI2.Rynth.Core;

// Managed observation state only: no game action or AI interfaces exist here.
public sealed class PilotSession(PilotSettings settings) : IDisposable
{
    private readonly PilotSettings options = Checked(settings);
    private static PilotSettings Checked(PilotSettings value) { value.Validate(); return value; }
    private readonly object sync = new();
    private SessionLease? session;
    private readonly Queue<PublicChatRecord> chat = new();
    private ObservedObject[] objects = [];
    private long chatEvents, objectEvents;
    private int dropped;
    public SessionLease? Current { get { lock (sync) return session; } }
    public SessionLease Begin(GameIdentity identity, DateTime now)
    {
        lock (sync)
        {
            End(); session = new SessionLease(identity, now); return session;
        }
    }
    public void End()
    {
        lock (sync)
        {
            session?.Dispose(); session = null; objects = []; chat.Clear(); chatEvents = objectEvents = dropped = 0;
        }
    }
    public void ObserveObject(SessionLease lease)
    { lock (sync) if (ReferenceEquals(lease, session) && !lease.Token.IsCancellationRequested) objectEvents++; }
    public void ObserveChat(SessionLease lease, string? raw, int nativeType, DateTime now)
    {
        lock (sync)
        {
            if (!ReferenceEquals(lease, session) || lease.Token.IsCancellationRequested) return;
            chatEvents++;
            if (!options.CapturePublicChat || string.IsNullOrWhiteSpace(raw) || raw.Length > 4096) return;
            // Anchored channel labels only. Tells/local/unknown formats remain unrecorded.
            var match = Regex.Match(raw, @"^\s*\[(General|Allegiance)\]\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            if (!match.Success) return;
            string channel = match.Groups[1].Value.Equals("General", StringComparison.OrdinalIgnoreCase) ? "General" : "Allegiance";
            string clean = new(raw.Where(c => !char.IsControl(c)).ToArray());
            if (chat.Count >= options.MaxChatRecords) { chat.Dequeue(); dropped++; }
            chat.Enqueue(new(now, nativeType, channel, clean));
        }
    }
    public void SetObjects(SessionLease lease, IEnumerable<ObservedObject> source)
    {
        var values = source.Where(x => x.Id != 0 && !string.IsNullOrWhiteSpace(x.Name) && x.Name.Length <= 256 &&
            x.CellId != 0 && float.IsFinite(x.X) && float.IsFinite(x.Y) && float.IsFinite(x.Z))
            .DistinctBy(x => x.Id).Take(options.MaxObjects).ToArray();
        lock (sync) if (ReferenceEquals(lease, session) && !lease.Token.IsCancellationRequested) objects = values;
    }
    public TelemetryFrame? Capture(DateTime now)
    {
        lock (sync) return session == null ? null : new(session, new(session.InstanceId, session.Identity, session.StartedUtc, now,
            chatEvents, objectEvents, dropped, objects.ToArray(), chat.ToArray()));
    }
    public void Dispose() => End();
}
