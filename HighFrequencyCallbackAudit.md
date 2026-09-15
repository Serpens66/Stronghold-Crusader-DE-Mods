# Offene Optimierungen häufiger Mod-Callbacks

Stand: 2026-09-15

## Zweck und Prüfgrundlage

Diese Datei enthält ausschließlich Callbackpfade, für die der aktuelle Code noch eine konkrete, funktionswahrende Optimierung zulässt. Bereits bestmöglich ausgeführte, fachlich unvermeidbare oder inzwischen erledigte Punkte werden nicht aufgeführt.

Geprüft wurden die produktiven Runtime- und eingebundenen Shared-Quellen der 13 Projekte aus `Shared/Release/release-projects.json`. Testmods, Helpers, Diagnoseprojekte, Backups, `bin`, `obj` und installierte Ausgaben wurden nicht als produktiver Optimierungsumfang gewertet.

Die installierte `CrusaderDE.dll` stimmt mit `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` überein:

- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Script Extender: `v2.6.0`
- lokaler Script-Extender-Commit: `2cee24e33b5a5d81d1c275efabc714ac59917b7b`

## Priorität A – lokale Optimierungen ohne neue gemeinsame API

### CastlePlanner: Free-Castle-Preview nur bei Bedarf pollen

`CastlePlanner/src/FreeCastlePreviewRuntime.cs` registriert `Application.onBeforeRender` weiterhin dauerhaft in `Initialize()`, obwohl die Arbeit nur in ausstehenden oder aktiven Previewzuständen benötigt wird.

Empfehlung:

- den Callback erst beim Eintritt in `pending` oder `active` registrieren;
- ihn bei Erfolg, Abbruch, Lobbyverlassen, Mapstart, Fehler und Runtimeende über eine zentrale idempotente Zustandsmethode abmelden;
- die bestehende Framefrequenz während eines aktiven Previews unverändert lassen;
- in den Zustandsübergangstests doppelte Registrierung und fehlende Abmeldung ausschließen.

### CastlePlanner: Deferred-Compound-Queue

`CastlePlanner/src/CastlePlannerRuntime.cs` erzeugt bei nichtleerer Queue mit `deferredCompoundBuildings.Keys.ToArray()` auf jedem Verarbeitungstick ein neues Array.

Empfehlung:

- einen wiederverwendbaren Schlüsselbuffer verwenden;
- den ersten möglichen Ausführungstick und die bestehende Platzierungsreihenfolge unverändert halten.

### BugfixesAndQoL: Extended-Shift-Tickleerlauf und Arbeitscontainer

`BugfixesAndQoL/src/ExtendedShiftCommandQueueRuntime.cs` besitzt bereits mehrere wiederverwendbare Puffer, erzeugt in aktiven Arbeitsphasen aber weiterhin temporäre Listen, Sets, Dictionaries und `Members.ToArray()`-Kopien. Der Tickpfad führt außerdem Prune/Reconcile/Coalesce aus, bevor vollständig leerer Zustand festgestellt wird.

Empfehlung:

- `currentTick` zuerst aktualisieren und danach bei leeren Cohorts, erwarteten Chore-/Eventsignalen und sonstigen Pendingzuständen sofort zurückkehren;
- verbleibende Member-, Branch-, Split- und Reconciliation-Container als instanzgebundene Puffer wiederverwenden;
- keine Frequenzdrosselung einführen: Commandtiming, Ausführungstick und Multiplayer-Chore-Reihenfolge bleiben unverändert;
- Reentrancy ausschließen oder verschachtelte Arbeit explizit mit getrennten Puffern absichern.

### BugfixesAndQoL: Plague-Popularity-Tick bedarfsgebunden aktivieren

`BugfixesAndQoL/src/PlaguePopularityFix.cs` bleibt während der Map dauerhaft am Tick und kehrt im Leerlauf erst im Callback zurück.

Empfehlung:

- Tick nur während Mapinitialisierung, bei nichtleerem Herd-/Projectilezustand oder bei ausstehender Callbackdiagnose aktivieren;
- Projectile-Spawn/Delete sowie Session-/Mapereignisse als Aktivierungssignale verwenden;
- bei leerem Zustand und abgeschlossener Diagnose wieder abmelden;
- die Reconciliation beibehalten, weil natürliche Expiry nicht zuverlässig durch ein Deleteevent gemeldet wird.

