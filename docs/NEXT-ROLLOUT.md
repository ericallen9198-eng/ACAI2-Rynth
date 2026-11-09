# Next rollout: reuse existing Rynth features

This plan supersedes the earlier feature-by-feature migration order. The next
rollout is a focused combat/UI integration milestone, not a complete ACAI2 port.
The existing ACAI2 installation remains the working deployment.

## Review status

The user explicitly authorized collaboration with
https://github.com/SootAC/rynth-dreamweave . This session cannot yet read it:
Git clone returned HTTP 403; GitHub repository lookup could not resolve it;
the public page/raw README returned 404. These results do not establish whether
it is private, renamed or unavailable. No Soot source was reviewed, copied or
modified, and no message/issue/PR was sent to its maintainers.

The decisions below use previously verified RynthCore/RynthSuite sources and
published RynthThreads metadata. Soot-specific redundancy decisions remain
pending an accessible source revision. Collaboration permission does not itself
make that source accessible to this execution session.

## Ownership and overlap matrix

| Area | Verified existing capability | Next-rollout decision |
| --- | --- | --- |
| Melee/missile power and manual height | Rynth settings include separate power/Auto Power and height preferences | Keep Rynth as sole controller. Validate its behavior on mode changes/restart; do not infer retail slider restoration from a saved preference alone. |
| General floating combat numbers | Rynth Vision supplies combat text | Reuse the general display. ACAI2 proc overlay remains optional/off by default; evaluate Soot's effects before extending another renderer. |
| Compact DPS/component/sneak statistics | ACAI2-Rynth v0.2.0 implements local aggregates and an ImGui panel | Keep for now. Compare Soot's meter/parser before adding more features or a second event feed. |
| Fragment Press recording and component rates | RynthThreads advertises press history, levels, modifiers, comparisons and export | Pause a second recorder/store. Verify current message compatibility and an import/event interface first. |
| Thread instance recognition | RynthAi public ServerPolicy recognizes Thread entry/realm messages and tracks the dungeon | Reuse its semantics; preserve the existing generated-patrol restriction. Do not equate instance detection with completion tracking. |
| Adaptive Thread attack height | No equivalent confirmed in the reviewed source | Preserve ACAI2's learner as candidate custom logic. Require supported attack observations and a single-owner Rynth setting-update path before enabling it. |
| Toon/mob anchoring and debuffs | Rynth has internal camera-aware projection; a suitable external anchor interface is not confirmed | Inspect Soot's anchors/debuff data first. Do not duplicate projection, identify effects by mob name alone, or expose a nonfunctional follow switch. |
| Recipe recommendations/interactions | Published RynthThreads descriptions establish rates, not validated optimal-recipe/interaction inference | Add only analysis missing after source/data review. Keep starting level, recipe, result and run outcome distinct. |
| Keyfer/arena/Bridge | Existing ACAI2 deployment owns the live behavior; new pilot only has optional telemetry | Leave live responder migration out of this rollout; avoid duplicate replies or broadened network routes. |

## Delivery order

1. **Finish the source comparison once Soot's repository is accessible.** Pin its
   commit and record features, exports, settings/data paths, dependencies and
   applicable attribution. Inspect parser and UI code, not only its README.
   Determine whether it contains or integrates RynthThreads; do not assume the
   separate repository is the same project because both target DreamWeave.
2. **Validate the current Windows UI before expanding it.** Exercise native
   loading, both render paths, game input, sample/live separation, locked saved
   profiles, viewport changes and character switching. Compare real poison,
   acid, sneak, pet and incoming damage with chat. Fix confirmed discrepancies.
3. **Choose one owner for each shared function.** Prefer existing supported APIs,
   events or exports. If none exist, propose a small versioned integration contract
   in our docs; user authorization permits collaboration, but no maintainer
   message or upstream PR is part of this review. Do not reference private
   assemblies by reflection or edit another plugin's live configuration files.
4. **Add a narrow Thread data adapter if an adequate interface exists.** Read-only
   import of documented exports is an acceptable fallback. Track source/version,
   originating record identity and confidence/completeness; deduplicate imports.
   Do not silently import old player data or write into another plugin's store.
5. **Ship only verified additions.** The UI milestone can proceed independently of
   an unavailable Thread/debuff interface. Leave unverified integrations clearly
   pending rather than replace working tools with partially connected features.

## Concrete Thread compatibility checks

Use the user's actual Raw Fragment (Level 300) description, the reported raw-325
case resulting in a level-335 Thread Gem, and the imprint message:

> The fragment takes the imprint: Yellow Taper x4, Willow Talisman, Colcothar, Powdered Lapis Lazuli, Mugwort, Gold Scarab.

Confirm dose quantity, starting versus result level, resulting modifiers and
entries, repeated component recipes, incomplete presses, re-entry versus new
run, and start/completion attribution. Starting level 325 in the example is
user-reported; the resulting level-335 gem dump alone does not prove that origin.
Never turn that assumption into a verified training label. Keep correlations
separate from proven causal effects or component interactions.

## Release acceptance

- Windows plugin loads and renders without errors; saved layout survives restart.
- Combat totals match representative logs; the preview never changes live totals.
- No duplicate power/height owner, automatic second text overlay or double-counted
  combat feed. No new game actions or live Keyfer responder.
- Every included upstream integration has a pinned revision/interface and relevant
  controlled checks plus Windows validation; missing capabilities remain explicit.
- If Thread import is included, matching known input/result examples and repeated
  import must preserve levels, quantities, uncertainty and record counts.
- Update STATUS/usage with actual results. Existing 56 checks establish the current
  CPU/compile baseline; this documentation review is not a new runtime validation.
