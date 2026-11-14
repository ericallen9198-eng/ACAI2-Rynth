# Combat effect reference metadata

`src/ACAI2.Rynth.Combat.Core/Data/EffectCatalog.json` contains 6,248 known retail
spell IDs and names for 49 hostile Imperil/vulnerability spells. It is generated
from the ID/name/negative-spell facts in RynthSuite's `Combat/SpellData.txt` at
commit `44571693d76c3b388e48b1a82f2a0518ecd3aac3`:

https://github.com/tombohar/RynthSuite/blob/44571693d76c3b388e48b1a82f2a0518ecd3aac3/Plugins/RynthCore.Plugin.RynthAi/Combat/SpellData.txt

The upstream repository is MIT licensed; its license is retained in
`THIRD-PARTY-NOTICES/RynthSuite-LICENSE.txt`. No bot implementation or engine file
is copied. The file is embedded in Combat.Core and requires no RynthAi plugin.
Only names ending in Imperil Other or the recognized elemental Vulnerability
Other forms (with optional Roman-numeral rank), and marked negative in the source,
are classified as these effects. The full spell dump is not redistributed.

Unknown server-specific IDs are retained as unclassified in hit context. They do
not justify claiming that the character is clean. Expiration and presence come
from native player-enchantment snapshots/events, never the reference's nominal
spell durations. Buff magnitudes, stacking precedence and caster ownership are
not inferred from this table.
