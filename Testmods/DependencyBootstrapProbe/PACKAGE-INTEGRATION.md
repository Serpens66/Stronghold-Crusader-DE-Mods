# Bootstrap-Paket: geprüfter Entwurf für die APIShared-Übertragung

## Aktueller Vorrang: unabhängige Testmod-Implementierung (17.10.2026)

Der APIShared-Entwicklungsstand wird vorerst nicht als Build- oder Quellabhängigkeit
verwendet. Die bereits vorhandene Übertragung bleibt unverändert liegen. Die neue
Offline-Implementierung und ihre Grenzen stehen in [package/CONTRACT.md](package/CONTRACT.md).
`tests/Test-PackageLifecycle.ps1` verwendet diese echten Komponenten; das frühere
`PackagePairModel.ps1` ist nur noch im Analysearchiv erhalten. Die unten beschriebenen
80 Modellfälle und damaligen Aussagen zum APIShared-Paketprüfer sind historische
Entwurfsnachweise, nicht der aktuelle Implementierungsstatus.

Der aktuelle lokale SE-Quellstand `10628df85cd432184742a50a1dee3969a66cf89e` bestätigt:
Abbestellen plant den Pluginordner, nicht den Patcher; Updates überspringen gleiche
oder ältere Versionen; der Windows-Updater löscht zuerst, kopiert anschließend und
startet Steam. Er wurde nicht ausgeführt. Vor Live-Entfernung bleibt ein verifizierter
Eigentumsadapter samt Koordination mit diesem Updater erforderlich. Das Vorhandensein
von Fixture-Statusdaten ersetzt weder Steam-Abonnementdaten noch einen Laufzeitnachweis.


Aktualisierung: Der Nutzer hat die gestufte Übertragung freigegeben. Der aktuelle
Implementierungsvertrag steht in `APIShared/docs/DEPENDENCY_BOOTSTRAP.md`
(workspace-relativ). Automatische Post-Exit-Bereinigung ist ausdrücklich ein späterer
Schritt; zunächst werden Code, Paket und manuelles Entfernungswerkzeug geprüft und
ohne Installation gebaut. Die folgenden Abschnitte dokumentieren den vorangegangenen
Entwurf und dessen Modellnachweise, nicht eine bereits freigegebene produktive
Installation oder einen erfolgreichen APIShared-Spielstart.

Stand: 16.10.2026. Dieser Vertrag beschreibt den nächsten Entwicklungsschritt;
APIShared enthält die Erweiterung noch nicht. Die installierte APIShared-Version
und die erfolgreiche aktive Korrekturprobe bleiben unverändert. APIShared-Arbeit
erfolgt ausschließlich auf `dev/Serpens66`, Build ausschließlich mit
`build.bat /nopause /noinstall`. Keine Veröffentlichung oder Versionsanhebung.

## Nachweise und Grenzen

Der Spielstart 21:54 bestätigt die tatsächliche Korrektur: 21 Gewinner, 11 originale
Indexeinträge, 10 Ergänzungen, 11 Anforderungen; Fehlerzahl 5 -> 3. Die originale
SE-Prüfung meldete exakt die fehlende Test-GUID und beide absichtlich unerfüllten
Versionsgrenzen. Die zwei bisherigen Fehlalarme verschwanden. Chainloader und
Post-Cleanup-Render wurden erreicht. Die Nutzerrückmeldung bestätigt ausschließlich
die TEST-ONLY-Meldung; eine separate ausdrückliche Menübedienbestätigung ist nicht
protokolliert. Kein weiterer Save-/Disabled-Test ist für diesen Startnachweis nötig.

Archiv: `_inspect/DependencyBootstrapProbe/Correction-accepted-20261016-2154`
(workspace-relativ). `checker-result.txt` wurde gegen die archivierten Dateien
erzeugt, einschließlich Kontrollmanifest und Paar der Diagnose-DLLs. Damit bleibt
die Auswertung nach Entfernen der Kontrollkomponente reproduzierbar.

`tests/Test-PackageLifecycle.ps1` führt ein Offline-Vertragsmodell aus. Es verwendet
echte archivierte Dateibytes, SHA-256 und den vorhandenen APIShared-Inventory-Writer
sowie ZIP-Prüfer. Die zweite Revision verändert Bytes bei gleicher Versionsnummer;
sie ist absichtlich keine ausführbare Releaseassembly. Es wird keine Pluginassembly
geladen. Auswahl-, Ownership- und Konkurrenzinformationen sind Testeingaben, keine
nachgewiesene neue Runtime-Erfassung. Resultate und Dateien bleiben in einem eigenen
Unterordner von `_inspect/DependencyBootstrapProbe/package-lifecycle` erhalten.

Das Modell prüft Erstinstallation und Upgrade in allen sechs Reihenfolgen von DLL,
Bootstrap und Manifest, mit Prüfung nach jedem Kopierschritt. Es prüft außerdem
fehlende/veränderte/vertauschte Dateien, falsche Auswahl, gleiche Version bei
anderen Hashes, Konkurrenz, Probe, Löschschutz, Reparse-Pfade und Wiederherstellung.
Es ist kein echter Steam-/Extender-Update-, Unity-Mono- oder Linux/Proton-Nachweis.

