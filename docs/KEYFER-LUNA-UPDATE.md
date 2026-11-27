# Keyfer Luna trial — Bridge v0.20.1

This update switches the Bridge's default model from GPT-5 mini to GPT-6 Luna.
It keeps your existing personality files, memories, chat history, moderation,
chest awareness, Discord connection, wiki lookup, arena and treasure services.
No game plugin or Rynth DLL replacement is needed for this model update.

## Install

1. Stop the current Bridge with Ctrl+C. If asked to terminate the batch job, enter Y.
2. Download and extract the entire Windows update ZIP. Keep a backup copy of your
   current Bridge folder before replacing its files.
3. Copy the ZIP's **Bridge folder** into the folder that contains your current
   **Bridge folder**, accepting file replacements. This updates the existing
   location. Do not delete or replace your Data, Knowledge, Config or keyfer.json.
   Keep your existing API key, data-path variables and custom startup settings.
4. Start the Bridge with your existing launcher. Its header should read:
   ```text
   ACAI2 Bridge v0.20
   AI model: gpt-6-luna (reasoning: none)
   ```
5. Only one Bridge should be running. Your existing game and Discord clients can
   reconnect to the same listener; no need to repeat the chest setup commands.

If the header names another model, your launcher or environment explicitly sets
**ACAI2_OPENAI_MODEL**. Change that setting to **gpt-6-luna** and restart the Bridge.
The included **Start-Keyfer-Luna.cmd**, placed beside the Bridge folder, forces
Luna for that launch only and does not change Windows environment settings.
Use your custom launcher instead if it supplies credentials or other configuration
that are not already in your Windows environment; keep those settings and update
only its model selection. Never paste an API key into chat or commit it to Git.

## What to try

- Say hello in a tell and ask about a previously remembered preference.
- Ask about his current chest pulls while he is actually pulling.
- Ask a factual DreamWeave question that should use his wiki lookup.
- Try a normal public exchange and, if you use it, an arena match.
- Confirm Discord responds through this same Bridge if you use that connection.

Look for his familiar voice, correct facts, short replies and ordinary response
speed. His code-enforced timeouts and command protections remain in place.
An unavailable provider/model is reported as a request failure; this trial does
not silently switch to another billable model or replay game actions.

## What was verified

Controlled fake-provider tests cover chat replies, the wiki tool round trip,
structured memory review against existing SQLite notes, arena JSON commentary,
and the legacy GPT-5 mini override. Existing moderation and client checks also run.
These checks prove request/response handling, not Luna's live personality quality,
latency, your account's model access or real usage cost. No player chat was sent to
an AI provider during testing. The live trial is the next step.

## Model and rollback

GPT-5 mini is scheduled to shut down December 11, 2026. The optional temporary
**ACAI2_OPENAI_MODEL=gpt-5-mini** override retains its **minimal** reasoning setting
for comparison before retirement. Luna uses **none**, including wiki function calls.
You can also stop the Bridge and restore the backed-up Bridge binaries. Neither
method rolls back or clears your memories. Do not leave the retiring model selected.

This is a Bridge-only update. Scrollwork, Threads, Marae, Combat and the Keyfer
client plugin are not replaced. The Bridge remains shared by game and Discord.

## v0.20.1 diagnostics

If a request fails, copy the new `[KEYFER] Request failed:` or `[MEMORY] Review
unavailable:` line from the Bridge console. It now reports the HTTP status and
recognized provider error code/parameter. It never prints provider error messages,
API keys, prompts or player conversations. Examples: HTTP 404 / model_not_found
means model availability or project access; HTTP 401 means authentication; HTTP 429 /
insufficient_quota means API credit or a spending limit. These are diagnostics,
not a claim that any one cause has already been confirmed on your PC.

The `[LEADERBOARDS] No recognized output` lines concern native /top capture;
they are separate from AI provider failures.
