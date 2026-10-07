# Gatehouse capture and true unit life

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Evidence: semantic SQLite function/call/xref exports, complete native handler disassembly, installed SE GameUnit metadata and installed RedBird implementation. Function role names below are probable; byte offsets and shown branch/data relationships are confirmed statically. Gameplay consequences remain unverified until an actual game test.

## Complete feature flow

- Large stone `A5320`, small stone `A57F0`, wooden `A5C80` updates reach common handler `B73D0..B7CE5` (2325 bytes). Full pristine function hash `F73E9FF6F69D9EC1ECD59D528BC6D4861739F54E0A9C59C6E6BAD91369FA57C8`.
- Native capture checks are staggered by building ID on the existing ten-tick cadence. Footprint helper `69850` supplies rotated gate tiles; the handler follows each tile's native unit chain, using signed one-based unit game IDs. Capture's `B7549` predicate tests only a nonzero unnamed 16-bit field at `GameUnit+2A0`; it does not test life/death/health.
- Owner/type scoring calls `112540`. An original-owner ally blocks hostile capture; otherwise player scores 1..8 choose the strict greatest score, retaining the earlier player on ties. The handler writes captured-player/open state and calls `C5300` for linked gate/drawbridge coupling. Original owner is not replaced. `89F40` consumes capture state for manual HUD control. Capture/path access consumers include `E2610/E2F60`. The later automatic-close branch already has a separate AliveState check and is not changed.
- Melee death writer `199110` reduces health and sets the low-word death marker while AliveState can stay IsAlive. Death animation/corpse handling in soldier dispatcher `13F540` and alternate unit handler `12B980` eventually sets MarkedForDeletion. Unit update dispatcher `182B00` then reaches `1869C0 -> 186AD0 -> 186AF0`, unlinking the tile chain and clearing the record. This explains why capture's original chain traversal can count a visible corpse until removal.
- Melee target selection `195170` and ranged/current target validation `18E9A0` directly combine AliveState IsAlive with zero death marker. Candidate helper `188A20` also excludes death-marked targets, with additional combat-specific type/team/range rules. AI order routine `122800` uses the same life combination; tribe cleanup `1261F0` removes death-marked members independently of their still-active AliveState.
- There is no established universal callable native life function. `199D50` checks a limited animation-state interval; `19B180` excludes particular unit types; `1867A0` is an attacker/victim applicability check. None is a substitute for the confirmed inline life predicate.

## Interop field map

GameUnit record size `490`; API arrays are zero-based and lookup IDs are one-based. Combat uses manager-relative unit addresses at `id*490+6E4` for AliveState and `id*490+8F8` for death. Subtracting the same validated GameUnit base gives offsets `88` and `29C`. Multiple corroborating fields: owner `92`, health `3C4`, AI state `2BC`. Capture uses module displacement `67E8CFC`, which maps to `GameUnit+2A0`; it is distinct from the death marker.

Installed SE exposes public `r_AliveState : AliveState` (signed Int16) and `N0000019A : UInt32`. Only the low 16 bits of the latter are tested natively; do not reject an unrelated nonzero upper word. APIShared.UnitAccess.IsReallyAlive mirrors this conjunction for reference and pointer views, null false. Health, animation, ownership, slot identity and applicability remain separate concerns.

## Capture filter boundary and compatibility

`B7540..B7552`: CDQE/IMUL/CMP lengths 2/7/9. `JE B75B7` remains outside. Complete handler branches and semantic xrefs include the `B75C2 -> B7540` backedge, no entry inside the span. RAX carries the extended unit ID, RDI the required slot displacement, R13 the module base, R14W the zero comparison. Both outgoing paths overwrite flags other than ZF before consuming them. All GPRs and volatile XMM0..5 must survive the managed call; TEST after wrapper cleanup drives the retained native JE.

SE scoring/capture hooks at `B757A/B790B`, Fixes farmer hook at `B7C39`, and APIShared later distance/timing/automation spans do not overlap. Canonical local Fixes source reviewed at commit `226e2e6960ad6f5992d4f5410d701161bb1cdc77`. Baseline SE provenance remains recorded in DATABASE_INFO.json; current fork/installed assembly are checked separately and not assumed equal to that older provenance.

Runtime installation is native-hash/layout/byte/backend guarded. Offline native and machine tests belong to Testmods/GatehouseLivingCaptureTest; actual gatehouse, save/load and real multiplayer acceptance require gameplay evidence and must not be inferred from successful installation.
