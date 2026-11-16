using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using ACAI2.Protocol;

namespace ACAI2.Bridge;

internal interface IArenaSheets
{
    Task<ArenaProfile> FindAsync(string name, CancellationToken ct, bool refresh = false);
    Task<ArenaProfile> RegisterAsync(string name, string url, CancellationToken ct);
}
internal sealed class ArenaSheets : IArenaSheets, IDisposable
{
    private readonly HttpClient http;
    private readonly ArenaStore store;
    private Dictionary<string, string[]>? directory;
    private DateTime directoryUtc;
    internal ArenaSheets(ArenaStore store, HttpMessageHandler? handler = null)
    {
        this.store = store;
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(12) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ACAI2-Arena/0.1");
    }
    internal static Uri SheetUri(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "char.acdreamweave.com" || !uri.IsDefaultPort ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || !Regex.IsMatch(uri.AbsolutePath, @"^/[A-Za-z0-9_-]{1,80}/?$"))
            throw new ArgumentException("Use a published https://char.acdreamweave.com/<sheet-id> URL.");
        return uri;
    }
    internal static Dictionary<string, string[]> ParseDirectory(string html)
    {
        var pairs = new List<(string Name, string Url)>();
        foreach (Match m in Regex.Matches(html, "<a\\b[^>]*href\\s*=\\s*[\"'](?<url>https://char\\.acdreamweave\\.com/[A-Za-z0-9_-]+/?)[\"'][^>]*>(?<name>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1)))
        {
            string inner = m.Groups["name"].Value;
            var heading = Regex.Match(inner, @"<h[1-6]\b[^>]*>(.*?)</h[1-6]>", RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
            string name = ArenaSheetParser.Plain(heading.Success ? heading.Groups[1].Value : inner).Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0) ?? "";
            name = Regex.Replace(name, @"\s+", " ").Trim();
            if (ArenaCommands.ValidName(name)) pairs.Add((name, SheetUri(WebUtility.HtmlDecode(m.Groups["url"].Value)).AbsoluteUri));
        }
        return pairs.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Select(v => v.Url).Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);
    }
    public async Task<ArenaProfile> FindAsync(string name, CancellationToken ct, bool refresh = false)
    {
        if (!ArenaCommands.ValidName(name)) throw new ArgumentException("Invalid character name.");
        var saved = store.Profile(name);
        if (!refresh && saved != null && saved.ParserRevision == ArenaSheetParser.Revision && DateTime.UtcNow - saved.RetrievedUtc < TimeSpan.FromMinutes(15)) { saved.Validate(name); return saved; }
        string url;
        if (saved != null) url = saved.Url;
        else
        {
            if (directory == null || DateTime.UtcNow - directoryUtc > TimeSpan.FromMinutes(10))
            {
                directory = ParseDirectory(await Fetch(new Uri("https://www.acdreamweave.com/sheets"), ct)); directoryUtc = DateTime.UtcNow;
            }
            if (!directory.TryGetValue(name, out var urls) || urls.Length != 1)
                throw new InvalidDataException("No unique published sheet found for " + name + ". That character can register using !sheet <url>.");
            url = urls[0];
        }
        return await RegisterAsync(name, url, ct);
    }
    public async Task<ArenaProfile> RegisterAsync(string name, string url, CancellationToken ct)
    {
        var profile = ArenaSheetParser.Parse(await Fetch(SheetUri(url), ct), name, url, DateTime.UtcNow);
        store.SaveProfile(profile); return profile;
    }
    private async Task<string> Fetch(Uri uri, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400) throw new InvalidDataException("Sheet site redirected; no redirect was followed.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 2000000) throw new InvalidDataException("Sheet response too large.");
        using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
        using var output = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = await input.ReadAsync(buffer, deadline.Token)) > 0)
        { if (output.Length + read > 2000000) throw new InvalidDataException("Sheet response too large."); await output.WriteAsync(buffer.AsMemory(0, read), deadline.Token); }
        return Encoding.UTF8.GetString(output.ToArray());
    }
    public void Dispose() => http.Dispose();
}
