MLTREASURE275-Rynth.met — cache doubloon pickup patch

Original MLTREASURE275.met and DWv_4.utl were not modified.

Finding:
DWv_4.utl has an enabled Keep rule for template type 1004101 (Marae Lassel
Doubloon), with a minimum of 6 free main-pack slots. CACHE_SEARCH in the meta
explicitly disables normal looting, uses Unearthed Cache repeatedly, and waits
for an inventory increase without any pickup action.

Patch:
- Only in CACHE_SEARCH, only while the currently open container is named exactly
  Unearthed Cache, and only before the existing 300-second search deadline:
  select contents with template type 1004101 and request moving one stack to the
  player's main pack. Retries are limited to one every 2 seconds.
- Uses Rynth's actiontrymove with stacking enabled where supported.
- Success requires inventory gain AND no remaining visible doubloons in the open
  container. This avoids leaving additional doubloon stacks behind.
- Do not repeatedly /ub use the cache while it is already open.
- Initializes/resets the new retry timer alongside the existing cache timer.
- Corpse looting remains on the original loot-profile path. No other items are
  picked up by the added direct-transfer rule. Other original states are retained.

Installation/test:
1. Pause the macro and keep the original meta as a backup.
2. Put MLTREASURE275-Rynth.met beside the original in the folder from which you
   normally load metas. Select/load this distinct filename using your usual method.
3. Leave free main-pack slots; direct pickup bypasses UTL free-slot rules.
4. Test one cache. Confirm doubloons leave the chest and enter your inventory,
   and the normal Treasure Hunter report appears before the route continues.
5. If it stalls, stop the macro. Record the active state, chest's exact name,
   installed RynthAi version and any expression errors before reverting.

Evidence and limits:
Verified functions against upstream RynthSuite snapshot 44571693d76c3b388e48b1a82f2a0518ecd3aac3
(Snapshot for release 2026.11.12.1):
Plugins/RynthCore.Plugin.RynthAi/Meta/ExpressionEngine.UbCompat.cs
Plugins/RynthCore.Plugin.RynthAi/Meta/ExpressionEngine.cs
Plugins/RynthCore.Plugin.RynthAi/Meta/ExpressionReference.txt
Parsed original (179 rules) and patch (180 rules) with that snapshot's actual
MetFileParser.cs and LoadedMeta.cs: zero warnings. The single embedded nav is
byte-preserved and parses identically. Expression functions confirmed in source;
no live game execution performed. Older installed builds or missing host API
capabilities may not support the same functions. A failed transfer will retry
within the original timeout; a process/game error is not a confirmed pickup.

The original meta already has a five-minute timeout path that resumes without
collection on timeout; this patch preserves it. Test attended before unattended use.
If the main-pack existing doubloon stack is full and the merge attempt is refused,
the current source's actiontrymove may keep retrying that merge. Leave room in
that stack, or report the behavior for an adjustment to unstacked transfer.
