#nullable disable
// Ported from ACAI2 fa363107; see origin/ACAI2-SOURCE.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ACAI2.Rynth.Combat.Core
{
    public sealed class CombatEventRecord
    {
        public DateTime TimestampUtc;
        public string Server = "";
        public string InstanceId = "";
        public string Character = "";
        public string SessionId = "";
        public string TestLabel = "";
        public string Direction = "";
        public string Mob = "";
        public string Weapon = "";
        public int WeaponGuid;
        public bool WeaponTransition;
        public string AttackType = "";
        public string Result = "";
        public string Element = "Unknown";
        public int Damage;
        public bool Critical;
        public bool RecklessTagged;
        public bool SneakAttack;
        public string Component = "Direct";
        public string Summon = "";
        public string Ability = "";
        public string Recipient = "";
        public int Amount;
        public string Detail = "";
        public string Raw = "";
    }

    public sealed class CombatMessageParser
    {
        private static readonly Regex Poison = Rx("^"+Prefix+@"Your poison deals (?<damage>[\d,]+) additional points of damage to (?<mob>.+)!$");
        private static readonly Regex Acid = Rx("^"+Prefix+@"Your acid deals (?<damage>[\d,]+) points of periodic damage to (?<mob>.+)!$");
        private static readonly Regex Thorns = Rx("^"+Prefix+@"Your thorns reflect (?<damage>[\d,]+) points of damage back at (?<mob>.+)!$");
        private static readonly Regex PetHit = Rx("^"+Prefix+@"(?<owner>.+?)'s (?<pet>.+?) hits (?<mob>.+?) for (?<damage>[\d,]+) damage!$");
        private static readonly Regex DrainSelf = Rx("^"+Prefix+@"You gain (?<amount>[\d,]+) points of health due to casting Drain Health Other I on (?<mob>.+?)[.!]?$");
        private static readonly Regex DrainPet = Rx("^"+Prefix+@"Your (?<owner>.+?)'s (?<pet>.+?) gains (?<amount>[\d,]+) points of health due to your Drain Health Other I on (?<mob>.+?)[.!]?$");
        private static readonly Regex PeriodicHeal = Rx("^"+Prefix+@"You receive (?<amount>[\d,]+) points of periodic healing\.$");
        private static readonly Regex PeriodicTaken = Rx("^"+Prefix+@"You receive (?<damage>[\d,]+) points? of periodic (?<element>[A-Za-z]+) damage[.!]$");
        private static readonly Regex KitHeal = Rx("^"+Prefix+@"You heal yourself for (?<amount>[\d,]+) Health points\. Your .+$");
        private static readonly Regex Block = Rx(@"^You block (?<mob>.+?)'s attack with your shield!$");
        private static readonly Regex ChargeReady = Rx(@"^Kinetic charge at full \((?<amount>\d+)/\d+\) - your next attack spends it\.$");
        private static readonly Regex ChargeRelease = Rx(@"^You release your kinetic charge! \((?<detail>.+)\)$");
        private static readonly Regex Taunt = Rx(@"^Your (?<ability>cloak's woven magic|surging aetheria) taunts (?<amount>\d+) nearby monsters? into attacking you!$");
        private static readonly Regex Dirty = Rx(@"^Dirty Fighting! (?<owner>.+?) delivers a (?<ability>Blinding Blow|Traumatic Blow|Bleeding Blow) to (?<mob>.+)!$");
        private static readonly Regex Sand = Rx(@"^You fling sand in (?<mob>.+?)'s eyes! \((?<detail>.+)\)$");

        private static readonly Regex OutgoingEvade = Rx(@"^(?<mob>.+?) evaded your attack\.$");
        private static readonly Regex IncomingEvade = Rx(@"^You evaded (?<mob>.+)!$");
        private static readonly Regex OutgoingResist = Rx(@"^(?<mob>.+?) resists your spell[.!]?$");
        private static readonly Regex IncomingResist = Rx(@"^You resist the spell cast by (?<mob>.+?)[.!]?$");

        internal const string Prefix = @"(?:(?:Critical hit!|Critical heal!|Sneak Attack!|Recklessness!|Reckless!|Overpower!)\s*)*";
        private static readonly Regex OutgoingPhysical = Rx("^" + Prefix + @"You\s+\w+\s+(?<mob>.+?)\s+for\s+(?<damage>[\d,]+)\s+points?\s+of\s+(?<element>[A-Za-z]+)\s+damage.*$");
        private static readonly Regex OutgoingMagic = Rx("^" + Prefix + @"You\s+(?<verb>\w+)\s+(?<mob>.+?)\s+for\s+(?<damage>[\d,]+)\s+points?\s+with\s+.+$");
        private static readonly Regex IncomingPhysical = Rx("^" + Prefix + @"(?<mob>.+?)\s+\w+\s+your\s+.+?\s+for\s+(?<damage>[\d,]+)\s+points?\s+of\s+(?<element>[A-Za-z]+)\s+damage.*$");
        private static readonly Regex IncomingMagic = Rx("^" + Prefix + @"(?<mob>.+?)\s+(?<verb>\w+)\s+you\s+for\s+(?<damage>[\d,]+)\s+points?\s+with\s+.+$");
        private static readonly Regex ExplicitOutgoingSpell = Rx("^"+Prefix+@"You\s+\w+\s+(?<mob>.+?)\s+for\s+(?<damage>[\d,]+)\s+points?\s+of\s+(?<element>[A-Za-z]+)\s+damage\s+with\s+.+$");
        private static readonly Regex ExplicitIncomingSpell = Rx("^"+Prefix+@"(?<mob>.+?)\s+\w+\s+(?:you|your\s+.+?)\s+for\s+(?<damage>[\d,]+)\s+points?\s+of\s+(?<element>[A-Za-z]+)\s+damage\s+with\s+.+$");
        private static readonly Regex IncomingLoseHealth = Rx(@"^(?:Magical energies|You) (?<verb>lose) (?<damage>[\d,]+) points? of health due to (?<mob>.+?) casting .+$");
        private static readonly Regex IncomingDrain = Rx(@"^(?<mob>.+?) casts .+ and (?<verb>drains) (?<damage>[\d,]+) points?.*$");

        private static readonly Regex[] KillMessages =
        {
            Rx(@"^You flatten (?<mob>.+)'s body with the force of your assault!$"),
            Rx(@"^You bring (?<mob>.+) to a fiery end!$"),
            Rx(@"^You beat (?<mob>.+) to a lifeless pulp!$"),
            Rx(@"^You smite (?<mob>.+) mightily!$"),
            Rx(@"^You obliterate (?<mob>.+)!$"),
            Rx(@"^You run (?<mob>.+) through!$"),
            Rx(@"^You reduce (?<mob>.+) to (?:a sizzling, oozing mass|a drained, twisted corpse)!$"),
            Rx(@"^You knock (?<mob>.+) into next Morningthaw!$"),
            Rx(@"^You (?:split|cleave) (?<mob>.+) (?:apart|in twain)!$"),
            Rx(@"^You slay (?<mob>.+) viciously enough to impart death several times over!$"),
            Rx(@"^You killed (?<mob>.+)!$"),
            Rx(@"^Your killing blow nearly turns (?<mob>.+) inside-out!$"),
            Rx(@"^Your attack stops (?<mob>.+) cold!$"),
            Rx(@"^Your lightning coruscates over (?<mob>.+)'s mortal remains!$"),
            Rx(@"^Your assault sends (?<mob>.+) to an icy death!$"),
            Rx(@"^The thunder of crushing (?<mob>.+) is followed by the deafening silence of death!$"),
            Rx(@"^The deadly force of your attack is so strong that (?<mob>.+)'s ancestors feel it!$"),
            Rx(@"^(?<mob>.+)'s seared corpse smolders before you!$"),
            Rx(@"^(?<mob>.+)'s perforated corpse falls before you!$"),
            Rx(@"^(?<mob>.+)'s death is preceded by a sharp, stabbing pain!$"),
            Rx(@"^(?<mob>.+)'s last strength (?:dissolves|withers) before you!$"),
            Rx(@"^(?<mob>.+) catches your attack, with dire consequences!$"),
            Rx(@"^(?<mob>.+) is (?:reduced to cinders|shattered by your assault|utterly destroyed by your attack|fatally punctured|torn to ribbons by your assault|liquified by your attack|dessicated by your attack|incinerated by your assault)!$"),
            Rx(@"^(?<mob>.+) suffers a frozen fate!$"),
            Rx(@"^Electricity tears (?<mob>.+) apart!$"),
            Rx(@"^Blistered by lightning, (?<mob>.+) falls!$")
        };

        public bool TryParse(string text, string character, out CombatEventRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4000 || string.IsNullOrWhiteSpace(character)) return false;
            string clean = Regex.Replace(text.Trim(), @"^\d{2}:\d{2}:\d{2}\s+", "");
            // Reject chat envelopes before considering quoted combat messages.
            if (Regex.IsMatch(clean, @"^(?:\[[^\]]+\]\s*)?.+?\s+(?:say|says|think|thinks|tells you),\s+""", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))) return false;
            if (!TryParseCore(clean, character, out record)) return false;
            record.Raw = text;
            record.Critical = Regex.IsMatch(Regex.Match(clean,"^"+Prefix).Value,@"Critical (?:hit|heal)!",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100));
            record.RecklessTagged = Regex.IsMatch(Regex.Match(clean,"^"+Prefix).Value,@"Reckless(?:ness)?!",RegexOptions.IgnoreCase);
            record.SneakAttack = record.Direction == "Outgoing" && record.Result == "Damage" && CombatComponents.IsDirect(record) &&
                Regex.IsMatch(clean, @"^(?:(?:Critical hit!|Recklessness!|Reckless!|Overpower!)\s*)*Sneak Attack!", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
            return true;
        }
        private bool TryParseCore(string text, string character, out CombatEventRecord record)
        {
            record = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            Match match;
            if ((match = Poison.Match(text)).Success) return Special(text, match, "Poison", "Outgoing", "Damage", "Unknown", out record);
            if ((match = Acid.Match(text)).Success) return Special(text, match, "Acid", "Outgoing", "Damage", "Acid", out record);
            if ((match = Thorns.Match(text)).Success) return Special(text, match, "Thorns", "Outgoing", "Damage", "Unknown", out record);
            if ((match = PetHit.Match(text)).Success)
            {
                if (!match.Groups["owner"].Value.Equals(character, StringComparison.OrdinalIgnoreCase)) return false;
                return Special(text, match, "Summons", "Outgoing", "Damage", "Unknown", out record);
            }
            if ((match = DrainSelf.Match(text)).Success)
            {
                if (!Special(text, match, "Drain", "Healing", "Healing", "None", out record)) return false;
                record.Ability = "Drain Health Other I"; record.Recipient = character; return true;
            }
            if ((match = DrainPet.Match(text)).Success)
            {
                if (!match.Groups["owner"].Value.Equals(character, StringComparison.OrdinalIgnoreCase)) return false;
                if (!Special(text, match, "Summons", "Healing", "Healing", "None", out record)) return false;
                record.Ability = "Drain Health Other I"; record.Recipient = record.Summon; return true;
            }
            if ((match = PeriodicHeal.Match(text)).Success)
            {
                if (!Special(text, match, "PeriodicHeal", "Healing", "Healing", "None", out record)) return false;
                record.Recipient = character; record.Ability = "Periodic healing (source unspecified)"; return true;
            }
            if ((match = PeriodicTaken.Match(text)).Success)
            {
                if(!Special(text,match,"PeriodicDamage","Incoming","Damage",NormalizeElement(match.Groups["element"].Value),out record))return false;
                record.AttackType="Periodic";return true;
            }
            if ((match = KitHeal.Match(text)).Success)
            {
                if (!Special(text, match, "Recovery", "Healing", "Healing", "None", out record)) return false; record.Recipient = character; return true;
            }
            if ((match = Block.Match(text)).Success) return Special(text, match, "Defense", "Incoming", "Block", "None", out record);
            if ((match = ChargeReady.Match(text)).Success) return Special(text, match, "Kinetic", "Utility", "ChargeReady", "None", out record);
            if ((match = ChargeRelease.Match(text)).Success) return Special(text, match, "Kinetic", "Utility", "ChargeReleased", "None", out record);
            if (text.Equals("Your kinetic charge dissipates.", StringComparison.OrdinalIgnoreCase))
            { Make(text, "Utility", "", "Utility", "ChargeExpired", "None", 0, out record); record.Component = "Kinetic"; return true; }
            if ((match = Taunt.Match(text)).Success) return Special(text, match, "Taunts", "Utility", "Taunt", "None", out record);
            if ((match = Dirty.Match(text)).Success)
            {
                if (!match.Groups["owner"].Value.Equals(character, StringComparison.OrdinalIgnoreCase)) return false;
                return Special(text, match, "Debuffs", "Utility", "Debuff", "None", out record);
            }
            if ((match = Sand.Match(text)).Success) return Special(text, match, "Debuffs", "Utility", "Debuff", "None", out record);
            if (text.Equals("You are dead!", StringComparison.OrdinalIgnoreCase) || text.Equals("You have died!", StringComparison.OrdinalIgnoreCase))
            { Make(text, "Incoming", "", "Unknown", "Death", "None", 0, out record); return true; }
            if ((match = IncomingEvade.Match(text)).Success)
                return Make(text, "Incoming", match.Groups["mob"].Value, "Melee/Missile", "Evade", "None", 0, out record);
            if ((match = OutgoingEvade.Match(text)).Success)
                return Make(text, "Outgoing", match.Groups["mob"].Value, "Melee/Missile", "Evade", "None", 0, out record);
            if ((match = IncomingResist.Match(text)).Success)
                return Make(text, "Incoming", match.Groups["mob"].Value, "Magic", "Resist", "None", 0, out record);
            if ((match = OutgoingResist.Match(text)).Success)
                return Make(text, "Outgoing", match.Groups["mob"].Value, "Magic", "Resist", "None", 0, out record);

            if ((match = ExplicitOutgoingSpell.Match(text)).Success)return MakeDamage(text,match,"Outgoing","Magic",true,out record);
            if ((match = ExplicitIncomingSpell.Match(text)).Success)return MakeDamage(text,match,"Incoming","Magic",true,out record);
            if ((match = OutgoingPhysical.Match(text)).Success)
                return MakeDamage(text, match, "Outgoing", "Melee/Missile", true, out record);
            if ((match = OutgoingMagic.Match(text)).Success)
                return MakeDamage(text, match, "Outgoing", "Magic", false, out record);
            if ((match = IncomingPhysical.Match(text)).Success)
                return MakeDamage(text, match, "Incoming", "Melee/Missile", true, out record);
            if ((match = IncomingMagic.Match(text)).Success)
                return MakeDamage(text, match, "Incoming", "Magic", false, out record);
            if ((match = IncomingLoseHealth.Match(text)).Success)
                return MakeDamage(text, match, "Incoming", "Magic", false, out record);
            if ((match = IncomingDrain.Match(text)).Success)
                return MakeDamage(text, match, "Incoming", "Magic", false, out record);

            foreach (Regex kill in KillMessages)
            {
                match = kill.Match(text);
                if (match.Success)
                    return Make(text, "Outgoing", match.Groups["mob"].Value, "Unknown", "Kill", "Unknown", 0, out record);
            }

            return false;
        }

        public bool LooksLikeCombat(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string lower = text.ToLowerInvariant();
            if (lower.StartsWith("[acai")) return false;
            return (lower.Contains("you") || lower.Contains("your")) &&
                   (lower.Contains(" damage") || lower.Contains(" point") ||
                    lower.Contains("evade") || lower.Contains("resist") ||
                    lower.Contains("critical hit") || lower.Contains("killing blow"));
        }

        private static bool Special(string text, Match match, string component, string direction, string result, string element, out CombatEventRecord record)
        {
            int damage = 0, amount = 0;
            if (match.Groups["damage"].Success && !int.TryParse(match.Groups["damage"].Value.Replace(",", ""), out damage)) { record = null; return false; }
            if (match.Groups["amount"].Success && !int.TryParse(match.Groups["amount"].Value.Replace(",", ""), out amount)) { record = null; return false; }
            Make(text, direction, match.Groups["mob"].Value, component, result, element, damage, out record);
            record.Component = component; record.Amount = result == "Damage" ? damage : amount;
            record.Summon = match.Groups["pet"].Value; record.Ability = match.Groups["ability"].Value; record.Detail = match.Groups["detail"].Value;
            return true;
        }

        private static bool MakeDamage(
            string text,
            Match match,
            string direction,
            string attackType,
            bool hasExplicitElement,
            out CombatEventRecord record)
        {
            int damage;
            if (!int.TryParse(match.Groups["damage"].Value.Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out damage)) { record = null; return false; }
            string element = hasExplicitElement
                ? NormalizeElement(match.Groups["element"].Value)
                : ElementFromVerb(match.Groups["verb"].Value, text);
            return Make(text, direction, match.Groups["mob"].Value, attackType, "Damage", element, damage, out record);
        }

        private static bool Make(
            string text,
            string direction,
            string mob,
            string attackType,
            string result,
            string element,
            int damage,
            out CombatEventRecord record)
        {
            record = new CombatEventRecord
            {
                Direction = direction,
                Component = direction == "Incoming" ? "Defense" : "Direct",
                Amount = damage,
                Mob = mob.Trim(),
                AttackType = attackType,
                Result = result,
                Element = element,
                Damage = damage,
                Critical = text.IndexOf("Critical hit!", StringComparison.OrdinalIgnoreCase) >= 0,
                Raw = text
            };
            return true;
        }

        private static string NormalizeElement(string value)
        {
            string lower = (value ?? "").ToLowerInvariant();
            if (lower.StartsWith("slash")) return "Slashing";
            if (lower.StartsWith("pierc")) return "Piercing";
            if (lower.StartsWith("bludge")) return "Bludgeoning";
            if (lower.StartsWith("fire")) return "Fire";
            if (lower.StartsWith("cold") || lower.StartsWith("frost")) return "Cold";
            if (lower.StartsWith("acid")) return "Acid";
            if (lower.StartsWith("electric") || lower.StartsWith("lightning")) return "Electrical";
            if (lower.StartsWith("nether")) return "Nether";
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lower);
        }

        private static string ElementFromVerb(string verb, string fullText)
        {
            string text = ((verb ?? "") + " " + (fullText ?? "")).ToLowerInvariant();
            if (HasAny(text, "slash", "cut", "scratch", "mangle")) return "Slashing";
            if (HasAny(text, "pierc", "gore", "impale", "nick", "stab")) return "Piercing";
            if (HasAny(text, "bludge", "smash", "bash", "graze", "crush")) return "Bludgeoning";
            if (HasAny(text, "fire", "burn", "singe", "scorch", "incinerate")) return "Fire";
            if (HasAny(text, "cold", "frost", "chill", "numb", "freeze")) return "Cold";
            if (HasAny(text, "acid", "blister", "sear", "corrode", "dissolve")) return "Acid";
            if (HasAny(text, "electric", "lightning", "jolt", "shock", "spark", "blast")) return "Electrical";
            if (HasAny(text, "eradicate", "wither", "scar", "twist")) return "Nether";
            if (HasAny(text, "deplete", "siphon", "exhaust", "drain", "lose", "health")) return "Typeless/Life";
            return "Unknown";
        }

        private static bool HasAny(string value, params string[] needles)
        {
            return needles.Any(x => value.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static Regex Rx(string pattern)
        {
            return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
    }
}
