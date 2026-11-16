namespace ACAI2.Bridge;

internal sealed class KeyferOptions
{
    public string Server { get; } = Environment.GetEnvironmentVariable("ACAI2_KEYFER_SERVER") ?? "DreamWeave";
    public string Character { get; } = Environment.GetEnvironmentVariable("ACAI2_KEYFER_CHARACTER") ?? "Keyfer Sutherland";
    public string Model { get; } = Environment.GetEnvironmentVariable("ACAI2_OPENAI_MODEL") ?? "gpt-5-mini";
    public string DataRoot { get; } = Environment.GetEnvironmentVariable("ACAI2_DATA_ROOT") ??
        (OperatingSystem.IsWindows() ? @"C:\ACAI2\Data" : Path.Combine(AppContext.BaseDirectory, "Data"));
    public string BehaviorPath { get; } = Environment.GetEnvironmentVariable("ACAI2_BEHAVIOR_PATH") ??
        (OperatingSystem.IsWindows() ? @"C:\ACAI2\Knowledge\behavior.txt" : Path.Combine(AppContext.BaseDirectory, "Knowledge", "behavior.txt"));

    public string? ServerBehaviorPath => Environment.GetEnvironmentVariable("ACAI2_SERVER_BEHAVIOR_PATH") ??
        (System.Text.RegularExpressions.Regex.IsMatch(Server, @"^[A-Za-z0-9 _-]+$")
            ? Path.Combine(Path.GetDirectoryName(BehaviorPath)!, "Servers", Server, "behavior.txt") : null);

    public bool Matches(string? server, string? character) =>
        string.Equals(Server, server, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Character, character, StringComparison.OrdinalIgnoreCase);
}
