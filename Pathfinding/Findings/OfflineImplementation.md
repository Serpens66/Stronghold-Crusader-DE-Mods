# Pathfinding: Offline-Implementierung und verbleibende Abnahme

Stand 13.11.2026, Modpack-HEAD `1d10d7ddf56eb02eefad502b8051c3ed40f6358c`
zuzüglich autorisiertem Arbeitsstand. Keine Spielsteuerung, Installation,
Versionsänderung, Veröffentlichung oder Commits. Feuer/Pest bleiben in
`EngineOptions.Capture` gesperrt; ihre Einstellungsbereiche bleiben verborgen.
Die [TODO](../src/TODO.md) unterscheidet abgeschlossene Offline-Teilverträge,
vorbereitete Spieltests und offene Abnahmen. Historische Nachweise bleiben erhalten.

## Korrigierte und abgesicherte Produktionsverträge

- **B1:** Solo- und Fluchtfälle derselben Einheit dürfen verschachtelt gespeichert
  sein. Restore erhält beide Eigentumsschichten, statt den Solo-Besitz vorzeitig
  zurückzugeben. Bei abgeschaltetem Solo-Wächter übernimmt die passende geborgte
  Flucht den ursprünglichen Split-Besitz. Feuer/Pest × Hinweg/sicheres Warten/
  Rückkehr werden gegen den ununterbrochenen Lauf geprüft; deaktivierte Optionen
  und Identitätswechsel sind einbezogen. Die bestehenden Save-Identitäten und
  lesbaren Datensatzformate bleiben erhalten.
- **Q3:** `SoldierTribeMoves.TryMoveSoldier` prüft die vollständige für geeignete
  Soldaten relevante Vanilla-Aufnahme von `0x11B520` vor dem Befehl. Ein alter
  Zustand 101/122 belegt bei ausgeschlossenen Mitgliedern keinen neuen Erfolg.
  Tatsächliche Spielmember wurden an der installierten Assembly auf Typ/Offset
  geprüft; Vertrag und Pflicht zur Neuprüfung stehen in
  [UpdateToNewDLL](../UpdateToNewDLL.md).
- **Warten:** `FireWaitIndefinitely`/`PlagueWaitIndefinitely` sind getrennte,
  host-synchronisierte Optionen, beide standardmäßig `false`. Begrenztes Warten
  endet mit Abbruch; unbegrenztes Warten endet bei zulässigem sicherem Umweg oder
  ersetzendem Auftrag/Identitätswechsel. Keine Frist erlaubt bewusstes Durchqueren
  der Gefahr. Rückkehrfristen und APIShareds `MaximumWaitTicks=0` bleiben getrennt.
  Settings, Presets, versteckte XAML und Sprachdateien sind konsistent ergänzt.
- **KI:** Gefahrenkosten wirken in vorhandenen Suchaufträgen. Neue automatische
  Stop-/Fluchtbefehle bleiben auf geeignete menschliche Soldaten beschränkt.
  Veraltete KI-Fälle werden beim Laden beziehungsweise Verarbeiten beendet und
  ihr Gruppenbesitz bereinigt. Eine KI-Freigabe erfordert Zustandsaudit und Spieltest.
- **G8:** Unterstützte Pathfinding-Bewegungsbefehle auf ein für den echten
  Gruppeneigentümer verbotenes Ziel werden vor Auftrag-/Queueänderungen abgelehnt.
  Die Spielerwrapper-Änderung an Tribe `+0x56C` wird dabei zurückgenommen.
  Verschachtelte Ablehnung erhält den Elternauftrag. Der spätere Engine-Subscriber
  ignoriert übersprungene Pre-Ereignisse; der Extender sendet dann kein Post.
  Start-im-Verbot bleibt verlassbar; öffentliche APIShared- und fremde Vanilla-
  Befehlsverträge bleiben erhalten.
- **T7:** Bei dauerhaft knappem Probe-Restbudget wurden hohe IDs unbegrenzt
  Wartender nachweislich benachteiligt. Eine wiederverwendete Arbeitsliste rotiert
  deterministisch anhand des nativen Ticks, sobald eigene unbegrenzte Wartefälle
  vorhanden sind. Budget/Suchgrenzen bleiben gleich; gleicher Bedienverlauf nach
  Capture/Stage/Apply und wiederhergestelltem Tick ist geprüft.

Vorher fehlgeschlagene Produktionsregressionen und anschließende Ergebnisse:
[B1 vorher](../../.work/a/pf-offline/001/b1-before.log),
[B1 ohne Solo-Wächter vorher](../../.work/a/pf-offline/001/b1-watch-disabled-before.log),
[B1 danach](../../.work/a/pf-offline/001/b1-after.log),
[Q3 vorher](../../.work/a/pf-offline/001/q3-before.log),
[Q3 danach](../../.work/a/pf-offline/001/q3-after.log).
Diese Logs sind datierte Entwicklungsnachweise; frühere Zählstände sind historisch.

