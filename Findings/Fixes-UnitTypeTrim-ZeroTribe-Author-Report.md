# Fixes 1.19.1: selected tribe ID is not validated before API calls

## Observed log errors

On 25 September 2026, a client running Fixes 1.19.1.0 recorded these four consecutive errors:

```text
[Error  :  SHCDE-SE] [T17-22:29:14.855] [GameTribeManagerAPI] [TryGetTribeById] Tried to access tribe index that was out of range: [0/4500]
[Error  :  SHCDE-SE] [T17-22:29:14.855] [GameTribeManagerAPI] [GetStance] Could not find tribe with id: 0
[Error  :  SHCDE-SE] [T17-22:29:14.855] [GameTribeManagerAPI] [TryGetTribeById] Tried to access tribe index that was out of range: [0/4500]
[Error  :  SHCDE-SE] [T17-22:29:14.855] [GameTribeManagerAPI] [GetUnits] Could not find tribe with id: 0
```

The log has no stack trace for these calls, so it does not establish which caller produced them.

## Verified Fixes code path

Decompilation of the installed `fixes.dll` (SHA-256 `080F35F090F84E7D4B68932B1796BF0C627C0147E142884D25A8C2B278DA467F`) shows that `FixesUnitDetours.c_game_tribe_remove_unit_type_hook_impl` calls `GameTribeManagerAPI.Create(playerId, false)`, reads `CurrentSelectedTribeId`, then passes that ID to `GetStance` and `GetUnits`. The same sequence appears in `src/shcde-fixes/Detours/UnitDetours.cs`, lines 385-396. There is no validity check between reading the ID and calling those APIs.

If `CurrentSelectedTribeId` is `0`, this hook passes `0` to both APIs. The Script Extender rejects tribe ID `0`, so this is a concrete invalid-ID path in the installed Fixes code. The four log entries are consistent with this path, but the missing stack trace prevents attributing this particular occurrence to the hook.

Please validate the selected tribe ID before calling `GetStance` or `GetUnits`, and move `Create` after that validation so an invalid selection does not reach it. A reproduction with selected tribe ID `0` and another with a valid selection would verify the corrected behavior.
