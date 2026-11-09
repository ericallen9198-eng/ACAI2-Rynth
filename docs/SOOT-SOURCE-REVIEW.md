# Soot DreamWeave source review

Source: user-supplied `rynth-dreamweave-main.zip`, sharing snapshot dated
2026-11-08. This resolves the earlier source-access blocker without requiring
GitHub account changes. No Git commit ID is available in this ZIP; identify this
review by archive hashes rather than inventing a revision.

- Outer archive SHA-256: `d0354406b28ea45ce929a171ee4def11651340f324b8aba0d3b243cca5555ecb`
- Nested source archive SHA-256: `8844847276cad3d6dffbe07b0863544d56bcdee0ac935fb8069a4136db509f4a`
- All 770 source content-manifest entries matched their supplied SHA-256 values.
  This checks internal consistency, not independent authorship/authenticity.
- Reviewed relevant source, not every route/meta. No installers, patchers, runtime
  DLLs, metas or bundled verification scripts were executed. No third-party source,
  runtime archive, player store or private dataset is committed to ACAI2-Rynth.

## Verified overlap

Paths below are relative to the nested `Rynth-DreamWeave` source package.

| Area | Source evidence | Decision |
| --- | --- | --- |
| Press capture | `projects/RynthThreadsFix/source/...Threads/PressParser.cs`: manual use start plus repaired exact `Pressed. It holds.` start; result level, entries, modifiers, timeout | Reuse/extend this capture rather than start another authoritative recorder. Automatic starting level remains unknown without a manual metadata line. |
| Existing statistics | Same folder, `PressInference.cs`: individual component and exact-recipe groups, with/without comparisons, Wilson intervals, Fisher comparison and values grouped by starting level | Reuse these descriptive reports. Exact-recipe frequency is not a fitted interaction model or an optimal-recipe predictor. |
| Thread run lifecycle | `projects/RynthDreamWeave/ThreadTracker/Plugin/RunTracker.cs`, `RunDatabase.cs`, `ThreadTrackerPlugin.cs`: realm/instance identity, resume, server progress, crumble/end, SQLite history | Prefer this run source to a new run database. Preserve observation gaps and inferred-versus-known labels. |
| Run combat/effects | Same plugin, `CombatFacts.cs`, `EffectContext.cs`, `EffectHostReader.cs`: self/selected-target enchantments, resist/death context, object-ID invalidation, stale/truncated coverage | Reuse semantics or a supported snapshot interface for the planned clear HUD. This is not an existing above-every-mob icon renderer. |
| Pet combat text | `native-source/RynthCore/.../MonsterHud/PetCombatLine.cs`, `CombatText.cs`: current-character pet boundary, prefixes, damage parse, engine projection | Prefer the existing engine renderer for general/pet numbers; keep custom proc text optional. Do not rebuild its camera transform. |
| Session XP/luminance | `native-source/RynthSuite/...RynthTracker/RynthTrackerPlugin.cs`, shared `ServerLphTracker.cs`: session state and server `/lph` handling | Do not create a second bank/luminance poller. RynthTracker and standalone ThreadTracker are different plugins. |
| Combat automation | Suite changes include Life/resource drains, live-pet/weakness handling, settings and compatibility work | Leave ownership with RynthAi. Do not import unrelated combat decisions, crafting or quest automation into the read-only pilot. |

No equivalent adaptive Thread attack-height learner or ACAI2's compact
component/sneak DPS UI was identified in the reviewed tracking/effect/text code.
This is a scoped finding, not a claim that no such feature can exist elsewhere.

## Confirmed data gaps to fix before model training

1. **Imprint/examination correlation:** the supplied imprint line does not start
   capture and is not parsed as `Components selected:`. The current manual start
   can capture `Raw Fragment (Level 325)` from chat, but automated press starts do
   not read an examined fragment's origin level. Correlate a cached examination
   and imprint to a specific item/use/press; ambiguous matches stay unknown.
2. **Unknown recipe is not an empty recipe:** `Press.Usable` currently requires a
   complete result with modifiers but not `ComponentsSeen`. `PressInference` puts
   zero-length component lists into its no-component baseline. A source-linked
   probe reproduced an imprint-only recipe being classified there. Require an
   explicit observed-empty recipe for controls; quarantine missing recipe evidence.
3. **Structured dose counts:** `SplitComponents` preserves `Yellow Taper x4` as one
   literal category; `HasComponent("Yellow Taper")` does not match it. Normalize to
   name/quantity/raw label before dose analysis. Preserve existing IDs and raw
   labels, and do not assume absent quantities when the recipe is incomplete.
4. **Filter evidence at the boundary:** while a press is open, unrelated chat can
   enter `Press.Extra`; `PressStore` serializes Raw/Extra. Our adapter should import
   only explicitly allowed structured fields, never forward those buffers to Bridge
   or model exports. Local source history remains untouched.
5. **Press/run relationship:** there is no reviewed public cross-plugin contract
   linking a crafted gem to a specific later run. Dungeon name or result level alone
   is insufficient. Clear progress and gem crumble are separate milestones: the
   tracker ends on crumble, not merely on the dungeon's cleared message.

## Integration constraints

ThreadTracker reads selected, already-observed enemies. Its snapshot reader uses
SDK player/object enchantments and server time, at about one-second intervals;
stale, full-buffer or missing results become unknown. Target object ID changes
invalidate previous context. This supports a selected-target/self HUD, not proof
of accurate registries for every nearby mob or world-positioned rendering.

The standalone tracker also issues `/b` requests and attempts a shared RynthAi
session reset. RynthTracker issues `/lph`. Reusing data/model logic is not permission
to silently embed their whole plugin lifecycles in the read-only ACAI2 pilot.
The reviewed ThreadTracker exports expose lifecycle callbacks, not a versioned
public run/effect snapshot export. Request/design a narrow contract or start with
an explicit read-only export; do not use reflection or live SQLite file copying.

The package targets Rynth 2026.11.3.2 with baseline revision labels Suite `a77109a`
and Core `6f1a2ed`. Our SDK pin is a different revision (`cf7e0f21...`). It contains
changed native files/patches, not a complete upstream checkout. Preflight against
a known compatible baseline before any eventual integration; no patch was applied
in this review and no supplied DLL was substituted for our managed plugin.

## Controlled verification

A temporary .NET 10 console harness compiled the unchanged pure press/inference
models and pet parser directly from the extracted source. Thirteen checks passed:
manual/automatic starting-level behavior, result fields, literal dose categories,
imprint gap, quoted-chat rejection, timeout, exact-recipe and level reports,
own-pet parsing/owner boundaries, unknown-recipe baseline and incidental Extra
capture. Compilation emitted nullable warnings in the reviewed reconstructed
models; no plugin/native runtime was loaded. The author's live-data import tests
were not run: they require excluded personal evidence and stores. Windows gameplay,
SQLite run persistence and graphical behavior remain unverified here.
