namespace ACAI2.Bridge;

internal sealed record ArenaEvent(int Tick, string Actor, string Target, string Kind, string Ability, double Damage, double Healing, string? Modifier = null, string? SourceAttack = null);
internal sealed class ArenaMatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Utc { get; set; }
    public string Model { get; set; } = "exhibition-story-3";
    public int Seed { get; set; }
    public ArenaProfile First { get; set; } = new();
    public ArenaProfile Second { get; set; } = new();
    public string Winner { get; set; } = "";
    public string Finish { get; set; } = "";
    public string Channel { get; set; } = "";
    public string CommentaryMode { get; set; } = "scripted";
    public double FirstMomentum { get; set; }
    public double SecondMomentum { get; set; }
    public double FirstHealth { get; set; }
    public double SecondHealth { get; set; }
    public List<ArenaEvent> Events { get; set; } = new();
    public string[] Warnings { get; set; } = Array.Empty<string>();
    public string[] Adaptations { get; set; } = Array.Empty<string>();
    public ArenaSurprise? Surprise { get; set; }
    public string[] Updates { get; set; } = Array.Empty<string>();
}
internal static class ArenaEngine
{
    private sealed class Dice
    {
        private uint state;
        internal Dice(int seed)
        {
            // Avalanche adjacent replay seeds before xorshift; the first two rolls set fighter momentum.
            uint z = unchecked((uint)seed + 0x9E3779B9u);
            z = unchecked((z ^ (z >> 16)) * 0x85EBCA6Bu); z = unchecked((z ^ (z >> 13)) * 0xC2B2AE35u);
            state = z ^ (z >> 16); if (state == 0) state = 1;
        }
        internal double Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state / 4294967296.0; }
    }
    private sealed record Wound(Fighter Source, double Amount, int Remaining, int Due);
    private sealed class Fighter
    {
        internal ArenaProfile P;
        internal double Hp, Next, Momentum = 1;
        internal int Casts, Reached, LastStyle, Hits, Openings, OpeningsUntil, BlindUntil, AdrenalineUntil, RageUntil;
        internal Wound? Acid, Curse;
        internal bool LastStandUsed;
        internal readonly HashSet<string> Noted = new();
        internal bool Imperiled;
        internal ArenaSkill[] Attacks;
        internal Fighter(ArenaProfile p)
        {
            P = p; Hp = p.Health;
            Attacks = p.Skills.Where(x => x.Buffed > 0 && !x.Training.Equals("Untrained", StringComparison.OrdinalIgnoreCase) && AttackNames.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Buffed).ThenBy(x => x.Name, StringComparer.Ordinal).ToArray();
            if (Attacks.Length == 0) throw new InvalidDataException(p.Name + " has no supported trained attack or Summoning skill.");
        }
        internal double Skill(string name) => P.Skills.Where(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !x.Training.Equals("Untrained", StringComparison.OrdinalIgnoreCase)).Select(x => x.Buffed).DefaultIfEmpty().Max();
        internal int Rank(string kind) => ArenaAbilities.Rank(P, kind);
    }
    private static readonly string[] AttackNames = { "Heavy Weapons", "Light Weapons", "Finesse Weapons", "Two Handed Combat", "Missile Weapons", "War Magic", "Void Magic", "Life Magic", "Summoning" };
    internal static ArenaMatch Simulate(ArenaProfile first, ArenaProfile second, int seed, DateTime now)
    {
        first.Validate(first.Name); second.Validate(second.Name);
        if (first.Name.Equals(second.Name, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Pick two different characters.");
        var a = new Fighter(first); var b = new Fighter(second); var dice = new Dice(seed);
        a.Momentum = .75 + dice.Next() * .5; b.Momentum = .75 + dice.Next() * .5;
        var match = new ArenaMatch { Seed = seed, Utc = now, First = first, Second = second, FirstMomentum = a.Momentum, SecondMomentum = b.Momentum };
        match.Adaptations = ArenaAbilities.Adaptations(first).Concat(ArenaAbilities.Adaptations(second)).ToArray();
        match.Warnings = ArenaAbilities.Unmodeled(first).Select(x => first.Name + ": " + x).Concat(ArenaAbilities.Unmodeled(second).Select(x => second.Name + ": " + x)).ToArray();
        for (int tick = 0; tick < 120 && a.Hp > 0 && b.Hp > 0; tick++)
        {
            TickAcid(a, tick, match); TickAcid(b, tick, match); TickCurse(a, tick, match); TickCurse(b, tick, match);
            if (a.Hp <= 0 || b.Hp <= 0) break;
            // Random initiative each tick prevents systematic first-slot advantage.
            if (dice.Next() < .5) { Act(a, b, tick, dice, match); if (b.Hp > 0) Act(b, a, tick, dice, match); }
            else { Act(b, a, tick, dice, match); if (a.Hp > 0) Act(a, b, tick, dice, match); }
        }
        match.FirstHealth = Math.Round(Math.Max(0, a.Hp), 2); match.SecondHealth = Math.Round(Math.Max(0, b.Hp), 2);
        if (a.Hp <= 0 && b.Hp <= 0) { match.Winner = ""; match.Finish = "double knockout"; }
        else if (a.Hp <= 0 || b.Hp <= 0) { match.Winner = a.Hp > 0 ? first.Name : second.Name; match.Finish = "knockout"; }
        else
        {
            double difference = a.Hp / first.Health - b.Hp / second.Health;
            match.Winner = Math.Abs(difference) < .005 ? "" : difference > 0 ? first.Name : second.Name;
            match.Finish = match.Winner.Length == 0 ? "draw at time limit" : "remaining-health percentage at time limit";
        }
        match.Surprise = ArenaSurprise.For(seed);
        match.Updates = ArenaNarrator.Updates(match); return match;
    }
    private static void TickAcid(Fighter target, int tick, ArenaMatch match)
    {
        if (target.Acid is not { } wound || tick < wound.Due || target.Hp <= 0 || wound.Source.Hp <= 0) return;
        Hit(wound.Source, target, tick, "Acid", "Acid Proc", wound.Amount, match);
        target.Acid = wound.Remaining > 1 ? wound with { Remaining = wound.Remaining - 1, Due = tick + 2 } : null;
    }
    private static void TickCurse(Fighter target,int tick,ArenaMatch match)
    {
        if(target.Curse is not {} wound||tick<wound.Due||target.Hp<=0||wound.Source.Hp<=0)return;
        Hit(wound.Source,target,tick,"Void DoT","Void Magic",wound.Amount,match,false,null,"Void Magic");
        target.Curse=wound.Remaining>1?wound with{Remaining=wound.Remaining-1,Due=tick+2}:null;
    }
    private static void Act(Fighter actor, Fighter target, int tick, Dice dice, ArenaMatch match)
    {
        if (actor.Hp <= 0 || target.Hp <= 0 || tick < actor.Next) return;
        var attack = actor.Attacks[actor.Hits % 3 == 2 && actor.Attacks.Length > 1 && actor.Attacks[1].Buffed >= actor.Attacks[0].Buffed * .8 ? 1 : 0];
        bool magic = attack.Name.EndsWith("Magic", StringComparison.OrdinalIgnoreCase);
        bool pet = attack.Name.Equals("Summoning", StringComparison.OrdinalIgnoreCase);
        bool melee = !magic && !pet && !attack.Name.Equals("Missile Weapons", StringComparison.OrdinalIgnoreCase);
        double speed = melee ? Math.Min(.5, actor.Rank("Speed") * Math.Min(5, actor.Hits) * .025) : 0;
        double castSpeed = magic ? Math.Min(.5, actor.Rank("Pace") * Math.Min(5, actor.Casts) * .05) : 0;
        if (magic) actor.Casts++;
        actor.Next = tick + Math.Max(2, (magic ? 5 : 4) * (1 - speed - castSpeed));
        double defense = target.Skill(magic ? "Magic Defense" : attack.Name == "Missile Weapons" ? "Missile Defense" : "Melee Defense");
        if (dice.Next() > Math.Clamp(.7 + (attack.Buffed - defense) / 1100 - (actor.BlindUntil > tick ? .08 : 0), .2, .95))
        {
            match.Events.Add(new(tick, actor.P.Name, target.P.Name, magic ? "Resist" : "Evade", attack.Name, 0, 0));
            if (target.Rank("Blind") > 0 && dice.Next() < target.Rank("Blind") * .12)
            { actor.BlindUntil = tick + 40; match.Events.Add(new(tick, target.P.Name, actor.P.Name, "Blind", ArenaAbilities.Name(target.P, "Blind"), 0, 0)); }
            return;
        }
        if (melee && target.Rank("Parry") > 0 && dice.Next() < target.Rank("Parry") * .08)
        {
            match.Events.Add(new(tick,target.P.Name,actor.P.Name,"Parry","Parry",0,0));
            if(target.Rank("Riposte")>0)
            {
                var weapon=target.Attacks.FirstOrDefault(x=>!x.Name.EndsWith("Magic")&&x.Name!="Summoning"&&x.Name!="Missile Weapons");
                if(weapon!=null) {
                    double counter=Hit(target,actor,tick,"Riposte","Riposte",8+weapon.Buffed*.025,match,true,null,weapon.Name);
                    ApplyPoison(target,actor,counter,tick,dice,match);
                }
            }
            return;
        }
        actor.Hits++;
        if (tick >= actor.OpeningsUntil) actor.Openings = 0;
        bool sneak = melee && actor.Skill("Sneak Attack") > 0 && (target.BlindUntil > tick || dice.Next() < .08 + .035 * actor.Rank("Sneak"));
        bool crit = dice.Next() < .1 + actor.Rank("Critical") * actor.Openings * .008;
        double damage = (12 + attack.Buffed * .065) * (.78 + dice.Next() * .44) * (crit ? 1.65 + actor.Rank("Critical") * actor.Openings * .03 : 1);
        if (sneak) damage *= 1.18;
        damage *= 1 + actor.Rank("Damage") * .02 + (magic ? (attack.Name == "Void Magic" ? actor.Rank("VoidPower") * .025 : 0) + actor.Rank("Burst") * .025 : attack.Name == "Missile Weapons" ? actor.Rank("RangedPower") * .035 : 0);
        bool adrenalineBoost = actor.AdrenalineUntil > tick;
        if (adrenalineBoost) { damage *= 1 + actor.Rank("Adrenaline") * .025; actor.AdrenalineUntil = 0; }
        if (actor.RageUntil > tick) damage *= 1.5;
        bool weave = actor.Rank("WeaveBuff") > 0 && ((magic && actor.LastStyle == 1) || (melee && actor.LastStyle == 2));
        if (weave) damage *= 1 + actor.Rank("WeaveBuff") * .03;
        actor.LastStyle = magic ? 2 : melee ? 1 : 0;
        bool charge = actor.Rank("Charge") > 0 && actor.Reached >= 5;
        if (charge) { damage *= 1.25; actor.Reached = 0; }
        target.Reached++;
        if ((crit || sneak) && actor.Rank("Critical") > 0) { actor.Openings = Math.Min(3, actor.Openings + 1); actor.OpeningsUntil = tick + 16; }
        damage *= actor.Momentum;
        damage *= Math.Clamp(1 + (actor.P.Level - target.P.Level) / 2500.0, .85, 1.15);
        if (melee) damage *= 1 + .04 * actor.Rank("Heavy");
        if (target.Imperiled) damage *= 1.1 + actor.Rank("Opportunist") * .02;
        if (melee && target.Hp / target.P.Health < .25) damage *= 1 + actor.Rank("Execute") * .06;
        bool block = !magic && !pet && target.Skill("Shield") > 0 && dice.Next() < Math.Clamp(target.Skill("Shield") / 2500 + target.Rank("Block") * .04, .05, .45);
        if (block) damage = 0;
        if (target.RageUntil > tick) damage = 0;
        damage *= 1 - target.Rank("Guard") * .025;
        if (pet) damage *= 1 + actor.Rank("SummonPower") * .07;
        var features = new List<string>();
        void Feature(string trait, bool active) { if (active && actor.Rank(trait) > 0) features.Add(ArenaAbilities.Name(actor.P, trait)); }
        Feature("Adrenaline", adrenalineBoost); Feature("Survival", actor.RageUntil > tick);
        Feature("WeaveBuff", weave); Feature("Charge", charge);
        Feature("Speed", speed > 0); Feature("Pace", magic && castSpeed > 0); Feature("Heavy", melee); Feature("Damage", true);
        Feature("VoidPower", attack.Name == "Void Magic"); Feature("SpellPower", magic); Feature("Burst", magic); Feature("RangedPower", attack.Name == "Missile Weapons");
        Feature("Execute", melee && target.Hp / target.P.Health < .25); Feature("Opportunist", target.Imperiled);
        Feature("Critical", crit || sneak); Feature("SummonPower", pet);
        string? modifier = features.Count == 0 ? null : features[(actor.Hits - 1) % features.Count];
        void GuardNote(string trait, bool active)
        {
            if (active && target.Rank(trait) > 0 && target.Noted.Add(trait))
                match.Events.Add(new(tick, target.P.Name, actor.P.Name, "Ward", ArenaAbilities.Name(target.P, trait), 0, 0));
        }
        GuardNote("Guard", true); GuardNote("Block", block);
        bool curse = attack.Name == "Void Magic" && actor.Casts % 4 == 0;
        if(curse) damage = damage * (1 + actor.Rank("DotPower")*.03)/3;
        string kind = block ? "Block" : curse ? "Void DoT" : crit ? "Critical" : sneak ? "Sneak Attack" : pet ? "Summon" : "Hit";
        string ability = pet ? "Summoning" : attack.Name;
        if (!curse && (magic || attack.Name == "Missile Weapons") && target.Rank("Reflect") > 0 && dice.Next() < target.Rank("Reflect") * .08)
        { Hit(target, actor, tick, "Reflect", ArenaAbilities.Name(target.P, "Reflect"), damage, match, false, null, attack.Name); return; }
        double dealt = Hit(actor, target, tick, kind, ability, damage, match, true, modifier);
        if(curse && target.Hp>0 && dealt>0)target.Curse=new(actor,dealt,2,tick+2);
        if (actor.Hp <= 0) return;
        if (melee && actor.Rank("Lifesteal") > 0) Heal(actor, tick, dealt * actor.Rank("Lifesteal") * .025, "Bloodlust", match);
        if (target.Hp <= 0) return;
        if ((melee || attack.Name == "Missile Weapons") && actor.Rank("Imperil") > 0 && dice.Next() < actor.Rank("Imperil") * .05)
        { target.Imperiled = true; match.Events.Add(new(tick, actor.P.Name, target.P.Name, "Debuff", ArenaAbilities.Name(actor.P, "Imperil"), 0, 0)); }
        if (actor.Rank("Vulnerability") > 0 && dice.Next() < .15)
        { target.Imperiled = true; match.Events.Add(new(tick, actor.P.Name, target.P.Name, "Debuff", "Sundermark", 0, 0)); actor.LastStyle = 2; }
        if (melee || attack.Name == "Missile Weapons") ApplyPoison(actor,target,dealt,tick,dice,match);
        if (magic && !curse && target.Hp > 0 && actor.Rank("Echo") > 0 && dice.Next() < actor.Rank("Echo") * .09)
            Hit(actor, target, tick, "Echo", ArenaAbilities.Name(actor.P, "Echo"), dealt, match, true, null, attack.Name);
        if (actor.Hp <= 0) return;
        if (!pet && target.Hp > 0 && actor.Skill("Summoning") > 0 && actor.Hits % 3 == 0)
            Hit(actor, target, tick, "Summon", "Summoning", (8 + actor.Skill("Summoning") * .025) * actor.Momentum * (1 + actor.Rank("SummonPower") * .07), match);
        if (target.Hp > 0 && actor.Skill("Summoning") > 0 && actor.Rank("ExtraSummon") > 0 && (pet || actor.Hits % 3 == 0))
            Hit(actor, target, tick, "Summon", ArenaAbilities.Name(actor.P, "ExtraSummon"), (8 + actor.Skill("Summoning") * .025) * actor.Momentum * (1 + actor.Rank("SummonPower") * .07), match);
        if (magic && target.Hp > 0 && actor.Rank("BloodMagic") > 0)
            Heal(actor, tick, dealt * actor.Rank("BloodMagic") * .025, ArenaAbilities.Name(actor.P, "BloodMagic"), match);
        if (actor.Skill("Life Magic") > 0 && actor.Hp < actor.P.Health * .55 && dice.Next() < .15)
            Heal(actor, tick, actor.Skill("Life Magic") * .035, "Life Magic", match);
    }
    private static void ApplyPoison(Fighter actor,Fighter target,double dealt,int tick,Dice dice,ArenaMatch match)
    {
        if(dealt<=0 || actor.Rank("Poison")==0 || actor.Hp<=0 || target.Hp<=0)return;
        double poison=dealt*.035*actor.Rank("Poison");
        bool acid=actor.Rank("Acid")>0 && dice.Next()<.30;
        Hit(actor,target,tick,"Poison","Poison Weapon",poison,match);
        if(acid && target.Hp>0)target.Acid=new(actor,poison,3,tick+1);
    }
    private static double Hit(Fighter a, Fighter b, int tick, string kind, string ability, double amount, ArenaMatch match, bool retaliate = false, string? modifier = null, string? sourceAttack = null)
    {
        if (a.Hp <= 0 || b.Hp <= 0) return 0;
        if (b.RageUntil > tick) amount = 0;
        double dealt = Math.Min(Math.Max(0, b.Hp), Math.Round(amount, 2)); b.Hp -= dealt;
        match.Events.Add(new(tick, a.P.Name, b.P.Name, kind, ability, dealt, 0, modifier, sourceAttack));
        ReactiveState(b, tick, match);
        if (retaliate && a.Hp > 0)
        {
            string trait = "Thorns";
            if (kind != "Summon" && ability != "Summoning" && b.Rank(trait) > 0)
            {
                double returned = Math.Min(a.Hp, Math.Round(dealt * b.Rank(trait) * .035, 2)); a.Hp -= returned;
                match.Events.Add(new(tick, b.P.Name, a.P.Name, trait, ArenaAbilities.Name(b.P, trait), returned, 0));
                ReactiveState(a, tick, match);
            }
        }
        return dealt;
    }
    private static void ReactiveState(Fighter b, int tick, ArenaMatch match)
    {
        if (b.Rank("Adrenaline") > 0) b.AdrenalineUntil = tick + 12;
        if (b.Hp <= 0 && !b.LastStandUsed && b.Rank("Survival") > 0)
        {
            b.LastStandUsed = true; b.Hp = 1; b.RageUntil = tick + 12;
            match.Events.Add(new(tick, b.P.Name, b.P.Name, "Last Stand", ArenaAbilities.Name(b.P, "Survival"), 0, b.Hp));
        }
    }
    private static void Heal(Fighter a, int tick, double amount, string ability, ArenaMatch match)
    {
        if (a.Hp <= 0) return;
        double healed = Math.Min(a.P.Health - a.Hp, Math.Round(amount, 2)); a.Hp += healed;
        if (healed > 0) match.Events.Add(new(tick, a.P.Name, a.P.Name, "Heal", ability, 0, healed));
    }
}
