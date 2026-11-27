using System.Net;
using System.Text;
using System.Text.Json;
using ACAI2.Bridge;
using ACAI2.Protocol;

internal static class ModelMigrationChecks
{
    internal static async Task Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "keyfer-model-" + Guid.NewGuid().ToString("N"));
        string[] keys = ["ACAI2_DATA_ROOT", "ACAI2_BEHAVIOR_PATH", "ACAI2_SERVER_BEHAVIOR_PATH",
            "ACAI2_KEYFER_SERVER", "ACAI2_KEYFER_CHARACTER", "ACAI2_OPENAI_MODEL", "OPENAI_API_KEY",
            "ACAI2_MEMORY_ENABLED", "ACAI2_ARENA_AI_COMMENTARY"];
        var old = keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        try
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "isolated-test-not-a-real-key");
            Environment.SetEnvironmentVariable("ACAI2_MEMORY_ENABLED", "false");
            Environment.SetEnvironmentVariable("ACAI2_ARENA_AI_COMMENTARY", "true");
            Environment.SetEnvironmentVariable("ACAI2_KEYFER_SERVER", "DreamWeave");
            Environment.SetEnvironmentVariable("ACAI2_KEYFER_CHARACTER", "Keyfer Sutherland");
            Environment.SetEnvironmentVariable("ACAI2_OPENAI_MODEL", null);
            check(new KeyferOptions().Model == "gpt-6-luna", "Bridge defaults to Luna without an environment override");
            foreach (string model in new[] { "gpt-6-luna", "gpt-5-mini" })
            {
                string data = Path.Combine(root, model);
                Directory.CreateDirectory(data);
                Environment.SetEnvironmentVariable("ACAI2_DATA_ROOT", data);
                Environment.SetEnvironmentVariable("ACAI2_BEHAVIOR_PATH", Path.Combine(data, "behavior.txt"));
                Environment.SetEnvironmentVariable("ACAI2_SERVER_BEHAVIOR_PATH", Path.Combine(data, "server.txt"));
                Environment.SetEnvironmentVariable("ACAI2_OPENAI_MODEL", model);
                File.WriteAllText(Path.Combine(data, "behavior.txt"), "ISOLATED_PERSONALITY_MARKER");
                var options = new KeyferOptions();
                check(options.Model == model, model + " explicit override is honored");
                var handler = new FakeModel();
                using var http = new HttpClient(handler);
                var registry = new ClientRegistry();
                registry.UpsertRegistration(new() { InstanceId = "model-test", Server = "DreamWeave",
                    Character = "Keyfer Sutherland", RuntimeId = "DreamWeave::Keyfer Sutherland", StartedUtc = DateTime.UtcNow.ToString("O") });
                var wiki = new FakeWiki();
                using var service = new KeyferService(options, registry, http, wiki);
                KeyferAsk Ask(string id, string text) => new() { InstanceId = "model-test", Server = "DreamWeave",
                    Character = "Keyfer Sutherland", Speaker = "Toe", SpeakerId = id, Channel = "Tell", Question = text };
                var chat = await service.AskAsync(Ask("chat", "Keyfer, hello there!"));
                check(chat.Ok && chat.Answer == "Good to see you.", model + " chat parses a provider answer");
                check(handler.Requests[0].GetProperty("messages").GetRawText().Contains("ISOLATED_PERSONALITY_MARKER"),
                    model + " uses the existing behavior file");
                handler.WikiNext = true;
                var reference = await service.AskAsync(Ask("wiki", "What is a Vanguard?"));
                check(reference.Ok && wiki.Calls == 1 && reference.Answer.Contains("https://wiki.acdreamweave.com/test"),
                    model + " wiki tool call and final response preserve verified source links");
                check(handler.Requests[1].GetProperty("tool_choice").GetString() == "required" &&
                    handler.Requests[2].GetProperty("messages").EnumerateArray().Any(m => m.GetProperty("role").GetString() == "tool") &&
                    !handler.Requests[2].TryGetProperty("tools", out _), model + " wiki uses one bounded tool round trip");
                var identity = new MemoryIdentity("dreamweave", "keyfer sutherland", "game", "toe", "tell");
                var store = new PlayerMemoryStore(data);
                store.Apply(identity, [new() { Action = "upsert", Key = "style", Kind = "preference", Text = "Enjoys archery",
                    Evidence = "I enjoy archery", Confidence = .9 }], ["I enjoy archery"], DateTime.UtcNow);
                using (var memory = new PlayerMemory(options, http))
                    await memory.LearnAsync(identity, ["I enjoy archery"]);
                check(store.Read(identity, DateTime.UtcNow).Any(n => n.Key == "style" && n.Text == "Enjoys archery"),
                    model + " existing SQLite memories survive model changes and a valid JSON review");
                check(handler.Requests[3].GetProperty("response_format").GetProperty("type").GetString() == "json_object",
                    model + " memory review retains JSON output enforcement");
                var match = new ArenaMatch { First = new() { Name = "Toe" }, Second = new() { Name = "Samm" },
                    Events = [new(0, "Toe", "Samm", "Hit", "Heavy Weapons", 20, 0)] };
                var commentary = await new ArenaAiCommentary(http, model).NarrateAsync(match, CancellationToken.None);
                check(match.CommentaryMode == "ai" && commentary.Length > 0,
                    model + " arena accepts valid structured commentary");
                string effort = model == "gpt-6-luna" ? "none" : "minimal";
                check(handler.Requests.Count == 5 && handler.Requests.All(r => r.GetProperty("model").GetString() == model &&
                    r.GetProperty("reasoning_effort").GetString() == effort),
                    model + " all actual chat/wiki/memory/arena payloads use compatible reasoning");
                check(handler.Requests.Select(r => r.GetProperty("max_completion_tokens").GetInt32()).SequenceEqual(new[] { 1200, 1200, 1200, 1000, 1400 }),
                    model + " preserves bounded output budgets across request paths");
                handler.Reject = true;
                using var diagnostics = new StringWriter();
                var originalOutput = Console.Out;
                bool chatFailed = false, memoryFailed = false, arenaFellBack = false;
                try
                {
                    Console.SetOut(diagnostics);
                    chatFailed = !(await service.AskAsync(Ask("rejected", "Keyfer, hello again!"))).Ok;
                    using var memory = new PlayerMemory(options, http);
                    try { await memory.LearnAsync(identity, ["I enjoy archery"]); }
                    catch (ProviderRequestException ex) { memoryFailed = ex.Diagnostic.Contains("insufficient_quota"); }
                    await new ArenaAiCommentary(http, model).NarrateAsync(match, CancellationToken.None);
                    arenaFellBack = match.CommentaryMode == "scripted";
                }
                finally { Console.SetOut(originalOutput); }
                check(chatFailed && memoryFailed && arenaFellBack,
                    model + " provider rejection fails chat and memory safely and retains scripted arena fallback");
                check(diagnostics.ToString().Contains("HTTP 429; code=insufficient_quota") &&
                    !diagnostics.ToString().Contains("PRIVATE_PROVIDER_TEXT"),
                    model + " actual request paths report sanitized provider rejection diagnostics");

            }
        }
        finally
        {
            foreach (string key in keys) Environment.SetEnvironmentVariable(key, old[key]);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class FakeModel : HttpMessageHandler
    {
        internal readonly List<JsonElement> Requests = [];
        internal bool WikiNext;
        internal bool Reject;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri?.AbsoluteUri != "https://api.openai.com/v1/chat/completions" ||
                request.Headers.Authorization?.Parameter != "isolated-test-not-a-real-key") throw new InvalidOperationException("Unexpected isolated request");
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var body = doc.RootElement.Clone(); Requests.Add(body);
            if (Reject) return new(HttpStatusCode.TooManyRequests) { Content = new StringContent(
                "{\"error\":{\"code\":\"insufficient_quota\",\"message\":\"PRIVATE_PROVIDER_TEXT\"}}") };
            object message;
            if (WikiNext)
            {
                WikiNext = false;
                message = new { role = "assistant", content = (string?)null, tool_calls = new[] { new { id = "test-wiki", type = "function",
                    function = new { name = "search_dreamweave_wiki", arguments = "{\"query\":\"Class Abilities Vanguard\"}" } } } };
            }
            else if (body.TryGetProperty("response_format", out _))
            {
                string user = body.GetProperty("messages")[1].GetProperty("content").GetString()!;
                using var data = JsonDocument.Parse(user);
                object result = data.RootElement.TryGetProperty("actions", out var actions)
                    ? new { actions = actions.EnumerateArray().Select(a => new { id = a.GetProperty("id").GetString(),
                        text = a.GetProperty("actor").GetString() + " lands a hit" }).ToArray() }
                    : (object)new { updates = Array.Empty<object>() };
                message = new { role = "assistant", content = JsonSerializer.Serialize(result) };
            }
            else message = new { role = "assistant", content = "Good to see you." };
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message } } }), Encoding.UTF8, "application/json") };
        }
    }
    private sealed class FakeWiki : IDreamWeaveWiki
    {
        internal int Calls;
        public Task<WikiResult> SearchAsync(string query, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new WikiResult { Status = "Found", Pages = [new() { Title = "Test class", Url = "https://wiki.acdreamweave.com/test", Text = "Synthetic reference fixture." }] });
        }
    }
}
