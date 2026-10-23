# Hash- und Adressprüfung: weitere Kandidaten

Diese Liste enthält Stellen, die nach einem Spiel- oder Extender-Update noch Handarbeit verursachen oder bei jedem Start unnötig Zeit kosten. Das Zielverfahren steht in [AGENTS/native-analysis.md](AGENTS/native-analysis.md#rva-signaturen-und-layouts):
- bei bekanntem Hash nur Bytevergleich an den Referenz-RVAs,
- bei unbekanntem Hash einmal Pattern- bzw. RIP-Fallback mit persistentem Cache,
- Datenadressen über validierte Code-Anker,
- Managed-Code über IL-Verträge statt Datei-Hash.

Die Liste ist eine Momentaufnahme vom 2026-10-22 (Native-Hash `FBCB9319…31E2`, Managed `Assembly-CSharp` `BC8B6A39…D789`), erstellt aus einer Codesuche. Vor einer Umstellung jedes Feature erneut prüfen und den Vanilla-Audit nach `AGENTS/native-analysis.md` abschließen.

Bereits umgestellt (nicht mehr hier):
- SavegameMissingMap und der Managed-Teil von APIShared-Audio
- die Startscans von Formation, NotificationQueue und den neun Stellen aus dem früheren Abschnitt „Startscans“

Die Details stehen in `BugfixesAndQoL/UpdateToNewDLL.md`, Abschnitt „Update fallback, startup checks and remaining hash bindings“.

## Voraussetzung: gemeinsame Befehls-Runtime (BugfixesAndQoL)

`UnitCommandPathRuntime` verlangt den Referenz-Hash. Es bindet etwa 40 feste Detour-Prefixe (`src/UnitCommands/Native/NativeDetourContracts.cs`, `nativeDetourPrefixes`) und rund 100 native Konstanten (`src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs`).

Davon hängen ab:
- ExtendedShiftCommandQueue
- Formation einschließlich FormationSpeech
- der Großteil der Befehlsfeatures

Solange diese Runtime keinen Fallback hat, bringt eine Umstellung dieser Features nach einem Spielupdate nichts.

Für ChoreQueue und Formation/Speech ist der Audit schon erledigt: Anker mit Zweitbelegen für die Chore-Staging-Globals, Manager, Strides und Speech-Call-Ziele stehen in `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MOVE_STAGER_DATA_ANCHORS.md`. Noch ohne Anker sind `TribeUnitCountOffset 0x5C` und `MovementTargetAvailability 0x3A11EA4`.

Reihenfolge:
1. Die Befehls-Runtime auf Resolver und Cache umstellen.
2. Danach ChoreQueue und Formation/Speech nach diesem Audit.

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

## Startscans in anderen Mods

In BugfixesAndQoL ist das erledigt. In anderen Mods noch nach `FindUniquePattern(` ohne vorherigen Referenz-Bytevergleich suchen.

## Managed-Hash-Gates in Prüfskripten

Diese Skripte prüfen den Datei-Hash der `Assembly-CSharp.dll` und brechen nach einem Spielupdate ab, auch wenn die geprüften Member unverändert sind. Durch Cecil-Member- bzw. IL-Prüfungen ersetzen:
- `BugfixesAndQoL/tests/BuildPreview.Tests/managed-contract.ps1:6`
- `BugfixesAndQoL/Test-GatehouseTargetMarkerHeight.ps1:17`
- `ExtraFeatures/verify-repair.ps1:16`
- `Shared/Tools/Release/Test-SCDEModManagerCompatibility.ps1:101`
- `Testmods/PerfProbe/verify.ps1:9` (nur Warnung)

## Weitere Hash-Gates in APIShared

- `APIShared/src/ScriptExtenderFixes/Popularity/PopularityDisplayRuntime.cs:22`: prüft weiter den Managed-Hash `BC8B6A39…`. Es gibt dort keinen IL-Vertrag. Der Hash sichert ab, welche Vanilla-GUI-Felder je `app_sub_mode` geschrieben werden (`PopularityPresentation.Apply`). Das Feature ist zusätzlich an den Native-Hash gebunden. Für Update-Festigkeit bräuchte es einen IL-Vertrag über die schreibende Vanilla-GUI-Methode.
- Native Teile von APIShared-Audio (`Audio/NativeContract.cs`: `NativeHash`, `MeleeFrameHash`, feste RVAs): Der Managed-Teil ist auf den IL-Vertrag umgestellt, der native Teil bleibt hashgebunden.
- KI-Verkauf (`APIShared/src/Economy/AiMarketSellNativeRuntime.cs`, `AiMarketSellAssembly.cs`):
  - Bleibt hashgebunden: `NativeHash`, die zwei Hookstellen 0x3F1F3/0x29723, das Epilog-Ziel 0x2978B und der Struktur- und Bridge-Stackvertrag (`PlayerStride`, Goods-Offsets).
  - Keine Datei-Hashes auf RedBird/Iced mehr: Spanne, erzeugter Stub und Sprungform werden zur Laufzeit geprüft.
  - Grenzen werden am unveränderten Ladeabbild geprüft.
  - Für Update-Festigkeit müssten die Hookstellen per Pattern plus Cache gefunden und der Strukturvertrag neu abgeleitet werden.

- `APIShared/src/ScriptExtenderFixes/ScriptExtenderFixesRuntime.cs:20`: `ManagedHash = "D98415…"` mit `Supports(...)` (`:141-143`). Betrifft eine andere Managed-Assembly als Assembly-CSharp; Zuordnung klären und auf IL-Vertrag prüfen.
- Native APIShared-Dienste mit festen RVAs, etwa die Konstanten in `AssassinPathAPI` (`BuildingGridRva`, `TileFlagsRva` usw.) und `EnemyGatePathfindingNativeDefinition`. Die Werte sind zum Teil jetzt öffentlich (`GateRoutingNativeLayout`); dieselbe Cache- und RIP-Strategie wäre hier anwendbar.

## Andere Mods

Bisher nicht inventarisiert: ExtraFeatures, ExtendedData, CastlePlanner, RandomEvents, StartConditions, UnitCosts, UnitLimit, BuildingCosts, BuildingLimit. Dort nach `Rva`, `ReferenceSha`, `NativeHash` und `Assembly-CSharp`-Hashes suchen und die Fundstellen hier ergänzen.
