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
