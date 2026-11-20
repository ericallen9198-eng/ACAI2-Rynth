# Plateau Treasure - Verified v1

Local adaptation of Soot's supplied 275 Plateau Treasure Beta. No plugin or Bridge changes.

INSTALL
1. Stop RynthAi. Put the .af/.met files in its active MetaFiles folder.
2. Put ALL .nav files in its active NavProfiles folder, including the eight PT Verify files.
3. Load the new, distinct name: /ra meta load 275 Plateau Treasure - Verified.met
4. Start in Default. Keep your existing treasure-map/doubloon loot profile and combat profile.

CHANGES
Each hunter has a separate approach route followed by an explicit /ra usel <NPC name> action. Type-7 NPC nav steps are not relied on: the reviewed Rynth navigation engine reads them but does not dispatch an NPC-use action. The meta stops that route after a recognized assignment, progress, cooldown or reward reply, then proceeds to the next hunter. Introductory level/flavor text is insufficient. It retries the same hunter at most 3 times (20-second waits), then names the failed hunter and holds. It never assumes a task was accepted.

The exact target and required kill count are captured from the assignment/progress reply, including Hive Guardians and Virindi Sentries. Completion notices are matched against those captured values, not hardcoded Mutilator/Observer flags. These markers are informative: task status does not stop treasure hunting or trigger automatic zone transfers.

The original initial region choice is retained where tasks remain; if every task is complete/on cooldown it starts the East hunt. Once hunting, it stays on that hunt route and keeps processing treasure maps. There is no 45-minute no-kill-task-progress stop. Death and existing travel/treasure recovery limits remain. Daily scheduling, new zone transfers, auto-selling and resupply are not added. Existing recorded routes do not prove coverage of every alternate task target.

UNCHANGED DEPENDENCIES
The Get to nav still uses spell 1636 (Lifestone Sending) and Soot's recorded landing/path to Plateau Hunters' Camp. Confirm your travel setup. Treasure encounters still request magic combat mode, as in Soot's version. AutoCram remains off. The original Questing character profile and loot filter were not included; use your own. No in-game actions were executed during offline verification.

FIRST CHECK
Watch VERIFY_ZEFIR_WAIT -> VERIFY_URSUIN: a terminal hunter reply must occur before moving on. /ra listvars shows pt_current_npc, pt_npc_attempt and the per-hunter pickup/target/required fields. If a reply is unrecognized, send its exact text; do not force flags.
