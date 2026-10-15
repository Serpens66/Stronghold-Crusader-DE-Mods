# IDs, Indizes, Enums und native Konstanten

**Lesen, wenn:** Code Unit-, Building-, Tribe- oder Spieler-IDs bzw. Arrayindizes verarbeitet oder fachliche Domänenwerte (Mapper, Unit-/Building-/Goods-/Chore-Typen, Flags) verwendet. Gilt zusätzlich zur Root-`AGENTS.md`.

## Unit- und Building-IDs

- Game-IDs sind 1-basiert, native Arrays und ihre .NET-Sichten 0-basiert: immer `gameId = spanIndex + 1`, `spanIndex = gameId - 1`.
- **0-basiert (Index):** `GetUnitsAsSpan`, `GetBuildingsAsSpan`, `GetUnitArray`, `GetBuildingsArray`, `SimpleNativeArray`-Indexer, `Span<T>`-Indexer; `GameStructQuery.ToIndexList`, `ForEachIndex`, `CurrentIndex`.
- **1-basiert (Game-ID):** `TryGetUnitById`, `TryGetBuildingById` inkl. `Ex`-Varianten, `IsValidId`, Managerfunktionen mit Parametern `unitId`/`buildingId`; `GameStructQuery.ToIdList`, `GameStructQuery.ForEach`, `GameStructQuery.Enumerator.CurrentId`, `GetAllUnits`, `GetAllBuildings`, `GetAllAliveUnits`, `GetAllAliveBuildings`, `GetByGlobalId`.
- ID-bewusst iterieren bevorzugt über die ID-Varianten von `GameStructQuery`. Bei Spaniteration die Variable `spanIndex` nennen und nur an der Grenze zu einer ID-API genau einmal `+ 1` rechnen; vorliegende IDs `unitId`/`buildingId`/`gameId` nennen und für Spanzugriff genau einmal `- 1` rechnen.
- Die Basis nie heuristisch aus Zahlenbereich oder Erfolg eines Lookups erraten (falsche Basis trifft meist den benachbarten gültigen Slot); immer Quelle und dokumentierten Vertrag prüfen.
- `BuildingTogglePauseEventArgs.BuildingId` ist eine 1-basierte Building-ID. `BuildingExtensions.UpdateLocalGoodsResourceVisuals` gemäß 1-basiertem Vertrag ohne Zusatzkorrektur verwenden.
- Spieler-IDs sind 1-basiert, `MissionIndex` 0-basiert.

## Tribe-Leader und Gruppenbewegung (Native-Build laut `CURRENT.json`)

- `GameTribe.r_LeaderUnitId` ist eine 1-basierte Unit-ID (roher Tribe-Manager: Record-Offset `0x5A`; strukturrelativ `0x30`, da `GameTribe` nach dem Managerpräfix beginnt). Unverändert an `TryGetUnitById` und andere Unit-ID-APIs übergeben; nur für direkten `GameUnitArray`-Zugriff einmal `- 1`.
- Auch die Struktur-/Exit-Prüfungen bei RVA `0xE9D90` und `0xE9FF0` erwarten die unveränderte 1-basierte ID: ihr scheinbar direkter Arrayzugriff ist relativ zum direkt vor `GameUnitArray` liegenden `LastOrderedUnit`-Sentinel aufgebaut (Slot 0 ist absichtlich nicht der erste Eintrag). Aus Pseudocode keine Indexbasis ableiten, ohne die native Basisadresse gegen das Managerlayout zu prüfen.
- Die gewöhnliche Gruppenbewegung ermittelt ihren Routenmodus über RVA `0x117C70` mit `(nativeTribeManager, tribeId)` und übergibt das Ergebnis als fünftes Argument an die Regionsprüfung bei RVA `0xE2610`. `E9D90/E9FF0` sind erst der nachgeschaltete Struktur-/Exit-Pfad, wenn diese modusabhängige Prüfung scheitert, und dadurch nicht die eigentliche Leiterprüfung. Beim Übertragen von Vanilla-Bewegungsverhalten den gesamten Aufruf- und Datenfluss inkl. Gruppenmodus prüfen; keine Funktionsrolle aus einem Fallback-Aufruf ableiten.

## Enums und native Konstanten

- Fachliche Mapper-, Struct-, Unit-, Building-, Goods-, Chore-, Flag- und vergleichbare Domänenwerte direkt über die Enums/Konstanten des Script Extenders referenzieren; keine harten Zahlen und keine parallelen mod-eigenen Konstanten wie `const int MapperMoat = 105`. Erlaubt: das Symbol (z. B. `eMappers.MAPPER_MOAT`) oder eine daraus abgeleitete typisierte `static readonly`-Referenz.
- Vor Nutzung Symbolname und Wert im kanonischen Extender-Quellcode **und** in der tatsächlich referenzierten Extender-Assembly prüfen. Pläne, frühere Analysen, Logs oder Pseudocode sind allein kein Beleg.
- Widersprechen sich Plan, Baseline, Extender-Symbol und Disassembly: anhalten, Widerspruch klären, betroffenen Patch bis dahin fail-closed behandeln; keinen Zahlenwert anhand seiner bisherigen Bezeichnung übernehmen.
- Zahlen bleiben zulässig, wenn sie Teil eines Maschinenvertrags sind (RVAs, Offsets, Instruktionsbytes, Bitmasken, native Protokollfelder). Repräsentiert ein Immediate zugleich einen Domänenwert, den zugehörigen Symbolnamen dokumentieren und Gleichheit möglichst statisch bzw. im Test gegen das Symbol prüfen.
- Vor jedem Build modweit nach neuen fachlichen Zahlenkonstanten und verdächtigen Immediates suchen; jede Ausnahme muss über Name, Kommentar und Test als Maschinenvertrag erkennbar sein.
