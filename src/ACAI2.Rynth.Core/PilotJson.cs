using System.Text.Json.Serialization;
using ACAI2.Protocol;

namespace ACAI2.Rynth.Core;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PilotSettings))]
[JsonSerializable(typeof(PilotSnapshot))]
[JsonSerializable(typeof(BridgeAck))]
[JsonSerializable(typeof(ClientRegistration))]
[JsonSerializable(typeof(ClientHeartbeat))]
[JsonSerializable(typeof(WorldPerception))]
public partial class PilotJson : JsonSerializerContext;
