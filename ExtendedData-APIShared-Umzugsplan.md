# ExtendedData → APIShared: Umzugs- und Integrationsplan

Stand: 20.10.2026. Die vorgesehenen gemeinsamen Dienste aus Etappen 0–4 sind im Quellstand implementiert. Bereinigung, Dokumentation, Beispiele und Installationsvorprüfung aus Etappe 5 sind umgesetzt; beide regulären Builds bestehen ohne Installation. Fachliche Spielabnahme und anschließende Versions-/Abhängigkeitsfestlegung bleiben offen. Der Gesamtumzug ist deshalb noch nicht abgeschlossen oder freigegeben.

## Aktueller Abschlussstand und Zusatzdaten-API (20.10.2026)

Dieser Abschnitt ist der aktuelle Arbeitsstand. Die nachfolgenden datierten Abschnitte
bleiben historische Nachweise; ihre damaligen offenen Quellschritte sind dadurch ersetzt.

| Etappe | Quellstand | Noch erforderliche Abnahme |
|---|---|---|
| 0: Verträge und Sicherheitsgrenzen | Audits, öffentliche Flächen und Lifecycle geprüft | Keine Behauptung einer vollständigen Laufzeitabnahme |
| 1: Savegame-Auswahl | APIShared-Dialog, persistente persönliche Wahl, fehlende-UI-Fallback, ExtendedData-Registrierungen | Tatsächliche Darstellung, Neustart, Mischinstallation und MP/Coop |
| 2: Dokumente und Codecs | Content-Assembly, Core-Adapter und Paketregeln umgesetzt | Vollständiges APIShared-Paket im Spiel ohne ExtendedData starten |
| 3: Gemeinsame Settings-Engine | Teilnehmerdiagnose, Capture, Materialisierung, Autorenregeln, Anwendung und Cleanup umgesetzt | Werte/Rechte/Rückkehrzustände in den echten Start-, Restart- und Abbruchpfaden |
| 4: Zusatzdaten | Gemeinsame Karten-/lokale Leser, optionaler Lord-Anbieter und alte Fassade umgesetzt | Echte Quellenwechsel und bestätigte Hostdaten im Spiel |
| 5: Abschluss | Übertragene Algorithmen entfernt, Projekte, Beispiele, Baselines, Dokumentation und Installationsvorprüfung aktualisiert; reguläre Builds bestanden | Spielmatrix, danach atomare Versions-/Abhängigkeitsangaben und erneute Prüfung |

Aktuellste reguläre Buildnachweise nach der erneuten Codeprüfung: APIShared
`APIShared/.local/review-guid-build.log`
(20.10.2026, 13:26 Uhr lokal, Exitcode 0, 675 Tests bestanden) und ExtendedData
`ExtendedData/obj/review-guid-build.log` / `.work/b/working/174.build.json`
(äußerer Exitcode 0, `NoInstall=true`, 74/74 Tests und beide Upload-Suiten bestanden).
Die früheren Ergebnisse im Folgenden bleiben als historische Nachweise erhalten.

### Erneute Codeprüfung: ordinale GUID-Zuordnung (20.10.2026)

Die erneute Prüfung hat einen Fehler in drei Eingangsvarianten der neuen
`MissionModSettingsSession` reproduziert: Wörterbücher mit einem eigenen,
Groß-/Kleinschreibung ignorierenden Comparer konnten eine laut Dokumentvertrag
fehlende GUID als vorhanden erscheinen lassen oder Regeln beziehungsweise
Start-Snapshots auf eine andere GUID anwenden. Dabei konnte auch ein vorheriger
Missionskontext verlassen werden, statt den fehlenden Anbieter vorher abzulehnen.

Die Session verwendet nun ordinale Zuordnungen für den erfassten Teilnehmerbestand,
Dokumenteinträge und die tatsächlich verwendeten optionalen Start-Snapshots.
Die Eingabewörterbücher werden nicht ersetzt. Ein nicht vorhandener Anbieter wird
vor dem Verlassen des alten Kontexts abgelehnt; Regeln und Startwerte bleiben bei
ihrer exakten GUID. Ausgangsverhalten für bereits ordinale Wörterbücher, öffentliche
Signaturen, Dateiformate, Lifecycle und Hookbesitz bleiben unverändert. Der bereits
dokumentierte ordinale Identitätsvertrag ist im Integrationsleitfaden präzisiert.

Alle drei neuen Verhaltenstests scheiterten vor der jeweiligen Korrektur:
`APIShared/.local/review-guid-before.log` und `review-starting-guid-before.log`.
Danach bestehen 40 gezielte Session-, Vorbereitungs-, Dialog- und Zusatzdatenfälle
(`review-guid-after.log`). Im vollständigen APIShared-Build bestehen 675 Tests;
zwei optionale Runtime-Integrationsfälle bleiben übersprungen. API-Baselines und
Paket-/Bootstrap-Prüfungen bestehen ebenfalls.

Auch der reguläre ExtendedData-Build gegen das neue Paket endet mit äußerem
Exitcode 0. Zusätzliche Nachweise: `ExtendedData/obj/review-guid-boundaries.log`,
`review-guid-presets-build.log` und `review-guid-presets.log` für Quellgrenzen und
die echte Preset-/Persistenzintegration. Die weitere gezielte Durchsicht ergab
keinen zusätzlichen konkreten Fehler. Die Spielabnahme bleibt mangels Testfenster
offen; sie wird durch diese Code- und CLI-Prüfungen nicht ersetzt. Keine Installation,
Versionsänderung, Commits oder Veröffentlichung.

### Umsetzung und erneute Codeprüfung

- `APIShared.ModData.ModDataAccess` liest das aktuelle Extender-Kartenarchiv oder
  ausdrücklich eine lokale Lord-Sidecar-Datei. Ein eigener, optionaler Anbieter liefert
  die bestätigte Lord-Auswahl. Allgemeine Leser funktionieren ohne ExtendedData;
  sie wählen keine Inhalte aus, schreiben keine Dateien und wenden keine Spielwerte an.
- Der einzelne Anbieter registriert Besitzer-GUID und stabile Registrierungs-ID.
  Fehler, fehlende Bereitschaft, rekursive Abfragen und Anbieterwechsel während einer
  Abfrage liefern definierte Fehlerzustände. Ein alter Dispose-Token entfernt keinen
  neuen Anbieter. Die Registry hält keine Ergebnisse über Missionswechsel hinweg vor.
- ExtendedData behält die inhaltliche Auswahl sowie Host-, Name-, Checksum- und
  Sitzungsregeln. Die bisherigen öffentlichen ModData-Methoden bleiben als Adapter
  erhalten. Fehlende Hostbestätigung wird niemals durch eine lokale Ersatzdatei ersetzt.
- Der Anbieter wird ausdrücklich bei der Runtimeinitialisierung veröffentlicht und
  zu Beginn ihres Fehler-Cleanups logisch abgemeldet. Verzögerte alte Setter können
  ihn danach nicht erneut veröffentlichen. Vorhandene Hooks bleiben verwurzelt.
- `ModDataDocumentJson` besitzt jetzt die bestehende 64-KiB-Dokument- und
  16-KiB-Namespace-Prüfung. Der Streamleser liest höchstens Limit plus ein Byte,
  dekodiert striktes UTF-8 und lässt den übergebenen Stream offen. LordDataSnapshot
  delegiert diese Prüfung; sein Wire-Protokoll, Digest und Gesamtlimit bleiben erhalten.
  Der bestehende Kartenleser erhält seinen bisherigen Größenvertrag.
- Die Sessionprüfung verhindert zusätzlich eine neue Anwendung, solange fehlgeschlagene
  Besitzerbereinigung noch aussteht. Damit kann ein neuer Authoring-Capture keinen
  unvollständig zurückgesetzten temporären Zustand als persönliche Basis übernehmen.
- Nachprüfung von Runtimewurzel, JSON-Links, Registrierung/Abmeldung, bestehenden
  Missions-/Lobby-Resets und Fremdmod-Verbrauch erfolgt. Keine neuen Hooks, nativen
  Offsets oder Zugriffe auf neue Spielmember. Installierte Extender-Verträge und
  Archiv-IL wurden geprüft; der native Hash stimmt mit der verwendeten Baseline überein.
- Katalog, Integrationsleitfaden, Architektur und `APIShared/docs/MOD_DATA.md` erklären
  die gemeinsamen Verträge. ExtendedData-Guides verlinken diese API und erklären weiter
  ihre Inhalte. Das kompilierte Fremdmod-Beispiel hat eine eigene GUID und keine
  ExtendedData-Referenz. Die README-Aktualisierung ist ausdrücklich vom Nutzer autorisiert.

### Nachweise dieses Arbeitsstands

- Vollständiger APIShared-Build `/workspace /nopause /noinstall`: Exitcode 0,
  672 Tests bestanden (18 Content, 3 PopularityDisplay, 300 Core, 305 Runtime,
  46 Lobby), zwei optionale Runtime-Integrationsfälle übersprungen. Die zusätzlichen
  Host-Bash-Fixtures sind unter Windows nicht ausgeführt. Paket-/Bootstrap-Prüfungen
  und öffentliche Verbraucherbeispiele bestehen. Log: `APIShared/.local/moddata-build.log`;
  aktueller Paketnachweis: `APIShared/.local/build-proof.json`, erzeugt 20.10.2026, 12:53 Uhr lokal.
- Der reguläre ExtendedData-Treiber hat die Vorprüfungen bestanden, wurde aber wegen
  des bereits laufenden Spiels vor Tests/Kompilierung beendet. Sein unveränderter
  historischer Record `.work/b/working/168.build.json` hat deshalb **Exitcode 1**.
  Er darf nicht als erfolgreicher Paketbuild verwendet werden. Log: `ExtendedData/obj/moddata-build.log`.
- Anschließend wurden in genau dieser isolierten Arbeitskopie die Produktionsprojekte
  mit den echten installierten Spiel-/Extender-Referenzen und dem vollständig geprüften
  APIShared-Paket kompiliert. Die Paketvalidierung blieb aktiv; ihre Workspace-Zuordnung
  wurde ausdrücklich auf den tatsächlichen APIShared-Publisher gerichtet. Ergebnis:
  ExtendedData-Kompilierung ohne Fehler und 74/74 Tests bestanden. Logs:
  `ExtendedData/obj/moddata-isolated-compile.log` und `moddata-isolated-tests.log`.
  Das sind zusätzliche Compiler-/Testnachweise, kein nachträglich erfolgreicher BAT-Lauf.
- Beide Upload-Suiten bestehen mit den neuen Core-Abhängigkeiten; Logs:
  `ExtendedData/obj/moddata-ExtendedData.Upload.Tests.log` und
  `moddata-ExtendedData.JsonUpload.Tests.log` samt benachbarten Buildlogs.
- 600 öffentliche/geschützte Core-Metadateneinträge bleiben unverändert; Core benötigt
  weiterhin nur netstandard und Content. Nachweis: `ExtendedData/obj/moddata-core-surface.log`.
  Unverändert vorher kompilierter Core-Verbraucher einschließlich zweimaligem atomarem
  Schreiben/Lesen und elf exakte Lord-Wire-/Digest-/Fehlerfälle bestehen mit den isoliert
  erzeugten DLLs: `.work/t/core-moddata/001/consumer.log` und `corpus.log`, verglichen
  mit `.work/t/lord-corpus/001/baseline.txt`.
- Auch alle sechs öffentlichen Accessor-/Overload-Verträge der alten Runtime-Fassade
  sind im Metadatenvergleich unverändert (`ExtendedData/obj/moddata-runtime-surface.txt`).
  Das zentrale APIShared-Paket enthält Content einmal; die isolierte ExtendedData-Ausgabe
  enthält keine eigene APIShared-/Content-Kopie.
- Shared-Boundaries: `ExtendedData/obj/moddata-boundaries.log`.
  Preset-/Persistenzintegration: `ExtendedData/obj/moddata-preset-build.log` und
  `moddata-preset-storage.log`. Dokumentationsprüfung: `APIShared/.local/moddata-docs.log`.
  Managed-/Extender-Audit: `APIShared/.local/moddata-contracts.txt`.
- Bezug der Nachweise: Mod-Repo `develop` / `997d3a5e627bf2ced52ee30c9f26c2c73211e692`,
  APIShared `dev/Serpens66` / `001ebfe737abe4d12d32b847427ec643699d675a`, jeweils mit
  Arbeitsänderungen. Parallel vorhandene Nutzeränderungen bleiben erhalten. Das laufende
  Spiel und installierte Pakete wurden durch diesen Arbeitsschritt nicht verändert.

### Verbleibende Arbeiten in sinnvoller Reihenfolge

1. Die vollständige lokale Spielmatrix aus Abschnitt 8.2 mit passenden Paketen abnehmen:
   APIShared allein, ExtendedData aktiv/deaktiviert, beide Checkboxentscheidungen und
   Neustartpersistenz, fehlender UI-Patch, geschützte/Legacy-Saves, Editor-/Trailoptionen,
   echte Missionsübernahme und Rückkehr zu persönlichen Einstellungen.
