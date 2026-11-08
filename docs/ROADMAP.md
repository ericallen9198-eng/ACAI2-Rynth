# Migration stages

1. **Read-only pilot:** compile against genuine SDK, isolate observations,
   test wire contracts and bounded/cancelled work, then validate on one Windows
   RynthCore client. No automation or outgoing game chat.
2. **Keyfer adapter:** reuse the Bridge, route validated incoming messages, add the
   existing fixed speech queue and arena completion receipts. Enable only one
   responder. Preserve private/public histories, General gates and cooldowns.
3. **Combat and Thread Gem adapters:** reuse selected portable models. Resolve the
   missing exact outgoing item-use/cast correlation before asserting ownership of
   ambiguous fragments/targets. Carry forward uncertainty rather than guessing.
4. **HUDs (started in v0.2.0):** compact ImGui meter, saved locks/color/alpha settings; remaining font controls, debuffs,
   world-projected floating text and mob icons, with screen-locked fallback.
5. **Automation integration:** reuse Rynth's saved melee/missile power and height
   settings. Investigate a supported setting-update channel for the existing
   Thread height learner; do not create competing power/height controllers.

The existing ACAI2 remains the working deployment until feature parity is proved.
Do not migrate its SQLite histories, player data or preferences implicitly. Keep
RynthCore/Suite capability checks and DreamWeave-specific patrol restrictions.
No implementation-date or complete-parity promise is made by this scaffold.
