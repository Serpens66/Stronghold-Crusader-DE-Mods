# Abschlussabnahme: Einheitenrettung in BugfixesAndQoL

**API-Integration, 27.11.2026:** Die Rettungs-API einschließlich Trampolinprüfung ist in `APIShared/`, Branch `dev/Serpens66`, Commit `421c1ed0457de7a8b7dfcbff160778a4f1f4d070` integriert. Dies ist derselbe getestete Quellstand; 1452 dateibasierte Build-Eingaben stimmen mit dem bisherigen vollständigen Buildnachweis überein. Buildproof, Buildschritte und TRX-Dateien sind unter `.work/a/api-consolidation/001/` erhalten. Die alte Rettungs-Arbeitskopie wurde nach erfolgreicher Integrationskontrolle mit `git worktree remove` vollständig entfernt. Ältere Pfade und Paketidentitäten unten bleiben historische Nachweise. Die Foreign-Control-Arbeitskopie ist ausdrücklich nicht integriert.

Stand:27.11.2026, nach Lauf215. Der vereinbarte Singleplayer-/Editor-Umfang N0–N8 ist abgeschlossen. Dies ist keine Multiplayer- oder Veröffentlichungsfreigabe.

## Gemeinsamer Versetzungsdienst: Nachprüfung vom 27.11.2026

Der aktuelle Arbeitsstand BQoL1.0.250 benötigt APIShared0.79.0. APIShared besitzt jetzt den gemeinsamen nativen Versetzungsdienst; der Lord-Schutz ist eine typgefilterte BQoL-Vetoregel. Die Rettung sowie ihre indirekten IdleEscape-/Worker-Verbraucher nutzen geprüfte öffentliche Versetzungsanfragen mit Welt- und Originalidentität. Der frühere private Lord-Hook und dessen geliehene Anbindung sind entfernt. Gebäudeversetzung und der unterschiedliche Fixes-Reiterhelfer sind ausdrücklich keine Verbraucher dieses Dienstes.

Der Integritätsfehler ist korrigiert: Der Zähler neu gemeldeter Fremdpatches ist kein Gesundheitsnachweis mehr. Die Rettung prüft ihre eigenen veröffentlichten Abhängigkeiten und bleibt nach einem Integritätsverlust deaktiviert; bereits protokollierte Fehler oder später wiederhergestellte Bytes aktivieren sie nicht erneut. Unbeteiligte Hooks beeinflussen diese Entscheidung nicht.

Der Plan wurde vor der Umsetzung unabhängig geprüft. Die unabhängige Abschlussprüfung fand einen Fehler bei teilweise fehlgeschlagener Metadateninitialisierung; dessen Korrektur und Gegenproben wurden erneut unabhängig geprüft. Anschließend blieben keine bestätigten Befunde offen. Der vollständige offizielle SDK-Build ohne Installation bestand mit2584 erfolgreichen Tests und einer bestehenden optionalen Auslassung;43 öffentliche API-Zeilen wurden ergänzt, keine entfernt. BQoL-Lordtests82, Settingsprüfung einschließlich968985 nativer Gegenproben und die Rettungsprüfungen einschließlich9 neuer Integritätsgegenproben bestanden. Der vollständige normale BQoL-Build mit ausgewähltem SDK-Paket und ohne Installation bestand abschließend ebenfalls (19:26:18 CET). Zwei bereits veraltete Testannahmen wurden nach Prüfung aktualisiert: der verbliebene Notification-Age-Vertrag wird direkt ausgeführt, und das AIV-Leserinventar enthält den bestehenden geprüften Rettungs-Footprintadapter. Die zusätzliche unabhängige Prüfung bestätigte, dass weiterhin geltende Garantien erhalten bleiben.

Der kontrollierte Spieltest `28e12433c18d4b8d962e2018637c0537` bestand19/19 Prüfungen mit normalen Artefakten und0 Error/Fatal-Logeinträgen: tatsächliche Versetzung mit ursprünglicher Identität/Gesundheit/Verband und kohärenter Belegung, Ablehnung unsicherer/veralteter Anfragen, typgefiltertes Veto, Lord-Schutz an/aus in getrennten Missionen sowie Save/Load-Weltwechsel. Eine echte, ununterdrückte KI-AIV-Zugbrückenplatzierung schloss den ursprünglichen Soldaten ein; ausschließlich die Produktionsrettung befreite ihn. Ein anschließend neu erteilter gewöhnlicher Bewegungsbefehl erreichte sein Ziel. Das ist kein zusätzlicher Nachweis zur Wiederaufnahme eines schon vorher bestehenden Befehls.