2. Alt-/Neupaket-Mischinstallationen praktisch prüfen. Der Laufzeitkonfliktcheck vor
   Veröffentlichung des Dialoghooks ist im Quellstand vorhanden. Die Paketvorprüfung
   ist mit Metadatenfixtures geprüft; der echte Installations-/Startnachweis fehlt weiterhin.
3. Echte MP-/Coop-Matrix mit zweitem Client und menschlichem Mitspieler durchführen.
   Der Nutzer hat am 20.10.2026 ausdrücklich mitgeteilt, dass diese derzeit nicht verfügbar
   sind. Dieser Nachweis bleibt offen und wird nicht durch lokale Simulation ersetzt.
4. Erst nach fachlicher Abnahme Versionen, Plugin-/Manifest-Mindestabhängigkeiten,
   Beispiel-Mindestversionen und produktbezogene Versionshinweise gemeinsam festlegen
   und erneut bauen/prüfen. Die jetzigen Entwicklungsnummern belegen keine Kompatibilität
   mit älteren gleich nummerierten Paketen. Keine Freigabe auf Grundlage dieser Nummern.

Ein weiterer pauschaler Umzug von Karten-, Trail-, Koop-, Lord-, AIV- oder Uploadlogik
ist nicht vorgesehen: Diese Funktionen sind Inhaltsverantwortung von ExtendedData.
Es bleiben somit konkrete Abschluss- und Laufzeitnachweise, kein weiterer großer
gemeinsamer Engineblock. Commits und Veröffentlichungen sind weiterhin nicht beauftragt.

### Nachprüfung der Installationsvorprüfung (20.10.2026)

Der erneute Anforderungsabgleich hat eine zuvor zu weitgehende Nachweisbehauptung
berichtigt: Der Laufzeit-Konfliktcheck war vorhanden, die Installationsvorprüfung
hatte aber nur API-Referenzen geprüft. Eine alte ExtendedData-DLL ohne solche
Referenzen wurde deshalb akzeptiert. Dieser Befund wurde vor der Änderung durch
einen tatsächlich fehlschlagenden Test reproduziert (`APIShared/.local/dialog-preflight-before.log`).

`Verify-WorkshopConsumerReferences.ps1` prüft nun zusätzlich die veröffentlichte
BepInPlugin-GUID und den öffentlichen booleschen Konstantenmarker des konkreten
Plugintyps. Alte, falsch deklarierte und lediglich umbenannte Plugins werden vor
dem Kopieren des APIShared-Pakets mit einer konkreten gemeinsamen Updateaufforderung
abgelehnt. Fremde GUIDs und passende ExtendedData-Plugins bleiben zulässig. Die
bestehende Memberauflösung bleibt erhalten; es werden keine Assemblies ausgeführt.

Acht Metadatenfälle bestehen im realen Windows-PowerShell-5.1-Unterprozess:
fehlendes optionales Plugin, umbenanntes Altplugin, falscher, nichtkonstanter,
privater oder nur auf einem fremden Typ vorhandener Marker, passendes Plugin
und fremde GUID. Nachweise: `APIShared/.local/dialog-preflight-after.log` und
`.work/t/dialog-preflight/005`. Der Test verlangt einen eigenen leeren Fixtureordner,
bleibt ohne Nachbar-Repository lauffähig und ist in CONTRIBUTING dokumentiert.

Die Vorprüfung erfasst Assemblies unter `BepInEx/plugins`. Ungeöffnete Workshoparchive
und manuelle Installationen benötigen weiterhin den Laufzeitcheck. Diese Prüfung
beweist keine tatsächliche UI-Darstellung oder konfliktfreie Spielausführung.

Der reguläre ExtendedData-Build vor dieser zusätzlichen Prüferänderung ist ebenfalls
erfolgreich abgeschlossen: `.work/b/working/169.build.json`, Exitcode 0,
`NoInstall=true`, 74/74 Tests und beide Upload-Suiten bestanden. Log:
`ExtendedData/obj/moddata-final-build.log`.

Auch nach der Prüferkorrektur bestehen beide regulären Builds: APIShared mit
672 bestandenen Tests, zwei optionalen übersprungenen Runtime-Integrationsfällen
und erfolgreichen Paket-/Bootstrap-Prüfungen; ExtendedData mit 74/74 Tests,
beiden Upload-Suiten und äußerem Exitcode 0. Nachweise:
`APIShared/.local/dialog-preflight-build.log`, `APIShared/.local/build-proof.json`
(13:12 Uhr lokal), `ExtendedData/obj/dialog-preflight-build.log` und
`.work/b/working/171.build.json` (`NoInstall=true`).

Die finale Core-Ausgabe aus Lauf 171 besteht ebenfalls den Vergleich aller
600 öffentlichen/geschützten Metadateneinträge, den unverändert kompilierten
Altverbraucher mit atomarem Schreiben/Lesen und die elf exakten Lord-Paketfälle:
`ExtendedData/obj/dialog-preflight-core-surface.log` und `.work/t/core-final/001`.
Dokumentationsprüfung: `APIShared/.local/dialog-preflight-docs.log`.
Keine Installation, Versionsänderung, Commits oder Veröffentlichung durch diesen Schritt.

## Gemeinsame Missionsanwendung und Autorenregeln (20.10.2026)

Vor diesem Schritt wurden aktuelle Arbeitsstände und bestehende Nutzeränderungen erneut
geprüft. Zwischenzeitliche Opacity-/UI-/Locale-Arbeit wird erhalten; sie ist kein Teil
dieses Umzugs. Die bisherigen JSON-Parser, statischen Runtimewurzeln nach LibraryLoaded
und logischen Fehler-/Teardownpfade bestehen weiter. Keine neuen Hooks oder Spielmember.

- APIShared besitzt mit `MissionModSettingsSession` jetzt die Teilnehmeranwendung und
  deren Bereinigung. `MissionModSettingsApplicationKind` unterscheidet Playable,
  Preview, Editable, Authoring, AuthoringTest und Materialized ausdrücklich. Die alte
  Kombination privater Consumer-Booleans wird nicht als neue öffentliche API übernommen.
- Autorenwerte werden vor dem Verlassen eines alten temporären Kontexts erfasst;
  spielbare Player-Werte kommen danach aus den persönlichen Einstellungen. Backend-
  Eigenwerte werden wie bisher vor der Player-Auflösung aktualisiert. Restart-Snapshots
  haben weiterhin Vorrang, außer bei bereits materialisierten Werten.
- Die Session hält konkrete Endpoint-Instanzen statt nur GUIDs fest. Bereinigung bleibt
  dadurch nach einem Wechsel der Teilnehmerermittlung möglich. Jeder Endpoint wird
  versucht; Fehler werden aggregiert, fehlgeschlagene Besitzer bleiben für einen Retry
  erhalten. Vorhersagbare Wertfehler und fehlende erwähnte Anbieter blockieren bereits
  vor dem Verlassen eines bisherigen Kontexts.
- Der bisherige Rollback erfasste nur vollständig eingetretene Teilnehmer. Nun werden
  auch teilweise fehlgeschlagene Eintrittsversuche bereinigt. Nach einem Fehler jenseits
  der Ersetzungsgrenze wird zum persönlichen Zustand zurückgekehrt, nicht stillschweigend
  der vorherige temporäre Missionskontext wiederhergestellt. Providers müssen ihr
  bestehendes Exit auch nach einem teilweise fehlgeschlagenen Enter unterstützen.
- Ein reproduzierter zusätzlicher Fehler im bestehenden APIShared-ViewModel ist
  korrigiert: Nach fehlgeschlagener persönlicher Wiederherstellung bleibt der
  Missionsstatus für einen erneuten Exit aktiv. Der neue Backend-Test scheitert
  vor dem Fix und besteht danach (`mission-exit-retry-before.log` / `-after.log`
  unter `APIShared/.local`). Erfolgreiche Exit-Pfade und öffentliche Signaturen
  bleiben unverändert; die Änderung betrifft die bisher unvollständige Fehlerbereinigung.
- Ein erfolgreiches Apply bestätigt keinen Kartenstart. `ConfirmStarted` bindet erst
  einen tatsächlichen positiven Lifecycle-Session-Identifier nach erfolgreicher
  Vorbereitung. ExtendedData ruft es am vorhandenen bestätigten Missionsstart auf;
  seine inhaltlichen Trail-/Coop-/Restart-Entscheidungen bleiben dort.
- `MissionModSettingsCreatorRules` übernimmt Autorenregelerzeugung und die bestehende
  Savegame-Regelauswertung. Modi, MessagePack-Daten, die 1-MiB-Propertygrenze und das
  bisherige Default/Player-Verhalten bei fehlerhaften festen Werten bleiben erhalten.
  Fehler optionaler Diagnosen verhindern keine anderen Regeln.
- Der ExtendedData-Coordinator delegiert Anwendung, Cleanup und Regeln; die bisherigen
  doppelten Algorithmen und ihr Reflection-Cache entfallen. Er behält Dokument-/UI-
  Zuordnung, Inhaltsquellen, Restartidentität und Entscheidungen des Missionslifecycle.
  Öffentliche alte Savegame- und Core-Verträge bleiben unverändert.

Abschließende Nachweise dieses Arbeitsstands:

- APIShared `/workspace /nopause /noinstall`: Exitcode 0, 661 Tests bestanden
  (13 Content, 3 PopularityDisplay, 300 Core, 299 Runtime, 46 Lobby). Zwei optionale
  Runtime-Integrationsfälle bleiben übersprungen; separate Host-Bash-Fixtures wurden
  unter Windows nicht ausgeführt. Finale Logs: `APIShared/.local/mission-engine-final-build.log`
  und `.local/build-proof.json`. Frühere Läufe bleiben als historische Nachweise erhalten.
- ExtendedData `/workspace /apishared:dev /nopause /noinstall`: 73/73 Tests und beide
  Upload-Suiten bestanden. Auch der äußere Treiber ist mit Exitcode 0 abgeschlossen.
  Paket: `.work/b/working/167/ExtendedData/BepInEx/plugins/ExtendedData_Serp`;
  Record: `.work/b/working/167.build.json`; Log: `ExtendedData/obj/mission-engine-build.log`.
- Öffentliche API-Baseline additiv geprüft, vorhandene Signaturen erhalten. Zusätzlich
  600 unveränderte Core-Metadateneinträge und ein unverändert vor dem Codec-Umzug
  kompilierter Verbraucher bestätigt, einschließlich atomarem Schreiben/Lesen ohne
  Spielassemblies: `ExtendedData/obj/mission-engine-core-surface.log` und
  `.work/t/core-engine/001/consumer.log`.
- Gezielte neue Fälle: vollständiger und teilweise fehlgeschlagener Eintritt,
  Bereinigung aller Teilnehmer trotz einzelner Fehler, Retry fehlgeschlagener Besitzer,
  Providerwechsel, fehlende Anbieter, unbekannte Properties, Callback-Veto/Reentranz,
  Authoring/Test/Materialized, Restart-Vorrang und echte persönliche Persistenz.
  Backend-Player-Werte und das Wiederholen eines fehlgeschlagenen persönlichen Restore
  sind mit Produktions-ViewModel und Preset-Controller getestet.
- Aktuelle Quell-/JSON-/Lifecycle-/XAML-/Hook-Prüfung:
  `ExtendedData/obj/mission-engine-final-boundaries.log`.
  Preset-/Persistenzintegration: `ExtendedData/obj/mission-engine-final-preset-build.log`
  und `mission-engine-final-preset-storage.log`.
  Dokumentationsprüfung: `APIShared/.local/mission-engine-docs.log` (316 lokale Ziele/Anker).
- Arbeitsstände: Mod-Repo `develop` / `997d3a5e627bf2ced52ee30c9f26c2c73211e692`,
  APIShared `dev/Serpens66` / `001ebfe737abe4d12d32b847427ec643699d675a`, jeweils mit
  den beim Build erfassten Änderungen. Vorhandene fremde Arbeitsänderungen erhalten.
  Keine Installation, Commits, Versionsänderung oder Veröffentlichung.

**Noch abzuarbeiten:** Etappe 4 (Runtime-Zusatzdatenzugriff und optionaler bestätigter
Lord-Anbieter), abschließende Bereinigung/Beispiele/Versionen nach fachlicher Abnahme
sowie die vollständige Spielmatrix. Karten-/Trail-/Koop-/Lord-Inhalte werden weiterhin
nicht pauschal nach APIShared kopiert. Der Gesamtauftrag ist nicht abgeschlossen.

Die folgenden Abschnitte bewahren die Nachweise vorheriger Arbeitsschritte.

## Fortsetzung von Etappe 3: Vorbereitung von Missionssettings (20.10.2026)

Die gemeinsame Teilnehmerermittlung wurde erneut auf Ausschlüsse, Duplikate,
dynamische Anbieter und Fehlerisolation geprüft. Dabei ergab sich kein weiterer
konkreter Änderungsbedarf. Der nächste zusammenhängende Teil ist umgesetzt:

- `MissionModSettingsPreparation` in APIShared übernimmt `CaptureEntry`,
  `CaptureCurrentSnapshot`, `ValidateEntry` und `MaterializeSource`. Die bestehende
  Property-Ermittlung und Konvertierung werden wiederverwendet; es gibt keine neue Registry.
