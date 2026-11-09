# ACAI2-Rynth

A separate migration project for ACAI2 on **RynthCore**, with the existing
Samm-AI / ACAI2 project preserved as the working installation.

**v0.2.0 adds an ImGui combat UI to the read-only pilot.** It does not run Keyfer,
arena games, combat automation, Thread Gem prediction or outgoing game commands.
The Windows renderer has not been run here; build/publish on Windows before testing.

## What works in this scaffold

- Compiles against the real, pinned RynthCore SDK and PluginCore.
- Observes login/logout, chat event counts, object-change counts and a bounded
  sample of position-bearing native objects in the player's landblock.
- Isolates each server/character/login with a unique instance ID and cancellation.
- Keeps optional public-chat text only in a bounded memory buffer. Private tells
  and unknown/local chat formats are excluded. Raw text capture defaults off.
- Optionally registers and sends heartbeat/perception to the existing local Bridge
  using protocol 2. **Telemetry defaults off.** It never uses Keyfer/chat/AI/prize routes.
- Provides `/acai2r status`, `/acai2r export` and `/acai2r ui` (appearance settings).
- Parses incoming combat messages locally into session/component totals and a
  bounded 60-second DPS meter; no full raw combat history is retained.
- Draws a compact ImGui meter with expandable breakdown, sneak statistics and
  explicitly labeled live/not-recording status.
- Offers optional clear-background proc text with inward arcs, effect RGBA color
  wheel/hex controls, black outline, scale/opacity/lifetime controls and isolated preview.
- Saves meter position, lock and appearance per server/character under
  `%LOCALAPPDATA%\ACAI2-Rynth\CombatProfiles`, separately from observation exports.
- Opens Rynth's Settings panel for its existing power/height controls. This plugin
  never restores power by issuing an attack and creates no competing power owner.

Proc text defaults off because Rynth Vision already provides general combat text.
Choose your preferred source for overlapping crit/heal numbers. Toon following,
Thread height learning, world debuff markers, recording/export of combat history,
custom font loading/weight and a DPS sparkline remain future work. See
[combat UI usage](docs/COMBAT-UI-USAGE.md).

Local observation data stays in memory until you explicitly export it. No existing
Data/configuration directories are imported or modified. Names with no verified
object classification are not labeled players/monsters, and no distance is guessed.

## Build from a Git clone

Install .NET 10 SDK. Use a separate source directory:

```powershell
git clone --recurse-submodules https://github.com/ericallen9198-eng/ACAI2-Rynth.git
cd ACAI2-Rynth
.\scripts\Test.ps1
.\scripts\Publish.ps1
```

## Build from GitHub Download ZIP

Extract the new repository to its own directory. GitHub ZIPs omit Git submodules;
the bootstrap script retrieves the exact reviewed RynthCore source archive and
verifies its SHA-256 before using it:

```powershell
.\scripts\Bootstrap-RynthCore.ps1
.\scripts\Test.ps1
.\scripts\Publish.ps1
```

`global.json` selects .NET 10. The managed plugin can be built with a regular .NET 10
SDK; it does not require rebuilding RynthCore or our old solution. Publishing here
means creating a local build package, not installing it into the game or uploading
it anywhere. The package is under `artifacts\plugin-managed`.

## Required RynthCore runtime

This pilot targets a **CoreCLR RynthCore engine, API 92 or newer**, matching the
reviewed dependency. It is a managed x86 plugin. The legacy NativeAOT engine cannot
load that package. RynthCore's `ManagedPlugins.cs` and
`scripts/Publish-PluginsCoreClr.ps1` establish this supported route; the older
NativeAOT-only README/build advice is not the whole current loader story.

Keep the published package together and add `ACAI2.Rynth.Plugin.dll` in the correct
RynthCore launcher's plugin list. Do not put it in Decal's plugin list. Windows
publish embeds the Rynth manifest. A Linux build validates source/ABI metadata but
emits an expected warning because Windows `UpdateResource` is needed for embedding.
**The Windows client has not been run in this workspace.**

## Optional settings

The pilot reads `%LOCALAPPDATA%\ACAI2-Rynth\pilot.json`. Missing settings mean the
safe defaults above. Copy `pilot.example.json` there only when configuring the pilot.
It never reads the old ACAI2 config or any AI key.

- `BridgeEnabled`: false by default. Turn on only when intentionally testing
  telemetry with the existing Bridge at `http://127.0.0.1:5052`.
- `CapturePublicChat`: false by default. Enabling it allows local public-chat
  text exports; raw chat is never sent to Bridge by this pilot.
- Buffer limits and telemetry interval are bounded and validated.

`/acai2r export` writes a snapshot to `%LOCALAPPDATA%\ACAI2-Rynth\Exports` and reports
the path locally. Exports are not committed or uploaded by these scripts. The
pilot adds no chat responder, so it cannot duplicate the existing Keyfer's replies.

## Development

Linux: `scripts/test.sh`. Windows: `scripts/Test.ps1`. All automated HTTP tests use
fake handlers. There are no production Bridge, game, AI or Discord calls in tests.
The plugin build checks real SDK references, and the tests inspect its compiled
x86 managed assembly and exported lifecycle ABI.

See [current status](docs/STATUS.md), [next-rollout plan](docs/NEXT-ROLLOUT.md), [roadmap](docs/ROADMAP.md),
[Windows validation](docs/WINDOWS-VALIDATION.md), [cloud development](docs/CLOUD-DEVELOPMENT.md),
and [source provenance](origin/ACAI2-SOURCE.md).
The archived [migration research](docs/MIGRATION-RESEARCH.md) includes follow-up
corrections for the managed loader. Dependency licenses are documented in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
