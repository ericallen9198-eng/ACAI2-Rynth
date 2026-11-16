# Loot-only no-salvage profile builder

Uses the genuine RynthSuite LootSdk, reviewed at
`708a4dafeaa2ac0a4392237cd008db05142cd02c`, to transform a user-provided UTL.
Source profiles and generated loot files stay out of Git. No character settings,
installer, plugin, behavior or Bridge changes.

```sh
dotnet run --project tools/ACAI2.MuleProfile -p:LootSdkRoot=/path/to/RynthSuite/Shared/RynthCore.LootSdk -- input.utl /tmp/output
dotnet run --project tools/ACAI2.MuleProfile -p:LootSdkRoot=/path/to/RynthSuite/Shared/RynthCore.LootSdk -- --verify /tmp/output/LootProfiles/Keyfer-Mule-NoSalvage.utl
```

All Salvage actions are removed, including disabled rules. Existing Keep/KeepUpTo
rules, enabled state, conditions, counts and ordering are retained. Adds an AND
name guard excluding Salvaged/Salvage bags and ItemType != TinkeringMaterial
(ACE 0x40000000). The UTL SalvageCombine block is disabled. Original sources with
other actions fail for review instead of silently changing them.

The file is a loot policy, not a global Rynth salvage command blocker. It does not
clear old in-memory salvage queues or change the engine's separate combine toggle.
Load the new loot file before starting the chest puller. If an old salvage operation
is already queued, stop the puller and restart the game before using the new file.

The manual workflow verifies the exact decoded UTL with the reviewed real SDK
before publishing a normal `.utl` release asset and SHA-256. Its dispatch input is
compressed profile data only; no uploaded scripts or settings are executed.
