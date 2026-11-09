# Migration roadmap

The immediate scope is now the [next-rollout integration plan](NEXT-ROLLOUT.md):
validate the v0.2.0 Windows combat UI and reuse existing Rynth/DreamWeave features
before extending the port. SootAC/rynth-dreamweave collaboration is authorized,
but its source review is currently blocked by repository access. Soot-specific
capabilities are unverified; no implementation is assumed from the project name.

1. **Next rollout: combat UI and redundancy review.** Validate native rendering,
   combat totals and saved layout. Compare Soot's source when accessible; choose
   one owner for each parser, renderer and setting. Rynth owns power/manual height;
   Rynth Vision owns general combat text unless the user explicitly selects the
   optional ACAI2 proc overlay. Keep the overlay off by default.
2. **Thread integration after compatibility checks.** Evaluate RynthThreads/Soot
   recording, current imprint/examination formats and export/event interfaces.
   Prefer an adapter to a second press store. Add missing recommendations and
   interaction analysis only after establishing trustworthy input data.
3. **Distinct combat/HUD additions.** Integrate verified self/target debuff data,
   camera-aware anchors and the portable Thread height learner where upstream
   interfaces support them. Do not expose disconnected controls or competing
   attack-height writers.
4. **Keyfer adapter in a separate milestone.** Reuse the Bridge, validated message
   routing, speech queue and arena receipts. Enable only one responder and preserve
   channel privacy, General gates, cooldowns and context-aware continuation.
5. **Remaining parity.** Reports, recording, Thread run outcomes and other features
   follow measured gaps in upstream support rather than a wholesale class port.

The existing ACAI2 remains the working deployment until feature parity is proved.
No implicit migration of its histories, player data or preferences. Preserve
RynthCore/Suite capability checks and DreamWeave-specific patrol restrictions.
No implementation-date or complete-parity promise is made by this roadmap.
