using System.Collections.Concurrent;
using ACAI2.Protocol;

namespace ACAI2.Bridge;

internal sealed class ClientRecord
{
    public string InstanceId { get; init; } = "";
    public string Server { get; set; } = "UnknownServer";
    public string Character { get; set; } = "UnknownCharacter";
    public string RuntimeId { get; set; } = "UnknownServer::UnknownCharacter";
    public string PluginVersion { get; set; } = "";
    public DateTime StartedUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public DateTime? LastPerceptionUtc { get; set; }
    public WorldPerception? Perception { get; set; }
    public bool RegistrationAnnounced { get; set; }
}

internal sealed class ClientRegistry
{
    private readonly ConcurrentDictionary<string, ClientRecord> _clients =
        new(StringComparer.OrdinalIgnoreCase);

    public ClientRecord UpsertRegistration(ClientRegistration request)
    {
        var now = DateTime.UtcNow;
        var record = _clients.GetOrAdd(request.InstanceId, id => new ClientRecord
        {
            InstanceId = id,
            StartedUtc = ParseUtc(request.StartedUtc) ?? now
        });

        lock (record)
        {
            record.Server = Clean(request.Server, "UnknownServer");
            record.Character = Clean(request.Character, "UnknownCharacter");
            record.RuntimeId = Clean(request.RuntimeId, record.Server + "::" + record.Character);
            record.PluginVersion = Clean(request.PluginVersion, "unknown");
            record.LastSeenUtc = now;
        }

        return record;
    }

    public ClientRecord UpsertHeartbeat(ClientHeartbeat request)
    {
        var now = DateTime.UtcNow;
        var record = _clients.GetOrAdd(request.InstanceId, id => new ClientRecord
        {
            InstanceId = id,
            StartedUtc = now
        });

        lock (record)
        {
            record.Server = Clean(request.Server, record.Server);
            record.Character = Clean(request.Character, record.Character);
            record.RuntimeId = Clean(request.RuntimeId, record.Server + "::" + record.Character);
            record.LastSeenUtc = now;
        }

        return record;
    }

    public ClientRecord UpsertPerception(WorldPerception request)
    {
        var now = DateTime.UtcNow;
        var record = _clients.GetOrAdd(request.InstanceId, id => new ClientRecord
        {
            InstanceId = id,
            StartedUtc = now
        });

        lock (record)
        {
            record.Server = Clean(request.Server, record.Server);
            record.Character = Clean(request.Character, record.Character);
            record.RuntimeId = Clean(request.RuntimeId, record.Server + "::" + record.Character);
            record.LastSeenUtc = now;
            record.LastPerceptionUtc = ParseUtc(request.CapturedUtc) ?? now;
            record.Perception = request;
        }

        return record;
    }

    public object Snapshot(int staleSeconds)
    {
        var now = DateTime.UtcNow;

        return _clients.Values
            .OrderBy(c => c.Server)
            .ThenBy(c => c.Character)
            .Select(c =>
            {
                lock (c)
                {
                    var age = Math.Max(0, (now - c.LastSeenUtc).TotalSeconds);
                    return new
                    {
                        c.InstanceId,
                        c.RuntimeId,
                        c.Server,
                        c.Character,
                        c.PluginVersion,
                        StartedUtc = c.StartedUtc.ToString("O"),
                        LastSeenUtc = c.LastSeenUtc.ToString("O"),
                        LastSeenAgeSeconds = Math.Round(age, 1),
                        Online = age <= staleSeconds,
                        LastPerceptionUtc = c.LastPerceptionUtc?.ToString("O"),
                        Counts = c.Perception == null ? null : new
                        {
                            Players = c.Perception.Players?.Count ?? 0,
                            Npcs = c.Perception.Npcs?.Count ?? 0,
                            Monsters = c.Perception.Monsters?.Count ?? 0,
                            Portals = c.Perception.Portals?.Count ?? 0,
                            Containers = c.Perception.Containers?.Count ?? 0,
                            Objects = c.Perception.Objects?.Count ?? 0
                        },
                        Perception = c.Perception
                    };
                }
            })
            .ToArray();
    }

    public IEnumerable<ClientRecord> Records => _clients.Values;

    private static string Clean(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static DateTime? ParseUtc(string? value)
    {
        if (DateTime.TryParse(value, null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed))
            return parsed.ToUniversalTime();

        return null;
    }
}
