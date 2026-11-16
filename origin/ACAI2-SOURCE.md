# Existing ACAI2 source provenance

Source repository: ericallen9198-eng/Samm-AI (private).
Snapshot: fa363107e3ec3e00d12589b9de8ae9c035542f10 (v0.9.29 / Bridge v0.16).

`src/ACAI2.Rynth.Core/BridgeContracts.cs` is a snapshot of
`ACAI2.Protocol/Contracts.cs` from that commit. Only nullability annotations/context
may differ. The wire names and protocol version remain compatible with the existing
Bridge. It is a snapshot, not a dependency on a sibling checkout or old binaries.

No player Data directories, credentials, saved histories, runtime binaries or
machine configuration were imported. The old repository is not modified by this
new project. The existing Bridge remains separately installed and managed.

The v0.2.0 combat port also carries `CombatParsing.cs` and the minimal `CombatComponents.IsDirect` helper from
that snapshot. The parsing guard is localized to combat messages rather than
importing Keyfer's GameChat routing; chat envelopes are rejected and the renderer
retains aggregate values rather than raw logs. No Decal renderer or native power
binding is copied.

## Keyfer port, v0.3.0

`src/ACAI2.Rynth.Core/Keyfer` ports the original project's KeyferConversation,
TreasureClient, LeaderboardClient and their portable protocol helpers from
Samm-AI `ff44cc3`. The new adapter replaces System.Web JSON with System.Text.Json,
uses session cancellation, bounded queues, isolated LOCALAPPDATA state, injected
HTTP/clock tests and the native Rynth pump. It preserves the existing Bridge wire
contracts and does not import the old installed state/config or Bridge source.
The original project is MIT licensed; its existing bundled notice is retained.

## Bridge v0.18 for own-loot awareness

`src/ACAI2.Rynth.Bridge` ports the existing Bridge v0.17 source and its two public
game-reference resources from the reference-only Samm-AI checkout at
`fec7a2827142576d512c6549f45e8c7925c7cdf5`. The old checkout was not modified.
The new .NET 10 executable references this repository's portable protocol/Core
types; it does not reference old binaries or ship a Decal component. It retains
existing configuration/environment and data paths. Own loot is a bounded optional
typed ask field consumed as observation data, not fabricated public chat or objects.
No player data, histories, keys, runtime files or machine configuration were copied.
