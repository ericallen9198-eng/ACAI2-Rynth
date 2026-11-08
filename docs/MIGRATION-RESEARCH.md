> Follow-up during scaffold implementation: the pinned RynthCore also supports
> managed plugins under its CoreCLR engine (API 88+, this pilot requires 92).
> `src/RynthCore.Engine/Plugins/ManagedPlugins.cs` and
> `scripts/Publish-PluginsCoreClr.ps1` establish that route. The initial review's
> NativeAOT-only build statements below are superseded for this pilot. See README.

# ACAI2 migration research: RynthCore / RynthSuite

Research only. No runtime, project dependency, installed plugin, configuration or application source was changed. No game/AI/Discord messages were sent. Repositories were inspected from source; neither framework was built or run in the AC client during this review.

## Conclusion

A migration away from Decal, VTank and UtilityBelt is feasible in principle. Build a separate ACAI2 RynthCore plugin, keep the existing Bridge and portable analysis code, and integrate optionally with RynthAi for automation settings. Do not port everything into RynthAi's large bot implementation or rewrite the Bridge into a NativeAOT plugin.

RynthCore is the new host: injection/loader, client hooks, plugin lifecycle, rendering and SDK. RynthAi is RynthSuite's combat/navigation/loot automation plugin. They occupy different layers. Our Keyfer conversation service and arena remain ACAI2 features; adopting a plugin named RynthAi does not replace those services automatically.

Source snapshots reviewed:

- RynthSuite: `a32c80bed1dee2e319b776707ef4eb43605381c3`, snapshot for release `2026.11.3.2`, committed November 5, 2026.
- RynthCore: `cf7e0f21cefa9733060bacc7a5d3b73055cd82c1`, same release snapshot/date.
- GitHub mirrors are accessible and contain the SDK/engine implementation, even though their READMEs point to a separate canonical host.

## Architecture to aim for

```text
AC client + RynthCore
  |
  +-- ACAI2.Rynth: chat, observations, HUDs, game integration
  |      +-- shared ACAI2 parsing/metrics/Thread Gem logic
  |      +-- existing local HTTP contract -> ACAI2.Bridge
  |                                        Keyfer, arena, wiki, memory, SQLite
  |                                        existing Discord client
  |
  +-- RynthAi: optional bot control/settings through versioned interfaces
  +-- other RynthSuite plugins as desired
```

The final ACAI2 client should depend on RynthCore, with RynthAi optional where automation is used. A Keyfer-only or observation-only client should not need the combat bot running. No Decal DLL is loaded by our new adapter. Preserve `C:\ACAI2\Data` and configuration and the existing server/character/session identity boundaries.

## What our code actually depends on today

- `ACAI2.Decal.csproj` targets .NET Framework 4.7.2/x86 and references `Decal.Adapter`, `System.Web.Extensions`, `System.Drawing` and our Protocol project.
- Chat interception, world objects, identification, enchantments, rendering and lifecycle use Decal.
- Height control in `CombatTracker.RequestPhysicalHeight` calls `Decal.dll` to dispatch `/vt opt set DefaultMeleeAttackHeight`. This is an actual VTank dependency for that mode.
- The power-restoration code already has a direct-client implementation derived from MIT-licensed UtilityBelt.Service; it does not require UtilityBelt to control the slider. Existing source attribution still matters if that binding is retained.
- The Protocol project targets netstandard2.0 and has no Decal references. Most combat parsing, metrics, settings models and Thread Gem analysis can be reused, subject to NativeAOT compatibility checks.
- The .NET 8 Bridge is a separate process. Its arena, AI, memory, wiki, storage and Discord-facing behavior can remain there.

## Feature mapping

