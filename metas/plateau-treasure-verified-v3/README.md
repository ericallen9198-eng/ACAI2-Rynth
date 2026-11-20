# Plateau Treasure - Verified v3.1: startup compatibility fix

Adapted from Soot's Plateau Treasure Beta and the supplied working MLTREASURE275-Rynth meta.

This version automatically continues after death instead of stopping. NPC verification, optional task flags, explicit currency pickup and the v2 currency loot profile are retained.

INSTALL
1. Stop the old macro outside an active encounter.
2. Replace both Verified .met and .af files in active MetaFiles.
3. Copy ALL 19 .nav files to active NavProfiles, including 275 - From Lifestone.nav and the eight PT Verify navs.
4. Keep DW_Farm_Peas_Rares_Custom-Currency selected; it is unchanged from v2. The included copy goes in LootProfiles if needed.
5. Load /ra meta load 275 Plateau Treasure - Verified.met and start in Default.
No plugin, build or Bridge update. Do not use v2's Default if you want automatic continuation.

RYNTH SETTINGS
Startup applies: StopMacroOnDeath=False, DeathRecallMode=0, TravelToOwnCorpse=False, KeepHuntingAfterDeath=False. These give the meta ownership of death recovery. In the settings layout inspected:
- Recharge -> Safety Stops -> Stop Macro On Death OFF.
- Recharge -> On Death -> Recall On Death Off.
- Looting -> Travel Back To My Corpse OFF.
- Outdoor Hunt panel -> Keep Hunting After Death OFF; don't run Outdoor Hunt alongside this route meta.
Recover My Corpse can stay on for opportunistic nearby corpse pickup; it is not changed. Do not disable ordinary recharge, buffing or looting. These recovery choices are written to the active RynthAi profile.

DEATH FLOW
Rynth's built-in death/portal hold prevents actions while dead and settling after respawn; the macro itself stays running. Every Death condition enters DEATH_RECOVER, which restores a cache-only pickup hold, clears movement, invalidates interrupted map/cache work without recording reward success, and waits for positive health plus portal exit. After at least six seconds it routes from the natural lifestone respawn. A verified prior hunting region is resumed; if the initial hunter sequence was interrupted it verifies the hunters first. There is no extra /ls issued for a normal death return, no required corpse trip and no manual macro restart. A second death restarts this recovery path.

KNOWN RETURN LOCATIONS
The working supplied meta waits for camp cell 0x21B70028; that is the direct-respawn return supported here. The recorded town landing in block 0x14B5 also works via 275 - From Lifestone.nav (the old camp-portal approach without any recall step). Unknown respawn locations hold after 90 seconds rather than walking blindly. Your lifestone should match one of these recorded return locations.
Normal startup still uses /ls if you are away from camp. It checks where /ls actually lands before choosing an approach: camp directly, or the recorded town portal path. Camp arrival can be confirmed by location even if portal chat/events were missed.

OTHER FAILURES
Unrelated errors still enter ERROR_HOLD/HOLD and stop with a message: an unrecognized NPC reply after 3 attempts, failed travel, a cache timeout or an unconfirmed sent pickup. Automatic death recovery does not replay an interrupted item move or silently claim its currency.

LOOT AND TASKS
Doubloons (template 1004101) and any TradeNote objects (class 15) present in the bound open Unearthed Cache are explicitly moved and acknowledged. Notes are optional; the existing corpse payout completes without a cache. Task completion/cooldown never stops the treasure loop or changes zones. The native Pickup with macro off setting is temporarily suppressed only during direct cache collection and restored on finish, death recovery, error handling and Default restart. A manual mid-cache stop followed by another meta may require restoring it yourself. Actual pack space is still required.
The 52-rule Currency loot profile differs from the supplied original only by removing the six-free-main-pack-slot condition on its two enabled currency Keep rules. All other rules/actions/conditions/order are preserved.

No source spell 1636/Lifestone Sending travel remains. The original treasure-encounter magic-mode request remains unchanged. Offline checks do not prove live game behavior; verify one death return and one cache.

V3.1 STARTUP FIX
Removes the portal-expression and optional maximum-health dependencies from startup/return readiness. Your reported camp cell 0x21B70028 and positive current HP now pass the same native cell condition used by the supplied working meta. Native portal entry/exit conditions restart the settle timer when observed; there is no latched portal flag that can block progress after a missed event. A timeout prints the actual cell and current HP rather than claiming your lifestone is wrong.
Replace BOTH .af and .met in MetaFiles and reload in Default. It should report 377 rules. Navs and the currency profile are unchanged from v3.
