# Occupied wall demolition native contract

Reference SHA-256 for the loaded game DLL:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The test is hash-bound because it uses interior control-flow sites, live register
state and the native cursor layout. A different build leaves the test disabled.

| Site | Displaced instructions and continuations |
| --- | --- |
| `0x79F84` (20 bytes, 6+8+6) | Publish wall selection, then test the pending-removal bit. Pending requests resume at `0x79F98` and are rejected. Vanilla otherwise goes to `0x7A044` (selected packed tile in R14D). The armed tile goes to `0x7A058` after the occupancy decision. |
| `0x63749` (15 bytes, 9+6) | Compare the 16-bit occupancy head at `tileManager + 0xBF6C00 + tile*2`. EDI holds the packed tile. An empty or armed tile resumes at `0x63758`. Other occupied tiles use Vanilla's epilogue at `0x63842`. |
| `0x3A11DC0` | First `0x80` bytes of cursor input, saved and restored under the APIShared engine monitor around the managed `EngineInterface.DeleteBuilding` call. This call queues the original Chore 41. |

The query hook is downstream of the wall owner and structure-absence checks.
The execution hook follows the original pending-bit clear and does not touch
the occupancy list. Both preserve the live registers and flags across the tile
comparison. Fixes' moat inline hook starts at `0x7A047`. The Script Extender's
wall bulldoze detour starts at `0x63700`. Both have disjoint displaced spans.

To re-audit, compare the raw DLL hash and decode both containing functions
with their direct callers, all incoming edges, switch targets and continuations.
The reference PE exception-table function scan found no direct branches to the
interior of either span, and the query's indirect type dispatch targets start at
`0x7A097`. Check the target-building and pending-bit gates and the Chore 41
refund sequence. Recheck the actual loaded RedBird displacement and x64
patch form, NativeSiteRegistry ownership, the installed managed wrapper,
and the audited cursor inputs. Run the empty, blocked and occupied controls
with and without Fixes. Do not rely on an RVA or pattern alone.
