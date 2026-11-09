# Next rollout: shared tracking, custom presentation

The user-supplied Soot source ZIP has now been reviewed. GitHub access is no longer
a blocker for this snapshot. See [source review](SOOT-SOURCE-REVIEW.md) for hashes,
code evidence and 13 controlled model checks. This plan replaces the earlier
provisional matrix. Existing ACAI2 remains the working deployment; this review
changes documentation, not installed plugins or game behavior.

## Ownership

| Function | Preferred owner | ACAI2 contribution |
| --- | --- | --- |
| Power/manual attack height | RynthAi | Existing settings shortcut; no second controller. Verify native-slider behavior separately from automation preferences. |
| General and pet floating numbers | Rynth Vision/Core with Soot's pet text fixes where compatible | Optional outlined DreamWeave proc display only; avoid duplicate crit/heal/pet numbers. |
| Press records and descriptive component/recipe statistics | Repaired RynthThreads | Fix imprint/starting-level/dose evidence and unknown-recipe handling; add missing analysis over one trusted record source. |
| Thread run history/progress | Standalone ThreadTracker | Read-only integration and eventual verified gem-to-run correlation, not a second run database. |
| General session XP/luminance | RynthTracker | Reuse its data; do not duplicate `/lph` or bank polling. |
| Self/selected-target effects | ThreadTracker effect model and SDK registry semantics | Compact clear HUD/visuals over a supported snapshot, retaining coverage and object identity. |
| Component/sneak DPS meter | ACAI2-Rynth | Keep the compact ImGui presentation; validate parser coverage and avoid counting both a chat hit and its native notification. |
| Adaptive Thread attack height | ACAI2 learner, only through Rynth-owned settings | Still pending supported attack observations and a single-writer update interface. |
| Keyfer/arena | Existing ACAI2 deployment | Separate migration milestone; no second live responder in this rollout. |

## Delivery order

1. **Validate the v0.2.0 Windows display and dependency baseline.** Confirm native
   loading, render paths, input behavior, saved locks, character isolation and
   actual poison/acid/sneak/pet totals. Compare Soot's 2026.11.3.2 patch baseline to
   our SDK pin before proposing any engine changes. Do not install the bundle or
   stack its cumulative patches merely to obtain one display feature.
2. **Prioritize Thread capture quality.** In the repaired recorder or a compatible
   adapter, support the supplied imprint message, cache identified raw fragment
   level, and correlate them to the actual press. Preserve starting and result
   levels independently. Normalize component name and dose; keep missing recipe
   evidence distinct from an explicitly observed no-component press. Maintain one
   authoritative press ID/store rather than record the same craft twice.
3. **Define the small read-only interface.** The reviewed standalone tracker has
   no public run/effect snapshot export. Define a versioned contract before making
   ACAI2 depend on its internals; a user-selected structured export is a reasonable
   first adapter. Scope by server/character and stable source record ID; make
   repeated imports idempotent. No raw/extra chat, implicit old-history imports,
   reflection into private assemblies, source-store writes or copied live WAL DBs.
4. **Add the clear self/selected-target debuff display.** Reuse verified registry
   classification/coverage semantics. Preserve unresolved effects and estimated
   versus observed expiry; clear stale readings and same-name/different-ID targets.
   All-mob icons and toon following remain later until camera-aware anchors and
   per-object observation coverage are available through a supported API.
5. **Extend analysis only after reliable capture.** Retain existing exact-recipe
   comparisons and uncertainty reports. Add dose/starting-level-aware predictions
   for luminance, XP, difficulty and salvage, with explicit sample counts and
   uncertainty. Evaluate on held-out crafts grouped appropriately by recipe/batch;
   do not present raw frequency comparisons as causal synergy. Join run outcomes
   only where gem identity is known, not by dungeon name or equal levels alone.

Steps 2–4 may ship as separate verified increments. Do not hold a working UI
release for an unavailable integration interface, or expose switches that lack
a working data path. Thread learning, Keyfer and general automation remain outside
this rollout's implementation scope until their interfaces are ready.

## Minimal proposed record contract (not implemented)

- **Press:** schema/source version, source record ID, server/character, craft time,
  raw-fragment object/correlation evidence, nullable starting level, recipe status
  (`observed`, `explicitly-empty`, `unknown`), components `{name, quantity, rawLabel}`,
  result level/entries/modifiers, completeness and evidence quality.
- **Run:** source record ID plus scoped realm/instance and time identity, observed
  progress/clear/exit milestones, gaps, metrics and source confidence; nullable
  linked press ID. Retain instance-reuse uncertainty after server restarts.
- **Effects:** subject object ID, selected-target identity, sampled server time,
  coverage/staleness, spell IDs/categories and nullable/estimated expiry. Selected
  target coverage must not imply all nearby mobs have been sampled.

Only structured allowed fields cross the boundary. Source logs and incidental
chat are neither required model inputs nor part of Bridge telemetry.

## Acceptance examples

- Raw 300 examination preserves origin 300. A reported raw 325 producing a 335
  gem must not become an observed origin-325 label from the result dump alone.
- `The fragment takes the imprint: Yellow Taper x4, Willow Talisman, Colcothar,
  Powdered Lapis Lazuli, Mugwort, Gold Scarab.` preserves six names and the taper's
  quantity four; missing imprint/recipe does not enter a no-component control.
- Automatic/manual press result, timeout, quoted chat, unrelated conversation,
  dose changes, item replacement, duplicate import and incomplete-record cases
  all preserve evidence quality without leaking Raw/Extra into integrations.
- Thread re-entry resumes one run. Clear and gem crumble remain separate. Unknown
  XP gaps, bank replies and gem-to-run links remain unknown; bank delta is not
  automatically earned luminance.
- Effects tests cover unknown spell IDs, unavailable/truncated snapshots, stale
  expiry and two enemies with the same name but different IDs.
- Windows checks verify no competing power/height writer or doubled floating
  events. Preview never affects live totals; layout survives restart.

The existing 56 ACAI2 checks are the prior compile/CPU baseline. The 13 additional
source-review probes do not establish native runtime, SQLite or game validation.
No new application feature is claimed implemented by this planning update.