### BugfixesAndQoL: Quarry-Tick und Key-Puffer

`BugfixesAndQoL/src/QuarryPileRelocationRuntime.cs` setzt `tickSubscribed` bereits bei Initialisierung dauerhaft. Der erste Maptick armiert die AI-Beobachtung; spätere Leerticks kehren früh zurück. Bei Pendingarbeit wird dennoch jedes Mal eine neue Liste der Quarry-IDs erzeugt.

Empfehlung:

- nur für den ersten Maptick und bei nichtleerem `pendingAIQuarriesByGlobalId` abonnieren;
- Building-Spawn und Netzwerkpakete als Reaktivierungssignale verwenden;
- eine wiederverwendbare ID-Liste statt `new List<int>(Keys)` einsetzen;
- Timeout, erste mögliche Relocation und Host-/Client-Reihenfolge unverändert testen.

### ExtraFeatures: Lord-Health-Scans reduzieren

`ExtraFeatures/src/LordHealthRuntime.cs` scannt während der gesamten aktiven Map alle zehn Ticks Spieler 1 bis 8. `appliedLordGlobalIds` verhindert nur die erneute Anwendung auf bereits bekannte Lords, nicht den weiteren Scan.

Empfehlung:

- `OnUnitCreate(Post)` als Dirty-Signal verwenden;
- weiterhin einen 10-Tick-Fallback behalten, weil die Player-Lord-ID möglicherweise erst nach dem Create-Callback gesetzt wird;
- im Fallback ausschließlich ungelöste Spieler prüfen und nach vollständiger Anwendung bis zum nächsten Mapzustand keine weiteren Scans durchführen.

### ExtraFeatures: AI-Marktpreis-Hotpath mit Snapshotflags

`ExtraFeatures/src/AIMarketVanillaPriceHook.cs` liest in jedem Buy-/Sell-Callback den Modstatus, `MarketPricesAlsoForAI` und über `GamePlayerManagerAPI` den AI-Spielerstatus. Diese Callbacks sind burstfähig und enthalten zusätzlich einen Breadcrumb-Scope.

Empfehlung:

- Modaktivierung und Setting als atomare Snapshotflags außerhalb der Preiscallbacks pflegen;
- die AI-Spielerklassifikation für die acht Spieler bei Sessionstart und relevanten Spieler-/Lobbyänderungen in einem kleinen Snapshot aktualisieren;
- im Callback zuerst Pointer-, Player-, Goods- und Snapshotguards prüfen und erst danach den Preis berechnen;
- Breadcrumbkosten separat messen und nur dann reduzieren, wenn die Crashdiagnose weiterhin ihren vertraglichen Anfangs-/Endzustand erhält;
- Vanilla in jedem nicht übernommenen oder fehlerhaften Pfad genau einmal aufrufen.

### UnitCosts und UnitLimit: nur konfigurierte Rekrutierungsbuttons prüfen

`UnitCosts/src/UnitCostsRuntime.cs` und `UnitLimit/src/UnitLimitRuntime.RecruitmentAvailability.cs` rufen in jedem Noesis-UI-Durchlauf die Prüfung für sämtliche Rekrutierungsbuttons auf. Erst im jeweiligen Helper wird festgestellt, ob für den Einheitentyp überhaupt eine aktive Kosten- oder Limitregel existiert.

Empfehlung:

- beim Anwenden der Einstellungen eine kompakte Liste aus konfiguriertem Einheitentyp und zugehörigem Buttonzugriff vorberechnen;
- im UI-Callback ausschließlich diese Einträge iterieren;
- die Liste nur bei Einstellungs- oder Panelwechsel neu bilden;
- beim Entfernen einer Regel den betroffenen Button einmal explizit auf den Vanilla-Zustand zurücksetzen;
- identisches Buttonverhalten bei Panelwechsel, deaktiviertem Mod und dynamisch geänderten Einstellungen testen.

### APIShared: Unit-HUD-Frameallokationen reduzieren

`APIShared/src/UnitHudPresentationCapability.cs` ist bereits der richtige prozessweite Besitzer. Bei aktiven Kategorien erzeugen `CategoryCopy()`, `TryCaptureSelectedUnits`, LINQ-Gruppierungen und mehrere Snapshotpfade jedoch Arrays, Listen und Dictionaries im Framepfad.