Die Spieltransaktion stellte Installation, Einstellungen und Nutzerdaten wieder her; der unsichtbare Testdesktop wurde geschlossen, der Eingabedesktop blieb Default. Der temporäre Testmod wurde nach gesicherter Quellarchivierung entfernt. Kompakte Quellen, genaue Artefaktidentitäten und Ergebnisgrenzen: [Spielnachweis](../../.work/a/relocation-game/001/acceptance.md), [SDK-Nachweis](../../.work/a/relocation-sdk/001/result.json), [Arbeits- und Prüfplan](../../.work/a/unit-relocate/001/plan.json). Diese gezielte Migration wiederholt nicht die gesamte historische Editor-/Arbeiter-/Keepmatrix und erteilt keine Multiplayer-Freigabe. Die ursprüngliche Abnahme unten bleibt als datierter Nachweis erhalten.

## Verhalten und Besitz

- Eine standardmäßig aktive Hostoption `EnableTrappedUnitRescue`; wirksam mit `EnableMod`. Der Clientmaster steuert diese Hostregel nicht. Die separat entfernbare Fixes-Reiterkorrektur folgt demselben Schalter und wird im Tooltip erwähnt.
- Nachweislich durch beobachtete Bau-/Geländeänderungen feststeckende gewöhnliche Bauern und menschliche Wirtschaftsarbeiter werden regulär entfernt. Waren/Inputs werden nicht erstattet; natürliche Neubesetzung bleibt bedingt und zeitversetzt.
- Soldaten sowie ursprüngliche Geräte/Besatzungen bleiben erhalten; sichere lokale Befreiung und weiterhin gültige Befehlsabsicht werden geprüft. Gesunde/CurrentSafe-/Transient-/Rekrutierungs-/fremde Identitätszustände erhalten keine blinde Entfernung.
- APIShared besitzt gemeinsame Ereignisse/Hooks und jetzt den gemeinsamen Versetzungsdienst; Lord-Schutz und Rescue nutzen denselben Besitzer ohne zweiten Relocationhook und kein dauernder Scan aller Einheiten pro Tick. Ereignis-/Queue-/Aktionsbudgets bleiben verbindlich.
- Der ursprüngliche Testmodkern, dessen Einstellungen und EarlyProvider sind entfernt. Nach abgeschlossener Integration wurde auch die separate Ingame-Automation auf Nutzerauftrag entfernt; die produktiven Offlinefixtures und die Performance-Logprüfung liegen bei BQoL. Nativerkenntnisse stehen in der Baseline.

## Verifizierte Abnahme

| Anforderung | Autoritative Belege |
|---|---|
| Native Klassifizierung, Entfernung, Belegung, Mitarbeiter-/Lebensgrenzen | Vollständiger featurebezogener Audit in der aktuellen Native-Baseline;24 Typen, Rekrutierungs-/Slotreuseguards; Vanilla-Wissen getrennt von Mod-Nachweisen; Knowledge-Lint148 Dokumente/9 Themen/0 Fehler |
| Funktionale Produktionsintegration |211/840a4a85a511: alle64 Fälle gemeinsam; Bergfriedrotationen/Schritte, Brücken, menschliche/KI-Gruppen, ursprüngliche Geräte/Crew, Quellen, Editor und zivile Entfernungs-/Gegenproben |
| Wirkliche Host-/Client-/Default-/OFF-Grenzen |212/8435768fbe68 und normales214: fünf tatsächliche AIV-Profile, Hostaktivierung und Nativegate1/0/1/0/1 |
| Rückstände, Fairness, sichere Fortsetzung und Save/Load |600 aktive/passive Originale;600 kanonische Pendingeinträge nach echtem Laden, anschließend600 ursprüngliche Ziele; maximal8 allgemeine Aktionen jeTick;211/214 und vorhandene9999er-Offlinegrenzen |
| Fixes-/Hookverträge |27 Prüfgruppen (25 Purefixtures und2 Maschinenprüfgruppen),263 ausführbare Worker-Guard-Prüfungen,116 Scannerprüfungen,18 produktionsgebundene Decoder-Gegenproben; relevante IL-/Helfer-/Live-Stubverträge statt pauschalem Fixes-Dateihash |
| Korrigiertes SDK | Interner Trampolinprüffehler behoben: bekannte Originalinstruktionen inklusive Rücksprung deterministisch wie RedBird/Iced neu encodieren und vollständig vergleichen.37 gezielte Prüfungen einschließlich echter Backend-Prologe und vollständiger offizieller APIShared-Build; tatsächliches Notification-READY213/214/215 |
| Effizienz im vereinbarten Profil |213/0ad2d176595b:9871 Originale,1000 Walker,8 ABAB-Fenster,4 eigene Ladevorgänge,5 genaue Ready-Handoffs,4 Gruppenendpunktprüfungen; aktuelle181-Prüfungen-Logqualifikation mit ausdrücklich ausgewiesener Diagnosegrenze |
| Normales Endpaket mit relevanten Begleitmods |214/0714d5b63904:14 Fälle mit Fixes/Pathfinding/CastlePlanner,0 Error/Fatal, Notification und PitchLine READY |
| Standalone mit harten Abhängigkeiten |215/29cd44bd7f5b:4 Fälle nur mit Extender/APIShared/BQoL plus Automation; Fixes ausdrücklich abwesend,0 Error/Fatal, Notification und PitchLine READY |
| Artefakt- und Rücksicherungsgrenzen | Normale Main-/Acceptance-Builds und NoDevGate bestanden; keine NoJesters-/Capture-/DevDiag-Helfertypen, kein zweiter Runtime-/Scannerbesitzer. Alle Spieltransaktionen restauriert, Testdesktops geschlossen, EingabedesktopDefault |

