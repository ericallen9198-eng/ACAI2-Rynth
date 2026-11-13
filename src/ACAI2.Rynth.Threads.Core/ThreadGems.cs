#nullable disable
// Adapted from the original ACAI2 Thread journal; see origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ACAI2.Rynth.Threads.Core
{
    public sealed class ThreadGemComponent
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public string Role { get; set; } = "Unknown";
    }
    public sealed class ThreadGemModifier
    {
        public string Name { get; set; } = "";
        public string Raw { get; set; } = "";
        public string Category { get; set; } = "Unknown";
        public double? Value { get; set; }
        public string Unit { get; set; } = "Unknown";
    }
    public sealed class ThreadComponentClaim
    {
        public string Component { get; set; } = "";
        public string Description { get; set; } = "";
        public string Role { get; set; } = "Unknown";
        public DateTime ObservedUtc { get; set; }
        public int Observations { get; set; }
    }
    public sealed class ThreadGemRoll
    {
        public string Id { get; set; } = "";
        public DateTime StartedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }
        public string Server { get; set; } = "";
        public string Character { get; set; } = "";
        public bool PressObserved { get; set; }
        public bool ModifierHeaderObserved { get; set; }
        public bool PrepRecipeConfirmed { get; set; }
        public bool FinalRecipeObserved { get; set; }
        public string RecipeSource { get; set; } = "Unknown";
        public string Status { get; set; } = "Capturing";
        public string FragmentType { get; set; } = "Unknown";
        public int? FragmentLevel { get; set; }
        public int? FragmentObjectId { get; set; }
        public string FragmentLevelBasis { get; set; } = "Unknown";
        public string FragmentLevelEvidence { get; set; } = "";
        public List<ThreadGemComponent> Components { get; set; } = new List<ThreadGemComponent>();
        public bool RandomSlots { get; set; }
        public string RecipeControl { get; set; } = "Unknown";
        public int? Level { get; set; }
        public int? Entries { get; set; }
        public List<ThreadGemModifier> Modifiers { get; set; } = new List<ThreadGemModifier>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string Rating { get; set; } = "";
        public ThreadGemForecast Forecast { get; set; }
        public string RecipeKey() => string.Join(" | ", Components.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => x.Name.ToLowerInvariant() + " x" + x.Count));
    }
    public sealed class ThreadRunRecord
    {
        public string Id { get; set; } = "";
        public string RollId { get; set; } = "";
        public string Server { get; set; } = "";
        public string Character { get; set; } = "";
        public string LinkBasis { get; set; } = "Unlinked";
        public string Dungeon { get; set; } = "";
        public DateTime OpenedUtc { get; set; }
        public DateTime? EndedUtc { get; set; }
        public string RealmInstance { get; set; } = "";
        public DateTime? EnteredUtc { get; set; }
        public DateTime? ClearedUtc { get; set; }
        public double ObservedSeconds { get; set; }
        public bool ObservationGap { get; set; }
        public string Status { get; set; } = "Awaiting entry";
        public int? MonstersKilled { get; set; }
        public int? MonstersTotal { get; set; }
        public bool? BossRemaining { get; set; }
        public int Deaths { get; set; }
        public int? RemainingReturns { get; set; }
        public List<string> Enemies { get; set; } = new List<string>();
    }
    public sealed class ThreadFragmentContext
    {
        public int ObjectId { get; set; }
        public string Type { get; set; } = "";
        public int Level { get; set; }
        public DateTime ObservedUtc { get; set; }
        public bool FromItemExamination { get; set; }
        public string Evidence { get; set; } = "";
    }
    public sealed class ThreadGemImprint
    {
        public int ObjectId { get; set; }
        public DateTime ObservedUtc { get; set; }
        public List<ThreadGemComponent> Components { get; set; } = new List<ThreadGemComponent>();
    }
    public sealed class ThreadGemJournal
    {
        public int Schema { get; set; } = 1;
        public string Server { get; set; } = "";
        public string Character { get; set; } = "";
        public List<ThreadGemRoll> Rolls { get; set; } = new List<ThreadGemRoll>();
        public List<ThreadComponentClaim> Claims { get; set; } = new List<ThreadComponentClaim>();
        public List<ThreadRunRecord> Runs { get; set; } = new List<ThreadRunRecord>();
        public List<ThreadGemImprint> Imprints { get; set; } = new List<ThreadGemImprint>();
        public List<ThreadFragmentContext> Fragments { get; set; } = new List<ThreadFragmentContext>();
        public string CurrentRollId { get; set; } = "";
        public string ActiveRunId { get; set; } = "";
        public ThreadGemRoll Current() => Rolls.FirstOrDefault(x => x.Id == CurrentRollId);
        public ThreadGemRoll LastComplete() => Rolls.LastOrDefault(x => x.Status == "Complete");
    }
    // A construction journal, independent of combat logging. Supplied research never becomes roll data.
    public sealed class ThreadGemCapture
    {
        private static readonly Regex Imprint = Rx(@"^The fragment takes the imprint: (?<recipe>.+)\.$");
        private static readonly Regex Feed = Rx(@"^The fragment takes the (?<name>.+?)\. (?<claim>It .+)$");
        private static readonly Regex Recipe = Rx(@"^Components selected:\s*(?<recipe>.+)$");
        private static readonly Regex Component = Rx(@"^(?<name>.+?)(?:\s+x(?<count>\d+))?$");
        private static readonly Regex Level = Rx(@"^Level (?<level>\d+), (?<entries>\d+) entr(?:y|ies)\.$");
        private static readonly Regex Modifier = Rx(@"^(?<name>[A-Za-z][A-Za-z0-9 '\-]+?)\s+-\s+(?<detail>.+)$");
        private static readonly Regex Number = Rx(@"(?<mult>x)(?<m>\d+(?:\.\d+)?)|(?<p>\d+(?:\.\d+)?)%|\+(?<r>\d+(?:\.\d+)?)");
        private static readonly Regex Fragment = Rx(@"^-\s*(?<type>Raw Fragment|Guide Fragment)\s*\(Level (?<level>\d+)\)$");
        private static readonly Regex Open = Rx(@"^The Thread Gem opens a way into (?<dungeon>.+?)\. It will bring you back (?<returns>\d+) more time\(s\) while the dungeon stands\.$");
        private static readonly Regex Enemy = Rx(@"^\[VS\] Matching item detected:\s*(?<name>(?:Elite )?Threadbound .+)$");
        public ThreadGemJournal Data { get; private set; }
        public ThreadGemCapture(ThreadGemJournal data) { Data = data ?? throw new ArgumentNullException(nameof(data)); }
        private static Regex Rx(string value) => new Regex(value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        public bool Observe(string raw, DateTime now, int selectedId = 0, string selectedName = "")
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > 4096 || !ThreadText.Native(raw)) return false;
            string text = Regex.Replace(raw, @"<[^>]+>", "").Trim();
            if (!ThreadText.Native(text)) return false;
            if(Data.Imprints.Count>128)Data.Imprints.RemoveRange(0,Data.Imprints.Count-128);
            if(Data.Fragments.Count>128)Data.Fragments.RemoveRange(0,Data.Fragments.Count-128);
            var imprint = Imprint.Match(text);
            if (imprint.Success)
            {
                int id = ThreadFragmentInspection.FragmentType(selectedName).Length > 0 ? selectedId : 0;
                Data.Imprints.RemoveAll(x => now - x.ObservedUtc > TimeSpan.FromMinutes(30) || (id != 0 && x.ObjectId == id));
                Data.Imprints.Add(new ThreadGemImprint { ObjectId = id, ObservedUtc = now, Components = ParseRecipe(imprint.Groups["recipe"].Value) });
                return true;
            }
            var feed = Feed.Match(text);
            if (feed.Success)
            {
                string name = feed.Groups["name"].Value, claim = feed.Groups["claim"].Value;
                if(name.Length>100||claim.Length>512)return false;
                var known = Data.Claims.FirstOrDefault(x => x.Component.Equals(name, StringComparison.OrdinalIgnoreCase) && x.Description == claim);
                if(known==null&&Data.Claims.Count>=512)return false;
                if (known == null) Data.Claims.Add(new ThreadComponentClaim { Component = name, Description = claim, Role = ClaimRole(claim), ObservedUtc = now, Observations = 1 });
                else { known.Observations++; known.ObservedUtc = now; }
                // Feed messages teach descriptions, never reconstruct an ambiguous interleaved recipe.
                return true;
            }
            var fragment = Fragment.Match(text);
            if (fragment.Success)
            {
                if (selectedId == 0 || !selectedName.Equals(fragment.Groups["type"].Value, StringComparison.OrdinalIgnoreCase)) return false;
                Data.Fragments.RemoveAll(x => x.ObjectId == selectedId);
                Data.Fragments.Add(new ThreadFragmentContext { ObjectId = selectedId, Type = selectedName, Level = int.Parse(fragment.Groups["level"].Value, CultureInfo.InvariantCulture), ObservedUtc = now });
                Data.Fragments.RemoveAll(x => now - x.ObservedUtc > TimeSpan.FromHours(1)); return true;
            }
            if (text.Equals("Pressed. It holds.", StringComparison.OrdinalIgnoreCase))
            {
                Start(now, true);
                var recent = Data.Fragments.Where(x => now >= x.ObservedUtc && now - x.ObservedUtc < TimeSpan.FromMinutes(30)).ToArray();
                var context = recent.LastOrDefault(x => x.ObjectId == selectedId);
                bool selected = context != null;
                if (context == null)
                {
                    var examined = recent.Where(x => x.FromItemExamination).ToArray();
                    if (examined.Length == 1) context = examined[0];
                }
                if (context != null)
                {
                    var roll = Data.Current(); roll.FragmentType = context.Type; roll.FragmentLevel = context.Level; roll.FragmentObjectId = context.ObjectId;
                    roll.FragmentLevelEvidence = context.Evidence;
                    roll.FragmentLevelBasis = context.FromItemExamination ? (selected ? "Cached fragment selection; press association unverified" : "Single recent examined fragment; association unverified") : "Selection-correlated chat inspection; attribution unverified";
                    Data.Fragments.Remove(context); // Never reuse the same examination for an unrelated later gem.
                }
                int fragmentId = context?.ObjectId ?? selectedId;
                var recipeImprint = Data.Imprints.LastOrDefault(x => x.ObjectId != 0 && x.ObjectId == fragmentId && now - x.ObservedUtc < TimeSpan.FromMinutes(30));
                if (recipeImprint != null)
                {
                    Data.Current().Components = recipeImprint.Components.Select(x => new ThreadGemComponent { Name = x.Name, Count = x.Count, Role = x.Role }).ToList();
                    Data.Current().RecipeSource = "Imprint (provisional until final summary)";
                    Data.Current().RecipeControl = Control(Data.Current().Components, false);
                    Data.Imprints.Remove(recipeImprint);
                }
                return true;
            }
            var opening = Open.Match(text);
            if (opening.Success)
            {
                string dungeon = opening.Groups["dungeon"].Value;
                var run = Data.Runs.FirstOrDefault(x => x.Id == Data.ActiveRunId && !x.EndedUtc.HasValue && x.Dungeon == dungeon && x.EnteredUtc == null && string.IsNullOrEmpty(x.RealmInstance) && now - x.OpenedUtc < TimeSpan.FromHours(4));
                if (run == null)
                {

                    run = new ThreadRunRecord { Id = Guid.NewGuid().ToString("N"), Server = Data.Server, Character = Data.Character, Dungeon = dungeon, OpenedUtc = now,
                        RollId = "", LinkBasis = "Unlinked: gem identity not observed" };
                    Data.Runs.Add(run); Data.ActiveRunId = run.Id;
                }
                run.RemainingReturns = int.Parse(opening.Groups["returns"].Value, CultureInfo.InvariantCulture); return true;
            }
            var enemy = Enemy.Match(text);
            if (enemy.Success)
            {
                var run = Data.Runs.FirstOrDefault(x => x.Id == Data.ActiveRunId && !x.EndedUtc.HasValue && now - x.OpenedUtc < TimeSpan.FromHours(4));
                if (run == null) return false;
                string name = enemy.Groups["name"].Value;
                if (!run.Enemies.Contains(name) && run.Enemies.Count < 64) { run.Enemies.Add(name); return true; }
                return false;
            }
            var recipe = Recipe.Match(text);
            if (recipe.Success)
            {
                if (Data.Current() == null || Data.Current().Status != "Capturing") Start(now, false);
                var roll = Data.Current();
                roll.Components = ParseRecipe(recipe.Groups["recipe"].Value);
                roll.FinalRecipeObserved = true; roll.RecipeSource = "Final Components selected summary";
                roll.RecipeControl = Control(roll.Components, roll.RandomSlots);
                if (!roll.ModifierHeaderObserved)
                { roll.Forecast = ThreadGemPrediction.Build(Data.Rolls, roll, now); roll.Forecast.BeforeOutcome = true; }
                return true;
            }
            var current = Data.Current();
            if (current == null || current.Status != "Capturing") return false;
            if (now - current.StartedUtc > TimeSpan.FromMinutes(10)) { current.Status = "Incomplete"; current.Warnings.Add("Capture timed out before its final level line."); return true; }
            if (text.Equals("Remaining slots drawn at random.", StringComparison.OrdinalIgnoreCase))
            { current.RandomSlots = true; current.RecipeControl = "Partial / Random Slots"; current.Forecast = null; return true; }
            if (text.Equals("The gem carries:", StringComparison.OrdinalIgnoreCase))
            { current.ModifierHeaderObserved = true; return true; }
            var level = Level.Match(text);
            if (level.Success)
            {
                current.Level = int.Parse(level.Groups["level"].Value, CultureInfo.InvariantCulture);
                current.Entries = int.Parse(level.Groups["entries"].Value, CultureInfo.InvariantCulture);
                current.Status = current.PressObserved && (current.FinalRecipeObserved || current.PrepRecipeConfirmed) && current.Components.Count > 0 && current.ModifierHeaderObserved && current.Modifiers.Count > 0 && current.Level > 0 && current.Entries > 0 ? "Complete" : "Incomplete";
                if (current.Status != "Complete") current.Warnings.Add("Missing press/recipe/modifier output or invalid level/entries; excluded from statistics.");
                current.CompletedUtc = now; return true;
            }
            var modifier = Modifier.Match(text);
            if (current.ModifierHeaderObserved && modifier.Success && !text.StartsWith("[", StringComparison.Ordinal))
            {
                // A final result block accepts future modifier names and retains the exact description.
                var parsed = ParseModifier(modifier.Groups["name"].Value, modifier.Groups["detail"].Value, text);
                if (!current.Modifiers.Any(x => x.Raw == parsed.Raw)) {if(current.Modifiers.Count<64)current.Modifiers.Add(parsed);}
                return true;
            }
            return false;
        }
        public bool ObserveExaminedFragment(int objectId, ThreadFragmentLevel item, DateTime now)
        {
            if (objectId == 0 || item == null || item.Level <= 0) return false;
            Data.Fragments.RemoveAll(x => now - x.ObservedUtc >= TimeSpan.FromMinutes(30));
            var previous = Data.Fragments.FirstOrDefault(x => x.ObjectId == objectId);
            if (previous != null && previous.FromItemExamination) return false; // Preserve the first pre-press level, not later component-induced changes.
            Data.Fragments.RemoveAll(x => x.ObjectId == objectId);
            Data.Fragments.Add(new ThreadFragmentContext { ObjectId = objectId, Type = item.Type, Level = item.Level, Evidence = item.Evidence, FromItemExamination = true, ObservedUtc = now });
            return true;
        }
        public bool Tick(DateTime now)
        {
            var roll=Data.Current();
            if(roll?.Status!="Capturing"||now-roll.StartedUtc<=TimeSpan.FromMinutes(10))return false;
            roll.Status="Incomplete";roll.Warnings.Add("Capture timed out; incomplete evidence excluded from statistics.");return true;
        }
        private void Start(DateTime now, bool pressed)
        {
            var previous = Data.Current();
            if (previous != null && previous.Status == "Capturing") { previous.Status = "Incomplete"; previous.Warnings.Add("Another roll began before this capture finished."); }
            var roll = new ThreadGemRoll { Id = Guid.NewGuid().ToString("N"), StartedUtc = now, Server = Data.Server, Character = Data.Character, PressObserved = pressed };
            Data.Rolls.Add(roll); Data.CurrentRollId = roll.Id;
        }
        public List<ThreadGemComponent> ParseRecipe(string text)
        {
            var result = new List<ThreadGemComponent>();
            foreach (string part in text.Split(',').Take(32))
            {
                var match = Component.Match(part.Trim());
                if (!match.Success) continue;
                string name = match.Groups["name"].Value.Trim(); int count = 1;
                if(name.Length>100){result.Add(new ThreadGemComponent{Name=name.Substring(0,100),Count=0,Role="Unknown"});continue;}
                if (match.Groups["count"].Success && (!int.TryParse(match.Groups["count"].Value, out count) || count < 1 || count > 100))
                { result.Add(new ThreadGemComponent { Name = part.Trim(), Count = 0, Role = "Unknown" }); continue; }
                var prior = result.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (prior != null) prior.Count += count;
                else result.Add(new ThreadGemComponent { Name = name, Count = count, Role = Role(name) });
            }
            return result;
        }
        public string Role(string name)
        {
            var roles = Data.Claims.Where(x => x.Component.Equals(name, StringComparison.OrdinalIgnoreCase) && x.Role != "Unknown").Select(x => x.Role).Distinct().ToArray();
            if (roles.Length == 1) return roles[0];
            if (name.EndsWith(" Scarab", StringComparison.OrdinalIgnoreCase)) return "Scarab";
            if (name.EndsWith(" Taper", StringComparison.OrdinalIgnoreCase)) return "Taper";
            if (name.EndsWith(" Talisman", StringComparison.OrdinalIgnoreCase)) return "Talisman";
            if (name.StartsWith("Powdered ", StringComparison.OrdinalIgnoreCase)) return "Powder";
            // Only category hints from the supplied research; they never create direct claims or observations.
            if (new[] { "Mugwort", "Hyssop", "Saffron", "Amaranth" }.Contains(name, StringComparer.OrdinalIgnoreCase)) return "Herb";
            if (new[] { "Colcothar", "Cinnabar", "Cadmia", "Brimstone", "Realgar" }.Contains(name, StringComparer.OrdinalIgnoreCase)) return "Reagent";
            return "Unknown";
        }
        private static string ClaimRole(string description)
        {
            string text = description.ToLowerInvariant();
            if (text.Contains("level and entry count")) return "Scarab";
            if (text.Contains("re-roll")) return "Reagent";
            if (text.Contains("strengthen")) return "Talisman";
            if (text.Contains("bonus modifier")) return "Herb";
            if (text.Contains("affinity")) return "Powder";
            if (text.Contains("add a monster modifier")) return "Taper";
            return "Unknown";
        }
        public static string Control(List<ThreadGemComponent> components, bool random)
        {
            if (random || components.Sum(x => x.Count) < 9) return "Partial / Random Slots";
            var expected = new Dictionary<string, int> { { "Scarab", 1 }, { "Herb", 1 }, { "Powder", 1 }, { "Reagent", 1 }, { "Talisman", 1 }, { "Taper", 4 } };
            return components.All(x => x.Count > 0 && expected.ContainsKey(x.Role)) && components.Sum(x => x.Count) == 9 && expected.All(e => components.Where(x => x.Role == e.Key).Sum(x => x.Count) == e.Value) ? "Fully Controlled" : "Unknown / Invalid Slots";
        }
        public static ThreadGemModifier ParseModifier(string name, string detail, string raw)
        {
            string lower = detail.ToLowerInvariant();
            var mod = new ThreadGemModifier { Name = name, Raw = raw, Category = name.EndsWith(" Affinity", StringComparison.OrdinalIgnoreCase) ? "Affinity" :
                lower.Contains("boss") ? "Boss" : lower.Contains("kills pay") || lower.Contains("kills drop") ? "Reward" :
                lower.Contains("monster") || lower.Contains("your shield") || lower.Contains("magic armor") ? "Monster" : "Unknown" };
            var number = Number.Match(detail);
            if (number.Success)
            {
                string group = number.Groups["m"].Success ? "m" : number.Groups["p"].Success ? "p" : "r";
                mod.Value = double.Parse(number.Groups[group].Value, CultureInfo.InvariantCulture);
                mod.Unit = group == "m" ? "Multiplier" : group == "p" ? "Percent" : "Rating";
            }
            return mod;
        }
    }
}
