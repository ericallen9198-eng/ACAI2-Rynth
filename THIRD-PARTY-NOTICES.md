# Third-party dependencies

RynthCore is a pinned Git submodule at `external/RynthCore`, commit
cf7e0f21cefa9733060bacc7a5d3b73055cd82c1, from
https://github.com/tombohar/RynthCore.

It is MIT licensed, copyright (c) 2026 Tom Bohar. Its full license is retained in
`external/RynthCore/LICENSE`. Retain that license and applicable upstream notices
when redistributing its SDK/PluginCore assemblies. This project references public
SDK and PluginCore projects; it does not fork or modify the engine.

No Decal, VTank or UtilityBelt binaries are dependencies of this project.
RynthSuite was reviewed as architectural reference; no RynthSuite implementation
file was copied into this scaffold. The lifecycle export glue follows RynthCore's
public ABI. No license for this application's own code is assigned by this notice.

ImGui.NET 1.91.6.1 (Eric Mellino, MIT) is referenced to match the pinned engine's
binding version: https://github.com/ImGuiNET/ImGui.NET . Managed plugins resolve
`cimgui` to the engine-owned native module; this plugin does not initialize a
second renderer/context. See the retained license in
`THIRD-PARTY-NOTICES/ImGui.NET-LICENSE.txt`.

The standalone Thread journal adapts this project's original ACAI2 Thread capture,
reporting and empirical prediction code from the user's Samm-AI source. The native
prep controller is new code informed by the user-supplied SootAC DreamWeave
ThreadPrep workflow. No supplied meta, Lua script, patched engine or private
archive is redistributed. See docs/THREADS-SETUP.md for the deliberately narrower
attended workflow and retained-result policy.

The standalone Combat plugin independently adapts this project's original ACAI2
combat parser (fa363107 provenance retained in the source) and adds new aggregate,
run/death, persistence and ImGui rendering code. No RynthVision implementation,
engine binary, RynthAi automation or third-party combat assets are copied into its
package. It references the same genuine SDK and matching ImGui bindings above.

Combat v0.1.2 embeds a compact known-ID/hostile-effect reference derived from the
spell ID/name facts in MIT-licensed RynthSuite Combat/SpellData.txt at 44571693.
See origin/COMBAT-SPELL-METADATA.md and the retained RynthSuite license. This is
reference metadata, not a RynthAi runtime dependency or copied bot implementation.

Combat's optional read-only requested-power sampler uses the published retail
x86 ClientCombatSystem layout previously validated by ACAI2 and documented by
UtilityBelt.Service (MIT, revision a3ef224a2c08f0e086cad62de2717030b42279a8).
Its constants also agree with the pinned RynthCore ClientCombatHooks getter.
See THIRD-PARTY-NOTICES/UtilityBelt-Service.txt. No UtilityBelt binary is required;
this sampler validates the getter/call sites and reads process memory only. It
never invokes combat methods or changes the power setting. Unsupported clients
show power unavailable rather than guessing a percentage.
