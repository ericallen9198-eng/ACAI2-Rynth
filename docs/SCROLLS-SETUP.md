# ACAI2 Scrollwork — level VIII scroll maker

Version 0.1.1 beta. A separate RynthCore app; no RynthAi DLL replacement or Lua script.

v0.1.1 fixes a first-live-test scan failure: learned spells now come from Rynth’s
thread-safe cached spellbook snapshot, including the check before each craft.
The live per-object spell-query API is not called from the plugin pump.

## Install

1. Close the game, extract the whole ZIP, and double-click **INSTALL-SCROLLS.cmd**.
2. In RynthCore → Plugins → Add Plugin, paste the DLL path copied by the installer. Enable **ACAI2.Scrolls**. Keep RynthAi enabled.
3. Log in and type `/acai2scrolls`. The panel opens automatically on first use.

The default DLL is `%LOCALAPPDATA%\ACAI2\Scrolls\App\ACAI2.Rynth.Scrolls.Plugin.dll`.
The installer also creates PLUGIN-PATH.txt there. It never installs API keys, changes
loot profiles, replaces the engine/RynthAi, or touches other ACAI2 apps.

## Use

1. Click **Rescan**. Choose a school, or All. **Missing only** hides learned spells.
2. Check the scrolls you want, or **Select missing in this view**. Scrolls already
   learned or already in your packs cannot be selected for another copy.
3. Open **Components** for combined Need / Owned / Short counts. **Print component
   list to chat** prints a local list; it does not send a public chat message.
4. Gather the shortages. The app does not buy, withdraw currency or exchange items.
5. Switch to peace mode and stand still. Start with ONE scroll for your first live
   test. Click **Make selected scrolls**.
6. The app pauses the running RynthAi macro, holds background item work, and waits
   for actions to settle. It uses the quill on a Mana Scarab, the infused quill on
   its ink, and the enchanted quill on its glyph. Inventory changes must confirm
   both consumed inputs and the expected output before the next step.
7. Finished scrolls stay in your packs. They are NOT automatically learned. On normal
   completion, only a RynthAi macro paused by this batch is resumed.

Intermediate quills already in your pack are preserved by the shopping calculation:
it lists the raw ingredients for complete selected recipes. A stopped partial recipe
leaves its intermediate in inventory. This first version does not credit existing
intermediates toward the raw component checklist, or resume a partial chain later.

## Stop and interrupted crafting

Click **Stop** or type `/acai2scrolls stop`. If a combine is pending, Stop waits for
its acknowledgment before ending. Movement, combat mode, death, logout, a competing
macro, or loss of the hold stops further dispatch. An unconfirmed combine stays
checkpointed for review; it is never blindly retried. RynthAi remains stopped after
uncertainty or a safety interruption.

If you reconnect or restart with an interrupted checkpoint, inspect the inventory
and any crafting messages first. Check the review box and click **Clear reviewed
checkpoint** only after you understand the outcome. Rescan and select a fresh batch.
This does not resend the previous action. Login never resumes crafting automatically.

Selections, filters, panel position and a maximum of 20 compact completion notes are
saved per server/character under `%LOCALAPPDATA%\ACAI2\Scrolls`. No ordinary chat
transcript or API/provider calls are involved.

## Catalog and first-live-test limits

The supplied wiki tables list 199 active scroll recipes and 6 retired Item recipes.
This build resolves 197 active recipes to ACE spell IDs, item template IDs and three
skill-free, one-item-per-input craft recipes. The 6 retired entries are excluded.
Two Light Weapon Mastery/Ineptitude recipes remain disabled: the ACE patch reference
contains conflicting outputs for the same combination. They are listed under
**Activity & recipes → Recipes awaiting verification**.

Item Tinkering and Missile Weapons have historical glyph templates with identical
names. The catalog selects only the template with a resolved crafting chain;
matching a component's name alone is insufficient. The wiki's “Glyph of Health Regeneration” resolves to ACE's “Glyph of
Regeneration” for Fester. Actual spell IDs are read from the resulting scrolls' Spell
property, rather than inferred from the wiki's Inscription labels.

Reference validation is not a live DreamWeave crafting test. Server modifications
can change results. A different output stops the batch for review. Verify one scroll
before a larger batch. RynthCore must expose spellbook/inventory/item-use APIs and
RynthAi Script, Activity and Yield v1 interfaces. No pending item action is replayed.

## Sources and credit

Recipe selection was transcribed from the user-supplied Asheron's Call Community Wiki
VIII Creature, Item, Life, War and Void Scrolls pages, available under the wiki's
CC-BY-SA terms. Names/formula facts were checked against ACEmulator's public
ACE-World-16PY-Patches data. Changes: page chrome and retired entries removed,
actual spell/template IDs resolved, glyph aliases checked and ambiguous entries
excluded. Each catalog entry retains its source links.

- https://asheron.fandom.com/wiki/Spell_Research
- https://asheron.fandom.com/wiki/VIII_Creature_Scrolls
- https://asheron.fandom.com/wiki/VIII_Item_Scrolls
- https://asheron.fandom.com/wiki/VIII_Life_Scrolls
- https://asheron.fandom.com/wiki/VIII_War_Scrolls
- https://asheron.fandom.com/wiki/VIII_Void_Scrolls
- https://github.com/ACEmulator/ACE-World-16PY-Patches

Immutable reference subtrees: weenies `77cabd01d67c645b5276aea16067525465b91362`;
craft `c5e4a7c49ddca269be8b50b9f90079041128598d`. Downloaded reference files were
verified against their Git blob hashes. Catalog JSON is provided with the package.