Ausführliche ursprüngliche Laufprotokolle liegen unter `.work/a/rescue-game/`, kompakte Endnachweise unter `.work/a/rescue-sp/001/`. Der [Produktions-Nativevertrag](../src/TrappedUnitRescue/UpdateToNewDLL.md) und die [Native-Baseline](../../_inspect/CrusaderDE-Native-Baseline/CURRENT.md) sind die aktuellen fachlichen Einstiege. Frühere Pläne und Zwischenstände sind über den unten genannten Git-Stand verfügbar.

## Performancegrenzen

Die künstliche Jester-freie Diagnosewelt ist kein allgemeiner Normalwelt-/FPS-Nachweis. Ein vollständig verifizierter früher PerfProbe-Monitorwrapper-Konflikt deaktiviert dort die unabhängige Pitch-/Grabenlinien-Funktion; der aktuelle Logprüfer weist genau diese frühe Diagnosegrenze aus und lehnt andere, spätere oder veränderte Fehler ab. Die normalen Endläufe214/215 bestätigen die normale Kombination ohne PerfProbe und ohne diesen Fehler.

OFF/ON-Median der beiden Fenstermittel: Ruhe5.011/4.8025ms, Bewegung5.3465/5.3395ms. Zeitliche Schwankungen verhindern eine kausale Verbesserungs-/reine Overheadbehauptung. Alle3 Aufrufe über50ms lagen im letzten ON-Bewegungsfenster (max71.220ms), keiner über100ms; die Ursache bleibt unzugeordnet. In den Messfenstern gab es keine eigenen Rettungs-/Entfernungsaktionen. Dichte tatsächliche Rettungsarbeit ist durch die getrennten600er-Fälle abgesichert.

## Quellen, Pakete und verbleibende Grenzen

Die ursprüngliche Abnahme prüfte BQoL-Version1.0.249 mit mindestens APIShared0.78; der aktuelle Versetzungsdienst benötigt BQoL1.0.250/APIShared0.79.0. Der geprüfte SDK-Quellstand ist jetzt im kanonischen Checkout `APIShared/` auf `dev/Serpens66` integriert (Commit und gesicherte Buildnachweise siehe Integrationsvermerk oben). Die folgenden Paketidentitäten gehören zur ursprünglichen Spielabnahme.

Normale DLL-Identitäten (vollständige SHA256/Paketpfade im [Endnachweis](../../.work/a/rescue-sp/001/n8-final-normal-summary.json)):

| Paket | MVID |
|---|---|
| BQoL normal |3248c996-023d-4779-8d28-e5dcd98f9884 |
| Acceptance normal |6caae01c-6d76-41da-b4a7-2c7e547845e6 |
| APIShared0.78 |beabf9e1-d26f-4223-9a0a-a1726aa3eed8 |

Keine echte MP-Rettung freigegeben. Kein neuer Vollscan für bereits zuvor feststeckende Einheiten alter Saves ohne kausalen Vorherbeleg. Unbekannte relevante Native-/Fixes-Verträge bleiben fail-closed; ausgelassene Aux-Kettenvarianten werden nicht als bestanden ausgegeben. Warenverlust und bedingter Bevölkerungsausgleich sind akzeptierte Produkteigenschaften.

Kein Commit, Push, Release oder dauerhafter Paket-/Settingswechsel wurde durch diese Abnahme vorgenommen. README blieb unverändert.

