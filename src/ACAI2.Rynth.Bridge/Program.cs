using System.Net;
using System.Text;
using System.Text.Json;
using ACAI2.Protocol;

namespace ACAI2.Bridge;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly ClientRegistry Registry = new();
    private static BridgeConfig _config = new();
    private static readonly HttpClient AiHttp = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly KeyferOptions KeyferConfig = new();
    private static readonly KeyferService Keyfer = new(KeyferConfig, Registry, AiHttp);

    public static async Task Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "--arena-preview" || args[0] == "--arena-preview-files"))
        {
            try { await ArenaPreview.RunAsync(args, KeyferConfig); }
            catch (Exception ex) { Console.Error.WriteLine("Arena preview failed: " + ex.Message); Environment.ExitCode = 1; }
            return;
        }
        Console.Title = "ACAI2.Bridge v0.18";
        _config = BridgeConfig.Load();

        using var listener = new HttpListener();
        listener.Prefixes.Add(_config.ListenPrefix);

        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine("ACAI2 Bridge could not start.");
            Console.WriteLine(ex.Message);
            Console.WriteLine("Expected listener: " + _config.ListenPrefix);
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
            return;
        }

        Console.WriteLine("ACAI2 Bridge v0.18");
        Console.WriteLine("Listening: " + _config.ListenPrefix);
        Console.WriteLine("Protocol: " + ProtocolInfo.Version);
        Console.WriteLine("Keyfer: " + KeyferConfig.Server + "::" + KeyferConfig.Character);
        Console.WriteLine("Purpose: identity, perception, Keyfer conversations, wiki lookup, and event observations");
        Console.WriteLine("Player memory: " + (string.Equals(Environment.GetEnvironmentVariable("ACAI2_MEMORY_ENABLED"), "false", StringComparison.OrdinalIgnoreCase) ? "OFF" : "automatic SQLite notes; review in background"));
        Console.WriteLine("Treasure: observed allegiance reports; 6pm Eastern rounds; fixed one-MMD dispatch on the Keyfer client.");
        Console.WriteLine("Chat: friendly default; persistent 5m/30m/24h timeouts; friendly General conversations move to tells.");
        Console.WriteLine("Arena: Allegiance " + (!string.Equals(Environment.GetEnvironmentVariable("ACAI2_ARENA_ALLEGIANCE_ENABLED"), "false", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF") + "; General " + (string.Equals(Environment.GetEnvironmentVariable("ACAI2_ARENA_GENERAL_ENABLED"), "true", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF for testing") + "; private previews available without AI.");
        Console.WriteLine("Leaderboards: fixed read-only /top commands; daily refresh while Keyfer is online and on-demand native capture.");
        Console.WriteLine("All ACAI2 client and Discord requests share this listener.");
        Console.WriteLine();

        Timer? printTimer = null;
        if (_config.RegistryPrintSeconds > 0)
        {
            printTimer = new Timer(_ => PrintRegistry(), null,
                TimeSpan.FromSeconds(_config.RegistryPrintSeconds),
                TimeSpan.FromSeconds(_config.RegistryPrintSeconds));
        }

        try
        {
            while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (HttpListenerException) when (!listener.IsListening)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

                _ = Task.Run(() => HandleAsync(context));
            }
        }
        finally
        {
            printTimer?.Dispose();
        }
    }

    private static async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? "";
            var method = context.Request.HttpMethod.ToUpperInvariant();

            if (method == "POST" && path.Equals("/v1/keyfer/ask", StringComparison.OrdinalIgnoreCase))
            {
                var ask = await ReadJsonAsync<KeyferAsk>(context.Request);
                if (ask == null) throw new ArgumentException("Missing conversation request.");
                var answer = await Keyfer.AskAsync(ask);
                await WriteJsonAsync(context, answer.Ok ? 200 : 503, answer);
                return;
            }

            if (method == "POST" && path.Equals("/v1/keyfer/arena/complete", StringComparison.OrdinalIgnoreCase))
            {
                var source = await ReadJsonAsync<ArenaCompletion>(context.Request) ?? throw new ArgumentException("Missing arena completion.");
                await WriteJsonAsync(context, 200, new { completed = await Keyfer.CompleteArenaAsync(source) }); return;
            }

            if (method == "POST" && path.Equals("/v1/keyfer/treasure/sync", StringComparison.OrdinalIgnoreCase))
            {
                var sync = await ReadJsonAsync<TreasureSync>(context.Request) ?? throw new ArgumentException("Missing treasure sync.");
                await WriteJsonAsync(context, 200, await Keyfer.SyncTreasureAsync(sync)); return;
            }
            if (method == "POST" && path.Equals("/v1/keyfer/treasure/claim", StringComparison.OrdinalIgnoreCase))
            {
                var action = await ReadJsonAsync<TreasureAction>(context.Request) ?? throw new ArgumentException("Missing treasure claim.");
                await WriteJsonAsync(context, 200, new { claimed = Keyfer.ClaimTreasure(action) }); return;
            }
            if (method == "POST" && path.Equals("/v1/keyfer/treasure/result", StringComparison.OrdinalIgnoreCase))
            {
                var action = await ReadJsonAsync<TreasureAction>(context.Request) ?? throw new ArgumentException("Missing prize result.");
                Keyfer.TreasureResult(action); await WriteAckAsync(context, "prize command outcome recorded"); return;
            }

            if (method == "POST" && path.Equals("/v1/keyfer/leaderboards/poll", StringComparison.OrdinalIgnoreCase))
            {
                var source = await ReadJsonAsync<LeaderboardRequest>(context.Request) ?? throw new ArgumentException("Missing leaderboard source.");
                await WriteJsonAsync(context, 200, Keyfer.PollLeaderboards(source)); return;
            }
            if (method == "POST" && path.Equals("/v1/keyfer/leaderboards/result", StringComparison.OrdinalIgnoreCase))
            {
                var result = await ReadJsonAsync<LeaderboardResult>(context.Request) ?? throw new ArgumentException("Missing leaderboard result.");
                Keyfer.ReceiveLeaderboard(result); await WriteAckAsync(context, "leaderboard captured"); return;
            }

            if (method == "POST" && path.Equals("/v1/keyfer/world-event", StringComparison.OrdinalIgnoreCase))
            {
                var observation = await ReadJsonAsync<DreamWeaveEvent>(context.Request);
                if (observation == null) throw new ArgumentException("Missing world-event observation.");
                Keyfer.ObserveWorldEvent(observation);
                await WriteAckAsync(context, "world event observed");
                return;
            }

            if (method == "POST" && path.Equals("/v1/keyfer/chat", StringComparison.OrdinalIgnoreCase))
            {
                var chat = await ReadJsonAsync<ChatObservation>(context.Request);
                if (chat == null) throw new ArgumentException("Missing chat observation.");
                Keyfer.Observe(chat);
                await WriteAckAsync(context, "observed");
                return;
            }

            if (method == "GET" && (path == "" || path == "/health"))
            {
                await WriteJsonAsync(context, 200, new
                {
                    ok = true,
                    service = "ACAI2.Bridge",
                    phase = 2,
                    version = "0.18",
                    protocol = ProtocolInfo.Version,
                    utc = DateTime.UtcNow.ToString("O")
                });
                return;
            }

            if (method == "GET" && path.Equals("/v1/clients", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context, 200, new
                {
                    utc = DateTime.UtcNow.ToString("O"),
                    staleAfterSeconds = _config.ClientStaleSeconds,
                    clients = Registry.Snapshot(_config.ClientStaleSeconds)
                });
                return;
            }

            if (method == "POST" && path.Equals("/v1/client/register", StringComparison.OrdinalIgnoreCase))
            {
                var request = await ReadJsonAsync<ClientRegistration>(context.Request);
                if (!ValidIdentity(request?.InstanceId, request?.Server, request?.Character))
                {
                    await WriteErrorAsync(context, 400, "Registration missing instance/server/character.");
                    return;
                }

                var record = Registry.UpsertRegistration(request!);
                var announce = false;
                lock (record)
                {
                    if (!record.RegistrationAnnounced)
                    {
                        record.RegistrationAnnounced = true;
                        announce = true;
                    }
                }

                if (announce)
                {
                    Console.WriteLine($"[REGISTER] {record.RuntimeId}");
                    Console.WriteLine($"           Instance: {Short(record.InstanceId)} | Plugin: {record.PluginVersion}");
                }

                await WriteAckAsync(context, "registered");
                return;
            }

            if (method == "POST" && path.Equals("/v1/client/heartbeat", StringComparison.OrdinalIgnoreCase))
            {
                var request = await ReadJsonAsync<ClientHeartbeat>(context.Request);
                if (!ValidIdentity(request?.InstanceId, request?.Server, request?.Character))
                {
                    await WriteErrorAsync(context, 400, "Heartbeat missing instance/server/character.");
                    return;
                }

                Registry.UpsertHeartbeat(request!);
                await WriteAckAsync(context, "heartbeat");
                return;
            }

            if (method == "POST" && path.Equals("/v1/client/perception", StringComparison.OrdinalIgnoreCase))
            {
                var request = await ReadJsonAsync<WorldPerception>(context.Request);
                if (!ValidIdentity(request?.InstanceId, request?.Server, request?.Character))
                {
                    await WriteErrorAsync(context, 400, "Perception missing instance/server/character.");
                    return;
                }

                var before = Registry.Records.FirstOrDefault(x =>
                    x.InstanceId.Equals(request!.InstanceId, StringComparison.OrdinalIgnoreCase))?.LastPerceptionUtc;
                var record = Registry.UpsertPerception(request!);

                if (before == null)
                {
                    var p = record.Perception!;
                    Console.WriteLine($"[WORLD]    {record.RuntimeId}");
                    Console.WriteLine($"           Players {p.Players.Count} | NPCs {p.Npcs.Count} | Monsters {p.Monsters.Count} | Portals {p.Portals.Count}");
                }

                await WriteAckAsync(context, "perception");
                return;
            }

            await WriteErrorAsync(context, 404, "Unknown ACAI2 endpoint.");
        }
        catch (JsonException ex)
        {
            await WriteErrorAsync(context, 400, "Invalid JSON: " + ex.Message);
        }
        catch (ArgumentException ex)
        {
            await WriteErrorAsync(context, 400, ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ERROR] " + ex);
            await WriteErrorAsync(context, 500, "Bridge error.");
        }
    }

    private static void PrintRegistry()
    {
        var now = DateTime.UtcNow;
        var records = Registry.Records
            .OrderBy(x => x.Server)
            .ThenBy(x => x.Character)
            .ToList();

        if (records.Count == 0)
        {
            Console.WriteLine("[REGISTRY] No ACAI2 clients registered.");
            return;
        }

        Console.WriteLine("[REGISTRY]");
        foreach (var record in records)
        {
            lock (record)
            {
                var age = Math.Max(0, (now - record.LastSeenUtc).TotalSeconds);
                var online = age <= _config.ClientStaleSeconds ? "ONLINE" : "STALE";
                var perceptionAge = record.LastPerceptionUtc.HasValue
                    ? $"{Math.Max(0, (now - record.LastPerceptionUtc.Value).TotalSeconds):0}s"
                    : "none";

                Console.WriteLine($"  {record.RuntimeId} | {online} | heartbeat {age:0}s | world {perceptionAge}");
            }
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
        var buffer = new char[4096];
        var body = new StringBuilder();
        int count;
        while ((count = await reader.ReadAsync(buffer)) > 0)
        {
            if (body.Length + count > 262144) throw new ArgumentException("Request body is too large.");
            body.Append(buffer, 0, count);
        }
        return JsonSerializer.Deserialize<T>(body.ToString(), JsonOptions);
    }

    private static Task WriteAckAsync(HttpListenerContext context, string message) =>
        WriteJsonAsync(context, 200, new BridgeAck
        {
            Ok = true,
            Message = message,
            BridgeUtc = DateTime.UtcNow.ToString("O")
        });

    private static Task WriteErrorAsync(HttpListenerContext context, int status, string message) =>
        WriteJsonAsync(context, status, new BridgeAck
        {
            Ok = false,
            Message = message,
            BridgeUtc = DateTime.UtcNow.ToString("O")
        });

    private static async Task WriteJsonAsync(HttpListenerContext context, int status, object? value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static bool ValidIdentity(string? instanceId, string? server, string? character) =>
        !string.IsNullOrWhiteSpace(instanceId) &&
        !string.IsNullOrWhiteSpace(server) &&
        !string.IsNullOrWhiteSpace(character);

    private static string Short(string value) => value.Length <= 8 ? value : value.Substring(0, 8);
}