- ExtendedData delegiert die entsprechenden bisherigen privaten Algorithmen an
  diese API. Die alten Core-Typidentitäten bleiben über einen schmalen DTO-Adapter
  erhalten. Dateizugriff, Quellenregistrierung, Modusauswahl und Anwendung bleiben lokal.
- Die Quellenauflösung bleibt Defaults → bekannte Spielerwerte → bekannte feste Werte.
  Fehlende Spielerwerte behalten Defaults; unbekannte Properties werden ignoriert.
  Float-/Enum-Werte behalten das bisherige MessagePack/base64-Missionsformat.
- Die Vorbereitung kopiert Dictionaries, Bytepuffer und erfasste mutable Sequenzen.
  Zuvor wurde ein Provider-Snapshot während der Vorbereitung direkt überschrieben.
  Selbst bei einem Provider, der seinen Rückgabespeicher intern behält, entstehen
  jetzt keine solchen unbeabsichtigten Schreibzugriffe. Die bestehende Pflicht der
  Provider zu unabhängigen, nicht verändernden Snapshot-Fabriken bleibt bestehen.
- Fehler werden vor einer Anwendung weitergereicht, ohne ein Teilergebnis zu liefern.
  Null-Snapshots und ausgewählte null-Spielerpuffer erhalten eindeutige Datenfehler.
  Persönliche Baseline-Werte bleiben erhalten; ihre Live-Getter werden nicht gelesen.
- Die Erweiterung ist additiv, erfordert keine neuen Spielmember oder Hooks und
  verändert weder Missionslifecycle noch Transportprotokolle oder Schema-Versionen.
  JSON bleibt bei den vorhandenen quellverlinkten Parsern. Die nach `LibraryLoaded`
  statisch verwurzelten Runtimes und logischen Teardownpfade wurden vorab geprüft.

Die neue öffentliche API ist in Katalog, Integrationsleitfaden, XML-Verträgen und
Architektur dokumentiert; die betroffenen ExtendedData-README-Abschnitte sind aktualisiert.

Prüfnachweise dieser Fortsetzung:

- Gezielte Vorbereitungs-/Teilnehmer-/API-Tests: `APIShared/.local/preparation-tests.log`.
- Vollständiger APIShared-Build: `APIShared/.local/preparation-build.log`.
- Shared-Boundaries: `ExtendedData/obj/preparation-boundaries.log`.
- Preset-/Persistenzintegration: `_inspect/HostClientPresetTests/preparation-build.log`
  und `preparation-storage.log`.
- Dokumentationslinks: `APIShared/.local/preparation-docs.log`.

Ergebnisse: Der vollständige APIShared-Lauf besteht mit 644 Tests (13 Content,
3 PopularityDisplay, 300 Core, 289 Runtime, 39 Lobby); zwei optionale Runtime-
Integrationsfälle bleiben wie zuvor übersprungen. Bootstrap-/Paket-/Recovery-
Prüfungen bestehen; die zusätzlichen Host-Bash-Fixtures wurden unter Windows
nicht ausgeführt. NuGet meldete nicht erreichbare Sicherheitsdaten, die Builds
verwendeten die bereits verfügbaren Abhängigkeiten.

ExtendedData wurde aus dem Arbeitsstand `develop` / `997d3a5e627bf2ced52ee30c9f26c2c73211e692`
mit dem APIShared-Arbeitsstand `dev/Serpens66` / `001ebfe737abe4d12d32b847427ec643699d675a`
gebaut, jeweils einschließlich der Änderungen dieser Fortsetzung. Der Build liegt
unter `.work/b/working/164`, das Log unter `ExtendedData/obj/preparation-build.log`.
73/73 Core-/Runtimefälle und beide Upload-Suiten bestehen. Auch der äußere Treiber ist
mit Exitcode 0 abgeschlossen (`.work/b/working/164.build.json`, `NoInstall=true`). Die Shared-Boundary-,
Preset-/Persistenz- und Dokumentationsprüfungen bestehen ebenfalls.

Der Core-Metadatenvergleich bestätigt 600 unveränderte öffentliche/geschützte
Einträge und ausschließlich die erwarteten `netstandard`-/Content-Abhängigkeiten
(`ExtendedData/obj/preparation-core-surface.log`). Der unverändert vor dem Codec-
Umzug kompilierte Verbraucher besteht erneut mit den neuen DLLs, einschließlich
atomarem Schreiben/Lesen; Nachweis: `.work/t/core-preparation/001/consumer.log`.

**Weiterhin offen:** Die Applying-/Rollback-Engine, Creator-Regeln und Übernahme
in eine tatsächliche Mission liegen weiterhin in ExtendedData. Als nächster
zusammenhängender Abschnitt ist deren Vorbereitung/Anwendung mit klarer
Transaktionsgrenze zu prüfen. Die einzige Quellenregistry bleibt
`ModSettingsWorkingSourceRegistry`. Echte Spiel-, Neustart-, UI-, MP- und Coop-Abnahmen
sind weiterhin erforderlich. Keine Installation, Versionserhöhung, Commits oder
Veröffentlichung in dieser Fortsetzung.

Die folgenden Abschnitte bewahren die Nachweise der vorherigen Arbeitsstände.

## Beginn von Etappe 3: Gemeinsame Teilnehmerdiagnose (20.10.2026)

Der zuletzt übertragene Schema-3-Codec und seine Core-Adapter wurden erneut geprüft. Es ergab sich kein weiterer konkreter Codec-Änderungsbedarf; die bisherigen Grenz-, Alias-, Fehler- und Dateivertragstests bestehen erneut. Der nächste zusammenhängende Schritt bündelt die bisherige Teilnehmerermittlung:

- `APIShared/src/ModSettings/ModSettingsParticipantDiscovery.cs` prüft die bestehenden Lobbyregistrierungen und dynamischen Einträge aus `ModSettingsApplication.Endpoints`. Es gibt keine zusätzliche Registry und keinen neuen Provider. Automatische Savegame-Auswahl und öffentliche Diagnose verwenden dieselbe Implementierung.
- `SavegameModSettings.GetParticipantDiagnostics()` liefert schreibgeschützte Metadaten für akzeptierte und abgelehnte Teilnehmer. `ModSettingsParticipantInfo` und `ModSettingsParticipantStatus` unterscheiden unter anderem fehlende GUIDs, doppelte Panels, Ausschlüsse, fehlende Endpoints, ungültige Hostschemas, ungültige Missionssnapshots, Inspektionsfehler und konkurrierende Anbieter. Endpoints und PropertyInfo-Ziele bleiben lebende Objekte; die Abfrage ist kein Wert-Snapshot und kein dauerhaft gültiger Berechtigungsnachweis.
- Der Überladungsparameter `ModSettingsParticipantPurpose` trennt automatische Savegames von `MissionAuthoring`. Ein reiner Savegame-Opt-out schließt den Mod weiterhin nicht aus der Map-/Trail-Bearbeitung aus. Die bisherigen zusätzlichen Lese-/Serialisierungsproben für solche Autorenendpoints bleiben erhalten. Existiert bereits ein expliziter dynamischer Anbieter für dieselbe GUID, behält dieser seinen bisherigen Vorrang; das verdeckte Panel wird nicht zusätzlich geprüft. Die parameterlose Diagnose und `GetCompatibleParticipants()` behalten die automatische Savegame-Auswahl.
- Reflection-Prüfungen lagen zuvor teilweise außerhalb der Fehlerbehandlung je Teilnehmer. Fehlerhafte Attribute, Snapshot-Fabriken oder dynamische Deskriptoren lehnen jetzt den betroffenen Teilnehmer ab, ohne andere Teilnehmer aus der Ermittlung zu verlieren. Echte Fehler der Registrierungsquelle selbst werden weiterhin weitergegeben. Zwei akzeptierte konkurrierende Anbieter werden diagnostiziert; automatische Operationen brechen dafür wie bisher mit einer Exception ab.
- ExtendedData bezieht die Ergebnisse für seine bestehende Map-/Trail-Kompatibilitätsanzeige mit Zweck `MissionAuthoring`. Die zweite private Endpoint-Prüfung entfällt; der veröffentlichte Core-Kompatibilitätshelfer bleibt erhalten. Inhaltsabhängige Panelauswahl, der bisherige explizite ExtendedData-Opt-out und die UI-Reihenfolge bleiben lokal.
- Ein zusätzlicher Prüfbefund ist abgesichert: Eine fremde Exception kann eine leere Meldung liefern. Der Teilnehmerstatus bleibt dann abgelehnt; ExtendedDatas bisheriger String-Adapter erhält einen nichtleeren Ersatzgrund und kann die Ablehnung nicht versehentlich als Kompatibilität behandeln.
- Die öffentliche API wurde additiv erweitert; bestehende Signaturen bleiben erhalten. Katalog, Integrationsleitfaden, Architektur und die betroffenen README-Abschnitte dokumentieren den neuen Stand. Für die neue Abfrage gelten Unity-Thread, abgeschlossene Registrierung und nicht verändernde Snapshot-Fabriken. Es wurden keine Hooks, Spielmember, Transportwege, Speicherformate oder Missionslifecycle-Wege geändert.

Prüfergebnis des abschließenden Arbeitsstands:

- Vollständiger APIShared-Build `/workspace /nopause /noinstall`: 639 Tests bestanden, zwei optionale Integrationsfälle wie zuvor übersprungen. Darin enthalten sind zwölf neue Teilnehmer-Verhaltenstests, die bestehenden 13 Content-Tests und die API-Kompatibilitätsprüfungen. Paket-/Bootstrap-Prüfungen und öffentliche Verbraucherbeispiele bestehen. Die separaten Host-Bash-Fixtures bleiben unter Windows nicht ausgeführt.
- Vollständiger ExtendedData-Build `/workspace /apishared:dev /nopause /noinstall`: 73/73 Core-/Runtimefälle und beide Upload-Suiten bestanden. Auch der äußere Buildtreiber ist mit Exitcode 0 abgeschlossen. Die abschließenden modbezogenen Shared-Boundary-Prüfungen bestehen.
- Die einschlägigen `HostClientPresetTests` mit `preset-storage` bestehen, einschließlich persönlicher Persistenz, Autorität, Restart-Wiederherstellung, externer Anbieter und dynamischer Settings bis 16384 Optionen.
- Der Vergleich zum Core-Paket aus Lauf 140 bestätigt erneut 600 unveränderte öffentliche/geschützte Metadateneinträge. Der gegen den Core-Stand vor dem Codec-Umzug kompilierte Verbraucher funktioniert unverändert mit dem neuen Paket, einschließlich zweimaligem atomarem Schreiben und anschließendem Lesen, ohne Spielassemblies oder APIShared-Runtime.
- Keine Installation, Veröffentlichung oder Versionsänderung durch diesen Schritt. Dieser Chat hat keine Commits ausgeführt. Während der Arbeit wurden beide Repositories extern weitercommittet; die abschließenden Nachweise beziehen sich auf die tatsächlich ausgewählten aktuellen Arbeitsstände: Mod-Repository `develop` / `321e513ce7ea8c5c00d1625960433bb1a2e9abf3`, APIShared `dev/Serpens66` / `3e71352b2422777a1be4aac086b3644258cc1e13`, jeweils einschließlich der zu diesem Lauf erfassten Änderungen.

| Gegenstand | Lokaler Nachweis |
|---|---|
| Abschließender APIShared-Build | `APIShared/.local/participant-final-build.log` und `APIShared/.local/build-proof.json` |
| Teilnehmer-Verhaltenstests | `APIShared/tests/APISharedTests/ModSettingsParticipantTests.cs`; Ergebnisse im vollständigen APIShared-Testlauf |
| Gezielt geprüfte Zweck-/Vorrangregeln | `APIShared/.local/participant-purpose-tests.log` |
| Abschließender ExtendedData-Build | `ExtendedData/obj/participant-final-build.log` und `.work/b/working/143.build.json` |
| Gebautes ExtendedData-Paket | `.work/b/working/143/ExtendedData/BepInEx/plugins/ExtendedData_Serp` |
| Abschließende Quell-/Lifecycle-/XAML-/Hook-Prüfung | `ExtendedData/obj/participant-final-boundaries.log` |
| Preset-/Persistenzintegration | `_inspect/HostClientPresetTests/participant-build.log` und `_inspect/HostClientPresetTests/participant-storage.log` |
| Core-Metadatenvergleich | `ExtendedData/obj/participant-core-surface.log`, `.before.txt` und `.after.txt` |
| Unverändert ausgeführter alter Verbraucher | `.work/t/core-participants/001/consumer.log` |

**Verbleibend:** Etappe 3 ist begonnen, nicht abgeschlossen. Als nächster Quellabschnitt folgen gemeinsame Dokumentprüfung, Capture und Snapshot-/Quellenmaterialisierung mit den bisherigen Endpoints; danach Anwendung, Rollback und Sitzungsübernahme. `ModSettingsWorkingSourceRegistry` bleibt dabei die einzige Quellenregistry. Die Runtime-Engine selbst liegt weiterhin in ExtendedData. Die offene UI-/Save-/Neustart-/MP-/Coop-Spielabnahme sowie der Spielstart des vollständigen APIShared-Pakets ohne ExtendedData sind durch die CLI-Prüfungen nicht ersetzt. Etappen 4 und 5 bleiben offen.

