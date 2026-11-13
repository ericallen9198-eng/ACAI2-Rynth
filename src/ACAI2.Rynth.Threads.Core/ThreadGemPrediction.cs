#nullable disable
// Adapted from the original ACAI2 Thread journal; see origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACAI2.Rynth.Threads.Core
{
    public sealed class ThreadGemProbability
    {
        public string Name { get; set; } = "";
        public double Probability { get; set; }
        public double Lower { get; set; }
        public double Upper { get; set; }
        public int Hits { get; set; }
    }
    public sealed class ThreadGemForecast
    {
        public int Version { get; set; } = 2;
        public int? StartingLevel { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Basis { get; set; } = "Insufficient data";
        public string Evidence { get; set; } = "No prediction";
        public int TrainingRolls { get; set; }
        public double EffectiveSamples { get; set; }
        public bool BeforeOutcome { get; set; }
        public List<string> TrainingIds { get; set; } = new List<string>();
        public List<ThreadGemProbability> Modifiers { get; set; } = new List<ThreadGemProbability>();
        public double? AverageLevel { get; set; }
        public int? LowestObservedLevel { get; set; }
        public int? HighestObservedLevel { get; set; }
        public Dictionary<string, double> EntryProbabilities { get; set; } = new Dictionary<string, double>();
    }
    // Transparent, offline empirical predictor. Effects are multi-label, not mutually exclusive.
    public static class ThreadGemPrediction
    {
        public static ThreadGemForecast Build(IEnumerable<ThreadGemRoll> history, ThreadGemRoll recipe, DateTime now)
        {
            var result = new ThreadGemForecast { CreatedUtc = now };
            if (recipe.RecipeControl != "Fully Controlled") { result.Evidence = "Partial, random or unclassified recipe: prediction withheld."; return result; }
            var eligible = history.Where(x => x.Id != recipe.Id && x.Status == "Complete" && x.RecipeControl == "Fully Controlled" && x.StartedUtc < now).ToArray();
            bool knownStart = recipe.FragmentLevel.HasValue && recipe.FragmentLevelBasis.StartsWith("Item data matched", StringComparison.Ordinal);
            if (knownStart)
            {
                result.StartingLevel = recipe.FragmentLevel;
                eligible = eligible.Where(x => x.FragmentLevel == recipe.FragmentLevel && x.FragmentLevelBasis.StartsWith("Item data matched", StringComparison.Ordinal)).ToArray();
            }
            var exact = eligible.Where(x => x.RecipeKey() == recipe.RecipeKey()).ToArray();
            ThreadGemRoll[] cohort; double[] weights;
            if (exact.Length >= 3)
            { cohort = exact; weights = exact.Select(x => 1d).ToArray(); result.Basis = "Repeated exact recipe"; }
            else
            {
                cohort = eligible.Where(x => Distance(x, recipe) <= 4).ToArray();
                if (cohort.Length < 8 || cohort.Select(x => x.RecipeKey()).Distinct().Count() < 2)
                { result.Evidence = (knownStart ? "At starting level " + recipe.FragmentLevel + ": " : "Without a controlled starting level: ") + "Need 3 exact repeats, or 8 similar controlled rolls across at least 2 recipes. Exact repeats: " + exact.Length + "."; return result; }
                weights = cohort.Select(x => 1d / (1 + Distance(x, recipe))).ToArray();
                result.Basis = "Similar recipes (at most two slot replacements)";
            }
            double total = weights.Sum(), effective = total * total / weights.Sum(x => x * x);
            result.TrainingRolls = cohort.Length; result.EffectiveSamples = effective;
            result.TrainingIds = cohort.Select(x => x.Id).ToList();
            result.Evidence = result.Basis.StartsWith("Similar", StringComparison.Ordinal) ? "Exploratory; co-components and fragment level may confound these estimates." :
                effective < 10 ? "Exploratory; few repeated rolls." : effective < 30 ? "Limited sample; observational prediction." : "Better sampled; validate against new rolls.";
            foreach (string name in cohort.SelectMany(x => x.Modifiers.Select(m => m.Name)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                double hits = 0; int rawHits = 0;
                for (int i = 0; i < cohort.Length; i++) if (cohort[i].Modifiers.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { hits += weights[i]; rawHits++; }
                double rate = hits / total, z2 = 3.8416;
                double center = (rate + z2 / (2 * effective)) / (1 + z2 / effective);
                double half = 1.96 * Math.Sqrt(rate * (1 - rate) / effective + z2 / (4 * effective * effective)) / (1 + z2 / effective);
                result.Modifiers.Add(new ThreadGemProbability { Name = name, Hits = rawHits, Probability = (rate * effective + 1) / (effective + 2), Lower = Math.Max(0, center - half), Upper = Math.Min(1, center + half) });
            }
            result.Evidence += knownStart ? " Compared at the same captured starting level " + recipe.FragmentLevel + "." : " Starting level is not controlled in this estimate.";
            result.Modifiers = result.Modifiers.OrderByDescending(x => x.Probability).ThenBy(x => x.Name).ToList();
            var levels = cohort.Select((r, i) => new { r.Level, Weight = weights[i] }).Where(x => x.Level.HasValue).ToArray();
            if (levels.Length > 0) { result.AverageLevel = levels.Sum(x => x.Level.Value * x.Weight) / levels.Sum(x => x.Weight); result.LowestObservedLevel = levels.Min(x => x.Level.Value); result.HighestObservedLevel = levels.Max(x => x.Level.Value); }
            double entryWeight = cohort.Select((r, i) => r.Entries.HasValue ? weights[i] : 0).Sum();
            if (entryWeight > 0) foreach (int entries in cohort.Where(x => x.Entries.HasValue).Select(x => x.Entries.Value).Distinct())
                result.EntryProbabilities[entries.ToString(System.Globalization.CultureInfo.InvariantCulture)] = cohort.Select((r, i) => r.Entries == entries ? weights[i] : 0).Sum() / entryWeight;
            return result;
        }
        public static int Distance(ThreadGemRoll a, ThreadGemRoll b)
        {
            var names = a.Components.Select(x => x.Name).Concat(b.Components.Select(x => x.Name)).Distinct(StringComparer.OrdinalIgnoreCase);
            return names.Sum(name => Math.Abs(a.Components.Where(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count) - b.Components.Where(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count)));
        }
        public static string Validation(IEnumerable<ThreadGemRoll> history)
        {
            var rolls = history.Where(x => x.Status == "Complete" && x.Forecast != null && x.Forecast.BeforeOutcome && x.Forecast.Modifiers.Count > 0).ToArray();
            if (rolls.Length == 0) return "Prospective validation: no completed rolls with saved pre-result predictions yet.";
            var summaries = rolls.GroupBy(x => x.Forecast.Version).Select(group => {
                var errors = group.SelectMany(r => r.Forecast.Modifiers.Select(p => Math.Pow(p.Probability - (r.Modifiers.Any(m => m.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)) ? 1 : 0), 2))).ToArray();
                int novel = group.Sum(r => r.Modifiers.Count(m => !r.Forecast.Modifiers.Any(p => p.Name.Equals(m.Name, StringComparison.OrdinalIgnoreCase))));
                return "v" + group.Key + ": " + group.Count() + " rolls; Brier " + errors.Average().ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "; unseen modifier occurrences " + novel;
            });
            return "Prospective validation (lower Brier is better, known labels only): " + string.Join(" | ", summaries) + ".";
        }
    }
}