Empfehlung:

- intern wiederverwendbare Auswahl-, Kategorie-, Gruppierungs- und Anzeigecontainer einsetzen;
- sortierte Registrierungsansichten nur bei Registrierung/Abmeldung neu erzeugen und danach immutable intern publizieren;
- unveränderte Auswahl-, Sprite-, Recruitment- und Panelzustände vor dem Aufbau öffentlicher Snapshots erkennen;
- öffentliche immutable Rückgaben beibehalten und niemals wiederverwendbare interne Container an Consumer herausgeben;
- mehrere Consumer, Callbackfehler und Reentrancy explizit testen.

### Friendly-Moat: verbleibende aktive Arbeitsallokationen

Die Leerlauf- und Diagnoseguards in `BugfixesAndQoL/src/FriendlyMoatMovementRuntime.cs` sind bereits vorhanden. In aktiven Building-/Pathfinding-Fallbacks entstehen aber weiterhin temporäre Listen, Dictionaries und Sets, unter anderem für Kandidaten, Regionen und Seen-Zustände.

Empfehlung:

- nur containerartige Arbeitsdaten mit eindeutig begrenzter Callbacklebensdauer poolen oder instanzgebunden wiederverwenden;
- vor jeder Wiederverwendung vollständiges Clear und Reentrancy-Schutz sicherstellen;
- keine nativen Scope-, Route-, Candidate- oder Fallbackentscheidungen cachen, deren Gültigkeit nur für den aktuellen Command gilt;
- Allokationen und Laufzeit getrennt für deaktivierten Pfad, Vanilla-Erfolg und tatsächlichen Fallback messen.

## Priorität B – gemeinsame Prozessbeobachtung (erledigt)

Der ursprüngliche Befund war zu hoch angesetzt: Obwohl die Shared-Datei in mehr Projekte eingebunden ist, aktivieren nur `BugfixesAndQoL`, `CastlePlanner` und `CustomCustomTrail` wegen ihrer `[SyncPerPlayer]`-Settings den Lobby-Poller. Der Ausgangszustand waren daher höchstens drei, nicht zehn Poller.

APIShared besitzt nun den einzigen prozessweiten Lobby-Observer. Er veröffentlicht immutable, wertverglichene Snapshots an die drei Consumer, verarbeitet Vanilla-Writer, Leave und Mapereignisse als Dirty-Signale und behält den 15-Renderframe-Fallback. Die modlokalen Coordinators behalten Publish-, Readiness-, Host-/Client- und Slot-Remapping-Logik. Alle drei Consumer verwenden eine harte APIShared-Abhängigkeit ohne lokalen Polling-Fallback.

## Reihenfolge der Umsetzung

1. Lokale Puffer und frühe Guards ohne Abonnementänderung umsetzen und mit Allokations-/Callzählern prüfen.
2. Bedarfsabhängige Tick-/Frameabonnements je Feature einzeln migrieren und alle Terminalpfade testen.
3. APIShared Unit-HUD intern optimieren, ohne die öffentliche Snapshotsemantik zu ändern.
4. Lobby-Observer als eigenständige Capability umgesetzt; die drei tatsächlichen Consumer sind migriert.

## Abnahme

- Für jeden Punkt einen Vorher-/Nachher-Callzähler und, wo relevant, Managed-Allokationen im Leerlauf und aktiven Pfad erfassen.
- Mapstart, Mapende, Save-Reload, Pause, Editor, Fokuswechsel, Lobbywechsel sowie Host und Client prüfen.
- Bei dynamischen Abonnements jeden Erfolgs-, Abbruch-, Timeout-, Fehler- und erneuten Initialisierungspfad auf genau eine Registrierung beziehungsweise vollständige Abmeldung testen.
- Für Queue-, Pathfinding-, Chore-, Packet- und UI-Pfade identischen Ausführungstick, identische Reihenfolge und identische finale Werte nachweisen.
- Vor Runtime-Builds die vorgeschriebenen JSON-/Lifecycle-/CRLF-Prüfungen durchführen und erst danach den jeweiligen `build.bat /nopause` einmal ausführen.
