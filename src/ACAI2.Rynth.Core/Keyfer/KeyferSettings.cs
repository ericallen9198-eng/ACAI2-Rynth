using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ACAI2.Rynth.Core.Keyfer;

public sealed class KeyferSettings
{
    public bool keyferEnabled { get; set; }
    public string bridgeUrl { get; set; } = "http://127.0.0.1:5052";
    public string keyferServer { get; set; } = "DreamWeave";
    public string keyferCharacter { get; set; } = "Keyfer Sutherland";
    public bool keyferPublicReplies { get; set; } = true;
    public bool keyferWorldEvents { get; set; } = true;
    public bool treasureEnabled { get; set; } = true;
    // Separate opt-in for transfers: the existing Bridge ledger is preserved.
    public string treasurePrizeCommand { get; set; } = "";
    public string[] keyferControllers { get; set; } = ["Samm"];
    [JsonIgnore] public string DataRoot { get; set; } = "";
    [JsonIgnore] public string InstanceId { get; set; } = "";
    [JsonIgnore] public CancellationToken SessionToken { get; set; }
    [JsonIgnore] public Func<DateTime> Clock {get;set;} = ()=>DateTime.UtcNow;
    [JsonIgnore] public DateTime UtcNow=>Clock();
    [JsonIgnore] public Func<HttpMessageHandler>? HandlerFactory { get; set; }
    internal HttpClient CreateHttp() => new(HandlerFactory?.Invoke() ?? new HttpClientHandler { AllowAutoRedirect=false }) { Timeout=TimeSpan.FromSeconds(75) };
    public bool Matches(GameIdentity identity) => keyferEnabled && keyferServer.Equals(identity.Server,StringComparison.OrdinalIgnoreCase) && keyferCharacter.Equals(identity.Character,StringComparison.OrdinalIgnoreCase);
    public void Validate()
    {
        new PilotSettings { BridgeUrl=bridgeUrl }.Validate();
        new GameIdentity(keyferServer,keyferCharacter,1).Validate();
        if(keyferControllers==null||keyferControllers.Length>32||keyferControllers.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>128||x.Any(char.IsControl)))throw new InvalidDataException("Invalid Keyfer controllers.");
        if(treasurePrizeCommand.Length>0 && ACAI2.Protocol.TreasureReports.PrizeCommand(treasurePrizeCommand,"Test")=="")throw new InvalidDataException("Use the exact supported one-MMD prize template or leave it empty.");
    }
    public static KeyferSettings Load(string path)
    {
        if(!File.Exists(path))return new();
        if(new FileInfo(path).Length>16384)throw new InvalidDataException("Keyfer settings exceed 16 KiB.");
        var value=JsonSerializer.Deserialize<KeyferSettings>(File.ReadAllText(path),KeyferJson.Options)??throw new InvalidDataException("Empty Keyfer settings.");
        value.Validate();return value;
    }
}
internal sealed class KeyferJson
{
    internal static readonly JsonSerializerOptions Options=new(){PropertyNameCaseInsensitive=true};
    public int MaxJsonLength {get;set;}=2*1024*1024;
    public string Serialize(object value)=>JsonSerializer.Serialize(value,Options);
    public T Deserialize<T>(string text)
    {if(text.Length>MaxJsonLength)throw new InvalidDataException("Keyfer JSON response too large.");return JsonSerializer.Deserialize<T>(text,Options)!;}
}
// Same interface as the old adapters, with bounded pending delivery memory.
internal sealed class BoundedQueue<T>
{
    private readonly ConcurrentQueue<T> queue=new();
    private int count;
    public bool IsEmpty=>queue.IsEmpty;
    public void Enqueue(T item)
    {
        if(Interlocked.Increment(ref count)>256){Interlocked.Decrement(ref count);return;}
        queue.Enqueue(item);
    }
    public bool TryDequeue(out T item)
    {if(queue.TryDequeue(out item!)){Interlocked.Decrement(ref count);return true;}return false;}
}