Die folgenden Abschnitte sind historische Zwischenstände; der aktuelle Stand oben ersetzt deren damalige nächste Schritte.

## Weitere Nachprüfung und Missionssettings-Codec (20.10.2026)

Der nächste zusammenhängende Quellschritt ist umgesetzt:

- `APIShared.Content` besitzt jetzt zusätzlich `APIShared.ModSettings.MissionModSettingsDocument`, `MissionModSettingsEntry` und `MissionModSettingsJson`. Die neuen Namen verhindern Mehrdeutigkeiten bei Verbrauchern, die gleichzeitig die alten Core-Typen und `APIShared.ModSettings` importieren.
- Der Schema-3-Algorithmus liegt nur noch in Content: JSON-Text und bereits dekodierte Objekte lesen, normalisieren, validieren, unbekannte Einstellungen entfernen und deterministisch serialisieren. Format, skalare Wertregeln, CRLF, UTF-8-Grenze von 8 MiB und die Grenze von 16384 Einstellungen je Mod bleiben erhalten.
- Die bisherigen Core-Modelle sind aus der gemischten `MissionModels.cs` getrennt, bleiben aber in `ExtendedData.Core` mit denselben öffentlichen Signaturen und Assemblyidentitäten. Der alte Codec delegiert an Content. Seine Adapter erhalten die geprüften Objektidentitäten, gemeinsam referenzierte Einträge, Dictionary-Comparer und die auch bei Fehlern sichtbaren Teilnormalisierungen. `Read` und `WriteAtomic` bleiben bei Core; Dateiauswahl, Koop-/Lord-Paketmodelle und Runtime-Engine bleiben bei ExtendedData.
- Bei der erneuten Buildprüfung wurde ein Fehler im bisherigen inkrementellen Wrapper gefunden: Verschachtelte Content-Dateien sowie entfernte Eingaben konnten übergangen werden. Das Framework-Ziel ruft jetzt das SDK auf und überlässt diesem die vollständige inkrementelle Entscheidung. Der neue Test führt das tatsächliche Produktionsziel mit isolierten Quellen aus und prüft Änderungen, Hinzufügen und Entfernen verschachtelter Dateien einschließlich der deterministischen Rückkehr zum vorherigen Binary.
- Acht zusätzliche Content-Verhaltenstests und ein gruppierter Core-Adaptertest decken insbesondere Grenzen, ungültige Dokumente, Konflikte, Aliase, Schemaentwicklung sowie Nebenwirkungen fehlgeschlagener Normalisierung/Serialisierung ab. Die Content-API-Baseline ist additiv erweitert. Content.Tests ist auch im spielunabhängigen Windows-CI-Ablauf eingetragen; ein Remote-CI-Lauf wurde nicht gestartet.
- Katalog, Integrationsleitfaden, Architektur, Beitragsdokumentation und die betroffenen README-Abschnitte sind aktualisiert. Das Content-Beispiel benötigt keine Runtime-Assembly.

Prüfergebnis des aktuellen Arbeitsstands:

- APIShared: vollständiger Build `/workspace /nopause /noinstall` erfolgreich, 627 Tests bestanden, zwei optionale Integrationsfälle wie zuvor übersprungen. Davon sind 13 Content-Tests. Paket-/Bootstrap-Prüfungen und öffentliche Verbraucherbeispiele bestehen ebenfalls; die gesonderten Host-Bash-Fixtures wurden unter Windows weiterhin nicht ausgeführt.
- ExtendedData: vollständiger Build `/workspace /apishared:dev /nopause /noinstall` erfolgreich, 73/73 Core-/Runtimefälle und die beiden Upload-Suiten bestanden. Auch der äußere Buildtreiber ist mit Exitcode 0 abgeschlossen. Die modbezogenen Shared-Boundary-Prüfungen bestehen.
- Der Metadatenvergleich zum vor diesem Codec-Umzug gebauten Core-Paket ergibt 600 unveränderte öffentliche/geschützte Einträge einschließlich Signaturen, Parameterdefaults und Enumkonstanten. Core referenziert weiterhin nur `netstandard` und `APIShared.Content`.
- Ein eigenständiger Verbraucher wurde gegen das frühere Core-Binary aus Lauf 137 kompiliert. Danach wurden ausschließlich Core-/Content-DLL ausgetauscht; derselbe Verbraucher läuft ohne Neukompilierung mit dem neuen Paket. Modelle, Normalisierung, Parse/Serialize, Entfernen unbekannter Namen, Wertregeln und zweimaliges atomares Schreiben mit anschließendem Lesen bestehen. Im Verbraucherordner liegen nur seine eigene DLL, Core und Content, keine Spielassembly oder APIShared-Runtime.
- Das zentrale Paket enthält genau eine Content-DLL; ExtendedDatas Paket enthält ausschließlich seine eigenen beiden DLLs. Keine Installation, Versionsanhebung in diesem Schritt, Commits oder Veröffentlichung. Die Builds enthalten weiterhin Warnungen zu überlappenden vorhandenen JSON-Typen bzw. vorhandenen Referenzkonflikten; NuGet konnte die externe Sicherheitsdatenquelle nicht erreichen. Es gab keine Build- oder Testfehler.

| Gegenstand | Lokaler Nachweis |
|---|---|
| Vollständiger APIShared-Build | `APIShared/.local/mission-content-build.log` und `APIShared/.local/build-proof.json` |
| Verschachtelte Content-Build-Eingaben | `APIShared/tools/Validation/Test-ContentBuild.ps1` und `.work/t/content-build/001` |
| Vollständiger ExtendedData-Build | `ExtendedData/obj/mission-content-build.log` und `.work/b/working/140.build.json` |
| Gebautes ExtendedData-Paket | `.work/b/working/140/ExtendedData/BepInEx/plugins/ExtendedData_Serp` |
| Quell-/Lifecycle-/XAML-/Hook-Prüfung | `ExtendedData/obj/mission-content-boundaries.log` |
| Vergleich der alten Core-Schnittstelle | `ExtendedData/obj/mission-content-surface.log`, `.before.txt` und `.after.txt` |
| Unverändert ausgeführter alter Verbraucher | `.work/t/core-consumer/001`, insbesondere `baseline.log` und `candidate.log` |

**Verbleibend:** Die Quellaufgaben aus Etappe 2 sind damit umgesetzt. Die Laufzeitabnahme des vollständigen APIShared-Pakets ohne ExtendedData sowie die ursprünglichen UI-/Save-/Neustart-/MP-/Coop-Spieltests stehen weiterhin aus. Daher ist der Gesamtumzug nicht als spielgetestet oder releasebereit abgeschlossen. Der nächste Quellabschnitt wäre Etappe 3: zunächst strukturierte Teilnehmerdiagnosen, danach gemeinsame Materialisierung und Settings-Anwendung über die bestehenden Endpoints und die vorhandene Quellenregistry. Etappen 3 bis 5 sind noch nicht umgesetzt.

Die folgenden Abschnitte dokumentieren die vorangegangenen Zwischenstände. Ihre damaligen nächsten Schritte werden durch den aktuellen Stand oben ersetzt; die historischen Prüfergebnisse bleiben erhalten.

## Nachprüfung und Beginn von Etappe 2 (20.10.2026)

Die Nachprüfung hat zwei relevante Fehlerpfade korrigiert:

- Dialogabschluss erfolgt nach der Bereinigung des Versuchs. Verschachteltes Öffnen ersetzt eine noch nicht akzeptierte Anfrage sofort logisch; die sichtbare Öffnung wartet auf das Ende der äußeren Operation. Der dauerhaft gehaltene Einstieg um Vanillas private Methode `RunOKAction` umfasst auch die spätere Editor-Überschreibbestätigung und Vanillas anschließendes Zurückschreiben des Dateinamens. Die Methode wurde gegen die echte installierte Spielassembly geprüft. Originalfehler werden nach der Bereinigung weitergegeben; fehlerhafte verzögerte Öffnungen überschreiben diesen Fehler nicht.
- ExtendedData verwurzelt seinen Runtime-Kandidaten vor der inkrementellen Veröffentlichung. Scheitert eine spätere Initialisierung, deaktiviert die Bereinigung die Funktionen und entfernt abmeldbare Subscriptions; bereits veröffentlichte Hooks und Trampoline bleiben verwurzelt. Das betrifft auch die Hookliste des Trail-Coordinators. Die deaktivierten Map-Einstiege laufen unmittelbar über ihre Originalmethoden.

Außerdem erfolgt die Lord-Vorbereitung jetzt in `Prepare`, nach sämtlichen Veto-Prüfungen. `PrepareSave` ist keine reine Prüfung: Es veröffentlicht Hostdaten und wendet Fixes-Präferenzen an. Fehlende Bereitschaft verwirft weiterhin den Bestätigungsversuch, bevor die ursprüngliche Aktion läuft. Die tatsächliche Multiplayer-Synchronisierung bleibt Bestandteil der offenen Spielabnahme.

Der erste abgeschlossene Quellschritt aus Etappe 2 umfasst:

- `APIShared/Content/APIShared.Content.csproj` mit Ziel `netstandard2.0`, ohne Spiel-, Extender-, BepInEx- oder sonstige Serializer-Abhängigkeiten. Der interne JSON-Parser wird ausschließlich innerhalb des APIShared-Repositories quellverlinkt.
- `APIShared.ModData.ModDataNamespaceReader`, `ModDataReadResult` und `ModDataReadStatus`: derselbe GUID-Namespace-Vertrag mit unveränderlichen verschachtelten Snapshots und ohne versehentliche Lord-Dateigrößenbegrenzung im generischen Leser.
- Die bisherige Implementierung in `ExtendedData.Core/ModDataNamespace.cs` ist durch einen Adapter ersetzt. Die alten öffentlichen Ergebnistypen, Enums und ihre Assemblyzuordnung bleiben erhalten. Lokale Lord-Auswahl, Hostdaten, Dateizugriff, Sidecar- und Snapshotgrenzen bleiben bei ExtendedData.
- Das APIShared-Paket enthält zusätzlich `APIShared.Content.dll` und dessen XML-Dokumentation. Build-, Release-Zulassung und Build-Provenienz berücksichtigen diese Dateien und Quellen. Neue Pakete binden die Content-DLL über `ContentSHA256` an ihren Bootstrap-Nachweis; fehlende, ausgetauschte oder nicht beanspruchte DLLs werden geprüft, auch in ZIP- und MAP-Paketen und nach der ersten Auswahl. Historische Pakete ohne Content bleiben unterstützt. Der neue Stand muss als vollständiges Paket einschließlich Bootstrap aktualisiert werden, weil alte Bootstrap-Binaries die erweiterte Nachweisform nicht kennen.
- Verbraucher referenzieren das zentrale Content-Paket; ExtendedData liefert keine eigene Kopie mit. Die Content-API besitzt eine separate Kompatibilitätsbaseline. Betroffene READMEs, Architektur und Integrationsdokumentation sind aktualisiert.

Prüfergebnis des abschließenden Arbeitsstands:

- APIShared: vollständiger Build mit `/workspace /nopause /noinstall` erfolgreich; 619 Tests bestanden. Zwei optionale Fälle sind übersprungen: der Fremdverbraucher-Scan ohne explizite Eingabe und der Audit eines aktuell nicht installierten Fixes-Binaries. Die Windows-Paket-/Wiederherstellungsprüfungen bestehen; Host-Bash-Fixtures benötigen weiterhin eine gesondert bereitgestellte Umgebung. Die 16 gezielten Dialogfälle und fünf Content-Fälle bestehen ebenfalls.
- Der Übergang zum SDK-Projekt ist mit dem bestehenden Framework-MSBuild geprüft. Ein inkrementeller `dotnet build` baut Content separat; die Runtime referenziert dessen Ausgabe. Ein direkter SDK-Projektverweis wurde durch diesen mit dem vorhandenen Toolset funktionsfähigen Buildweg ersetzt.
- ExtendedData: vollständiger Build mit `/workspace /apishared:dev /nopause /noinstall` erfolgreich; 72/72 Core-/Runtimefälle sowie die Upload- und JSON-Upload-Suiten bestehen. Die nochmals auf den endgültigen Quellstand begrenzten Shared-Boundary-Prüfungen bestehen.
- Der Vergleich mit dem vor diesem Teilumzug gebauten Core-Binary zeigt 600 unveränderte öffentliche/geschützte Metadaten-Einträge: Assemblyname, Typen, Signaturen, Parameterdefaults und Enumkonstanten. Ein absichtlich umbenannter öffentlicher Enumwert wird vom Vergleich abgelehnt. Die gebaute Core-Bibliothek referenziert ausschließlich `netstandard` und `APIShared.Content`.
- APIShared liefert die Content-DLL mit; ExtendedDatas gebautes Paket enthält nur seine beiden eigenen DLLs. Keine Installation, keine Commits und keine Veröffentlichung.

