# DreamWeave Thread overlap review

Historical public-release review. The later [Soot source review](SOOT-SOURCE-REVIEW.md)
adds implementation evidence and supersedes the source-unavailable limitations
for that uploaded local-repair snapshot.

Reviewed public RynthSuite HEAD a32c80bed1dee2e319b776707ef4eb43605381c3 and
pinned RynthCore cf7e0f21cefa9733060bacc7a5d3b73055cd82c1. No game execution.

## RynthThreads: separate DreamWeave-only plugin

The launcher lists a Fragment Press recorder/component inference plugin. The
public update feed advertises version 2026.11.3.2, API 92. Its published managed
DLL was downloaded to /tmp only, not loaded or executed. SHA-256 matches its feed
entry: b1be028b52c57ac7910e7b68adb8e42aaa9ad1422e046d47429f54629e401355.
The feed's signature was not independently verified in this review.

Embedded UI/help strings and managed metadata describe:

- `/rthreads`, `/rthreads stats`, `/rthreads export`, `/rthreads help`.
- Component/modifier/press history tabs and CSV export beside presses.jsonl.
- Fragment level, result level, entries, components, modifier descriptions and
  extracted numeric values; complete/incomplete press classification.
- Per-component modifier rates versus presses without that component, with a
  95% uncertainty range; baseline no-component presses and exact-recipe views.
- Views by material and fragment level; records across characters on one PC.

These are shipped UI/data-schema descriptions, not a verified live parser test.
Public implementation source was not found in the supplied GitHub repositories.
Do not claim a validated interaction model, optimal recipe recommendation,
compatibility with the current imprint/examination messages, or dungeon completion
tracking from these strings alone. Test with real starting/imprint/result records
before replacing ACAI2's Thread Gem plans. Prefer integration/import to a second
press-history store if current-format capture proves adequate.

## RynthAi: confirmed public policy implementation

`Plugins/RynthCore.Plugin.RynthAi/ServerPolicy.cs` recognizes gem-entry messages
and `[REALM] ... eph=1`, tracks a run's dungeon landblock, forgets it on exit/logout,
and blocks generated dungeon patrol inside DreamWeave Threads. Recorded nav
routes and point-to-cell navigation remain separate; this is not a blanket ban
on all navigation. No adaptive Thread attack-height learner was found in this
review; ordinary power/height preferences are not evidence of one.

RynthDreamWeave is a separate server-information card: published metadata describes
rules, features, player commands, links and account setup, not a Thread solver.

Sources:
- https://github.com/tombohar/RynthCore/blob/cf7e0f21cefa9733060bacc7a5d3b73055cd82c1/src/RynthCore.App.Avalonia/RynthUpdater.Available.cs
- https://github.com/tombohar/RynthSuite/blob/a32c80bed1dee2e319b776707ef4eb43605381c3/Plugins/RynthCore.Plugin.RynthAi/ServerPolicy.cs
- https://aelrynth.com/downloads/rynth/update.json
