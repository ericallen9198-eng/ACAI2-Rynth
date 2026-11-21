# Actual Rynth expression audit

Run this tool against a genuine built RynthAi DLL, the mission JSON emitted by
ACAI2.MaraeMission, and that build's actual ExpressionEngine.cs. An optional fourth
argument supplies expressions recorded during the genuine MoonSharp controller tests.
The Windows release workflow runs the auditor as x86 against the deployable DLL.

Reflection invokes the actual nested Parser with its execution-suppression flag set,
checks full input consumption, and recursively audits function arguments, lazy branches
and quoted list templates. Function names and text/template argument positions are
read from the same source's registry. No meta game actions are executed in this pass.

Separate semantic probes use a null host, an in-memory persistent-variable dictionary
and safe variable/list operations only. They verify the reported task-selection flags,
timers, cache exclusion and slider clamps. A currency call with no world cache must
refuse. This does not prove live server acknowledgments or game loading on Linux.