## Quellenvertrag des bestehenden Systems

- SE `API/Components/Archive/MapModManager.cs`: `TryStageModUpdate` entpackt den
  BepInEx-Baum. `ShouldUpdate` überspringt gleiche oder ältere Versionen. Abbestellen
  plant die Entfernung des Pluginordners und der SE-Verwaltungsdatei; es entfernt
  keinen außerhalb liegenden Bootstrap.
- SE `deps/data/mod-updater.ps1`: erst Löschmanifest, danach sequenzielles Kopieren
  mit Überschreiben. Keine Transaktion und keine erneute Hashprüfung vor Löschungen.
  Der Test modelliert diese Kopierschritte; er startet den Updater nicht, weil dieser
  anschließend Steam starten und seine Stagingdateien löschen würde.
- APIShared `WorkshopOwnership` erfasst konkurrierende Pluginpfade und verlangt
  einen abonnierten Eigentümer. Ein Abbestellkontext mit null verbleibenden
  Eigentümern und ein Patcherpfad brauchen eigene Behandlung.
- APIShared `tools/WorkshopPackage.ps1` und `WorkshopMapPackage` erlauben den neuen
  Patcherpfad derzeit nicht. Die bestehende ZIP-Ablehnung wird tatsächlich getestet.
  Inventory-v1 bleibt pluginrelativ; der vorhandene Writer inventarisiert das neue
  Paarmanifest bereits als gewöhnliche Datei ohne Pfadaufweitung.
- Der vorhandene APIShared-Workshop-Transaktionshelfer schützt APIShared und
  Patcherpfade vor allgemeinen Verbraucheroperationen. Diese Schutzgrenze wird
  nicht für beliebige Pakete geöffnet.

## Festgelegter Paket- und Aktivierungsvertrag

Feste Ziele, relativ zum Spielverzeichnis:

| Rolle | Pfad |
| --- | --- |
| Runtime | `BepInEx/plugins/APIShared_Serp/APIShared.dll` |
| Bootstrap | `BepInEx/patchers/APIShared.DependencyBootstrap.dll` |
| Paarmanifest | `BepInEx/plugins/APIShared_Serp/dependency-bootstrap-pair.json` |

Paarmanifest Schema 1 enthält ausschließlich `Schema`, `Guid` (`APIShared_Serp`),
`Version`, `RuntimeSHA256` und `BootstrapSHA256`. Hashes sind 64 kleingeschriebene
Hexzeichen. Keine konfigurierbaren Zielpfade. Das Manifest entsteht nach beiden
Builds; damit gibt es keine zirkulär eingebetteten DLL-Hashes. Es beschreibt
Konsistenz, keine kryptographische Herausgeberauthentifizierung.

Die Bootstrap-Assembly ist ein separates Projekt außerhalb des Runtime-Compile-
Globs. Eigene Quellen und interner APIShared-JSON-Parser, keine Workspace-Shared-
Quelllinks, keine frühe Referenz auf oder Ausführung der vollständigen APIShared-DLL.
Öffentliche APIShared-Signaturen und Verbraucher-Mindestversionen bleiben unverändert.

Aktivierung in zwei Stufen:

1. `Finish` richtet nur verwurzelten Listener und unabhängige Diagnose ein. Beim
   bewährten synchronen `Chainloader ready` werden eigene geladene Identität,
   kanonische Dateien, Paarmanifest und Loaderverträge geprüft. Ein vorhandener
   Diagnosepatcher blockiert den neuen Bootstrap vor jeder Hookinstallation, auch
   wenn dessen Probe derzeit deaktiviert erscheint. Mehrere eigene Bootstrap-
   Instanzen, andere Ablagepfade oder Reparse-Pfade sind ebenfalls Ablehnungsgründe.
2. Erst die echte Chainloader-Gewinneraufnahme legitimiert die Korrektur: genau der
   ausgewählte Anbieter `APIShared_Serp` muss kanonischem Pfad, Version und Hash
   entsprechen. Ein anderes ausgewähltes APIShared lässt die Indexkorrektur inaktiv.
   Auch bereits geladener Bootstrap und aktuelle Datei müssen übereinstimmen.

Der Chainloader-Beobachtungshook kann für Stufe 2 bereits erforderlich sein; ein
abgelehnter Anbieter bedeutet dann keine Indexkorrektur, nicht nachträglichen
Hookabbau. Keine zweite Gewinnerauswahl anhand von Verzeichnisnamen. Vollständig
geprüftes Paar aktiviert die Korrektur beim nächsten vollständigen Start;
unbekannte Loaderidentitäten bleiben inaktiv. Ein eigener logischer Ausschalter
wird vor Installation der Hooks geprüft. Veröffentlichte Hooks bleiben verwurzelt.

