# Next rollout — Keyfer first (2026-11-13)

The user has paused the combat rollout and may make combat a separate plugin.
Rynth owns the normal game/automation UI. v0.3.0 therefore ships the Keyfer adapter
and excludes combat rendering and aggregation from its dependency graph.

The existing Bridge remains owner of conversation, arena/sheets, AI, memories,
wiki, timeouts, private handoffs and treasure ledger. The Rynth adapter owns native
chat observation, verified login identity, bounded queues, fixed commands and paced
native delivery. CoreCLR engine API 92+ is required by the reviewed managed SDK.

Immediate gate: successful Windows publish, compatible installed engine, one
active responder, and attended tell/Allegiance/arena/logout checks. Test treasure
reporting before enabling the separately configured one-MMD payment template.
No production game commands or AI requests were run during source validation.
See [Keyfer setup](KEYFER-SETUP.md).

Deferred from the Keyfer package: combat UI/scrolling text. Thread research and
Soot-inspired preparation now have a separate standalone build described below.
The earlier research remains in SOOT-SOURCE-REVIEW.md and RYNTH-THREADS-OVERLAP.md. The Future combat assembly is
not a ready standalone plugin; extracting its renderer/manifest/package is later
work if requested.

## Soot collaboration review — November 13

The user wants Soot's features to run as a companion plugin, not require a custom
Rynth distribution. [Current patch/boundary review](SOOT-PLUGIN-BOUNDARY-REVIEW.md)
finds this feasible for an extracted subset: the supplied API97 archive currently
patches 139 Suite paths and 37 Core paths. Use existing versioned Rynth interfaces,
keep the action owner in RynthAi, and propose engine/LOS/chat fixes upstream.
First candidate is read-only Thread/survey data/UI with one authoritative recorder.
Keyfer remains separate; combat stays excluded. No patch was applied or deployed.

## Standalone Threads v0.1.0

The user subsequently authorized native gem preparation in the separate plugin.
Implemented compact per-character Thread run/press records, recipe prediction,
an engine-owned ImGui panel and an explicitly started gem-prep controller using
stock identify/vendor/item APIs. Optional shortage purchases are budgeted; each
dose and press requires acknowledgment. No Lua/meta runner, engine patch, movement
or automatic destruction is included. Keyfer remains separate.

36 fake-world Thread checks and the existing 99 Keyfer checks pass locally.
Windows packaging and an attended one-gem test are separate release gates.
See [Thread setup](THREADS-SETUP.md); no live gem actions were made here.
