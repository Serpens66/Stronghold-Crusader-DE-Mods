# Dependency Bootstrap Probe: Schatteninventar

## Abgeschlossene Startprüfung

Observe bestand am 16.10.2026 um 18:34 und 18:47 Uhr. Der aktive Start um 19:22 Uhr bestand beide Transformationen, Delegate-Erzeugung und JIT-Vorbereitung im Unity-Mono-Prozess, Gewinneraufnahme vor SE-Start, genau einen Eintritt in die Originalprüfung, Chainloader-Abschluss und 120 Renderframes nach Startup-Cleanup. Der Nutzer bestätigte den korrekten Start. Beide bisherigen Warnungen blieben erhalten; in den geprüften Logs fehlten die früheren Initialisierungsfehler.

Abnahme gilt für die archivierten Binäridentitäten, nicht für beliebige BepInEx-/Extender-Versionen oder spätere Awake-Erfolge aller Gewinner. Save-/Simulationstest und zusätzlicher Disabled-Neustart sind nach Nutzerentscheidung für diese Startprüfung nicht erforderlich.

Beweissicherung: `_inspect/DependencyBootstrapProbe/Active-accepted-20261016-193043` im Workspace. Enthält Trace, BepInEx-/Unity-Log, Dateiidentitäten, vier Vergleichsassemblies und Hashes des unveränderten frühen Einstiegscodes. Patcher des bestandenen Laufs: EA182E455F8E1F5D8630C0065690DC2AF55D92BDAD1522BA5EA9DC330AD83A26. Plugin: C44E1B8E00976A5C458394513D6DE41970F6AA111916E76275F0F5473919F6B6.

Der fehlerhafte Build von 17:06:49 bleibt ausschließlich als Vergleich auf dem Desktop erhalten. Seine konkrete früh scheiternde Operation ist weiterhin nicht abschließend bewiesen. Keine Wiederinstallation dieser Fassung.

## Schattenstufe und Grenzen

Finish und der synchrone Listener bleiben unverändert. Erst bei `Chainloader ready` wird die Hookruntime erreicht; kein manuelles Initialize/Start, keine Flagänderung. Alle Hooks bleiben statisch verwurzelt und werden beim Unity-Cleanup nicht zurückgebaut.

Die Probe erfasst die tatsächliche Gewinnerliste nach BepInEx-Auswahl einschließlich Prozess-/Inkompatibilitätsfilterung. Am Einstieg vor SE-AddComponent kopiert sie die eindeutig zugeordnete lokale Menge bereits fehlgeschlagener Plugins. Statische harte BepInEx-Ketten werden transitiv bewertet; optionale Abhängigkeiten sind keine Pflicht. Eine neue Dateisuche zur Auswahl anderer DLL-Gewinner findet nicht statt.

Die Manifestprüfung verwendet ausschließlich `Shared.DependencyFreeJson` per Quelllink. Das nächste Manifest innerhalb des Plugin-Unterbaums entscheidet; kein Überspringen ungültiger Kindmanifeste. Reparse-Verzeichniswege, GUID-/Versionskonflikte, doppelte/case-kollidierende Gewinner, ungültige AssetMode-Werte und ungeklärte Quellen liefern keine Ergänzung. Manifestabhängigkeiten werden zusätzlich konservativ geprüft; Zyklen sind ausgeschlossen.

Offline liest das Testprogramm nur Cecil-Metadaten und Manifeste. Es lädt/führt keine Pluginassembly aus. Es verwendet für Manifestabhängigkeiten vorläufig die ausgewählten Pluginversionen; das ist ausdrücklich kein rekonstruierter SE-Index. Kandidaten mit bestandenen statischen Prüfungen bleiben **Unclear**, solange Originalindex, KillSwitch und vorherige Loaderfehler nicht live belegt sind. Der Bericht enthält aktuelle DLL-Hashes und seine Traceidentität; die alte Trace enthält keine historischen Gewinner-DLL-Hashes.

Im Spiel beobachtet die Probe den vollständig aufgebauten lokalen Extender-Index vor der ersten Abhängigkeitsprüfung. Sie kopiert ihn und verwendet nur in der Kopie neue Einträge. Der originale SE-Kandidatenleser prüft geeignete verschachtelte Quellen einschließlich KillSwitch; temporäre Quellen werden sofort freigegeben, nie registriert. Der originale Versionsprüfer vergleicht ursprünglichen und Schattenindex für die unveränderten ursprünglichen Verbraucher. Keine zusätzlichen verschachtelten Verbraucher werden in die echte Prüfung aufgenommen.