| ACAI2 feature | Rynth replacement evidenced in source | Port work / limits |
|---|---|---|
| Keyfer chat, arena, announcements, tells | `RynthPluginBase.OnChatWindowText`, `OnChatBarEnter`; `Host.InvokeChatParser` | New client adapter and UTF-16/chat-type normalization. Keep filtering, channel restrictions, game completion receipts and speech pacing. No duplicate active responder during cutover. |
| Character/server identity and world objects | Host world-name, player/object APIs, create/delete/update callbacks and property readers | Preserve separate server/character/instance identities; compare lifecycle timing and object GUID conventions. |
| Combat/DPS parsing and exports | Chat callbacks; `OnCombatDamage`; inbound server-message subscription | Reuse our richer chat parser for poison/acid/sneak/periodic damage. A generic combat callback alone is not proof it includes every DreamWeave damage source or correct actor attribution. |
| Compact HUD, setup, transparency/fonts | ImGui overlay and existing RynthSuite UI examples | Rewrite rendering/input; retain our settings and data models. Native GDI/Decal widgets do not carry across as-is. |
| Toon-following text and above-mob icons | `WorldToScreen`, viewport size, object position; in-world Nav3D facilities | Screen-project entity/head positions into a transparent ImGui overlay. Validate axis order, landblock coordinates, camera, DPI, visibility and object disappearance. Retain a screen-locked fallback. |
| Self debuffs | `ReadPlayerEnchantments`, enchantment add/remove callbacks, server time | Reuse classification/layout. Verify IDs, expiry units and metadata/layer fidelity rather than assume identical Decal observations. |
| Target debuffs | `ReadObjectEnchantments` implementation + our observed-success estimator | Client registry contents may be incomplete/absent. Unknown must not mean zero effects. Do not advertise a complete server-side enemy buff list. Source includes inventory-registry crash cautions. |
| Automatic melee/missile power and height | RynthAi persisted settings and `RynthAi.Script.SetSetting`; native attack APIs | Translate differing height conventions. Coordinate with the bot through one authority. See manual slider caveat below. |
| Thread Gem recipes, modifiers, prediction | Chat, object string/int properties, inventory/appraisal APIs and inbound server messages | Shared model/store can stay. Exact raw-fragment -> press correlation needs a replacement for our outgoing use-with packet observation. |
| Quest capture, leaderboards, events, treasure | Chat/server events, inventory/action APIs | Reuse parsers/storage and fixed command allowlists. Validate action acknowledgments and avoid duplicate prize dispatches. |

## Strong findings

1. **World-to-screen is a real SDK API**, used by RynthVision and RynthAi overlays, not merely a TODO in a README. This is directly relevant to our earlier toon/mob attachment difficulties. It gives the projection primitive, not a ready-made ACAI2 floating text implementation.
2. **Native object/player enchantment readers have implementations.** RynthCore uses main-thread reads and requested snapshots for off-thread access. It still cannot show information the client has not received; there are failure/empty/stale distinctions to handle.
3. **RynthAi already persists separate melee/missile power and height.** Its shared `RynthAi.Script` interface can read/write named settings, and its implementation saves changed settings. This is preferable to dispatching VTank options.
4. **The code knows about DreamWeave.** `ServerPolicy.cs` tracks Thread entries/realm observations and preserves the requested restriction on automatic dungeon patrol in DreamWeave Threads. Recorded routes and other navigation are not all banned by that check. Retain the restriction. This is not proof it already implements our component-effect learning model.
5. **Other companion plugins exist:** RynthTracker (session stats), RynthChat, RynthDefective, RynthVision, RynthOracle, RynthNav, RynthLua, RynthNet, RynthInventory and RynthSort. They provide examples/possible integrations, not automatic replacements for our custom features. RynthOracle is character/quest tooling, not our conversational LLM.
6. **Both root projects are MIT licensed.** New adapters can use their public APIs, and source reuse is permitted under the notices. Preserve copyright/license notices for copied code and relevant third-party notices (including UtilityBelt-derived code). VTank-format profiles/namespaces and compatibility commands do not by themselves imply a VTank runtime dependency.

## Gaps to resolve before claiming independence

### Manual power slider restoration

`Host.MeleeAttack`, `MissileAttack` and `NativeAttack` take a power/accuracy value and issue an attack. They are not a safe substitute for restoring a manually controlled slider after login. I did not find a dedicated read/write-only requested-power slider API in the public host surface reviewed. RynthAi's automated power settings and its bar-suppression API are different functions.

Options: retain/retarget our existing guarded direct-client binding, or add a small upstream SDK/engine API for read/write-only slider state. Test that restoration never attacks, respects melee/missile transitions and survives relaunch. Do not claim the existing automated settings already solve that exact user requirement.

### Exact outgoing action correlation

Our current fragment observer and target attribution use Decal's outgoing action packets. RynthCore exposes inbound reassembled server-message subscriptions, lifecycle/UI hooks and action-result counters. I did not find a public equivalent of a general outgoing use-with/cast packet stream. `UseDone` is not proof of the source and destination object IDs.

Investigate an upstream native action-observation hook carrying source/target IDs, or a versioned RynthAi interface for actions it owns. Do not silently replace exact association with current selection, especially for same-named mobs or several fragments. Retain explicit uncertainty/ambiguity rejection until that is solved.

### NativeAOT and UI rewrite

The new plugin targets `net10.0-windows`, `win-x86`, NativeAOT, with SDK/PluginCore project references and unmanaged lifecycle exports. Source build instructions require .NET 10 and the Windows native toolchain; `dotnet publish` produces the injectable DLL. Our current .NET Framework/Decal DLL cannot be dropped into that loader.

Replace the in-process `JavaScriptSerializer`/System.Web path with AOT-compatible serializers, remove reflection-based Decal spell-table discovery, and supply typed spell metadata through the new host/catalog. Keep SQLite and the existing AI service in the Bridge to minimize in-process dependency and trimming work.

