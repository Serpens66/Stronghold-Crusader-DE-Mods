# Native demand save roundtrip, 2026-11-22

## Identity and scope

Controlled local gameplay acceptance, executed by the coordinating agent on an
inactive Win32 desktop. Native reference:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
APIShared 0.62.0 and the corrected AI market diagnosis/fix were tested together
with Script Extender, Fixes, AIBuildDiagnoseTest and its BugfixesAndQoL dependency.
The acceptance mod uses the public Automation API and read-only reflection into
the fix's private request state. It never writes pending cells or fabricates a
source/provenance callback.

Run: `4d73582e76624d789ecf0636724503a7`.
Compact evidence source:
`.work/a/market-save/003/4d73582e7662/LogOutput.log`.
The runner also retained the run-owned save in that directory. The coordinating
agent verified restoration of the original files/configuration after execution.

## Verified transitions

Local log times, 22 November 2026:

| Time | Observation |
| --- | --- |
| 15:34:11.716 | Custom AI player2 funded to 1000000 gold; native recruitment gate +2B6C=0, ready peasants=0, existing peasants=4, population/housing=114/114. Physical equipment deficit established. |
| 15:34:12.232 | After natural unpaused warmup, +2B6C=1 and ready peasants=1. Actual native recruitment returned failure2, missing good21 (maces), producing a tracked package5. |
| 15:34:13.568 | Physical AddGood1 followed by RemoveGood1 left stock0, native pending4, tracked expected4, original package5, credited1, last-observed stock0. Ordinary native save writer succeeded. |
| 15:34:14.013 | First raw native restore, session2: pending4 and credited1 restored before the first fold. |
| 15:34:14.609 | First managed start and actual shared purchase-decision observation passed. |
| 15:34:15.003 | Second raw native restore, session3: the same pending4/credited1 invariants held before the first fold. |
| 15:34:15.573 | Second managed start and shared decision gate passed. |

`AI_ECONOMY_CAPABILITY: feature=shared-ai-market-observation, available=True`
confirms that AIBuildDiagnoseTest registered with the shared broker. Its existing
emergency-demolition entry was reported unavailable with the foreign owner
preserved; no competing detour was installed there.

## Preconditions and earlier fixture failures

The first two attempts did not establish the necessary native recruitment gate.
The first also limited manual producer calls to four without accounting for its
actual AIC recruitment cadence. The log's ready-peasant field +88 is distinct
from existing peasants +8C; they must not be confused.

The native producers require a positive +2B6C gate. Identified peasant-AI
12A8F0 paths increment it. Merely having housing, money, barracks or a positive
existing-peasant count does not establish that gate. The final fixture waits for
it naturally under the engine monitor, then issues the normal pause command and
runs the actual producer within the same monitor scope. It never writes +2B6C.

Within 190CA0, gold and missing equipment are checked before scanning idle
peasants; this does not bypass the outer producer's +2B6C requirement.

## Limits

This verifies one genuine recruitment-source package for maces and two local
save roundtrips. It does not independently prove every good, mode, partial native
MP repair or two-peer transport. Native broker/relay and save-policy CLI tests
cover additional contracts separately. This short run does not establish an
eight-hour log budget or full-game callback latency percentiles.
