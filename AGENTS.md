# ACAI2-Rynth working rules

This is the separate Rynth migration project. Do not modify the old Samm-AI checkout,
installed C:\ACAI2 tree, player data, or configuration as part of this project.

Current scope is a read-only pilot. No game chat, /tells, commands, attacks, casts,
item use/identification requests, trading, movement, Keyfer asks, prize claims or AI
calls may be added to the pilot. The only network routes are optional loopback
Bridge registration, heartbeat and perception. Telemetry and raw-chat retention
are off by default. Private tell text must never be exported or sent to Bridge.

Use the genuine pinned RynthCore SDK/PluginCore dependency; never substitute a
mock SDK assembly and claim native compatibility. Pure observation/HTTP interfaces
may be faked for controlled tests. Preserve session cancellation, bounded queues,
callback/pump separation and server/character/instance identities.

Use .NET 10. The default plugin build is managed x86 for the CoreCLR engine at API
92 or later. A Linux build cannot embed its Win32 plugin manifest and cannot prove
in-game loading. Document that warning. Windows publish is required for a deployable
package. NativeAOT is a separate future build mode, not a Linux-tested deliverable.

Run scripts/test.sh (Linux) or scripts/Test.ps1 (Windows). Tests must not contact a
production Bridge, game client, AI provider or Discord. Do not broaden testing after
checks pass unless another change/failure justifies it. Keep credentials, runtime
histories, player exports, binaries and local configuration out of Git.

The user has authorized an ImGui combat display and local appearance/profile
controls. `/acai2r ui` and opening the engine's Settings panel are permitted local
UI actions. Rynth remains the owner of melee/missile power and attack height;
do not introduce a competing controller or make attacks to restore a slider.
Combat parsing may consume incoming combat messages for local aggregate counters
without retaining raw lines. New combat rendering must use the engine-owned
ImGui context and the matching ImGui.NET version; never create a second context.
