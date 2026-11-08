using System.Threading.Channels;

namespace ACAI2.Rynth.Core;

// Native callbacks never await HTTP. At most one pending immutable frame is retained.
public sealed class BridgeWorker : IAsyncDisposable
{
    private readonly PilotSettings settings;
    private readonly ReadOnlyBridgeClient client;
    private readonly CancellationTokenSource stop = new();
    private readonly Channel<TelemetryFrame> frames = Channel.CreateBounded<TelemetryFrame>(new BoundedChannelOptions(1) {
        FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false });
    private readonly Task runner;
    private string status = "Bridge disabled";
    private int stopped;
    public string Status => Volatile.Read(ref status);
    public BridgeWorker(PilotSettings settings, HttpMessageHandler? handler = null)
    {
        this.settings = settings; client = new(settings, handler);
        if (settings.BridgeEnabled) status = "Bridge waiting for identity";
        runner = Task.Run(RunAsync);
    }
    public bool Submit(TelemetryFrame frame) => settings.BridgeEnabled && !frame.Session.Token.IsCancellationRequested && Volatile.Read(ref stopped) == 0 && frames.Writer.TryWrite(frame);
    private async Task RunAsync()
    {
        try
        {
            await foreach (var frame in frames.Reader.ReadAllAsync(stop.Token))
            {
                if (frame.Session.Token.IsCancellationRequested) continue;
                try
                {
                    if (await client.SendAsync(frame, stop.Token) && !frame.Session.Token.IsCancellationRequested) Volatile.Write(ref status, "Bridge telemetry OK");
                }
                catch (OperationCanceledException) { if (!stop.IsCancellationRequested) Volatile.Write(ref status, "Bridge frame expired"); }
                catch (HttpRequestException) { Volatile.Write(ref status, "Bridge unavailable; observations continue locally"); }
                catch (Exception ex) { Volatile.Write(ref status, "Bridge telemetry rejected: " + ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) { }
        finally { client.Dispose(); }
    }
    public void Stop()
    {
        if (Interlocked.Exchange(ref stopped, 1) != 0) return;
        stop.Cancel(); frames.Writer.TryComplete();
    }
    public async ValueTask DisposeAsync() { Stop(); await runner; stop.Dispose(); }
}
