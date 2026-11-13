# Vorbereiteter Feuer-/Pest-Beobachter

Stand 13.11.2026: Quellcode und Build-/Offlineprüfungen vorbereitet, **kein Spieltest
ausgeführt**. Dieser Testmod schaltet die produktive Freigabesperre nicht frei.
Er benötigt den gemeinsam mit ihm geprüften Pathfinding-Arbeitsstand; die Versionsnummer
allein beweist den internen Reflexionsvertrag nicht.

## Einstieg und Voraussetzungen

`build.bat /nopause /noinstall` kompiliert nur den Testmod und führt seine Offline-
Vertragsprüfung aus. Er installiert nichts, startet kein Spiel und schreibt keine
Laufanforderung. Optional `-ApiSharedDir <Ordner>` an den PowerShell-Treiber übergeben.
Die Vertragsprüfung liest das kompilierte Hauptmod-Paket im Workspace; zuerst den
aktuellen Hauptmod ohne Installation bauen.

Für einen später ausdrücklich autorisierten Lauf den üblichen
`Shared/Tools/Automation/Invoke-InGameTest.ps1` mit Testmod-GUID
`PathfindingHazardTest_Serp`, passendem PluginLayout und ModPackage verwenden.
Der Einstieg konsumiert ausschließlich bekannte Szenarien in
`BepInEx/config/SerpAutomation/PathfindingHazardTest_Serp.run`: Lauf-GUID im N-Format
und Szenario, jeweils eine Zeile. Unbekannte Szenarien und ungültige Anforderungen
werden nicht gelöscht. Ohne Anforderung keine Aktivierung.

In `BepInEx/config/PathfindingHazardTest_Serp.cfg`, Abschnitt `[Fixture]`, angeben:

| Schlüssel | Vorbereitung |
| --- | --- |
| `SavePath` | Absoluter Pfad zu einem bestehenden isolierten SP-Testsave; keine Datei wird vom Test erzeugt/überschrieben. |
| `ActorId` | Lebender menschlich kontrollierter Speerkämpfer in einer eigenen lebenden Einmanngruppe, anfangs stehend. Global-ID und Gruppenidentität werden im echten Simulationstick erfasst. |
| `HazardX`, `HazardY` | Kachel mit einem echten aktiven Feuer-/Pesteffekt, die der Produktionsscanner markieren muss. |
| `TargetX`, `TargetY` | Bei Wartefällen Ziel hinter der Gefahr. Beide Achsen müssen auch für Fluchtszenarien gültig konfiguriert sein. |
| `WaitSeconds` | Endliche Vergleichsfrist, Standard 2 Spielsekunden; begrenzt weiterhin die Fortsetzungsfrist eines vorher laufenden Fliehenden. |
| `MaximumTicks` | Native Beobachtungsgrenze, Standard 12000; mindestens `WaitSeconds × 40 + 80`, höchstens 72000. |

Vorbereitete Saves müssen echte Effekte und passende Wegverhältnisse mitbringen.
Dieser Treiber spawnt/zündet/verschiebt keine Effekte und verändert keine Timeline.
Keine aktiven Flucht-, Solo- oder Wartefälle beim Aktivieren; Basis-Routenkern muss
aktiv und verfügbar sein. Das Testsave muss isoliert sein: keine externen Befehle,
keine anderen gefährdeten menschlichen Truppen oder laufenden Automationen.

## Bereits ausführbar vorbereitete Szenarien

| Szenario | Erforderlicher Save und tatsächlicher Erfolgsnachweis |
| --- | --- |
| `fire-escape`, `plague-escape` | Actor steht in einer echten gefährlichen Fläche; erreichbares sicheres Umfeld. Gefahr endet rechtzeitig innerhalb der normalen Rückkehrfrist. Beobachtet werden echter Scan, Fluchtfall, sicheres Ankommen, `Returning`, tatsächliche Rückkehr bis auf eine Kachel und gleiche ursprüngliche Gruppenidentität. Ein erteilter Befehl allein ist kein Erfolg. |
| `fire-wait-bounded`, `plague-wait-bounded` | Actor steht sicher vor einer Passage ohne zulässigen sicheren Umweg. Der Treiber erteilt einen normalen Gruppen-Move erst nach echtem Scanzeugen. Gefahr bleibt über die Wartefrist hinaus bestehen. Ein echter Wartefall, dessen eigene Frist, gestiegener Abbruchzähler und anschließender Stillstand müssen beobachtet sein. |
| `fire-wait-unlimited`, `plague-wait-unlimited` | Gleiche Passage; Gefahr bleibt zunächst länger als die endliche Vergleichsfrist und verschwindet später oder eröffnet einen zulässigen sicheren Weg. Wartefall muss die Vergleichsfrist überleben, anschließend verschwinden und Actor muss tatsächlich am Ziel ankommen. |

