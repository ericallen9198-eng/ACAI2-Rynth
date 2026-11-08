# Combat UI redesign proposal

Status: source-reviewed proposal; no combat UI has been added to the pilot yet.
The existing ACAI2 deployment is unchanged.

## Findings from the existing implementation

- `ACAI2.Decal/CombatTracker.cs` is 1,987 lines and owns parsing orchestration,
  session storage, rendering, setup controls, recording and attack helpers.
  Split these responsibilities rather than porting the class wholesale.
- `ObserveChat` returns on a disabled component before adding it to sessionEvents.
  A presentation filter therefore affects collected totals. Separate observation,
  totals and display visibility; explicitly label any filtered total.
- Session events accumulate in a list; rolling rates filter that history and
  search for its first damage timestamp. Replace repeated full-history scans
  with bounded rolling buckets plus incremental session/component counters.
  Keep full event history only when recording is explicitly enabled; disclose
  bounded export retention rather than claiming an unlimited live-only history.
- FloatingCombatHud and ToonCombatText render the same text through different
  Decal paths. The object-anchored path uses a separate shadow and does not apply
  the full screen renderer's outline/bold options. One projection-aware ImGui
  renderer should give fixed and character-following modes identical styling.
- FloatingCombatText caps the visible list at six and groups same-kind bursts
  within 150 ms. Preserve intentional grouping but include target identity in
  its key, expose limits, and reserve room for rare procs during dense combat.
- Current presets cycle a few fonts, colors and arc shapes. Keep presets as easy
  starting points; offer precise controls in an Advanced section.

Retain the existing combat parsing knowledge, poison/acid distinction, sneak
attack percentage/average, component reporting, optional recording and character
profiles. Verify parser behavior with recorded examples before changing it.

## Proposed visual layout

Compact meter, roughly 260 logical pixels wide, user-scalable:

    COMBAT · Toe                       lock
    668 DPS                     3 taken/s
    ───── short optional DPS history ─────
    Poison 42%   Direct 35%   Acid 23%

Numbers above are illustrative, not measurements. Breakdown rows and history
are optional. Hover/click opens damage, healing, defense, summons and sneak
statistics without permanently enlarging the meter. Label the selected rolling
window (for example DPS · 60s); keep encounter/session DPS separately named.
Distinguish Live from Recording visibly. New session clears live statistics;
recorded files remain only when such files actually exist.

Floating text has no window background, border or input capture while locked:

                  +173 POISON       69 SNEAK
                     +HEAL         1,221 ACID
                            toon

Use actual parsed effect labels: a poison log must never be relabeled acid.
Effect colors remain consistent across floating text, breakdown rows and charts:
poison green, acid lime, healing teal, crit gold, physical neutral. Labels and
symbols preserve meaning for users who cannot distinguish these colors.

Two inward-curving lanes gently hug the toon while rising and fading. Use stable
spawn slots so a newly arriving line does not relocate every existing line.
Offer curvature, lane gap, rise distance, lifetime and scale controls with a
live local preview. Treat critical emphasis as modest size/weight change;
avoid flashing or large pulsing text.

## ImGui implementation direction

RynthCore API 92 exposes ImGuiContext, OnRender, WorldToScreen and viewport size.
These are verified SDK surfaces, not proof of working projection in this client.
Verify the compatible ImGui binding/native ABI before adding a package.

- Standard ImGui windows for meter/settings; transparent draw-list text for
  floating events and world-anchored debuff icons.
- A shared text draw helper renders a black outline using surrounding offset
  draws followed by colored text. This is a real outline approximation, not
  stretched glyphs. Bound visible lines and outline samples to control cost.
- Use actual regular/bold font assets supported by the engine's font atlas;
  do not promise arbitrary system fonts before verifying atlas ownership.
- Project the player's world anchor each render, then animate in screen pixels.
  Verify coordinate units, height offset and viewport origin in-game. If the
  anchor is invalid/behind camera, suppress those markers without substituting
  screen origin; offer an explicit fixed-screen fallback setting and status.
- Project debuff icons from each known object's ID, not a matching mob name.
  Cap marker count and prioritize selected/nearby affected targets. Show a
  timer only when duration is known; distinguish inferred effects from verified
  enchantments. Hide occluded/off-screen markers when reliable information is
  available; projection alone does not prove visibility through walls.
- Call native readers from supported callbacks and publish immutable render
  snapshots. No HTTP, file writes or combat aggregation inside drawing.

## Settings and persistence

Simple: enable text/meter, anchor mode, scale, preset, lock layout.
Advanced: per-effect RGBA picker with numeric hex entry, separate panel/text
opacity, font weight, outline thickness/opacity, smoothing, lanes and curvature,
proc filters, grouping interval, density cap and preview. Verify color-picker
availability against the chosen ImGui binding. Reset each section independently.

Save versioned profiles per server and character with atomic replacement and
save debounce. Preserve lock, window position/size, effect colors and settings.
Store viewport metadata and clamp/reposition windows after resolution changes.
Offer explicit copy-profile; do not silently overwrite another character's UI.
Attack power restoration belongs in its own later action module, not the meter.

## Delivery order

1. Parser/event model and aggregation with representative log checks; no game actions.
2. ImGui fixed meter and preview, settings persistence and input/lock behavior.
3. Live parsed floating text with fixed-screen mode and effect colors/outline.
4. Character projection and target markers after Windows coordinate validation.
5. Reconcile reports/recording and migrate compatible existing preferences explicitly.

The pilot's existing checks do not validate any of these future features. Each
stage requires the relevant CPU-only checks and a Windows visual/game validation.
