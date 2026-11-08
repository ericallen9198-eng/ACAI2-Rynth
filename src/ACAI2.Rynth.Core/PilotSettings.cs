using System.Net;
using System.Text.Json;

namespace ACAI2.Rynth.Core;

public sealed record PilotSettings
{
    public bool BridgeEnabled { get; init; }
    public string BridgeUrl { get; init; } = "http://127.0.0.1:5052";
    public bool CapturePublicChat { get; init; }
    public int MaxChatRecords { get; init; } = 128;
    public int MaxObjects { get; init; } = 128;
    public int TelemetrySeconds { get; init; } = 10;
    [System.Text.Json.Serialization.JsonConstructor]
    public PilotSettings(bool bridgeEnabled = false, string bridgeUrl = "http://127.0.0.1:5052", bool capturePublicChat = false,
        int maxChatRecords = 128, int maxObjects = 128, int telemetrySeconds = 10)
    {
        BridgeEnabled = bridgeEnabled; BridgeUrl = bridgeUrl; CapturePublicChat = capturePublicChat;
        MaxChatRecords = maxChatRecords; MaxObjects = maxObjects; TelemetrySeconds = telemetrySeconds;
    }
    public void Validate()
    {
        _ = BridgeAddress();
        if (MaxChatRecords is < 1 or > 256 || MaxObjects is < 1 or > 256 || TelemetrySeconds is < 5 or > 300)
            throw new InvalidDataException("Pilot bounds: chat/objects 1..256; telemetry 5..300 seconds.");
    }
    public Uri BridgeAddress()
    {
        if (!Uri.TryCreate(BridgeUrl, UriKind.Absolute, out var uri) || uri.Scheme != "http" || uri.UserInfo.Length != 0 ||
            uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            !(uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip)))
            throw new InvalidDataException("BridgeUrl must be a loopback HTTP root, such as http://127.0.0.1:5052.");
        return uri;
    }
    public static PilotSettings Load(string path)
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 16384) throw new InvalidDataException("Pilot settings exceed 16 KiB.");
        var value = JsonSerializer.Deserialize(File.ReadAllText(path), PilotJson.Default.PilotSettings) ?? throw new InvalidDataException("Empty pilot settings.");
        value.Validate(); return value;
    }
}
