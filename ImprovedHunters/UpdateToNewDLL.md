# Updating Improved Hunters for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24530188`
- DLL size: `3450880` bytes
- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`

The mod is strictly hash-gated because it uses raw unit layouts. On the audited
hash, it validates each pattern only at its direct RVA and does not scan the
DLL. Every other DLL leaves the complete runtime inactive.

## Native address map

| Source pattern | Reference RVA | Use / offset |
| --- | ---: | --- |
| `CamelDespawnTickTimePattern` | `0x158418` | signed immediate at `+13` |
| `ChickenDespawnTickTimePattern` | `0x1633C5` | signed immediate at `+13` |

The source constants contain the complete wildcard patterns.

## Required update audit

1. Require one semantic match for both entries and verify that operand 1 at
   pattern offset `13` remains the signed 16-bit despawn duration.
2. Revalidate the Script Extender unit array and raw fields `+0x88`, `+0x92`,
   `+0x94`, `+0xC0`, `+0xC2`, `+0x29C`, `+0x2BC`, `+0x2C4`, `+0x370`,
   `+0x39A`, `+0x39C` and `+0x448`.
3. Confirm hunter/prey states, corpse flag, death timer, reservations, target
   IDs, coordinates, camel health and visual health refresh behavior.
4. Test hunter retargeting, projectile compensation, corpse cleanup, camel
   health and chicken neutralization on fresh and loaded maps.
5. Update both RVAs before approving the new shared hash.
