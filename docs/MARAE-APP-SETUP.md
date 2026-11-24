# Marae Lassel - Quick Start
Version 1.16.1

DreamWeave tasks and treasure maps for the 180 Lowlands, 225 High Country and 275 Plateau.

## Before you start

- Have an updated RynthCore and RynthAi, with a working combat and loot profile. Enable RynthNav only if you want mesh map travel.
- Your /ls command must return you to the 275 Plateau camp. Test that before starting.

## Install and start

1. **Extract.** Close the game. Right-click the downloaded ZIP, choose Extract All, then open the extracted folder.
2. **Install.** Double-click INSTALL-MARAE.cmd. It finds RynthAi automatically. If a folder picker opens, choose your RynthAi folder containing MetaFiles or NavProfiles. Your RynthSuite folder also works; any drive is fine.
3. **Add the plugin.** Open RynthCore > Plugins > Add Plugin. Paste with Ctrl+V in the file-name box: the installer copied the full DLL path. Select it and enable ACAI2.Marae. Keep RynthAi enabled.
4. **Start.** Log into DreamWeave and type /acai2marae. Choose your areas and options below, then click Start / Resume. The app loads its mission and routes for you.

## Pick your options

| Option | What it does |
| --- | --- |
| Area | Tick the areas you want to visit. |
| Use maps | Also work treasure maps while hunting. Keep Treasure Map and wanted corpse loot enabled in your loot profile. |
| Hunt only | ON: skip NPC tasks and repeat the hunting route. OFF: talk to hunters, do tasks and turn them in. |
| 2-hour task limit | A maximum per camp. Finish early and move on, or turn in completed work when the limit is reached. After all camps, hunt in 180. |
| Mesh map travel | Optional; needs RynthNav. Default close/far steps are 30/130 yards. The sliders tune the next mesh step. |

## Quick help

- Suggested tasks + maps: tick your areas and Use maps; leave Hunt only OFF. Enable the 2-hour limit if you want a bounded daily circuit.
- Start / Resume keeps progress. New run starts the area's task checks over. Stop saves progress and stops automatic login resume.
- Panel closed? Type /acai2marae. Lost the plugin path? Open %LOCALAPPDATA%\ACAI2\Marae and read PLUGIN-PATH.txt.
- Wrong data location? In the app, open Advanced and set the folder containing MetaFiles / NavProfiles. You can also rerun the installer.
- Watch the first NPC and map/cache before leaving it unattended. If a pickup needs review, check your pack/cache before clicking the review button.

## Credit

Soot (xSoot / SootAC): nav paths and the hard work on the original mission logic, including his custom RynthAi DLL implementation. ACAI2 adaptation: standalone app, stock-RynthAi compatibility and packaging.

No earlier treasure-hunting installation, Lua setup, nav copying or RynthAi DLL replacement is needed.

Tested with unmodified RynthSuite 2026.11.23.3 and genuine Rynth SDK APIs. Live server behavior still needs a first-run check.
