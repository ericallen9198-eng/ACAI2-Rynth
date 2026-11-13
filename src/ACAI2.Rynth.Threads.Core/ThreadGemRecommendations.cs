#nullable disable
// Adapted from the original ACAI2 Thread journal; see origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ACAI2.Rynth.Threads.Core
{
    // Goal-oriented observed rankings, not claims of causal component effects or guaranteed farming rates.
    public static class ThreadGemRecommendations
    {
        private sealed class Candidate
        {
            public ThreadGemRoll[] Rolls;
            public double Score;
            public string Detail;
        }
        public static string[] Recommend(ThreadGemJournal journal, string request)
        {
            request = request.Trim();
            string lower = request.ToLowerInvariant();
            bool salvage = lower.StartsWith("salvage ", StringComparison.Ordinal), difficulty = lower == "easy" || lower == "hard";
            string modifier = lower == "lum" || lower == "luminance" ? "Radiant" : lower == "xp" || lower == "experience" ? "Enlightened" : lower == "loot" ? "Bounteous" : salvage ? request.Substring(8).Trim().Trim('"') + " Affinity" : "";
            if (!salvage && !difficulty && modifier.Length == 0) return new[] { "Goals: lum, xp, loot, easy, hard, salvage <material>." };
            var cohorts = journal.Rolls.Where(x => x.Status == "Complete" && x.RecipeControl == "Fully Controlled")
                .GroupBy(x => x.RecipeKey() + " | starting=" + StartLabel(x)).Select(x => x.ToArray()).ToArray();
            var candidates = new List<Candidate>();
            foreach (var rows in cohorts.Where(x => x.Length >= 3))
            {
                if (difficulty)
                {
                    var rated = rows.Where(x => x.Rating == "easy" || x.Rating == "tough" || x.Rating == "nightmare").ToArray();
                    if (rated.Length < 3) continue;
                    int successes = rated.Count(x => lower == "easy" ? x.Rating == "easy" : x.Rating == "tough" || x.Rating == "nightmare");
                    candidates.Add(new Candidate { Rolls = rows, Score = successes / (double)rated.Length, Detail = lower + " ratings " + successes + "/" + rated.Length + " (subjective; builds and play vary)" });
                }
                else
                {
                    var found = rows.Select(r => r.Modifiers.FirstOrDefault(m => m.Name.Equals(modifier, StringComparison.OrdinalIgnoreCase))).ToArray();
                    if (!found.Any(x => x != null) || found.Any(x => x != null && (!x.Value.HasValue || x.Unit != (salvage ? "Percent" : "Multiplier")))) continue;
                    int seen = found.Count(x => x != null);
                    if (!salvage && seen < 3) continue;
                    // The raw-fragment dump shows baseline XP/luminance can differ from x1.
                    // Never invent the multiplier for rolls where this named reward modifier is absent.
                    double score = salvage ? found.Average(x => x?.Value ?? 0) : found.Where(x => x != null).Average(x => x.Value.Value);
                    candidates.Add(new Candidate { Rolls = rows, Score = score, Detail = modifier + (salvage ? " observed affinity average " : " conditional modifier average ") + (salvage ? score.ToString("0.0", CultureInfo.InvariantCulture) + "%" : "x" + score.ToString("0.00", CultureInfo.InvariantCulture)) + "; present " + seen + "/" + rows.Length });
                }
            }
            var lines = new List<string> { "Observed recipe ranking: " + request + ". At least 3 repeated controlled rolls per recipe/starting-level group.",
                "Reward ranks are conditional on the named modifier occurring; its frequency is shown separately. These are not total payouts, XP/hour, or proof of synergy." };
            if (candidates.Count == 0)
            {
                lines.Add(difficulty ? "No sufficiently rated cohorts yet. Rate runs easy/tough/nightmare; level alone does not establish difficulty." : "No sufficiently repeated cohort has a parsed outcome for this goal yet.");
                return lines.ToArray();
            }
            foreach (var c in candidates.OrderByDescending(x => x.Score).ThenByDescending(x => x.Rolls.Length).Take(5))
            {
                lines.Add(c.Rolls[0].Id.Substring(0, 8) + " | starting " + StartLabel(c.Rolls[0]) + " | n=" + c.Rolls.Length + " | " + c.Detail);
                lines.Add(c.Rolls[0].RecipeKey());
            }
            lines.Add("Small-sample ranks can change. Different starting levels/builds are not a controlled component comparison.");
            return lines.ToArray();
        }
        private static string StartLabel(ThreadGemRoll roll) => roll.FragmentLevel.HasValue && roll.FragmentLevelBasis.StartsWith("Item data matched", StringComparison.Ordinal) ? roll.FragmentLevel.Value.ToString(CultureInfo.InvariantCulture) : "unknown/unverified";
        public static string[] Pair(ThreadGemJournal data, string first, string second, string outcome)
        {
            var rows = data.Rolls.Where(x => x.Status == "Complete" && x.RecipeControl == "Fully Controlled").ToArray();
            bool Has(ThreadGemRoll r, string name) => r.Components.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            var lines = new List<string> { first + " + " + second + " → " + outcome, "Presence association only: dosages, other components and starting levels may differ. This does not prove synergy." };
            for (int mask = 0; mask < 4; mask++)
            {
                bool a = (mask & 1) != 0, b = (mask & 2) != 0;
                var group = rows.Where(r => Has(r, first) == a && Has(r, second) == b).ToArray();
                int hits = group.Count(r => r.Modifiers.Any(m => m.Name.Equals(outcome, StringComparison.OrdinalIgnoreCase)));
                lines.Add((a ? "With " : "Without ") + first + "; " + (b ? "with " : "without ") + second + ": " + hits + "/" + group.Length + (group.Length > 0 ? " (" + ThreadGemReports.Percent(hits / (double)group.Length) + ")" : " (no evidence)"));
            }
            lines.Add("Use dose-specific stats and matched recipe comparisons before attributing an effect to a component combination.");
            return lines.ToArray();
        }
    }
}
