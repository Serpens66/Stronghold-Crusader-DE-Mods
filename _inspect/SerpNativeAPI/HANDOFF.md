# Übergabe: gemeinsame SerpNativeAPI für native Patches und Hooks

## Auftrag und Zielbild

In mehreren Workspace-Mods werden derzeit direkt RVAs, AOB-Pattern, relative Zielauflösungen, `VirtualProtect`-Schreibvorgänge und `NativeDetour`-Instanzen verwendet. Diese native Infrastruktur soll schrittweise in eine gemeinsame BepInEx-Mod `SerpNativeAPI` überführt werden.

Die API ergänzt den vorhandenen Script Extender. Sie ist weder ein Fork noch ein Ersatz dafür und der Ordner `shcde-script-extender` darf nicht verändert oder mit Buildartefakten beschrieben werden. V1 richtet sich zunächst an die eigenen Workspace-Mods. Die Architektur soll eine spätere öffentliche Nutzung ermöglichen, garantiert vor Version 1.0 aber noch keine langfristig stabile Drittanbieter-ABI.

Das Ziel der ersten Umsetzung ist ein vollständiger, getesteter API-Kern mit Konfliktmodell sowie zwei produktionsnah migrierte Pilotfunktionen:

1. Gatehouse-Timing-Datenpatch aus `ExtraFeatures`.
2. Selected-Unit-Command-Detour für die Assassin-Kletterabbruchlogik aus `BugfixesAndQoL`.

Die fachliche Modlogik, Settings, Multiplayer-Klassifikation und UI bleiben in den Verbraucher-Mods. Die API übernimmt ausschließlich sichere native Mechanik, Zielidentifikation, Kompatibilitätsprüfung, Patchbesitz und Hook-Vermittlung.

## Relevanter Istzustand

### Native Analysebasis

