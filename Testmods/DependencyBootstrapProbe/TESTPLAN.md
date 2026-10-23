# Dependency Bootstrap Probe: Schatteninventar und begrenzte Korrektur

## Aktuelle Entscheidung: inaktiver Bootstrap genügt (17.10.2026)

Automatische Bootstrap-Entfernung ist nach Nutzerentscheidung kein Pflichtziel.
Die folgende Entfernungsauswertung bleibt als Begründung erhalten; ihre Vorschau-
und Helferentwicklung wird nicht als nächster Auftrag verfolgt.
Ohne gültigen Partner muss der Bootstrap vor Hookinstallation abbrechen.
Diagnose/Listener dürfen bestehen bleiben; keine Korrektur ohne vollständiges
Paar und passenden Gewinner. Vorhandene Offline-Tests prüfen fehlende Partner.
Der eigene Unity-Mono-Nachweis dieser APIShared-Assemblies einschließlich
entferntem Pluginordner bleibt offen und benötigt eine separate Installationsfreigabe.

Die pauschalen Bootstrap-Installations-/Release-Sperren wurden auf dev/Serpens66
auf ausdrücklichen Nutzerauftrag entfernt. SDK-Verwendung ist bei entsprechender
Freigabe zulässig; NoInstall und technische Paket-/Dateischutzprüfungen bleiben.
Kein Build, keine Installation und kein Verbraucher-SDK-Wechsel in diesem Schritt.

## Entfernungsaudit vom 17.10.2026: Vorbereitung abgeschlossen

Ergebnis: Der vorhandene Extender bietet keinen ausreichend koordinierten
automatischen Bootstrap-Entfernungspfad. Die Offline-Entfernung bleibt auf
Workspace-Fixtures begrenzt. Kein Build, Spielstart oder Installationsauftrag;
APIShared-Code und installierte Probe bleiben unverändert.

### Geprüfte Grundlage

Installierte SHCDESE 2.14.2.0, SHA-256:
577F9AAB5DE504BE0BFA2193C7DEB734B26076ECBBAF0A408FC9ABC4CC5E7175.
Lokaler Fork: Commit 10628df85cd432184742a50a1dee3969a66cf89e,
Tree a612b537010c88e2837bb8895c9657e730365423, Arbeitsbaum sauber.
MapModManager wurde direkt aus der installierten DLL mit ilspycmd gelesen und
mit src/SHCDESE.BepInEx/API/Components/Archive/MapModManager.cs abgeglichen.
Der unten beschriebene Ablauf stimmt überein. Der Windows-Launcher verwendet
data/mod-updater.ps1 neben seiner Assembly. Dieses installierte Skript ist mit
deps/data/mod-updater.ps1 im Fork bytegleich, SHA-256:
627BE3BCCEBEFD57749B5E8C938F6312AB3110E8BC9D9D13E9EE16F52AFBFC1A.
Identitäten dokumentieren diesen Audit, keine neue Runtime-Freigabeliste.

### Ablauf und Beweisgrenzen

- Setup verwirft altes _SE/.staging. Fehlendes Staging beim nächsten Start
  beweist deshalb keine erfolgreiche Installation oder Entfernung.
