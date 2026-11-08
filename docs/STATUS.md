# Migration pilot status

This repository starts a separate ACAI2 implementation on RynthCore. The existing
ACAI2/Samm-AI deployment remains unchanged. This is a read-only foundation, not a
complete feature migration or a replacement to deploy over the current client.

## v0.2.0 combat display

An ImGui.NET 1.91.6.1 renderer now uses the pinned engine's own native ImGui context.
It provides a live compact DPS meter, expandable component and sneak statistics,
optional outlined proc text with inward arcs, advanced RGBA color wheel/hex inputs,
an isolated sample preview and per-character saved layout/lock settings.
`/acai2r ui` opens setup. Live combat events are parsed locally; raw combat lines
are not retained or exported. Rynth owns existing power and height controls; the
ACAI2 setup links to Rynth Settings and does not add a second action controller.

Rynth Vision already has general combat text, so ACAI2 proc text defaults off.
Character projection remains disabled pending reliable camera-frame coordinates;
fixed-screen anchors use normalized viewport positions. Thread height learning,
font weight/loading, charts and world debuffs are not connected in this version.

## Implemented

- Real RynthCore SDK and lifecycle exports, pinned to API 92.
- Managed x86 plugin for the CoreCLR engine; no Decal, VTank or UtilityBelt references.
- Bounded observations with character/session isolation and cancellation on logout.
- Local status and JSON export commands; raw chat capture defaults off.
- Optional loopback Bridge registration, heartbeat and perception only, disabled
  by default. No AI requests, game commands, automated chat or prize actions.
- Windows build/publish scripts, pinned dependency bootstrap for ZIP downloads,
  and Linux/Windows CI definitions.

## Validation and remaining work

The Linux build against the real SDK and 56 automated checks pass. Checks cover
session isolation, bounded buffers, configuration, fake HTTP transport, compiled
x86 metadata, native export signatures and the read-only host API boundary.
Tests contact no live Bridge, AI or game service.

Linux cannot embed the Win32 plugin manifest and reports that expected warning.
Publish on Windows using `scripts/Publish.ps1`; do not install the Linux build.
The Windows client, native plugin loading, launcher setup and live observations
have not been verified here. PowerShell scripts and the GitHub CI workflow still
need execution on Windows. Follow [Windows validation](WINDOWS-VALIDATION.md)
before enabling optional telemetry.

Keyfer, Thread Gems, remaining combat/HUD features and automation remain migration work;
see the [roadmap](ROADMAP.md).
