# Keyfer behavior reference — plugin v0.3.6 / Bridge v0.20.4

These are source defaults and enforced rules. Your existing keyfer.json, saved
quiet/owner/exclusion settings and Bridge behavior files can change the configured
options; this package preserves them. This guide does not claim to have read the
current settings on your PC.

See [KEYFER-CONVERSATION-AUDIT.md](KEYFER-CONVERSATION-AUDIT.md) for the v0.3.6
warmth/banter guidance, silence handling, public awareness and native absorption
capture. The existing moderation rules below remain in effect.

## Existing player behavior retained

| Area | Behavior |
| --- | --- |
| Identity | Only the configured server and character run the responder. Default identity is DreamWeave / Keyfer Sutherland; enabling is explicit. |
| Personality | Friendly, welcoming, concise first-person player conversation. No unsolicited roasts, invented experiences, or roleplay. Existing personality/server behavior files remain in use. |
| Tells | Answer the sender privately. Private text is not broadcast as telemetry or ambient chat; scoped Bridge conversation/memory handling still applies. |
| Public conversation | Addressed General/Allegiance messages can receive replies when enabled. Same-speaker/channel follow-ups have a 45-second client window. |
| Public brevity | Ordinary public answers target one short sentence, ideally under 160 characters, at most 200 before private routing. Long answers and continuing General conversations move to tells. A handoff lasts five minutes; its announcement is limited to once per ten minutes. |
| Samm and Toe | Keep their asking channel instead of the public-to-tell handoff. Addressed local dialogue with these two is supported too. This familiar-player exception is distinct from the configurable controller list. |
| Public pacing | Bridge public reservations: at least 20 seconds per speaker, at most three within two minutes; at most six per channel per minute, with six seconds between channel reservations. Other queues/quiet controls may further suppress replies. |
| Quiet and mute | Public quiet, General mute, emote enablement, exclusions and controller settings persist. Quiet still permits private conversation and authorized controls. Familiar-player exceptions remain. |
| Command protection | Arbitrary Rynth, Utility Belt, shell, bank or game commands requested through conversation are refused before execution. Instruction overrides, secret extraction and explicit bait are guarded. Asking how a documented command works is different from asking Keyfer to execute it. |
| Existing timeout ladder | Client command/override/explicit-bait offenses: five minutes, then 30 minutes, then 24 hours; client strike history expires after 30 days. Bridge scored manipulation/repeated bait also uses the five-minute/30-minute/24-hour ladder. These are separate existing layers, not one universal counter. |
| Emotes and ambient reactions | Sparse fixed contextual reactions to nearby/allegiance chat; no quoting player bait or reacting to spell incantations. Every fifth eligible sample may react; one-minute startup delay, five-minute ambient cooldown, shared 90-second emote cooldown. Quiet, pending replies and active arena activity can suppress them. |
| Arena, world events, treasure, leaderboards | Existing services remain. Arena narration is paced; leaderboard actions use the fixed read-only /top catalog. Prize transfers remain separately configured, fixed-template and disabled by default. |
| Chest controls | Only actual in-game Toe/Samm tells or allegiance requests can start/stop/query a locally configured key/chest. Fixed requests: “Keyfer pull keys”, “Keyfer stop pulling”, “Keyfer pull status”. These bypass AI and do not permit arbitrary commands. |
| Loot awareness | Confirmed Keep pickups and native chest activity inform answers. Rule previews and salvage messages are not kept-item evidence. Recent findings are capped at 12, notable findings at 24; no AI call per pull. |
| Restart handling | Quiet/emote preferences and saved timeouts persist. Logout cancels pending native replies so a stale session cannot speak after reconnecting. |

## New direct-insult rule

* A recognized direct insult or derogatory renaming of Keyfer immediately starts
  a 24-hour reply timeout. It does not step through five and 30 minutes first.
* Actual in-game **Samm and Toe are exempt from this new rule**. They remain subject
  to command/secret safeguards. A Discord display name alone does not establish
  the in-game exemption.
* The first notice goes to the channel where the insult occurred: General,
  allegiance, local, tell, or the Discord request channel. It is not redirected
  to allegiance or tells. This one boundary notice can pass public quiet/General
  mute so it can be delivered where requested.
* Example: “I don't like being spoken to that way. I'm taking 24 hours away from
  your messages.” Three curated first-person variants avoid quoting the insult
  or an offensive player name. The notice works without asking the AI model.
* Subsequent messages receive no normal answers during the timeout. Repeated
  insults neither restart its clock nor repeat the public announcement. Existing
  private remaining-time reminders are limited to once per minute.
* Deadlines persist across logins/restarts. The new marker stores identity and
  deadline, not an expanding raw insult transcript. Existing Bridge moderation
  records from other rules retain their prior bounded-summary behavior.
* Recognition is deliberately conservative: direct insults and the supplied
  derogatory nickname pattern, not perfect understanding of every possible
  insult. Ordinary anatomy questions, identity labels, negated insults, reported
  quotations and offensive player names alone do not trigger this rule.

A timeout means **Keyfer ignores that player's messages**. It is not a game ban,
Discord server timeout, or channel moderation action. Game and Discord identities
are scoped separately; no cross-platform human identity is guessed.

## Local commands

```text
/acai2r keyfer status
/acai2r keyfer reload
/acai2r keyfer quiet on
/acai2r keyfer quiet off
/acai2r keyfer emotes on
/acai2r keyfer emotes off
/acai2r keyfer treasure
/acai2r keyfer standings
```

Update both plugin and Bridge for this release. Preserve existing keyfer.json,
API keys, environment variables, behavior files and Data directory. See
KEYFER-SETUP.md for installation.