## Laufzeitbudgets und Wiederholungsprüfung

Bis zu9999 kausal belegte Originalidentitäten werden unterstützt. Beobachtungs-/Queuearbeit ist auf32 Besuche und neue allgemeine Aktionsversuche auf8 je Simulationstick begrenzt. Ressourcenwartefälle bleiben begrenzt gespeichert. Save/Load erhält Originallebenszeit, Auftragsautorität und geometrische Vorherbelege; es erteilt keine neue Rettungsautorität für beliebige alte Fallen. Die600er-Ingamefälle und9999er-Offlinefälle prüfen verschiedene Grenzen und ersetzen einander nicht.

Nach Änderungen an Rettung, SDK, Fixes oder dem nativen Bild: erst die vorhandene BQoL-Testsuite einschließlich Guard-/Scanner- und Log-Gegenproben ausführen; danach betroffene Spielpfade separat prüfen. Die relevante Spielmatrix umfasst Bau-/Geländequellen, Human-/KI-Gruppen, ursprüngliche Geschütze und Ingenieure, Bauern/Arbeiter samt gesunden Gegenproben, tatsächliches Native0, Pending-Save/Load, neue/geladene Editorwelt sowie Host-/Clientmaster/Option OFF/ON. Keepfälle umfassen vier Reitertypen, drei Bergfriede, vier tatsächliche Lagerfeuerrichtungen, Stillstand und begonnene Schritte. KEEP1 ist ausdrücklich eine No-roof-Gegenprobe ohne positive Dachrettung; die positiven Dachfälle betreffen KEEP2/KEEP3. Die historische Matrix enthält pro eigener Einstellung48 stehende und49 begonnene Fälle, davon16+16 KEEP1-Negativfälle; Identität, Gesundheit, Verband, Ziel, Kachelbelegung, blockierten Ausgang und ausbleibende zweite Versetzung im folgenden Fixes-Tick prüfen. CastlePlanner kann die Rotation verändern: tatsächlich vorhandene Geometrie prüfen. Das ist keine Freigabe des bislang nicht getesteten CastlePlanner-UI-Rotationsablaufs oder besetzter nichtleerer Aux-Ketten.

## Fixes-Folgeprüfung und Entfernbarkeit

Der geprüfte Fixes1.30.0-Stand verwendet beim Keep-Reitercode einen veränderlichen gemeinsamen Scanner in verzögert erzeugtem Code; die tatsächlich emittierte Zieladresse kann dadurch auf `0xE2610` statt auf den vorbereiteten Zweiargument-Helfer `0x187110` zeigen. Der tatsächliche unveränderte Vergleichslauf088 ließ vier Reitertypen auf KEEP2/Rotation0 stehen. Dies ist ein reproduzierter begrenzter Negativnachweis, keine Aussage über jede Fixes-Version oder alle möglichen Fälle. Korrigierte149 und gegatete208/211 prüfen die dokumentierte Matrix; kompakte Mod-/Vanillagrenzen stehen im Baseline-Audit.

Bei einem Fixes-Update zuerst die relevanten Installer-/Closure-/Hilfsmethoden und den tatsächlich publizierten CALL neu prüfen, nicht nur Versionsnummer oder Dateihash. Bekannte scalar-korrigierte Varianten lässt unsere Insel unverändert. Sobald das unveränderte Original die relevanten Fälle zuverlässig rettet und die Kombination erneut geprüft ist, die vier `FixesMountedScanner*.cs`-Dateien und ausschließlich ihre Registrierungs-/Gate-/Testanbindung zusammen entfernen. Die allgemeine Rettung und der unabhängige Worker-Guard bleiben getrennte Verantwortlichkeiten. Keine fremden Einstellungen umschreiben. Bis dahin folgt die Korrektur demselben `EnableMod && EnableTrappedUnitRescue`-Gate; OFF führt den Original-Fixes-Aufruf aus.

## Entfernung des abgeschlossenen Testmods

Am27.11.2026 wurde die Entfernung von `Testmods/TrappedUnitRescueTest` ausdrücklich beauftragt. Die produktiven25 Offlinefixtures liegen unter `BugfixesAndQoL/tests/TrappedUnitRescue.Tests/Fixtures`; die Performance-Logprüfung samt Gegenproben bleibt ebenfalls unter `BugfixesAndQoL/tests`. Der vollständige Testmod-Ordner einschließlich automatisierter Ingame-Szenarien, Launcher, zusätzlichem Plugin und reproduzierbarer Ausgaben wurde nach Abhängigkeits-/Dateiinventur entfernt. Künftige neue Ingame-Automation bleibt gemäß Workspace-Regeln ein separater Testmod und gehört nicht in die Produktionsruntime.

