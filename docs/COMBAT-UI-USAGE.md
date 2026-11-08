# ImGui combat display (v0.2.0)

Publish on Windows with `scripts/Publish.ps1` and load through the RynthCore
CoreCLR launcher (API 92+). This is independent of the old ACAI2/Decal installation.
The source build and CPU checks pass here, but the Windows renderer is unverified.

After login the meter shows **DPS · 60s**, damage taken/sec and Live/Not recording.
Startup rates divide by elapsed time since the first damage event, capped at 60s,
with a one-second minimum. The ring contains whole-second buckets; it is not an
exact subsecond trailing window. Total/component damage remains for the session
until New live session, logout or character change. Nothing is written as a combat
recording. The optional general observation JSON export is a separate feature.

Use `/acai2r ui` or the meter's Setup button:

- Show/hide meter; lock/unlock its position. Drag an unlocked meter to move it.
- Enable ACAI2 proc text. It starts **off**: Rynth Vision already supplies general
  combat numbers. Choose one source for effects shown by both.
- Preview sample poison/sneak messages without adding them to real totals. The
  preview is explicitly labeled and automatically exits when changing characters.
- Advanced appearance: font size, black outline thickness, inward curvature,
  lane gap, rise, lifetime, normalized screen anchor and per-effect RGBA picker.
  Click a color swatch for the wheel/alpha/hex popup. The engine's current font
  is used; arbitrary font loading and bold weight are not enabled.
- New live session clears this meter's counters; there is no file deletion.
- Open Rynth Settings for its existing Combat melee/missile Auto Power, power
  percentage and attack-height controls. ACAI2 does not start attacks or change
  these preferences. The engine owns their persistence and attack behavior.

Appearance and position save at most once every two seconds while changing and
flush on logout/shutdown. Profiles are server/character-specific and independent
of login object ID. Small, versioned JSON files live in
`%LOCALAPPDATA%\ACAI2-Rynth\CombatProfiles`. Unknown/corrupt profiles are preserved
and reported in setup; they are not silently replaced. Moving an off-screen
meter back inside a resized viewport updates its saved position.

Proc display has a bounded 16-event buffer. Same-effect events from the same named
target within 150ms group as a total plus tick count; different target names do
not merge. Outgoing direct hits are shown as floating text only for sneak/crit.
The meter still counts all parsed damage independently of floating visibility.
Targets with identical names cannot be distinguished by text-only logs.

## Windows acceptance checks

1. Leave Bridge telemetry and public raw-chat retention off. Confirm the real
   server/character and that the ImGui meter opens with no errors.
2. Open Preview; verify clear background, black outline, rising inward arcs and
   the SAMPLE label. Verify live DPS stays unchanged by preview.
3. Change a color (including alpha/hex), opacity and arc. Drag and lock the meter;
   restart/relog and confirm settings restore only for that character.
4. Resize the game, hide the meter, then use `/acai2r ui` to restore it. No invisible
   floating-text window should capture game clicks. Settings controls do capture
   clicks while being used.
5. Disable Preview, optionally enable ACAI2 proc text, then compare poison/sneak
   events with game chat. Observe groups/tick counts and rate expiry after combat.
6. Open Rynth Settings; confirm that power and height use its existing values.
   Changing ACAI2 appearance must not change attack settings or initiate attacks.
7. Change character/server: totals and preview reset; saved appearance is isolated.
8. Test both with the Rynth shell open and with its overlay render path active.

Toon-following text is intentionally not exposed yet. Rynth's own MonsterHud
converts landblock coordinates relative to a captured camera frame and swaps the
vertical axis before projection. The public SDK does not expose that frame cell;
raw `TryGetObjectPosition` coordinates cannot safely be passed through unchanged.
Thread Auto likewise awaits a supported attack-observation/settings-update path.
These pending items are not represented as working switches.
