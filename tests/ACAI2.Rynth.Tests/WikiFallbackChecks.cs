using System.Net;
using System.Text;
using System.Text.Json;
using ACAI2.Bridge;
using ACAI2.Protocol;

internal static class WikiFallbackChecks
{
    internal static async Task Run(Action<bool, string> check)
    {
        foreach (string personal in new[] { "what is your favorite weapon lately?", "your favourite weapon?",
            "Keyfer you been looting chests you dont remember any of the weapons you pulleD?",
            "What weapons have you pulled?", "Have we kept any weapons?", "Are you still pulling chests?" })
            check(KeyferService.UsesOwnContext(personal), "personal preference or own-loot question selects observation context");
        foreach (string reference in new[] { "What is a Vanguard?", "How do I loot a chest?", "Can you explain weapon damage?",
            "What does Legendary Defender do?", "How can I get a Harbinger wand?" })
            check(!KeyferService.UsesOwnContext(reference), "game-reference question retains wiki eligibility");
        string root = Path.Combine(Path.GetTempPath(), "keyfer-wiki-fallback-" + Guid.NewGuid().ToString("N"));
        string[] keys = ["ACAI2_DATA_ROOT", "ACAI2_BEHAVIOR_PATH", "ACAI2_SERVER_BEHAVIOR_PATH", "ACAI2_KEYFER_SERVER",
            "ACAI2_KEYFER_CHARACTER", "OPENAI_API_KEY", "ACAI2_MEMORY_ENABLED"];
        var old = keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
        try
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("ACAI2_DATA_ROOT", root);
            Environment.SetEnvironmentVariable("ACAI2_BEHAVIOR_PATH", Path.Combine(root, "behavior.txt"));
            Environment.SetEnvironmentVariable("ACAI2_SERVER_BEHAVIOR_PATH", Path.Combine(root, "server.txt"));
            Environment.SetEnvironmentVariable("ACAI2_KEYFER_SERVER", "DreamWeave");
            Environment.SetEnvironmentVariable("ACAI2_KEYFER_CHARACTER", "Keyfer Sutherland");
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "isolated-test-not-a-real-key");
            Environment.SetEnvironmentVariable("ACAI2_MEMORY_ENABLED", "false");
            var registry = new ClientRegistry();
            registry.UpsertRegistration(new() { InstanceId="wiki-test", Server="DreamWeave", Character="Keyfer Sutherland",
                RuntimeId="DreamWeave::Keyfer Sutherland", StartedUtc=DateTime.UtcNow.ToString("O") });
            var model = new FakeModel(); using var http = new HttpClient(model);
            var wiki = new EmptyWiki();
            using var service = new KeyferService(new(), registry, http, wiki);
            KeyferAsk Ask(string id, string question) => new() { InstanceId="wiki-test", Server="DreamWeave", Character="Keyfer Sutherland",
                Speaker="Toe", SpeakerId=id, Channel="Tell", Question=question };
            var optional = await service.AskAsync(Ask("optional", "Keyfer, how has the market been?"));
            check(optional.Ok && optional.Answer == "I haven't observed enough to say yet." && wiki.Calls == 1 && model.Requests.Count == 2,
                "empty optional wiki lookup receives one conversational continuation instead of the fixed referral");
            check(!model.Requests[1].TryGetProperty("tools", out _) && model.Requests[1].GetRawText().Contains("optional wiki lookup returned no usable evidence"),
                "optional wiki continuation disables more tool calls and explicitly forbids inventing missing facts");
            var mandatory = await service.AskAsync(Ask("required", "What is a Vanguard?"));
            check(mandatory.Ok && mandatory.Answer == KeyferService.WikiFallback && model.Requests.Count == 3 && wiki.Calls == 2,
                "required game-reference lookup keeps the evidence gate when the wiki is unavailable");
            model.Direct = true;
            var missing = await service.AskAsync(Ask("personal-missing", "What is your favorite weapon lately?"));
            var last = model.Requests.Last();
            check(missing.Ok && missing.Answer != KeyferService.WikiFallback && !last.TryGetProperty("tools", out _) &&
                last.GetRawText().Contains("No own-loot observations supplied") && last.GetRawText().Contains("never invent a pickup"),
                "personal question without recorded loot retains explicit uncertainty and no wiki referral");
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
        internal bool Direct;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var body = doc.RootElement.Clone(); Requests.Add(body);
            object message = !Direct && body.TryGetProperty("tools", out _)
                ? new { role="assistant", content=(string?)null, tool_calls=new[] { new { id="empty-wiki", type="function",
                    function=new { name="search_dreamweave_wiki", arguments="{\"query\":\"synthetic fixture\"}" } } } }
                : (object)new { role="assistant", content="I haven't observed enough to say yet." };
            return new(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(new { choices=new[] { new { message } } }), Encoding.UTF8, "application/json") };
        }
    }
    private sealed class EmptyWiki : IDreamWeaveWiki
    {
        internal int Calls;
        public Task<WikiResult> SearchAsync(string query, CancellationToken ct)
        { Calls++; return Task.FromResult(new WikiResult { Status="Unavailable", Message="Synthetic unavailable fixture." }); }
    }
}
