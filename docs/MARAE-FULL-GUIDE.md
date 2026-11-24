# Marae Lassel
Tasks, hunting & treasure maps • Complete player guide • v1.17.0

A standalone ACAI2 app for Rynth, built for DreamWeave’s 180 Lowlands, 225 High Country and 275 Plateau. Choose NPC tasks, continuous hunting, treasure maps, or a mixture of them from one panel.

**Credit to Soot (xSoot / SootAC):** the navigation paths, original mission logic and initial custom RynthAi DLL foundation. ACAI2 brings that work into a standalone app with a native panel, stock-RynthAi compatibility, recovery and cache handling.

## Your first run at a glance
1. Install the app and enable it in RynthCore’s Plugins panel.
2. Type `/acai2marae` in game to open the panel.
3. Select your areas and **Use maps**; leave **Hunt only** OFF for NPC tasks.
4. Enable **2-hour task limit per camp** for a bounded daily task circuit.
5. Click **Start / Resume**. Watch the first NPC visit and treasure map before leaving it unattended.

## Inside this guide
- **2** — Download, requirements and installation
- **3** — Panel controls and choosing your mode
- **4** — NPC tasks, mixed progress and the two-hour limit
- **5** — Treasure maps, mesh travel and the digsite boundary
- **6** — Nearby caches and explicit reward pickup
- **7** — Saving, death, disconnects and recovery
- **8** — Troubleshooting and commands to keep handy

> Important: `/ls` must return your character to the **275 Plateau camp**. The travel plan uses that camp as its hub.