Rynth documentation contains stale build/deploy advice: BUILD.md describes automatic copy into the engine's plugin directory, while the current plugin csproj explicitly removed that copy. Pin a matching Core/Suite snapshot and verify actual output/launcher plugin registration.

### Coexistence and validation

RynthCore documents coexistence with Decal/UB/VTank, but that mode skips the D3D9/ImGui route and uses different ticking. It is useful for a read-only observation pilot, not proof that the final ImGui HUD works. Final rendering validation needs a dedicated RynthCore-only client. RynthAi also guards against two bots running simultaneously.

Keep game calls on the appropriate plugin pump/native thread; queue work from callbacks and use immutable render snapshots. Do not block hooks/ticks on Bridge/AI HTTP. Respect API version/capability checks and reacquire optional plugin interfaces across hot reloads.

## Recommended implementation sequence

1. **Small read-only pilot:** new `ACAI2.Rynth` plugin loads, records identity/chat/object/appraisal observations and talks to the current Bridge, with outgoing game commands disabled. Compare supplied combat/Thread Gem logs and state transitions with the Decal adapter. No production cutover yet.
2. **Keyfer first:** move conversation routing, arena, tells, paced fixed commands and completion receipts. Activate exactly one responder. Confirm General/Allegiance restrictions, private histories, timeouts, restart behavior and no duplicate prizes or announcements.
3. **Combat and Thread Gem observations:** reuse portable models, implement the new observation adapters, resolve exact-use/cast correlation, migrate settings/storage without overwriting history. Both unknown and unavailable states must remain explicit.
4. **HUD and projection:** implement compact ImGui panels, font/outline/color/alpha controls, locks/saved layouts, self/target debuffs and character/mob projection. Validate visibility while moving/turning/zoning and after object deletion.
5. **Automation ownership:** replace VTank height control with RynthAi settings; validate separate power defaults and manual slider restoration; then test a client with Decal, VTank and UtilityBelt absent. Keep the old installation as rollback until parity is demonstrated.

The first useful deliverable is the read-only pilot plus a Keyfer-only migration, not a full bot rewrite. A precise total migration estimate would require that pilot and the Windows/API-gap results.

## Primary source links

Pinned links avoid relying on a moving README:

- [RynthSuite README](https://github.com/tombohar/RynthSuite/blob/a32c80bed1dee2e319b776707ef4eb43605381c3/README.md)
- [RynthCore README](https://github.com/tombohar/RynthCore/blob/cf7e0f21cefa9733060bacc7a5d3b73055cd82c1/README.md)
- [Core SDK host APIs](https://github.com/tombohar/RynthCore/blob/cf7e0f21cefa9733060bacc7a5d3b73055cd82c1/src/RynthCore.PluginSdk/RynthCoreHost.cs)
- [Core plugin callbacks](https://github.com/tombohar/RynthCore/blob/cf7e0f21cefa9733060bacc7a5d3b73055cd82c1/src/RynthCore.PluginCore/RynthPluginBase.cs)
- [Enchantment reader implementation](https://github.com/tombohar/RynthCore/blob/cf7e0f21cefa9733060bacc7a5d3b73055cd82c1/src/RynthCore.Engine/Compatibility/EnchantmentHooks.cs)
- [RynthAi plugin target/dependencies](https://github.com/tombohar/RynthSuite/blob/a32c80bed1dee2e319b776707ef4eb43605381c3/Plugins/RynthCore.Plugin.RynthAi/RynthCore.Plugin.RynthAi.csproj)
- [RynthAi shared setting/control interface](https://github.com/tombohar/RynthSuite/blob/a32c80bed1dee2e319b776707ef4eb43605381c3/Plugins/Shared/RynthAiScriptApi.cs)
- [Interface implementation and persistence](https://github.com/tombohar/RynthSuite/blob/a32c80bed1dee2e319b776707ef4eb43605381c3/Plugins/RynthCore.Plugin.RynthAi/ScriptInterface.cs)
- [DreamWeave Thread policy](https://github.com/tombohar/RynthSuite/blob/a32c80bed1dee2e319b776707ef4eb43605381c3/Plugins/RynthCore.Plugin.RynthAi/ServerPolicy.cs)
- [Core MIT license](https://github.com/tombohar/RynthCore/blob/cf7e0f21cefa9733060bacc7a5d3b73055cd82c1/LICENSE), [Suite MIT license](https://github.com/tombohar/RynthSuite/blob/a32c80bed1dee2e319b776707ef4eb43605381c3/LICENSE), [Suite third-party notices](https://github.com/tombohar/RynthSuite/blob/a32c80bed1dee2e319b776707ef4eb43605381c3/THIRD-PARTY-NOTICES.md)
