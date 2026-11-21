# Marae Lassel NPC fix — RynthAi 2026.11.19.1 (32-bit)

## Install — no commands or building needed

1. Close all Asheron's Call clients.
2. Extract this **November 19** ZIP into a normal folder in Downloads. Keep its files together.
3. Double-click **INSTALL-NPC-FIX.cmd**. It runs the bundled PowerShell installer with this path already filled in:
   `C:\Games\RynthSuite\RynthAi\RynthCore.Plugin.RynthAi.dll`
4. Read the result. On success it prints **Installed NPC reply patch** and the original DLL backup path. The window stays open until you press a key. Do not use the earlier November 17 package for this DLL.

The installer checks the new DLL checksum and the exact reviewed original SHA256. It verifies a timestamped backup BEFORE replacing the active DLL and restores it if copying/verification fails. Different/newer DLLs are refused; do not bypass that check. Re-running the same successful installation reports already installed. Original backups and restore information go under `%LOCALAPPDATA%\ACAI2\Backups\RynthAi-NPC\`.

This installer replaces only RynthAi's DLL at the path above. It does not replace the engine or other plugins, alter saved character settings, install the beta, or start the game/macro. A custom location can be passed with `-PluginPath` to the PowerShell script if needed. The CMD launcher requires no typed path and keeps both success and errors visible.

## Beta files and in-game test

If the beta is already installed, keep those files. Otherwise copy these folders from `Beta` (default installation):

- `Beta\MetaFiles\*` → `C:\Games\RynthSuite\RynthAi\MetaFiles\`
- `Beta\NavProfiles\*` → `C:\Games\RynthSuite\RynthAi\NavProfiles\`
- `Beta\Scripts\MaraeLassel` → `C:\Games\RynthSuite\RynthLua\Scripts\MaraeLassel`

The Lua panel belongs under **RynthLua**, not RynthAi. Keep `index.lua` inside the `MaraeLassel` folder. Back up any older beta files before replacing them.

Start AC. Check RynthAi/RynthLua loaded, then type `/lua start MaraeLassel`. `/lua list` lists available scripts. Select just one area in KT mode for the first test. A marked hunter waypoint should show `waiting for '<hunter>' reply`, stay there while the hunter is silent, retry after eight seconds, and advance after that hunter replies. Ordinary portal behavior remains unchanged. Review Soot's beta README: a second death in an area or certain travel/turn-in failures intentionally holds the run.

The cloud checks do not prove live Lua HUD loading, native NPC use, or server replies. Those final checks must be performed in-game on the player's PC.

## Compatibility and source

Reviewed installed DLL: **2026.11.19.1**, managed **win-x86**, 32-bit-required flag, **Core API 97+**, informational version `2026.11.19.1+48a8cc7e78eb25b69ab74d771ab5996cafe320b3`.

Reviewed SHA256: `97a917edf02e58c6938cdb866ca21cf1d1fd996eb103dc593671a2e8802b717c`.

Pinned RynthSuite: `1e7c9924c816d8b2c29f27eda95b8ab7e5239870`.
Pinned RynthCore SDK: `fc246c0a4277fbc68207302e6d7ce22a16715fa6`.

Only `#rynth-talk:<zero-based index>` marked type-7 NPC waypoints get the new behavior. The pump approaches the recorded coordinate, uses an exact-name nearby NPC, holds until that NPC's tell/speech reply, and retries (eight seconds after a sent use; two seconds if unavailable/rejected). The callback only records replies; it never issues native actions or advances. Wrong-name/quoted/channel replies cannot advance. Route swaps, resets, advancement and a stopped macro clear the gate; temporary buff/combat pauses preserve it. Saving and embedded-route conversion retain markers. Unmarked NPCs and portals retain existing behavior. No HP/buff gate was added. The included Soot beta is unchanged.

The source patch builds on Windows with .NET 10 and the genuine SDK; it preserves the 2026.11.19.1 dependency versions and ImGui.NET 1.91.6.1. It is not a downgrade to November 17. No Keyfer, Bridge, Threads, Combat or engine replacement is included. Linux compilation alone cannot embed the Windows manifest or establish game loading.

To rebuild, check out the pinned repositories as siblings, apply `npc-reply.patch` at RynthSuite root and copy `NpcReplyGate.cs` into `Plugins/RynthCore.Plugin.RynthAi`. On Windows:

```powershell
dotnet build Plugins/RynthCore.Plugin.RynthAi/RynthCore.Plugin.RynthAi.csproj -c Release -p:PublishAot=false -p:NativeLib= -p:SelfContained=false -p:RuntimeIdentifier=win-x86 -p:PlatformTarget=x86 -p:Version=2026.11.19.1 -p:InformationalVersion=2026.11.19.1+acai2-npc-reply.1 -p:RynthPluginSharedSdk=true
```

## Rollback

Close AC. Copy the original DLL from the printed backup folder back to `InstalledPath` in that folder's `restore-info.json`, replacing the patched DLL. Restart AC. Separately installed beta files are unaffected.