<!-- page -->
# 01 / Install and open
## Download the release
[Download the Windows app ZIP](https://github.com/ericallen9198-eng/ACAI2-Rynth/releases/download/marae-stock-app-v1.17.0/ACAI2-Marae-App-v1.17.0-Windows.zip). This guide describes v1.17.0. The release also includes a [one-page quick-start PDF](https://github.com/ericallen9198-eng/ACAI2-Rynth/releases/download/marae-stock-app-v1.17.0/Marae-Quick-Start-v1.17.0.pdf).

## Before installing
Use an updated RynthCore and RynthAi. First make sure the character can fight, buff and loot normally with your chosen combat and loot profiles. The app coordinates the mission; RynthAi still handles those character activities.

Your character needs access to the selected areas, suitable equipment and supplies. Test `/ls` and confirm it lands at the **275 Plateau camp**. Mesh treasure-map travel is optional and requires RynthNav. No previous treasure-hunting installation, Lua setup, manual nav copying or replacement RynthAi DLL is required.

## Install in five steps
1. Close the game. Right-click the downloaded ZIP and choose **Extract All**. Open the extracted folder.
2. Double-click **INSTALL-MARAE.cmd**. Follow its prompts. It detects Rynth’s paths and offers a folder picker if it cannot find them.
3. If asked, choose the RynthAi data folder containing **MetaFiles / NavProfiles**, or the RynthSuite parent folder when offered. Your installation may be on any drive; it does not have to be on C:.
4. Open RynthCore → **Plugins** → **Add Plugin**. The installer copies the app DLL path to your clipboard; paste it into the file-name box with **Ctrl+V**, then select it.
5. Enable **ACAI2.Marae** and keep **RynthAi** enabled. Confirm the app version is **1.17.0**. Log into DreamWeave and type `/acai2marae`.

## Where the app is installed
The default app DLL is below. Paste this into Explorer’s address bar or the plugin file picker; Windows expands `%LOCALAPPDATA%` for your account.

```
%LOCALAPPDATA%\ACAI2\Marae\ACAI2.Rynth.Marae.Plugin.dll
```

The same folder contains **PLUGIN-PATH.txt**. The installer also opens Explorer with the DLL selected. On Start, the app installs and loads its bundled mission and embedded routes for you.

<!-- page -->
# 02 / Choose your hunting mode
Open or bring back the panel with `/acai2marae`. Read the table by its headers: **Area**, **Use maps**, then **Hunt only**. Each row has its own settings.

| Control | What it means |
| --- | --- |
| Area | Include 180 Lowlands, 225 High Country or 275 Plateau in the run. |
| Use maps | Work supported inventory treasure maps while hunting in that area. |
| Hunt only | ON skips NPC task checks and repeats the hunting route. OFF visits hunters, works tasks and turns completed tasks in. |

## Three useful setups
**NPC tasks + treasure maps:** enable the areas you want and their **Use maps** boxes. Leave **Hunt only** OFF. This is the usual choice for task progress with treasure hunting along the way.

**NPC tasks without maps:** enable your areas, leave **Use maps** OFF and **Hunt only** OFF. The nearby-cache watcher can still check local caches if it is enabled.

**Continuous hunting:** enable an area and turn **Hunt only** ON. **Use maps** remains optional. For an overnight Lowlands salvage-item hunt, choose just **180 Lowlands**, turn **Hunt only** ON and leave **Use maps** OFF. Completed or cooldown tasks do not block this mode.

If you mix task areas with hunt-only areas, task areas are handled first. Multiple hunt-only areas rotate through their hunting circuits.

## Start, change options, or start over
Click **Start / Resume** to continue saved progress. Click **New run** when you want fresh app task checks and run progress. Neither button resets quests on the server.

Area and main mode settings are disabled during a run. Click **Stop** before changing them. The nearby-cache watcher and visible mesh distance sliders can be adjusted while running; distance changes affect the next mesh clue step.

## Expected task flow
**Travel from the 275 hub → visit selected-area hunters → hunt and optionally work maps → turn in completed tasks → move to the next area.** Continuous hunting then follows your hunt-only or fallback choices.

> Keep **Treasure Map** and wanted ordinary loot enabled in your RynthAi loot profile. The app’s explicit cache-reward pickup is separate from normal hunting loot.

<!-- page -->
# 03 / Tasks and the two-hour limit
## Mixed quest states are normal
You do not need every hunter to be at the same stage. Some tasks may be new, some in progress, some ready for a turn-in and some on cooldown. The mission checks hunter replies and uses the available work.

An unfinished hunt remains unfinished. Completed work can be turned in. A cooldown reply does not create a new task. The mission does not depend on your manually counting kills or remembering every NPC’s state.

## NPC replies and missing confirmations
The app uses stock RynthAi support to coordinate NPC visits and reply checks. It should verify a hunter’s interaction before continuing, with retries and bounded fallbacks for missing confirmations. It should not wait forever for one unrecognized line while abandoning all other available work.

If a reply is not recognized, capture the hunter’s actual dialogue and the panel status. That lets us improve recognition without guessing that the task completed. An ordinary unavailable task is different from an uncertain item move that needs review.

## Set a maximum time per camp
Enable **2-hour task limit per camp** to put a maximum on each camp’s task phase. This suits a daily circuit where you want useful progress in all three areas rather than spending the whole session on one difficult hunt.

The two hours are a **maximum**, not a required wait. If the active tasks finish earlier, the mission can turn them in and move on early. At the limit, it turns in completed work and moves on without claiming unfinished tasks are complete.

The app preserves the deadline through disconnects. This is a saved time deadline, not a fresh two-hour allowance on every reconnect; time away can count toward it.

## What happens after task areas finish?
With the two-hour task circuit enabled, the eventual fallback is **180 continuous hunting with maps OFF**. This gives the rest of a long session to Lowlands hunting.

Without that timed circuit, **Fallback to 180 after tasks** controls the normal task-completion fallback. Enabled, it permits Lowlands fallback hunting with treasure-map behavior; disabled, the task run can finish rather than continue hunting indefinitely.

For a deliberate continuous hunt from the start, select **Hunt only** for the area. You do not need to use task cooldowns or the time limit to get into that mode.

> Use **New run** for fresh app progress and deadlines. It does not erase cooldowns or task progress held by DreamWeave.

<!-- page -->
# 04 / Treasure maps and navigation
## Enable maps for the right area
Tick **Use maps** beside each area where you want maps worked. The character needs a supported map in inventory, and your loot profile must keep map drops if you want to replenish them while hunting.

Map handling interrupts the ordinary hunting route, works the map and its event, looks for the reward, then returns to hunting. An active map takes priority over opportunistic nearby-cache checks.

## Optional mesh travel
**Mesh map travel (RynthNav)** uses RynthNav for treasure-map clue travel. Enable RynthNav before turning it on. It needs usable mesh coverage and a complete path for the requested step.

The mesh option requires a complete path instead of silently substituting a straight run when pathfinding fails. If it cannot proceed, read the panel status and check RynthNav availability and coverage. Turning mesh travel off selects the mission’s non-mesh travel behavior; it does not make obstacles disappear.

## Tune the clue distances
| Setting | Default | Adjustable range |
| --- | --- | --- |
| Close clue | 30 yards | 10–60 yards |
| Far clue | 130 yards | 60–180 yards |

These are the distances for individual clue-travel steps, not an overall search radius. Defaults come from live experimentation. You can adjust the sliders while running; the next mesh clue step uses the new value. A leg already underway is not redrawn instantly just because a slider moved.

Start with **30 / 130**. Tune one setting at a time if a particular landscape needs it, and watch the result before leaving the character unattended.

## Stay tied to the digsite
The mission records the digsite location. During the event, an outer bound of **55 yards** helps prevent a melee character from chasing ordinary monsters so far that it misses event spawns or the final reward.

The character can move to fight, but the digsite remains the event’s reference point. When event work completes, cache handling should return to that locality rather than use the character’s last distant combat position as the reward location.

## Unusual encounters
Some maps finish through a short boss encounter rather than a long wave event. Other players can also overlap a dig. A visible cache alone does not prove it belongs to you. The nearby watcher adds a second opportunity to find a usable reward when the usual event-completion signal is unclear.

<!-- page -->
# 05 / Caches and reward pickup
## The nearby-cache watcher
**Nearby cache watcher (55 yd)** is ON by default and saved per character. It works independently of **Use maps**. Leave it on if you want the app to notice local rewards during ordinary hunting, including unusual encounters and overlapping digs.

The watcher checks during normal hunting after combat settles. It waits while an active map, NPC travel or native corpse recovery owns the character. It is intended to investigate local candidates, not search the entire landscape for every cache ever seen.

## Several caches can be together
DreamWeave can spawn multiple **Unearthed Cache** objects around the same event. Their names can be identical, while only one opens for a particular player. The app tracks individual object IDs and probes candidates rather than repeatedly choosing the first matching name.

Known ownership refusals are skipped while that object remains present. A cache that simply gives no opening confirmation gets bounded attempts, including at most one delayed retry. Successful or empty checks are remembered so the same object is not endlessly reopened.

The candidate list is bounded, and a nearby-watcher candidate must still be within **55 yards** when dispatched. This keeps the opportunistic check local instead of deliberately recalling across the map for stale sightings.

## Open the cache, then collect rewards
Opening and looting are separate steps. Once the right cache is open, the app explicitly requests supported reward-currency pickups instead of relying only on ordinary loot-profile matching.

**Doubloons are the priority. Trade notes are optional.** The app recognizes Trade Note currency by its actual object classification. A definite refusal to pick up an optional note should be skipped so it does not block the doubloons or the rest of the hunt.

This direct pickup is for supported reward currencies, not every item in every container. Your normal loot profile continues to govern ordinary hunting loot.

## When a pickup is uncertain
A move that may already have been issued but never confirmed needs review. The app preserves that uncertainty through reconnects rather than repeatedly sending the same move. Check your inventory and the open cache before using **I reviewed the pickup — resume**.

After a watcher check, the mission restores its saved hunting route and position. If you see an unexpected detour or a usable cache is missed, capture the panel status while the situation is still present.

<!-- page -->
# 06 / Saving and recovery
## Progress and preferences
The app keeps character-specific options and mission checkpoints. You do not need to reselect the same areas each login. Use **Start / Resume** to continue that progress, or **New run** for fresh app task checks and deadlines.

**Stop** saves progress and turns off automatic login resume for that run. Closing or hiding the panel is a display action; use **Stop** when you intend to stop automation.

## After a disconnect
An active run is designed to resume after login, once native travel and recovery have settled. The app does not log your account into the game. Your normal reconnect/login setup must get the character back online first.

Automatic resume will not take over another running macro. If another macro is active, stop it when appropriate and use the app’s **Start / Resume** button. A saved uncertain pickup still requires review; reconnecting does not clear it.

## After a death
RynthAi owns corpse recovery. The app coordinates around that recovery rather than running a competing corpse system. It enables native recovery for its run and waits for the recovery/travel phase to settle before continuing mission work.

Check **RynthAi → General → Safety stops**. A setting that stops automation on death can prevent the continuation you expect. Set death-related stops to match your intended recovery behavior, and test recovery while you can watch it.

The app cannot guarantee a corpse is reachable or that travel succeeds. Read the panel when recovery cannot proceed. Do not change unrelated safety settings simply to hide a real travel or inventory problem.

## Useful Rynth checks before a long run
- Verify `/ls` actually lands at the **275 Plateau camp** for this character.
- Confirm combat, buffs, healing and ordinary looting work on their own.
- Keep the maps and ordinary drops you want enabled in the loot profile.
- Enable RynthNav if **Mesh map travel** is checked.
- Check inventory space, supplies and native corpse-recovery behavior.

## What should need attention?
Unavailable tasks and inaccessible caches should have bounded ways to move on. A possibly issued, unconfirmed item move is different: review avoids blindly replaying an action. If the panel says **Needs attention**, read its specific reason before resetting anything.

> Watch a first NPC circuit and a map reward pickup after setup. Recheck those milestones after significant Rynth or app updates.

<!-- page -->
# 07 / Help and commands
## The panel disappeared
Type the command below. You do not need to restart Rynth just to reopen the app panel.

```
/acai2marae
```

## A checkbox is missing or the version looks old
Check the enabled **ACAI2.Marae** entry in RynthCore’s Plugins panel. This guide describes **v1.17.0**, including the nearby-cache watcher. The installer copying a new file does not change an old plugin entry pointing somewhere else. Disable/remove an obsolete entry and add the DLL path from **PLUGIN-PATH.txt** in the new installation folder.

## It loads the wrong mission, or stays at HOLD
Use the app’s **Stop**, then **Start / Resume**, rather than starting an unrelated meta manually. If startup is not confirmed, open **Advanced** and verify **RynthAi folder** points to the data location with **MetaFiles / NavProfiles**. Avoid **New run** unless you intend to reset the app’s run progress.

## It hunts without talking to NPCs
Check the row’s **Hunt only** box. It must be OFF for NPC tasks. Also read the status: a run that has already reached its continuous-hunting fallback will behave differently from a new task circuit. Stop before changing the area mode.

## It is not using a map in my pack
Check **Use maps** for the current area, not just another selected row. Let travel, NPC checks, recovery or an already-active map finish. A retry delay may also apply after a failed attempt. Remember that the timed circuit’s final Lowlands fallback has maps OFF.

## A cache opens but does not loot
Leave it open long enough to capture the status if possible. Send the app version, a panel screenshot and the diagnostic result below. Mention whether you manually looted anything. A refused optional note should not block doubloons; an uncertain move may show the review button.

```
/ra mexec listcreate[vtgetmetastate[],getvar[ml_zone],getvar[ml_use_maps],getvar[mm_watch_active],getvar[ml_status]]
```

Paste the command as one line in game. It reports mission state, area, map mode, watcher activity and status. It reads state; it does not move or loot anything. Do not repeatedly force a pickup or clear saved uncertainty to troubleshoot.

## Keep these links
[Release and downloads](https://github.com/ericallen9198-eng/ACAI2-Rynth/releases/tag/marae-stock-app-v1.17.0) • [Project source](https://github.com/ericallen9198-eng/ACAI2-Rynth)

**Thank you, Soot**, for the routes and original mission foundation. Guide prepared for the DreamWeave allegiance by ACAI2 • November 24, 2026.