Vollständige historische Quellen, Audits, Matrizen und Szenarien sind in Modrepo-Commit `5c2fe284c` erhalten: beispielsweise `git show 5c2fe284c:Testmods/TrappedUnitRescueTest/SP-ACCEPTANCE.md` bzw. `git show 5c2fe284c:Testmods/TrappedUnitRescueTest/UpdateToNewDLL.md`. Diese historischen Quellen enthalten auch inzwischen ersetzte Ansätze, insbesondere Arbeiter-Jobfortsetzung und eine frühere unabhängige Reiteraktivierung; der aktuelle Produktvertrag oben und der Produktions-Nativevertrag haben Vorrang. Keine vollständige Ersatzkopie des Testmods ist notwendig. Bereits erfasste Spiel-/Buildnachweise außerhalb des Testmods bleiben erhalten; ihre ursprünglichen Pfade und Paketidentitäten sind historische Angaben.

### Prüfung nach Entfernung

Die produktive Suite kompiliert nach vollständiger Löschung des Testmod-Ordners und besteht27 Prüfgruppen (einschließlich116 Scanner- und263 Worker-Guard-Prüfungen). Alle25 verschobenen Fixturequellen sind inhaltlich unverändert. Die verschobene Performance-Logprüfung besteht181 Gegenproben in einem frischen Windows-PowerShell-Prozess und ist im BQoL-Build eingebunden. Der kanonische APIShared0.78-Stand wurde vollständig ohne Installation gebaut:2572 Tests bestanden, ein optionaler Consumer-Scan übersprungen; zusätzliche native und Paketprüfungen bestanden. BQoL-Verbrauchergrenzen und Baseline-Knowledge-Lint bestehen. Nachweise: `.work/a/rescue-retire/001/test-migration-result.json` und `retirement-inventory.json` mit zugehörigen Logs. Keine neuen Ingame-Tests, Installation oder Veröffentlichung. Die BQoL-Paket-DLL wurde für die reine Entfernung der veralteten Friend-Assembly-Deklaration nicht neu gebaut; dies ist eine Quellbereinigung ohne neue Laufzeitfunktion, die nächste reguläre Paketerzeugung übernimmt sie.

Eine zusätzlich ausgeführte, über BQoL hinausgehende UnitAccess-Quellprüfung lehnt den direkten Gebäudezugriff in `APIShared/src/Economy/Storage/NativeStorageRuntime.Delivery.cs:93` ab (`bqol-boundaries.log`). Die Stelle stammt unverändert aus Commit7bf02bc; das kanonische SDK-Arbeitsverzeichnis ist sauber. Die eigentliche BQoL-Prüfung einschließlich expliziter Quelllinks besteht (`bqol-consumer-boundaries.log`). Dieser separate Scannerbefund wurde hier nicht korrigiert und ist kein Nachweis eines Laufzeitfehlers oder einer Regression durch die Testmod-Entfernung.

## Unabhängige Performance-Nachprüfung, 27.–28.11.2026

Ausgangspunkt ist `develop` auf `284852391` mit dem ersten bereits integrierten Performancefix. Die kanonische native DLL bleibt FBCB9319; APIShared auf `a9ae32f` / 0.79.0 ist unverändert. Der aktuelle normale Kandidat ist `035A240E…1079B73` (vollständige DLL-/PDB-Identitäten im Buildnachweis und den folgenden Spielprotokollen). Die finalen normalen AN/AUS-Serien018 und019 sind vollständig bestanden und zurückgesetzt. Die früheren Zwischenstände werden nicht mit diesem Build gleichgesetzt.

### Umgesetzte Kostenkorrekturen und unveränderte Grenzen