## Geprüfte Grenzen und Optimierungen

[OfflineHazards](OfflineHazards.md) beschreibt 1273 Prüfungen der vollständigen
Produktionsscanner mit kontrollierten Spielgrenzen, den Höhen-/Immunitätsabgleich,
1206 native APIShared-Schadens-/Backendprüfungen, Scheduler-/Journalreihenfolge
und isolierte Lastfälle. Kein bestätigter Pestmodellfehler: tatsächliche dynamische
Höhenübergänge bleiben offen. Scanlogik wird nicht als Testmodell nachgeschrieben;
native Bindung und echte Callbacks sind durch diese Fixtures nicht abgenommen.

[OfflineOptimization](OfflineOptimization.md) dokumentiert den vollständigen
Feindsuch-Audit und tatsächlich ausgeführte G1/G2-Assemblerkandidaten im installierten
RedBird-Backend. 7.095.335 Gleichheits-/Maschinenprüfungen bestanden, aber inklusive
Einbaukosten waren die Kandidaten langsamer. Sie bleiben ausschließlich im
Offline-Testprojekt; Originalcode bleibt aktiv. G3 benötigt weiterhin einen
vollständigen Writer-/Reihenfolgevertrag. G4–G7 wurden anhand heutiger Produktions-
methoden untersucht und teilweise isoliert profiliert, ohne neue unbelegte
Produktionsoptimierung. G9 besitzt eine ausführbare Vorbereitungsmatrix; keine
aktuellen Ingame-Häufigkeiten oder Nettogewinne werden daraus abgeleitet.

## Integrierte Prüfungen und Nachweisgrenzen

Vom Workspace-Root:

    dotnet run --project Pathfinding/tests/Engine/Pathfinding.Engine.Tests.csproj --no-restore -- .

Bestanden am abschließenden Arbeitsstand: **215580 Runtime-Assertions**, zusätzlich
**1273 Scanner**, **546 Settings**, **30 Wartemodus-** und **9 integrierte
Veto-Subscriberprüfungen**. Ebenso bestanden die vorhandenen 84032 unabhängigen
Suchprüfungen, 289388 Fast-Feldprüfungen, 1103 Zustands-/Saveprüfungen und
1469340 Cursorvergleiche. Der neue Friendly-Movement-Vertrag bestand **18** Prüfungen.
Siehe [Engine-Protokoll](../../.work/a/pf-offline/001/engine-final.log).
Zwei bestehende CS8321-Warnungen betreffen ungenutzte lokale Testhelfer.
Native Kernprüfungen und SDK-/Buildnachweise liegen im selben
[Nachweisordner](../../.work/a/pf-offline/001).
Die vorhandene Host-/Client-Presetsuite wurde mit klassischem MSBuild für net481
gebaut und als EXE ausgeführt: bestanden. Scoped Shared-Boundaries und der
Preflight für alle 21 Sprachdateien bestanden ebenfalls.
Der vollständige lokale APIShared-Build `/source:working /tests:full /noinstall`
bestand. Seine isolierte Uploadfixture benötigte zwei Anpassungen an bestehende
Werkzeugabhängigkeiten (WorkshopPublisherScope kopieren und das bereits erforderliche
MainSource-Feld im Fake liefern); keine Produktions- oder Freigabeprüfung wurde
abgeschwächt. Die frische Windows-PowerShell-5.1-Fixtureprüfung bestand separat.
SDK-HEAD `d91fd86a6fbecd9d400062b5f44f534e52bbb00f`, Arbeitsstand und SHA-256
der Assemblies sind in `sdk-identity.json` festgehalten.
Der DEVDIAG-Build `/source:working /apishared:dev /devdiag /notests /noinstall`
bestand einschließlich Paketprüfung. Die relevanten Tests liefen separat am selben
Quellstand. Der **normale vollständige Treiberlauf**
`/source:working /apishared:dev /noinstall` bestand am 13.11.2026 um 19:28:18
einschließlich sämtlicher vorhandener Treibertests, Hauptkompilierung, beider
nativer Kerne und aller 25 Paketassets (`pathfinding-normal-final.log`, Exit 0).
Die normale Assembly besteht außerdem die DEVDIAG-Ausschlussprüfung.