Nachweise dieser Nachprüfung:

| Gegenstand | Lokaler Nachweis |
|---|---|
| Vollständiger APIShared-Build | `APIShared/.local/content-review-final.log` und `APIShared/.local/build-proof.json` |
| Separat geprüfte Dialogfälle | `APIShared/.local/dialog-review-tests.log` |
| Content-Vertrag und Tests | `APIShared/tests/Content.Tests` und `APIShared/tests/ApiBaseline/Content.PublicApi.txt` |
| Endgültiger ExtendedData-Build | `ExtendedData/obj/content-review-build.log` und `.work/b/working/137.build.json` |
| Gebautes ExtendedData-Paket | `.work/b/working/137/ExtendedData/BepInEx/plugins/ExtendedData_Serp` |
| Endgültige Quell-/Lifecycle-/XAML-Prüfung | `ExtendedData/obj/content-review-boundaries-final.log` |
| Core-Vergleich einschließlich erfasster Oberflächen | `ExtendedData/obj/core-public-compatibility.log`, `.before.txt` und `.after.txt` |
| Negativnachweis des Core-Vergleichs | `ExtendedData/obj/core-surface-negative.log` |

**Nächster verbleibender Quellschritt:** Missionssettings-DTOs aus der gemischten `MissionModels.cs` und den Schema-3-Codec fachlich nach Content übertragen, unter Beibehaltung der alten Core-Typen und ihrer Adapter. `WriteAtomic`, Dateiauswahl, Koop-/Lord-Paketmodelle und die Runtime-Engine werden dabei nicht beiläufig mit übertragen. Etappe 2 ist insgesamt noch nicht abgeschlossen; Etappen 3 bis 5 sind weiterhin offen. Die ursprüngliche UI-/Spiel-/MP-/Coop-Abnahme bleibt ebenfalls offen.

Der nachfolgende Stand vom 19.10.2026 bleibt als historischer Nachweis erhalten.

## Umsetzungsstand der ersten Etappe (19.10.2026)

- Implementiert: APIShared-eigene Savegame-Checkbox, lokale dauerhaft gespeicherte Präferenz, Fallback bei fehlender nutzbarer UI, zentraler Dialogdienst und ExtendedData-Registrierungen für Lord-Freigabe und Editor-Speichern. Der Tooltip entspricht dem vorgegebenen Wortlaut.
- Effektive UI-Verfügbarkeit wird vor Vanillas Ausblendung erfasst. Ein ungebundener APIShared-UI-Beitrag bleibt per XAML-Fallback unsichtbar. Gespeicherte Missionssperren, Autorenregeln und die Bedeutung expliziter öffentlicher PrepareLoad-Aufrufe bleiben erhalten.
- Versionen konsistent aktualisiert: APIShared 0.12.1; ExtendedData einschließlich Core 1.0.15; ExtendedData benötigt mindestens APIShared 0.12.1. Die neue öffentliche API-Baseline enthält ausschließlich 21 zusätzliche Zeilen, keine entfernten bestehenden Verträge.
- APIShared: vollständiger Build mit `/workspace /nopause /noinstall` erfolgreich; 610 automatisierte Tests bestanden. Der zusätzliche Scan zuvor kompilierter Fremdverbraucher war mangels expliziter Eingabe übersprungen. Die 27 gezielten Dialog-/Savegame-/Restart-/Baseline-Fälle bestehen ebenfalls.
- ExtendedData: Build mit `/workspace /apishared:dev /nopause /noinstall` einschließlich vorgesehener Core-, Upload-, JSON-Upload- und Runtime-Prüfungen erfolgreich. Die gesonderten HostClientPresetTests im Modus `preset-storage` sowie die auf ExtendedData begrenzten Shared-Boundary-Prüfungen bestehen.
- Dokumentiert: verwalteter Dialog-Zeitpunkt in der nativen Baseline; neue Spielmember gegen die echte installierte Assembly geprüft. Die API-Dokumentationsprüfung und CRLF-/Diff-Prüfungen bestehen.
- Keine Installation ins Spiel, keine Commits und keine Veröffentlichung. Die Native-DLL und die Spielassembly stimmen mit den bei der Untersuchung genannten Baseline-Hashes überein.

Nachweise und Artefakte dieser Momentaufnahme:

| Gegenstand | Lokaler Nachweis |
|---|---|
| Endgültiger APIShared-Build | `APIShared/.local/save-dialog-build-final.log` und `APIShared/.local/build-proof.json` |
| APIShared-Paket | `APIShared/BepInEx/plugins/APIShared_Serp` |
| ExtendedData-Build | `ExtendedData/obj/save-dialog-build.log` |
| Quellstand, SDK und NoInstall des ExtendedData-Builds | `.work/b/working/126.build.json` |
| Gebautes ExtendedData-Paket | `.work/b/working/126/ExtendedData/BepInEx/plugins/ExtendedData_Serp` |
| Quell-/Lifecycle-/XAML-Prüfung | `ExtendedData/obj/save-dialog-boundaries.log` |
| Host-/Client-Presetfälle | `_inspect/HostClientPresetTests/save-dialog-storage.log` |

Noch offen sind der sichtbare Dialog einschließlich Hover und ausgefallener Bindung, tatsächliche Save-Loads mit beiden Entscheidungen und Spielneustart sowie echte Host-/Client-/Coop-Läufe. Auch die Runtime-Diagnose mit einem tatsächlich installierten alten ExtendedData-Binary ist noch im Spiel zu bestätigen. Die erste Etappe wird deshalb noch nicht als vollständig im Spiel abgenommen oder releasebereit bezeichnet. Etappen 2 bis 5 sind nicht umgesetzt; insbesondere wurde keine Content-Assembly angelegt.

## 1. Ziel und empfohlener Umfang

Die allgemeine Infrastruktur für Modsettings, Savegame-Auswahl und GUID-getrennte Zusatzdaten soll APIShared gehören. ExtendedData bleibt die eigenständige Erweiterung für konkrete Karten-, Trail-, Koop- und Lord-Pakete und verwendet diese Infrastruktur über öffentliche Schnittstellen.

Empfohlen wird ein schrittweiser Teilumzug. Das vollständige Verschieben von ExtendedData nach APIShared ist ausdrücklich nicht das Ziel dieses Plans: Damit würden alle APIShared-Nutzer zusätzlich die konkrete Koop-Trail-, Lord-, Editor- und Paketverwaltung erhalten. Deren Aktivierung, Hooks, Dateiverwaltung und Kompatibilitätsanforderungen gehören nicht automatisch zur gemeinsamen Bibliothek.

Das Ergebnis soll diese Eigenschaften haben:

- Savegame-Checkbox und korrekte Modsettings-Wiederherstellung funktionieren ohne ExtendedData.
- Karten, Trails und Savegames verwenden gemeinsame Dienste für Einstellungen; Dateiformate und Regeln werden dabei nicht vereinheitlicht, wenn ihre Bedeutung verschieden ist.
- Fremde Mods können Zusatzdaten lesen und Einstellungen integrieren, ohne ExtendedData-Implementierungen oder Workspace-Quellen einzubinden.
- ExtendedData verwaltet weiterhin seine Inhalte, Auswahloberflächen, Exporte und Lord-Synchronisierung.
- Jede fachliche Implementierung hat einen Besitzer. Verbleibende alte öffentliche Schnittstellen sind ausschließlich Adapter, keine zweite Engine.
- Keine Commits, Veröffentlichung oder Implementierung durch das bloße Erstellen dieses Plans.

## 2. Prüfgrundlage und Grenzen der Untersuchung

### 2.1 Geprüfter Quellstand

Die folgenden Angaben sind eine Momentaufnahme und vor Beginn der Implementierung erneut zu prüfen:

| Repository | Branch | HEAD bei der Untersuchung |
|---|---|---|
| Mod-Workspace | `develop` | `664cd027ae8e5d60e037c008406725eff39a08a4` |
| Eigenständiges APIShared | `dev/Serpens66` | `7dfd5e742e10f1d012241a2e01a83811aec06a3e` |

Der Workspace enthält andere, uncommittete Arbeiten. Die gezielte Statusprüfung zeigte keine Änderungen im APIShared-Checkout. Vorhandene Nutzeränderungen und geschützte Arbeitskopien aus `.work/INDEX.json` bleiben erhalten. Branches, Versionen und Zustand sind keine dauerhaften Voraussetzungen dieses Plans.

Geprüft wurden insbesondere Plugin-Initialisierung, Coordinatoren, öffentliche Datenzugriffe, relevante Core-Modelle und Codecs, Projektziele, bestehende Tests, APIShared-Verträge sowie Paketprüfungen. Die Untersuchung war eine Quell- und Architekturprüfung. Es wurden keine Builds, Spieltests, Tests mit einem echten Multiplayer-Client oder vollständigen nativen Funktionsaudits durchgeführt.

Die hier vorgeschlagene Verantwortungsverteilung benötigt keine neuen RVAs oder Änderungen nativer Spielalgorithmen. Vor jeder späteren Änderung von Hook-Einstiegen, Start-/Ladepfaden oder privaten Spielzugriffen sind die dafür vorgeschriebenen vollständigen featurebezogenen Audits und Prüfungen gegen die echte installierte Assembly durchzuführen. Dieser Plan ersetzt diese Nachweise nicht.

### 2.2 Wichtige Befunde

1. **Savegame-Verarbeitung liegt bereits überwiegend in APIShared.** `SavegameModSettings`, dessen Vorbereitung und `SavegameLoadCoordinator` besitzen Erfassung, Wiederherstellung, Restart-Prüfung und Multiplayer-Freigabe. ExtendedData liefert derzeit die Checkbox und ruft die Vorbereitung auf.
2. **Die Checkbox verliert ihre Auswahl mehrfach.** `EditorModSettingsSaveOptionsViewModel.OpenLoad`, `SelectSavegame` und `CloseLoad` setzen den angezeigten Zustand zurück. Eine dauerhafte Benutzerpräferenz existiert dort nicht.
3. **Der fehlende UI-Fall ist kein eigener Entscheidungszustand.** Die Hostvorbereitung verwendet ohne passende Auswahl bisher `PrepareLoad(path, false)`; beim lokalen Laden führt der vorhandene Datensatz ebenfalls zur gespeicherten Quelle. Deshalb muss der Fallback in der zentralen Vorbereitung korrigiert werden.
4. **Der Trail-Coordinator mischt mehrere Verantwortlichkeiten.** Er behandelt Trail-Oberflächen und Startpfade, aber auch Dokumentprüfung, Teilnehmerdiagnose, Snapshot-Erzeugung, Anwendung und Wiederherstellung von Einstellungen.
5. **Ein Teil vermeintlich zu verschiebender Infrastruktur ist schon zentral.** `ConvertJsonValue` delegiert bereits an `ModSettingsPresetJson.ConvertValue`; kompatible Teilnehmer werden über `SavegameModSettings.GetCompatibleParticipants()` bezogen. Diese Dienste erweitern und wiederverwenden, nicht nochmals implementieren.
6. **Quellenregistrierung ist derzeit ein Singleton-Vertrag.** `ModSettingsWorkingSourceRegistry.Register` akzeptiert nur denselben Anbieter erneut; ein anderer Anbieter führt zu einer Exception. APIShared darf daher nicht einfach zusätzlich einen eigenen Standardanbieter registrieren.
7. **Zusatzdatenzugriff ist mehr als JSON-Lesen.** `ExtendedDataModDataApi` unterscheidet lokale Auswahl, bestätigte lokale Lord-Pfade und Host-Snapshots. Ein fehlender Hosteintrag darf nicht durch eine zufällige lokale Datei ersetzt werden.
8. **Core und Runtime haben unterschiedliche Zielplattformen.** `ExtendedData.Core` verwendet `netstandard2.0`, APIShareds Runtime .NET Framework 4.8.1. Ein direkter Verweis der bisherigen Core-Bibliothek auf die Runtime ist keine saubere Lösung für spielunabhängige Verbraucher.
9. **Ein zusätzliches APIShared-Datenassembly benötigt Paketarbeit.** Die aktuelle APIShared-Paketprüfung weist weitere Runtime-Binärdateien außer der vorgesehenen APIShared-DLL zurück. Ein neues Assembly muss explizit in Build, Prüfung, Bootstrap-Nachweisen und Auslieferung berücksichtigt werden.
10. **Kompatibilität darf nicht aus Dateinamen abgeleitet werden.** Einige öffentliche Typen mit Namespace `ExtendedData` liegen physisch in `ExtendedData.Core.dll`. Bei Adaptern sind Assemblyidentität, Member und Rückgabetypen maßgeblich.

## 3. Zielverteilung der Zuständigkeiten

