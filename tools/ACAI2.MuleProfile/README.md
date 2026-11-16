# Loot-only no-salvage profile builder

Uses the genuine RynthSuite LootSdk, reviewed at
`708a4dafeaa2ac0a4392237cd008db05142cd02c`, to transform a user-provided UTL.
Source profiles and generated loot files stay out of Git. No character settings,
installer, plugin, behavior or Bridge changes.

```sh
dotnet run --project tools/ACAI2.MuleProfile -p:LootSdkRoot=/path/to/RynthSuite/Shared/RynthCore.LootSdk -- input.utl /tmp/output
dotnet run --project tools/ACAI2.MuleProfile -p:LootSdkRoot=/path/to/RynthSuite/Shared/RynthCore.LootSdk -- --verify /tmp/output/LootProfiles/Keyfer-Mule-NoSalvage.utl
```

All Salvage actions are removed, including disabled rules. EVERY non-salvage
rule is compared in full against the working original: no additional name/type
filters, no changed conditions, flags, priority, action, count or ordering. The
UTL SalvageCombine Enabled flag is turned off; other combine metadata is preserved.

```sh
dotnet run --project tools/ACAI2.MuleProfile -p:LootSdkRoot=/path/to/RynthSuite/Shared/RynthCore.LootSdk -- --compare original.utl mule.utl
```

The publication workflow requires BOTH the exact original and the mule output and
runs this comparison with the genuine SDK before publishing. Never substitute an
older similarly named source file when the user's reported working rule is absent.
The v1 profile had extra Keep conditions and is superseded; do not use it as the
input for a correction. Always start from the actual working original.

This is a loot policy, not a global Rynth command blocker. It does not clear old
in-memory salvage queues or change the engine's separate combine toggle. No
character settings, Keyfer behavior, plugins, or Bridge files are changed.
