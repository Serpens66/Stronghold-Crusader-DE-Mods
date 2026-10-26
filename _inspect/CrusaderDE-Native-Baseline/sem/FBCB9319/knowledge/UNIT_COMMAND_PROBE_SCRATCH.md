# Read-only manual unit path probes [Scope: Vanilla + mod integration]

Verified native hash: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Review date: 2026-10-07.

18E1E0 hardcodes the global path manager at RVA 60AD660. Its calls to 196840, E7C40, E2610, DF720, E62D0 and F4930 are relevant to an exact movement witness. The selected GameUnit is addressed by its unchanged one-based game ID relative to the native manager/sentinel base.

The writable scratch closure includes:

- The entire selected 490-byte GameUnit, including portal/exit fields.
- The global path manager through offset 3C886C: queue at 32BE2C plus 320800 ushort entries. Modes at 80/84/88, counters/generations, portal outputs and the output pointer at 155F60 all belong to this snapshot.
- Distance grid at RVA 5225B10 and immediately adjacent stamp grid at 52C2550; each is 9CA40 bytes. Generation wrap invokes 7490 to clear stamps.
- Rectangle helper at RVA 34A9F50. 79C0 writes its first four int fields.
- Memory helper at RVA 60AD648, immediately before the path manager. 7490 writes the output pointer at helper+8, byte count at +10 and fill word at +14 before invoking the native memset implementation.

Preserving only the small manager header is insufficient. Restoring a generation without restoring its stamp grid is also insufficient. E62D0 installs a stack-backed output pointer for the probe, which must never remain published after it returns.

There is a second category of writes beyond scratch. When E1640 cannot reconstruct a path, it sets output length to zero and calls DAA50, E49D0, C3E50 and DE6A0. These calls rebuild masks/PCL/building connections and mutate global topology. A read-only probe can suppress these repair calls only in its isolated thread-local context; the result remains failure. Simulation must preserve the complete original sequence. E4E90 otherwise only reverses the probe's own packed output buffer. 181E00 and 107160 are read-only access/capability predicates.

Evidence: complete semantic function records and installed disassembly, additionally exported to `.inspect/UnitCommandSplit/scratch-native-audit.txt`. NativeX64 Indirect prefixes are checked against the installed image, with no baseline xrefs entering displaced interiors; see `.inspect/UnitCommandSplit/incoming-native-audit.txt`. Actual detour/backend and buffer/context regression fixtures are in `BugfixesAndQoL/tests/FriendlyMoatMovement.Tests`. These are static/native fixture proofs, not in-game acceptance.

## 2026-10-25: mandatory ordinary-region rejection

Rechecked against the installed DLL with the same full hash. The complete
18E1E0 instruction stream confirms the ordinary branch at 18E391..18E3C6.
For unequal PCLs with modes +84/+88 cleared, E2610 is mandatory; a zero return
branches directly to 18E468 (failure). Arguments are manager, signed player,
signed source/destination PCL and signed 16-bit connection mode. The mode load
is native unit record +9B8, corresponding to public GameUnit.r_PathConnectionMode
at +35C with the independently verified 65C structure bias. Current coordinates
are +71C/+71E (GameUnit +C0/+C2), not the movement executor's next-tile start.
196840 reads the completed-moat flag at the unit's current tile ID (+72C,
GameUnit +D0). The installed SHCDESE assembly's field types/offsets were checked.

Thus an effective E2610 zero observed during a fully restored failed ordinary
probe can rule out another mandatory query with identical player/PCLs/mode
inside the existing unchanged consecutive cursor batch. This is a negative
necessary-condition proof, not positive path permission. Same-region, arrived,
moat flood, surviving special scopes and ambiguous query sequences cannot use
that proof. E2610 itself writes scratch/counters and must not be called as an
unprotected read-only precheck. The optimization adds no call or hook, extends
no batch/cache lifetime and changes no executed probe's restoration closure.
Runtime performance/behavior acceptance of the optimization remains pending.

## 2026-10-25: same-PCL island and closed directed search sets

The Log_365 user reproduction surrounds dry ground with a completed moat. The log
shows 17505 actual probes in its peak five-second window, zero learned E2610
rejections, 1235.176 ms native work and 2309.566 ms restoration. Exact per-unit PCL
values were not in that recording; same PCL is a native-contract explanation, not
a directly observed value from this log. The previous region proof nevertheless
avoided 315472 probes across the full run. It remains useful and cause-independent.

Re-audit against the same installed hash includes all ordinary F4930 branches:
F3060 (nonzero profile direct line) and F32B0 (zero profile direct line) use outgoing
bits from 51890D0 and row*8 neighbor deltas at 405EDB0. DAAC0 adds occupancy checks;
failure falls back to DA590 at budget 400000. Mode +80 can select DA590 directly.
After a successful search, E1640 reconstructs. The profile-zero/+94 refinement
uses DB650 and then DA590 if needed. DB650 has extra terrain/structure exclusions.
All those ordinary paths use subsets of the directed connection grid; +84/+88
special traversal is a different graph and excluded. SE profile enums are Large=0,
Default=1; ordinary soldiers (including native type 26) normally use Default.

DA590 and DAAC0 write generation at manager+4, stamps at 52C2550, distances at
5225B10 and the existing queues. An exhausted queue differs from success/budget
termination, but queue state alone is not needed as a certificate. A stronger,
independent check validates a proposed stamped set: actual start inside, target
outside, no outgoing allowed edge leaves it. Every subsequent ordinary source
inside such a set is unable to reach that target in the same graph, even if edges
are one-way. A failed/budgeted/stale search without this closure supplies no proof.

The ordinary graph can include an immutable gate mask only when the exact active
scope and snapshot match. A missing scope is not equivalent to an active mask;
unfiltered closure is the conservative alternative. Existing publication identity,
map/tick/placement/diplomacy boundaries remain required. The stamp set must be
materialized before restoring the probe but published only after restoration.
The complete original scratch closure above is still required for every executed
probe. No smaller buffer, expanded batch lifetime, or extra native search follows.

Dry mixed-group command recovery uses a separate macro-query write closure, not
a reduced 18E1E0 snapshot. See [MANUAL_GROUP_REGION_RECOVERY.md](MANUAL_GROUP_REGION_RECOVERY.md)
for the full E2610 machine audit, three-int restoration and command Post ordering.
