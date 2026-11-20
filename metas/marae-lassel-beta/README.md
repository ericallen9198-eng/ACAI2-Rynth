# Marae Lassel for DreamWeave

One Rynth meta and Lua panel for **180 Lowlands**, **225 High Country**, and **275 Plateau**.

## Requirements

- **RynthAi with the DreamWeave NPC navigation change.** The KT modes use type-7 NPC waypoints with `#rynth-talk` reply confirmation, including the retry that keeps the waypoint active until the hunter replies. I verified that the stock RynthSuite 2026.11.19.1 source lacks that runtime behavior. Use the DreamWeave-patched RynthAi 2026.11.19.1 build or a compatible build that contains the same NPC navigation changes. This ZIP contains the meta, navs, and panel. Install the compatible Rynth build separately.
- **RynthLua and RynthCore with ImGui script HUDs.** The Lua panel uses the bundled `views`, `imgui`, `storage`, and `rynth.ai` APIs. Script HUDs require RynthCore API v71 or newer. The panel uses the standard Lua API and does not require a separate Lua patch. All meta expressions were found in the stock 2026.11.19.1 RynthAi source.
- **DreamWeave and a working recall.** The included `Get to` navs expect spell 1636 to take you to the Redspire approach. Confirm your own recall destination and camp access. The two higher tiers use named camp portals from there. Keep the separate RynthAi **Recall On Death** setting off because the meta handles the return.
- **Character combat and loot settings.** Select a character profile with an appropriate combat and loot setup. Any profile name works. A separate questing profile keeps the meta's range and other setting changes out of your normal profile. Keep `Treasure Map` in your loot filter if you want map drops saved. AutoCram is turned off.

The survey viewer, route builder, and map overlays are authoring tools. The package does not need them at runtime. The treasure watchdog's one reroll attempt uses DreamWeave's server-side `/rrtm`. If the attempt still fails, the meta abandons that map for the current area and resumes the circuit.

## Install and run

1. Copy `MetaFiles/Marae Lassel.met` into the active `RynthAi/MetaFiles` folder. The included `.af` is editable source.
2. Copy the 19 files in `NavProfiles` into the active `RynthAi/NavProfiles` folder.
3. Copy `Scripts/MaraeLassel` into `RynthLua/Scripts`.
4. Stop the macro, select your preferred character profile, and type `/lua start MaraeLassel`. Use `/marae` to show or hide the panel. Select the mode and areas, then click **Start run**. If you already have the old panel open, reload it with `/lua restart MaraeLassel`.

## Modes

| Mode | Behavior |
|---|---|
| Kill tasks | Runs available KTs in each checked area, in level order. Check **Use maps** beside any selected area to work maps there. All three map toggles start off. |
| Maps only | Choose one area. The meta skips the hunters and continuously repeats that area's dedicated treasure circuit. |

Kill tasks leaves an existing fellowship on arrival at a new hunt area and then tries DreamWeave's `/fship joinlandblock` once when hunting starts in each area. Hunting continues whether a fellowship is available or not. The meta advances after confirmed cooldowns or turn-in. Maps only stays in its selected area until stopped or held. The first death in an area retries camp travel and skips the active map. A second death, a camp travel timeout, or unconfirmed turn-in holds the run for review. **Stop run** stops immediately.

This beta was compiled and checked offline. The combined cross-area flow, panel layout, and treasure circuits still need an in-game run.
