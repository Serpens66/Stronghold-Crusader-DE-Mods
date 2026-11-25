# Lifecycle-, Zeit-, Persistenz- und Kompatibilitätsaudit

Geprüft am 25.11.2026, ausschließlich offline. Dieses Dokument ergänzt die nativen Bau-, Bewegungs- und Geländeaudits. Es beschreibt bestehende Verträge und daraus folgende Integrationsbedingungen. Es installiert keine Rettungsruntime und gibt Multiplayer nicht frei.

## 1. Identität und Versionsabgleich

Der kanonische native SHA-256 ist `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; der echte Managed-Spielhash ist `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`. Beide wurden erneut berechnet und stimmen mit CURRENT/DATABASE_INFO überein. Nachweis: `.work/a/rescue-audit/001/identities.json`. Die Hashbindung gilt für die beschriebenen nativen Fakten; symbolische Decompilernamen sind keine zusätzlich bestätigten Herstellerbezeichnungen.

Die drei Extenderstände dürfen nicht gleichgesetzt werden:

| Bezug | Beobachteter Stand | Bedeutung |
|---|---|---|
| Baseline-Dataset | Commit `4014ca67c96974a5e6d697665baf5351cbfebde6`, Tree `372b43a9e9d614075c3b827d3df2a2346ec356fa` | Historische Herkunft der Extender-gebundenen Wissensschicht |
| Installierte Referenz | Manifest 2.14.4; Assembly separat gehasht | Tatsächlich verwendete Referenz des vorausgehenden Offline-Builds |
| Lokaler Quellbaum | 2.14.6, Commit `81a7a945c47c2c5dcc347cd1ad3661a54be02429`, Tree `3215dd016cdca0ae7c3a89001f1116ba4d06d0ee` | Neuester lokaler Quellstand dieses Audits; kein Installationsnachweis |

Der vollständige Dateidelta seit der Dataset-Provenienz umfasst 16 Dateien. Er liegt in den Identitäten bei. Betroffen sind Release-/Updater-/Deployment-/UI-Dateien, Archive/Modmanager, `BulkMapLoaderDetours` und `GameTribe`. Die für Bau/Bewegung/Gelände konsumierten nativen Interop- und Detourquellen wurden nicht pauschal aus dem Dataset übernommen, sondern gegen die tatsächlich gelesenen Quellen und den identischen nativen Datenfluss abgeglichen. Die einzige geänderte Tribe-Deklaration teilt das DWORD bei `+0x5CC` in zwei UInt16; folgende Offsets verschieben sich nicht. Die Rettungs-/Pathfinding-Quellen verwenden diese beiden Felder nicht. Daraus wird keine allgemeine Kompatibilität aller Extenderverbraucher abgeleitet.

`BulkMapLoaderDetours` wurde zusätzlich aus der installierten Assembly dekompiliert (`installed-maploader.cs` im Laufverzeichnis). Die installierte Fassung enthält noch die breite Exceptionbehandlung mit anschließendem Original-Fallback. Die aktuelle Quellfassung schützt dagegen mit `originalAttempted`/`originalCompleted` vor Wiederholung nach begonnenem/erfolgreichem Original und stellt `_preventUnloadingNotification` in `finally` auf den vorigen Wert zurück. Deshalb werden Fehler-/Lifecycle-Verträge im Audit versionsbezogen beschrieben. Keine ungeprüfte Gleichsetzung von aktuellem Git-Quelltext und installierter Assembly.

Die globale Semantic-Datenbank wurde nicht lediglich auf einen neuen Commit umgestempelt. Ein vollständiges `UpdateForScriptExtender` bleibt ein eigener Dataset-Pflegeschritt. Die hier erneut belegten nativen Maschinenverträge und der explizite Quelldelta-Abgleich sind davon getrennte, featurebezogene Nachweise.

## 2. Startup und Lebensdauer

Konkreter Aufrufweg: `Plugin.Awake` → statische Settings/Runtimewurzel und `CrusaderLibrary.LibraryLoaded` → `NativeRelocation.Create`/Fixes-Verträge → langlebiger AIV-Broker. APIShared-Readiness registriert Settings und `IMissionLifecycleCapability`. Spätere Arbeit hängt an diesen Publishern, nicht an Unity-Update-/Coroutine-Methoden der zerstörten Plugin-Komponente. Im Plugin gibt es keinen OnDestroy-/OnDisable-Abbau veröffentlichter Hooks.

Beleg: `src/Plugin.cs:35–96`; `APIShared/src/Missions/MissionLifecycleContracts.cs`, insbesondere `IMissionLifecycleCapability`. Die Registrierung erfolgt mit stabiler owner-lokaler ID. Sie kann einen bereits fertigen Start synchron wiederholen, bevor die Registrierung zurückkehrt. Deshalb dürfen Initialisierung und Replay nicht unkontrolliert denselben Hook oder dieselbe Mutation erneut installieren.

`settingsReady` und `libraryReady` werden getrennt veröffentlicht. Beide Reihenfolgen führen über `UpdateGeneralSession` zur Aktivierungsentscheidung. Der allgemeine Pilot ist standardmäßig aus; eine Fixture ist eine zusätzliche ausdrückliche Aktivierung. Ein Fehler hält die Ersatzfunktion inaktiv. Das ist ein Quellnachweis des Aufrufwegs, kein neuer Startup-Laufzeitnachweis.

## 3. Weltgrenze und ausstehende Arbeit

`MissionLifecycleState.Begin` installiert den neuen Versuch und invalidiert die native Uhr, bevor das Ende der alten Sitzung veröffentlicht wird. `Finish` unterscheidet Exception, nativen Misserfolg, Managed-Erfolg und verzögerten Client. `Ready` veröffentlicht nur den noch aktuellen Versuch. `End` entfernt Current/Pending und invalidiert vor Benachrichtigung. Reentrante Benachrichtigungen werden in einer FIFO abgearbeitet; einzelne Callbackexceptions sind isoliert.

Beleg: `APIShared/src/Missions/MissionLifecycleState.cs:40–160`; der Lade-Wrapper in `MissionLifecycleService.cs:580–649` unterscheidet erfolgreiche native Welt und erfolgreichen äußeren Managed-Start. Er nimmt während nativer Ladung denselben Engine-Monitor und verwirft einen überholten Versuch. Das öffentliche Startsignal allein garantiert weder ersten Renderzustand noch gestartete MP-Simulation.

Der bestehende AIV-Receipt ist single-use, vergleicht sein Kontextobjekt und seine Sitzung und verlangt `VanillaCompleted`. Er behandelt den Rückgabecode nicht als Geometriebeweis. Ein währenddessen ersetzter Versuch führt keine alte Rettung mehr aus (`GeneralRescueObserver.Completion`). Der Pilot führt seine Prüfung synchron durch und hat noch keine dauerhafte Kandidatenqueue.

Die normale AIV-Beobachterexception-Isolation besitzt eine eng begrenzte bestätigte Ausnahme: Wenn die Textformatierung der gefangenen fremden Exception erneut wirft, kann der Fehlerreporter Original oder weitere Receipts verhindern beziehungsweise eine Originalexception maskieren. [AUDIT-FINDINGS.md](AUDIT-FINDINGS.md) belegt drei Gegenbeispiele und eine normale Kontrolle am tatsächlichen Workspace-Produktionsdispatch. Die Aussage über stabile Missionszustände ist davon unabhängig; eine künftige Brokererweiterung muss auch ihren Fehlerreporter nicht werfend kapseln. Hier wurde kein Runtimefix eingebaut.

Für eine spätere Queue folgt daraus:

- Keine `GameUnit*`-/AIV-/Buildingpointer über Callbackgrenzen speichern. Slot plus globale Identität, Weltgeneration, Besitzer, Typ und relevante Zustandswerte beim Verwenden neu auflösen.
- Eine SessionId ist prozesslokal. Sie ist kein identischer Peer-Schlüssel und allein keine Erkennung einer wiederholten nativen Ladung innerhalb desselben Versuchs. Ausstehende Zustände müssen an die tatsächlich geladene Welt gebunden sein.
- Vorherdaten eines alten Bauvorgangs dürfen nach Laden/Resync nicht als Beweis über die neue Welt gelten. Native Zeiger, unvollständige Frames und offene Suppressionsscopes werden nicht restauriert.
- Save-Verwerfen versus Speichern noch gültiger Nachbeobachtungen bleibt eine Produktentscheidung D3. Blindes Verwerfen kann die spätere Erkennung bereits entstandener Fallen verlieren; es ist keine belegte vollständige Reparaturstrategie. Soll Kontinuität zugesichert werden, braucht sie einen validierten Savedatenvertrag oder einen nachgewiesenen Abschluss vor dem Save.

Das ist eine abgeschlossene Prüfung der bestehenden Lifecycle-Grenzen; die Entscheidung über eine noch nicht implementierte Queue ist keine fehlende Untersuchung des bestehenden Codes.

## 4. Zeit und Ausführungspunkt

`GameTimeManagerAPI.OnTick` liefert im geprüften Extender einen lokalen Engine-Run-Zähler. Er ist für tickabhängige MP-Rettungsentscheidungen nicht geeignet. APIShared `SimulationTickEvents` liest stattdessen denselben nativen, savebewahrten Kartenzeitwert einmal pro Veröffentlichung. `SimulationTickRegistry` verwirft doppelte/ältere Ticks derselben Sitzung und reentrante Publikationen. `SimulationClock.TryGetElapsedTicks` behandelt den signed Int32-Wrap als vorwärts gerichtete Distanz bis Int32.MaxValue.

Belege: `APIShared/src/Simulation/SimulationTickEvents.cs`, `SimulationTickSource.cs`, `SimulationTickRegistry.cs`, `SimulationClock.cs`. Native Zeitquelle und Manageradresse werden validiert. Ein Tickwert 0 ist kein Fehlerzustand. Die Uhr nimmt selbst keinen Engine-Lock und autorisiert nicht beliebige Mutationen. Die Deduplizierung des Events benutzt Sitzung und Tick; sie ersetzt keine eigene Weltgenerationsbindung gespeicherter Receipts bei wiederholter nativer Ladung innerhalb desselben Versuchs. Daraus wird ohne einen erreichbaren konkreten Fehlerpfad kein allgemeiner API-Fehler abgeleitet.

`EngineRunEvents` ist eine Beobachtungsgrenze um zugelassene Managed-Run-Aufrufe: Begin/End laufen auch bei Nullresultat/Originalexception, verschachtelte Aufrufe erhalten eigene Paare, abgelehnte Runs kein Paar. Ein End-Ereignis ist somit weder ein neuer Kartentick noch ein erfolgreich abgeschlossener Bauvorgang. Der aktuelle vorgeschaltete `WorkerProductionRunGate` hält den Engine-Monitor, prüft Generation, Zulassung und Fehlerzustand. Die öffentliche Eventdokumentation garantiert trotzdem nur den Publisherthread; Verbraucher dürfen sich nicht auf undokumentierte optionale Gatepräsenz verlassen.

APIShared besitzt außerdem eine **interne** `SimulationWorkPump`: Ausführung nach nativer Tickrückkehr vor Bufferveröffentlichung, nur unter Engine-Monitor, Run-Tiefe1 und ohne Reentranz. Sie verarbeitet den beim Drainbeginn vorhandenen Queueumfang; neue Arbeit wartet auf einen weiteren Lauf. Sie ist keine öffentliche allgemeine Rettungsschnittstelle, kein Bauabschluss und kein Uhrersatz. Für pausierte Editoroperationen ist ein nächster Simulationslauf nicht garantiert.

Folgerung: Kandidaten werden am belegten Mutationsabschluss gewonnen; spätere Nachbeobachtung wird nur bei Bedarf und nach nativer Kartenzeit budgetiert. Kein neuer per-Frame-Vollscan. Ein erfolgreicher Nachhercallback darf weder einen noch laufenden nativen Einheitenupdate-Frame noch eine Vorschaukopie irrtümlich als sicheren Rettungspunkt behandeln; dafür gelten die Bewegungsverträge.

## 5. Save und Multiplayer-Repair

Vanilla speichert/überträgt bereits eigene Unit- und Pfadrecords. Chore56 (`0x15710`) überträgt einen `0x490`-Byte-Wirerecord und 1.000 Pfadbytes. Sein Pointer beginnt vier Bytes vor der typisierten GameUnit-Sicht. Diese Formate sind keine Kopie einer modseitigen Queue oder eines alten Erreichbarkeitsbeweises. Beleg: Baseline `DESYNC_CAPTURE.md`, vollständiger vorhandener Transfer-/Checksumvertrag; Pointer-/Offsetbasen nicht vermischen.

APIShared `VersionedSaveData` liefert versionierte, begrenzte Records mit unterscheidbaren Ergebnissen für NoRecord, Restored, TooNew, Corrupt und MigrationFailed. Capture läuft auf dem Save-Publisherthread. Ein bestehendes Recordformat ist kein Beleg für sicheren nativen Zugriff während Capture. Eine neue Rettungsqueue kann darüber Daten speichern, braucht aber einen eigenen deterministischen Validierungs-/Wiederherstellungsvertrag.

MP-Repair ist keine neue Mission. APIShared `MultiplayerResyncEvents` meldet vor ausgeführtem Chore54 und nach ausgeführtem Chore67, nicht bei Größenmessung/Serialisierung. Started/Completed sind kritische Engine-serialisierte Callbackgrenzen; Exception bedeutet Weltfehler und kein stilles lokales Weiterlaufen. Native Unit-Wiederherstellung kann unterhalb der gewöhnlichen Bau-/Bewegungsevents schreiben.

Für tatsächlich notwendige verwaltete Simulationsdaten gibt es `MultiplayerResyncData`: identische Startregistrierung aller Peers, canonical private Capturebytes, reine Prepare-Validierung gegen die reparierte Welt und nicht werfender Commit unter Engine-Monitor. Alte vorreparierte Pointer sind ausdrücklich unzulässig. Daher wäre ein eigener Steam-Paket-Fallback für eine Rettungsqueue weder notwendig noch zulässig.

Die existierende Testmod-Freigabe blockiert echte MP-Sitzungen. Das ist hier korrekt und wird nicht aufgehoben. Ein künftiger MP-Pfad benötigt gleiche Einstellungen, gleiche Kandidatenreihenfolge, gleiche Quellen, deterministische Arbeitsbudgets und eine definierte Repair-/Savepolitik; native Checksumgleichheit kann erst mit echten Peers abgenommen werden. Host-only-Teleportieren im lokalen Callback ist kein fertiger Lockstepvertrag.

## 6. Fixes und andere Hookbesitzer

`FixesIntegration.Initialize` erlaubt Abwesenheit von Fixes. Bei Anwesenheit validiert sie die tatsächlich beeinflusste AIWall-Closure einschließlich Hilfsmethoden/Konstanten und stimmt Disk-MVID/geladene Methoden ab. Versionsnummern und unabhängige neue Features sind keine Sperre. Geänderte relevante IL deaktiviert dagegen die Ersatzfunktion vor ihrer Freigabe. Das ist die angemessene Updategrenze; beliebige unbekannte relevante Änderungen können nicht gleichzeitig garantiert unterstützt werden.

Der native Worker-Guard belegt `0x196342` mit 16 Bytes; Fixes beginnt direkt danach bei `0x196352` mit 14 Bytes. Der volle Movebody, direkte Innenkanten, Originalinstruktionen, echte installierte Fixes-Stubbytes und Rücksprünge werden geprüft. Die eigene veröffentlichte Spanne wird bei Missions-/Editorstart über NativePatchGuard erneut verglichen. Bei neuer Fremdänderung: Error und logische Inaktivierung; kein Zurückpatchen. Beleg: `src/FixesIntegration.cs:76–82,190–270`, `UpdateToNewDLL.md`.

Im **automatischen AIV-Piloten** bleibt die Aktivierungszelle außerhalb des eigenen synchronen Rettungsaufrufs mit Relocate/Reissue null. Weder dessen AIV-Capture noch die gesamte Mission unterdrücken Fixes. Dadurch bleibt dessen AIWall-Pre im ursprünglichen automatischen Baupfad aktiv. Davon getrennt öffnet `Fixture.Execute` (`Fixture.cs:249–261`) die Unterdrückung um die ganze kontrollierte Fixtureaction; diese kann absichtlich auch einen Bauaufruf enthalten. Diese Testisolation ist kein Vertrag des normalen Piloten. Gatehouse-Farmer und MountedKeep sind in beiden Fällen von der Unterdrückung ausgenommen. Eine neue Rettungsquelle muss ihre tatsächlich zu ersetzenden Fixes-Pfade einzeln belegen; den vorhandenen Guard nicht missionsweit aktivieren.

`NativeRelocation.Create` ist ein Call-only-Nutzer von `0x19BA40`; ein bereits vorhandener, exakt geprüfter `Lord.HomeTeleportGuard` von BugfixesAndQoL ist erlaubt. Das ist kein zweiter Detour auf derselben Funktion. Pathfinding besitzt weitere Topologie-/Builder-/Bewegungsstellen; APIShared besitzt AIV-, Assassin- und andere koordinierte Stellen. Registrierung und Live-Bytevergleich lösen verschiedene Probleme: Ein Registereintrag allein beweist keine unveränderten tatsächlichen Bytes. Kein neuer Verbraucher darf vorhandene private Implementierungen quellverlinken oder Hooks doppelt installieren.

### Fixes-Reiterrettung und CastlePlanner

Der aktuelle externe Quellstand und der zuvor geprüfte installierte 1.31.0-Vertrag enthalten weiterhin `DataScanner relocate = scanner.Scan(...)` und lesen `relocate.CurrentAddress` erst im verzögerten Hookgenerator. Der Detourmanager konstruiert danach Pathing mit demselben Scanner und committet erst anschließend. Die isolierte Cecil-Korrektur friert nur die skalare Adresse früher ein; sie ersetzt weder den Recovery-Emitter noch seine Einstellungen. Der zuvor bestandene Offline-Selbsttest akzeptiert genau diesen unterstützten Vertrag. Der aktuelle Live-CALL wurde in diesem Auftrag nicht aus einem Spielprozess gelesen.

Der Recovery-Emitter prüft Current und bei bewegtem Zustand auch Next, identifiziert das Keep und löst das zugehörige echte Campground über den Player auf. Dessen **gespeicherte Richtung** bestimmt den Ausgang: 0/default `(x+3,y+1)`, 2 `(x+1,y+3)`, 4 `(x+3,y+5)`, 6 `(x+5,y+3)`. Bounds/aktive Koordinaten und Zieloberfläche werden vor dem Relocate geprüft. Ein blockierter Ausgang wird nicht blind verwendet. Das ist Quellsemantik des fremden Emitters, keine neu bestandene Spielprüfung.

Die Richtung ist Building `+0x92` (`N00012042_2`), nicht Spritevariation `+0x102`. Der aktuelle CastlePlanner liest das korrekt (`KeepDirectionReader.cs`). Native `0x74DA0` schreibt die wirksame Richtung in Root/Childrecords, und `0xD0630` liest sie für Keepzugang unabhängig vom Sprite. Eine vom CastlePlanner gewählte Rotation muss deshalb aus dem tatsächlich erzeugten Compound gelesen werden; Fixes-Konfiguration, Kamerawinkel oder ursprünglicher AIV-Parameter sind kein Ersatz. Spätere Tests müssen diese echte Geometrie verwenden.

## 7. Auditentscheidung

Für Lifecycle, Zeit, Thread-/Publishervertrag, Save-/Repairgrenzen, versionsbezogene Fixes-Unterdrückung und Keep-Rotation ist der statische Integrationsvertrag beschrieben. Die bestehenden Offline-Tests vom vorausgehenden Auftrag bleiben ihre jeweils begrenzten Verhaltensnachweise. Hier wurden keine neuen Builds, Installationen oder Spielprozesse ausgelöst.

Nicht statisch bewiesen sind reale Startupreihenfolge unter beliebigen Fremdmods, tatsächliche Peer-Synchronität, Render-/Editorinteraktion, alle natürlichen Arbeitsfortsetzungen und Laufzeitkosten großer Karten. Diese Beobachtungen gehören zur späteren Abnahme. Offene Produktentscheidungen über Besitzerpolitik, Altsaves und Queuepersistenz werden nicht als bereits implementiert behandelt.