- Der native Built-Cooldown umfasst alle sechs Treppen. Die zusätzliche Verzweigung für Stair5/6 ist im AIV_BUILD_RANGE-Audit gegen die tatsächliche kanonische DLL belegt. Weitere dort dokumentierte Ressourcen-/Kapazitätsgates wurden nicht ohne Häufigkeitsnachweis in die Runtime übernommen. Der Originalaufruf läuft weiterhin.
- Teilflächen laufen direkt in eine private Union und werden einmal abschließend sortiert. Arbeitsbudget je Teilanfrage, globale Kachel-/Aliasgrenzen, native Lesebudgets und vollständige Cleanup-/Farm-/Verbundflächen bleiben erhalten. Normale Quadrate benötigen keine Hilfslisten oder Hashsets für die Offset-Duplikaterkennung. Die Snapshot-Vorbereitung entfällt erst nach vollständig validierter leerer Discovery.
- Native Planung und Discovery behalten je Runtime höchstens eine vollständig geleerte private Session. Verschachtelte Aufrufe erhalten getrennte Instanzen. Alle nativen Werte, Indizes, Graphreferenzen und Zähler werden vor Wiederverwendung verworfen; nur Containerkapazitäten bleiben. Ergebnislisten und Discovery-Nodes bleiben frisch. Native Retention folgt dem tatsächlichen Arbeitsbudget. Die erste feste Grenze von 8192 Reads wurde verworfen: Schon 1612 gültige Farmen erzeugen 8194 Arbeitseinheiten und würden sonst regelmäßig dieselben Puffer neu anlegen.
- Completion-Closures entstehen erst bei tatsächlich benötigten Receipts. Reader-Delegates werden einmal je Runtime gebunden. Die stabile Snapshot-Filterung bewahrt Referenzen, Reihenfolge und die ursprüngliche Unsupported-/Ownerregel ohne Predicate-Closure. Erschöpfte Diagnosebudgets vermeiden zusätzlich den Aufbau nicht mehr auszugebender Fehlermeldungen.

Die produktionsquellverlinkte Rettungssuite besteht vollständig, darunter 961 Footprint-, 889 Native-Footprint- und 150 Discoveryprüfungen. Die vorhandene Discoverymatrix läuft mit und ohne Pool. Gegenproben umfassen frische native Werte, Alias-/Budgetfehler, Fehler und anschließenden Erfolg, getrennte verschachtelte Sessions und unveränderte frühere Ergebnislisten. Filtertests vergleichen mit dem ursprünglichen `RemoveAll`; die 32er-Loggrenze und ihr Weltreset bleiben geprüft. Die tatsächliche normale Assembly bestätigt entfernte Entry-Closures und per-Capture-Delegatekonstruktionen. Vorprüfungen, normale Builds, NoDev-Assemblyprüfung und unabhängige Reviews bestehen. Relevante Logs: `.work/a/rescue-perf-audit/001/`.

Zwei getrennte CLR-Prozesse mit wechselnder Reihenfolge liefern identische Ergebnisse und Lese-/Budgetzähler. Beispiele: Brückenplanung 31.112 → 2.480 Bytes/Aufruf; 3999-Farmen-Planung gegenüber der verworfenen 8192-Grenze etwa 716 KB → 1232 Bytes; leere 13×13-Discovery 23.424 → 64 Bytes. Dichte 64×32-Discovery sinkt von etwa 1,027 MB auf 242 KB. Kleine Aufrufe nach großen Puffern können geringfügig langsamer werden (etwa 0,1–0,5 µs im CLR-Test). Dies ist kein Mono-/FPS-Nachweis. Der vorgeschlagene zusätzliche XY-Hashcache war langsamer und speicherintensiver und wurde nicht übernommen. Quellen, tatsächliche Eingabehashes und beide Prozesse liegen getrennt unter `batchbench`, `sessionbench`, `sessiondensebench`, `discoverypoolbench` und dem verworfenen `discoverybench`.

### Zeitliche Zuordnung der verbliebenen Bildspitze

Die normalen Kontrollen ändern nur `EnableTrappedUnitRescue` vor Spielstart, prüfen die tatsächlich aktive Runtime einmal vor dem Messfenster und vergleichen die übrigen 148 Einstellungen. `AutomationOnly` läuft ohne Sampler und Extras, jeweils 47 Sekunden auf `test_diagonal.sav`, Zoom 0,5, 1280×720 und Affinität 0xFFFF. Die frühen INI-Runnerfehler und fehlgeschlagenen Controller bleiben mit ihren tatsächlichen Zuständen erhalten; sie werden nicht nachträglich als erfolgreiche Läufe ausgegeben.

| Normaler Zwischenstand | Frames >100 ms AN | Frames >100 ms AUS | GC AN / AUS |
|---|---:|---:|---:|
| e53, Serie006 | 6 / 5 | 0 / 0 | 84 / 84 gegenüber 45 / 46 |
| 81AC, Serie009 | 1 / 1 | 0 / 0 | 50 / 54 gegenüber 45 / 46 |
| 88FC, Serie013 | 1 / 1 | 0 / 0 | 50 / 53 gegenüber 45 / 46 |

