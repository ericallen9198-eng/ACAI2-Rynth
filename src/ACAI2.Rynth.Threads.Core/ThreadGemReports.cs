#nullable disable
// Adapted from the original ACAI2 Thread journal; see origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ACAI2.Rynth.Threads.Core
{
    public static class ThreadGemReports
    {
        public static string[] Roll(ThreadGemRoll roll)
        {
            if (roll == null) return new[] { "No captured rolls yet." };
            return new[] { roll.Id.Substring(0, 8) + " | " + roll.Status + " | " + roll.StartedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) + " local",
                string.Join(", ", roll.Components.Select(x => x.Name + (x.Count > 1 ? " x" + x.Count : ""))),
                roll.RecipeControl + " | Level " + (roll.Level?.ToString() ?? "?") + " | Entries " + (roll.Entries?.ToString() ?? "?"),
                roll.FragmentLevel.HasValue ? "Starting level: " + roll.FragmentLevel.Value + " (" + (roll.FragmentLevelBasis.StartsWith("Item data matched") ? "item data" : "candidate") + ")" : "Starting level: not captured" }
                .Concat(roll.Modifiers.Select(x => x.Raw)).Concat(roll.Warnings.Select(x => "Note: " + x)).ToArray();
        }
        public static string[] Forecast(ThreadGemForecast forecast)
        {
            if (forecast == null) return new[] { "No saved prediction." };
            var lines = new List<string> { forecast.Basis + " | n=" + forecast.TrainingRolls + " | effective n=" + forecast.EffectiveSamples.ToString("0.0", CultureInfo.InvariantCulture), forecast.Evidence };
            lines.AddRange(forecast.Modifiers.Select(x => x.Name + ": estimate " + Percent(x.Probability) + "; approximate interval " + Percent(x.Lower) + "-" + Percent(x.Upper) + "; observed in " + x.Hits + "/" + forecast.TrainingRolls + " rolls"));
            if (forecast.AverageLevel.HasValue) lines.Add("Weighted level average " + forecast.AverageLevel.Value.ToString("0.0", CultureInfo.InvariantCulture) + "; observed range " + forecast.LowestObservedLevel + "-" + forecast.HighestObservedLevel + " (not a prediction bound).");
            if (forecast.EntryProbabilities.Count > 0) lines.Add("Observed entries: " + string.Join(", ", forecast.EntryProbabilities.OrderBy(x => int.Parse(x.Key, CultureInfo.InvariantCulture)).Select(x => x.Key + " entries " + Percent(x.Value))));
            lines.Add("Modifiers can co-occur. Unseen modifiers remain possible; this is not a causal recipe rule."); return lines.ToArray();
        }
        public static string[] Component(ThreadGemJournal data, string name, int? dose = null)
        {
            var lines = new List<string> { name + (dose.HasValue ? " x" + dose : " (all counts)") };
            lines.AddRange(data.Claims.Where(x => x.Component.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(x => "Game description (" + x.Observations + " captures): " + x.Description));
            var rolls = data.Rolls.Where(x => x.Status == "Complete" && x.Components.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && (!dose.HasValue || c.Count == dose))).ToArray();
            lines.Add("Completed roll appearances: " + rolls.Length + ". Correlation only; co-components vary.");
            foreach (var group in rolls.GroupBy(x => x.RecipeControl))
            {
                lines.Add(group.Key + ": n=" + group.Count());
                lines.AddRange(group.SelectMany(x => x.Modifiers.Select(m => m.Name).Distinct(StringComparer.OrdinalIgnoreCase)).GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(x => x.Count()).Take(8).Select(x => x.Key + ": " + x.Count() + "/" + group.Count() + " (" + Percent(x.Count() / (double)group.Count()) + ")"));
            }
            lines.Add("Dose counts: " + string.Join(", ", rolls.GroupBy(r => r.Components.First(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Count).OrderBy(x => x.Key).Select(x => "x" + x.Key + ": " + x.Count() + " rolls")));
            var targets = data.Claims.Where(x => x.Component.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(x => System.Text.RegularExpressions.Regex.Match(x.Description, @"may add (.+?) Affinity\.", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).Where(x => x.Success).Select(x => x.Groups[1].Value + " Affinity").Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (string target in targets.Where(x => !x.StartsWith("a random", StringComparison.OrdinalIgnoreCase)))
            {
                var hits = rolls.Where(r => r.Modifiers.Any(m => m.Name.Equals(target, StringComparison.OrdinalIgnoreCase))).ToArray();
                lines.Add("Claimed " + target + ": " + hits.Length + "/" + rolls.Length + " completed rolls" + (rolls.Length > 0 ? " (" + Percent(hits.Length / (double)rolls.Length) + ")" : "") + ".");
                foreach (bool hit in new[] { true, false })
                {
                    var values = rolls.Where(r => r.Modifiers.Any(m => m.Name.Equals(target, StringComparison.OrdinalIgnoreCase)) == hit).SelectMany(r => r.Modifiers.Where(m => m.Category == "Affinity" && m.Unit == "Percent" && m.Value.HasValue).Select(m => m.Value.Value)).ToArray();
                    if (values.Length > 0) lines.Add((hit ? "Hit" : "Miss") + " affinity percentage average: " + values.Average().ToString("0.0", CultureInfo.InvariantCulture) + "% (" + values.Length + " observed affinity values).");
                }
            }
            return lines.ToArray();
        }
        public static string Percent(double value) => (value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }
}
