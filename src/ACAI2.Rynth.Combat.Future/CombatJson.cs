using System.Text.Json.Serialization;
namespace ACAI2.Rynth.Core;
[JsonSourceGenerationOptions(WriteIndented=true,PropertyNameCaseInsensitive=true)]
[JsonSerializable(typeof(CombatAppearance))]
public partial class CombatJson : JsonSerializerContext;
