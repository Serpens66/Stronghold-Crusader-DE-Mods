# Updating Improved Hunters for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

The mod writes a timestamped Error and initializes no runtime for every other
DLL.

## What must be checked after an update

1. Revalidate the Script Extender `GameUnit` array and every raw unit field used
   by `ImprovedHuntersRuntime`: `+0x88`, `+0x92`, `+0x94`, `+0xC0`, `+0xC2`,
   `+0x29C`, `+0x2BC`, `+0x2C4`, `+0x370`, `+0x39A`, `+0x39C`, and `+0x448`.
2. Confirm the meaning and values of the hunter/prey AI states, corpse flag,
   death timer, reservation, target unit/global IDs, and tile coordinates.
3. Revalidate the camel and chicken despawn AOBs. Each must match exactly once,
   and the signed 16-bit immediate at pattern offset `13` must still be the
   despawn duration.
4. Verify camel health reads/writes and the Script Extender unit-health refresh.
5. Test hunter retargeting, projectile compensation, collected-corpse cleanup,
   camel health, and chicken neutralization on a fresh and a loaded map.
6. Only then update the shared current DLL hash.

Any scan or patch failure already logs an Error and leaves the affected native
path inactive.
