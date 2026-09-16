# Missions-Lifecycle: Prüfung und Auslieferung

Stand: 16.09.2026. Implementierung und automatisierte Prüfung abgeschlossen; neue Spieltests stehen noch aus.

## Implementierter Vertrag

APIShared stellt `IMissionLifecycleCapability` mit `OnStart`, `OnEnd` und den ausdrücklich frühen `OnInitialization`-Phasen bereit. Editor, neue Partie, Save-Wiederherstellung, Missionstyp, Customize-Herkunft und Netzwerkrolle werden gemeinsam erfasst. Shared vermittelt die Meldungen pro Assembly und aktualisiert zuerst das Aktivierungsgate. Die bisherigen Mod-/Feature-Regeltabellen liegen einmalig in APIShared; Einstellungen verbleiben in den Mods.

Die editorbezogene Capability wurde ersetzt. Direkte Extender-Lifecycle-Abonnements außerhalb des zentralen Dienstes und der verbliebene Editor-Kartengrößendetektor in EnemyGatePathfindingTest wurden entfernt. Die ExtremePowers-Begleitassembly sowie die internen APIShared-Lobby-/HUD-Verbraucher benutzen ebenfalls die gemeinsame Quelle.

## Zweite Codeprüfung

Die zusätzliche Durchsicht hat folgende Probleme erkannt und korrigiert:

- Doppelte Endbehandlungen aus ehemaligen Editor-Sondercallbacks und neuen allgemeinen End-Abonnements.
- Einen stehenbleibenden Save-Ladestatus nach Fehlschlag in MoatMove.
- Falsche Reihenfolge bei der Rekonstruktion des Quarry-Zustands.
- Verlust der Lobby-Host-/Clienterkennung außerhalb einer aktiven Mission.
- Verwechslung lokaler Coop-Starts mit freiem Skirmish vor dem nativen Start. Die tatsächlich übergebenen PreInit-Parameter sind jetzt maßgeblich.
- Unvollständige Bereinigung durch eine mechanisch entstandene konstante Bedingung in TroopMovementFix3.
- Uneindeutige beziehungsweise ungültige Metadaten: Spieler-ID, Missionsindex und unbekannte Angaben werden ausdrücklich unterschieden.
- Veraltete Testannahmen zu Dateipfaden und zur ersetzten Multiplayer-Übergangsheuristik. Der ExtendedData-Treiber stoppte zunächst bei 40/42 Tests; nach Anpassung an den neuen Vertrag bestehen 42/42. Die Tests wurden nicht übersprungen.

## Automatisierte Nachweise

- JSON-/Unity-Lifecycle-/Abhängigkeits-/CRLF-Präflight: 26 Runtimeprojekte, 569 unterschiedliche Quelldateien. Vor jedem Runtime-Build erneut ausgeführt.
- Roslyn-Prüfung ohne Ausgabe einer Runtimeassembly: sämtliche Runtimeprojekte plus APISharedTests ohne Fehler; öffentliche API-Dokumentationspflicht eingeschlossen.
- 22 Managed-Hook-Signaturen gegen die Metadaten der geprüften Spielassembly validiert.
- 103 Assertions gegen produktive Zustandsmaschine und öffentliche Verträge: alle 16 Startart-Wechselkombinationen, gleiche Kartenmerkmale, Fehler, Replay, Reentranz, Identität und verzögerte Clients.
- 27 Assertions gegen den produktiven Dienst mit nachgebildeten Spielgrenzen: Installation/Rollback, genau ein Vanilla-Aufruf, boolesche Editor-Ergebnisse, Phasenreihenfolge, verschachtelte Unloads, native Fehler trotz äußerem Erfolg, Optionen/Szenenwechsel, Coop-Parameter, verzögerte/doppelte/ersetzte Clientabschlüsse und Beobachter-/Loggerfehler.
- HostClientPresetTests: Freigabereihenfolge, Session-/Editorintegration, Mod-/Featurematrix, Customize-Herkunft, Netzwerkautorität, Host-/Client-Sperren, Routing, Persistenz und MessagePack bestanden.
- LobbyModSettingsPresetTests bestanden; neun vorhandene Legacy-MessagePack-Dateien ausschließlich im Speicher validiert.
- EnemyGatePathfindingTest: 823 Assertions bestanden. PreplacedTest: 655 Prüfungen bestanden. ExtendedData: 42/42 Tests bestanden.
- APIShareds vollständige Baseline-/Native-Vertragstests im eigenen Buildtreiber bestanden. Weitere eingebundene Modtests bestanden ebenfalls, darunter 13.871 Assassin-A*/Dijkstra-Assertions und die Bewegungs-/Hookprüfungen von BugfixesAndQoL.