Die fachliche Korrektur bleibt die bestandene Probe: originale Einträge behalten
Vorrang, nur geeignete fehlende Anbieter kommen in eine separate Kopie. Erst nach
vollständiger Vorbereitung lokale Indexvariable umstellen; bei Fehler Original
verwenden. Keine Änderung an Kandidaten, Assets, Ladereihenfolge, Dialogen oder
zurückgegebenen Fehlern. Keine SerpsMods-Sonderregeln. Späteres erfolgreiches Awake
aller ausgewählten Plugins kann die frühe Prüfung nicht garantieren.

## Update und Entfernung

Der neue Bootstrap bleibt für sich allein ladbar und inaktiv, wenn Runtime oder
Manifest fehlen. Das muss vor jeder Runtime-Integration zusätzlich als IL-/Typ-
Vertrag und später im Spiel nachgewiesen werden. Die Dateipaarprüfung allein
beweist nicht die Fehlerfreiheit einer beliebigen defekten DLL.

Beim Upgrade auf ein neues vollständiges Paar wird derselbe Bootstrappfad
überschrieben. Keine vorgelagerte Bootstrap-Löschung einplanen. Jedes Teilpaket
bleibt hinsichtlich Korrektur inaktiv. Gleichversionige Wiederherstellung erfolgt
manuell bei geschlossenem Spiel durch vollständige geprüfte Neuinstallation;
kein Versprechen automatischer Reparatur durch den SE-Updater.

Für Abbestellen beziehungsweise spätere Entfernung des Features gilt ein eigener
enger Löschauftrag mit beim noch gültigen Paket erfasstem Bootstrap-Hash. Er darf
nur den obigen festen Bootstrappfad betreffen. Update verlangt genau einen
nachgewiesenen Eigentümer; Abbestellen verlangt null verbleibende Eigentümer plus
den früheren eindeutigen Eigentumsnachweis. Fehlende Subscription-Daten, fremde
Pfadansprüche, veränderte Dateien oder eine neue gestagte Fassung verhindern die
Entfernung. `SCDEModManagerLaunchId` sperrt eigene Dateioperationen.

Den Bootstrap NICHT als bloßen Pfad in das ungeprüfte SE-Löschmanifest aufnehmen.
Die spätere Umsetzung braucht einen eng begrenzten Post-Exit-Auftrag mit erneuter
Hash-, Link-, Konkurrenz- und Stagingprüfung unmittelbar vor dem Löschen. Dies
bleibt ein eigener APIShared-Wartungspfad, nicht eine Aufweitung des öffentlichen
Verbraucher-Transaktionsprotokolls. Unklare Reste bleiben erhalten und inaktiv;
manuelle, hashgeprüfte Entfernung dokumentieren. Kein rekursives Patcher-Aufräumen.

## Umsetzungsschritte und weitere Abnahme

1. Auf `dev/Serpens66` zunächst interne Policy und Bootstrapprojekt übertragen,
   inklusive bestehender Negativ-/Transformations-/Stack-/Lebensdauertests.
   Diagnoseprobe dabei weder neu bauen noch installieren.
2. Build, Provenienz und beide ZIP-/Map-Prüfer um ausschließlich den festen
   Bootstrappfad erweitern; Paarmanifest im bisherigen Plugininventory lassen.
   Publisher und Release-Staging müssen dasselbe vollständige Paar verwenden.
   Die Entfernung separat nach obigem Post-Exit-Vertrag implementieren und testen.
3. Offline-Fälle gegen die echte neue Implementierung wiederholen. Insbesondere
   Gewinnerbindung, konkurrierende Probe und Löschprüfungen nicht nur im Modell
   testen. Native APIShared-Verbraucherverträge unverändert lassen.
4. Relevante Prüfungen abschließen, dann einmal `APIShared/build.bat /nopause
   /noinstall`. Alle neuen Ausgaben nur im Workspace; installierte DLLs und
   Probe-Schalter vor/nachher per Hash vergleichen. Keine automatische Installation.
5. Erst nach gesonderter Freigabe später APIShared-Paar im Spiel prüfen. Vor diesem
   Versuch Diagnoseprobe gezielt entfernen, damit niemals beide Hookbesitzer
   gleichzeitig aktiv sind. Echter Windows-Update-/Entfernungstest und Unity-Mono-
   Nachweis bleiben bis dahin offen; kein neuer Spielversuch im aktuellen Auftrag.

## Ergebnis der isolierten Paketprüfung

80 Fälle bestanden unter frischer Windows PowerShell. Evidenz (workspace-relativ):
`_inspect/DependencyBootstrapProbe/package-lifecycle/6b091e5f13b1460299fad5292909d760`.
Dort stehen `results.json` und `provenance.json` mit den geprüften Quellen.
Acht installierte Binär-/Konfigurationsidentitäten wurden nach der Kontrollentfernung
gegen das Archiv verglichen und sind unverändert (`after-cleanup-identities.json`).
APIShared-Checkout unverändert auf `dev/Serpens66`; kein Runtime-Build erforderlich,
weil nur Offline-Testskripte und Planungsdokumente ergänzt wurden.
