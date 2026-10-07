# Read-only manual unit path probes

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