Ein dabei bestätigter Buildfehler ist korrigiert: Der Dispatcher bereinigt das
Paket, aber `Movement.Tests` referenziert dessen Haupt-DLL als `PathfindingModel`.
Die Suite lief zuvor vor der Hauptkompilierung und scheiterte deshalb mit CS0430
(`pathfinding-normal-order-before.log`). Im Treiber läuft nur diese abhängige
Suite nun nach der Hauptkompilierung; `/notests`, Fehlerabbruch und Paketprüfung
bleiben erhalten. Der vollständige tatsächliche Treiberlauf besteht nach dieser
Änderung. Auch der direkte normale Rebuild und die separat ausgeführten Restprüfungen
bestanden; keine Erwartung und kein Prüfgate wurde abgeschwächt.
Der Testhost musste außerhalb der Sandbox wiederholt werden, nachdem er ohne
Ausgabe/CPU auf dem lokalen Verbindungskanal wartete. Ein späterer Versuch brach
korrekt wegen eines inzwischen laufenden Spiels ab; erst nach dessen Ende wurde
erneut gebaut. Kein Spiel wurde dafür gestartet, gesteuert oder beendet.

Der Gefahren-Testmod kompiliert und besteht **97** Konfigurations-/Reflexionsprüfungen
gegen die frisch gebaute normale Haupt-DLL (`testmod-final.log`).
Verbleibende Compilerwarnungen betreffen insbesondere vorhandene Assembly-
Versionskonflikte (Mono.Cecil/Serilog), ungenutzte Fixture-/Diagnosefelder und nicht
erreichbare NuGet-Sicherheitsmetadaten (NU1900); kein neues Spielergebnis daraus ableiten.

Alle ausgeführten Buildwege hatten Installation deaktiviert. Eine Zwischenkontrolle
der zuvor erfassten 74 installierten Dateien fand dennoch vier geänderte
APIShared-Dateien: Runtime-DLL/PDB sowie Workshop-/Bootstrap-Paarmetadaten.
Die installierte DLL wurde um 19:18:01 geschrieben und hat einen anderen Hash und
Umfang als unser um 19:15:02 gebautes lokales SDK. Pathfindings installierte Dateien
sind unverändert. Herkunft des parallelen Installationsstands ist hier nicht belegt;
keine Rücksetzung fremder Änderungen. Im abschließenden Vergleich sind sieben
APIShared-Dateien abweichend (zusätzlich Content-DLL/PDB und XML); Pathfinding
weiterhin unverändert. Details und letzte Schreibzeiten in `installed-comparison.json`.

## Vorbereitung und weiter offene Arbeit

- **R1/T2–T10:** Der getrennte [Gefahren-Testmod](../../Testmods/PathfindingHazardTest/ACCEPTANCE.md)
  bereitet sechs konkrete Beobachtungsszenarien vor; daraus folgt noch kein
  bestandener Spieltest. Er aktiviert nur mit eigener gültiger Laufanforderung und
  vorbereitetem Save im echten Simulationsthread. Kein PASS vor echter Bewegung
  beziehungsweise Warte-/Abbruchfolge und bestätigter Wiederherstellung. Seine
  Erfolgs-, Fehler-, Cancel- und pausierten Cleanup-Pfade bleiben im Spiel abzunehmen.
- **Q2/Q4:** echte Höhenübergänge, native Maskenübernahme, Erkennung nach dem Scan,
  tatsächlich gelaufener Flucht-/Rückweg, Schaden und Animation prüfen.
- **T4–T8:** reale Auftrags-/Formations-/Queue-, Save-/Editor-, optionale Dienste-
  und Lastmatrix ausführen. Besonders gleichzeitige Fluchten haben noch keinen
  gemeinsamen Suchbudgetdeckel; isolierte CLR-Zeiten sind keine Mono-/Spielzeiten.
- **Q5:** `.work/a/pf-opt/001/native-dissolve-audit.md` und
  `.work/a/pf-startup/001/fix-result.md` erneut ausgewertet. Gesunder AI110 mit
  Life=3 ohne vorherigen Pfad/Flags liefert keinen kausalen Pathfinding-Nachweis.
  Kein spekulativer Fix. Vergleich mit/ohne Mod muss den ersten Zustandswechsel
  einschließlich vorheriger Route/Flags erfassen. BFQoLs behobene ID-0-Startmeldung
  ist ein anderer Fall.
- **T9/T10/R2:** echte Zweirechner-MP- und manuelle Sichtabnahme bleiben offen.
  Runtime-Sperre und ausgeblendete Einstellungen dürfen erst nach bestandener
  passender Abnahme gemeinsam freigegeben werden.

Die G9-Matrix enthält erforderliche Hostsettings, Runnerparameter und Messgrößen;
sie setzt Settings nicht selbst und startet nichts. Stresssave/Szenario vor einem
späteren Lauf prüfen. Neue Spielmessungen benötigen identischen Quell-/SDK-/Save-
Stand und getrennte uninstrumentierte Kontrollen. Version, README, Veröffentlichung
und Installation sind keine Schritte dieses Offline-Auftrags.
