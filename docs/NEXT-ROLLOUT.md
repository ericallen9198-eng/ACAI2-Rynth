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

Deferred: combat UI/scrolling text, Thread Gem prediction and the previously
researched Soot Thread integration. Their previous research stays available in
SOOT-SOURCE-REVIEW.md and RYNTH-THREADS-OVERLAP.md; this milestone does not deploy
those features or duplicate Rynth's own systems. The Future combat assembly is
not a ready standalone plugin; extracting its renderer/manifest/package is later
work if requested.