## Builds und Installation

Ausschließlich die erhöht aufgerufenen eigenen `build.bat /nopause` verwendet, APIShared zuerst. Danach alle 24 betroffenen Verbraucher-Buildtreiber erfolgreich, einschließlich ExtremePowers.API über den ExtremePowers-Treiber. Kein Versionswechsel.

17 bereits installierte Haupt-DLLs und ExtremePowers.API stimmen per SHA-256 mit ihren gebauten Paketen überein. Acht Testmods blieben durch `/noinstall` uninstalliert: AIDefenseTest, EnemyGatePathfindingTest, MoatMove, OxTetherIdleFixTest, PreplacedTest, SkinTest, StockpileAccessFixTest und VirtualUnitsPrototype.

Einzelprotokolle liegen in diesem Ordner. `build-results.csv` enthält alle Versuche einschließlich des zunächst gestoppten ExtendedData-Laufs; `installed-hashes.csv` dokumentiert die Haupt-DLLs. APIShared wurde davor separat erfolgreich gebaut und installiert (`APIShared-build.log`).

Nicht blockierende Buildwarnungen: Mono.Cecil-Referenzkonflikte/Assemblyvereinheitlichung in bestehenden Referenzen, Nullable-Annotationen außerhalb eines Nullable-Kontexts, nicht gesetzte Diagnosefelder in MoatMove und die nicht erreichbare NuGet-Vulnerability-Abfrage in ExtendedData. APIShared selbst: null Warnungen, null Fehler.

Der kanonische Script-Extender-Fork blieb unverändert. Installierter Native-Hash stimmt weiterhin mit CURRENT.json überein. README-Dateien, Modversionen, ElevatedMoat-Dateien und die vorbestehenden Benutzeränderungen am Release-Workflow/-Code wurden nicht bearbeitet.

## Noch benötigte Spielbeobachtungen

Der frühere ElevatedMoat-Test bestätigte nur die vorherige Editor-Implementierung. Für diese Vereinheitlichung wurden keine neuen Spieltests behauptet.

1. Editor Neu → Neu → Laden → Neu sowie Laden → Laden; gleiche Größe/Spieler-ID muss neue Sitzungskennungen ergeben. ElevatedMoat und Blueprint/HUD-Funktionen sichtbar prüfen.
2. Normale neue Partie und Save-Laden; Startressourcen/Burgen nur bei tatsächlich neuer erlaubter Partie. Fehlgeschlagenes Laden darf keinen Start veröffentlichen oder alte Freigaben erhalten.
3. Kampagne, Einzelmission, Tutorial, Vanilla-/Custom-/Coop-Trail und Sands of Time, jeweils soweit verfügbar direkt und Customize/Save. Bestehende Freigaben müssen unverändert bleiben; Tutorial erhält keine neue Freigabe.
4. Echtes Multiplayer auf Host und Client, einschließlich Save, verzögertem Client, Rückkehr ins Menü und erneutem Start. `MissionStart` erst nach vollständiger Managed-Initialisierung; fehlende erste Simulation ist zu diesem Zeitpunkt zulässig.
5. Optionen sowie Sieg-/Niederlagenanzeige dürfen keine weiterhin aktive Karte beenden. Tatsächlicher Szenenwechsel und akzeptiertes Verlassen müssen genau ein `MissionEnd` erzeugen.

Auswertung anhand `session`, `source`, `mode`, `phase`, `reason`, `started` und der modbezogenen Gate-Meldungen, ergänzt um sichtbare Featurefunktion. Automatisierte Tests ersetzen diese Laufzeitbeobachtungen nicht.
