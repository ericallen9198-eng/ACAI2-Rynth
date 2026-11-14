# ACAI2-Rynth

**Independent Rynth plugins: Keyfer v0.3.1, Threads v0.1.4 and Combat v0.1.7.**
Each has its own Windows package and setup guide. The Keyfer package excludes
combat; the standalone Combat app is described below.

The adapter reconnects the existing local ACAI2 Bridge for conversation, private
tells, contextual follow-ups, arena games, paced announcements/emotes, world
events, public leaderboard queries and treasure reporting. Existing Bridge memory,
AI configuration, wiki and simulation behavior remain there. The old Samm-AI
project and installed ACAI2 files are not modified.

[Install and configure Keyfer](docs/KEYFER-SETUP.md).

## Standalone Threads app

The separate **ACAI2.Threads v0.1.4** plugin combines a compact Thread journal,
recipe research and an explicitly started native gem maker. It does not replace
RynthCore/RynthAi or require Keyfer/Bridge. Choose 300/325/350/375 blanks; Start temporarily pauses RynthAi and reports exact
blockers. All results are retained; uncertain
actions never automatically retry. See [Thread setup and first test](docs/THREADS-SETUP.md).
Build on Windows with `scripts/Publish-Threads.ps1`; pure checks use
`scripts/test-threads.sh`. The independent package excludes combat and Keyfer.

## Standalone Combat app

**ACAI2 Combat v0.1.7** is its own read-only plugin: an adaptive DPS/defense meter,
class/proc readouts, artistic outlined floating text, compact Thread run/mob
summaries and exact server death/overkill notices. It requires neither Keyfer nor
Threads/RynthAi and duplicates no debuff icons. [Combat setup](docs/COMBAT-SETUP.md)
covers installation, preview, RynthVision toggles and the first build's adjustable
fixed-screen anchor. Build with `scripts/Publish-Combat.ps1`; controlled checks use
`scripts/test-combat.sh`. No ordinary combat log/CSV is recorded.

v0.1.7 replaces solid floating notice panels with open accents and adds saved
Buffs/Debuffs/Skills scale controls. Each group scales text and graphics together;
ordinary damage, crits and healing keep their existing controls. Per-pack DPS,
three HUD modes and the Kinetic/Reckless handling from v0.1.6 are preserved.
[Download Combat v0.1.7 for Windows](https://github.com/ericallen9198-eng/ACAI2-Rynth/releases/download/combat-v0.1.7/ACAI2-Combat-v0.1.7-Windows.zip).

## Package and runtime

v0.3.1 fixes the registration `MissingMethodException` caused by the host's private JSON assembly context.

Download [the compiled Keyfer Windows ZIP](https://github.com/ericallen9198-eng/ACAI2-Rynth/releases/download/keyfer-v0.3.1/ACAI2-Rynth-Keyfer-v0.3.1-Windows.zip). No source rebuild is needed. The package is also available in its successful verification Actions run as **ACAI2-Rynth-Keyfer-Windows** (GitHub sign-in required). Or build on Windows with .NET 10:

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