Ein unpassendes Save führt zu einem Fehler/Timeout und niemals zu einem fingierten
Gameplay-PASS. Der Speerkämpfer und die Umgebung werden nicht künstlich unsterblich
gemacht; ausreichende Gesundheit und passende Effektlebensdauer gehören zur
Savevorbereitung. Unterschiedliche Einheitentypen bleiben weitere Matrixfälle.

## Thread, Aktivierung und Wiederherstellung

Die öffentliche Automation läuft auf Unity und lädt das Save regulär. Aktivierung,
Optionen, Befehle und Beobachtung laufen ausschließlich im registrierten echten
`SimulationTickEvents`-Callback mit Ordnung 700, nach Pathfindings Scheduler 600.
`AutomationRun.InSimulation` auf Unity wird nicht als echter Simulationscallback
ausgegeben. Logdaten werden als unveränderliche Texte über den öffentlichen
UI-Dispatcher ausgegeben; keine Noesis-/Unity-Arbeit aus dem Simulationsthread.

Der Treiber kopiert das bestehende interne EngineOptions-Objekt und verändert nur
seine private Testkopie. Hauptmod-Capture und versteckte Einstellungsbereiche bleiben
unverändert. Reflexion ist auf benannte, vor Verwendung geprüfte Verträge begrenzt;
keine rohen nativen Adressen und keine neue Hauptmod-Test-API.

Vor einem Erfolg endet der Treiber vorhandene Testfälle mit deaktivierter
Flucht/Neuplanung über den tatsächlichen Save-Wiederherstellungspfad, damit eigene
Tribes wieder aufgenommen werden. Danach setzt er ursprüngliche Optionen und
Fallbytes einschließlich Auftragssequenz zurück und prüft die Referenz/Bytes.
Nur solange der Provider noch genau die Testkopie hält und dieselbe Mission aktiv
ist, darf er wiederherstellen. Eine neue Missionskonfiguration wird nicht überschrieben.
Die Engine-Welt, Positionen, HP und Effekte werden nicht zurückgespult: dafür das
disposable Testsave neu laden; den normalen Runner für Datei-/Plugin-Rücksetzung nutzen.

Bei Fehler/Abbruch wird die Wiederherstellung für den nächsten tatsächlichen
Simulationstick angefordert. Ist die Simulation pausiert, bleibt sie ausdrücklich
ausstehend, bis die Simulation wieder läuft. Deshalb kein automatisches Beenden
und kein PASS vor bestätigter Wiederherstellung. Bei Missions-/Optionswechsel wird
die alte Lease verworfen, ohne alte Einheiten- oder Gruppendaten auf eine neue Karte
anzuwenden. Dieser Ablauf ist offline vorbereitet, noch nicht im Spiel abgenommen.

## Weiterhin erforderliche Abnahmen

- Sichtprüfung, weitere Soldatentypen, echte HP-Schadensvergleiche, Höhen-/Treppen-
  Übergänge und erhöhte Garnisonen; gegenseitige Feuer-/Pestüberlagerung.
- Save/Load während aller Phasen, Solo-/Formationsüberschneidung, neue Befehle,
  Queue, KI-Gruppen, ID-Wiederverwendung, Editor, optionale Modkombinationen.
- Große Gefahrenlast, aktuelle Karten-/Savevergleiche und echte Zweirechner-MP-Läufe.
  Dieser lokale Testtreiber startet keinen Multiplayer und ist kein MP-Nachweis.
- Erfolgs-, Fehler-, Cancel- und pausierte Cleanup-Pfade dieses Treibers selbst
  im Spiel prüfen. Kein Spieltest oder Installation wurde für seine Erstellung ausgeführt.
