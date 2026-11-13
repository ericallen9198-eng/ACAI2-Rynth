# ACAI2-Rynth working rules

This is the separate Rynth migration project. Do not modify the old Samm-AI checkout,
installed C:\ACAI2 tree, player data, or configuration as part of this project.

Current rollout scope is Keyfer chat migration. The user explicitly authorized restoring
normal Keyfer functionality, including conversational tells, public replies, arena,
emotes and existing event/treasure/leaderboard services through the local Bridge.
Only the configured server/character may enable this adapter. Session cancellation,
bounded queues and pump-only native dispatch are required. Private tells may reach
only the existing local Keyfer ask route for that conversation; they must not be
exported as telemetry or public observations. Prize transfers require a separately
configured exact supported one-MMD template; they default disabled.
No combat UI or combat parsing is included in the normal plugin package. Preserve
its source for a possible separate future plugin. No attacks, casts, movement,
item use/identification, equipment changes or competing Rynth automation.

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

The user additionally authorized a separate standalone Thread-handling plugin,
including an explicitly started native gem-preparation workflow adapted from the
provided ThreadPrep meta. This does not broaden Keyfer's action surface. The Thread
plugin may request identification, buy shortages within an explicit batch budget,
and apply components/press only for its bound batch, on the pump, with durable
pre-action checkpoints and inventory/server acknowledgments. It must stop on
uncertainty, logout, combat, movement or a competing macro, and never redose/repress
a possibly issued action. No automatic gem destruction, movement or broad dialog
acceptance is authorized in this first standalone implementation. Keep it separate
from Keyfer and preserve private source archives out of Git.

The user additionally authorized automatic RynthAi pause/resume for an explicit
Thread gem batch. Use its versioned Script API on the pump, remember runtime
ownership, wait for idle, and restore only a macro this batch paused after a
normal completion or settled user stop. Use an acknowledged expiring Yield hold
to prevent background item moves without editing saved AutoCram settings. Never
resume on uncertainty, death, identity change, logout, restart or stale ownership.
