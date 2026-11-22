# Marae Lassel v1.14 — digsite return and cache approach

Stop the mission, close all AC clients, extract the entire ZIP, and run
UPDATE-MARAE-MISSION.cmd. Then start AC, run `/lua start MaraeLassel`, open `/marae`,
and choose Resume run. The installer backs up and verifies the DLL/meta/Lua together.
Saved task progress and UI preferences remain. Core API 97+ and the same mission
API 6 compatibility DLL are used. No Bridge, Keyfer, Threads or loot profile is replaced.

## Digsite behavior

- The actual encounter start records its location, replacing any earlier failed-dig guess.
- Melee can chase and fight normally inside the event area.
- Between fights, if farther than ten yards from the digsite, navigation returns there.
  Valid new targets interrupt this gentle return; active native actions settle first.
- At or beyond 55 yards from the digsite, combat yields to a forced return. Far unrelated
  targets cannot keep that return in combat. Rynth's normal combat controller drops the
  target when combat is disabled; no second attack controller or global target filter
  is installed. This is a return trigger, not a physical barrier; movement can overshoot.
- Combat resumes near the digsite. The target is within three yards, with native route
  completion accepted inside ten yards to respect configured navigation arrival tolerance.
- Failed return paths retry with 15-second backoff, increasing to 60 seconds after three
  failures. Healing/buffing remains enabled. A forced return keeps combat yielded during
  its retries; its status explains that navigation is still pending.
- Event completion and actual corpse payouts are handled during the return as well as
  during combat. The original 30-minute event budget is absolute and is not renewed by
  return trips. Death/native corpse recovery retain their existing ownership.

## Cache behavior

The route aims 2.5 yards short of the selected cache center. Within three yards, or
when the accepted native approach route completes, the meta stops navigating and
uses the exact cache object. The game's native interaction handles final reach.
An already-open cache acknowledgment ends approach without using it again. Other
owners' cache IDs still rotate fairly; currency pickup remains serial and acknowledged.
All v1.13 failure recovery and persistent unconfirmed-pickup review protections remain.

## Verification

Genuine AF/nav parsing, actual expression-parser audit, MoonSharp controller/allocation
checks, fake-game boundary/approach/recovery scenarios, Windows x86 manifest build and
installer checks run before publication. No tests issue live game actions. Actual
server reach, collision and pathfinding behavior still needs live verification.