- Kanonische Spiel-DLL: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll`
- Beim bisherigen Scan erwarteter SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Vor jeder Implementierung oder Validierung den aktuellen kanonischen Hash erneut prüfen. Bei Abweichung nicht stillschweigend mit den alten RVAs arbeiten.
- Historische Workspace-DLL: `x86_64\CrusaderDE.dll`; sie ist nur Vergleichsmaterial und muss stets mit ihrem Hash gekennzeichnet werden.
- Native Analyse und semantischer Index: `_inspect\CrusaderDE-Native-Baseline`.
- Einstieg für spätere Analysen: `_inspect\CrusaderDE-Native-Baseline\CURRENT.md`.
- Die große semantische SQLite-Datei ist ein lokaler, reproduzierbarer und ignorierter Index. Sie ist keine Runtime-Abhängigkeit der API.
- Bekannter Script-Extender-Stand der semantischen Analyse: Commit `171d68e155a8f98c5f8c4ee154d9af154c9a2443`.

### Vorhandene gemeinsame Infrastruktur

- `Shared\NativePatternResolver.cs` löst bekannte Referenz-RVAs mit Bytevalidierung auf und verwendet andernfalls eine eindeutige AOB-Suche.
- Es unterstützt ausführbare PE-Bereiche, relative Ziele und Fehler bei keinem beziehungsweise mehreren Treffern.
- Mindestens zwölf Produktions-/Diagnoseprojekte linken diese Datei derzeit als Source-Link ein.
- Eine Bestandsaufnahme ergab ungefähr 313 benannte `*Rva`-Konstanten in 13 Projekten und 24 exakte RVAs, die von mehreren Mods verwendet werden.
- Besonders viele native Ziele befinden sich unter anderem in `ImprovedHunters`, `BugfixesAndQoL`, `EnemyGatePathfindingTest`, `ExtremePowers`, `AssassinCombatFix`, `MoveMoatTest`, `RandomEvents` und `ExtraFeatures`.
- Wiederkehrende Probleme sind pro Mod duplizierte DLL-Hashprüfungen, Zielkataloge, relative Zielvalidierungen, Speicherschreiblogik und separat installierte Detours.

Die erste Umsetzung migriert ausdrücklich nicht alle vorhandenen Stellen. Sie schafft einen belastbaren Kern und überführt nur die beiden Piloten. Andere Verwendungen von `Shared.NativePatternResolver` bleiben vorerst bestehen.

### Pilot 1: Gatehouse-Timing

Maßgebliche Datei: `ExtraFeatures\src\GatehouseTimingPatch.cs`.

Der bestehende Patch:

- verwendet den Entscheidungsblock bei Referenz-RVA `0xB7BBB`;
- verwendet das Human-Delay-Pattern bei Referenz-RVA `0xB7C32`;
- leitet vier 32-Bit-Immediate-Adressen über Offsets `8`, `15`, `24` und `3` ab;
- erwartet Vanilla-Werte AI-Distanz `200`, AI-Delay `1200`, Human-Distanz `140`, Human-Delay `100`;
- verwendet `40` Ticks pro Referenzsekunde und `8` native Einheiten pro Feld;
- schreibt die vier Werte gemeinsam, prüft vorher den erwarteten Zustand, setzt Speicherschutz und leert den Instruction Cache;
- stellt bei `Dispose()` Vanilla-Werte wieder her.

Die öffentliche Capability soll fachliche Werte in Sekunden und Feldern entgegennehmen. Konvertierung, native Adressen, erwartete Werte und Transaktion gehören in die API. `ExtraFeatures` behält Wertebereiche, Settings und Aktivierungsentscheidungen. Für diese Funktion darf nach der Migration kein versteckter lokaler RVA-/Pattern-Fallback parallel bestehen.

Wichtig für den Lebenszyklus: Das BepInEx-Plugin-GameObject wird beim Spielstart früh zerstört. Deshalb darf `OnDestroy()` weder den API-Patch zurücksetzen noch die native Infrastruktur entsorgen. Ein explizites Deaktivieren der Modoption darf weiterhin Vanilla-Werte setzen, ohne den zentralen Besitz der Capability während des Prozesses freizugeben.

### Pilot 2: Selected-Unit-Command / Assassin

Maßgebliche Datei: `BugfixesAndQoL\src\AssassinClimbCancellationRuntime.cs`.

Der bestehende Hook:

- Ziel-RVA `0x199C70`;
- erwartete Implementierung `0x11E960`;
- erwarteter Tribe-/Selection-Manager `0x7CC6720`;
- Pattern `48 8D 0D A9 CA B2 07 E9 E4 4C F8 FF`;
- Signatur: `int (IntPtr unitManager, int tribeId, int command, int argument1, int argument2, int argument3)` mit `CallingConvention.Winapi`;
- validiert beide relativen Ziele;
- installiert derzeit selbst einen `NativeDetour` und ruft nach der mod-eigenen Vorverarbeitung Vanilla auf.

Nach der Migration besitzt ausschließlich die API den echten Detour. `BugfixesAndQoL` registriert einen typisierten Before-Callback. Die bestehende Fachlogik zum Erkennen und Abschließen kletternder Assassinen bleibt vollständig im Mod. Die API garantiert, dass Vanilla genau einmal ausgeführt wird.

## Festgelegte Architektur

### Projekt und Abhängigkeiten

- Neuer Projekt-/Modordner: `SerpNativeAPI`.
- BepInEx-Plugin-GUID: `SerpNativeAPI_Serp`.
- anfängliche Testversion: `0.1.0`.
- Zielframework entsprechend den Workspace-Mods: .NET Framework 4.8.1.
- harte BepInEx-Abhängigkeit auf den Script Extender mit GUID `000shcdese`.
- Die einzelne Assembly `SerpNativeAPI.dll` enthält öffentliche Contracts und interne Implementierung. Keine separate Contracts-DLL in V1.
- Migrierte Mods erhalten eine harte Abhängigkeit auf `SerpNativeAPI_Serp` in Code und Paketmetadaten.
- Verbraucher referenzieren die API-Assembly mit deaktiviertem lokalem Kopieren, sodass keine privaten Duplikate in Modordnern entstehen.
- Das API-Projekt erhält einen eigenen `build.bat` nach den bestehenden Workspace-Konventionen. Verbraucher-Builds müssen die bereits gebaute beziehungsweise installierte API eindeutig finden und bei fehlender Referenz verständlich abbrechen.
- Keine README-Datei ändern. Eine kurze API-Architekturdatei darf außerhalb einer README angelegt werden.

### Öffentliche API

Die öffentliche Oberfläche bleibt katalogisiert und typisiert. Es gibt keine öffentliche Methode für beliebige Pointer, RVAs, Patternscans, Speicherschreibvorgänge oder freie Detours.

Vorgesehene Kernkonzepte:

- `NativeApiState`: `Pending`, `Ready`, `Unavailable`.
- `NativeCapabilityState`: mindestens `Pending`, `Available`, `UnsupportedBuild`, `PatternMissing`, `Ambiguous`, `ValidationFailed`, `Conflict` und `Faulted`.
- `NativeCapabilityDiagnostic`: Capability-ID, Zustand, vollständiger DLL-Hash, kurze technische Begründung und gegebenenfalls Besitzer-GUID des Konflikts. Keine nativen Adressen in der normalen öffentlichen Oberfläche.
- statischer, leicht referenzierbarer Einstieg, beispielsweise `SerpNativeApi.Current`.
- `WhenReady(Action<ISerpNativeApi>)`: Callback wird gespeichert, solange der Zustand `Pending` ist, und sofort synchron wiedergegeben, wenn die API bereits initialisiert ist.
- typisierte Capability-Abfrage statt generischem Raw-Target-Zugriff, zum Beispiel `TryGetGatehouseTiming(...)` und `TryGetSelectedUnitCommand(...)`.

Die konkreten Namen dürfen an die Workspace-Namenskonventionen angepasst werden, die Semantik ist jedoch verbindlich. Abfragen vor der Initialisierung dürfen nicht so aussehen, als sei ein Build endgültig inkompatibel; sie liefern `Pending` und ermöglichen `WhenReady`.

#### Gatehouse-Capability

- Nimmt einen Besitzerbezeichner auf Basis der BepInEx-Mod-GUID entgegen.
- Reserviert alle vier nativen Werte als eine exklusive semantische Einheit.
- Bietet eine fachliche Settings-Struktur mit Human-/AI-Delay in Sekunden, Human-/AI-Distanz in Feldern und einem Aktivierungszustand.
- Validiert endliche Zahlen und die von `ExtraFeatures` erlaubten Wertebereiche, bevor native Werte berechnet werden.
- Rundung bleibt `MidpointRounding.AwayFromZero`.
- Deaktivierung schreibt die vier Vanilla-Werte zurück, behält aber den Prozessbesitz der Capability.
- Mehrmaliges Anwenden identischer Werte ist idempotent.

#### Selected-Unit-Command-Capability

- Exponiert einen typisierten Argumentwert statt eines rohen Delegate-Pointers.
- V1 unterstützt Before-Callbacks. Diese dürfen Beobachtungen und mod-eigene Side Effects ausführen, aber weder den Originalaufruf ersetzen noch dessen Rückgabewert manipulieren.
- Callback-Reihenfolge ist deterministisch: zunächst Phase, dann ordinal nach Besitzer-GUID.
- Ein Callbackfehler wird mit Mod-GUID und Capability geloggt; weitere Callbacks sowie Vanilla werden trotzdem ausgeführt.
- Die Originalfunktion wird exakt einmal aufgerufen und ihr Rückgabewert unverändert zurückgegeben.
- Eine Registration liefert ein Handle zum expliziten Aktivieren/Deaktivieren. Das Freigeben eines Verbraucher-Handles entfernt nicht den zugrunde liegenden Prozess-Detour.

### Build- und Zielkatalog

Der Katalog ist kompilierter C#-Code, kein Runtime-JSON. Jede Builddefinition verwendet den vollständigen SHA-256 und enthält pro Ziel:

- stabile interne Target-ID;
- Referenz-RVA;
- erwartete Bytes und Maske;
- erlaubten PE-Bereich;
- abgeleitete relative Ziele beziehungsweise Immediate-Offets;
- semantische Invarianten;
- alle betroffenen Speicherintervalle;
- Kennzeichnung, ob ein Pattern-Fallback auf einem abweichenden Build überhaupt zulässig ist.

Initial unterstützt der Pilot den oben genannten vollständigen DLL-Hash. Der Implementierer muss ihn vor dem ersten Build gegen die aktuell installierte DLL prüfen. Falls der Hash inzwischen abweicht, wird kein alter Pilotpatch aktiviert; stattdessen ist die Analysebasis für einen neuen, separat dokumentierten Buildkatalog zu verwenden.

Auf bekannten Builds gilt:

1. Referenz-RVA und erwartete Bytes prüfen.
2. Alle relativen Ziele, Ableitungen und PE-Grenzen prüfen.
3. Nur bei ausdrücklich freigegebenem Target eine eindeutige AOB-Suche als Fallback verwenden.
4. Kein oder mehrere Treffer führen zu einer nicht verfügbaren Capability.

Für die beiden V1-Piloten ist eine Aktivierung auf unbekanntem Hash nicht erlaubt, selbst wenn das Pattern zufällig eindeutig erscheint. Eine unbekannte Version deaktiviert nur die betroffenen Capabilities; sie soll die API und unabhängige zukünftige Capabilities nicht pauschal abstürzen lassen.

### Initialisierung und Lebensdauer

- Das API-Plugin registriert sich in `Awake()` auf `CrusaderLibrary.Instance.LibraryLoaded`.
- Die eingebauten Targets sind bereits vor dem Ereignis bekannt und werden beim Ereignis einmal aufgelöst.
- DLL-Dateihash, PE-Basis, Imagegröße und relevante Sections werden einmal validiert.
- Ein übergebener `ReadOnlySpan<byte>` wird nicht über den Callback hinaus gespeichert.
- Die fertige Capability-Tabelle wird danach unveränderlich veröffentlicht.
- Spät ladende Verbraucher erhalten über `WhenReady` sofort den aktuellen Zustand.
- Echte Detours, Trampoline und unmanaged Delegates bleiben über statische Prozessreferenzen verwurzelt.
- Weder `OnDisable()` noch `OnDestroy()` räumt dauerhafte API-Infrastruktur auf.
- Logging enthält immer Zeitstempel mit Millisekunden und nennt Capability, Buildhash sowie Verbraucher-GUID.

### Konflikt- und Besitzmodell

- Reines Auflösen eines Targets reserviert noch keinen Speicher.
- Eine Mutation oder ein Hookabonnement registriert Besitzer-GUID, Capability-ID, Modus und die betroffenen halboffenen Intervalle `[Start, Ende)`.
- Direkte Daten-/Codepatches sind standardmäßig exklusiv.
- Überlappende exklusive Intervalle verschiedener Besitzer werden fail-closed abgewiesen und diagnostiziert.
- Wiederholte Anforderung desselben Besitzers für dieselbe Capability ist idempotent.
- Hookfähige Einstiegspunkte werden nicht mehrfach detourt. Sie sind über den zentralen Broker absichtlich gemeinsam nutzbar.
- Es gibt kein Last-writer-wins und keine automatische Übernahme bereits fremd veränderter Bytes.
- Vor jedem Patch werden die zuletzt erwarteten Werte erneut gelesen. Unerwartete externe Änderungen deaktivieren die Capability beziehungsweise den konkreten Vorgang.

### Transaktionale Schreiboperationen

Für Gatehouse und spätere Datenpatches:

1. Alle Eingaben umrechnen und validieren, bevor Speicher verändert wird.
2. Alle aktuellen Werte gegen den intern erwarteten Zustand prüfen.
3. Den kleinsten sinnvollen Bereich beziehungsweise exakt ermittelte Seiten mit `VirtualProtect` freigeben.
4. Alte Werte sichern und alle Änderungen schreiben.
5. Alle geschriebenen Werte erneut lesen.
6. Bei Schreib- oder Prüfungsfehler alle alten Werte best-effort zurückschreiben und den Fehler melden.
7. Ursprünglichen Speicherschutz zuverlässig wiederherstellen.
8. Instruction Cache leeren und abschließend erneut prüfen.

Fehler beim Wiederherstellen des Speicherschutzes dürfen nicht durch eine andere Exception verdeckt werden; die Implementierung muss ursprünglichen Fehler und Cleanup-Fehler gemeinsam diagnostizierbar halten.

## Umsetzungsschritte

1. Aktuellen Git-Status dokumentieren und bestehende Benutzeränderungen unangetastet lassen. Zum Zeitpunkt dieser Übergabe bestanden unter anderem Änderungen in `AssassinCombatFix` und `_inspect\HostClientPresetTests`; sie gehören nicht zu diesem Auftrag.
2. Kanonischen DLL-Hash prüfen und den unterstützten Build festschreiben.
3. `SerpNativeAPI` nach einer passenden vorhandenen Workspace-Mod strukturieren, ohne eine README zu ändern.
4. Native Modulidentität, Buildkatalog, Resolveradapter, Capabilitydiagnosen und Readiness-Mechanismus implementieren.
5. Zentralen Intervall-/Besitzmanager und den Hookbroker implementieren.
6. Gatehouse-Capability samt transaktionaler Vierfachschreiboperation implementieren.
7. Selected-Unit-Command-Capability mit einem zentralen `NativeDetour` implementieren.
8. `ExtraFeatures` ausschließlich für Gatehouse auf die API umstellen. Vorhandene API-unabhängige Resolvernutzungen bleiben bestehen.
9. `BugfixesAndQoL` ausschließlich für Assassin Selected-Unit-Command auf die API umstellen. Die Fachlogik bleibt lokal und der alte Detourpfad wird entfernt, nicht als Fallback behalten.
10. Harte Abhängigkeiten und Referenzen in Projekt- und Paketmetadaten ergänzen.
11. Einen kurzen, maschinenlesbaren oder tabellarischen Native-Surface-Audit unter `_inspect\SerpNativeAPI` ablegen, der weitere Migrationskandidaten nach gemeinsamem Ziel gruppiert. Pfade kurz halten und das bestehende 240-Zeichen-Limit prüfen.
12. Tests und statische Kontrollen vollständig abschließen, erst danach die vorgesehenen `build.bat`-Treiber direkt aus PowerShell mit erhöhten Rechten ausführen.

## Tests und Abnahmekriterien

Ein fokussiertes Testprojekt unter einem kurzen `_inspect`-Pfad soll mindestens folgende Fälle abdecken:

- bekannter DLL-Hash mit gültigem Referenz-RVA;
- unbekannter Hash ergibt für beide Piloten `UnsupportedBuild` und keinerlei Mutation;
- manipulierte Bytes am bekannten RVA;
- Pattern mit keinem, genau einem und mehreren Treffern;
- ungültige relative Zieladresse und Ziel außerhalb erlaubter PE-Bereiche;
- Readiness-Callback vor und nach Initialisierung;
- unabhängige Capabilities bleiben verfügbar, wenn eine andere fehlschlägt;
- nicht überlappende Reservierungen verschiedener Besitzer;
- überlappende exklusive Reservierungen werden abgewiesen;
- identische Wiederholungsanforderung desselben Besitzers ist idempotent;
- deterministische Before-Callback-Reihenfolge;
- ein werfender Callback verhindert weder weitere Callbacks noch den Originalaufruf;
- Vanilla wird genau einmal aufgerufen und der Rückgabewert unverändert weitergegeben;
- Gatehouse-Werte werden gemeinsam geschrieben und verifiziert;
- simuliertes Scheitern nach Teiländerung rollt alle vier Werte zurück;
- Deaktivierung stellt Vanilla-Werte wieder her, ohne den Besitz freizugeben;
- unerwartet extern veränderter Wert führt fail-closed zu keiner Überschreibung.

Zusätzliche Abnahme:

- Beide Pilotmods kompilieren gegen die API.
- Für die migrierten Funktionen ist kein alter Low-Level-Ausführungspfad mehr erreichbar.
- Es existiert pro hookbarem Target höchstens ein tatsächlicher `NativeDetour`.
- Verbraucher starten ohne ihre deklarierte harte API-Abhängigkeit nicht stillschweigend mit unvollständiger Funktionalität.
- Kein Build oder Test schreibt in `shcde-script-extender`; dessen Status und relevante Dateihashes bleiben unverändert.
- Rohbaseline unter `_inspect\CrusaderDE-Native-Baseline` bleibt unverändert.
- Keine Runtime-JSON-Abhängigkeit wird eingeführt.
- Sämtliche geänderten Textdateien verwenden CRLF, enthalten keine nackten LF und keine unbeabsichtigten literalen `\r\n`-Sequenzen.
- Sämtliche für Git vorgesehenen Dateien bleiben unter GitHubs 100-MiB-Grenze und dem Workspace-Pfadlimit von 240 Zeichen.
- Kein Spielstart, Laufzeitscan, Debugger-Attach oder Runtime-Hook-Test ohne gesonderte Erlaubnis des Nutzers.

## Versions- und Releasevorgaben

- `SerpNativeAPI` beginnt während der Implementierung mit `0.1.0`.
- Versionen von `ExtraFeatures` und `BugfixesAndQoL` während Entwicklung und Tests nicht ändern.
- Wenn die Pilotmigration fachlich final bestätigt ist, den Nutzer vor der Versionsanhebung beziehungsweise README-Dokumentation fragen.
- Eine spätere Versionsänderung je Mod atomar über Plugin-Konstanten, `info.json`, Manifeste, Build-/Releasekonfiguration und mitgeführte Paketmetadaten durchführen.

## Nicht Bestandteil von V1

- keine Migration aller vorhandenen RVAs und Patternpatches;
- keine öffentliche Raw-Memory-, Pointer-, RVA-, AOB- oder Universal-Detour-API;
- kein Ersatz und keine Änderung des Script Extenders;
- keine automatische Unterstützung unbekannter Spielversionen;
- kein stiller Fallback auf die alten Implementierungen der beiden Piloten;
- keine Runtime-Abhängigkeit auf SQLite, Ghidra-, Rizin- oder semantische Analyseartefakte;
- keine Änderung von Modsettings-XAML oder Lokalisierungen für die Pilotmigration;
- kein Modversionssprung während der Testphase.

## Hinweise für den übernehmenden Chat

Zuerst vollständig die Workspace-`AGENTS.md` lesen und danach diese Datei als verbindlichen Arbeitsplan verwenden. Besonders zu beachten sind der frühe BepInEx-`OnDestroy()`-Lebenszyklus, die kanonische DLL, das Änderungsverbot des Script Extenders, CRLF, bestehende Benutzeränderungen und die Buildregel, nach abgeschlossenen Prüfungen genau die vorgesehenen `build.bat`-Treiber zu verwenden.

Empfohlener Auftragstext im neuen Chat:

> Implementiere `_inspect/SerpNativeAPI/HANDOFF.md` vollständig. Prüfe zuerst den aktuellen Arbeitsbaum und den Hash der kanonischen CrusaderDE.dll, erhalte alle bestehenden Benutzeränderungen und ändere den Script-Extender nicht.
