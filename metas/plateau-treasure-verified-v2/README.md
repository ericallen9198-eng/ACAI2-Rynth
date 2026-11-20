# Plateau Treasure - Verified v2

Preserves the verified sequential NPC conversations and task-independent treasure hunting from v1.

INSTALL
1. Stop the current macro outside an active dig encounter.
2. Copy the .met and .af to active MetaFiles (replace the previous Verified files).
3. Copy ALL .nav files to active NavProfiles, especially the updated 275 - Get to.nav and the eight PT Verify navs.
4. Copy DW_Farm_Peas_Rares_Custom-Currency.utl to LootProfiles and select it.
5. Load: /ra meta load 275 Plateau Treasure - Verified.met ; start in Default.
No plugin, build, Bridge, or character-profile replacement.

CACHE PICKUP
Uses explicit item movement adapted from the supplied, working MLTREASURE275-Rynth.met. Only Marae Lassel Doubloons (template 1004101) and TradeNote objects (class 15, all denominations including MMDs) in the bound OPEN Unearthed Cache are moved. General looting stays off during collection. Each dispatched move must produce inventory growth and source removal/shrinkage before the next item; partial stack merges count as progress and the remainder is moved to a free slot. Trade notes are optional; a doubloon-only cache completes normally. The existing corpse-payout path is preserved and does not wait for a cache. In the cache path it does not finish just because doubloons increased: any currencies that were present must be absent from the same opened cache on two checks two seconds apart, after at least one confirmed pickup. No blind resend of an unconfirmed dispatched move. Busy/rejected local dispatches can retry.

The native Pickup with macro off option is temporarily held off during cache collection to avoid a second looter competing. Its prior value is saved and restored on normal finish, hold/timeout handlers, hunt resume and next Default start. If death stops the macro before its handler runs, restoration occurs when this meta next starts in Default. If you manually stop in the middle and switch to another meta, restore that setting yourself or start this meta in Default first. Ordinary hunt looting resumes as before; this direct cache collector does not take other cache item types. Actual inventory capacity is still required.

A cache that cannot be opened/collected within five minutes, or a dispatched move lacking acknowledgment for 20 seconds, holds with a local explanation instead of claiming success or walking away. An initially empty cache with no confirmed pickup is not assumed successfully looted.

LOOT PROFILE
The supplied profile already had enabled Keep rules for trade notes and doubloons. The Currency copy removes only the six-free-main-pack-slot condition from those two rules; every other rule, action, condition, order and enabled state is preserved. No broad Keep-all rule.

TRAVEL
All Lifestone Sending nav steps are replaced with a Chat /ls step, retaining the original ten-second wait and recorded approach to the Plateau camp portal. Your /ls destination must match that recorded starting area. No wand or Lifestone Sending spell is requested for this travel.

UNCHANGED
NPC reply verification, alternate assignments, continued treasure hunting after task completion, existing regional hunt routes, combat profile and core map/dig recovery. No new zone switching, auto-selling, resupply or daily scheduling. Original treasure encounter magic-mode request remains. Offline parser/simulation checks do not replace a live NPC and cache test.

DEATH AND STRUCTURE REVIEW
Native Stop Macro On Death remains enabled. Every active state has a first-priority Death branch. Failure/timeout holds clear motion, disable activities and issue /ra stop; there is no automatic /ra start. Restart deliberately in Default after death/review so the cache-only option is restored before new travel. Default also clears stale held movement. RynthCore/RynthAi's independently configured death-recall/corpse-recovery features are not changed by this meta.

An ordinary XP message alone cannot end the encounter: that fallback also requires a visible Unearthed Cache. Explicit encounter-completion messages and the existing corpse doubloon payout still work. Encounter timeout now holds for review instead of resuming an unconfirmed encounter.
