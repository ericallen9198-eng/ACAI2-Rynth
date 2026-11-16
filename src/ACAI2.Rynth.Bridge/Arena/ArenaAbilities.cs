namespace ACAI2.Bridge;

// These are GAME DESIGN choices, not claims about server mechanics. Names/ranks come from the sheet.
internal sealed record ArenaAbilityRule(string Name, string Tree, string Kind, string KnownEffect, string Source)
{
    internal string Role => Kind switch {
        "Speed" => "attack-speed buff",
        "Pace" => "casting-speed buff",
        "WeaveBuff" or "Charge" => "conditional damage modifier on an existing attack",
        "Cleave" => "area effect requiring additional targets",
        "Cloak" => "conditional cloak proc and critical Thorns window; equipment not known",
        "Heavy" or "Damage" or "VoidPower" or "DotPower" or "SpellPower" or "Burst" or "RangedPower" or "SummonPower" or "Execute" or "Opportunist" => "damage modifier on an existing attack",
        "Adrenaline" => "conditional buff to the next attack",
        "Critical" => "temporary critical-chance/damage buff",
        "Lifesteal" or "BloodMagic" => "healing triggered by damage",
        "Guard" or "Block" or "Parry" => "defensive mitigation",
        "Blind" or "Imperil" or "Vulnerability" => "debuff",
        "Thorns" or "Reflect" or "Riposte" => "reactive counter-damage",
        "Poison" or "Acid" or "Echo" or "Spellweave" or "Whirlwind" => "triggered damage proc",
        "ExtraSummon" => "additional summon action",
        "Sneak" => "sneak-attack opportunity modifier",
        "Survival" => "survival activation",
        "Passive" => "passive stat enhancement already represented in buffed stats",
        _ => "flavor"
    };
}
internal static class ArenaAbilities
{
    private const string Fiction = "Fictional Keyfer arena adaptation, not a DreamWeave formula";
    private static ArenaAbilityRule R(string name, string tree, string kind, string effect) => new(name, tree, kind, ArenaAbilityDescriptions.Text.GetValueOrDefault(name, effect), ArenaAbilityDescriptions.Text.ContainsKey(name) ? "User-supplied DreamWeave class planner; exhibition numbers remain fictional" : Fiction);
    internal static readonly ArenaAbilityRule[] Rules = {
        R("Poison Weapon", "Rogue", "Poison", "Carrying strikes add poison."),
        R("Acid Proc", "Rogue", "Acid", "Occasional stronger poison follow-up."),
        R("Bloodlust", "Berserker", "Lifesteal", "Strikes recover a little missing health."),
        R("Frenzy", "Berserker", "Speed", "Landed hits build capped attack speed. Frenzy does not deal a separate hit or directly add damage to an individual strike."),
        R("Heavy Blows", "Berserker", "Heavy", "Strengthens an existing melee strike; not a standalone attack."),
        R("Break Armor", "Berserker", "Imperil", "Occasional armor-opening mark."),
        R("Executioner", "Berserker", "Execute", "Extra melee damage against low-health foes."),
        R("Opportunist", "Rogue", "Opportunist", "Bonus against marked foes."),
        R("Killer Instinct", "Rogue", "Critical", "Crits/sneaks build capped, temporary Openings that modify critical chance/damage; not a separate attack."),
        R("Parry", "Rogue", "Parry", "Chance to fully block a melee hit; shared cap with Shield Block."),
        R("Riposte", "Rogue", "Riposte", "Occasional counter after an evade."),
        R("Rogue Training", "Rogue", "Passive", "More opportunities for trained Sneak Attack."),
        R("Pocket Sand", "Rogue", "Blind", "Evading can blind the opponent, enabling trained sneak attacks."),
        R("Adrenaline", "Berserker", "Adrenaline", "Taking damage empowers the next strike for a short time."),
        R("Berserk Rage", "Berserker", "Survival", "One last stand with a brief damage boost."),
        R("Damage Rating", "Berserker", "Damage", "Small capped damage bonus."),
        R("Whirlwind", "Berserker", "Cleave", "Occasional spinning follow-up in the exhibition."),
        R("Archmage Training", "Archmage", "Passive", "Small spell-power bonus."),
        R("Spell AOE", "Archmage", "Cleave", "Splash compressed into a small duel spell bonus."),
        R("Overchannel", "Archmage", "Burst", "More force behind spell hits."),
        R("Echo Cast", "Archmage", "Echo", "Occasional reduced-damage repeated spell."),
        R("Mana Barrier", "Archmage", "Guard", "A small protective ward."),
        R("Mana Rush", "VoidSummon", "Pace", "Quicker recovery between actions."),
        R("Empowered Summons", "VoidSummon", "Passive", "Stronger summon hits."),
        R("Void Damage", "VoidSummon", "VoidPower", "Small spell-power bonus."),
        R("Withering", "VoidSummon", "DotPower", "Void damage-over-time spells tick harder."),
        R("Summon 2x", "VoidSummon", "ExtraSummon", "A second reduced-power summon strike."),
        R("Spellweave", "Spellsword", "WeaveBuff", "Weapon hits can trigger a magical follow-up."),
        R("Runic Ward", "Spellsword", "Guard", "A small protective ward."),
        R("Sundermark", "Spellsword", "Vulnerability", "Occasional armor-opening mark."),
        R("Thorns", "Vanguard", "Thorns", "Returns a little melee damage."),
        R("Reflect", "Vanguard", "Reflect", "Returns a little spell damage."),
        R("Shield Block", "Vanguard", "Block", "More chances to block when Shield is trained."),
        R("Kinetic Charge", "Vanguard", "Charge", "Strengthens an existing melee strike; not a standalone attack."),
        R("Cloaked in Power", "Vanguard", "Cloak", "A small protective ward.")
    };
    internal static ArenaAbilityRule Rule(ArenaAbility ability)
    {
        var known = Rules.FirstOrDefault(x => x.Name.Equals(ability.Name, StringComparison.OrdinalIgnoreCase) && x.Tree.Equals(ability.Class, StringComparison.OrdinalIgnoreCase));
        if (known != null) return known;
        // Buffed skills/attributes already include enhancements; avoid applying the same stat increase twice.
        if (ability.Name.StartsWith("Enhanced ", StringComparison.OrdinalIgnoreCase)) return R(ability.Name, ability.Class, "Passive", "Uses the sheet's buffed stats; no second enhancement bonus.");
        return R(ability.Name, ability.Class, "Flair", "Unreviewed ability: no invented mechanics until its description is known.");
    }
    internal static int Rank(ArenaProfile p, string kind) => Math.Min(kind is "WeaveBuff" or "Adrenaline" ? 5 : 3,
        p.Abilities.Where(x => x.Rank > 0 && Rule(x).Kind == kind).Select(x => x.Rank).DefaultIfEmpty().Max());
    internal static string Name(ArenaProfile p, string kind) => p.Abilities.Where(x => x.Rank > 0 && Rule(x).Kind == kind)
        .OrderByDescending(x => x.Rank).Select(x => x.Name).FirstOrDefault() ?? kind;
    internal static string[] Unmodeled(ArenaProfile p) => p.Abilities.Where(x => x.Rank > 0 && Rule(x).Kind == "Flair").Select(x => x.Class + ": " + x.Name + " " + x.Rank).ToArray();
    internal static string[] Adaptations(ArenaProfile p) => p.Abilities.Where(x => x.Rank > 0).Select(x => p.Name + ": " + x.Name + " " + x.Rank + " -> " + Rule(x).KnownEffect).ToArray();
}
