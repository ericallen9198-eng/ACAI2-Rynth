# Migration pilot status

This repository starts a separate ACAI2 implementation on RynthCore. The existing
ACAI2/Samm-AI deployment remains unchanged. This is a read-only foundation, not a
complete feature migration or a replacement to deploy over the current client.

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

The Linux build against the real SDK and 39 automated checks pass. Checks cover
session isolation, bounded buffers, configuration, fake HTTP transport, compiled
x86 metadata, native export signatures and the read-only host API boundary.
Tests contact no live Bridge, AI or game service.

Linux cannot embed the Win32 plugin manifest and reports that expected warning.
Publish on Windows using `scripts/Publish.ps1`; do not install the Linux build.
The Windows client, native plugin loading, launcher setup and live observations
have not been verified here. PowerShell scripts and the GitHub CI workflow still
need execution on Windows. Follow [Windows validation](WINDOWS-VALIDATION.md)
before enabling optional telemetry.

Keyfer, combat tracking, Thread Gems, HUDs and automation remain migration work;
see the [roadmap](ROADMAP.md).