| Bereich | Zielbesitzer | Umfang |
|---|---|---|
| Savegame-Checkbox, Präferenz, Quellentscheidung | APIShared | Vollständige Übernahme einschließlich fehlender UI und Multiplayer-Hostvorbereitung |
| Gemeinsamer Lade-/Speicherdialog-Einstieg | APIShared | Ein Hookbesitzer, öffentliche Beiträge für Prüfungen und anfragebezogene Vorbereitung/Bereinigung |
| Modsettings-Dokumentmodell und Codec | APIShared | Allgemeine Dokumente, bestehendes Schema, Normalisierung und Größenregeln |
| Teilnehmerprüfung und Diagnosen | APIShared | Bestehende Teilnahmeentscheidung weiterverwenden; ergänzend strukturierte Gründe anbieten |
| Erstellung und Anwendung von Missions-Snapshots | APIShared | Gemeinsame Engine über vorhandene Endpoints und `ModSettingsApplication` |
| Autorenregeln und deren Materialisierung | APIShared | Gemeinsame Auswertung; ExtendedData entscheidet, welche Regeln zu einem Inhalt gehören |
| Karten-/Trail-Quellen | Geteilt | ExtendedData liefert Dokumente und Herkunft; APIShared materialisiert Einstellungen |
| GUID-getrennte JSON-Zusatzdaten | APIShared | Parser, Ergebnisverträge, isolierte Lesesnapshots und lokaler Dateizugriff |
| Bestätigte Lord-Datenquelle | Geteilt | ExtendedData bestätigt Auswahl und synchronisiert; APIShared stellt den allgemeinen Lesezugriff bereit |
| Karte mit Modsettings speichern, Sidecar anlegen/entfernen | ExtendedData | Inhaltsspezifische Autorentscheidungen und Dateiverantwortung |
| Custom-/Koop-Trails, Missionskatalog, Paketwahl und Vorschau | ExtendedData | Vollständige Fachfunktion einschließlich Grenzen kurzer Trails |
| Lord-/AIV-Auflösung, Pakete und Fixes-Präferenzen | ExtendedData | Auswahl, Identitäten, Vollständigkeit und Synchronisationsprotokoll |
| Workshop-Pakete für Lords und Trails | ExtendedData | Inhalt und Vorprüfung; allgemeine APIShared-Workshopdienste weiterverwenden |
| Modsettings-Suche, Presets, Rollen, Lifecycle, Networking | Bestehendes APIShared | Keine parallelen Systeme und kein zweiter Startdetektor |

`ExtendedDataRuntime`, `TrailMissionSettingsCoordinator` und `ExtendedData.Core` werden nicht jeweils als Ganzes kopiert. Herausgelöst werden fachlich zusammengehörige Teile entlang dieser Grenze.

## 4. Verbindliches Verhalten der Savegame-Checkbox

Diese Anforderungen wurden bereits mit dem Nutzer festgelegt und gelten unabhängig vom weiteren Umzug:

| Situation | Verhalten |
|---|---|
| Checkbox verfügbar, Save frei konfigurierbar | Gemerkte Auswahl anzeigen und beim Laden verwenden |
| Noch keine persönliche Auswahl gespeichert | Checkbox zunächst aus: Einstellungen aus dem Save |
| Letzte bewusste Auswahl geändert | Sofort lokal dauerhaft speichern; auch bei späterem Dialogabbruch behalten |
| Wechsel von Save, Partie, Dialog oder Spielprozess | Persönliche Auswahl erhalten |
| Checkbox tatsächlich nicht verfügbar | Aktuelle unterstützte Modsettings verwenden, soweit die gespeicherten Missionsregeln dies erlauben |
| ExtendedData fehlt oder ist deaktiviert | APIShared-Checkbox bleibt verfügbar |
| Legacy-/unlesbarer APIShared-Datensatz | Bestehendes Verhalten mit aktuellen Einstellungen erhalten; persönliche Auswahl nicht überschreiben |
| Save erlaubt ausschließlich gespeicherte Einstellungen | Sperre erhalten; erzwungenen Anzeigezustand nicht als Benutzerpräferenz speichern |
| Multiplayer-Client | Entscheidung des Hosts übernehmen; eigene Präferenz hat keine Autorität über Hostwerte |

Tooltiptext, einschließlich Schreibweise, exakt: **only by APIShared supported modsettings**. Keine zusätzlichen Sätze und keine eigenmächtige Übersetzung dieses ausdrücklich vorgegebenen Textes. Tooltip im vorhandenen dunklen Stil, mit Umbruch, Auflösungsskalierung und 60000 ms Anzeigedauer. Die Beschriftung kann weiterhin sprachabhängig sein.

Persönliche Auswahl, effektiver Checkboxzustand und anfragebezogene Ladeentscheidung sind drei getrennte Zustände. Eine interne Entscheidung muss mindestens zwischen fehlender Auswahl, aktuellen Einstellungen und gespeicherten Einstellungen unterscheiden. Ein globales Boolean reicht dafür nicht.

Die Präferenz gehört in APIShareds lokale BepInEx-Konfiguration und weder in Saves noch in Gameplay-Presets oder Netzwerkpakete. Der voreingestellte Wert ist `false`. Automatische UI-Aktualisierungen schreiben ihn nicht. Bei einem Schreibfehler bleibt die Auswahl für den laufenden Prozess erhalten; eine begrenzte Fehlermeldung darf keinen erfolgreichen Save-Load vortäuschen oder verhindern. Der nächste Spielstart darf nur tatsächlich gespeicherte Daten berücksichtigen.

Ein vorhandener, aber fehlgebundener, versteckter oder für diese Anfrage nicht nutzbarer UI-Beitrag zählt nicht als bestätigte Auswahl. Die bloße Existenz des ExtendedData-Plugins oder des Patchfiles genügt nicht als Verfügbarkeitsnachweis. Die absichtliche Sperre eines Saves ist davon getrennt zu behandeln.

Bestehende explizite öffentliche Aufrufe von `PrepareLoad` und `PrepareLoadWithRestartCheck` behalten ihre Bedeutung. Der neue Fallback greift bei fehlender anfragebezogener Entscheidung, nicht durch Umdeuten von `false` in bestehenden Verbraucheraufrufen.

## 5. Technische Architektur

### 5.1 Laufzeitdienste und Abhängigkeiten

APIShared besitzt die Laufzeitdienste in seinen bestehenden Bereichen `Savegames`, `ModSettings` und `Presentation`. ExtendedData referenziert nur deren öffentliche Verträge; APIShared erhält keine Referenz auf `ExtendedData.dll`, `ExtendedData.Core.dll`, Workspace-Shared-Quellen oder bekannte Modpack-GUIDs als fachliche Voraussetzung.

Für die tatsächlich übertragenen spielunabhängigen Dokumentmodelle und Codecs wird innerhalb des APIShared-Repositories ein kleines Projekt `APIShared.Content` mit Ziel `netstandard2.0` angelegt. Es ist eine normale Bibliothek ohne BepInEx-Plugin, Unity-, Steam-, Noesis-, Extender- oder MessagePack-Abhängigkeit. Seine Themen sind Missionssettings-Dokumente und GUID-getrennte Zusatzdaten. Die Runtime ergänzt typisierte Spielzugriffe, Snapshot-Serialisierung und Netzwerkautorität.

Diese zusätzliche Assembly ist durch die bestehende Plattformgrenze und die Erhaltung der alten Core-Schnittstellen begründet. Sie wird nicht zur Sammelstelle für Koop-Pakete oder beliebige Helfer. Eigene öffentliche Namespaces sind `APIShared.ModSettings` für die übertragenen Dokumentverträge und `APIShared.ModData` für Zusatzdaten. Bestehende APIShared-Typen werden dabei nicht in eine andere Assembly verlegt.

Die neue Bibliothek verwendet die im APIShared-Repository gepflegte interne DependencyFreeJson-Implementierung; bei Bedarf wird deren bestehende Quelldatei projektintern eingebunden, ohne physische Kopie und ohne Zugriff auf Workspace-Shared. Allgemeine Parser bleiben intern. Fachliche Codecs existieren genau einmal. Laufzeitabhängige Methoden wie Dateipublikation und Property-Konvertierung bleiben bei den dafür geeigneten vorhandenen Diensten.

`APIShared.Content.dll` wird ausschließlich mit APIShared ausgeliefert. ExtendedData referenziert sie mit `Private=false`; es wird keine zweite Kopie in dessen Pluginordner installiert. Die bisherige `ExtendedData.Core.dll` bleibt für deren verbleibende Inhalte und erforderliche öffentliche Adapter bestehen.

### 5.2 Gemeinsamer Dialogdienst

Der gemeinsame Einstieg ist der bestehende verwaltete Lade-/Speicherdialog. Ein vorhandenes geeignetes Extender-Ereignis hat Vorrang vor einem neuen Hook; die bisherige Quellensuche zeigte keine direkt passende veröffentlichte Ereignisstelle. Verfügbarkeit und reale Signaturen sind vor Implementierung erneut zu prüfen. Ist ein Hook erforderlich, besitzt ihn ausschließlich APIShared und hält ihn bis Prozessende.

Der Dienst bietet ownergebundene Registrierungen mit Mod-GUID, stabiler Registrierungs-ID und stabiler Reihenfolge. Beiträge erhalten einen anfragebezogenen Kontext mit Requestertyp, ausgewählter Datei, Generation und Ergebnis. Benötigte Phasen:

1. Dialog geöffnet: lokale UI anbinden und Auswahl beobachten.
2. Vor Bestätigung: Prüfungen ohne externe Ausführung; ein Veto verhindert die ursprüngliche Aktion.
3. Vorbereitung nach allen Prüfungen: beispielsweise Savegame-Snapshot vorbereiten oder Editor-Schreibentscheidung vormerken.
4. Abschluss: ursprüngliche Aktion zurückgekehrt, fehlgeschlagen, abgebrochen oder Dialog ersetzt; anfragebezogene Ressourcen genau einmal bereinigen.

Die ursprüngliche Aktion läuft pro akzeptierter Bestätigung höchstens einmal. Ein Veto darf keine halb vorbereitete Snapshotanwendung zurücklassen. Für gestartete Vorbereitungen werden Fehlerpfade rückwärts abgewickelt, ohne veröffentlichte Hooks zu entfernen. Fehler einer erforderlichen Freigabe verhindern den betroffenen Vorgang und liefern eine konkrete Diagnose; optionale Beobachterfehler verhindern keine unabhängigen Beiträge.

Ein zurückgekehrter UI-Callback ist noch kein abgeschlossener Kartenstart. Die endgültige Sitzungsübernahme bleibt beim bestehenden Missionslifecycle und der Savegame-Vorbereitung. Wiederholte Klicks, neuer Dialog und geänderte Datei invalidieren alte Anfragezustände; Neustartjournal und Dateifingerprint behalten ihre vorhandene Bedeutung.

ExtendedData registriert seine Lord-Freigabe und seine Editor-Speichervorbereitung. Seine eigene konkurrierende Dialog-Interception entfällt im neuen Quellstand. Dafür werden nur die tatsächlich benötigten Erweiterungspunkte veröffentlicht; kein allgemeiner austauschbarer Game-Callback-Bus.

### 5.3 Modsettings-Dokumente und Anwendung

Die neue Missionssettings-API erhält Operationen zum Parsen/Serialisieren, Validieren, Ermitteln betroffener Teilnehmer, Erfassen von Autorenwerten, Vorbereiten eines Anwendungssnapshots sowie Anwenden/Verlassen eines Missionskontexts. Runtime-Verträge referenzieren vorhandene `IModSettingsPresetEndpoint`- und WorkingCopy-Endpunkte und bleiben an `ModSettingsApplication` angeschlossen.

Transport, Dokument, Entscheidung und Anwendung werden getrennt:

- ExtendedData bestimmt anhand von Karte, Trail, Customize oder Trail Maker das Eingabedokument, Herkunft und Bearbeitbarkeit.
- APIShared validiert und materialisiert die unterstützten Hostwerte.
- Vor Änderungen werden Werte und erforderliche Endpoints geprüft. Erst danach beginnt die Anwendung.
- Die Anwendung erhält eine anfrage-/sitzungsgebundene Besitzzuordnung; normale Freigabe und Same-Mission-Restart werden über die vorhandenen Lifecycle-Verträge behandelt.
- Ein teilweise angewendeter Teilnehmer wird auch dann im Rollback berücksichtigt, wenn sein Setter nach einer ersten Mutation wirft. Der aktuelle Ablauf darf nicht ungeprüft kopiert werden.

Folgende Unterschiede bleiben ausdrücklich erhalten:

- Disabled-Mission-Baseline ist nicht dasselbe wie ein gewöhnliches Modstandard-Preset.
- Vorschau, Autoren-Arbeitskopie, direkter Missionsstart und materialisierte Customize-Werte bleiben unterschiedliche Anwendungskontexte.
- Feste Autorenwerte haben innerhalb des bestehenden Regelvertrags Vorrang; die Checkbox hebt keine festen Regeln oder Modussperren auf.
- Persönliche und Per-Player-Werte gehören nicht in das Hostdokument.
- Bei Trailstarts haben Trailquellen Vorrang. Kartenquellen werden nicht ungefragt zusätzlich angewendet.
- Persönliches Preset-Speichern verändert keine Trail-/Kartendatei. Nur ausdrücklich bestätigte ExtendedData-Speicherpfade schreiben bzw. entfernen diese Quellen.
- Veraltete Settings werden entsprechend dem bestehenden Verfahren behandelt; unbekannte Mod-GUIDs dürfen nicht schon beim Parsen still entfernt werden. Fehlende erforderliche Teilnehmer sind separat zu melden.

