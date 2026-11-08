# Read-only Windows check

Use the new repository's Test/Publish scripts with .NET 10. Keep the old ACAI2
installation intact. Use a RynthCore **CoreCLR** engine at API 92+, matching the pin;
this is not a Decal DLL or a plugin for the legacy NativeAOT engine.

1. Add the published plugin DLL (with its sibling dependencies) to RynthCore's
   launcher. Do not copy files into `C:\ACAI2` or its Data/configuration folders.
2. Leave BridgeEnabled and CapturePublicChat off. Log in to one character and run
   `/acai2r status`. Confirm the actual server/character, read-only status and
   bounded object count. This command only writes local diagnostics.
3. Run `/acai2r export`. Inspect the local JSON: identity/instance, counters, native
   sample coordinates and appraisal-presence flags. Raw chat should be empty.
4. Log out/change character or server. Confirm a new instance and cleared counts.
   Do not treat unknown object kinds/coordinates as verified combat distances.
5. If desired, explicitly configure BridgeEnabled=true, reload the pilot and
   inspect the existing Bridge's registry. Expect an additional `rynth-` instance
   and only observation traffic, no Keyfer prompts or chat replies.
6. Optionally enable CapturePublicChat for comparison logs. General/Allegiance
   text may then appear in local exports; private tells must not. Remove exported
   files locally when no longer useful; none are uploaded by the pilot.

Record actual loader version, manifest/load errors, lifecycle timing and any
observed mismatch. Compile/controlled tests alone do not establish native runtime
correctness. The pilot makes no attacks, casts, item-use/identify requests, trade,
movement, emotes, arena announcements or prize transfers.
