# ACAI2-Rynth

**v0.3.1 focuses on restoring Keyfer on RynthCore. Combat is excluded from this
rollout**, with its source retained for a possible separate plugin.

The adapter reconnects the existing local ACAI2 Bridge for conversation, private
tells, contextual follow-ups, arena games, paced announcements/emotes, world
events, public leaderboard queries and treasure reporting. Existing Bridge memory,
AI configuration, wiki and simulation behavior remain there. The old Samm-AI
project and installed ACAI2 files are not modified.

[Install and configure Keyfer](docs/KEYFER-SETUP.md).

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
