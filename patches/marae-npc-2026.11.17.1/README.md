# Marae Lassel NPC reply patch — matching RynthAi 2026.11.17.1

This optional RynthAi patch supports Soot's `#rynth-talk:<zero-based waypoint index>` extension. It is based on the public RynthSuite **5e70799b1c83d3cc1f2b06e06a274dd3f7449c82** and RynthCore **3f0c07647c958439fdec97afc4a0f43cf2b06030** November 17 snapshots. It is a managed **win-x64** build for **RynthCore API 97+**, matching the uploaded DLL and its embedded manifest, not the x86 NativeAOT build described by the older public BUILD.md.

Reviewed original: version 2026.11.17.1, informational version `2026.11.17.1+d924ac04b877be0ecf0a464ed904312919970560`, SHA256 `84eb07ef0d31e026351af092d227cf136cdf6557fb23a8df8dfb1ac60b318102`. Its decompiled NavigationEngine exactly matches an unmodified rebuild of the pinned public source. The installed binary parses type-7 points but lacks their runtime dispatch and ignores these trailing markers.

Only marked NPC waypoints change: approach the recorded coordinate, use an exact-name nearby NPC, hold that waypoint until that NPC's tell/speech reply, retry sent uses every eight seconds and unavailable/rejected uses every two seconds. Replies are recorded by the chat callback; native uses and advancement remain on the navigation pump. Saving/embedded-route conversion retains valid markers. Wrong-name/quoted/channel replies cannot advance. A route swap, route reset, next waypoint or stopped macro clears the gate. A temporary combat/buff pause preserves it. Ordinary unmarked NPC and portal behavior are unchanged. No HP threshold or buff requirement was added.

The beta files in `metas/marae-lassel-beta` are Soot's uploaded beta, unchanged. This patch is not a general rewrite of Rynth or any ACAI2 module. No Bridge/Keyfer/Threads/Combat update is involved.

## Installation

1. Extract the download into a normal folder. Close all AC clients.
2. In RynthCore's Plugins tab, note the exact active RynthAi DLL path.
3. Right-click `Install-NpcPatch.ps1` and choose **Run with PowerShell**. Paste that full DLL path, or press Enter if the displayed default is correct. The installer requires the exact reviewed original SHA, checks the new DLL, and verifies a timestamped backup before replacing the original. The backup and restore path are printed and recorded under `%LOCALAPPDATA%\ACAI2\Backups\RynthAi-NPC\`.
4. If the beta is not installed, copy its files to the correct folders (default installation shown; use your configured suite folder if different):
   - `Beta\MetaFiles\*` → `C:\Games\RynthSuite\RynthAi\MetaFiles\`
   - `Beta\NavProfiles\*` → `C:\Games\RynthSuite\RynthAi\NavProfiles\`
   - `Beta\Scripts\MaraeLassel` → `C:\Games\RynthSuite\RynthLua\Scripts\MaraeLassel`
   The Lua panel belongs under **RynthLua**, not RynthAi. Keep its `index.lua` inside `MaraeLassel`. The DLL installer does not change these files or saved settings. Back up existing files if installing the beta over an older copy.
5. Start AC and confirm RynthAi and RynthLua load without plugin errors. Run `/lua start MaraeLassel` (or start it in the Lua scripts manager). Its HUD should show area/mode controls. `/lua list` shows the scripts that RynthLua can find.
6. For an initial test, select just one area and KT mode, start at its supported town/camp, and watch one marked hunter step. The nav status should say `waiting for '<hunter>' reply`; the index must remain there if no reply arrives, and a matching reply should advance it. A silent NPC should retry after eight seconds. Another NPC or a player quoting the reply must not advance it.
7. Test an ordinary existing portal route separately before leaving the beta unattended. Review Soot's README: the beta intentionally holds after a second death in an area and on travel/turn-in failures.

Cloud tests cannot prove live RynthLua HUD loading, native NPC use, or server replies. Those checks remain on the player's PC. Missing RynthLua/HUD APIs require a compatible Lua/Core install; this patch does not replace them.

## Rollback

Close all clients. Copy the backed-up DLL from the printed backup directory to the `InstalledPath` in its `restore-info.json`, replacing the patch. Start AC again. This does not revert separately copied beta files.

## Reproducible source build

Check out the pinned RynthSuite and RynthCore snapshots into sibling directories. Apply `npc-reply.patch` at the RynthSuite root; copy `NpcReplyGate.cs` into `Plugins/RynthCore.Plugin.RynthAi`. Build on Windows with .NET 10:

```powershell
dotnet build Plugins/RynthCore.Plugin.RynthAi/RynthCore.Plugin.RynthAi.csproj -c Release -p:PublishAot=false -p:NativeLib= -p:SelfContained=false -p:RuntimeIdentifier=win-x64 -p:PlatformTarget=x64 -p:Version=2026.11.17.1 -p:InformationalVersion=2026.11.17.1+acai2-npc-reply.1 -p:RynthPluginSharedSdk=true
```

Windows embeds the required plugin manifest. A Linux build verifies compilation only and is not the deployable package. Use the original installed dependencies; the patch preserves the matching 2026.11.17.1 assembly versions and 1.91.6.1 ImGui binding.
