namespace ACAI2.Bridge;

internal sealed record ArenaBeat(int Round, ArenaEvent[] Actions, bool Knockout);
internal static class ArenaNarrator
{
    internal static ArenaBeat[] Beats(ArenaMatch match)
    {
        var used = new HashSet<string>(); var beats = new List<ArenaBeat>();
        int size = Math.Max(1, (int)Math.Ceiling(match.Events.Count / 3.0));
        for (int round = 0; round < 3; round++)
        {
            var slice = match.Events.Skip(round * size).Take(size).ToArray(); if (slice.Length == 0) continue;
            ArenaEvent? Pick(string name) => slice.Where(x => x.Actor == name).OrderByDescending(x => Score(match, x, used)).FirstOrDefault();
            var first = Pick(match.First.Name); var second = Pick(match.Second.Name);
            ArenaEvent? finisher = (round + 1) * size >= match.Events.Count && match.Finish == "knockout" && match.Winner.Length > 0 ?
                match.Events.LastOrDefault(x => x.Actor == match.Winner && x.Target != match.Winner && x.Damage > 0) : null;
            if (finisher != null) { if (finisher.Actor == match.First.Name) first = finisher; else second = finisher; }
            foreach (var action in new[] { first, second }.Where(x => x != null)) used.Add(action!.Actor + ":" + Feature(match, action));
            var actions = finisher?.Actor == match.First.Name ? new[] { second, first } : new[] { first, second };
            beats.Add(new(round + 1, actions.Where(x => x != null).Select(x => x!).ToArray(), finisher != null));
        }
        return beats.ToArray();
    }
    internal static string Feature(ArenaMatch match, ArenaEvent e)
    {
        var profile = e.Actor == match.First.Name ? match.First : match.Second;
        bool weapon = e.Ability is "Heavy Weapons" or "Light Weapons" or "Finesse Weapons" or "Two Handed Combat";
        if (weapon && e.Damage > 0 && profile.Abilities.Any(x=>x.Name=="Whirlwind"&&x.Rank>0) &&
            (e.Modifier==null || !profile.Abilities.Any(x=>x.Name==e.Modifier&&ArenaAbilities.Rule(x).Kind is "Charge"))) return "Whirlwind";
        if (e.Modifier != null && profile.Abilities.FirstOrDefault(x=>x.Rank>0&&x.Name==e.Modifier) is {} modifier)
        {
            if (ArenaAbilities.Rule(modifier).Kind=="Critical" && e.Kind!="Critical")return "";
            if (ArenaAbilities.Rule(modifier).Kind is "Damage" or "VoidPower" or "SpellPower" or "RangedPower" or "SummonPower" or "Passive") return "";
            return e.Modifier;
        }
        return profile.Abilities.Any(x => x.Rank > 0 && x.Name == e.Ability) ? e.Ability : "";
    }
    internal static string[] Setup(ArenaMatch match,ArenaEvent e)
    {
        if(e.Damage<=0 || e.Target==e.Actor || e.Kind is not ("Hit" or "Critical" or "Sneak Attack" or "Block" or "Echo"))return Array.Empty<string>();
        int at=match.Events.IndexOf(e);if(at<0)return Array.Empty<string>();
        var profile=e.Actor==match.First.Name?match.First:match.Second;
        return match.Events.Take(at).Where(x=>x.Actor==e.Actor&&x.Target==e.Target&&x.Tick>=e.Tick-12&&x.Tick<=e.Tick&&
            x.Kind is "Debuff" or "Blind" && profile.Abilities.Any(a=>a.Rank>0&&a.Name==x.Ability))
            .Select(x=>x.Ability).Distinct().TakeLast(2).ToArray();
    }
    private static double Score(ArenaMatch match, ArenaEvent e, HashSet<string> used)
    {
        if(e.Damage<=0 && e.Kind is "Hit" or "Critical" or "Sneak Attack" or "Echo" or "Acid" or "Poison" or "Thorns" or "Reflect")return -100;
        string feature=Feature(match,e);
        var profile=e.Actor==match.First.Name?match.First:match.Second;
        string kind=profile.Abilities.FirstOrDefault(x=>x.Name==feature) is {} ability?ArenaAbilities.Rule(ability).Kind:"";
        double priority=e.Kind switch {
            "Last Stand"=>3600,"Echo"=>2100,"Reflect"=>1900,"Blind"=>1850,"Debuff"=>1800,
            "Acid"=>1650,"Poison"=>1600,"Summon"=>1550,"Thorns"=>1500,"Riposte"=>1500,
            "Sneak Attack"=>1300,"Critical"=>1200,"Ward"=>50,_=>300
        };
        if(feature=="Whirlwind")priority=1750;
        if(kind is "Charge" or "WeaveBuff")priority=1800;
        if(kind is "Speed" or "Pace" or "Heavy" or "Adrenaline")priority=Math.Min(priority,700);
        priority+=Setup(match,e).Length*700;
        if(feature.Length>0&&used.Contains(e.Actor+":"+feature))priority-=650;
        return priority+Math.Min(100,e.Damage+e.Healing);
    }

