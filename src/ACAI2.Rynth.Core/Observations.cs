namespace ACAI2.Rynth.Core;

public sealed record GameIdentity(string Server, string Character, uint PlayerId)
{
    public string RuntimeId => Server + "::" + Character;
    public void Validate()
    {
        if (PlayerId == 0 || string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Character) || Server.Length > 128 || Character.Length > 128 ||
            Server.Any(char.IsControl) || Character.Any(char.IsControl) || Server.Equals("UnknownServer", StringComparison.OrdinalIgnoreCase) || Character.Equals("UnknownCharacter", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A real server, character and nonzero player ID are required.");
    }
}
public sealed class SessionLease : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private int disposed;
    public GameIdentity Identity { get; }
    public string InstanceId { get; } = "rynth-" + Guid.NewGuid().ToString("N");
    public DateTime StartedUtc { get; }
    public CancellationToken Token { get; }
    public SessionLease(GameIdentity identity, DateTime now) { identity.Validate(); Identity = identity; StartedUtc = now; Token = cancellation.Token; }
    public void Dispose() { if (Interlocked.Exchange(ref disposed,1)==0) { cancellation.Cancel(); cancellation.Dispose(); } }
}
public sealed record ObservedObject(uint Id, string Name, uint CellId, float X, float Y, float Z, bool HasAppraisal, string Kind = "Object");
public sealed record PublicChatRecord(DateTime Utc, int NativeType, string Channel, string Text);
public sealed record PilotSnapshot(string InstanceId, GameIdentity Identity, DateTime StartedUtc, DateTime CapturedUtc,
    long ChatEvents, long ObjectEvents, int DroppedChatRecords, ObservedObject[] Objects, PublicChatRecord[] PublicChat);
public sealed record TelemetryFrame(SessionLease Session, PilotSnapshot Snapshot);