Die Pufferwiederverwendung reduziert die zusätzliche Allokationslast erheblich, erklärt jedoch nicht allein die verbleibende Bildspitze. Die normalen 88FC-AN-Läufe erreichten Maxima von 124,1 / 120,8 ms bei maximalen Ticklücken von nur 48,1 / 45,4 ms. Die EngineCalls-Diagnose014 instrumentierte 103 EngineInterface-Methoden; die untersuchten Spitzen hatten keine passende gemessene Getter-/Monitorwartezeit. Die GC-Zählerzuordnung war zwischen Diagnosen nicht konsistent; daraus folgt kein Nachweis einer entsprechend langen GC-Pause.

Die kontinuierlichen Mainthread-Proben017 erfassten vier Bildspitzen mit vollständigen Zeitfenstern: 94,7–97,2 % der inneren Modframes lagen in `AreaConstruction.PlacementInput.AfterInput`, überwiegend im nativen Wartepfad; das vollständige Fenster enthält `mono_monitor_enter_v4_internal`. Sourceaudit und kontrollierte Gegenprobe zeigen die unnötige Anforderung des gemeinsamen Engine-Monitors selbst bei vollständig leerer Bauaktion. Die vorherige 88FC-Runtime wartete im echten `AfterInput` auf einen absichtlich belegten Monitor etwa 750 ms. Dies ist eine deterministische Fehlerkontrolle, keine normale Latenzmessung. Serie017 enthält einen Controllerfehler beim regulären Samplerabbruch nach beendetem AUS-Messfenster; Rohdaten und dieser Status bleiben getrennt erhalten.

`test_stresstest5.sav` zeigt bereits ohne Rettung schwere Last: Serie010 auf 81AC erfasste AN 22 / 22 und AUS 23 / 24 Bildintervalle über 100 ms, p99 etwa 223–231 ms. Dies beweist Last im damaligen Modsetup bei ausgeschalteter Rettung, keine alleinige Vanilla-Ursache und keine Freigabe des späteren Builds. Keine dieser Leistungsserien protokollierte tatsächliche Rettungs-/Entfernungsaktionen; sie ersetzen funktionale Fälle nicht.

### Mehrfachplatzierung: lokale Bauaktion und Leerlauf

`AfterInput` bleibt vor dem Engine-Monitor, wenn weder Gestureigentum, Drag, Bauqueue, Lagerauftrag, Steinbruchauftrag noch eine ausstehende Cancellation-Epoche existieren. Die Vorschau wird weiterhin auf Unity ausgeblendet. Neue Arbeit entsteht auf Unity; andere Threads können diese lokalen Aufträge nur abbrechen oder konsumieren. Aktive Gesten, Release, schnelle Aus-/Einschaltungen, Lager-Cadence und bereits bestätigte Steinbruchaufträge bleiben auf ihrem bisherigen geschützten Pfad.

`Primary` lässt Nicht-Unity-Aufrufe und fremde Eingaben durch, solange keine eigene Geste gehalten wird; eine neue Geste benötigt lokale Aktivierung und die aktuelle Bauaktion. `AllowCursor` greift nur auf Unity während eines eigenen Drags ein. Fokusverlust ohne Drag fordert den Monitor nicht mehr an. Menschliche Editoraktionen auf einem KI-Spielerplatz bleiben möglich; lokale Eingabeautorität ist nicht gleichbedeutend mit dem AI-Flag des gewählten Spielerplatzes. Bereits freigegebene menschliche Steinbruchaufträge werden weiterhin im Simulations-/authentifizierten Netzwerkpfad ausgeführt. Die getrennte bestehende Hostregel für Lagergrenzen bleibt unverändert. Es wurde keine zusätzliche ungegatete KI-Bauwirkung der Mehrfachgeste gefunden.

### Funktionale Abnahme des normalen Kandidaten035A

Die gemeinsame Transaktion `ca570a3b91a0488cb513c5967ab55a1c` besteht mit **7/7 Eingabe-/Gatefällen und 19/19 ursprünglichen Rettungsfällen**. Beide geordneten Fallnamenslisten werden separat geprüft; die bloße Gesamtsumme ersetzt keine Scopeprüfung. Ein Pluginlayoutwechsel und ein abschließender Restore sichern die gesamte Serie.

Die echten Idle-, Primary-, Cursor-, Focus- und Nicht-UI-Aufrufe kehren zurück, während ein Helperthread den tatsächlichen Engine-Monitor noch hält. `AfterInput` benötigt dort etwa 0,132 ms statt der absichtlich erzwungenen 750-ms-Wartezeit der Negativkontrolle. Die echte Cancellation wartet dagegen weiterhin auf Freigabe und übernimmt anschließend die Epoche. Keine privaten Produktionsfelder wurden zum Herstellen der Zustände beschrieben; es gab keine globalen Maus-/Tastatureingaben. Dies qualifiziert weder aktive Maus-Draggesten noch Betriebssystem-Eingabeverhalten.