`SHADOW_COMPLETE` bestätigt Anzahl Kandidaten/Indexeinträge/Ergänzungen/Abhängigkeiten, ursprüngliche und hypothetische Fehleranzahl sowie unveränderten Originalindex und unveränderte vorbereitete Kandidaten. Einzelentscheidungen stehen in `SHADOW_CANDIDATE`, Indexeinträge in `SHADOW_INDEX`, Vergleiche in `SHADOW_DEPENDENCY`. Diagnosefehler werden abgefangen; die Originalprüfung läuft unverändert weiter und ein Fehler verhindert die Abnahme.

## Prüfungen und Ausführung

1. `tests/Run-Tests.ps1`: gemeinsame Policy, Negativfälle, echte BepInEx-/SE-DMD-Transformationen, Originalkontrollfluss/Stack, frühe Typen/Listener und Vergleich mit dem originalen SE-Versionsprüfer. Desktop-Delegate-Erzeugung ersetzt die Mono-JIT-Prüfung nicht.
2. `tests/Test-LogChecker.ps1`: Observe/Active/Disabled und Schattenmarker einschließlich fehlender/unvollständiger/widersprüchlicher Ergebnisse.
3. `Analyze-Shadow.ps1 -TracePath <gesicherte Trace> -OutputPath <Analysebericht.json>` erstellt den Offlinebericht nach den Tests. Die Spielinstallation wird nicht verändert.
4. Nach Quellkontrolle, verify, Shared-Boundaries und Abhängigkeitsprüfung einmal `build.bat /nopause` erhöht ausführen. Nur die eigenen zwei DLLs und info.json werden installiert; Standard bleibt Observe. Version während Diagnose weiterhin 0.1.0.
5. Bei unverändertem frühen Einstieg kann der bestandene aktive Start für diese Schattenweiterentwicklung verwendet werden: `Set-ProbeMode.ps1 -Mode Active -ConfirmMenuAndUnityLog -AcceptedStartupRoot <absoluter Beweissicherungsordner>`. Der Treiber prüft archivierten Start, frühere Quellen, aktuelle Core-/SE-Hashes und lokale/installierte Probeidentität. Das ist keine Abnahme der neuen Schattenruntime.
6. Nutzer: vollständig neu starten, beide erwarteten Warnungen bestätigen, Hauptmenü bedienen und kurz warten. Kein Spielstand erforderlich.
7. `Check-Run.ps1 -Mode Active -RequireShadow` sowie aktuelle Unity-/BepInEx-Logs auswerten. Erwartung in der unveränderten aktuellen Modauswahl: BugfixesAndQoL 1.0.192 wäre für beide bekannten Anforderungen geeignet; Warnungen bleiben im echten Lauf erhalten. Abweichungen zuerst diagnostizieren, nicht Prüfungen abschwächen.

## Rückweg und nächster Schritt

Bei schwarzem Bildschirm Spiel schließen, Trace/Player.log und vorhandenen BepInEx-Log sichern und mit `Set-ProbeMode.ps1 -Mode Remove` ausschließlich die eigenen Diagnosekomponenten entfernen. `-WhatIf` zeigt die Ziele. Keine Wiederholung derselben fehlerhaften Fassung. `Observe`/`Disabled` bleiben verfügbar, ein neuer separater Kontrollstart ist kein Pflichtschritt dieser Abnahme.

Erst nach vollständigem Schattenbericht und erfolgreichem Spielstart die begrenzte Indexkorrektur separat planen. APIShared, Script Extender, fremde Patcher und Assets bleiben unverändert; keine Veröffentlichung.

## Installierter Schattenversuch

Build und Installation am 16.10.2026 19:44:18 erfolgreich. Modus Active mit passender Buildbindung gesetzt; lokale/installierte DLLs identisch. 32 Logpruefer-Faelle, zwoelf negative Hookvertraege, Policy-Fixtures, 75 originale SE-Abhaengigkeitsvergleiche und Pflichtpruefungen bestanden. Der neue Spielnachweis steht noch aus.

Patcher SHA256: 46303CFE62CB69CA82F052E21EB272D5E11DB0B5B5E03670BDE24B0574F2778A. Plugin SHA256: C44E1B8E00976A5C458394513D6DE41970F6AA111916E76275F0F5473919F6B6.
