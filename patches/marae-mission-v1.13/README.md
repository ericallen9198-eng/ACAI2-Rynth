# Marae Lassel v1.13 — recoverable failures keep hunting

## Install

1. Stop the mission and close all AC clients.
2. Extract the entire ZIP and run UPDATE-MARAE-MISSION.cmd.
3. Start AC; run `/lua start MaraeLassel`, then `/marae`, then **Resume run**.
4. Confirm `/lua autostart MaraeLassel character` for future logins.

The updater backs up and verifies the active compatibility DLL, embedded meta and
Lua panel together. Existing character preferences and task checkpoints remain.
The bundled DLL retains mission API 6 and the v1.12 NPC approach correction; Core
API 97+ is required. No Bridge, Keyfer, Threads or loot profile is replaced.

## What continues automatically

- Cache search/approach and exhausted map-mesh failures record the source state,
  restore owned movement/pickup settings, wait ten seconds, and reload hunting.
- Failed cache searches are not credited as successful loot. Saved map preferences
  stay enabled; entry to another map backs off for five minutes. The panel shows
  the retry countdown and last failing state. Native corpse recovery retains ownership.
- Camp travel has two retries after fifteen-second backoffs. Exhausted task-camp
  travel defers that camp and tries other selected work. Continuous/fallback travel
  uses a five-minute slow retry rather than repeated recalls or permanent HOLD.
- No task progress defers work instead of stopping the whole mission. If unresolved
  camps remain and Lowlands fallback was disabled, they are retried after five minutes,
  without falsely completing tasks. Normal explicit completion remains completion.
- A server-confirmed unfinished hunt clears stale local completion/cooldown evidence.
  An ordinary turn-in that confirms unfinished tasks returns to hunting instead of
  another recall. Timed partial turn-ins still move on when their camp budget expires.
- Recovering runs remain active in their per-character checkpoint, so Lua autostart
  can resume them after reconnect. Explicit Stop and native macro stops are respected.

## Reasons that still require intervention

Missing required interfaces, invalid configuration, a controller load error or an
unconfirmed issued inventory move do not get blindly restarted. The last failure
state/reason overwrite fixed per-character variables; no growing chat log is added.

Before an explicit cache currency move, the meta sets a native persistent review
marker. Successful acknowledgment or a native refusal clears it. If confirmation is
missing, the HOLD explains the item instead of the former generic message. The marker
is checked on a fresh meta load and retained through normal logout/reconnect.
After checking the pack/cache, the local **Resume after reviewing pickup** button
clears the marker and resumes saved work. Neither New run nor reconnect silently
clears it. A process/power loss has the normal upstream pvar-save limitations.

These changes prevent the reproduced stale-task/turn-in loop; the user's particular
180-camp live loop was reset before capture, so its exact cause remains unconfirmed.

## Validation

Genuine AF/nav parsing, actual native expression-parser audit, MoonSharp controller
and allocation tests, fake-game recovery/quest-loop simulations, Windows x86 build
and installer checks run before release. No test issues live game commands. The
exact server behavior still requires live verification.
