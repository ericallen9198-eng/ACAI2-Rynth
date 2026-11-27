namespace ACAI2.Bridge;

internal static class ModelCompatibility
{
    // GPT-6 Chat Completions function calls require no reasoning for Luna.
    // Apply the same low-latency setting to every bridge request; retain the
    // previous setting for explicitly selected legacy GPT-5 models.
    internal static string ReasoningEffort(string model) =>
        model.StartsWith("gpt-6-", StringComparison.OrdinalIgnoreCase) ||
        model.StartsWith("gpt-6.", StringComparison.OrdinalIgnoreCase) ? "none" : "minimal";
}
