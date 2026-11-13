# Soot collaboration: standalone plugin boundary review

Reviewed 2026-11-13 at the user's request. Goal: collaborate on useful DreamWeave
features as an optional plugin on official Rynth, while keeping ACAI2 Keyfer separate
and combat out of its current release. This is a source/architecture review, not an
installation, code port, approval of unattended automation, or message to Soot.

## Evidence and scope

The GitHub URL https://github.com/SootAC/rynth-dreamweave returned HTTP 404 with the
current credentials. The review therefore uses the supplied
`rynth-dreamweave-main (1).zip`, not an assumed live repository revision.

- Uploaded archive SHA-256:
  `c2ab03bd193924971ab450fc00690df521f2766a51b65a4c004a1bdbd3507580`.
- README identifies current patch base **2026.11.12.1 / API 97**, updated November 13.
- Independently counted **139 Suite patch paths** (+6,423/-403 lines), including
  tests, and **37 Core patch paths** (+1,209/-108 lines), including tests/data.
  Of these, 63 paths affect RynthAi and 28 affect the engine. These are patch-path
  counts, not 176 independent features or a complete replacement codebase.
- Verified all 12 listed SHA-256 values in the supplied checksum file. This proves
  internal consistency only. No supplied scripts, binaries or metas were executed.
- Source includes current cumulative Suite/Core patches, a separate Threads parser
  patch and build/audit notes. It contains no ready standalone Soot plugin and no
  distributed runtime DLLs. A newer private encounter overlay is explicitly absent;
  its features cannot be assessed from this archive.
- The supplied build/test results are the author's reports, not tests rerun here.
  The exact imported baseline revisions are not assumed equivalent to public HEAD.
  Patches were not applied to either working Rynth checkout or any installed game.

This supersedes the old November 8 sharing snapshot's packaging description in
SOOT-SOURCE-REVIEW.md. Older detailed press-data findings are not automatically
resolved by this new archive: the separate Threads patch still adds only the exact
`Pressed. It holds.` start case, not raw-fragment examination/imprint/dose modeling.

## Feasibility decision

**Yes, a plugin-first collaboration is feasible. The current patch bundle cannot
become a standalone plugin merely by renaming/building another DLL.** It edits
private RynthAi manager/arbiter code, other plugins, engine rendering, chat dispatch
and geometry implementations. Those dependencies must be replaced by supported
interfaces, accepted upstream changes, or consciously deferred features.

Use an independently identified companion plugin with its own manifest/settings
and ordinary RynthCore lifecycle. Do not reuse the official RynthDreamWeave plugin
identity accidentally or overwrite RynthAi/Core binaries. Leave official updater
ownership intact. ACAI2.Rynth remains the separate Keyfer adapter.

## Feature-by-feature disposition

| Feature in the supplied patches | Suitable destination | Required boundary / limitation |
| --- | --- | --- |
| Thread press/run interpretation, journals, descriptive statistics and future recipe analysis | Standalone DreamWeave companion or an existing recorder plus a consumer | Pick one authoritative recorder; use a versioned snapshot/export for other consumers. The supplied Threads source is only a one-file patch, not a complete source package. Missing starting level/recipe evidence must remain unknown. |
| Survey checkpoints, boundary visualization, route authoring and saved encounter notes | Companion-owned UI/data; delegate route requests | RynthAi.Routes and RynthNav routes/path interfaces cover planning. They do not prove that all new controls can be injected into the engine's existing map panel. Own a window first, request a supported overlay extension if needed. |
| Server facts, custom command reference, bank-backed vendor/luminance values | Existing server-profile mechanism or companion with a deliberate identity | RynthServer.Profile already has vendorBank, vendorBankPyreals and bankLuminance keys. Avoid two active owners of the same server card or two /lph pollers. |
| Blood Mage attack/heal spell choices | Partly feasible through RynthAi.Combat | Existing attack/heal requests are leased; return a decision/decline and let RynthAi execute/preserve pacing. This is not a general pet/buff/recovery scheduler API. |
| Resource drains, dispel priority, summon order, Duo-gem selection and live-pet recovery | Supported policy/data hooks or upstream changes; defer unsupported parts | The current patch edits BuffManager, CombatManager, PetManager and the activity arbiter. Do not run a second native action loop alongside them. A cast hook alone does not reproduce all stop, death, mana, resist and combat-yield semantics. |
| Quick Give delayed-refusal/partial-stack correctness | Upstream fix to existing Quick Give | It fixes the existing Item Tool behavior; a second always-running hand-in controller is redundant. An alternative companion tool would need explicit exclusive ownership and manual invocation, not simultaneous retries. |
| Fixed-scenery/Sanamar collision and LOS corrections | Upstream RynthAi/RynthNav fix | Pathfinding and collision correctness affect the bot globally. Expose needed data/configuration through APIs; do not ship shadow copies of the whole navigation implementation. |
| Chat-history retention and ordering before RynthChat captures a line | RynthChat option and generic engine ordering/filter support upstream | Consuming a chat event in a later plugin does not remove what an earlier listener already persisted. Core patch specifically reorders incoming listeners. Independent plugin observation is not equivalent. |
| Pet damage support in existing RynthVision text | Upstream parser/feed fix or separate optional renderer | Current Core patch edits CombatText/HudFeed and passes chat origin to reject player quotes. A companion can parse messages, but cannot patch private renderer internals through a public name alone. Our Keyfer release remains without combat rendering. |
| Monitor presets / map editing inside existing Core panels / enchantment caster snapshots | Public host API/configuration extension or upstream implementation | Current code uses internal PanelLayout/PanelStateStore and EnchantmentHooks snapshots, including caster IDs. Do not assume those internals are exposed by API 97 just because the patch declares the same base version. |
| Exact automatic press start reply | Small upstream RynthThreads parser repair | Retain its anchored/quoted-message exclusion; no need to fork the engine for this change. |

