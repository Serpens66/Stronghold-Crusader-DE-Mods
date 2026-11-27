# Vanilla knowledge index [Scope: Vanilla]

What we know about Vanilla Stronghold Crusader Definitive Edition (native `CrusaderDE.dll` FBCB9319 and managed `Assembly-CSharp.dll`), ordered by game system. Every topic file is a current-state summary. Its statements link to the audit documents in the parent directory, which stay the authoritative evidence. Mod behavior is not described here; see [our mods](../mods/INDEX.md).

Before using any address: check the installed DLL hash against [CURRENT.json](../../../../CURRENT.json) as described in [CURRENT.md](../../../../CURRENT.md).

## Reading order

1. Pick the topic below. Search it for your function, field or feature, or read it completely (40-95 KB each; dense tables first, behavior sections after).
2. Follow its evidence links into the audit documents for details, exact instruction proofs and dated observations.
3. For a function, caller, string or field not covered here, use the query tool (below). A topic file that does not mention something is not proof that nothing is known.

## Topics

| Topic | Covers | Typical questions |
|---|---|---|
| [Pathfinding and movement](pathing.md) | route builders, region/portal tables, moat traversal, group moves, formations, Assassin climbing and search cost, gate and bridge routing | Which function builds a ground route? How are portals numbered? Why do large AI groups stall a tick? |
| [Fortifications and construction](fortifications.md) | gatehouses, drawbridges, walls, crenels, Keep, pitch, stairs, moats, demolition, repair, placement, cost tables, building pause/sleep | Where does Vanilla measure a gatehouse from? Which mask blocks walls on deposits? |
| [Units and combat](units.md) | unit records and states, line of sight, HP, siege engines and crews, healers, firemen, fire, plague, recruits, Lord return, voices and sounds | What does AliveState 4 mean? Which eye height does a ranged order use? |
| [Economy and workers](economy.md) | worker cycles and production, hunters, woodcutters and trees, workshops, markets, gold delivery, popularity, goods transfers, defeat rewards | When does a Tanner publish output? How is incoming gold debited? |
| [AI lords](ai.md) | AI strategy, economy, markets, sell thresholds, AIV castle building and lobby selection, military groups, sieges, raids, outposts, drawbridge decisions | How does the AI pick a raid target? When does it start a siege? |
| [Game session](session.md) | mission and editor lifecycle, saves, lobby, Coop and Trails, Lord slots and media, modsettings activation, peace time, defeat and surrender | Which route writes a save? Which flags does peace time gate? |
| [Presentation and input](presentation.md) | HUD and side HUD, health bars, minimap, markers, player colors and skins, Keep flag, notifications, cursor dispatcher, move command release | Which pass draws the minimap? How is a move command released? |
| [Network and control](network.md) | Chore system and opcodes, checksums, desync capture, player-control context, GameAction classes, spectator views | What does Chore opcode N do? Which inputs enter the checksum? |
| [Memory layouts](memory.md) | manager globals, record strides and indexing, field catalog, Script Extender interop and hook spans | Where is the unit manager? What is the building record stride? |

Every audit document belongs to exactly one topic; the assignment is [topics.json](../topics.json). Scope per document (Vanilla, Vanilla + mod integration, mod, external) is in [document-scopes.json](../document-scopes.json).

## Query tool

From the workspace root (needs the local SQLite database; restore with `Build-SemanticBaseline.ps1 RestoreDatabase` after a fresh clone):

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function <Name|VA|RVA>
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' search <Text>
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' callers <Function>
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' callees <Function>
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' managed <Method>
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' chore opcode <n>

More queries (flow, trace, slice, memory, type, hookcheck) are described in [ANALYSIS.md](../../../../tools/semantic/ANALYSIS.md).

## Confidence vocabulary

`confirmed` means exports, disassembly or unambiguous evidence. `probable` means a strong assignment. `candidate` means a search hint only. The evidence kind follows: `static`, `disassembly`, `runtime`, `fixture`, `in-game`, `user-confirmed`. Pattern hits, call-graph reachability, string proximity and pseudocode alone never prove runtime behavior.

## Adding knowledge

1. Record new evidence in the audit document of the topic (or a new audit document registered in `topics.json`, `document-scopes.json` and here). Append only; line numbers are referenced by the field catalog and analysis dossiers.
2. In the same work step, update the topic file here with the resulting current-state fact and its evidence link. Correct superseded statements instead of adding a second version.
3. Mod design, test runs and acceptance results go to [mods/](../mods/INDEX.md), not into Vanilla topic files.
4. Run `python -I _inspect\CrusaderDE-Native-Baseline\tools\semantic\knowledge_lint.py` (links, topic assignment, mojibake, CRLF).