Die bestehende Wertkonvertierung in `ModSettingsPresetJson` wird wiederverwendet. Ein schema-3-Missionsdokument wird nicht in das schema-1-Format persönlicher Presets umgeschrieben. Die vorhandenen MessagePack-Base64-Marker und Zahlen-/Arraykonvertierungen bleiben erhalten.

Die Teilnehmerdiagnose wird bei APIShared ergänzt, ohne die Menge der akzeptierten Teilnehmer durch das Refactoring beiläufig zu erweitern. Der alte `ExtendedDataModSettingsOptOut`-Marker, das APIShared-Exclusion-Attribut und deren jeweilige Reichweite werden explizit abgebildet; diese Verträge nicht pauschal gleichsetzen.

### 5.4 Quellen und Autorenregeln

Der vorhandene `ModSettingsWorkingSourceRegistry` bleibt zunächst der öffentliche Singleton-Vertrag. APIShared registriert keinen zweiten Anbieter. ExtendedData bleibt Quellenanbieter und delegiert dessen allgemeine Materialisierung an den neuen Dienst. Seine Dateien, Quellenverfügbarkeit und Autorenoberfläche bleiben dort.

Die IDs `mod-default`, `trail`, `map`, Präferenzkontexte, Benachrichtigungsthread und vorhandene Fehlersemantik bleiben stabil. Eine allgemeine Mehranbieterregistrierung wäre ein eigenständiges zukünftiges Feature und ist für diesen Umzug nicht nötig.

Die bestehende `RegisterTrailCreatorRulesProvider`-Schnittstelle und ihre Aufrufzeitpunkte bleiben erhalten. Das Erzeugen und Auswerten der Regeln wird an den gemeinsamen Settingsdienst gebunden; ExtendedData liefert die Zuordnung zum aktiven Trail und den Customize-Nachweis. Es entsteht kein zweiter Regelcache mit eigener Sitzungslebensdauer.

### 5.5 GUID-getrennte Zusatzdaten

APIShared erhält einen allgemeinen Parser sowie Runtime-Lesezugriffe für die aktuelle Karte, eine ausdrücklich angegebene lokale Lord-Datei und den bestätigten Lord eines Spielers. Ergebnisse unterscheiden weiterhin Erfolg, fehlende Datei, fehlenden Namespace, ungültiges Dokument, Lesefehler, ungültige Anfrage und nicht verfügbare Hostdaten. Daten und verschachtelte Sammlungen werden als unabhängige unveränderliche Snapshots geliefert.

Datei- und Containerregeln werden aus der bestehenden Implementierung übertragen: UTF-8-Behandlung, BOM, Objektwurzel, GUID-Vergleich, Ablehnung mehrdeutiger Schreibweisen und Grenzen. Die aktuell bestehenden Lord-Grenzen sind 64 KiB je Sidecar, 16 KiB je Namespace und 256 KiB je gesamtem Lord-Snapshot. Das sind unterschiedliche Grenzen; der generische Kartenleser erhält nicht versehentlich die Lordgrenzen. Zusätzliche Einschränkungen wären eine getrennt auszuweisende Verhaltensänderung.

Für ausgewählte Lords wird ein typisierter optionaler Quellenanbieter registriert. Er liefert den bestehenden bestätigten Zustand mit Sitzungs-/Generationsbezug sowie den Unterschied zwischen explizit fehlenden Daten und noch nicht verfügbarer Bestätigung. ExtendedData adaptiert seinen vorhandenen Snapshot bzw. seine bestätigten lokalen Pfade; APIShared erhält keine Abhängigkeit auf dessen Snapshottypen.

Ohne diesen Anbieter bleiben reines JSON-Lesen, aktueller Kartenzugriff und der ausdrücklich lokale Dateizugriff möglich. Der Zugriff auf den bestätigten ausgewählten Lord meldet fehlende Verfügbarkeit, solange keine gleichwertig geprüfte Auswahlquelle vorhanden ist. Er sucht keine lokale Datei als Ersatz für fehlende Hostdaten. Die lokale Dateioperation ist im Vertrag als lokale Quelle kenntlich und kein Multiplayer-Autoritätsnachweis.

Lord-Auswahl, AIV-Auflösung, Manifestvergleich, Fixes-Präferenzen und deren Netzwerkprotokoll bleiben bei ExtendedData. Ein generischer Datei-/Namespace-Leser übernimmt keine automatische gameplaywirksame Anwendung der gelieferten Werte.

## 6. Bestehende Formate und Kompatibilität

### 6.1 Gespeicherte Daten

| Bestehender Vertrag | Behandlung beim Umzug |
|---|---|
| `APIShared-SavegameModSettings` | Identifier und bestehendes Saveformat erhalten |
| `ExtendedData-MapModSettings` | Bleibt von ExtendedData geschriebener Archiveintrag |
| `ExtendedData-LordData` | Bleibt bei ExtendedData einschließlich Legacy-Lesern |
| `ExtendedData-LaunchOrigin` | Bleibt bestehender Herkunftsnachweis; keine zweite Savequelle anlegen |
| Missionssettings schema 3 | Dokumentsemantik und Ablehnung nicht unterstützter Schemata erhalten |
| `.modtrail.json`, `Trail_Mission_*.modtrail.json` | Namensbildung und Dateiverantwortung bei ExtendedData erhalten |
| `modmap.json`, `.modlord.json` | Namen, GUID-Isolation und Statusunterscheidung erhalten |
| Persönliche APIShared-Presets | Format und persistierte Auswahl unangetastet lassen |

Keine beiläufige Umstellung auf `VersionedSaveData`, keine neuen Archiv-Identifier für bereits vorhandene Nutzdaten und kein automatisches Neuschreiben aller Sidecars. Der vorhandene versionierte Savedata-Dienst bleibt verfügbar, ist aber kein Anlass für eine zusätzliche Formatmigration in diesem Vorhaben.

### 6.2 Öffentliche Typen und alte Verbraucher

Vor dem Entfernen fachlicher Implementierungen werden die öffentlichen Flächen von `ExtendedData.dll` und `ExtendedData.Core.dll` einschließlich Assemblyzuordnung erfasst. Zu prüfen sind insbesondere `ExtendedDataModDataApi`, seine Ergebnis-/Statustypen, `ModDataNamespaceReader`, `ModSettingsDefinition`, `ModSettingsEntry`, `ModSettingsJson` sowie die veröffentlichten Kompatibilitätshelfer.

Alte Methodensignaturen, Rückgabetypen, Enums und Assemblyidentitäten bleiben für unterstützte veröffentlichte Verbraucher erhalten. Die alten DTOs werden an der Grenze explizit in neue Dokumente überführt; beide Typidentitäten nicht einfach austauschen. Statische APIs und Codecs leiten zur gemeinsamen Implementierung weiter. Adapter dürfen Daten kopieren und Statuswerte abbilden, aber keine zweite Fachlogik enthalten.

Die verbleibende Core-Bibliothek kann die neue `netstandard2.0`-Content-Bibliothek referenzieren. Dadurch bleiben auch reine Core-Verbraucher ohne Unity-/BepInEx-Start nutzbar. Bestehende öffentliche APIShared-Typen bleiben unverändert in ihrer bisherigen Assembly; neue öffentliche Typen und die zusätzliche Assembly erhalten eine passende API-Kompatibilitätsprüfung.

Die Herkunfts-API bleibt ein ExtendedData-Anbieter für das vorhandene `CustomizedLaunchOrigins`. Keine konkurrierende Moduserkennung und keine automatische Übernahme aller dortigen Zustandsfelder in APIShared.

### 6.3 Versionen und Installation

Die neuen APIShared-Dienste werden zuerst als additive Schnittstellen fertiggestellt, anschließend wird ExtendedData dagegen umgestellt. Plugin- und Manifest-Mindestversionen werden bei Fertigstellung gemeinsam aktualisiert; konkrete zukünftige Versionsnummern werden nicht vorab erfunden.

APIShareds Build-/Paketregeln müssen die neue Content-DLL gezielt aufnehmen: erwartete Dateien, Hash-/Buildnachweise, Installer, Workshop-Inventar und die betroffenen Bootstrap-/Pakettests. Die bisherige pauschale Ablehnung zusätzlicher Binaries wird durch eine genaue Liste der benötigten Assemblies ersetzt; beliebige DLLs bleiben ausgeschlossen. Die neue DLL darf weder in ExtendedData noch in fremden Verbrauchern zusätzlich ausgeliefert werden.

Die installierbare Zieleinheit für den Dialogumzug ist ein zueinander passendes APIShared-/ExtendedData-Paar. Die Matrix enthält auch APIShared allein sowie den neuen Stand mit deaktiviertem ExtendedData. Neuer ExtendedData-Stand mit zu alter APIShared-Version wird über dessen deklarierte Mindestabhängigkeit blockiert.

Neues APIShared mit älterem ExtendedData muss vor Freigabe ausdrücklich geprüft werden: ältere Binaries können noch den alten Dialog-Hook und Patch installieren. Solange keine belastbare konfliktfreie Koexistenz nachgewiesen ist, wird diese Kombination nicht als unterstützte Zielkonfiguration ausgeliefert. Paket-/Installationsvorprüfung muss sie erkennen und eine konkrete gemeinsame Aktualisierung verlangen; für manuelle Mischinstallationen ist eine Laufzeit-Kompatibilitätsdiagnose vor Veröffentlichung des neuen Dialogdienstes erforderlich. Kein nachträgliches Entfernen fremder veröffentlichter Hooks und kein stiller Parallelbetrieb. Eine Behauptung allgemeiner Rückwärtskompatibilität wäre hier ohne diese Prüfung falsch.

Bereits ausgelieferte XAML-/Paketpfade bleiben typgültig vorhanden. Beim alten Dialogpatch werden die übernommenen Savegame-Operationen entfernt, während benötigte Editoroperationen verbleiben. Falls ein ausgelieferter Patch vollständig leer wird, einen gültigen neutralen Patch einsetzen. Historische Paketnachweise bleiben unverändert.

## 7. Umsetzung in überprüfbaren Etappen

### Etappe 0: Ausgangsverträge und Sicherheitsgrenzen festhalten

- Aktuelle Branches, HEADs, Arbeitsänderungen und aktive Arbeitskopien erneut prüfen. Keine automatische Branchbewegung, Stash- oder Resetaktion.
- Öffentliche Flächen, Archiveinträge, registrierte Pakete, Dialogpatches und betroffene Hookstellen inventarisieren. Bei Reflection-/String-Verbrauchern reichen direkte C#-Referenzen als Suche nicht aus.
- Bestehende relevante Tests gegen den gewählten Ausgangsstand ausführen; bereits vorhandene Fehler separat dokumentieren.
- Für jeden tatsächlich veränderten Spielpfad den erforderlichen featurebezogenen Vanilla-/Managed-/Extender-Audit abschließen und melden. Neue Assemblyzugriffe gegen die echte installierte `Assembly-CSharp.dll` prüfen.
- Runtime-Precheck für JSON, Startup-Cleanup, langlebige Verwurzelung und alle erreichbaren Dispose-Pfade durchführen. Bereits veröffentlichte Hooks dürfen auch bei späterem Initialisierungsfehler nicht zurückgebaut werden.

Abnahme: Es ist eindeutig, welche Verträge übertragen, welche beibehalten und welche Verhaltenserweiterungen ausdrücklich beabsichtigt sind. Native oder Laufzeitunsicherheiten werden nicht als gelöst ausgegeben.

### Etappe 1: Ursprüngliche Savegame-Anforderung unabhängig fertigstellen

- Zentralen Dialogdienst und APIShared-eigene Checkbox samt lokalem Präferenzspeicher einführen.
- Fehlende Auswahl im lokalen Ladepfad und in der Hostvorbereitung auf aktuelle Einstellungen abbilden, unter Erhaltung der Sperren und Autorenregeln.
- ExtendedData-Dialoghook in Registrierungen umwandeln; Lord-Freigabe und Editor-Speicherentscheidung erhalten.
- Checkbox samt Bindung und Tooltip aus dem ExtendedData-ViewModel/Patch entfernen; Karten-/Trail-Checkboxen bleiben.
- UI-Initialisierung unabhängig von optionalen ExtendedData- und nativen Spezialdiensten halten. Ein Fehler einer anderen APIShared-Capability darf die Checkbox nicht unnötig deaktivieren.
- Saveidentität, Restart-Check, Abbruch und Sitzungstransfer auf den bestehenden Vorbereitungsmechanismus aufsetzen.

Abnahme: Alle Zeilen der Verhaltenstabelle aus Abschnitt 4 bestehen, einschließlich fehlender UI und echtem Multiplayer. Diese Etappe benötigt noch keine neue Content-Assembly und darf nicht bis zum Abschluss aller weiteren Auslagerungen aufgeschoben werden.

### Etappe 2: Spielunabhängige Dokumente und Codecs herauslösen

