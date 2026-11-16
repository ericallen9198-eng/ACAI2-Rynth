namespace ACAI2.Bridge;

// Descriptions supplied by the user from the DreamWeave class planner.
// These establish ability meaning, not the exhibition's damage formulas.
internal static class ArenaAbilityDescriptions
{
    internal static readonly Dictionary<string,string> Text = new(StringComparer.OrdinalIgnoreCase) {
        ["Poison Weapon"] = "Your weapon strikes deal separate poison damage on every landed hit. Ranks in Acid Proc raise it further. The poison scales like the hit that carried it: your damage and critical damage ratings, the target's damage resistance, and on a critical hit the critical multiplier and Killer Instinct. Extra Multishot arrows and Riposte counters carry less poison, in line with their damage.",
        ["Acid Proc"] = "Your poisoned hits have a 30% chance to open a caustic wound: acid damage over 3 ticks, equal to your Poison Weapon damage. Needs Poison Weapon. A new proc on an open wound resets it to 3 ticks instead of adding one. Ranks also raise all your poison damage, from Poison Weapon hits and acid ticks alike. A wound scales like the hit that last opened or reset it: damage ratings, the target's resistance, critical bonuses, and the lower damage of Multishot extra arrows and Riposte counters.",
        ["Frenzy"] = "Each melee hit you land (any melee style) raises your attack speed by 2.5%. The stacks reset after 15 seconds without a hit.",
        ["Heavy Blows"] = "Your power bar hits harder in melee. A full-power swing normally deals 150% damage, and Heavy Blows raises that. Lower power settings gain less, and a zero-power swing is unchanged.",
        ["Break Armor"] = "Your melee and missile hits have a 15% chance to shatter the target's guard and cast Imperil on it. It cannot be resisted.",
        ["Executioner"] = "Your melee hits deal more damage to targets below 25% health.",
        ["Opportunist"] = "You deal more damage to any target with a debuff or crowd control from you or a fellow: Vulnerability, Imperil, blindness, taunts or Weakened Blood.",
        ["Killer Instinct"] = "Each critical hit gives you an Opening for 8 seconds, stacking up to 3. Each Opening adds critical damage and 2% critical chance. A Sneak Attack that lands gives an Opening even without a critical.",
        ["Riposte"] = "When you Parry a melee attack, you strike back for free. The counter is a real hit, so Poison Weapon and Acid Proc apply, scaled to its damage. Requires Parry.",
        ["Rogue Training"] = "Raises your base Lockpick, Alchemy, Sneak Attack and Deception. Counts toward wield requirements.",
        ["Pocket Sand"] = "When you avoid an attack (evade, parry, block or resist a spell), you can fling sand in the attacker's eyes, lowering all its attack skills by 40 for 20 seconds. While it is blinded, any attack on it from someone with Sneak Attack trained counts as a Sneak Attack. Stacks with Dirty Fighting's Blinding Assault.",
        ["Adrenaline"] = "When you take damage, your next attack or spell within 6 seconds deals more damage.",
        ["Berserk Rage"] = "When a creature's attack, spell or damage over time would kill you, you stay at 1 health instead. For 5 seconds you take no damage and deal 50% more. Environmental hazards never trigger it and are not blocked by it.",
        ["Damage Rating"] = "Raises your Damage Rating, which boosts all damage you deal. It also adds part of your total Critical Damage Rating to your Damage Rating.",
        ["Whirlwind"] = "Your melee swings (any melee style) hit in a full circle and strike extra targets on top of your weapon's own cleave, even if it has none. Each Whirlwind swing costs extra stamina, and with too little stamina you swing normally.",
        ["Archmage Training"] = "Raises your base Mana Conversion, Arcane Lore and Magic Item Tinkering. Counts toward wield requirements.",
        ["Spell AOE"] = "When one of your Arc spells, from any school, damages a target, it also blasts out from that target to every other enemy nearby, with no limit on targets. These casts cost extra mana.",
        ["Overchannel"] = "Your damaging spells from any school deal more damage. Boosted casts cost +100% mana; with Spell AOE, an Arc cast costs up to +200%.",
        ["Echo Cast"] = "Your damaging spell projectiles from any school can cast again at the same target for free, with no mana and no wind-up. The echo is a full second cast with all your bonuses, and Spell AOE can spread it. An echo never echoes.",
        ["Mana Barrier"] = "Part of the damage you take comes out of your Mana instead of your Health, up to 40%. It stops at 50 Mana, so it never drains you below that.",
        ["Mana Rush"] = "Casting spells from any school builds Mana Rush stacks that speed up your casting, up to 5 stacks and +50% cast speed. Stacks reset after 20 seconds without a cast.",
        ["Empowered Summons"] = "Raises your base Summoning skill. Counts toward wield requirements.",
        ["Spellweave"] = "Casting any spell, procs included, makes your next weapon hit deal more damage. Landing a weapon hit makes your next spell deal more.",
        ["Runic Ward"] = "Each weapon hit you land adds part of its damage to a ward, up to a share of your maximum health. The ward absorbs damage from creatures and lapses after 12 seconds without a weapon hit. While it holds, it also works as a shield against creatures' physical attacks from the front and adds its shield level to what Thorns reflects.",
        ["Sundermark"] = "Your weapon hits and damaging spell projectiles can put the matching element's Vulnerability on the target. Your Life Magic sets its level. A Nether hit puts a random void curse on the target instead (Corruption, Corrosion or Destructive Curse), at the level your Life Magic sets.",
        ["Thorns"] = "While you have a shield equipped, creatures within 8 metres that hit you with melee, missiles or spells take damage back, based on your shield's armor level. While a Runic Ward or Sanguine Ward stands, its shield level adds to that armor level.",
        ["Reflect"] = "Incoming projectiles that would hit you (arrows, bolts, thrown weapons, war bolts and void projectiles) can bounce straight back at whoever fired them. You take nothing, and the projectile hits them instead. After a creature's spell damages you, you are also briefly immune to creature spell damage.",
        ["Shield Block"] = "While a shield is equipped, some incoming hits are fully blocked. It shares a 50% cap with Parry. Blocked hits still trigger Thorns.",
        ["Kinetic Charge"] = "Every attack that reaches you builds a charge, whatever its damage type and whether it lands, is blocked or is parried. At 5 charges your next attack spends them all for extra damage. A melee attack that spends them also cleaves up to 2 more nearby enemies, even with a weapon that cannot cleave, and each of them takes the extra damage too. Charges fade after 10 seconds without being hit.",
        ["Cloaked in Power"] = "Your cloak's spell proc always has at least a 10% chance to fire on a hit that reaches you, however small the hit. It does not shorten the cloak's cooldown, and an unleveled cloak still never procs. When the cloak procs, your Thorns reflects as a critical hit for a few seconds, including on the hit that triggered it. A critical reflect deals double damage, and Critical Damage Rating adds to that.",
        ["Bloodlust"] = "Your melee hits heal you for part of the damage you deal.",
        ["Void Damage"] = "Raises your void magic damage.",
        ["Summon 2x"] = "Your summoning essence calls two combat pets for one charge.",
    };
}
