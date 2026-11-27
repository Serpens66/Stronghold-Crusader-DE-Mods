# Our mods and their Vanilla touch points [Scope: Mod work, not Vanilla]

How our mods, testmods and the external components we depend on interact with the Vanilla game, ordered by game system. Nothing here is Vanilla evidence; Vanilla facts are in the [Vanilla index](../vanilla/INDEX.md). The player-facing description of each mod is its own `README.md`; native addresses each mod uses are documented in its `UpdateToNewDLL.md`.

## Per topic

| Topic | Mod knowledge |
|---|---|
| Pathfinding and movement | [pathing.md](pathing.md) |
| Fortifications and construction | [fortifications.md](fortifications.md) |
| Units and combat | [units.md](units.md) |
| Economy and workers | [economy.md](economy.md) |
| AI lords | [ai.md](ai.md) |
| Game session | [session.md](session.md) |
| Presentation and input | [presentation.md](presentation.md) |
| Network and control | [network.md](network.md) |
| Memory layouts and Script Extender interop | [memory.md](memory.md) |

## Per mod

Topics where a mod appears in the audit documents (derived from `document-scopes.json` components, state 2026-11-27).

| Mod | Purpose | Topics | Native address register |
|---|---|---|---|
| APIShared | Shared library and hook host for our mods; also owns Wildlife (Hunter gates and prey reservation) since ImprovedHunters 1.2 | all topics | [UpdateToNewDLL.md](../../../../../../APIShared/UpdateToNewDLL.md) |
| BugfixesAndQoL | Configurable Vanilla fixes and quality-of-life options | all topics | [UpdateToNewDLL.md](../../../../../../BugfixesAndQoL/UpdateToNewDLL.md) |
| Pathfinding | Faster and shared route searches, moat movement (successor of MoatMove) | [pathing](pathing.md), [units](units.md) | [UpdateToNewDLL.md](../../../../../../Pathfinding/UpdateToNewDLL.md) |
| ExtraFeatures | Gameplay, economy, building and AI options | [ai](ai.md), [economy](economy.md), [fortifications](fortifications.md), [units](units.md) | [UpdateToNewDLL.md](../../../../../../ExtraFeatures/UpdateToNewDLL.md) |
| ImprovedHunters | Hunter prey choice and Tannery uses | [economy](economy.md) | [UpdateToNewDLL.md](../../../../../../ImprovedHunters/UpdateToNewDLL.md) |
| CastlePlanner | Spawns AIV castles for human players | [ai](ai.md), [fortifications](fortifications.md), [presentation](presentation.md) | [UpdateToNewDLL.md](../../../../../../CastlePlanner/UpdateToNewDLL.md) |
| Spectator | Spectator and eliminated-player views | [network](network.md) | |
| ExtendedData | Extended Trail and AIC data | [session](session.md) | |
| RandomEvents | Random events in skirmishes | [economy](economy.md) | [UpdateToNewDLL.md](../../../../../../RandomEvents/UpdateToNewDLL.md) |
| Testmods | In-game acceptance and diagnosis (`Testmods/<Name>`) | each topic file lists its testmods | |

## External components

Script Extender, Fixes and UU-ImGUI are third-party. Their compatibility facts are in the topic files above under "External components", mainly [session](session.md) (Fixes, UU-ImGUI) and [memory](memory.md) (Script Extender hook audits). Working rules: `AGENTS/external-components.md` and `AGENTS/script-extender.md`. Script Extender update reviews live in `Shared/Tools/ScriptExtenderUpdate/`, for example the [2.14.1 to 2.14.2 source review](../../../../../../Shared/Tools/ScriptExtenderUpdate/2.14.1-2.14.2.review.md) (music-only managed change; installed extender remained 2.14.1).

## Analyses of other authors' mods

Kept in the workspace `Findings/` folder, not in the baseline: [CrusaderToolkit compatibility](../../../../../../Findings/CrusaderToolkit-Kompatibilitaet.md), [CrusaderToolkit learnings](../../../../../../Findings/CrusaderToolkit-Lernanalyse.md), [AIToolkitDE](../../../../../../Findings/AIToolkitDE.md), [Crusader DE Tweaker](../../../../../../Findings/Crusader-DE-Tweaker-Compatibility.md), [SCDE Mod Manager](../../../../../../Findings/SCDE-Mod-Manager-Compatibility-Analysis.md).
