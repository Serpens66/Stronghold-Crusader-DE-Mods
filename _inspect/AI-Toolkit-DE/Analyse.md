# AI Toolkit: Bewertung der DE-Unterstützung

Stand: 22.10.2026. Untersucht wurde der unveränderte Branch `main` von
https://github.com/Schlossgespensty/AI-Toolkit bei Commit
`0a12082582b63a7d531ae4683e7689382773f892` (Paketversion 0.11.0).
Diese Bewertung ist eine Momentaufnahme; Quellstand und offene PRs vor einer
Implementierung erneut prüfen.

## Ergebnis

Das Projekt ist primär ein Editor für klassisches Crusader/Extreme mit UCP.
Es enthält aber bereits ausdrücklich entwickelte DE-AIV-Unterstützung.
Für einen zusätzlichen eigenständigen DE-Lord-Editor ist eine begrenzte,
mittlere Erweiterung plausibel. Einige JSON-Hilfsdateien allein genügen nicht.
Ein vollständiger Neubau des Editors erscheint für diesen Umfang unnötig.
Eine vollständige DE-Projektverwaltung einschließlich Spielgrafik, Karten,
Analyse und Medien wäre dagegen ein deutlich größerer eigener Arbeitsbereich.

Die aussagekräftigste Upstream-Quelle ist der bereits gemergte
[PR #5: Add DE castle support](https://github.com/Schlossgespensty/AI-Toolkit/pull/5),
gemergt am 20.09.2026. Die zugehörige lokale Datei
`docs/definitive-edition-integration.md` nennt Implementierung und Grenzen
ausdrücklich: experimenteller JSON-Austausch, keine bestätigte vollständige
DE-Spielkompatibilität. Die README bildet den aktuellen Stand nur unvollständig
ab: Sie beschreibt Electron, während der normale Start inzwischen Tauri nutzt.

## Lokaler Klon

Verzeichnis:
`D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Fremde Mods\AI-Toolkit`

- Vollständiger Git-Klon, `origin` zeigt auf das angefragte GitHub-Repository.
- Lokale `update.bat`, CRLF, über `.git/info/exclude` ausgeschlossen.
- Skript zeigt Status, führt `git fetch origin --prune` aus und aktualisiert
  ausschließlich `main` mit `git merge --ff-only origin/main`.
- Andere Branches und detached HEAD erhalten nur das Fetch; kein Reset,
  automatischer Mergecommit, Stash oder Branchwechsel.
- `/nopause` ist vorhanden. Der echte Aufruf wurde erfolgreich geprüft:
  bereits aktuell. Abschließender Git-Status: sauber.
- Für die Formatprüfungen sind die Laufzeitabhängigkeiten lokal in dem bereits
  ignorierten `node_modules` installiert. Keine Installationsskripte ausgeführt,
  keine getrackten Paketdateien geändert. Kein Desktop-Build durchgeführt.

## Architektur und konkrete Fundstellen

Alle folgenden Repositorypfade sind relativ zum genannten AI-Toolkit-Klon.

| Bereich | Zuständige Dateien | Bedeutung für DE |
| --- | --- | --- |
| Klassischer AIV-Codec | `src/node/aiv-codec.mjs`, `src/node/aiv-file.js` | Liest/schreibt klassische AIV-Sektionen, PKWARE-Kompression, Mapper-/Templateabbildung, Y-Transformation, klassische Markergrenzen. Das ist kein DE-BAIV-Codec. |
| Gemeinsames Burgdokument | `src/js/castle-editor.js` | Bereits `state.format` mit `aiv`/`aivjson`. Getrennte Normalisierung verhindert, dass klassische Kompaktierung auf DE angewandt wird. |
| DE-Burgvalidierung | `src/js/castle-format.js` | DE-JSON-Prüfung und Serialisierung; `classicIssues` verhindert verlustbehafteten klassischen Export bestimmter DE-Inhalte. |
| Aktiver Desktop-Dateipfad | `src/desktop/documents.ts`, `src-tauri/src/storage.rs` | Tauri-Öffnen/Speichern unterstützt AIV und AIVJSON. Character-Dialog filtert bisher nur `.json`. Rust behandelt allgemeine Character-Inhalte als Text. |
| Weiter vorhandener Electron-Pfad | `main.js`, `preload.js` | Ebenfalls AIVJSON-Unterstützung; bei einer Formatänderung berücksichtigen, solange dieser alternative Start weiter angeboten wird. |
| Burgkatalog | `config/aiv_constants.json`, `aiv_categories.json`, `aiv_templates.json`, `aiv_gamedata.json` | DE-Stockade, drei Outposts und acht Bedouin-Marker bereits vorhanden. Definitionen tragen teilweise `edition: de` oder `classicAiv: false`. |
| Character-Schema | `config/template.json`, `templateOrdered.json`, `fieldPools.json`, `optionPools.json`, Sections und Hilfetexte/Locales | Ein einziges UCP-Datenmodell, kein vollständiges separates DE-Character-Profil. |
| Character-Laden/Speichern | `src/js/character-editor.js` | `loadFromContent`, `renameKeysPreserveOrder`, `applyTemplateOrder`, `mergeDefaults`, `prepareOutputData`. Genau hier muss eine DE-Formatgrenze eingeführt werden. |
| Bibliothek und Installation | `src/node/ucp-library.js`, `src-tauri/src/library.rs`, `src-tauri/src/storage.rs`, `src/desktop/api.ts` | Erwartet `ucp/plugins`, UCP-Projektstruktur, `character.json` und klassische `.aiv`-Zuordnung. Keine vorhandene DE-Lord-Bibliotheksabstraktion. |
| Spielressourcen | `src/node/game-graphics.js`, `game-map.js`, `castle-exe-costs.js`, `src-tauri/src/game/` | Klassische GM1-/MAP-/EXE-/UCP-Datenquellen. Der DE-Spielordner kann nicht einfach als Ersatzpfad eingesetzt werden. |
| Analyse und Truppenvorschau | `src/js/character-planning.js`, `castle-troops.js`, `castle-analysis.js` | Greifen auf UCP-Feldnamen und teilweise klassische Regeln zurück. DE-Import allein macht ihre Ergebnisse nicht DE-genau. |

## AIV: Was bereits funktioniert und was offenbleibt

Das interne Burgmodell entspricht bereits der DE-JSON-Form:
`pauseDelayAmount`, `frames`, `miscItems`; pro Frame `itemType`,
`tilePositionOfsets`, `shouldPause`; pro Marker `positionOfset`, `itemType`,
`number`. Die ungewöhnliche Schreibweise `Ofset` ist Bestandteil des Formats.

Vorhanden sind Öffnen und Export von `.aivjson` sowie dem Alias `.aijson`,
getrennte klassische und DE-Speicherpfade, Erhalt leerer Frames und Pausen,
Marker-Nummerierung sowie unbekannter Felder und IDs im JSON-Pfad.
DE-spezifische Gebäude ohne klassische Grafik erhalten eine Vorschau mit
passender Grundfläche. Das ist keine vollständige DE-Grafikunterstützung.

Die klassischen Binärpfade bleiben absichtlich klassisch: unter anderem
AIV-Sektionsverzeichnis und Kompression, klassische Gebäudetemplates und
begrenzte Markerarrays. DEs `.baiv` ist ein anderer Binärvertrag. Für einen
Editor für Custom Lords ist dessen Implementierung nicht Voraussetzung:
`.aivjson` ist der relevante vorhandene Einstieg.

Gezielt geprüft wurden sechs vorhandene Testdateien:
`castle-format`, `aiv-format`, `aiv-file`, `aiv-native-codec`,
`character-aic-names` und `character-startup`. Ergebnis: **17/17 bestanden**.

Zusätzlich führt `inspect.cjs` die echten Upstream-Validierungs- und
Normalisierungsfunktionen auf lokalen Dateien aus. Ergebnis in `result.json`:

- 134 Dateien mit Endung `.aivjson` in CustomLords und ExtendedLords gefunden.
- 126 durchlaufen JSON-Serialisierung und Editor-Normalisierung mit strukturell
  identischem Ergebnis. Das ist keine Aussage über identische Textformatierung.
- Sieben Pestdoktor-Dateien werden bei reinen Pausenframes
  `{"shouldPause":true}` abgelehnt. Der Validator akzeptiert `{}`, verlangt
  für andere Frames aber zusätzlich `itemType` und `tilePositionOfsets`.
  Das ist ein konkreter Importfall für weitere Prüfung; ob diese Sonderform
  im Spiel gültig ist, wurde hier nicht nachgewiesen.
- Eine Datei im Unterordner `_LegacyMediaSource` ist ein ZIP mit falscher
  Endung und kein JSON; sie ist kein Beleg für einen Editorfehler.
- Unbekannter Bau-Itemtyp 160 kommt im Datenbestand vor; in den akzeptierten
  Dokumenten bleibt er erhalten. Sichtbare Bearbeitbarkeit und Spielwirkung
  wurden damit nicht geprüft.

Keine Datei im Spiel-/Lord-Verzeichnis wurde verändert. Kein GUI- oder
Ingame-Test durchgeführt. Der Test deckt Öffnen/Normalisieren/Serialisieren ab,
nicht sämtliche Zeichen-, Verschiebe-, Rotations- und Timeline-Aktionen.

## AIC/LORDJSON: Warum Konfigurationsdateien allein nicht reichen

Das Toolkit verwendet UCP-`character.json` mit drei Hauptbereichen:
`lord`, `startTroops`, `aic`. DE verwendet einen `lord`-Wrapper mit anders
benannten Feldern, numerischen Typwerten und echten Arrays.

| UCP-/Toolkit-Modell | DE-Dateivertrag | Erforderliche Umsetzung |
| --- | --- | --- |
| `aic.TaxesMin`, `TaxesMax` | `lord.min_tax`, `max_tax` | Feld- und Bereichszuordnung |
| `aic.BuildInterval` | `lord.build_rate` | Feldzuordnung; gleiche Wertsemantik separat prüfen |
| `aic.Farm1` bis `Farm8`, z. B. `AppleFarm` | `lord.farm_types` mit acht Zahlen | Arrayabbildung plus geprüfte Typ-ID-Tabelle |
| `aic.DefUnit1` bis `DefUnit8` | `lord.defensive_troops` | Symbol-/ID- und Arrayabbildung |
| `startTroops.normal/crusader/deathmatch` mit benannten Truppen | `lord.starting_troops_*` | Explizite Reihenfolge der DE-Arrays, zusätzliche Truppentypen |
| Klassische Auswahlpools | Neue DE-Einheiten und Maschinen | Weitere Optionen und ID-Domänen; AIV-Marker-IDs nicht mit AIC-Einheitentypen gleichsetzen |
| UCP-Erweiterungen wie `AIOxTethers_*`, `RunningUnits_*`, `AIVTroops_*` | Kein automatischer gleichnamiger Vanilla-DE-Vertrag | Format-/Fähigkeitsabhängige Felder und klare Exportregeln |

DE enthält außerdem Felder ohne direkte vollständige Abdeckung im derzeitigen
Schema: zum Beispiel `trade_flour_at`, `trade_pitch_at`, `lord_hps_percent`,
`siege_max_troops`, die beiden Wave-Multiplikatoren, `use_improved_sieging`
und ein deutlich größeres `siege_wall_troops`-Array.

Der reproduzierte Wolf-Ladeversuch zeigt die praktische Gefahr:

- Original: nur `lord`, darunter unter anderem `build_rate: 3`, `min_tax: 3`
  und ein echtes `farm_types`-Array.
- Nach den aktuellen Character-Normalisierungsfunktionen: zusätzlich
  `startTroops` und `aic` mit UCP-Defaults, darunter `BuildInterval: 0`,
  `TaxesMin: 0`.
- `farm_types` ist anschließend ein Objekt mit numerischen Schlüsseln,
  kein Array: `renameKeysPreserveOrder` behandelt Arrays wie allgemeine Objekte.

Damit ist weder bloßes Umbenennen der Endung noch ein Eintrag in
`legacyKeyMap.json` eine sichere Lösung. Diese Datei bildet alte UCP-Namen ab;
sie ist kein DE-Konverter. Der Versuch war ausschließlich im Speicher, ohne
Speichern über die App.

## Lokale DE-Quellen und Grenzen des Audits

Verwendete Quellen im Mod-Workspace:

- `_inspect/CustomisationFileManager.cs`: `LordRoot`, `NewAIC`,
  `ProcessExtendedLordFile`; die `.aivjson`- und `.lordjson`-Ladezweige.
- `_inspect/AIVLoader.cs`: `SaveData`, `Frame`, Markerstruktur und
  `SaveData.GetRawData` als verwaltete Serialisierungsgrenze.
- `_inspect/EngineInterface.cs`: `CreateAILordConfigData` und die Abbildung
  der öffentlichen Arrays auf die Transferstruktur.
- `shcde-script-extender/src/SHCDESE.AICDecoder/`: insbesondere
  `PublicAIC.cs`, `InternalAIC.cs`, Reader/Writer und README.
- `shcde-script-extender/src/SHCDESE.AIVDecoder/`: Modelle und dokumentierte
  JSON-/BAIV-Grenze.
- Reale Dateien in CustomLords und ExtendedLords, darunter Wolf als
  reproduzierbares Character-Beispiel; insgesamt 40 `.lordjson` im Datenbestand.

Die installierte `Assembly-CSharp.dll` wurde gehasht:
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Sie entspricht der verwalteten Baselinequelle. Diese Untersuchung betrifft
Offline-Dateiformate und den Editor, nicht die Ausführung von KI-Verhalten.
Es wurden keine nativen RVAs, Hooks oder Laufzeit-Patches hergeleitet und keine
DE-Gameplay-, Zeit-, Kosten- oder Pathing-Regeln als neu bestätigt behauptet.
Für solche Erweiterungen ist vor der Implementierung der separate
featurebezogene Native-Audit erforderlich.

## Sinnvoller PR-Zuschnitt

1. **Kleine AIV-Nachbesserung:** reale DE-Fixtures und Fehlerfälle absichern,
   insbesondere reine Pausenframes untersuchen. Bestehende DE-Unterstützung
   ergänzen. Kein neuer Codec und keine zweite Burgengine nötig.
2. **Mittlerer PR: eigenständige LORDJSON-Bearbeitung.** Expliziten
   Character-Formatstatus, Import-/Exportadapter und DE-Schema/Fähigkeiten
   einführen. Gemeinsame Oberfläche weiterverwenden. Originaldaten,
   unbekannte Felder, unbekannte numerische IDs und Arrays verlustfrei halten;
   UCP-Defaults nicht in DE-Dokumente mischen. Nicht darstellbare UCP-/DE-Werte
   dürfen nicht stillschweigend konvertiert werden. Dateifilter und Save As
   in Tauri und gegebenenfalls Electron ergänzen. Analysefunktionen nur für
   unterstützte Profile anbieten. DE-Dateien zunächst separat öffnen/speichern;
   die UCP-Bibliothek ist für diesen ersten PR nicht erforderlich.
3. **Weiterer mittlerer bis größerer PR: DE-Lord-Bibliothek.** Einen
   Bibliotheksprovider für CustomLords/ExtendedLords ergänzen: Verzeichnislayout,
   mehrere Burgen/Configs, Identität, Metadaten und Avatar. UCP-Projekte
   weiter über ihren bestehenden Provider verwalten. Medien und Workshop
   gesondert behandeln.
4. **Großer Folgebereich:** echte DE-Karten/-Grafiken, korrekte Kosten,
   Produktion, Truppenverteilung, Feuer und Pathing. Dafür eigene Datenquellen,
   Profile und überprüfte Regeln entwickeln; vorhandene Classic-Analysen nicht
   automatisch auf DE übertragen.

Für PR 2 sollten Tests reale LORDJSON-Dateien einschließlich DE-Truppen,
alle relevanten Arraylängen, unbekannte Felder/IDs, bekannte Feldänderungen,
formatgetreues Speichern, abgewiesene verlustbehaftete Konvertierung und
unverändertes UCP-Verhalten prüfen. Danach tatsächlicher Import im offiziellen
DE-Editor/Spiel. Einen vollständigen DE-Runtime-Build benötigt das Toolkit
als eigenständiger Desktopeditor dafür nicht.

Bei der Umsetzung den derzeit offenen
[PR #11](https://github.com/Schlossgespensty/AI-Toolkit/pull/11) berücksichtigen:
Er verändert ebenfalls Oberfläche/Rendering und enthält nach seiner
Beschreibung zusätzliche DE-Markierungsfunktionen. Diese Änderungen wurden
nicht in den Klon gemergt und sind kein Bestandteil der obigen Testergebnisse.

Es wurde kein Fork angelegt, kein Commit erstellt und kein PR veröffentlicht.
Der Auftrag umfasste Klon, lokalen Updater und Machbarkeitsprüfung.
