# Keyfer conversation audit — client v0.3.6 / Bridge v0.20.4

## Findings and changes

- Silence: the Bridge previously suppressed [NO_REPLY] only in public channels.
  It now consumes the exact internal silence marker in every channel, including
  tells and Discord. Recognized quote/code wrappers are consumed too. A silence
  response produces no speech, emote, handoff notice or saved assistant turn.
  Older saved silence turns are excluded from model history.
- Public awareness in tells: the context previously selected only the asking
  channel, so tells omitted all observed General/Allegiance chat. Toe and Samm's
  tells can now use the same client's recently observed public chat. Ordinary
  visitors' tells get General observations without gaining allegiance context.
  Public General replies continue excluding allegiance observations. Private
  tells remain scoped to their original conversation.
- Avatar absorbs: the supplied line has no player-chat envelope, so the old
  player parser skipped it. The client now recognizes that exact bare native
  server format and submits a typed, session-bound observation. Quoted player
  chat and pasted tells cannot become native absorption evidence. The Bridge
  reparses and validates the line, timestamp, identity and receipt ID.
- Recording: native absorption evidence is in-memory only, bounded to 64 receipts
  and one hour. A prompt includes at most 12 recent records plus the retained
  count, timestamps, levels and XP. Zero-XP absorption is still an observed absorb.
  Retransmitted receipt IDs do not increment counts; two genuine equal lines
  with separate capture IDs remain separate observations. Counts are explicitly
  incomplete retained observations, not a lifetime allegiance total or a timer.
  Native allegiance evidence is supplied only in Allegiance or Toe/Samm tells,
  and only for the asking game session. It is not supplied to General/Discord.
- Personality: warm, easygoing in-character banter, self-deprecation and absurd
  metaphors are explicitly welcome. When invited, Keyfer can make a clearly
  labeled silly guess without refusing merely because there is no real forecast.
  He must still distinguish a joke from an observed event or factual prediction.
  He should use supplied observations, and acknowledge a missing specific line
  rather than claiming an entire observed channel is invisible.
- Text: game-compatible punctuation and the personal-loot wiki routing from the
  preceding Bridge updates remain in place. Long tells may still be split to fit
  the game transport; text is not discarded to force one short message.

## Protections retained

Existing timeouts, insult/command/identity-manipulation protections, owner
exceptions, private-history separation, per-session cancellation, bounded request
queues and pump-only native dispatch remain. The model gains no game-action
capability. Native absorption capture makes no AI request and does not echo the
announcement. Existing chest, treasure, world-event and arena services remain.
No ordinary server transcript or additional persistent absorption file is created.

## Limits and live check

Old announcements cannot be backfilled. Recording starts with the updated client
and is cleared when the Bridge exits. Busy/cancelled/disconnected observations
may be missing; counts are never presented as complete history. Timers, absorption
mechanics and future outcomes are not inferred from an announcement.

The audit covers repository prompts and policies. Your existing external
Knowledge/behavior files are preserved and have not been inspected here; conflicting
instructions in those files could still affect voice. Live personality still
needs a conversation check. All automated checks use fake HTTP/game interfaces.

Update both client plugin dependencies and Bridge from the v0.3.6 full Windows
package, with the game and old Bridge closed. Keep keyfer.json, Data, Knowledge,
Config, API keys and environment settings. Do not replace those with examples.
Launch using your existing Luna launcher and confirm Bridge v0.20.4. After a new
native absorption, ask Keyfer about it in allegiance or a Toe/Samm tell. Try a
friendly acknowledgment and an explicitly silly prediction request too.
