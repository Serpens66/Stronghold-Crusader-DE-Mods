# Hash- und Adressprüfung: weitere Kandidaten

Diese Liste enthält Stellen, die nach einem Spiel- oder Extender-Update noch Handarbeit verursachen oder bei jedem Start unnötig Zeit kosten. Das Zielverfahren steht in [AGENTS/native-analysis.md](AGENTS/native-analysis.md#rva-signaturen-und-layouts):
- bei bekanntem Hash nur Bytevergleich an den Referenz-RVAs,
- bei unbekanntem Hash einmal Pattern- bzw. RIP-Fallback mit persistentem Cache,
- Datenadressen über validierte Code-Anker,
- Managed-Code über IL-Verträge statt Datei-Hash.

Die Liste ist eine Momentaufnahme vom 2026-10-22 (Native-Hash `FBCB9319…31E2`, Managed `Assembly-CSharp` `BC8B6A39…D789`), erstellt aus einer Codesuche. Vor einer Umstellung jedes Feature erneut prüfen und den Vanilla-Audit nach `AGENTS/native-analysis.md` abschließen.

Bereits umgestellt bzw. in Arbeit (nicht mehr hier): SavegameMissingMap, ExtendedShiftCommandQueue (Chore-Staging-Globals), Formation/FormationSpeech, APIShared-Audio und -PopularityDisplay sowie die Startscans aus Formation, FormationSpeech und NotificationQueue.

## Native Daten-RVAs ohne Fallback (BugfixesAndQoL)

Alle hängen am globalen Native-Hash (`Shared.DebugLogHelper.IsCurrentNativeLibraryVersion()` → `referenceHashMatches` in `BugfixesAndQoLRuntime.InitializeNative`). Weicht der Hash ab, bleibt das Feature aus.

| Feature | Datenadressen | Gate | Hinweis |
|---|---|---|---|
| AiRaidRetarget | `src/AiRaidRetargetFixRuntime.cs:29-37` | `BugfixesAndQoLRuntime.cs:491` | |
| AssassinPathfinding (Grid-Layer) | `src/AssassinPathfindingRuntime.cs:33-42` | `:146` | |
| AssassinCombatResume | `AssassinPathContextFlagRva 0x60AD6E8` | `AssassinCombatResumeRuntime.cs:42` | Wird schon per RIP dekodiert (`:234-256`), aber nur gegen die Konstante verglichen; naheliegender erster Kandidat. |
| UnitCommandPathRuntime | `src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:101-114` | `UnitCommandPathRuntime.Initialization.cs:43` | Großer Verbund; Gate pro Teilfeature prüfen. |
| LargeMoveTargetMarkerRenderer | `src/Formations/Markers/LargeMoveTargetMarkerRenderer.cs:27-38` | `:165` | |
| MountedStockpile | `:54-59` | `:108` | |
| GatehouseTargetMarkerHeight | `:17-19` | `GatehouseTargetMarkerHeightHook.cs:39` | |
| BuildPreview | `PreviewNativeContract.cs:15-17` | eigener Hash-Check `BuildPreviewRuntime.cs:50` | |
| NativeTannerFade | `:21-22` | `:281` | |
| WorkshopIdleDelay | `:14-15` | — | Scannt außerdem bei jedem Start. |
| AIPreplacedBuilding | `AIPreplacedBuildingFixRuntime.cs:33-43` | — | Nutzt an `:402` bereits RIP-Ableitung für ein Ziel. |
| VanillaPeaceTime | `:57-58` | `:146` | `VanillaPeaceTimeNativeContract.cs:399, 442` dekodiert schon RIP-relative Ziele mit Iced. |
| AIBuildingTemporaryAccessClassifier | `:48` | — | |
| RaidSearchObserver | `ContextRva` (`:17`) | — | |
| PathfindingGlobalsBaseline | `:101-103` | — | |
| NativeTroopCommandModeReader | `CommandModeRva 0x67E8410` (`src/UnitCommands/Cursor/GroundMovePreviewEligibility.cs:125`) | Bytevergleich an festen Code-RVAs (`:151-169`) | |
| Lebensbalken-HP-Felder | `UpdateToNewDLL.md:29-36` | — | Strukturfeld-Offsets; bleiben voraussichtlich hashgebunden. |

Weitere Einträge mit „kein Fallback“ in `BugfixesAndQoL/UpdateToNewDLL.md`: Zeilen 78, 292, 372, 628, 633, 767.

## Code-Ziele mit Zwang zur Referenz-RVA

Diese Ziele werden per Pattern gefunden, das Ergebnis muss aber der Referenz-RVA entsprechen. Damit ist der Fallback faktisch wirkungslos.
- Weitere Stellen mit `ResolveUnique(...)` und anschließendem Gleichheitsvergleich mit der Konstante. Suche nach `!= .*Rva` bzw. `resolved.Rva != `.
- Die Code-Ziele von UnitCommandPathRuntime (`NativeDetourContracts.cs`, Prefix-Tabelle `nativeDetourPrefixes`, ~40 Einträge): Bei unbekanntem Hash wird jeder Detour abgelehnt (`Unaudited command detour entry`). Prefix-Prüfung und Pattern-Fallback pro Eintrag ergänzen.

## Startscans, die bei bekanntem Hash unnötig sind

Diese Stellen scannen bei jedem Start die ganze Executable-Sektion (~70 ms je Vollscan). Auf den Referenzweg umstellen (`Shared.NativePatternResolver.ResolveUnique`):
- AivDefenderPosition
- AiDefensePatrol
- AIResourceShortageSleep
- AiWallTargeting
- ControlGroupDisband
- HealerAttack
- LordControlGroup
- WorkshopIdleDelay
- NativeMovementRecovery

## Managed-Hash-Gates in Prüfskripten

Diese Skripte prüfen den Datei-Hash der `Assembly-CSharp.dll` und brechen nach einem Spielupdate ab, auch wenn die geprüften Member unverändert sind. Durch Cecil-Member- bzw. IL-Prüfungen ersetzen:
- `BugfixesAndQoL/tests/BuildPreview.Tests/managed-contract.ps1:6`
- `BugfixesAndQoL/Test-GatehouseTargetMarkerHeight.ps1:17`
- `ExtraFeatures/verify-repair.ps1:16`
- `Shared/Tools/Release/Test-SCDEModManagerCompatibility.ps1:101`
- `Testmods/PerfFixTest/verify.ps1:9`, `Testmods/PerfProbe/verify.ps1:9` (nur Warnungen)

## Weitere Hash-Gates in APIShared

- `APIShared/src/ScriptExtenderFixes/ScriptExtenderFixesRuntime.cs:20`: `ManagedHash = "D98415…"` mit `Supports(...)` (`:141-143`). Betrifft eine andere Managed-Assembly als Assembly-CSharp; Zuordnung klären und auf IL-Vertrag prüfen.
- Native APIShared-Dienste mit festen RVAs, etwa die Konstanten in `AssassinPathAPI` (`BuildingGridRva`, `TileFlagsRva` usw.) und `EnemyGatePathfindingNativeDefinition`. Die Werte sind zum Teil jetzt öffentlich (`GateRoutingNativeLayout`); dieselbe Cache- und RIP-Strategie wäre hier anwendbar.

## Andere Mods

Bisher nicht inventarisiert: ExtraFeatures, ExtendedData, CastlePlanner, RandomEvents, StartConditions, UnitCosts, UnitLimit, BuildingCosts, BuildingLimit. Dort nach `Rva`, `ReferenceSha`, `NativeHash` und `Assembly-CSharp`-Hashes suchen und die Fundstellen hier ergänzen.
