# Wall, crenel and stair construction costs and refunds [Scope: Vanilla]

Static audit, 2026-11-28. Native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Managed SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
The installed files matched CURRENT.json and the selected database identities.
The call inventory comes from the baseline xrefs and decompilation, checked
against the installed PE's x64 instructions at the listed RVAs. Confidence is
confirmed for the static data flow and arithmetic. No game was started.

## Construction, availability and debit

The human wall tools are mappers 25 (wall), 26 (crenel), 27 (stairs) and 46
(low wall, symbol MAPPER_WOODWALL). All four spend stone in ordinary play.
The placement path uses tile layers, with no per-building price row or slot.
The separate managed menu rollover still uses GameData.getStructureCosts when
it requests a structure-price row. That lookup is independent of the native
wall-line quote.

| Stage | Evidence |
|---|---|
| Tool selection | `0x8BD60` calls `0xCF640` for good 4 for all four tools and clears the mapper if the result is zero. |
| Drag preview | `0x94120` calls `0xCF640` at `0x941AB` and writes the quota at `0x941CB` (RVA `0x60AD508`, tile manager +0x204E758). It calls the geometry validator `0x7A3B0` and preview `0x61210`; preview limits the count at `0x6136F`, removes uncharged existing cells from its count and calls the pricing helper with preview=1. A rejected preview zeroes the count. |
| Release and queue | `0x94252` tests the same quota for zero, with a world-kind-1 exception. A valid release enqueues Chore 25 with four coordinates, a mapper byte and a signed Int16 maximum. `0x11B50` executes with the Chore sender and forwards that maximum as argument eight at `0x11D74`. |
| Placement | `0x6F230` revalidates with `0x7A3B0`, recomputes quota at `0x6F2C2` (mapper in SI), and limits visits by the transmitted maximum and charged cells by the current quota. The quota branch tests quota < already placed; preview tests count >= quota. Ordinary wall over an intact crenel converts the tile without incrementing the paid count. Intact existing non-converted wall cells are skipped; a damaged cell may be rebuilt. Stair tiles require a positive rise and stop at an existing wall or a low attachment height. |
| Debit | `0xC8CC0` receives manager, player, high count, low count, preview flag. Its only native callers are `0x61210`, `0x6F230` and `0x6CB20`. Manual mapper 46 supplies the low count; mappers 25/26/27 supply the high count. `0xC8D2A` and `0xC8D2E` add 2*high + low to the player's build remainder. Actual placement writes total & 3 at `0xC8D3A`; the signed quotient by four is sent to `0xB7D80` as stone (good 4). Preview leaves the build remainder intact and passes the preview flag to storage accounting. The whole-stone quote is published at RVA `0x60AD534`, exported as a word by `0x19D960` at state offset +0x896. |
| AI/AIV | Executor `0x51790` uses preparation `0x5CD90`, then `0x6CB20`/validator `0x77CF0`. This path places a tile and updates pathing before charging. At `0x6CD8E`/ `0x6CD92` it skips the debit for mappers 181–186, the AIV stair range, including mapper 186 without a specific height writer. Other accepted mappers reach `0x6CDAC` with high=1, low=0, including mapper 46 and crenels 26/35. The AIV wall step has its own stock/retry logic, including a stone>=1 gate on the repeat path, and does not call `0xCF640`. |

The ordinary build remainder is the Int32 at
`0x379CF20 + player * 0x583C`, the same stone remainder read by
`0xCF640` (resource manager RVA `0x366C210`, offset +0x130D10).
`0xCF640` exposes a fixed, conservative quota of
`min(250, 2*stock - (remainder != 0 ? 1 : 0))` for both stone and, on its
separate good-2 branch, planks with their own remainder. It receives no mapper.
Thus standard walls, crenels and manual stair cells cost two stone quarters
per charged tile, and low walls one. Low walls still have the high-wall
availability limit in Vanilla. For example, one stone with zero remainder
gives a quota of two even with the low-wall tool. Cheat/world-kind 1 return
100 and bypass `0xC8CC0` pricing. World-kind 6 returns its separate stock,
takes the high count as whole stone, and bypasses the quarter calculation.

