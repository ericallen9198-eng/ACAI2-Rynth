# ACAI2 Threads — standalone attended-test build

This is an independent managed x86 Rynth plugin, not a replacement for RynthCore,
RynthAi, Soot's installation, or Keyfer. Requires CoreCLR RynthCore API 92+ and
matching engine ImGui support. Native gem preparation also requires stock
RynthAi.Script v1 and RynthAi.Activity v1 interfaces. Missing interfaces disable
preparation rather than guessing. No Bridge or AI subscription is needed.

## Install

1. Close the game. Extract the complete Windows package into its own folder,
   for example `C:\Games\ACAI2-Threads`. Keep the DLLs together.
2. In RynthCore's Plugins panel, add/select `ACAI2.Rynth.Threads.Plugin.dll`
   from that folder and enable it. Do not select its Core/SDK dependency DLLs.
3. Launch DreamWeave and log in. Use `/acai2threads` to show the panel;
   `/acai2threads status`, `/acai2threads export`, and `/acai2threads stop`
   are also supported. Leave the existing Keyfer installation alone.

## First test

Start with tracking only: Current Run records native Thread opening, realm,
progress, clear and crumble messages. Opening and actually entering are separate.
If realm information is unavailable, manually mark entry/outside. Time counts
observed intervals; reconnect gaps are labeled. Linking a crafted gem to a run is
explicit, never an assumption that the latest press opened the dungeon.

For one attended gem-making test, stop RynthAi's macro and other inventory,
crafting and AutoCram activity. Stand still in peace mode immediately beside one
Fragment Press. Supply an untouched level-325 Raw Fragment and the exact selected
components. In Gem Maker select one new gem, leave buying OFF, review the recipe,
and click START BATCH. Do not use other inventory tools during the batch.

The builder identifies the fragment, applies each selected dose once, requires
native acknowledgment plus inventory consumption, presses once, and freshly
identifies the resulting gem. It does not navigate. Optional buying opens only
the named nearby fragment/reagent vendors and buys shortages within the selected
purchase budget; this budget excludes any server-imposed pressing fee. Funds,
space and vendor availability must permit the transaction.

STOP prevents further actions; an already issued request can still finish.
Timeouts, uncertain actions and restarts require manual review. No interrupted
batch resumes automatically. Do not restart a batch blindly on a partially
loaded fragment. All resulting gems, including Stalwart, are retained. Automatic
rejection/destruction and replacement from Soot's meta are intentionally absent.

## Research and saved data

Native imprint and modifier messages feed compact recipe observations. Verified
native preparation links its starting level and confirmed doses; manually
selected fragments are labeled unverified associations. Prediction is empirical,
not a promise or proof that a component caused an effect. Unknown and partial
recipes remain labeled; collect repeated controlled recipes for useful rankings.

Per-server/character data lives under `%LOCALAPPDATA%\ACAI2-Threads`, in a hashed
identity folder: compact press/run records, capture state, `prep.json` checkpoint,
`ui.json` appearance and explicit exports. No raw chat transcript or combat-event
CSV is retained. No old ACAI2 history is automatically imported. Position, opacity,
lock and recipe selections persist independently for each character. History is
not automatically deleted; back up this folder before manually managing records.

This first release has automated fake-world checks and a genuine SDK build;
these cannot establish live server message behavior. Test one gem attended before
larger batches. No combat meter or Keyfer binaries are included.
