# AIV lobby selection audit

Installed `CrusaderDE.dll` SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Image base: `0x180000000`; VA is base plus RVA. The entry and data path below
was reviewed against the current semantic decompiler export.

| Native role | RVA / VA | Confidence |
| --- | --- | --- |
| Skirmish player setup and AIV selector source | `0x94350` / `0x180094350` | High for branching; medium for managed lobby mapping |
| Candidate import and mapper write | `0x55320` / `0x180055320` | High for call order; medium for all field semantics |
| Fixed candidate fit | `0x54DE0` / `0x180054DE0` | High |
| Auto candidate and rotation choice | `0x54F60` / `0x180054F60` | High for control flow and thresholds |
| Mapper rotation | `0x56670` / `0x180056670` | High |
| Projected raster scan | `0x57080` / `0x180057080` | High for scan and score flow |
| Tile placement rules | `0x7B060` / `0x18007B060` | Medium for mapper-specific effects |
| Prepare chosen castle | `0x53D00` / `0x180053D00` | High for call order; medium for effects |
| Execute completed castle | `0x55F50` / `0x180055F50` | High for frame sequence |
| Execute one build step | `0x51790` / `0x180051790` | Medium for all constructors |

`0x94350` scans player IDs in order, resolves a per-player selector, starts
native AIV state, and routes negative selectors to `0x54F60`. Selector `-1`
passes an enabled alternative-rotation flag; `-2` and lower pass disabled.
The function receives an `AivSystem*`, zero-based village slot, and one-byte
flag. Its candidate count comes from the imported specification. The initial
orientation is stored in the village slot and rotated by `0x56670`; subsequent
orientations advance by two in the native eight-step direction field, wrapping
at eight. The caller later reads the chosen variant index, orientation and
placement state. Complete results prepare the layout and, with completed
enemy castles enabled, execute build steps into the mutable map before the
next player. The offline AIV plan alone cannot reconstruct that later state.

`0x54F60` starts at RNG modulo candidate count and increments before the first
visit. It imports and checks every candidate at the initial rotation. The
first full score `999999` returns immediately with state 2. Partial scores
track the first visited fit above 95%, the best sequential score, and the best
percentage. All ties use strict `>` and retain the earlier visited candidate.
If the flag permits, it checks three more rotations in rotation-major order.
Any full fit returns immediately. A partial alternative is considered only if
there was no positive initial score. With no positive initial score it accepts
an alternative at 86% or higher; otherwise no candidate is selected. With a
positive initial score it selects the first above 95%, else the highest
sequential score when greater than 29, else the highest percentage when at
least 91%, else the highest sequential score. Partial selection has state 1.

The installed Script Extender exposes imported AIV and live build-state views,
but no offline query against an unloaded lobby map. `AivSystem` must remain
pointer-based. The local Fixes mod changes some AIV capacities and KEEP3
handling, so it remains part of runtime compatibility review. The owner-aware
reconstruction of tightly neighboring starts is a model assumption supported
by the archived owner-marked wall discrepancy, not yet by a current-hash
runtime capture. The rechecked archive matches 4/145 cases after the owner
fix; unresolved dense-map states are blocked from later-AI lobby predictions
when a rebuilt start has cross-owner wall adjacency. Serialized walls with
owner zero still require the previous
adjacency-based removal rule; the archived first-player full-fit case would
otherwise become a false partial fit. A current-hash Oracle trace should
resolve this uncertainty. See `Helpers/MapParser/Docs/AIV_PLACEMENT_RULES.md` for the
existing fit and corpus details.
