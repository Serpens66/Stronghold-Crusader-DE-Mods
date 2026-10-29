# PerfProbe: native accesses and DLL updates

## Scripted AI orders (`src/PerfProbeAutomationActions.DevDiag.cs`, DEVDIAG builds only)

- Owning feature: `Automation.Actions`, scripted AI attack orders during an automated run.
- Reference image: `CrusaderDE.dll` SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Target: AI ally order function at RVA `0xD7580`, 587 bytes, called as `(attackTarget, orderedAi, requester, order)` with order 1 = attack. This is the same contract as `Testmods/SpectatorEditorBuildTest` (`TestToolsNativeOrders.cs`).
- Validation:
  - SHA-256 of the installed DLL against the reference.
  - The executable section containing the function.
  - Byte-for-byte comparison of all 587 live bytes with the hashed file image.
- Fallback: none. The function contains relative references that are audited only at this origin, so the feature stays hash-bound.
- Failure behavior: the log line `AUTOMATION ACTIONS_DISABLED <reason>`. No order is issued; the rest of the automation and PerfProbe keep running.
- Update check: confirm the new DLL hash and the function boundary and parameters (see the SpectatorEditorBuildTest audit), then update the hash, RVA and length here and in the source together.

## Pitch-fire probe (`Automation.Actions` entry `seconds:IgnitePitch`, DEVDIAG builds only)

- Reads the native direction table at RVA `0x51890D0` (one byte per tile id; see the native baseline `GROUND_ROUTE_BUILDERS.md`) before and 3 s after setting every dormant pitch ditch on fire through the Extender pitch API.
- It is a read-only diagnostic. The feature is active only once the order function above passed its hash and byte checks (same image); there is no separate fallback.
- Update check: confirm the table address against the new baseline before running the probe.