Two notable examples of coupling in the actual patches:

- `RynthDreamWeave-Core-2026.11.12.1.patch` changes `PluginManager` listener order
  so filtering plugins run before RynthChat and its history; that needs a generic
  host contract/upstream decision rather than a plugin assuming load order.
- The Suite patch empties `ServerPolicy.PatrolOffInThreadsServers`, previously
  containing DreamWeave, and adds Thread-hunt behavior. Preserve/reconcile the
  server-owner policy explicitly; packaging it as a plugin is not permission to
  bypass that policy. Existing policy is not merely a missing navigation feature.

## Supported integration already present upstream

Reviewed the genuine RynthSuite snapshot `44571693d76c3b388e48b1a82f2a0518ecd3aac3`
and pinned RynthCore SDK, using these existing contracts:

- Host `GetPluginInterface` (API 68+) and `GetPluginExportJson` for optional
  cross-plugin boundaries. Interfaces can be absent during startup/reload.
- `RynthAi.Activity`: read-only activity bits; useful for showing status and
  coordinating tools without interfering with loot, casts or inventory actions.
- `RynthAi.Script`: expression/settings operations, bounded command submission,
  route movement helpers. Existence is not authorization to issue actions here.
- `RynthAi.Routes`, `RynthNav.Path` and `RynthNav.Routes`: route generation/path
  queries rather than shipping another complete movement engine.
- `RynthAi.Hub`: selected shared UI/quest/spell/loot/session facilities. It is a
  concrete contract, not access to every private manager or panel.
- `RynthAi.Combat`: attack/heal kinds only in the reviewed version. Claim/Poll/
  Decide/Release, finite lease/timeouts and fallback to native decisions already
  exist. A future approved policy plugin should use these rather than direct casts.
- `RynthServer.Profile`: existing server-owned facts and structured values.

Relevant source references:

- https://github.com/tombohar/RynthSuite/blob/44571693d76c3b388e48b1a82f2a0518ecd3aac3/Plugins/Shared/RynthAiCombatApi.cs
- https://github.com/tombohar/RynthSuite/blob/44571693d76c3b388e48b1a82f2a0518ecd3aac3/Plugins/Shared/RynthAiActivityApi.cs
- https://github.com/tombohar/RynthSuite/blob/44571693d76c3b388e48b1a82f2a0518ecd3aac3/Plugins/Shared/RynthAiRoutesApi.cs
- https://github.com/tombohar/RynthSuite/blob/44571693d76c3b388e48b1a82f2a0518ecd3aac3/Plugins/Shared/RynthNavPathApi.cs
- https://github.com/tombohar/RynthSuite/blob/44571693d76c3b388e48b1a82f2a0518ecd3aac3/Plugins/Shared/RynthServerProfileApi.cs

Check table version and size, call on the proper pump thread, resolve again after
reload, and never retain stale unmanaged pointers. Missing capabilities disable
only the affected feature. Pass primitive/UTF-8 DTO data through these contracts;
do not share C# objects or JsonTypeInfo across collectible contexts. The recently
fixed ACAI2 JSON/HTTP loading issue is evidence that these boundaries matter.

## Concrete collaboration sequence

1. Agree a supported feature inventory with Soot and identify each patch's owner:
   companion feature, generic upstream fix, existing-plugin option or missing API.
   The uploaded source is sufficient for this inventory; private repository write
   access is not yet verified, and no message/PR has been sent.
2. First standalone milestone: stock RynthCore/RynthAi plus an independently
   loadable DreamWeave companion, read-only Thread/survey capture and its own UI,
   per-character settings and versioned structured snapshots. Do not add combat
   rendering to the Keyfer package. Choose one press/run data owner.
3. Split the small generic fixes into reviewable upstream proposals. Keep Core
   loader/runtime behavior, LOS, Quick Give and chat history fixes out of a private
   mandatory replacement installer. Request missing hook contracts separately.
4. Consider opt-in policy integrations only after existing broker capabilities
   and server policy are confirmed. One action owner, existing pacing/stop/portal/
   death safeguards, cancellation, expiration and explicit outcomes are required.
5. Acceptance: installs as an additional plugin on unmodified official API97,
   survives update/reload/logout, removes cleanly without changing Core/Suite DLLs,
   produces no duplicate casts/pollers/loot/payment owners, and leaves Keyfer's
   working Bridge/session untouched. Host-dependent features must be unavailable
   clearly when their required upstream hook is absent.

No supplied author tests were rerun and no native gameplay claims are made. This
review does not ship or enable the proposed companion or any automation changes.