Geometry and terrain checks are documented in [CRENELATED_WALLS.md](CRENELATED_WALLS.md)
and [PITCH_STAIRS_HEIGHT.md](PITCH_STAIRS_HEIGHT.md). The regular building
cost path is documented in [CONSTRUCTION_COST_TABLES.md](CONSTRUCTION_COST_TABLES.md).

## Bulldoze and quantization

The confirmed normal route is delete mapper 39 through `DLL_MapAction`,
`0x8AAF0`, input/permission `0x90830` / `0x79B90`, then Chore 41.
The producer queues the packed tile and 50. Chore 41 also accepts signed byte
-1 for dug-moat removal, a separate branch before wall math. In wall execution,
`0x137FE` sign-extends the byte, `0x1386B` and `0x13873` load the unsigned
default/current height bytes, and `0x13880` takes the skip branch only when
current minus default >60. Otherwise the handler halves the byte with signed
division, yielding 25 for the normal command. This uses actual height delta.
A stair can land on either side of the threshold.

At `0x138A6`, the handler calls `0xC4480(manager, -1, sender, percentage)`.
Here -1 is the stone wall sentinel and the fourth argument really is a
percentage. The other sentinel branches are -2 for a plank accumulator and
-3/-4 for stationary siege weapons. The wall handler always uses -1,
including for mapper 46. A zero percentage exits immediately; world-kind 1
skips refund, and world-kind 6 adds one to the alternate stock for any
nonzero wall refund input.

In ordinary play the wall branch at `0xC44F0..0xC453B` adds
`trunc(2 * percentage / 100)` **quarters of stone per tile** to a signed
Int16 per-player refund remainder at `0x379CFE8 + player * 0x583C`, distinct
from the build remainder. It sends the whole quotient to the stone-add
routine `0xB58B0` at `0xC45AB`, with the modulo-four remainder stored
before the call. A failed addition can call the native storage warning.
The wall branch does not read tile, original price, category, or the regular
building table. Its integer buckets for 0–100 are:

| Fourth argument | Quarters added per wall tile |
|---|---:|
| 0–49 | 0 |
| 50–99 | 1 |
| 100 | 2 |

Thus four ordinary full-height wall deletions with zero refund remainder
produce one stone, whereas any number of ordinary deletions of tiles at
delta <=60 contribute zero quarters. This also explains why halving 50 to
25 cannot approximate a half refund for a one-quarter low wall. These are
arithmetic consequences, with successful storage needed to receive the stone.

Chore 41 runs the refund before `0x63700`. That helper clears the pending
bit, then checks tile-unit occupancy at `0x63749` before clearing wall logic,
height and decoration and scheduling path refresh. Its other native callers
`0x62460` and `0x62AB0` remove tiles without the refund call. A refund
event alone is therefore insufficient evidence of successful wall removal,
and a wall-removal event alone is insufficient evidence of a paid demolition.
See [AREA_DEMOLITION.md](AREA_DEMOLITION.md) for permission and removal details.

## Undo is a separate refund path

Local placement opens Undo mode 3 at `0x72890`, captures tile snapshots via
`0x596B0` (at most 100), and records the calculated whole-stone debit for the active
player at `0xC8D74`. `0x63CD0` enqueues Chore 76 (0x4C);
`0x17CC0` transfers three Int32 values (tile count, paid amount, good
selector), one signed mode byte and 0x4B0 bytes of tile snapshots. Mode 3
calls `0x63E90` with the recorded amount in R9D. It credits that amount
directly at `0x63EC4` and then restores the snapshots, without visiting
`0xC4480` or reversing the build/refund remainders. Mode 4 is the separate
pitch Undo route. The stock and tile restoration in mode 3 are independent
of the Chore-41 percentage. No new editor, save/load, or multiplayer
acceptance follows from these static findings.
