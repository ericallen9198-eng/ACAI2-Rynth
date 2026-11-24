# Marae Lassel app v1.15.1 — stock RynthAi beta

This is a separate Rynth app for the 180 Lowlands, 225 High Country and 275 Plateau routes. It uses the normal RynthAi DLL. There is no replacement RynthAi DLL in this download and no Lua panel is required.

## Install

1. Stop the old mission and close the game before installing/updating the app.
2. Download the Windows ZIP, right-click it, choose **Extract All**, and open the extracted folder.
3. Double-click **INSTALL-MARAE.cmd**. It installs the app to `C:\Games\ACAI2-Rynth-Marae` and backs up any app files it replaces.
4. In RynthCore's Plugins list, add/enable `C:\Games\ACAI2-Rynth-Marae\ACAI2.Rynth.Marae.Plugin.dll`. Leave RynthAi enabled. Enable RynthNav if you want mesh map travel.
5. Log in. If the old Marae Lua script is running, enter these two commands, one at a time:
   ```text
   /lua stop MaraeLassel
   /lua autostart MaraeLassel none
   ```
   The app also sends these fixed commands when starting a run. Other Lua scripts are unaffected.
6. Enter **`/acai2marae`** to show the native app panel. `/marae` also opens it once the old Lua script is stopped.
7. Under **Advanced**, check the **RynthAi folder**. The usual location is `C:\Games\RynthSuite\RynthAi`; it must contain the `MetaFiles` and `NavProfiles` folders. The app can detect it from a loaded meta's full path.
8. Choose your areas/options and click **Start / Resume**. The app installs its embedded mission into MetaFiles, backs up the previous same-named AF once, loads the correct mission, and starts it. You do not need to copy navs or start a Lua script.

Use RynthSuite **2026.11.23.3 or later** with its normal matching RynthCore update. This beta was audited against the unmodified 2026.11.23.3 source. Later releases are not guaranteed compatible if their public interfaces change.

## Choose what to do

The panel has three clearly labeled columns: **Area**, **Use maps**, **Hunt only**.

- Check an **Area** to visit it.
- **Use maps** lets the mission work treasure maps while hunting that area. Your normal loot profile must still collect maps and desired corpse loot.
- Leave **Hunt only** OFF to talk to hunters, hunt their tasks, and turn in completed tasks. Mixed completed, in-progress and cooldown tasks are accepted; target counts do not have to match a hard-coded number.
- Turn **Hunt only** ON to skip the hunters and repeat that area's route, even if every task is finished or on cooldown. Multiple hunt-only areas rotate after each circuit.
- **2-hour task limit per camp**, when enabled, bounds task hunting. After two hours the mission turns in completed work, moves on, and eventually returns to 180 for continuous hunting with maps off. Saved deadlines survive reconnects. Hunt-only areas are not timed.
- **Fallback to 180 after tasks** controls the normal task-complete fallback when the two-hour limit is off. With maps enabled for fallback, it can keep hunting treasure in 180. Turning this off permits a completed run to finish.
- **Mesh map travel** plans complete RynthNav paths for clue steps. It needs RynthNav and mesh coverage. It does not silently replace a missing mesh path with a straight run. Turn this option off to use the mission's original clue movement.
- The **Close clue** slider is 10–60 yards (new app default 30); **Far clue** is 60–180 yards (new app default 130). Existing saved values are imported when available. Live changes affect the next mesh clue step.

**Start / Resume** keeps saved task progress. **New run** clears this app's task/area progress and deadlines, then checks the hunters again. **Stop** saves progress and prevents automatic restart after login. Settings and panel position are saved separately for each server/character. First use imports the existing mission's persistent variables where available; the old Lua storage files are left intact. If the old mission is not loaded when importing, verify the panel options before starting.

## Travel, NPCs, maps and recovery

`/ls` must land at the verified **275 Plateau camp**. The mission uses the nearby named 180/225 portals to reach the other camps. This download does not change your lifestone binding.

Each hunter receives an explicit native use request. The app waits for a task-status reply from that exact hunter before continuing. A greeting alone does not count. It retries up to three times, then continues to the next hunter. The mission can retry the route once and then use confirmed tasks or its 180 fallback instead of waiting forever. An entire NPC route has a seven-minute limit including walking. It never pretends a missing reply means the task succeeded.

Melee may fight within 55 yards of a recorded digsite. Crossing the boundary triggers a return so the event mobs/cache are not abandoned. Cache search tries distinct object IDs, approaches short of the object center, opens through the normal client action, and collects supported cache currency serially with inventory and source-change confirmation. A single cache is valid; multiple caches do not have to spawn. Corpse rewards remain handled by RynthAi's normal looting.

Native RynthAi corpse recovery remains the owner. Keep **Loot Own Corpse** and **Travel To Own Corpse** available in your profile and review **General → Safety stops** so a death does not stop the macro. The mission enables native recovery and disables its death-stop option when running. Its recovery observation does not depend on maximum HP, so buff changes do not block it. An unconfirmed native recovery wait is bounded to 15 minutes 30 seconds; it then continues without claiming that the corpse was recovered.

After reconnect, an app-started active run reloads the mission and uses the saved area/task progress to travel as needed. It does not take over another running macro. An unconfirmed item move is a deliberate exception: inspect the pack/cache and use the explicit **review** button. The app never automatically repeats a possibly issued currency move.

## Local files and first test

Per-character settings/progress and the latest 16 NPC mismatch entries are under `%LOCALAPPDATA%\ACAI2-Marae\<character-key>\`. Only known hunter replies enter that bounded diagnostic file; ordinary chat is not recorded. Two private temporary nav files per character are kept under `RynthAi\NavProfiles\ACAI2-Marae`. Original navs are never overwritten.

For the first live test, select one task area, leave Hunt only off, and watch one hunter interaction before leaving it unattended. Verify the panel advances after a recognized reply, then watch a map and cache pickup if maps are enabled. Offline parser/scenario checks and Windows packaging do not prove live server behavior.

The installer only copies this app's files. The app installs its bundled AF at Start; it never patches or replaces `RynthCore.Plugin.RynthAi.dll`. Future RynthAi updates can follow the normal update process, subject to the public interfaces remaining compatible.
