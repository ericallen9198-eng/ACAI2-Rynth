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

The user has now authorized short client-controlled approach movement between the
three named nearby Thread stations. Do not gate native use on a center-distance
comparison with UseRadius. Bind stations to the original local batch area (12m),
allow positional/cell changes only while the intended vendor/press use is pending,
then re-anchor after acknowledgment. No route runner or general navigation. The
user clarified DreamWeave bank funding is automatic: do not manage, exchange,
require, or denomination-convert physical trade notes. Submit native vendor quotes
unchanged and report native refusal reasons. Keep budget accounting separate for
each quoted currency rather than combining incompatible denominations.

The user explicitly authorized a per-character Auto-withdraw notes toggle. Default
off: a physical vendor-currency shortage stops with an error. When enabled, only
for the confirmed Trade Notes (250,000) currency, the Thread batch may issue the
fixed `/b w n <positive integer shortage>` command on the pump after its durable
checkpoint. Require matching native BANK withdrawal acknowledgment and a counted
currency inventory increase before purchasing. No automatic retry, bank-balance
guessing, exchanges, deposits, unrelated bank commands or replay after restart.

The user has now authorized the separate ACAI2 Combat plugin: adaptive DPS/defense
meter, artistic floating text, automatic observed class modules, bounded mob/run
statistics, and the explicitly supplied server death notice with numeric overkill.
No Keyfer/Threads dependency or shared live parser state. No combat actions,
settings changes to other plugins, debuff renderer or full chat/CSV recording.
Only bounded explicit death notices may be persisted with aggregate run records.
Use engine-owned ImGui, real pinned SDK and matching bindings. Do not guess camera
projection without frame-cell data; disclose fixed-anchor limitations. Preview
must be labeled synthetic and isolated from real statistics and saved records.

Combat display refinement authorized by the user: incoming effects left, outgoing
right, heals centered above the head/screen anchor. Short landed vuln/imperil text
is permitted while icons remain Rynth-owned. Keep physical hits/evades separate
from magic hits/resists; unknown delivery must not enter either denominator.
Repeated identical float values may use xN without changing true damage totals;
criticals remain separate and visually stronger than regular hits of every kind.

The user authorized compact big-hit records with attacker, delivery/spell,
crit/Reckless markers and at-hit imperil/vuln context, plus an automatic Reckless
percentage/average/high/low module. Keep at most 24 structured big hits per run and
bounded death records; do not retain an ordinary combat transcript. Native player
enchantment reads and confirmed change events are allowed, without identification
or game actions. Use pre-hit samples/deltas; missing/stale/partial data is unknown,
not proof of no debuffs. Preserve Reckless as hit metadata, not part of a mob name
or an inferred damage multiplier/skill owner. Never retroactively invent context
for old aggregates, or double-count a correlated hit and its killing-blow notice.

The user now authorizes Normal/Compact/Ultra-compact modes with one shared per-pack
DPS lifecycle and per-character appearance persistence. Locked ultra digits use
only the existing foreground floating-text renderer; unlocking may reuse the
existing meter's transparent drag target, not a new window system. Reckless is now
an outgoing-only recent-tag ON/OFF indicator; stop accumulating/displaying incoming
Reckless while preserving its actual physical damage and legacy files. A validated
read-only retail ClientCombatSystem power sample is authorized; no writes, combat
method calls, hooks or fabricated percentages. Kinetic ready/release/expiry are
separate states. A release may decorate the next observed direct weapon hit, but
never add that damage twice or invent an amount when correlation is unavailable.
Do not suppress ordinary equal chat lines globally: genuine hits, ticks and evades
can be identical. Preserve the separate physical/spell/periodic classification.

Floating notice backings are now removed: vuln/Imperil use open line accents and
Kinetic Ready uses electric edges, settling to a smaller READY cue. The user
authorized only three grouped notification scales (Buffs/Debuffs/Skills), saving
per character and scaling text plus graphics together. Keep ordinary damage,
crits and healing on their existing controls. Preserve explicit status/skill
origins; a generic Utility label must not guess classification or alter totals.
