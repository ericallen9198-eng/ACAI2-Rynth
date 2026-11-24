# ACAI2-Rynth

**Independent Rynth plugins: Keyfer v0.3.5, Threads v0.1.6 and Combat v0.1.10.**
Each has its own Windows package and setup guide. The Keyfer package excludes
combat; the standalone Combat app is described below.

The adapter reconnects the existing local ACAI2 Bridge for conversation, private
tells, contextual follow-ups, arena games, paced announcements/emotes, world
events, public leaderboard queries and treasure reporting. Existing Bridge memory,
AI configuration, wiki and simulation behavior remain there. The old Samm-AI
project and installed ACAI2 files are not modified.

[Install and configure Keyfer](docs/KEYFER-SETUP.md).

## Standalone Threads app

The separate **ACAI2.Threads v0.1.6** plugin combines a compact Thread journal,
recipe research and an explicitly started native gem maker. It does not replace
RynthCore/RynthAi or require Keyfer/Bridge. Choose 300/325/350/375/400/450/500 blanks; Start temporarily pauses RynthAi and reports exact
blockers. All results are retained; uncertain
actions never automatically retry. See [Thread setup and first test](docs/THREADS-SETUP.md).
v0.1.6 adds new blank levels and up to two taper types sharing four doses, including
mixed random tapers. Shopping and research preserve the complete recipe per gem.
[Download Threads v0.1.6 for Windows](https://github.com/ericallen9198-eng/ACAI2-Rynth/releases/download/threads-v0.1.6/ACAI2-Threads-v0.1.6-Windows.zip).
Build on Windows with `scripts/Publish-Threads.ps1`; pure checks use
`scripts/test-threads.sh`. The independent package excludes combat and Keyfer.

## Standalone Combat app

**ACAI2 Combat v0.1.10** is its own read-only plugin: an adaptive DPS/defense meter,
class/proc readouts, artistic outlined floating text, compact Thread run/mob
summaries and exact server death/overkill notices. It requires neither Keyfer nor
Threads/RynthAi and duplicates no debuff icons. [Combat setup](docs/COMBAT-SETUP.md)
covers installation, preview, RynthVision toggles and the first build's adjustable
fixed-screen anchor. Build with `scripts/Publish-Combat.ps1`; controlled checks use
`scripts/test-combat.sh`. No ordinary combat log/CSV is recorded.

v0.1.10 adds an automatic missile hit percentage (landed / observed attempts),
using fresh native missile stance to keep melee, spells and procs separate.
The previous version added Hunter’s Echo afterimages and separate damage counts, open Protection
shields and Destruction shards, and a Normal meter that fits its contents.
Buffs/Debuffs/Skills scales, per-pack DPS, all three HUD modes and saved character
preferences remain available.
[Download Combat v0.1.10 for Windows](https://github.com/ericallen9198-eng/ACAI2-Rynth/releases/download/combat-v0.1.10/ACAI2-Combat-v0.1.10-Windows.zip).

## Standalone MapHunt app

**ACAI2.MapHunt app v0.1.0 beta** replaces the MapHunt Lua controller with a small
native ImGui panel. RynthAi owns combat, loot and corpse recovery; the app bundles
the existing verified AF workflow, prepares its own meta on Start, and preserves
per-character settings/checkpoints. Strict optional mesh clue travel, 30/130-yard
defaults, cache rotation and the 55-yard digsite boundary remain.
[MapHunt app installation](docs/MAPHUNT-APP-SETUP.md).
[Download the Windows app](https://github.com/ericallen9198-eng/ACAI2-Rynth/releases/download/maphunt-app-v0.1.0/ACAI2-MapHunt-App-v0.1.0-Windows.zip).
This beta uses maphunt.nav; native Hunt mode awaits an acknowledged area-preserving
pause/resume interface. The documented mission API 6 support is still required.

## Generic MapHunt meta

[MapHunt v0.1.0 beta](metas/maphunt/README.md) follows any user-supplied
`maphunt.nav`, hunts pack maps with measured 30-yard close / 130-yard far steps,
retains the digsite and 55-yard boundary, rotates cache IDs, confirms currency
pickup, and restores the hunt route. No NPC tasks, camp levels or recall routes.
The panel and per-character `meshnav` pvar switch mesh clue travel on/off.
It uses the documented mission API 6 support already used by Marae v1.14;
the default installer replaces only this meta/helper. See its guide for the
optional version-guarded support installer and native corpse recovery limits.

## Package and runtime

v0.3.5 adds immediate 24-hour direct-insult reply timeouts, same-channel notices
and the in-game Samm/Toe exemption. Includes Bridge v0.19: update both parts.
[Existing and new Keyfer behavior parameters](docs/KEYFER-BEHAVIOR.md).

v0.3.5 adds persistent 24-hour direct-insult reply timeouts, same-channel notices
and the Samm/Toe exemption. Update both plugin and included Bridge v0.19.
[Keyfer behavior parameters](docs/KEYFER-BEHAVIOR.md) documents existing and new rules.

v0.3.4 adds the KeyferChest setup panel and fixed Toe/Samm tell/allegiance
start, stop and status requests. Configure the key/chest locally first. This update
needs no Bridge change. Follow the Keyfer setup guide above for the Lua folder.

v0.3.3 adds own chest-loot awareness and includes Bridge v0.18; update both parts
while retaining existing keys/configuration/data. v0.3.1 fixed the registration `MissingMethodException` caused by the host's private JSON assembly context.

Download [the compiled Keyfer Windows ZIP](https://github.com/ericallen9198-eng/ACAI2-Rynth/releases/download/keyfer-v0.3.5/ACAI2-Rynth-Keyfer-v0.3.5-Windows.zip). No source rebuild is needed. The package is also available in its successful verification Actions run as **ACAI2-Rynth-Keyfer-Windows** (GitHub sign-in required). Or build on Windows with .NET 10:

```powershell
git clone --recurse-submodules https://github.com/ericallen9198-eng/ACAI2-Rynth.git
cd ACAI2-Rynth
.\scripts\Publish.ps1
```

For GitHub source ZIPs, run `scripts\Bootstrap-RynthCore.ps1` first; it fetches the
exact checksum-verified pinned SDK because GitHub ZIPs omit submodules.

The resulting `artifacts\keyfer-managed` package requires **CoreCLR RynthCore,
managed x86 plugin support, API 92+**. A NativeAOT-only engine cannot load it.
Windows publish embeds the manifest. Linux tests cannot prove native game loading.

## Enable only Keyfer

Copy `keyfer.example.json` to `%LOCALAPPDATA%\ACAI2-Rynth\keyfer.json`. Missing
settings leave Keyfer disabled. The sample enables only **DreamWeave / Keyfer
Sutherland** through the existing Bridge at `http://127.0.0.1:5052`. Stop the old
responder first; keep the same Bridge and its data/configuration.

Use `/acai2r keyfer status` and `/acai2r keyfer reload`. See the setup guide for
quiet/emote controls and the separate exact-template treasure payout option.
Private tells are not exported or put into public observation routes. HTTP runs
in cancellable workers; game command dispatch occurs only on the plugin pump for
the current login. Automatic payments default disabled; reporting remains enabled.

Observation-only settings in `pilot.json` still default off for telemetry and raw
public-chat retention. `/acai2r export` is an explicit local observation export;
tells are always excluded. No combat parser or renderer runs in this package.

## Development

Linux: `scripts/test.sh`. Windows: `scripts/Publish.ps1`. Tests use fake HTTP and
native-send delegates; no production Bridge, game, AI or Discord is contacted.
`ACAI2.Rynth.Combat.Future` retains pure combat code for tests/development only;
it is not referenced by the published plugin. The old ImGui renderer source is
excluded from compilation.

[Current status](docs/STATUS.md) · [rollout plan](docs/NEXT-ROLLOUT.md) ·
[cloud development](docs/CLOUD-DEVELOPMENT.md) · [source provenance](origin/ACAI2-SOURCE.md)