- `APIShared.Content` einführen und Paket-/Bootstrap-/Buildregeln zusammen aktualisieren.
- Missionssettings-Modelle, deren Schema-Codec und GUID-Namespace-Parser fachlich übertragen; die bisher gemischte `MissionModels.cs` gezielt aufteilen.
- Alte öffentliche Core-Typen als Adapter erhalten. Koop-, Missions-, Lordmanifest- und Paketmodelle verbleiben in ExtendedData.Core.
- Vorhandene Codec-/Grenzwerttests zur neuen Produktion migrieren. Separate Tests prüfen die alten Adapter gegen dieselben erwarteten Ergebnisse.
- `WriteAtomic` und runtimeabhängige Aufrufe an geeignete Besitzer delegieren; unterstützte alte Core-Aufrufe bleiben ohne Spielstart testbar.

Abnahme: Content-Bibliothek und alte Core-Verbraucher laufen ohne Spielassemblies; APIShared startet mit seinem vollständigen eigenen Paket ohne Nachbar-Repositories.

### Etappe 3: Gemeinsame Settings-Engine übernehmen

- Strukturierte Teilnehmerdiagnose an die vorhandene Teilnehmerermittlung anbinden, ohne neue Sonderlisten für bekannte Mods.
- Dokumentprüfung, Capture, Snapshot-Materialisierung, Quellenmaterialisierung und Autorenregelerzeugung aus dem Trail-Coordinator in APIShared übernehmen.
- Die unterschiedlichen Anwendungskontexte ausdrücklich modellieren; die bisherige Kombination vieler Booleans nicht als neue öffentliche API konservieren.
- Anwendung über vorhandene Endpoints, `ModSettingsApplication` und Missionslifecycle führen. Vorbereitung, tatsächliche Anwendung, Fehler-Rollback und Sitzungsübernahme getrennt testen.
- ExtendedData-Coordinator auf Inhalts-/UI-/Quellenzustand und Aufrufe der neuen Engine reduzieren. Quellenanbieter bleibt bei ExtendedData; Registry-Vertrag unverändert.
- Savegame-Regelauswertung an dieselbe fachliche Materialisierung anschließen, soweit ihre bisherige Semantik übereinstimmt. Save-Datenformat und Restore-Transaktion erhalten.

Abnahme: Karte, direkter Trailstart, Customize, Trail Maker, Save-Load, Restart und Abbruch liefern dieselben bisherigen Werte, Rechte und persönlichen Rückkehrzustände; allein die ausdrücklich vereinbarte Savegame-Quellentscheidung ändert sich.

### Etappe 4: Allgemeine Zusatzdaten-API übernehmen

- Runtime-Fassade für Karte und ausdrücklich lokale Lord-Dateien ergänzen.
- Optionalen bestätigten Lord-Quellenanbieter definieren und ExtendedData dagegen anbinden.
- Bisherige `ExtendedDataModDataApi` auf eine Fassade mit Status-/DTO-Abbildung reduzieren.
- Größenregeln aus dem bisherigen Lord-Codec als unabhängige Zusatzdatenvalidierung herauslösen; `LordDataSnapshot` verwendet sie weiter, behält aber Protokoll und Gesamtgrenze.
- Endzustände bei Kartenwechsel, Save-Laden, Restart, Providerfehler und fehlender Hostbestätigung prüfen. Veraltete Ergebnisse gehören nie zur nächsten Sitzung.

Abnahme: Neue Verbraucher verwenden APIShared allein für allgemeine Lesezugriffe; alte Verbraucher erhalten dieselben Daten und Fehlerklassen. Bestätigte Multiplayer-Daten bleiben maßgeblich.

### Etappe 5: Bereinigung, Dokumentation und Freigabenachweis

- Übertragene Fachimplementierungen in ExtendedData entfernen; nur erforderliche öffentliche Adapter behalten.
- Projektlisten, Quelllinks, Testprojekte, Paketdateien, Referenzen, Prüfskripte und Beispiele auf den endgültigen Besitzer aktualisieren.
- APIShared-Katalog, Integrationsleitfaden und Architektur um die tatsächlichen neuen Dienste ergänzen. Allgemeine Verträge dort erklären; ExtendedData-Guides beschreiben Inhalte und Bedienung und verlinken die gemeinsame API.
- Die betroffenen README-Abschnitte entsprechend der inzwischen ausdrücklich erteilten Nutzerfreigabe aktualisieren. Allgemeine API-Verträge bleiben in APIShareds Dokumentation.
- Versions- und Abhängigkeitsangaben erst nach bestandener fachlicher Abnahme atomar aktualisieren. API-Baselines anhand eines geprüften Diffs ergänzen, nicht fehlgeschlagene Erwartungen ungeprüft neu generieren.
- Unterstützte Pakete und deren Eingangsstände dokumentieren. Erst danach kann separat eine Integration oder Veröffentlichung beauftragt werden.

Abnahme: Ein Fremdmod-Beispiel mit eigener GUID funktioniert ohne Modpack-/ExtendedData-Referenz; die gemeinsame Installation funktioniert mit passenden Paketen und ohne doppelte Hooks, Handler oder Assemblykopien.

## 8. Test- und Nachweismatrix

### 8.1 Tests ohne gestartetes Spiel

| Thema | Erforderliche Fälle |
|---|---|
| Checkbox-Präferenz | Erststart aus; an/aus speichern und wieder einlesen; Save-/Dialogwechsel; erzwungene Zustände ändern Präferenz nicht; Schreibfehler |
| Ladeentscheidung | Sichtbare Auswahl, keine UI, falsche Generation/Datei, Legacy, unlesbar, SavedOnly, expliziter alter API-Aufruf |
| Dialogablauf | Alle vier Save-Ladearten, Editor-Speichern, Veto, wiederholte Bestätigung, Callbackexception, Abbruch, Ersatzdialog; ursprüngliche Aktion höchstens einmal |
| Dokumentformat | Schema 3, fremde GUIDs, leere Dokumente, widersprüchliche Player-/Fixed-Einträge, veraltete Properties, ungültige Werte und Grenzen |
| Materialisierung | Disabled-Baseline, Vorschau, Arbeitskopie, feste Werte, Spielerwerte, Backend-Endpunkte, Base64-Werte, Arrays und Zahlenkonvertierung |
| Transaktion/Lifecycle | Teilweise werfender Setter, Rollbackfehler, vorheriger Kontext, Abbruch, ersetzte Mission, Restarttransfer, Fehlstart |
| Quellen | Singleton-Vertrag, Quellen-IDs, Trailvorrang, explizites Map-Laden, Fehlerpropagation und keine implizite Mehrziel-Atomizität |
| Zusatzdaten | GUID-Isolation, unveränderliche verschachtelte Ergebnisse, UTF-8/BOM, Mehrdeutigkeit, getrennte Größenlimits und Statuswerte |
| Autorität | Unbestätigte Lord-Auswahl, explizit fehlender Hosteintrag, mehrdeutiger Name, falsche Sitzung, kein lokaler Ersatz |
| Kompatibilität | Vorher kompilierte Verbraucher, Enum-/DTO-/Assemblyidentität, Adapterparität, bestehende APIShared-Baseline |
| Paket | Content-DLL genau einmal; vollständige und unvollständige Pakete; unerwartete Binaries weiterhin abgelehnt; Hash-/Bootstrap-Nachweise |

Vorhandene passende Suiten verwenden: APIShared `Core.Tests`, `APISharedTests` einschließlich Savegame-Tests sowie `LobbyModSettingsPresetTests`; ExtendedData `ExtendedData.Tests`; relevante Fälle aus `_inspect/HostClientPresetTests`. Upload-/JsonUpload-Suiten nur zusätzlich ausführen, wenn tatsächlich deren Projekt-, Codec- oder Paketabhängigkeiten betroffen sind.

Die HostClientPresetTests werden über ihren vorgesehenen MSBuild-/EXE-Weg ausgeführt. Persistenztests mit atomarem Ersetzen benötigen den dokumentierten erhöhten bzw. unsandboxierten Lauf; ein Sandboxfehler ist kein bewiesener Produktfehler. APIShared-Tests bleiben unabhängig von benachbarten Workspace-Testassemblies.

### 8.2 Integration im Spiel

- APIShared allein: normale Menüs, Saveauswahl, Checkbox, Tooltip, Laden mit beiden Entscheidungen und nach Spielneustart.
- APIShared und ExtendedData aktiv sowie ExtendedData deaktiviert: genau eine Checkbox, richtige Bindung, unveränderte Editor-/Trail-Speicheroptionen.
- Fehlender oder wirkungsloser UI-Patch: korrekter Fallback; kein Rückgriff auf eine alte globale Auswahl.
- Frei konfigurierbares Save, geschützter Trail, Legacy-Save, direktes Kartenladen, Editor, Customize, Save-Wechsel aus laufender Partie und Restart.
- Rückkehr ins Menü: eigene Presets, lokale Werte und Auswahlzustände wiederhergestellt; keine fremden Werte dauerhaft in lokalen Presets.
- Echte MP-/Coop-Läufe mit mehreren menschlichen Teilnehmern: abweichende lokale Ausgangswerte, Hostauswahl vor Clientbeitritt, Join/Leave, fehlende Lord-Bestätigung, große Settings, verzögerter Start und Abbruch.
- APIShared-Startup-Cleanup: dauerhafte Callbacks und tatsächlicher späterer Dialog-/Missionsaufruf nachgewiesen. Ein READY-Log allein reicht nicht.
- Gemeinsame Installation mit lokalem Fixes-Mod und Script Extender; kein doppelter Dialogpatch und keine unzulässigen Thread→UI-Zugriffe.

Ein nicht erreichbarer Client wird als fehlender Laufzeitnachweis dokumentiert, nicht durch einen Solo-Lauf ersetzt. Automatisierte Tests oder erfolgreiche Builds beweisen weder Darstellung noch Multiplayer-Gleichlauf.

## 9. Risiken und verbindliche Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
|---|---|
| Größeres APIShared lädt unbeteiligte ExtendedData-Funktionen | Nur definierte Dienste übertragen; Paket-/Trail-/Lord-Runtime bleibt optional bei ExtendedData |
| Neue Content-DLL fehlt oder liegt mehrfach vor | Gemeinsame Paketprüfung, genaue Binaryliste und Referenzen mit `Private=false` |
| Alte Core-Verbraucher brechen | Alte öffentliche Assembly-/Typflächen erhalten, Adapter sowie vorher kompilierte Verbraucher testen |
| Zwei Anbieter oder Hooks konkurrieren | Registry-Vertrag erhalten; ein Dialogbesitzer; gemischte Alt-/Neupakete als Freigabethema prüfen |
| Anwendung setzt bei einem Fehler nur einen Teil der Mods | Vorbereiten vor Anwenden, Teilnehmer vor Mutation erfassen, vollständigen Rollback und Fehlerzustand testen |
| Benutzerwahl wird mit erzwungenem Savezustand verwechselt | Drei getrennte Zustände; Persistenz ausschließlich bei bewusster Änderung |
| Fall „keine UI“ verändert gespeicherte Missionsrechte | Entscheidung und Berechtigung getrennt auswerten; SavedOnly-/Konfliktregeln beibehalten |
| Lorddaten stammen vom falschen Peer oder einer lokalen Ersatzdatei | Bestätigte Quelle mit Session/Generation; keine implizite lokale Ersatzsuche |
| Protokollumzug verändert dynamische Paketregistrierung | Bestehende ExtendedData-Pakete in diesem Vorhaben nicht umbenennen oder nach APIShared verschieben |
| Neuer Fehlerpfad entfernt veröffentlichte Hooks | Prozessweite Verwurzelung; Fehler nur logisch deaktivieren; Kandidatenrollback ausschließlich vor Veröffentlichung |
| Unbeabsichtigte Formatmigration | Bestehende Identifier, Schemas, Größenregeln und Legacy-Fälle als Golden-/Negativfälle prüfen |

## 10. Endzustand und Nichtziele

Nach Abschluss besitzt APIShared die Savegame-Bedienung und Quellentscheidung, die gemeinsame Verarbeitung kompatibler Missionssettings und den allgemeinen Zusatzdatenzugriff. ExtendedData besitzt weiterhin Inhalte und die Entscheidungen über Karten-/Trail-Dateien, Koop-Pakete, Lord-/AIV-Auswahl, deren Synchronisierung und die konkreten Autorenoberflächen.

Nicht Bestandteil dieses Umzugs sind eine neue Modsettings-Synchronisierung, ein neues Saveformat, ein allgemeines Content-Downloadsystem, die Übernahme aller Fixes-Präferenzen, neue Koop-Spielregeln, eine neue Mehranbieter-Quellenregistry oder das automatische Abschaffen von ExtendedData.

Ein späterer vollständiger Umzug wäre eine eigene Produktentscheidung. Dafür müssten Aktivierung, öffentliche Identitäten, Workshop-Verteilung, sämtliche Paket-/Lordfunktionen und Altinstallationen separat geplant werden. Der hier beschriebene Teilumzug schafft dafür klare Schnittstellen, ohne diese wesentlich größere Änderung vorwegzunehmen.
