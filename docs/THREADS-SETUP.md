# ACAI2 Threads — standalone attended-test build

This is an independent managed x86 Rynth plugin, not a replacement for RynthCore,
RynthAi, Soot's installation, or Keyfer. Requires CoreCLR RynthCore API 92+ and
matching engine ImGui support. Native gem preparation also requires stock
RynthAi.Script v1 and RynthAi.Activity v1 interfaces. The optional RynthAi.Yield v1
interface holds background item moves without changing saved settings. Missing interfaces disable
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

v0.1.4 adds **Auto-withdraw notes when needed**, saved per character and off by
default. Off: a shortage stops with a clear error. On: before a fragment purchase,
the builder counts carried notes matching the vendor's currency and sends only
`/b w n <shortage>` for the confirmed 250,000-pyreal note currency. It waits for
the matching native BANK confirmation and inventory increase before continuing.
Insufficient funds, refusal or timeout stops without retry; a restarted batch
never replays a possibly issued withdrawal. No automatic deposits or exchanges.
This covers the quoted purchase shortage, not a guessed extra pressing fee.

v0.1.3 supersedes the strict center-distance check from v0.1.2. The client/server
handle reach and any normal short approach to the intended vendor or press.
The three exact-name stations must be unique within 12m of the batch's original
position; this is a local workspace boundary, not a claim about use radius.
Turning does not count as travel. Position/cell changes during a pending vendor
or press use are allowed within that workspace, and the stationary anchor is
updated after acknowledgment. No general navigation or route runner is added.

Vendor quotes are sent unchanged through Rynth's native purchasing API. DreamWeave
handles bank funding. The native purchase helper still checks carried currency;
the optional withdrawal toggle supplies that shortage. No denomination conversion
or note exchange is performed. The purchase cap applies separately to each vendor's quoted
currency units (pyreals remain pyreals); unlike denominations are never added
together. Native purchase refusals are reported with their reason rather than
silently waiting for a timeout.

v0.1.1 added exact Start diagnostics, automatic macro pause/resume and selectable
300/325/350/375 blanks. Start reports refusals directly in game chat and the Gem
Maker tab; `/acai2threads status` includes the latest diagnostic.

Start with tracking only: Current Run records native Thread opening, realm,
progress, clear and crumble messages. Opening and actually entering are separate.
If realm information is unavailable, manually mark entry/outside. Time counts
observed intervals; reconnect gaps are labeled. Linking a crafted gem to a run is
explicit, never an assumption that the latest press opened the dungeon.

For one attended gem-making test, stop other inventory and crafting tools. START
BATCH pauses RynthAi automatically and waits up to 15 seconds for idle. Stand still in peace mode immediately beside one
Fragment Press. Choose blank level 300, 325, 350 or 375 (the vendor cap). Supply an untouched
Raw Fragment of that level and the exact selected
components. In Gem Maker select one new gem, leave buying OFF, review the recipe,
and click START BATCH. Do not use other inventory tools during the batch.

The builder identifies the fragment, applies each selected dose once, requires
native acknowledgment plus inventory consumption, presses once, and freshly
identifies the resulting gem. It does not navigate. Optional buying opens only
the named nearby fragment/reagent vendors and buys shortages within the selected
per-currency purchase cap; this excludes any server-imposed pressing fee. Funds,
space and vendor availability must permit the transaction.

STOP prevents further actions; an already issued request can still finish.
After normal completion or a user Stop between acknowledged actions, RynthAi resumes only if this
batch paused a previously running macro. A macro already paused stays paused.
If Yield is unavailable, AutoCram must be disabled manually when reported as a
blocker. No saved RynthAi settings are changed. Timeouts, uncertain actions and
restarts leave the macro paused and require manual review. Stopping while a
request is outstanding also leaves it paused. No interrupted
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
