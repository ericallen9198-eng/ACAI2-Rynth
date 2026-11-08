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
