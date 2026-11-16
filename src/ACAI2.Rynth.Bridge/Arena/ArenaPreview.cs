using System.Text.Json;

namespace ACAI2.Bridge;

internal static class ArenaPreview
{
    internal static async Task RunAsync(string[] args, KeyferOptions options)
    {
        // Explicit command-line mode: never starts the listener or connects to a game/AI/chat transport.
        int seed = 42; int seedAt = Array.IndexOf(args, "--seed");
        if (seedAt >= 0 && (seedAt + 1 >= args.Length || !int.TryParse(args[seedAt + 1], out seed))) throw new ArgumentException("--seed requires an integer.");
        ArenaProfile first, second;
        if (args[0] == "--arena-preview-files")
        {
            if (args.Length < 5) throw new ArgumentException("--arena-preview-files <first-file> <first-name> <second-file> <second-name> [--seed N]");
            first = ArenaSheetParser.Parse(await File.ReadAllTextAsync(args[1]), args[2], "local-preview", DateTime.UtcNow);
            second = ArenaSheetParser.Parse(await File.ReadAllTextAsync(args[3]), args[4], "local-preview", DateTime.UtcNow);
        }
        else
        {
            if (args.Length < 3) throw new ArgumentException("--arena-preview <first-name> <second-name> [--seed N]");
            var store = new ArenaStore(Path.Combine(options.DataRoot, "ArenaPreview", "sheets.sqlite"));
            using var sheets = new ArenaSheets(store); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            first = await sheets.FindAsync(args[1], timeout.Token); second = await sheets.FindAsync(args[2], timeout.Token);
        }
        var match = ArenaEngine.Simulate(first, second, seed, DateTime.UtcNow);
        Console.WriteLine("LOCAL PREVIEW ONLY. Simplified fictional exhibition, not real PvP. No chat sent; no public statistics changed.");
        Console.WriteLine($"Profiles: {first.Name} level {first.Level}, HP {first.Health}; {second.Name} level {second.Level}, HP {second.Health}.");
        foreach (string line in match.Updates) Console.WriteLine(line);
        Console.WriteLine("Narration: " + match.CommentaryMode);
        Console.WriteLine($"Model {match.Model}, seed {seed}, events {match.Events.Count}; {match.Warnings.Length} unmodeled abilities:");
        foreach (string warning in match.Warnings) Console.WriteLine("  " + warning);
        Console.WriteLine($"Fictional adaptations: {match.Adaptations.Length}; exact server formulas are not used.");
        Console.WriteLine("Event totals: " + string.Join(", ", match.Events.GroupBy(x => x.Kind).Select(x => x.Key + "=" + x.Count())));
        int runsAt = Array.IndexOf(args, "--runs");
        if (runsAt >= 0)
        {
            if (runsAt + 1 >= args.Length || !int.TryParse(args[runsAt + 1], out int runs) || runs < 1 || runs > 1000)
                throw new ArgumentException("--runs must be between 1 and 1000.");
            var results = Enumerable.Range(0, runs).Select(i => ArenaEngine.Simulate(first, second, unchecked(seed + i), match.Utc)).ToArray();
            Console.WriteLine($"LOCAL BATCH ({runs} seeds starting {seed}): " + string.Join(", ", results.GroupBy(x => x.Winner.Length == 0 ? "Draw" : x.Winner).OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Count())));
            Console.WriteLine("Batch results describe Keyfer's fictional game, not actual PvP odds.");
        }
        int outputAt = Array.IndexOf(args, "--output");
        if (outputAt >= 0)
        {
            if (outputAt + 1 >= args.Length) throw new ArgumentException("--output requires a file path.");
            await File.WriteAllTextAsync(args[outputAt + 1], JsonSerializer.Serialize(match, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