    internal static string Id(ArenaBeat beat, int action) => beat.Round + ":" + action;
    internal static string[] Updates(ArenaMatch match, IReadOnlyDictionary<string, string>? prose = null)
    {
        string[] disclaimers = { "For laughs only, not a real fight simulator.", "Make-believe for laughs, not real combat math.", "Just a fun exhibition, not a real fight prediction." };
        uint variety = unchecked((uint)match.Seed);
        var lines = new List<string> { $"KEYFER'S FUN ARENA: {match.First.Name} vs {match.Second.Name}. {disclaimers[variety % 3]}" };
        foreach (var beat in Beats(match))
        {
            string[] descriptions = beat.Actions.Select((e, i) => prose != null && prose.TryGetValue(Id(beat, i), out var text) ? text : Describe(match, e, (int)((variety + (uint)beat.Round + (uint)i) % 3))).ToArray();
            // One slang punchline per round is enough; prefer the last action
            // (the actual finisher in a knockout round) when both sides use it.
            bool slangUsed=false;
            for(int i=descriptions.Length-1;i>=0;i--)
            {
                string actor=beat.Actions[i].Actor;
                string tail=descriptions[i].Substring(actor.Length);
                if(!System.Text.RegularExpressions.Regex.IsMatch(tail,@"\b(?:git gud|gb2ff|skill issue)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase))continue;
                if(!slangUsed){slangUsed=true;continue;}
                descriptions[i]=actor+System.Text.RegularExpressions.Regex.Replace(tail,@"[;,]?\s*(?:git gud|gb2ff|skill issue)(?: or bring a helmet)?", "",System.Text.RegularExpressions.RegexOptions.IgnoreCase).TrimEnd();
            }
            string Join(string[] parts) => $"ROUND {beat.Round}: " + string.Join("; ", parts) + ".";
            string line = Join(descriptions);
            if (line.Length > 180) line = Join(beat.Actions.Select(e => Brief(match, e)).ToArray());
            if (line.Length > 180) line = Join(beat.Actions.Select(Tiny).ToArray());
            if (match.Surprise?.Round == beat.Round)
            {
                string twist=match.Surprise.Text;
                string compact=Join(beat.Actions.Select(e=>Brief(match,e)).ToArray());
                if(line.Length+twist.Length+1<=180)line+=" "+twist;
                else if(compact.Length+twist.Length+1<=180)line=compact+" "+twist;
                else if(compact.Length+24<=180)line=compact+" A drudge eats the bell.";
            }
            if (beat.Knockout && line.Length + 10 <= 180) line += " Knockout!";
            lines.Add(line);
        }
        lines.Add(match.Winner.Length == 0 ? "SIM RESULT: A DRAW. My dramatic finale is ruined." :
            $"SIM RESULT: {match.Winner} WINS by {match.Finish}! Remaining HP {match.FirstHealth:0}/{match.SecondHealth:0} ({match.First.Name}/{match.Second.Name}).");
        double remaining=match.Winner==match.First.Name?match.FirstHealth:match.SecondHealth;
        if(match.Winner.Length>0 && remaining>0 && remaining<=5 && lines[lines.Count-1].Length+8<=180)lines[lines.Count-1]+=" Clutch!";
        return lines.Select(Limit).ToArray();
    }
    internal static string Attack(ArenaEvent e)
    {
        if(e.Kind=="Void DoT") return "void curse";
        string source = e.SourceAttack ?? e.Ability;
        string[] forms = { "bolt", "arc", "streak", "blast" };
        return source switch {
            "Void Magic" => "void " + forms[e.Tick % forms.Length],
            "War Magic" => "war " + forms[e.Tick % forms.Length],
            "Life Magic" => "life spell", "Finesse Weapons" => "blade",
            "Two Handed Combat" => "two-handed swing", "Heavy Weapons" => "heavy swing",
            "Light Weapons" => "weapon strike", "Missile Weapons" => "missile volley",
            "Summoning" => "pet strike", _ => "strike"
        };
    }
    private static string Describe(ArenaMatch match, ArenaEvent e, int variant)
    {
        string a=e.Actor,n=Amount(e.Damage),f=Feature(match,e),attack=Attack(e);
        string Pick(params string[] choices)=>choices[variant%choices.Length];
        if(e.Damage<=0 && e.Kind is "Hit" or "Critical" or "Sneak Attack" or "Echo" or "Acid" or "Poison" or "Thorns" or "Reflect")
            return e.Kind=="Acid"?$"{a}'s Acid Proc wound ticks for nothing; {e.Target} won't subscribe":$"{a}'s {attack} can't get through; rough crowd";
        string[] setup=Setup(match,e);
        if(setup.Length>0)
        {
            string sequence=string.Join(", ",setup.Select(x=>x=="Break Armor"?"Break Armor imp":x=="Sundermark"?"Sundermark vuln":x=="Pocket Sand"?"Pocket Sand blind":x));
            string delivery=f=="Whirlwind"?"Whirlwind to the face":e.Kind=="Echo"?"a doubled "+attack:attack;
            return Pick($"{a}: {sequence}, then {delivery}; helmet optional",$"{a}: {sequence} into {delivery}; git gud",$"{a}: {sequence}, then {delivery}; gb2ff");
        }
        if(f=="Whirlwind")return Pick($"{a} Whirlwinds a two-handed swing to the face; skill issue",$"{a}'s Whirlwind swing rearranges the dental records; gb2ff",$"{a} sweeps in with Whirlwind; git gud or bring a helmet");
        if(e.Modifier!=null && f.Length>0)
        {
            var p=e.Actor==match.First.Name?match.First:match.Second;
            string kind=ArenaAbilities.Rule(p.Abilities.First(x=>x.Name==f)).Kind;
            return kind switch {
                "Speed"=>Pick($"{a}'s frenzied attacks blur; the cutlery files for overtime",$"{a}'s Frenzy quickens the stabs; the air files a grievance",$"{a}'s frenzied attacks turn footwork into tap-dancing"),
                "Pace"=>Pick($"{a}'s Mana Rush speeds the {attack}s; no queueing",$"{a} chains {attack}s faster with Mana Rush; wizard espresso",$"{a}'s Mana Rush quickens casting; the robe needs brakes"),
                "WeaveBuff"=>$"{a}'s Spellweave empowers the next {attack}; rude teamwork",
                "Charge"=>$"{a} cashes Kinetic Charge into a swing; five grudges due",
                "Heavy"=>Pick($"{a}'s {f}-backed swing rearranges the seating",$"{a} puts {f} behind a swing; the helmet rings for service",$"{a}'s {f} powers a swing with structural consequences"),
                "Adrenaline"=>$"{a} lands an Adrenaline-fueled {attack}; sit down",
                "Critical"=>Pick($"{a} cashes Killer Instinct into a {attack} crit; teeth scatter",$"{a}'s Killer Instinct feeds a {attack} crit; dental raid",$"{a} exploits Killer Instinct Openings; {attack} crit, git gud"),
                _=>Pick($"{a}'s {f} empowers the {attack}; the referee ducks",$"{a}'s {f}-boosted {attack} puts the stools in formation",$"{a} backs a {attack} with {f}; teeth consider relocation")
            };
        }
        return e.Kind switch {
            "Echo"=>Pick($"{a}'s Echo Cast sends a second full {attack}; wizard karaoke",$"{a}'s {attack} lands twice via Echo Cast; double tap, skill issue",$"{a}'s Echo Cast repeats the {attack}; the first was a warning"),
            "Void DoT"=>$"{a}'s void curse ticks for {n}; even the ailment wants overtime",
            "Acid"=>Pick($"{a}'s Acid Proc wound ticks for {n}; spicy instalments",$"{a}'s Acid Proc ticks for {n}; the wound has office hours",$"{a}'s Acid Proc ticks for {n}; cleanse that, skill issue"),
            "Poison"=>Pick($"{a}'s Poison Weapon makes the stab a health-code violation",$"{a}'s Poison Weapon adds {n}; now the wound needs a permit",$"{a}'s Poison Weapon leaves a deeply unhelpful aftertaste"),
            "Reflect"=>$"{a} reflects {n}; the projectile finds its return address",
            "Thorns"=>$"{a}'s Thorns returns {n}; hugging has been discontinued",
            "Debuff"=>e.Ability=="Sundermark"?$"{a}'s Sundermark applies Vulnerability; a terrible new hobby":$"{a}'s {e.Ability} applies Imperil; armor gets performance anxiety",
            "Blind"=>$"{a} flings Pocket Sand; can't hit what you can't see, git gud",
            "Resist"=>$"{a}'s {attack} is resisted; the spell needs references",
            "Parry"=>$"{a} parries; the blade is redirected to customer services",
            "Ward"=>e.Ability=="Mana Barrier"?$"{a}'s Mana Barrier diverts damage into mana; robe insurance":$"{a}'s {e.Ability} absorbs the indignity; mostly",
            "Last Stand"=>$"{a}'s Berserk Rage leaves one HP; the obituary is shredded",
            "Heal"=>$"{a}'s {e.Ability} restores {Amount(e.Healing)} HP; medically irritating",
            "Summon"=>e.Ability=="Summon 2x"?$"{a}'s second pet joins in; Summon 2x forgot the guest limit":$"{a}'s pet lands {n}; obedience school denies involvement",
            "Block"=>$"{a}'s {attack} meets a full block; terrible door policy",
            "Evade"=>Pick($"{a} misses; the floor ducks anyway",$"{a} threatens an innocent pocket of air",$"{a}'s swing attends the wrong postcode"),
            "Sneak Attack"=>$"{a} sneaks a {n}-point {attack} past the dress code",
            "Critical"=>$"{a}'s {attack} crits for {n}; helmet diff, skill issue",
            _=>Pick($"{a} lands a {attack} for {n}; dignity leaves by portal",$"{a}'s {attack} lands {n}; the cheap seats flinch",$"{a} lands {attack} for {n}; someone fetch the emotional healer")
        };
    }
    private static string Brief(ArenaMatch match, ArenaEvent e)
    {
        string feature = Feature(match, e), n = Amount(e.Damage);
        var profile = e.Actor == match.First.Name ? match.First : match.Second;
        var ability = profile.Abilities.FirstOrDefault(x => x.Name == feature);
        var setup=Setup(match,e);
        if(setup.Length>0)return $"{e.Actor}: "+string.Join(" + ",setup.Select(x=>x=="Break Armor"?"imp":x=="Sundermark"?"vuln":"blind"))+" -> "+(feature=="Whirlwind"?"Whirlwind":Attack(e));
        if(feature=="Whirlwind")return $"{e.Actor}: Whirlwind swing ({n})";
        if (ability != null && ArenaAbilities.Rule(ability).Role is "attack-speed buff" or "casting-speed buff") return $"{e.Actor} quickens {Attack(e)}s with {feature}";
        if (ability != null && e.Damage > 0 && (ArenaAbilities.Rule(ability).Role.Contains("modifier",StringComparison.Ordinal) || ArenaAbilities.Rule(ability).Role.Contains("buff",StringComparison.Ordinal)))
            return $"{e.Actor}: {feature}-boosted {Attack(e)} ({n})";
        if (e.Kind == "Reflect") return $"{e.Actor} reflects {n}";
        if (e.Kind == "Echo") return $"{e.Actor}: Echo Cast repeats {Attack(e)} ({n})";
        if (e.Kind == "Summon" && e.Ability == "Summon 2x") return $"{e.Actor}'s second pet hits {n} (Summon 2x)";
        if (feature.Length > 0) return e.Healing > 0 ? $"{e.Actor}: {feature} heals {Amount(e.Healing)}" : e.Damage > 0 ? $"{e.Actor}: {feature} for {n}" : $"{e.Actor}: {feature}";
        return e.Kind switch {
            "Last Stand" => $"{e.Actor} makes a last stand", "Heal" => $"{e.Actor} heals {Amount(e.Healing)} HP",
            "Evade" => $"{e.Actor} misses", "Blind" => $"{e.Actor} blinds the foe", "Debuff" => $"{e.Actor} weakens defenses",
            "Reflect" => $"{e.Actor} reflects {n}", "Critical" => $"{e.Actor} crits for {n}", "Sneak Attack" => $"{e.Actor} sneaks for {n}",
            _ => $"{e.Actor} lands {Attack(e)} for {n}"
        };
    }
    private static string Tiny(ArenaEvent e) => e.Healing > 0 ? $"{e.Actor}: +{Amount(e.Healing)} HP" : e.Damage > 0 ? $"{e.Actor}: {Amount(e.Damage)} damage" : $"{e.Actor}: {e.Kind}";
    internal static string Amount(double value) => value.ToString(value > 0 && value < 1 ? "0.##" : "0", System.Globalization.CultureInfo.InvariantCulture);
    private static string Limit(string text) { if (text.Length <= 180) return text; int end = text.LastIndexOf(' ', 176); return text.Substring(0, end > 0 ? end : 176) + "..."; }
}
