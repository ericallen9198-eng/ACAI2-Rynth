# MapHunt app v0.1.0 — first beta

A standalone RynthCore plugin for DreamWeave treasure maps. RynthAi continues to
own combat, buffing, healing, looting and corpse recovery. This app replaces the
MapHunt Lua helper; it does not replace RynthAi. No Keyfer, Bridge, Threads or
Combat plugin is required. Any DreamWeave character can use it.

## Fresh installation

1. Have a working RynthCore managed/CoreCLR installation with RynthAi. The reviewed
   compatibility build used by this workflow requires **engine API 97+**. The app
   itself uses the older stable Script/host ABI; a loaded app panel alone does not
   establish that the required RynthAi helpers are installed.
2. Close all AC windows. Download the **MapHunt App Windows ZIP** and right-click
   it in Downloads, choose **Extract All**, then Extract.
3. Move the extracted folder somewhere permanent, for example
   `C:\Games\ACAI2-MapHunt`. Keep all its files together. Do not run from inside the ZIP.
4. In the RynthCore launcher's plugin list, use its Add/Browse control and select
   **ACAI2.Rynth.MapHunt.Plugin.dll** from that folder. Enable the entry. Avoid adding
   the same plugin twice.
5. MapHunt currently needs the same **mission API 6 helpers** used by the verified
   Marae v1.14 build. For a fresh installation, use `Compatibility\INSTALL-SUPPORT.cmd`
   while AC is closed. It installs only the unchanged reviewed RynthAi support DLL,
   backs up your old copy, and refuses unknown versions before replacing anything.
   Its default target is `C:\Games\RynthSuite\RynthAi\RynthCore.Plugin.RynthAi.dll`.
   If it refuses or cannot find the target, ask for help; do not manually overwrite
   an unknown DLL to bypass the check. An already-compatible DLL is left unchanged.
6. Put your own hunting route in RynthAi's **NavProfiles** folder and name it
   **maphunt.nav**. It must be appropriate for where you will stand and contain
   movement waypoints. No route or loot profile is included or replaced.
7. Start AC, log in, and type **/acai2maphunt** in game to show the panel. Select
   **Mesh clue travel** if RynthNav is loaded and you want it. Press **Start / Resume**.
   There is no `/lua start` command and no manual meta installation step.

The app discovers the RynthAi data folder from the current meta/nav path when
available. Otherwise it defaults to `C:\Games\RynthSuite\RynthAi`. If your folder
is different, enter its full path under **Advanced**, then Start. It must already
exist. The app prepares only **MetaFiles\MapHunt App.af** from its bundled resource;
if replacing an older copy, it keeps one `.previous` backup. Other metas, DLLs,
navs and loot profiles are untouched by normal app startup.

## What is available

* **Hunting source: maphunt.nav.** Normal RynthAi hunting resumes on the original
  route/index after map movement. A Once route reloads after its end.
* **Mesh clue travel:** optional; uses complete RynthNav paths with portal avoidance
  and wall clearance. It retries shorter steps after a path refusal, then returns
  to hunting with a five-minute map backoff. No silent straight-run fallback.
* **Close/far distance:** 30 / 130 yards by default, adjustable over 10–60 / 60–180.
  Changes apply to the next clue. Direct mode uses measured displacement, not a
  guessed running time. Cache approach and digsite return use RynthAi's native GoTo;
  the mesh checkbox controls clue travel rather than replacing native GoTo behavior.
* **Start / Resume**, **Stop**, clear progress text and a conditional **Review complete**
  button for a dispatched currency move whose outcome could not be confirmed.
* Per-character saved panel position, preferences and bounded run checkpoint.
  The app also uses Rynth pvars `meshnav`, `maphunt_close`, and `maphunt_far`; existing
  values are imported. No growing chat history is recorded.

**Native Rynth Hunt mode is not enabled in this first app beta.** Inspection of its
current public interface shows that Hunt start anchors from the current position;
it does not expose an acknowledged pause/resume of the original hunting area.
Simply stopping and starting it after a map could shift the hunt. Use a recorded
route here until that integration is supported and verified.

## Expected behavior and recovery

The existing tested meta handles map IDs/clues, dig confirmation, the absolute
30-minute encounter budget, returning between fights, and the 55-yard outer digsite
boundary. Only real reward completion/currency growth starts cache handling; ordinary
XP does not. It rotates exact cache object IDs, stops short of the selected cache,
waits for an actual open acknowledgment and moves currency serially. Notes are
optional; doubloon-only/notes-only caches can finish. The Aun corpse reward path
continues using the native loot profile and confirmed doubloon growth.

Set up normal Rynth combat/buff/healing/loot profiles first. Keep Treasure Maps and
the desired corpse rewards in the loot profile. Watch the first few maps in game.

The workflow enables native own-corpse recovery and disables Stop Macro On Death.
It waits while recovery owns movement, then reloads the supplied hunt route. It
cannot invent a portal trip back from a remote lifestone; bind/configure recovery
appropriately for the hunting area.

An active app run has a small checkpoint and can resume after login. A still-live
event near its anchor can resume without extending its deadline. An automatic
resume will not interrupt a different running macro. **Stop** clears the saved
active intent. A native macro Stop also clears it. Unconfirmed item moves remain
persistently marked and require inventory review; the app never blindly replays them.

## Updating from the older MapHunt Lua beta

Stop the old MapHunt run and its Lua script, and disable that script's autostart in
RynthLua before starting the app. Do not run both controllers. The app uses its own
meta name and preserves the old files. This step is unnecessary for a fresh install.

## Verification

Controller tests use a fake RynthAi boundary, including strict mesh refusal,
serial dispatch tokens, cache stand-off, reconnect and uncertain-item behavior.
The bundled AF is generated from the previously tested MapHunt workflow with its
state logic unchanged. Genuine Rynth AF parsing and fake-world scenarios are run
against it. Windows publish uses the real pinned SDK and embeds the native plugin
manifest. No automated checks issue live game actions. Actual reach, collision,
chat timing and the first full map/death/reconnect run still need beta verification.
