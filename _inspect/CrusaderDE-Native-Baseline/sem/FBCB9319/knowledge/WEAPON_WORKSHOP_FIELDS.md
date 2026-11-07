# Weapon workshop production fields [Scope: Vanilla]

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The installed DLL, `CURRENT.json` and the queried native function records matched on 2026-11-07.

| Meaning | Raw encoding | Manager-relative field displacement | Public `GameBuilding*` field offset | Extender declaration |
|---|---|---|---|---|
| `BuildingProductionCurrentGood` | Signed little-endian 16-bit goods ID | `0x2EA` | `0x28E` | `r_NextProducedGoodId` |
| `BuildingProductionNextGood` | Signed little-endian 16-bit goods ID | `0x2EC` | `0x290` | `r_ProducedGoodId` |

Vanilla uses these meanings for the Fletcher, Poleturner and Blacksmith. The field
address is `manager + buildingId * 0x32C + displacement`. The public interop view
starts at `manager + buildingId * 0x32C + 0x5C`; `TryGetBuildingById` subtracts one
for an array whose first entry is already after the slot-zero record. The
installed Extender declares the two fields as `eGoods : Int16` in the packed
812-byte `GameBuilding` struct. Its names are lookup aliases for those exact
bytes. The native readers and writers establish their meaning.

`SetNextWeaponMade` sends Chore 33. The single-building handler at `0xD5870`
compares the global ID and writes a word at manager-relative `0x2EC`; its bulk
branch calls `0xC52B0`, which also writes `0x2EC` for matching active buildings.
`0xB47E0` initializes both words. The three worker updates (`0x12D230`,
`0x13AAD0`, `0x13BDD0`) copy the next word into the current word at native latch
points, then use the current word for the production/transport branch and
delivery calls. `0x19D960` publishes the current word's low byte for the weapon
being made and the local selected-production cache for the next choice. Opening
one of the three panels refreshes that cache from the next word.

The Armourer (`0x13CF30`) and Tanner (`0x13E0B0`) use fixed goods in their Vanilla
production paths. A raw read from either of those building types does not
describe its current or next output. The field pair also does not indicate that
an idle or stopped workshop has started a batch.

For the choice command, goods bounds and related Vanilla flows see
[the workshop findings in the Hunter audit](HUNTER_PREY_RULES.md#cows) and
[the AIV initialization audit](PREBUILT_WORKSHOP_BOTH.md).

## Ingredient thresholds and debits

The following control flow is confirmed statically against the same installed image.
The local quantities are 32-bit fields in the building record. Manager displacements
for wood planks, cowhides and iron ingots are respectively `0x184`, `0x190` and
`0x194` (public interop offsets `0x128`, `0x134` and `0x138`).

| Workshop | Vanilla input per charge | Collection and debit |
|---|---|---|
| Fletcher | 2 wood for bows, 3 for crossbows | `0xB9280` stock search asks for one plank per trip. The local threshold at `0x12D755` is evaluated after drop-off against the next good. After preparation, the block at `0x12D9E7` clears the local planks on the production path. |
| Poleturner | 1 wood for spears, 2 for pikes | Same single-plank stock search. The local threshold is at `0x13B10F`; the debit block at `0x13B3A7` clears the local planks. |
| Blacksmith | 1 iron | The worker deposits one ingot at a time, with local capacity 48. In state 6 at `0x13C4E2`, the local iron test separates the `0xB6100` debit from the finished-product recovery route. |
| Armourer | 1 iron | Same collection and local capacity. The state-6 test is at `0x13D6AC`, followed by the iron debit or finished-product recovery. |
| Tanner | 1 hide | State 1 at `0x13E408` can send the worker for a cow when there is no local stock. State 9 grants 3 hides via `0xB6100` at `0x13EA38..0x13EA4C`, with capacity 3. State 6 debits one hide at `0x13ECC0` and advances to state 5, whose later updates compute yield at `0x13EBF7`. |

The wood workshops can search the stockpile before rechecking local planks. This
matters if an extension leaves surplus in the building after a debit or changes
the next cost. In the iron state-6 blocks, a shortage by itself does not establish
that a finished lot exists. The recovery path's withdrawal attempt and following
yield are separate operations. `0xB6100` rejects a deposit that would exceed the
specified capacity as a whole. A three-hide deposit with any retained hides
therefore exceeds the Tanner's Vanilla cap.