Die ursprünglichen 19 Schritte bleiben unverändert und prüfen unter anderem echte ununterdrückte KI-AIV-Zugbrückenrettung mit ursprünglicher Identität, Gesundheit, Verband und Kachelbelegung, anschließende neue Bewegung, Lordregeln und Save/Load. Keine Error-/Fatal-Einträge. Gesamtbelege, Quellenarchiv und ursprüngliche Gegenprobe: `.work/a/area-idle-gate/001/result-composite.json`, `acceptance-composite.log`, `composite-source.zip`, `result-before.json`. Dies erweitert nicht stillschweigend die historische Keep-/Worker-/Editor-/Multiplayer-Matrix.

### Abschließende normale AN/AUS-Messung

Serie018 auf `test_diagonal.sav` ist vollständig bestanden und zurückgesetzt. Alle vier Läufe verwenden dieselbe normale035A-DLL, prüfen Active=True/False vor dem Messfenster und bewahren die übrigen Einstellungen. Pluginlayout wurde einmal je Serie gewechselt.

| Messwert | Rettung AN1 / AN2 | Rettung AUS1 / AUS2 |
|---|---:|---:|
| Frames >50 / >100 ms | 0 / 0 in beiden Läufen | 0 / 0 in beiden Läufen |
| Größtes Bildintervall | 14,0 / 14,7 ms | 15,2 / 13,7 ms |
| Frame-p99 | 5,340 / 5,174 ms | 5,205 / 5,216 ms |
| Simulationsticks/s | 140,6 / 140,6 | 140,6 / 140,5 |
| GC-Zähler | 51 / 53 | 46 / 45 |

Die vorher in jedem AN-Lauf reproduzierte Bildspitze um120 ms tritt in keiner Wiederholung mehr auf. Das ist eine begrenzte Spielstand-/Setupqualifikation, keine absolute Nullkosten- oder universelle Lagfreiheitszusage. Insbesondere bleiben zusätzliche GC-Zähler und unterschiedliche maximale Ticklücken sichtbar. Native Kontrollwerte: Rekonstruktionsfehler0, Assassinensuchen20 und Bauersuchen1113 in allen vier Läufen; PCL-Neuaufbauten104/102 gegenüber104/103. Quelle: `.work/a/rescue-lag/018/measurement-summary.json`. Auch Serie019 auf `test_stresstest5.sav` besteht vollständig:

| Messwert | Rettung AN1 / AN2 | Rettung AUS1 / AUS2 |
|---|---:|---:|
| Frames >50 / >100 ms | 0 / 0 in beiden Läufen | 0 / 0 in beiden Läufen |
| Größtes Bildintervall | 15,1 / 13,2 ms | 13,9 / 13,8 ms |
| Frame-p99 | 5,190 / 5,031 ms | 5,045 / 5,050 ms |
| Simulationsticks/s | 91,5 / 92,2 | 95,0 / 93,8 |
| Größte Ticklücke | 312,8 / 299,7 ms | 297,0 / 327,6 ms |
| GC-Zähler | 51 / 51 | 49 / 49 |

Die sichtbaren Bildwartezeiten bleiben in allen vier Stressläufen aus; die langsamen Simulationsticks bestehen auch ohne Rettung. Durchsatz und GC sind nicht identisch, und diese Ergebnisse begründen keine absolute Nullkostenbehauptung. Die Leistungsserien protokollieren keine Rettungsaktionen; deren Funktion wird durch die getrennten echten Fälle oben belegt. Quelle: `.work/a/rescue-lag/019/measurement-summary.json`.

Alle acht finalen Läufe: tatsächlicher Active-Nachweis vor jedem Messfenster, keine Error/Fatal/MOD_FAILURE, übrige Einstellungen und Savehashes unverändert. Ein Pluginlayoutwechsel je Serie; abschließend vollständige Rücksicherung und externer Nachweis ohne Spiel, Testsperre, Testplugins oder Konfigurationsreste. Der normale getestete DLL-/PDB-Satz bleibt bytegleich. Der temporäre Testmod wurde nach SHA-256-Abgleich aller fünf Quelldateien mit dem vollständigen Quellenarchiv entfernt; Ergebnisprotokolle und das kompakte Quellenarchiv bleiben erhalten. Der exakt geprüfte normale035A-DLL-/D1E9-PDB-Satz ist installiert, ohne Neubuild; Installationsnachweis: `.work/a/rescue-perf-audit/001/final-installation.json`.
