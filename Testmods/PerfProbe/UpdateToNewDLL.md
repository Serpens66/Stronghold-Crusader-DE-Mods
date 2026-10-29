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