- DetectAndStageUninstalls vergleicht die Namen der jeweils ersten gefundenen
  Map-Datei mit _SE/*.json, ohne Beachtung der Groß-/Kleinschreibung.
  Fehlende Ordner, fehlende Maps und umbenannte Maps können wie Abbestellungen
  aussehen. Eine vollständige Abonnementliste oder stabile Workshop-ID-Bindung
  wird an dieser Stelle nicht nachgewiesen.
- Vorgemerkt werden der Verwaltungsdatensatz und BepInEx/plugins/<GUID>,
  sofern die GUID verwendbar ist und der Ordner existiert. Der separate Patcher
  wird nicht erfasst; mehrere Pakete mit gleicher GUID werden nicht als
  Eigentumskonflikt aufgelöst. Anschließend werden Updates in dasselbe Staging
  geschrieben: Entfernung und Ersatz können im selben Vorgang auftreten.
- Der Originaldialog entscheidet erst danach. Ablehnung verwirft Staging;
  bei fehlgeschlagenem Updaterstart bleibt das Spiel bestehen. Die vorhandene
  Lifecycle-Aufnahme liegt vor dem Dialog und belegt keine Zustimmung.
- Nach Zustimmung startet der Extender den Updater und beendet das Spiel hart.
  Der Updater wartet auf die PID, pausiert eine Sekunde, löscht, kopiert Updates,
  entfernt Staging und startet das Spiel über Steam. Wartefehler werden wie ein
  bereits beendeter Prozess behandelt; einzelne Löschfehler werden unterdrückt.
  Es gibt keinen verbindlichen Abschlussbeleg oder Übergabepunkt vor Neustart.

LifecycleOwnership.Unique belegt nur einen beobachteten Pfadanspruch, keine
Bytegleichheit oder exklusive Eigentümerschaft. Ein gesetztes externes
Managersignal kann eine Ablehnung begründen; ein fehlendes Signal beweist keine
exklusive Verwaltung. Keine weitere Workshop-Abfrage: der entsprechende
Extender-Hook kann Updateprüfungen und Downloads auslösen.

### Integrationsvertrag und derzeitige Sperre

Vor Verlust des Pluginordners wäre ein eigenständiger Paketbeleg erforderlich:
eindeutige Paketzuordnung und Eigentümerschaft, GUID, tatsächlicher Gewinner,
Paarmanifest und beide Hashes. Dazu muss ein bestätigter Entfernungsvorgang
mit Startidentität und Deployment-Generation kommen. Pfadabwesenheit, Logtext,
Löschlisten-Eintrag oder Zeitablauf ersetzen diese Belege nicht.

Helfer und Beleg müssten in einem eigenen festen Werkzeug-/Zustandsordner
außerhalb des Plugins und außerhalb von Staging bestehen bleiben. Der Auftrag
darf keinen freien Löschpfad enthalten; einziges Ziel ist der eigene Probe-
Bootstrap. Vor Ausführung erneut prüfen: Prozessidentität einschließlich
Startzeit, Eigentum, Ersatzpakete, Konkurrenz, Hash und Reparse-Pfade.
Der vorhandene Windows-Helfer bindet Hashprüfung und Löschung an denselben
Dateihandle; Elternverzeichnisse, Paketverwaltung und Neustart werden dadurch
nicht transaktional geschützt.

Es fehlt eine vom tatsächlichen Updater respektierte gemeinsame Transaktion
für Dateiaustausch, Abschlussprüfung und Neustart. Ein eigener Mutex, Sleep
oder Warten auf verschwundenes Staging genügt nicht. Auch ein byteidentischer
Ersatz kann einen neuen Eigentümer haben. Eine momentane Prozessabwesenheit
ist keine Startsperre. Ein paralleler Löschhelfer wäre daher nicht freizugeben.

Keine Erweiterung der SE-Löschliste und keine Lockerung der Fixture-Grenze.
Eine koordinierte Updater-Erweiterung oder ausdrücklich übernommene
Deployment-Steuerung benötigt eine separate Architekturentscheidung.
Bis dahin bleibt die hashgeprüfte manuelle Entfernung bei geschlossenem Spiel
der begrenzte Rückweg; automatische Entfernung wird nicht vorgetäuscht.

### Konkret nächster Schritt und spätere Abnahme

Ein separater Umsetzungsschritt kann einen reinen Vorschau-Auswerter im Testmod
ergänzen: verifizierter Vorher-Paketbeleg, Deployment-Beobachtung und aktueller
Dateizustand hinein; Blocked mit Gründen oder EligibleForCoordinatedRemoval
heraus. Letzteres setzt belegte Koordination voraus und startet keinen Helfer.
Die heutigen realen SE-Belege müssen weiterhin zur Ablehnung führen.
Erfassung, Eigentumsbewertung und Diagnose bleiben getrennt; keine APIShared-
Quellabhängigkeit und keine öffentlichen API-Änderungen.

Nur neue Lücken testen: abgelehnter Dialog/fehlgeschlagener Updaterstart,
gleichzeitiger Ersatz auch mit identischen Bytes, unvollständige Eigentumsbelege,
gelöschter Pluginordner bei extern erhaltenem Paketbeleg, durch Setup verworfenes
Staging und fehlende Neustartbarriere. Die unveränderten 96 Paketfälle nicht
nochmals ausführen. Tests behalten nur einen finalen Bericht mit Quellhashes.

Ein echter Entfernungstest folgt erst nach Entscheidung und Umsetzung des
Koordinationsvertrags: eigenes Probepaket, geprüfter Vorherzustand, bestätigter
Entfernungsvorgang, geordnete Post-Exit-Ausführung und anschließend genau der
eigene Bootstrap entfernt bei unveränderten fremden Dateien. Eine künstliche
Paketzuordnung beweist keine Steam-Abbestellung. Kein normaler Wiederholungsstart
ohne neue Frage. Die APIShared-Spielabnahme bleibt separat offen.

Dieser Audit erzeugt nur diesen Abschnitt, keine neuen Fixture- oder Binärarchive.

## Abnahme: echter lokaler Paketwechsel bestanden (17.10.2026)

Der Extender hat nach Bestätigung seines Originaldialogs A 0.1.1 durch B 0.1.2
ersetzt und neu gestartet. Der B-Start erreicht am 17.10.2026 um 17:22:02.447
den Post-Cleanup-Marker (120 Renderframes); der Nutzer bestätigt das Hauptmenü.
Check-UpdateTrial -Phase After besteht: alle sechs installierten Dateien stimmen
mit B überein, tatsächlicher Gewinner und Paarbindung passen, Korrektur aktiv,
zehn geeignete Ergänzungen, Originalergebnis 0 Fehler wie vorbereitet. Dieser
Lauf hatte bereits ohne Ergänzung keine Fehler und ersetzt deshalb nicht den
früheren positiven/negativen Korrekturfunktionstest.

Beide realen Extender-Aufrufe vergleichen lokal 0.1.2 mit remote 0.1.2 und überspringen
das Testpaket. Kein erneuter Updatebedarf, kein Staging und keine Löschaufträge.
APIShared und Updaterskript sind unverändert. In den aktuellen BepInEx-/Unity-Logs
wurden keine Startfehler gefunden; zwei Suchtreffer auf „exception“ waren normale
Debugtexte zu fachlichen Ausnahmen, keine Exceptions.

Nachweise kompakt unter _inspect/DependencyBootstrapProbe/local-update:
stage-20261017-172023-complete.zip und after-20261017-172202.zip mit Hashlisten,
stage-check.log und after-check.log. Der echte Extender-Dateiaustausch und das
Laden des neuen Probe-Paars sind damit nachgewiesen. Steam-Download, Abbestellung,
automatische Bootstrap-Entfernung und die endgültigen APIShared-Assemblies bleiben
separate offene Nachweise. Kein weiterer Pflichtstart für diesen Versuch.

Abgeschlossen: Nach bestätigtem Spielende wurden mit Finish-UpdateTrial.ps1 -Apply
ausschließlich der hashgeprüfte Versuchsauftrag und der künstliche _SE-Datensatz
entfernt. Alle sechs B-Paketdateien und APIShared anschließend unverändert geprüft.
B und seine Korrekturschalter bleiben installiert. Nachweise: local-update/finish.log
und finished.json. Kein zusätzlicher Spielstart erforderlich.


## Lokaler Extender-Paketwechsel: vorbereiteter Versuch (17.10.2026)

Build/Installation abgeschlossen: A 0.1.1 ist installiert, B 0.1.2 liegt ausschließlich
unter `Testmods/DependencyBootstrapProbe/update-trial` bereit. Der installierte
Extender hat das echte Archiv gelesen; seine originale Versionsentscheidung erkennt
A -> B und lehnt B -> B ab. Alle relevanten Probeprüfungen bestanden. Die 96
unveränderten Dateilebenszyklusfälle wurden nicht wiederholt. APIShared und Updater
sind hashgleich zum Ausgangsstand. Hashnachweis: `_inspect/DependencyBootstrapProbe/local-update/installation.json`.
Der echte Austausch und der anschließende B-Spielstart sind ausdrücklich noch offen.

Der gemeinsame Quellenprüfer besitzt nun den expliziten Modus IndependentRuntime:
er benötigt einen Modpfad und lehnt deklarierte APIShared-Referenzen/Quelllinks ab.
Damit blockiert ein unabhängiger APIShared-Befund diese Probe nicht; der normale
Prüfumfang bleibt unverändert. Bei dieser Arbeit wurde APIShared nicht bearbeitet.

Der Paarbindungslauf vom 17.10.2026 16:13:46.761 ist bestanden und unter
`_inspect/DependencyBootstrapProbe/Pair-accepted-20261017-161346` samt Prüfergebnis,
Originalberichten, Logs und Hashliste archiviert. 20 Gewinner, zehn Ergänzungen,
sechs Anforderungen, Fehler 1 -> 0, zwei Lifecycle-Aufnahmen und Post-Cleanup.
Die Nutzerbestätigung des abgeschlossenen Starts bleibt getrennt vom Lognachweis.

Der nächste Versuch prüft ausschließlich den echten Extender-Dateiaustausch:
A 0.1.1 -> B 0.1.2. Beide ausführbaren Pakete haben dieselbe Implementierung;
die Versionsdifferenz ist ausschließlich für diesen Updateversuch erforderlich.
Der optionale Adapter kopiert die vom originalen Workshop-Aufruf gelieferte Liste
und ergänzt einen festen Workspace-Pfad. Originalabfrage und Originalliste bleiben
erhalten. Auftrag, beide freigegebenen DLL-Paare und Map-Hash müssen passen.
Reale und künstliche Pfade werden getrennt erfasst; der Testpfad begründet keine
Abonnement-, Eigentums- oder Löschberechtigung. Finish bleibt unverändert.

Das lokale B-Archiv enthält ausschließlich Runtime, Bootstrap, info.json,
Paarmanifest und zwei für B gebundene Schalter. Ein eigener künstlicher
`_SE/DependencyBootstrapProbeLocalTrial.json` stellt den Ausgangsstand A dar.
Dieser Aufbau testet weder Steam-Downloads noch automatische Bootstrap-Entfernung.
APIShared und die installierten Extender-Updaterskripte bleiben unverändert.

Ablauf: Start mit A; am ursprünglichen Update-/Neustartdialog warten, noch nicht
bestätigen. `Check-UpdateTrial.ps1 -Phase Stage` muss genau die sechs B-Dateien
und den eigenen Verwaltungsdatensatz nachweisen, ohne fremde Dateien oder
Löschaufträge. Dieser absichtlich unterbrochene Start benötigt keinen Menümarker.
Erst nach Prüfung den Originaldialog bestätigen. Nach Neustart Menü bedienen;
`-Phase After` prüft B, Paarbindung, Korrektur, leeres Staging und Post-Cleanup.
Kein Save-Test und kein zusätzlicher unveränderter Start erforderlich.

Nach erfolgreicher Abnahme und geschlossenem Spiel entfernt
`Finish-UpdateTrial.ps1 -Apply` nur den unveränderten eigenen Verwaltungsdatensatz
und Versuchsauftrag; ohne Apply erfolgt nur Vorschau. Die funktionierende Probe
bleibt installiert. Bis dahin den festen Ordner `update-trial/remote` erhalten.
Automatische Entfernung bleibt offen; Desktopprüfungen ersetzen den Spielversuch nicht.


## Aktueller Stand: Runtime-Paarbindung installiert (17.10.2026)

Der Lifecycle-Lauf 15:29:51 ist mit dem korrigierten Prüfer automatisiert bestanden:
20 Gewinner, zehn Ergänzungen, sechs Anforderungen, ein Fehlalarm -> null;
zwei vollständige Lifecycle-Berichte, keine Staging-/Löschaufträge. Archiv:
`_inspect/DependencyBootstrapProbe/Lifecycle-accepted-20261017-152951`.
Menübedienbarkeit bleibt eine separate manuelle Bestätigung. Die späteren
The-Leopard-Medienfehler werden nicht als Bootstrapfehler gewertet.

Die neue Probe wurde über `build.bat /workspaceprobe /nopause /lifecycle` gebaut
und installiert. `/workspaceprobe` ist die ausdrücklich erlaubte Ausnahme für
uncommittete Probequellen; normale Branchbuilds bleiben unverändert.
`patcher/Package` trennt Paarvertrag, Aktivierungsentscheidung und Diagnose.
Finish bleibt unverändert. Vor Hooks: PAIR_PREPARED; nach tatsächlicher Auswahl:
PAIR_BOUND. Nur gültiges Paar plus Korrekturschalter erlaubt den lokalen Indexwechsel.
Fehlende/veränderte Partner, falscher Gewinner oder konkurrierender Bootstrap
lassen die Korrektur inaktiv. Veröffentlichte Hooks werden nicht zurückgebaut.

Paket: feste Runtime-/Bootstrap-Pfade und `dependency-bootstrap-pair.json` im
Probe-Pluginordner. Manifest nach beiden Builds erzeugt, SHA-256-gebunden.
C#-Tests verwenden den tatsächlichen Runtime-Prüfer, einschließlich aller sechs
Kopierreihenfolgen mit Unterbrechungen. Die 96 bisherigen Paketfälle bestehen.
Prüfer akzeptiert fehlende historische Verbraucher im Lifecycle-Modus; bei
vorhandenen Verbrauchern bleibt deren erwartete Anforderung Pflicht.
Der historische Kontrollmodus bleibt streng. Originalfehler werden zusätzlich
nach Verbraucher/Abhängigkeit mit der Vorhersage verglichen.

Nächste Abnahme: `Check-Run.ps1 -Mode Active -RequirePair`, aktuelles Unity-/BepInEx-
Log, Hauptmenü und Post-Cleanup-Render. Kein Save nötig. Der neue Unity-Mono-
Paarbindungsnachweis steht aus. APIShared und Loader sind hashgleich geblieben.
Keine automatische Entfernung, keine Update-/Abonnementänderung.
Nachweise und Rückfallsicherung: `_inspect/DependencyBootstrapProbe/INDEX.md`.

## Historischer Stand vor diesem Schritt

## Aktueller Arbeitsschritt: echte Lifecycle-Zustandsaufnahme (17.10.2026)

Beobachter, Offline-Prüfungen, Build und Installation sind abgeschlossen.
Nach beendetem Spiel und Rückgabe des vom Nutzer verschobenen Pluginordners lief
`build.bat /nopause /lifecycle` erfolgreich. Sicherung, installierte DLL-Identitäten
und erneute Korrekturbindung sind geprüft; APIShared und Loader unverändert.
Der eigene Spielnachweis steht noch aus. Ziel ist die tatsächlich installierte
SE 2.14.2.0, Hash `577F9AAB5DE504BE0BFA2193C7DEB734B26076ECBBAF0A408FC9ABC4CC5E7175`.
Die erneute Prüfung dieser Version wurde ausdrücklich freigegeben. Historische
Spielnachweise mit 2.14.1 sind keine Laufzeitabnahme dieser Kombination.

`patcher/Lifecycle` trennt Hook, Datenerfassung und Eigentümerbewertung.
Finish bleibt unverändert. Der permanente Hook beobachtet die ursprüngliche
Workshop-Pfadliste, bereits geladene Archive und den Zustand nach Verarbeitung,
vor der Neustartentscheidung. Keine zusätzliche Workshop-Abfrage, Extraktion,
Updateausführung oder automatische Entfernung durch die Probe.

Der Bericht `BepInEx/DependencyBootstrapProbe.lifecycle.json` enthält Build-/Run-ID,
Gewinner, Paketansprüche, lokale _SE-Metadaten, Staging und Löschliste. Unique ist
eine eindeutige beobachtete Pfadzuordnung, kein Eigentums- oder Löschbeleg.
Externe Verwaltung bleibt ohne autoritative Informationen offen; ein vorhandenes
SCDEModManagerLaunchId wird nur als Hinweis erfasst. Konflikte bleiben ungeklärt.
Der bestehende Startlog belegt zwei originale Workshop-Aufrufe pro Start.
Jeder Aufruf erhält deshalb eine eigene ID und eine unveränderliche
`DependencyBootstrapProbe.lifecycle.<ID>.json`; die feste Datei zeigt den letzten
Bericht. Der Prüfer verlangt alle Aufrufe vollständig und in korrekter Reihenfolge.

Abnahme: `Check-Run.ps1 -Mode Active -RequireLifecycle`, Menübedienung und aktuelle
Unity-/BepInEx-Fehler prüfen. Der Prüfer verlangt vollständige, zum Start und Build
passende Marker/Berichte sowie weiterhin wirksame Indexkorrektur. Die entfernte
TEST-ONLY-Komponente wird nicht neu installiert. `Preview-Lifecycle.ps1` verwendet
danach ausschließlich die lesende Paarprüfung; ohne Paarmanifest ist deren
Ablehnung erwartbar. Der Entfernungstreiber wird nicht aufgerufen.

Build: `build.bat /nopause /lifecycle`, nur bei geschlossenem Spiel. Eigene Dateien,
Schalter und Logs werden vorher gesichert; die alte Korrekturbindung wird geprüft
und ausdrücklich auf die neue DLL geschrieben. APIShared und Loaderidentitäten
werden gegen Änderungen während des Vorgangs geschützt. Bei Startfehlern zuerst
Logs sichern, dann geschlossenes Spiel und ausschließlich eigene Sicherungen
wiederherstellen. Die historische Probe korrigiert unter 2.14.2 wegen ihres alten
Binärvertrags nicht; die Wiederherstellung ist kein SE-Downgrade.

Nachweise/Audit: `_inspect/DependencyBootstrapProbe/lifecycle-runtime` im Workspace.
Historische Paketfixtures wurden auf Nutzerwunsch unter `compacted/` verlustfrei
nach Inhalts-Hash zusammengefasst. Ergebnisse und wichtige Logs bleiben direkt
lesbar; ZIP-manifest.json ordnet sämtliche früheren Pfade den gespeicherten Bytes zu.
Ein erfolgreicher Start beweist Zustandsaufnahme, noch kein echtes Update/Abbestellen.
APIShared bleibt unverändert. Dort gelten SE-Mindestversion 2.14.0, keine gesetzte
Höchstversion und keine SE-Hash-Freigabesperre dieses Bootstraps.

## Abgeschlossener vorheriger Schritt: unabhängiger Paketlebenszyklus (17.10.2026)

APIShared wird in diesem Schritt weder geändert noch gebaut oder installiert.
`tests/Test-PackageLifecycle.ps1` prüft die tatsächlichen eigenständigen Komponenten
unter `package/`: Paarprüfung, Aktivierungsentscheidung und Post-Exit-Entfernung.
Die bisherige Modellimplementierung ist aus dem aktiven Testpfad entfernt und im
Analysearchiv erhalten. Es gibt keine APIShared-Imports oder Quelllinks.

96 Offline-Fälle bestehen in frischem Windows PowerShell: alle sechs Kopierreihenfolgen,
unterbrochene Paare, Gewinnerbindung, nachträgliche Änderungen, echte Dateisperren,
Prozessende, Hardlinks, Junctions und idempotente Entfernung. Der Worker prüft Hash
und Dateiidentität auf demselben Handle und darf ausschließlich Workspace-Fixtures
bearbeiten. Transformations-/Inventartests und Logprüfertests bestehen weiterhin.

Eigentümer-/Abonnementzustände sind noch explizite Fixtures. Automatische Integration
in den Extender, Laufzeitaktivierung dieser Paketlogik und koordinierte Live-Entfernung
sind damit nicht nachgewiesen. Die installierte Probe bleibt unverändert.
Vertrag: [unabhängiger Paketvertrag](package/CONTRACT.md).
Nachweise: `_inspect/DependencyBootstrapProbe/lifecycle-implementation/22a0aeb6bd3e4b9892369d214308c39f` (Workspace).


## Entwicklungsstruktur am 17.10.2026

APIShared bleibt auf `dev/Serpens66`. Der eigenständige Bootstrap ist unter
`bootstrap/src` nach `EarlyEntry`, `DependencyCorrection`, `Package` und
`Diagnostics` gegliedert; Tests und Paketwerkzeuge folgen den vorhandenen
APIShared-Projektbereichen. Der abschließende NoInstall-Nachweis wird unter
`APIShared/.local/dependency-bootstrap-evidence/STATUS.md` festgehalten.
Installation, eigener Unity-Mono-Nachweis und automatische Bereinigung bleiben
separate Schritte; die installierte Release-Version und Probe bleiben unverändert.

## Nachfolgende APIShared-Entwicklung

Die getestete Korrektur wird separat im APIShared-Checkout auf `dev/Serpens66`
übertragen und nur mit `/noinstall` gebaut. Die Probe bleibt unverändert installiert.
Der neue Bootstrap verweigert seine Hookinstallation bei erkannter Diagnoseprobe.
Die APIShared-Fassung benötigt später einen eigenen Spielnachweis; der bestandene
Probe-Lauf ist keine automatische Freigabe. Details stehen in
`APIShared/docs/DEPENDENCY_BOOTSTRAP.md` (workspace-relativ).

## Aktuell abgeschlossen: Korrekturversuch 16.10.2026, 21:54 Uhr

`Check-Run.ps1 -Mode Active -RequireCorrection` besteht: 21 Gewinner, Originalindex
11 Einträge, 10 Ergänzungen, 11 Anforderungen, Fehler 5 -> 3. Der lokale Index
wurde tatsächlich übernommen. Die originale Prüfung lieferte genau die drei
Kontrollfehler; beide bisherigen falschen BugfixesAndQoL-Warnungen verschwanden.
Chainloader-Abschluss, genau ein Prüfeintritt und Post-Cleanup-Render vorhanden.
Aktuelle BepInEx-/Unity-Logs ohne gefundene neue Startfehler. Nutzer bestätigt nur
TEST-ONLY-Meldung; separate ausdrückliche Menübedienbestätigung nicht protokolliert.

Archiv mit erneut bestandenem Prüflauf:
`_inspect/DependencyBootstrapProbe/Correction-accepted-20261016-2154` (workspace-relativ).
Reproduktion: `Check-Run.ps1 -Mode Active -RequireCorrection -GameDir <Archiv>
-ExpectedArtifactRoot <Archiv> -LogPath <Archiv>/BepInEx/DependencyBootstrapProbe.trace.log`.
Die drei Kontrollfehler werden ausschließlich mit diesem archivierten Kontroll-
inventar geprüft. Nach Entfernung der Kontrollkomponente ist derselbe Live-Aufruf
kein geeigneter Prüfer für weitere normale Nutzerstarts: er erwartet absichtlich
genau die drei Kontrollfehler. Das ist kein Fehler der weiterhin aktiven Korrektur.

Kontrollmanifest und leerer Ordner wurden bei geschlossenem Spiel hashgeprüft
entfernt. Probe-DLLs, eigene reguläre info.json, Active-/Correction-Schalter und
installierte APIShared-Version bleiben unverändert. Kein neuer Spielstart nötig.

Nächste Stufe: [isolierter Paketvertrag](PACKAGE-INTEGRATION.md), ausführbar mit
`powershell.exe -NoProfile -ExecutionPolicy Bypass -File
Testmods/DependencyBootstrapProbe/tests/Test-PackageLifecycle.ps1` aus dem Workspace.
Keine APIShared-Codeänderung oder Installation in dieser Stufe.

Nachfolgende Abschnitte sind chronologische Versuchsstände. Frühere Aussagen zu
ausstehenden Spielnachweisen werden durch den aktuellen Status oben abgelöst.

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

## Abgenommene Schattenstufe und Korrekturversuch

Der Schattenlauf vom 16.10.2026 19:46 bestand: 21 Gewinner, 10 originale Indexeinträge, 10 geeignete Ergänzungen, 8 geprüfte Anforderungen, Fehler 2 -> 0 ausschließlich in der Kopie. Alle 6 vorher erfüllten Anforderungen blieben erfüllt. Beide Originalwarnungen erschienen, keine gefundenen Startfehler, Chainloader-Abschluss und Post-Cleanup-Render vorhanden. Gesichert unter `_inspect/DependencyBootstrapProbe/Shadow-accepted-20261016-1946` einschließlich Vergleichsassemblies. Kein zusätzlicher Beobachtungs-/Disabled-Start erforderlich.

Der nächste Build kann den geprüften Schattenindex lokal übernehmen. Finish/Listener sind unverändert. Erst die späte Hookruntime liest `BepInEx/config/DependencyBootstrapProbe.correct`; Inhalt muss genau dem Patcher-SHA256 entsprechen. Ohne diesen Schalter bleibt Active ein Schattenlauf. Ein veralteter Schalter verhindert die Hookvorbereitung; ein Vorbereitungsfehler im Callback gibt den Originalindex zurück. Keine teilweise Befüllung des Originals, kein Entfernen von Ergebnisfehlern, kein Dialoghook.

Der vorhandene SE-Callback liefert den gewählten Index zurück; IL speichert ihn ausschließlich in der bisherigen lokalen Indexvariablen. Direkt danach bestätigt `CORRECTION_APPLIED` die wirklich gespeicherte Referenz. Ein stackneutraler Beobachter vor der originalen Rückgabe protokolliert jede `ORIGINAL_ISSUE` und `ORIGINAL_RESULT`. Der originale Ergebniswert wird unverändert zurückgegeben. `CORRECTION_PREPARED`, `CORRECTION_APPLIED` und übereinstimmendes Originalergebnis sind Pflichtnachweise; Schattenmarker allein genügen nicht.

### Kombinierter positiver und negativer Versuch

- `Prepare-CorrectionTrial.ps1 -AcceptedShadowRoot <gesicherter Schattenlauf>` erstellt ausschließlich im Workspace eine eigene leere Asset-Testkomponente. Positive Anforderungen werden aus den zwei im Schattenlauf gelösten Warnungen übernommen. Der Anbieter muss eindeutig geeignet sein.
- Aktuelle drei Kontrollfehler: GUID `DependencyBootstrapProbe_IntentionallyMissing` fehlt; BugfixesAndQoL 1.0.192 erfüllt weder Mindestversion 2.0.0.0 noch Höchstversion 0.0.0.0. Grenzen werden aus der geprüften Version abgeleitet. Die Test-GUID darf im Ausgangsinventar nicht vorhanden sein.
- Ziel ist ausschließlich `BepInEx/plugins/DependencyBootstrapProbeControl_Serp/info.json`; keine DLL/Assets. Der Name lautet `TEST ONLY - Dependency correction controls (3 expected errors)`. Vorhandene Zielordner werden nicht überschrieben.
- `trial/expectations.json` hält Manifest-SHA256, Anbieter, ursprüngliche Version und positive Vergleichspaare fest. `Test-CorrectionControl.ps1` prüft Identität und Grenzwerte. Der Aufräumtest prüft WhatIf, fremde Dateien, veränderte Hashes und wiederholte Entfernung.
- Nach allen Prüfungen `build.bat /nopause /correctiontrial` erhöht ausführen. Dieser Treiber installiert die zwei eigenen DLLs, das eigene reguläre Manifest und das Kontrollmanifest. Standard nach Build bleibt Observe.
- Anschließend `Set-ProbeMode.ps1 -Mode Active -Correction -ConfirmMenuAndUnityLog -AcceptedStartupRoot <gesicherter Schattenlauf>`. Archivierter Schatten-PASS, unveränderter früher Quellpfad, Loaderhashes, lokale/installierte DLLs und Kontrollmanifest werden vor Aktivierung geprüft.
- Nutzer startet einmal neu: Im Dialog ist nur die klar benannte Kontrollkomponente zu erwarten. Mit Ja fortsetzen, Menü bedienen und kurz warten. Die drei konkreten Kontrollfehler stehen im Log; die beiden bisherigen falschen Warnungen dürfen nicht mehr erscheinen. Kein Spielstand erforderlich.
- Abnahme mit `Check-Run.ps1 -Mode Active -RequireCorrection` und aktuellen BepInEx-/Unity-Logs. Erwartung bei unveränderter Auswahl: 21 BepInEx-Gewinner, Originalindex 11 Einträge einschließlich Kontrollmanifest, 10 Ergänzungen, 11 Anforderungen, Fehler 5 -> 3; tatsächliche Originalrückgabe genau die drei Kontrollfehler. Keine sonstigen Fehler, vollständige Start-/Rendernachweise.
- Erst nach erfolgreicher Auswertung und geschlossenem Spiel `Remove-CorrectionControl.ps1` ausführen. Es entfernt nur das hashbestätigte Kontrollmanifest und dessen dann leeren Ordner. Bei fremden Dateien, Reparse-Punkt oder verändertem Hash bleibt alles erhalten. Keine rekursive Löschung. Weitere vollständige Probeentfernung erfolgt separat über `Set-ProbeMode.ps1 -Mode Remove`.

Status: Korrekturcode und Kontrollversuch vorbereitet; Spielnachweis dieser Stufe noch offen. APIShared-/Extender-Dateien bleiben unverändert; produktive Integration separat.

Korrekturversuch am 16.10.2026 20:05:56 gebaut, installiert und mit Active + Correction aktiviert. Vier eigene Dateien lokal/installiert identisch; beide Aktivierungsschalter passen zum Patcher. Patcher-SHA256: 859A0F9A2AA63CE89CDBD8D7AE3C0A3AC56810CB3B8A19A20EBA9BD188AE1839. 42 synthetische Logpruefer-Faelle, 14 negative Hookvertraege, Indexwechsel-/Policy-/Aufraeumtests, 75 originale SE-Vergleiche und Pflichtpruefungen bestanden. Spielabnahme steht aus; Kontrollkomponente bis dahin installiert lassen.
