# MapHunt v0.1.0 — allegiance distribution beta

A generic DreamWeave treasure-map hunter for **any character** and a user-supplied
**maphunt.nav**. No named characters, hunter NPCs, kill tasks, area levels, camp
portals or lifestone recalls. This is a separate meta, not a Rynth engine rewrite.

## Requirements and installation

* RynthCore managed x86 / API 97+, RynthAi and RynthLua.
* The same **mission API 6 compatibility support used by Marae v1.14** is required
  for strict mesh clue planning, route/index restoration, native recovery status
  and explicit acknowledged cache pickup. Stock RynthAi is not assumed to provide
  these custom helpers. This release reuses that reviewed DLL without changing it.
* RynthNav with local mesh coverage is required only for **mesh clue travel**.
* Your own suitable combat/buff/loot profiles and supplies. Enable Keep rules for
  Treasure Maps and the desired corpse rewards. Cache currency is collected explicitly.

1. Stop your macro, close AC, and extract the whole ZIP anywhere (Downloads is fine).
2. Already using Marae v1.14? Run **INSTALL-MAPHUNT.cmd**. It installs just this meta
   and Lua helper, backs up prior copies, and leaves the DLL and Marae files alone.
3. Allegiance members without the support build can use
   **INSTALL-MAPHUNT-WITH-SUPPORT.cmd**. It backs up the active DLL and installs the
   bundled reviewed support build only when the existing DLL matches a reviewed
   compatible version. An unknown/newer DLL is refused before changing any files.
   Do not manually overwrite an unreviewed version to bypass that check.
4. Default destinations are `C:\Games\RynthSuite\RynthAi\MetaFiles\MapHunt.af` and
   `C:\Games\RynthSuite\RynthLua\Scripts\MapHunt\index.lua`. The **MapHunt folder
   belongs inside Scripts**; index.lua belongs inside MapHunt. Custom paths can be
   passed to Install-MapHunt.ps1 with `-RynthAiFolder` and `-LuaScripts`.
5. Put **your own route**, renamed **maphunt.nav**, in RynthAi's **NavProfiles**.
   There must be one unambiguous route with that name and at least one waypoint.
   No sample route is supplied because it could send someone into the wrong area.
6. Start AC and run `/lua start MapHunt`. Use `/maphunt` if the panel is hidden.
   Press **Start / Resume**. The helper loads MapHunt.af and starts the correct meta.
   Do not run it alongside MaraeLassel or a competing movement script.

Both the AF and Lua helper are required. Loading only the AF will fail route
verification rather than wander. The installer does not replace your nav, loot
profile, other metas, Keyfer, Threads, or saved character preferences.

## Persistent settings

Rynth supports character-scoped `setpvar` / `getpvar`. Use **one variable named
meshnav**, whose value is 1 or 0, rather than separate variables meshnav_1/meshnav_0.

```text
/ra mexec setpvar[meshnav,1]
/ra mexec setpvar[meshnav,0]
/ra mexec setpvar[maphunt_close,30]
/ra mexec setpvar[maphunt_far,130]
```

Defaults: mesh **off**, close **30 yards**, far **130 yards**. Existing values are
preserved at startup. The panel exposes the same controls. Close is clamped to
10–60 yards and far to 60–180 yards. Changes apply to the next clue step.

Mesh on uses a complete RynthNav path in the clue direction, avoiding portals and
using wall clearance. A failed path retries at shorter distances up to three times;
it never silently substitutes a raw run for a failed mesh clue step. Mesh off
faces the clue and runs until measured displacement reaches the configured yards,
with a bounded timeout. Run skill is not used to guess distance from seconds.
The toggle controls **map clue steps**; the ordinary hunt follows your nav. Cache
approach and digsite return use RynthAi's native GoTo behavior (its planning depends
on the installed Rynth version). Do not run RynthNav's separate Go controller too.

## Expected flow

1. Validate maphunt.nav, then hunt with native combat, buffing and looting.
2. Check maps already in the pack as well as newly looted maps. Lock one map ID,
   read its clue, walk 30/130 yards as appropriate, read again, and dig when ready.
3. Keep the actual encounter anchor. Fight normally inside the event area; return
   between fights when more than 10 yards away. At **55 yards**, yield combat and
   return before pursuing more targets. The event has one absolute 30-minute budget.
4. Only actual digsite completion or confirmed reward currency starts reward
   handling. Ordinary XP, unrelated kills or a visible cache alone do not.
5. Return to the digsite, enumerate Unearthed Cache object IDs, approach each
   candidate and use that exact object. Stop short of the center. Opening another
   player's cache unsuccessfully rotates to the next; one cache is sufficient.
6. After an open-container acknowledgment, explicitly collect doubloons and any
   notes present, one move at a time. Confirm inventory growth and source change.
   Doubloon-only and note-only caches work. The Aun corpse reward path still uses
   native profile looting and a confirmed doubloon increase.
7. Restore the original hunt route/index after temporary movement and continue.
   A Once route reloads after its end; a Circular route continues naturally.

Non-destructive map/path/cache-search failures resume hunting with a five-minute
map backoff. **A dispatched but unconfirmed currency move is the exception:** it
holds for inventory review rather than blindly repeating a possibly completed move.
Inspect the inventory/cache, then use **Review complete**. Missing support or a
missing/empty nav also requires fixing the real setup issue before resuming.

## Death, disconnect and stopping

The meta enables native own-corpse recovery and disables Stop macro on death. It
waits while recovery owns movement, then reloads the supplied nav. It does not
require full HP or an unchanged buffed maximum to recover. It cannot invent a
portal route back from an arbitrary distant lifestone: bind locally or configure
native recovery/travel appropriately for your hunting area.

An active run registers this Lua script for character autostart and stores a small
bounded checkpoint. After login, it resumes the hunt; a still-live encounter near
its recorded anchor can resume without extending its event deadline. Unconfirmed
item moves remain marked for review across reconnects. Auto-resume does not take
over a different running meta. **Stop in this panel** clears the active intent so
it will not restart after login. No chat transcript or growing history is recorded.

## Verification limits

Generated from the working Marae v1.14 treasure workflow. Genuine Rynth AF parsing,
actual x86 expression-parser auditing, genuine MoonSharp controller execution,
fake-world cache/boundary/recovery scenarios and Windows installer checks are run
before publication. No checks issue live game actions. Server collision, exact
chat delivery timing and path coverage still need an in-game beta test.
