# MoveMoatTest – Erkenntnisse und Übergabestand

Stand: 6. September 2026

## Historische, durch Required-only abgelöste Teststufe: AttackUnit-Moat-Start und Fast-Path-Shadow (05.09.2026)

Dieser Abschnitt dokumentiert die Messung, die zum Rückbau führte. Der beschriebene
Shared-Modus und Fast-Path-Shadow sind im aktuellen Code nicht mehr vorhanden.

Die neuen Ingame-Messungen erklären, weshalb der Modus „Gemeinsame Gruppenwege“ den
680er-Angriff noch nicht deutlich beschleunigt. Der Standardlauf benötigte für den
synchronen `AttackUnit`-Befehl **594,776 ms**, davon **85,801 ms** gewichtete Suche bei
432 beteiligten Units. Der gemeinsame Lauf benötigte **625,262 ms**: sieben
Hauptsuchen (**14,203 ms**), **4,949 ms** Anschlussarbeit, 375 Wiederverwendungen und
57 Rückfälle reduzierten die gewichtete Phase auf **59,114 ms**. Der zentrale native
Builder `0xF4930` wurde aber weiterhin für jede tatsächliche Unit ausgeführt. Die
gemeinsamen 1–10 Suchgeometrien ersetzen daher bislang nur die modseitige Suche, nicht
die hunderten nativen Einzelbuilder. Zusätzlich erzeugten die synchronen Diagnosen im
Standardlauf 1.287 Zeilen/480.910 Zeichen und im gemeinsamen Lauf 960 Zeilen/394.941
Zeichen. Suchersparnis, unveränderte Einzelbuilder und Logkosten erklären zusammen das
enttäuschende Gesamtergebnis; ein sicherer Builder-Bypass ist damit noch nicht belegt.

Stufe 1 behebt jetzt den bestätigten Region-Fallback: `sourceRegion=0` ist bei
`AttackUnit` ausschließlich zulässig, wenn der konkrete aktuelle Starttile gültig,
ein fertig gebauter Moat und für den Spieler eigen oder verbündet ist. Der echte
Startregionwert muss weiterhin exakt passen. Feindliche Moats, normale Tiles,
ungültige Tiles und jede andere Region-0-Konstellation bleiben fail-closed. Die
konkrete Zielidentität, der lebende feindliche Zielkontext, Spieler-/Unitidentität,
die vollständige freundliche Moatroute und der bereits unterstützte
`targetRegion=0`-Sentinel bleiben unverändert maßgeblich.

Angriffsdetails werden nun pro `stage` auf drei repräsentative Einträge begrenzt und
erst nach dem synchronen Dispatch geschrieben. Gesamtzahl, Zeichenmenge und
unterdrückte Kategorien erscheinen in `attack-command-performance`. Exceptions,
Owner-/Puffer-Vertragsverletzungen und Rollbacks bleiben sofortige Fehler- oder
Warnmeldungen. `attack-command-summary` trennt Anzahl und Zeit von `0xDBC60`,
Qualifikation (einschließlich des darin verschachtelten Anteils), `0xF4930`,
Pfadaudit, gewichteter Veröffentlichung, gemeinsamem Haupt-/Anschlussweg,
Fast-Path-Shadow und nicht zugeordnetem Rest.

Der neue `fast-path-shadow` ist rein beobachtend. Er läuft nur für notwendige,
`Shared`-qualifizierte freundliche Moatpfade ohne Strukturen und ohne Arbeits- oder
Rekonstruktionsziel. Vor dem nativen Aufruf bindet er Unit-ID und Global-ID, Spieler,
Start/Ziel, Profil, Tick, Epoche, Terrain-/Placementrevision und exakt den eigenen
Unitpuffer; der gemeinsame kodierte Pfad erhält einen frischen Live-Kanten- und
Owner-Audit. Nach `0xF4930` werden Bindungen und Pfad erneut geprüft, die nativen Bytes
und Länge verglichen und Änderungen an Route-ready, Variante, den beobachteten
PathManager-Modi, Erfolgs-/Fehlerzählern und globalem Moatmodus aggregiert. Es wird
noch kein nativer Aufruf übersprungen und kein zusätzlicher öffentlicher Schalter
eingeführt.

Die Regressionfixture enthält eigene Prüfungen für eigenen/verbündeten Region-0-Moat,
gewöhnliche/feindliche Region-0-Starts, ungültige Tiles, positive Regionen und
Regionsabweichungen. Der aktuelle Lauf bestand mit **227.342 Assertions**, **8.999**
gerichteten Shared-Anschlussvergleichen, **18.258** unabhängigen Suchassertions,
**6.480** Gebäudefelddistanzen und **1.469.340** Cursorvergleichen; 21 Runtime-Dateien
und 180 tatsächliche Runtime-Member wurden kompiliert beziehungsweise ausgeübt.

Freigabe für Stufe 2 bleibt gesperrt, bis Ingame-Läufe mit 1/20/120/680 Units, beiden
Routenmodi, eigenen und verbündeten Moat-Starts sowie gemischten Starts weder
Identitäts-, Puffer-, Owner-, Kanten- noch Zustandsabweichungen zeigen und
`nativeBuilderMs` den dominanten Rest bestätigt. Erst dann darf ein eng begrenzter
Publisher `0xF4930` umgehen. Modversion bleibt **1.0.0**; README, öffentliche API und
Script Extender bleiben unverändert.

Nach allen Prüfungen wurde dieser Stufe-1-Stand am **05.09.2026 um 23:46:07** genau
einmal über `MoveMoatTest/build.bat /nopause` gebaut und installiert: **0 Warnungen,
0 Fehler**. Lokale und installierte DLL sind SHA-256-identisch:
`EB31ABC2EF43EF671822AED94F4DF3322A477302595638C87A3DDD9C879081CE`.
Die dabei verwendete kanonische `CrusaderDE.dll` stimmt mit der Native-Baseline
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` überein.

## Aktueller Teststand: Required-only ersetzt den Shared-Modus (06.09.2026)

Der frühere Modus 1 wurde selektiv anhand von Git-Commit `160c77d3` zurückgebaut.
`GroupRouteSession`, `SharedRouteField`, gemeinsame Connectoren, Shared-Zähler und der
beobachtende Fast-Path-Shadow sind aus Runtime, Projekt und Regressionen entfernt. Das
Settings-/UI-/Preset-Grundgerüst aus demselben Commit bleibt erhalten. Ein pauschaler Revert
wurde deshalb ausdrücklich nicht verwendet.

- Modus 0 **Individuelle Wege – genau** behält die bisherige profilgewichtete Auswahl und
  optionale schnellere Moatwege.
- Modus 1 **Nur notwendige Moatwege – schnell** ist neuer Standard und Resetwert. Gespeicherte
  Werte 0 und 1 behalten ihren Zahlenwert; ungültige Werte fallen weiterhin geschlossen auf 0.
- Der synchrone Aktivierungs-/Modussnapshot ist nun ein kleiner unveränderlicher
  `MovementOptionsSnapshot`. Verschachtelte Move-/Attack-Aufrufe übernehmen den Snapshot des
  laufenden Befehls und reagieren nicht mitten im Auftrag auf geänderte Settings.
- Required-only erfasst kein Unit-Geschwindigkeitsprofil. Zuerst wird ausschließlich die
  gecachte Boden-Erreichbarkeit geprüft. Gewöhnliche, strukturfreie Start-/Zielfelder derselben
  positiven nativen PCL-Region gelten dabei unmittelbar als bodenerreichbar; Moats, Region 0,
  Strukturen und ungültige Endpunkte sind von diesem Fast-Proof ausgeschlossen. Unterschiedliche
  positive Regionen verwenden weiterhin Topologieausschluss und exakte Bodensuche. Bei
  erreichbarem Ziel bleiben Modusfreigabe,
  Moatsuche, Weighted-Shadow, optionale Veröffentlichung und zusätzlicher Pfadaudit aus.
- Nur bei nicht erreichbarem Boden wird eine ungewichtete, kürzeste kodierbare Feldroute durch
  fertige eigene oder verbündete Moats gesucht. Veröffentlichung bleibt an konkrete Unit- und
  Global-ID, Spieler, Start/Ziel, Tick, Kartenepoche, Terrainrevision und den eigenen
  Unitpuffer gebunden. Kanten, Owner, Strukturvertrag und Roundtrip werden vollständig
  auditiert; jeder Fehler rollt den Puffer zurück und bleibt bei der einmaligen Vanilla-
  Builderausführung.
- Der reparierte `sourceRegion=0`-Vertrag gilt ausschließlich für einen konkret geprüften
  eigenen oder verbündeten fertigen Moat-Start. Gewöhnliche, feindliche und ungültige
  Region-0-Starts bleiben gesperrt; Vanillas `targetRegion=0`-Sentinel bleibt erlaubt.
- Required-only-Entscheidungen und Suchfelder sind an einen gemeinsamen Attack-/Move-
  Commandcache gebunden. Spieler, Start/Ziel, Tick, Kartenepoche, Terrainrevision, reservierter
  Endpunkt und Arbeitsziel bleiben Teil des Schlüssels; jede Abweichung erzeugt einen neuen
  konservativen Nachweis.
- Zusammenfassungen trennen Same-PCL-Treffer, Topologieausschlüsse, exakte Bodensuchen,
  Feld-/Entscheidungscachetreffer, Knoten, Required-Suchen, Publikation/Audit und exklusive Zeiten.
  Required-only verfolgt höchstens acht repräsentative Units pro explizitem Befehl; ungebundene
  Hintergrundwege sind auf acht gleichzeitige Tracker begrenzt und geben Plätze nach Abschluss
  frei. Move- und Hintergrunddetails sind pro Kategorie begrenzt, Unterdrückungen werden
  aggregiert. Shared-/Fast-Path-Felder entfallen. Erfolgreiche
  `weighted-path-consumer-contract`-Folgeprüfungen werden pro Tick und Befehl aggregiert;
  Verletzungen bleiben sofort sichtbar.

Die sieben Ingame-Großbefehle vor dieser Optimierung benötigten zusammen rund **1.712 ms**.
Davon entfielen rund **1.532 ms** auf den Bodenbeweis, aber nur rund **33 ms** auf alle nativen
`0xF4930`-Builder und rund **33 ms** auf Required-Suchen. Der Builder bleibt deshalb unverändert
einmal pro tatsächlichem Aufruf aktiv; die Optimierung setzt am positiven Bodenbeweis und an der
befehlweiten Wiederverwendung an. Die praktische Wiederholungsmessung mit demselben Save steht
noch aus.

Der finale Standalone-Lauf bestand mit **224.446 Runtime-Assertions**, **18.258**
unabhängigen Suchassertions, **6.480** Gebäudefelddistanzen und **1.469.340**
gerichteten Cursorvergleichen; 20 Runtime-Dateien und 177 tatsächliche Runtime-Member
wurden kompiliert beziehungsweise ausgeübt. Er prüft beide Modi, den neuen Default/Reset,
Preset/Trail/Client-Sperren,
den Same-PCL-Fast-Proof ohne Grid-/Moatsuche, unterschiedliche positive Regionen mit exaktem
Bodenbeweis, einen notwendigen eigenen Moatweg, den gemeinsamen verschachtelten Attack-/Move-
Cache, die Acht-von-680-Trackergrenze, Command-Snapshot-Stabilität, Gruppengrößen bis 1.000 sowie bestehende Puffer-, Owner-,
Kanten-, Angriffs-, Gebäude-, Queue-/Patrol-, Fill-/Dig-, Rekonstruktions- und
Rollbackverträge. Der endgültige Ingame-Performancevergleich mit identischem Save und
1/20/120/680 Units bleibt nach Installation erforderlich.

Die kanonische installierte `CrusaderDE.dll` ist SHA-256-identisch zur CURRENT-Baseline
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

## Historischer, durch Required-only abgelöster Stand: Host-Modsetting und gemeinsame Gruppenwege (05.09.2026, 22:56)

Der folgende Abschnitt bleibt als Mess- und Entstehungshistorie erhalten. Seine Runtime-,
Settings- und Logfeldbeschreibungen gelten nicht mehr für den aktuellen Modus 1.

Die unterbrochene Umsetzung wurde anhand des sauberen, inzwischen eingecheckten Arbeitsstands fortgesetzt. Die vorherigen Gebäude-, Angriffs-, Fill-, Cursor- und Verteilungsreparaturen bleiben erhalten. Nach Abschluss nochmals Code, API, native Verträge, Tests, Textdateien und installiertes Paket geprüft.

### Einstellungen und Lebenszyklus

- `MoveMoatSettings` verwendet das gemeinsame Presetsystem und `SerpLocalization`; Registrierung durch `LobbyModSettingsPresetRegistration.Register` während `Awake`. Die Settings und native Runtime bleiben statisch verwurzelt; kein Startup-Teardown und keine neue Unity-Update-Schleife.
- `EnableMod` und `RouteMode` sind `[SyncHostOnly]`. Standard: aktiviert und Modus 0 **Individuelle Wege – genau**. Modus 1: **Gemeinsame Gruppenwege – schneller**. Ungültige gespeicherte Moduswerte werden auf 0 normalisiert. Der geometrische dritte Modus ist nicht implementiert.
- Die Setter verwenden `CanMutateSetting` und die gemeinsame `OnPropertyChanged`-Schicht. Ein einfacher Aufruf der Extender-Basismethode `SetSynced` würde die zusätzliche Preset-Benachrichtigungsschicht umgehen; deshalb wird das vorhandene gemeinsame Mutationsmuster verwendet. Trail-Sperre, autorisierte Hostübernahme und Schutz lokaler Presets bleiben wirksam.
- Oberfläche: Host-Aktivierung, beschriftetes Preset, Zurücksetzen, Rollenstatus, HOST-OPTIONEN und Modusauswahl; beide Scrollrichtungen. Lokalisierung unter `Locales/de-DE.txt` und `Locales/en-US.txt`, XAML unter `Override/ScriptExtenderUI`. Diese Dateien werden vom bestehenden Buildpaket installiert.
- Der synchrone Auftrag hält seinen Aktivierungs-/Modussnapshot. Einstellungswechsel verändern keine bereits veröffentlichten Wege. Neue Cursor- und Bewegungsfreigaben berücksichtigen die Aktivierung; laufende Unit-Kontexte behalten ihre bisherige Sicherheitsprüfung.

### Gemeinsame Weggeometrie

`GroupRouteSession` trennt Spieler und kompatible vollständige Geschwindigkeitsprofile. Referenzwahl deterministisch über ganzzahligen Abstand zum arithmetischen Mittelpunkt, Gleichstand nach Game-ID. Mitglieder innerhalb zwölf Feldern Chebyshev-Abstand bilden eine Gruppe; übrige Mitglieder werden erneut gruppiert. Einzelmitglieder bleiben individuell.

Die Hauptstrecke entsteht erst bei einer passenden konkreten Anfrage. Native Ziele bleiben unverändert. Ziele außerhalb des zwölf Felder großen Endbereichs erhalten eine weitere Hauptstrecke beziehungsweise den individuellen Rückfall. Der Referenzstart wird als geometrischer Auftragssnapshot verwendet: Startet Vanilla die Referenzeinheit bereits, wird ihre alte Geometrie dadurch nicht für nachfolgende Mitglieder unbrauchbar. Aktueller Start und Identität jedes tatsächlichen Empfängers werden weiterhin individuell geprüft.

`SharedRouteField` baut zwei gewichtete Felder auf, jeweils höchstens 25 × 25 Felder: rückwärts um den Anfang und vorwärts um das Ende. Rückwärtssuche prüft die ursprüngliche gerichtete Kante. Mehrere Hauptstreckenknoten dienen als Anschlüsse; ihre Präfixkosten sind in den Seedkosten enthalten. Rekonstruktion verlangt Einstieg vor Ausstieg, keine doppelten Knoten und höchstens 2.000 kodierbare Kanten. Strukturwege ohne kalibrierte Kosten und terminale Arbeitskontakte verwenden das bisherige Verfahren. Ein reservierter Endpunkt wird nicht als allgemein durchquerbarer Anschluss geöffnet.

Notwendige Wege übernehmen die geprüfte gemeinsame Geometrie direkt in den qualifizierten Pfad. Sie wird ausdrücklich **nicht** als individuell optimal markiert. Die spätere gewichtete Phase erkennt dieselben bereits veröffentlichten Bytes und startet dafür nicht wieder eine individuelle Optimierung. Bei optionalen Abkürzungen gelten unverändert mindestens 40 Ticks Ersparnis im tatsächlichen Profil und eine Verbesserung in allen plausiblen Profilen. Scheitern Anschluss, Profilvergleich oder Audit, folgt die bisherige individuelle Suche. Auch ein interner Fehler der gemeinsamen Berechnung fällt darauf zurück; es entstehen keine Ersatzbefehle.

Caches gelten nur für den synchronen Auftrag, Kartenepoche, Tick und Zustandsrevision. ID-Wiederverwendung und Spielerwechsel werden geprüft. Relevante verschachtelte Unit-Aufrufe invalidieren die Suchdaten; Arbeitskontakte erhalten keine Gruppen-Geometrie. Unit-Puffer und Pläne werden niemals gemeinsam gespeichert. Beide Builder behalten ihren bestehenden Audit und Rollback. Die zusätzliche Gruppenroutine installiert keine neuen nativen Hooks.

### Tatsächliche Prüfungen

- Lokale Referenz und installierte API gegen Script Extender **1.42.0** geprüft; kanonischer Fork unverändert. Sämtliche 21 Runtime-Dateien einschließlich gemeinsamer UI-/Preset-Quellen semantisch geprüft.
- Native DLL erneut gegen Baseline geprüft: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Bestehende Maschinen-/ABI-Prüfung einschließlich Formation, Entzerrung, Angriffs-Producer und Gebäude-Consumer bestanden; keine neuen Hookbereiche.
- **227.337 Runtime-Assertions**, einschließlich wiederholter vollständiger Unit-Pre → Modus/Region → individueller Builder/Puffer → Unit-Post-Simulation. Beide Builder, beide Modi und Gruppengrößen bis 1.000 enthalten; bestehende Owner-, Endpunkt-, Fill-, Cursor-, Verteilungs- und Rollbackfälle erneut ausgeführt.
- **8.999 gemeinsame gerichtete Anschlusswege** gegen unabhängige Bellman-Ford-Referenz verglichen. Deterministische Mittelpunkt-/Radius-/Spieleraufteilung zusätzlich geprüft. Bestehende 18.258 Suchassertions einschließlich 6.480 Gebäude-Distanzvergleichen und 1.469.340 Cursorvergleichen bestanden.
- Settings-Test verwendet den tatsächlichen neuen ViewModel-Code, den gemeinsamen Presetcontroller und den Quellcode der Extender-Basisklasse aus 1.42.0; native UI/Netzwerkumgebung wird simuliert. Defaults, ungültiger Wert, Presetwechsel, Trail-Sperre, Client-Ablehnung, autorisierte Hostübernahme, lokale Dateiisolation und Reset bestanden. Ein älterer Presettest meldete zunächst eine fehlende Migration. Die neue Prüfung mit sichtbarer Fehlerausgabe wies eine Sandbox-Sperre beim atomaren `File.Replace` nach und besteht mit erhöhtem Dateizugriff; der ältere Test wurde nicht als Abnahmenachweis verwendet. Dies ist kein Nachweis eines echten LAN-Laufs.
- Finales Protokoll: `_inspect/MoveMoatRegressionTests/latest-shared-regression.log`. Kleine gemeinsame Produktionskorridore mit 1/120/680/1.000 Einheiten geprüft. Im letzten 1.000er-Korridor etwa **2,48 ms** und **1,58 MB** Allokationen; der Test enthält zusätzlich absichtliche Ablehnungen, Referenzbewegung und eine Invalidierung. Deshalb zwei Hauptsuchen statt einer. Separates Anschlussmodell: 1.000 Rekonstruktionen etwa **0,42 ms**, 1.250 Feldknoten. **Das sind kleine synthetische Karten, keine Spielbeschleunigung und kein Nachweis für unter 300 ms auf großen Spielkarten.** Kalte Initialisierung/JIT und unterschiedliche Prüfumfänge machen einen direkten Vergleich der Modellzeilen ungeeignet.
- Native Befehlszusammenfassungen enthalten `routeMode`, `sharedMain`, `sharedMainMs`, `sharedConnectorMs`, `sharedNodes`, `sharedReuse` und `sharedFallback` zusätzlich zur gesamten Befehlszeit und bisherigen Bewegungsdiagnose. Rückfälle zählen Berechnungsversuche, nicht zwingend unterschiedliche Einheiten.
- Projekt-/XAML-XML, Referenzdateien, Versionskonsistenz, Lifecycle, `git diff --check`, CRLF und Unicode geprüft. Keine README- oder öffentliche QoL-Bridge-Änderung.

### Build und verbleibende Spielabnahme

Nach allen Codeprüfungen am **05.09.2026 um 22:55:25** genau einmal den bestehenden `build.bat /nopause` direkt erhöht ausgeführt: **0 Warnungen, 0 Fehler**, Installation erfolgreich. Modversion weiterhin **1.0.0**. Lokales und installiertes Paket einschließlich XAML, beider Sprachdateien, PDB und Manifest stimmen byteweise überein. Neue Mod-DLL:

`133350936AFF34ECBAB944ACA674433140FC839BF99AB39FC4681CF4E4BB1DBF`

Die Oberfläche muss noch im Spiel materialisiert und beide Modi müssen unter gleicher Ausgangslage verglichen werden: besonders große Bewegungs-, Unit-Angriffs- und Gebäudeangriffsgruppen, Queue, Patrol, gemischte Starts sowie spätere Fill-/Dig-Zyklen. Echte Host-/Client-Synchronisierung und spätere Runtime-/Zielankunftsmarker stehen ebenfalls aus. Große Entfernungen innerhalb einer Armee, Profilunterschiede, Hindernisse zwischen nahen Units oder abgelehnte Anschlüsse können weiterhin individuelle Berechnungen verursachen. Unter 300 ms für 1.000 Einheiten bleibt ein Messziel.

## Aktuelle Reparatur: gemeinsame Gebäudeplatzprüfung

Der Spiellauf mit Mod-DLL D463D13A8320F8ED46AB6CEFAD31ACB5559DD0E5CEA81F721227C5A2F83A0EF8
zeigt am 05.09.2026 um 21:12:18 einen Gebäudeangriff von etwa 9513 ms (Pre/Post).
Davon: 9468,617 ms Consumer-Fallback, 8160 Einzelprüfungen (680 Units x 12 Plätze),
12.845.944 Suchknoten und anschließend zwölf bewertete Unit-Pfade. Beim Vergleich
um 21:13:09: etwa 32 ms, 351 bewertete Unit-Pfade. Die 339 fehlenden Plätze waren
native Annäherungsplätze mit Footprint null, keine fehlerhaften Datensätze.

Native Basis unverändert: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
1230BD/1230CA lesen den Gruppenführer aus Tribe+5A und seinen Unit-Slot;
1230D1/1230D9 lesen dessen aktuelle Y/X-Koordinaten. Consumer 123090 führt je nach
Variante genau einen DA590-/D9C40-/DB650-Flood aus, bewertet anschließend die Liste
und sortiert nur den zusammenhängenden gepaarten Präfix. DA020 erzeugt bewusst
weitere Einträge mit Footprint null. 11E960 verbraucht diese zur Bewegung; sie werden
nicht zu unmittelbaren Angriffsplätzen. Die native Kompaktierung setzt nur das erste
Wort des Endmarkers auf null; ein alter Companion dahinter ist kein weiterer Eintrag.
Diese Verträge sind im erweiterten Validate-PlacementContracts.py hashgebunden geprüft.

### Umsetzung

- Consumer-Schleife Units x Kandidaten ersetzt durch ein gerichtetes gemeinsames
  Distanzfeld ohne Pfadkodierung und gewichtete Optimierung. Native gültige Bewertungen
  bleiben erhalten. Der Führer liefert die primäre Distanz (Schritte+1); für übrige Ziele
  gibt es höchstens einen ergänzenden Mehrquellenlauf aus geeigneten Starts desselben
  gebundenen Spielers. Fremde Spieler liefern keine Freigabe. Ergänzende gepaarte Plätze
  werden hinter Führerplätzen bewertet; weitere Annäherungsplätze behalten native Reihenfolge.
- Feldspeicher wird pro synchroner Verschachtelung ausgeliehen und zurückgegeben;
  jeder Lauf beginnt mit frischen Generationen. Normale Kanten erweitern die Suche;
  besondere Endpunktkanten erreichen nur das konkrete Ziel. Reservierungen werden
  nicht zu allgemeinen Durchquerungen. Keine Suchdaten über Auftragsänderungen.
- Auch die vorgelagerte Gebäude-Regionsprüfung verwendet einen gemeinsamen Mehrquellenlauf
  statt Unit-x-Endpunkt-Suchen. Regionsentscheidungen bleiben pro Producer-Kontext gecacht.
- Null-Footprint-Plätze bleiben in der nativen Liste und sind ausdrücklich veröffentlichte
  Bewegungsziele. Echte Angriffspaare bleiben separat geprüft. Zielidentität, Unit-Identität,
  eigene Puffer, Live-Kantenaudit und Rollback bleiben zwingend.
- Der Angriffssuchkontext bleibt über Unit-Frames erhalten; Arbeitskontexte haben Vorrang.
  Ein Kandidatenfeld ist kein individueller oder kostenoptimaler Unit-Pfad. Die vorhandenen
  40-Tick-/Profilregeln sind unverändert. Keine neuen nativen Hooks.
- Angriffszusammenfassungen enthalten elapsedMs, Consumer-/Producer-Diagnosen sharedNodes.
  Consumer-Ausgaben unterscheiden attackPlaces und approachOnly. Synchrone und Post-Details
  sind jeweils auf 24 begrenzt; aggregierte Erfassung bleibt unabhängig davon.

### Prüfungen und Grenzen

173.683 Runtime-Assertions, 18.258 unabhängige Suchprüfungen (darunter 6.480 neue
Gebäude-Felddistanzen) und 1.469.340 gerichtete Cursorvergleiche bestanden.
167 tatsächliche Runtime-Member plus vollständige Suchkerne werden verwendet.
Der reale Consumer verarbeitet 351 Kandidaten (12 Paare + 339 weitere Plätze), publiziert
sie; die Fixture bildet die native sequenzielle Zuweisung über Unit-Pre, Modus, beide
Builder und Unit-Post nach. 1/120/680 Units, getrennte Komponenten, ungeeignete Units,
Feindfelder, geänderte Gebäude-Global-ID, stale Endmarker und unveränderter kompletter
Puffer bei Eigentümerfehler geprüft. Unabhängiger Matrix-BFS prüft gerichtete und
terminale Kanten sowie frische Feldverwendung nach Terrainänderung. Sicherheitsregressionen bestehen.

Im schmalen Produktionsmodell besuchen alle Gruppengrößen 404 Knoten für 351 Plätze;
keine gewichtete oder kodierende Suche während der Kandidatenbewertung. Aufgewärmt
etwa 0,084/0,089 ms für 120/680 Units mit Moat und 0,028/0,031 ms ohne Moat. Diese kleine
Karte ist kein Nachbau des Spiellaufs und erlaubt keinen Beschleunigungsfaktor für dessen
9513 ms. Feldspeicher einmalig rund 7,7 MB pro maximaler gleichzeitiger Verschachtelung;
spätere Läufe verwenden ihn erneut. Native Kandidatenzahl bleibt maßgeblich: 351 Plätze
bedeuten nicht, dass alle 680 Einheiten einen Platz erhalten müssen.

Nächste Spielabnahme: gleicher Gebäudeangriff mit/ohne Moat, gesamte Befehlszeit,
tatsächliche Bewegung und spätere Arbeit/Angriffe. Mehr bewegte Units verursachen mehr
individuelle Wegarbeit als die zuvor zwölf. Queue, Patrol, Fill/Dig und Host/Client
bleiben Teil der Spielabnahme. Keine vollständige Lagfreiheit behauptet.
Script Extender 1.42.0, Modversion 1.0.0, README und öffentliche QoL-Bridge unverändert.

Buildstatus Gebäudeplatzprüfung: erfolgreich gebaut und installiert am 05.09.2026 um 21:47:41; 0 Fehler, 0 Warnungen. Der erste Build um 21:46:58 zeigte eine ungenutzte Diagnosevariable; nach deren Entfernung und erneuter Prüfung wurde der bereinigte Stand nochmals gebaut. Lokale und installierte DLL SHA-256-identisch: 7BE8F4989DD4D069B4954422B5D9D53C85062B108BD8C62823275AECDD42A29A. Laufzeitabnahme offen.

## Aktuelle Reparatur: gemeinsame Gruppenoptimierung

Ausgangslauf: Befehl 22 um 19:59:19, 680 Units, 1114,968 ms synchroner Befehl,
470,232 ms Qualifikation und 573,294 ms gewichtete Suche. Alle 680 Units haben
später `path-completed-at-target` erreicht. Keine Exceptions oder protokollierten
Eigentümerverletzungen. Native Basis weiterhin
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

### Alle vier Optimierungen

1. Die Qualifikation speichert einen gekapselten Suchdatensatz mit kodiertem Pfad,
   Kantenkostenbeschreibung, Start/Endpunkt, Spieler, Tick/Epoch/Revision und Kostenprofil.
   Cache-Schlüssel sind Werttypen einschließlich Profil und Arbeitsziel. Unit-Pläne
   bleiben individuell. Der Builder prüft Identität, Endpunkte und Revision und kopiert
   den gespeicherten Pfad in den eigenen nativen Puffer; der vollständige Audit bleibt.
2. Bei gültigem Profil sucht die notwendige Qualifikation bereits kostenoptimal. Ein
   bytegleich veröffentlichter, weiterhin passender optimaler Pfad braucht keine zweite
   Optimierung (`qualified-optimal`). Auch gegen einen positiven Vanilla-Pfad wird ein
   passender optimaler Kandidat zuerst an allen Profilschranken geprüft. Ungewichtete
   Erreichbarkeit gilt ausdrücklich nicht als Optimalitätsnachweis. Nicht kodierbare
   Wege behalten die getrennte topologische Prüfung; Fill-Abschluss und Strukturregeln
   bleiben eigenständig. Die optionale 40-Tick-Marge ist unverändert.
3. Pro Suchkern werden höchstens acht deterministisch per LRU verwaltete, nach exakt
   normalisiertem Boden-/Moatkostenverhältnis und Strukturpolitik getrennte Felder
   gehalten. Kostenschranken werden ganzzahlig konservativ umgerechnet. Invalidation
   macht Feldgenerationen ungültig; die bedarfsgerecht belegten Seiten können erneut
   benutzt werden. Spieler und synchroner Suchkontext bleiben im äußeren Vertrag.
   Eine gemessene Präzisierung gegenüber dem Plan: Ein fremdes Formationsziel führt
   nicht zum teuren Ausbau eines entfernten Ankers. Liegt es auf einem bereits
   optimalen Pfad, wird dessen ebenfalls optimaler Präfix direkt verwendet; sonst
   wird dasselbe Feld auf das tatsächliche Ziel zurückgesetzt. Das verwirft keine
   Route und ist kein Zeit-/Suchbudget. Die Variante mit pauschalem Anker-Ausbau
   war im großen Vergleichsmodell langsamer und wurde vor dem Build ersetzt.
4. Die bisherige Cursor-Bodensicht lässt physische Übergänge zugunsten nativer
   Portalverträge aus und ist deshalb kein allgemeiner negativer Bodennachweis.
   `GroundUpper` bildet separat sämtliche GroundOnly-Kanten ab und fasst gewöhnliche
   Regionen konservativ zusammen. Nur ein negatives Ergebnis der vollständigen,
   aktuellen Sicht schließt eine konkrete Bodensuche aus. Positive zusammengefasste
   Verbindungen bleiben unbekannt; ebenso fehlende/unfertige/verschmutzte Topologien.
   Es wird allein dafür keine neue Vollkarte bei einem kleinen oder KI-Befehl gebaut.
   Die normale native positive Vorprüfung bleibt bestehen. Moat-Verbindungen sind
   in der neuen Sicht nicht enthalten. Bestehende Terrainhooks aktualisieren sie mit.

### Tests, Gesamtmessung und Grenzen

169.165 Runtime-Assertions, 11.618 unabhängige Suchprüfungen und 1.469.340 gerichtete
Cursorvergleiche erfolgreich. Neue Tests umfassen 1/120/680 reale simulierte
UnitPre-Modus-Builder-UnitPost-Ketten mit beiden Produktionsbuildern, direkte
Pfadwiederverwendung ohne erneute Suchläufe, veränderte Identität/Start/Tick/Revision,
proportionale Kostenprofile, exakte Kostengrenzen und LRU-Verdrängung. Negative
Regionsantworten werden gegen den tatsächlichen gerichteten GroundOnly-Suchkern geprüft.

Die Leistungsreferenz wird ausschließlich im Testprozess aus dem unveränderten
Git-Blob `5c772900aba0db1a742fe95786f4d468f8068772` geladen. Keine alte Runtime oder
Fallback-Implementierung wird mit dem Mod ausgeliefert. Ein vorheriger Modellvergleich
mit bereits optimiertem Referenzkern war als Vorgängervergleich ungeeignet und wurde ersetzt.

Drei Wiederholungen ohne wechselnde .NET-JIT-Tiers, Median des größeren Suchmodells:

| Units | vorherige Suchfolge/Kern | kombinierte Suche | Suchknoten vorher / neu |
| --- | ---: | ---: | ---: |
| 1 | 6,59 ms | 0,27 ms | 4.607 / 1.101 |
| 120 | 58,65 ms | 48,58 ms | 220.133 / 192.008 |
| 680 | 303,67 ms | 298,79 ms | 1.213.351 / 1.100.866 |

Beim 680er-Modell sanken die Allokationen von 2.387.568 auf 1.653.072 Bytes.
Der Zeitgewinn ist in diesem Modell klein und streut (neu 280,30 bis 301,61 ms).
Der Einzelfall enthält JIT-Anlaufkosten und eignet sich nicht als Beschleunigungsfaktor.
Die separate schmale Produktionsfixture braucht für 680 Units etwa 2,2 bis 2,6 ms,
bildet aber ausdrücklich nicht die Spielkarte ab. Diese Zahlen erlauben keine
Umrechnung der bisherigen 1115 ms Spielzeit. Cursor- und Bodenregressionen bleiben grün;
der neue Graph erhöht die einmalige Topologiearbeit und benötigt zusätzlichen Speicher.

`cachedSearchFields` ergänzt die vorhandene aggregierte Befehlsdiagnose. Die nächste
Spielabnahme muss dieselbe 680er-Situation mehrfach wiederholen, Gesamtzeit und
Zielankünfte vergleichen und anschließend Angriffe, Fill/Dig, Queue/Patrol sowie
Host/Client bestätigen. Script Extender 1.42.0, Version 1.0.0, README und QoL-Bridge
unverändert. Keine neuen nativen Hooks.

Buildstatus Gruppenoptimierung: am 05.09.2026 um 20:44:45 einmal über die erhöhte build.bat /nopause erfolgreich gebaut und installiert; 0 Fehler, 0 Warnungen. Lokale und installierte DLL sind SHA-256-identisch: D463D13A8320F8ED46AB6CEFAD31ACB5559DD0E5CEA81F721227C5A2F83A0EF8. Die Spielabnahme dieser Optimierung steht noch aus.

## Aktuelle Reparatur: Angriffskandidaten und native Vergleichswege

Der Lauf ab 19:21 Uhr verwendet Mod-SHA-256
`6A9F01CF77EA2F8FB68981EBF1890B68B77ED062207A6B7E436C0F423895220D`.
Native DLL und semantische Datensätze stimmen mit
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` überein.
Lokaler Extender: Tag v1.42.0; Prüfung der installierten 1.42.0-API ebenfalls erfolgreich.

### Befund und Änderungen

- Das Log enthält 26 Fill-Veröffentlichungen und 38 `native-edge-invalid`-Ablehnungen.
  Die vorherige Reparatur war nicht nur Logging. Die genaue Zuordnung des gemeldeten
  optionalen Angriffs-Umwegs ist weiterhin offen: gewichtete Zähler wurden erst nach
  erfolgreicher Pfadbeschreibung erhöht. Null Bewertungen beweisen keinen fehlenden Builder.
- `TryDescribeEncodedPath` trennt nun den ausdrücklich angeforderten Vergleichsmodus
  von der standardmäßig strikten Veröffentlichungskontrolle. Kodierung, Start/Endpunkt,
  gültige Felder, bekannte Höhen/Kosten, Besitzer und exakt gebundene Fill-Kontakte bleiben
  Voraussetzungen. Ein fehlendes modseitiges Richtungsmaskenbit des nativen Vergleichswegs
  verhindert nicht mehr die Suche nach einer sicher geprüften Alternative.
  Unbekannte Strukturkosten und ungebundene feindliche Pfadknoten werden nicht geschätzt.
- Neue Pfade und übernommene Fill-Abschlusskanten bleiben strikt geprüft. Der Vergleichsmodus
  ist keine Bewegungsfreigabe. Die 40-Tick-Marge im aktuellen Profil und strikte Verbesserung
  in allen plausiblen Profilen bleiben bestehen. Beide Builder nutzen dieselbe Bewertung.
- `0xDBC60` baut zuerst eine auf Tiefe 10 begrenzte native Queue und extrahiert danach
  50 bis 500 Ergebnisse (`requestedResults * 2`, begrenzt). Bei feindlichem Moat im
  Abstand bis 2 zum exakt gebundenen Unit-Ziel wird in demselben Aufruf der native Pool
  bis 500 Ergebnisse angefordert. Keine erneute Flood-Suche pro Einheit.
  Die Ergebnisdatensätze mit drei Integers werden stabil eigentümersicher kompaktiert;
  native Angriffskennzeichen und dritte Felder bleiben erhalten. Filterausnahmen verändern
  keine halbe Liste. Native Rückgabe, neue Suchgeneration und Zielidentität werden geprüft.
- Gebäude-Konsument `0x123090`: auch teilweise unzulässige Moat-Plätze werden entfernt.
  Gültige native Paare behalten Reihenfolge und Score. Der bestehende qualifizierende
  Fallback ergänzt fehlende Paare aus dem vorliegenden nativen Kandidatenbestand.
  Es gibt keine beliebige räumliche Ersatzsuche oder künstliche Angriffserlaubnis.
- Bestehende Hooks und ABI bleiben unverändert, insbesondere kein zusätzlicher Hook auf
  `0x196280`. Producer/Consumer `0xDBC60 -> 0x11E960` und Ergebnisgrenzen sind zusätzlich
  in der hashgebundenen statischen Prüfung abgesichert. Pseudocode-Nachweise für diese
  Erweiterung ersetzen keinen späteren Spieltest.

### Diagnose und Nachweise

`attack-slot-filter` nennt entfernte Plätze; `route-capture-rejected` erfasst unter anderem
Puffer-, Start- und Profilprobleme. Die historischen `fill-route`-/`fillRoutes`-Marker enthalten
jetzt auch andere Befehle (mit `command`), Builder-Eintritte und getrennte Entscheidungsgründe.
`native-cost-only-traversal-differs` wird vor der Alternativsuche protokolliert. Begrenzte
Kantendetails enthalten Index, native Tile-IDs, Richtung, Flags, Masken, Höhen, Spieler,
Eigentümerbeziehungen und konkrete Traversierungsregel. Detailstrings entstehen erst bei Ausgabe.
Die Zähler sind Ereigniszähler, keine Anzahl verschiedener Units: Suche und Endentscheidung
können je einen Eintrag erzeugen.

Standalone-Regression: 162.673 Runtime-Assertions, 6.792 unabhängige Suchprüfungen und
1.469.340 gerichtete Cursorvergleiche erfolgreich. Neue Fälle prüfen Kostenvergleich trotz
fehlender Bodenmaske, weiterhin strikte Veröffentlichung, fehlerhafte Nibbles/Endpunkte,
feindliche Endpunkte, Strukturkosten sowie stabile Angriffspools von 1/20/120/500 Einträgen,
Sentinel, Puffergrenzen und Ausnahmen ohne Teiländerungen. Bestehende Fill-Kette inklusive
beider Builder, Arbeitsbindung und Präfixoptimierung bleibt grün. Die neuen nativen
Angriffspool-Adapter sind semantisch/API-geprüft; ihr vollständiger nativer Spielablauf und
die teilweise Gebäude-Ergänzung sind noch nicht durch eine native Laufzeitsimulation bewiesen.

Bekannte Testwarnung CS1701 betrifft das Standalone-Referenzmodell. Es wird keine neue
Spielbeschleunigung aus den schwankenden synthetischen Laufzeiten abgeleitet.
Nächste Spielabnahme: langer Bodenweg vs. günstigere Moat-Route beim Unit-Angriff,
Unit-/Gebäudeziel neben feindlichem Moat und mehrere automatische Fill-Zyklen;
danach Queue, Patrol und Host/Client. Version 1.0.0, README und QoL-Bridge unverändert.

Build am 05.09.2026 um 19:54:34 einmal über den erhöhten build.bat /nopause erfolgreich: 0 Warnungen, 0 Fehler.
Lokales Paket und Installation SHA-256 identisch: A5BD484A157485E4985FE4474926EFFE88DA1190DEF0C8557ABB378EA18A4CC6.
Laufzeiterfolg dieser Reparatur noch nicht getestet.

## Aktueller Übergabestand: Fill-Abkürzungen und native Ausweichziele

### Laufzeitbelege und verbleibende Unsicherheit

Untersucht wurde der neueste Spielstart im Append-Log mit Mod-SHA-256
`32F875CF79656F049BC025149871EDCB109656A55F38BFC452059F627B693787`.
Die kanonische native DLL stimmt weiterhin mit CURRENT.json und den verwendeten Datensätzen
überein: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

- Befehl 27 um 18:11:42: 128 Unit-Aufrufe, 103 positive Rückgaben, 25 ohne Builder.
  Die begrenzten Einzelmeldungen zeigen unter anderem Units 13/22/23 mit Zielen
  (391,373)/(392,373)/(392,374), Flags `0x40008000`, Abbruch vor der Regionsprüfung.
  Befehl 28 um 18:12:14 (Mauervergleich des Nutzers): 156/156 positive Aufrufe, keiner ohne Builder.
- Die normale Formation verwendet bei Moat-Freigabe die eigentümerneutrale Suche `0xDAFD0`.
  `0xE1D30` übernimmt deren Kandidaten; die individuelle Modprüfung verhindert erst danach
  das Betreten feindlicher Ziele. Zu diesem Zeitpunkt erfolgt keine allgemeine Neuvergabe mehr.
- Fill ist nicht allgemein von Abkürzungen ausgeschlossen: 89 protokollierte Arbeitsbewegungs-
  Abschlüsse/Unterbrechungen tragen `weighted-path-published`, 27 `native-friendly-moat`.
  Einige Abschlüsse bestätigen tatsächliche Überquerungen, andere wurden durch neue Befehle beendet.
  Die genaue Zuordnung sämtlicher vom Nutzer beobachteter Umwege bleibt offen.
- Bestätigte Code-Lücke: Der bisherige Owner-Audit erlaubt den exakt gebundenen terminalen
  Fill-Kontakt; `TryDescribeEncodedPath` verwarf denselben Pfad wegen `FriendlyOnly`.
  Ohne gültige native Kostenbasis fand anschließend keine Abkürzungssuche statt. Ablehnungen
  außerhalb von Gruppen wurden bisher nur in nicht vorhandene Befehlszähler geschrieben.

### Umsetzung und native Verträge

`FillWeightedRoutes` bindet die Kontaktbeschreibung an aktuelle Unit-ID/Global-ID, Spieler,
Arbeitsdurchlauf, Arbeitsfeld und effektives Builder-Ziel. Der gemeinsame Knotentest erlaubt
genau das Arbeitsfeld als vorletzten Knoten; andere feindliche Knoten und Diagonalecken bleiben
gesperrt. Kosten und Länge beschreiben den gesamten Weg einschließlich beider Abschlusskanten.
Die Suche selbst bleibt freundlich: Sie vergleicht die normale vollständig freundliche Alternative
mit einem verbesserten Präfix vor den unveränderten beiden nativen Abschlusskanten. Deren Kosten
werden vorab aus allen Profilgrenzen abgezogen; für das Präfix bleiben maximal 1.998 Schritte.
Auch ein leerer oder reiner Bodenpräfix ist zulässig. Die bisherigen 40 Ticks im tatsächlichen
Profil und strikt positive Ersparnis unter allen plausiblen Profilen bleiben erforderlich.
Live-Audit und Rollback prüfen weiterhin eigenen Puffer, Start, Ziel und Identität.

`NativeFormationSlots` ergänzt ausschließlich die passende synchrone Bewegungs-Platzvergabe.
Feindliche oder ungültig besessene Moat-Kandidaten werden über den nächsten nativen Listenindex
übersprungen; Abstandsraster und Reihenfolge bleiben nativ. Keine zusätzliche Karten-/Wegsuche,
keine Änderung von Terrain, PCL, Visit-Stamps oder gemeinsamen Pfadfeldern. Ein Indexrücksprung
auf null wird als Erschöpfung behandelt. Negative Erschöpfungsergebnisse sind an Befehl, Tick,
Karte, Suchstempel, Spieler, Abstandsraster und Änderungsrevision gebunden.

Der gemeinsame Index ist zugleich Vanillas Gruppenabbruchzähler: Bei 4.000 bricht der Aufrufer
noch vor der Zielfeldzuweisung ab. Deshalb wird an dieser Grenze kontrolliert auf das weiterhin
zulässige Klickziel zurückgefallen, mit Index null. Strukturziele bleiben der individuellen nativen
Portalprüfung überlassen. Ein ungültiges Klickziel erhält den nativen Fehlerendpunkt (0,0).
Bei einer Ausnahme wird die Ausgangstriple X/Y/Index vor dem unveränderten nativen Selektor
wiederhergestellt. Die späteren Unit-/Puffer-/Owner-Verträge bleiben die Sicherheitsgrenze.

Neuer Entry-Detour `0xE1D30`, Win64 `void(manager, spacing, x, y)` über RCX/EDX/R8D/R9D.
Die ersten 15 Bytes bis exklusiv `0xE1D3F` sichern RBX, RBP und RSI auf den Stack:
`48 89 5C 24 08 48 89 6C 24 18 48 89 74 24 20`.
Das eindeutige 40-Byte-Pattern, die vollständige Funktion bis `ret` bei `0xE1E6E`, der fehlende
Call-/Incoming-Flags-Vertrag und die Reads vor den Writes wurden gegen die installierte DLL geprüft.
Die einzigen globalen Writes sind Y bei `0xE1E46`, Index bei `0xE1E4C` und X bei `0xE1E5F`,
auf TribeManager `0x7CC6720` + `0x10/0x14/0x0C`. Vorher werden Listenindex und X/Y-Quellen
aus `pathManager+0x155F6C` und `+0x28F3EC` gelesen. Das Trampolin erhält Originalprolog,
Stack und nichtflüchtige Register; kein eigener Ersatzblock clobbert Register. Die Prüfung ist
in `Validate-PlacementContracts.py` reproduzierbar. Die Workspace-Suche fand keine zweite
C#-Hookstelle auf diesem RVA; auf `0x196280` wurde kein zusätzlicher Hook installiert.

### Tatsächliche Tests und Abnahme

- 162.225 Runtime-Assertionen, 6.792 unabhängige Suchassertionen und 1.469.340 gerichtete
  Cursorgraph-Vergleiche bestanden; 18 Runtime-Dateien gegen lokale und installierte SE-1.42.0-APIs
  semantisch geprüft. Der Standalone-Roslyn-Prüfer meldet nur die bekannte CS1701-Referenzwarnung.
- Die Tests führen jetzt die Produktionsauswahl, Resolver-Modus 1 und 2, Unit-Pre, Modusprüfung,
  beide Builder und die tatsächliche gewichtete Bewertung zusammen aus. Ein positiver nativer
  Kontaktweg wird von 17 auf 7 Schritte verkürzt; eine günstigere vollständig freundliche Route
  gewinnt mit 9 Schritten und weniger Moatkanten. Leerer Präfix, fehlende Ersparnis, ID-Wechsel,
  falsches Arbeitsziel und Endpunkt sowie zusätzliche feindliche Kanten werden geprüft.
- Die frühere Attrappe lieferte einen Moat-Datensatz aus einem unechten Pointer und ließ den
  Spieler ungebunden. Sie verwendet jetzt echten Fixture-Speicher und den Produktionsleser.
  Terrainänderungen im Test durchlaufen die tatsächliche Suchinvalidierung.
- 1/20/120/128/156 Formationsmitglieder erreichen jeweils ihren individuellen Builder. 25
  feindliche Kandidaten verursachen insgesamt 25 zusätzliche native Selektoraufrufe, keine Wege.
  Für 156 Ziele: 181 simulierte native Aufrufe, rund 0,013 ms; die 40 gemessenen Bytes stammen
  von der Test-Stopwatch. Dies ist keine Messung nativer Ingame-Laufzeiten.
- Erschöpfung, Wiederverwendung, native Indexgrenze, Struktur-Rückfall, verschachtelte Arbeit,
  Besitzerrevision und Ausnahme-Rücksetzung sind geprüft. Bestehende Platzierungs-, Cursor-,
  Fill-, Struktur-, Profilkonflikt- und Fremdpufferregressionen bleiben grün.

Neue Diagnosen: `stage=fill-route` (maximal drei Details pro 60 Simulationsticks), aggregierte
`fillRoutes` sowie `formationRejected`, `formationReplaced`, `formationFallbacks` in den
periodischen Runtime-Markern. Kosten- und Kontextablehnungen werden damit auch ohne Gruppe sichtbar.
Die neue Spielabnahme bleibt offen: mehrere spätere Fill-Zyklen, Moat/Mauer-Vergleich, Queue,
Patrol, Angriffe und Host/Client. Es wird weder vollständige Lagfreiheit noch die Behebung aller
beobachteten Fill-Umwege allein aus den automatisierten Tests abgeleitet.

Buildabschluss: `build.bat /nopause` am 05.09.2026 um 19:05:16 einmal direkt erhöht
ausgeführt; **0 Warnungen, 0 Fehler**, Build und Installation erfolgreich. Lokale und installierte
DLL sind identisch: SHA-256 `6A9F01CF77EA2F8FB68981EBF1890B68B77ED062207A6B7E436C0F423895220D`.
Ziel und installierter Script Extender bleiben `1.42.0`, Modversion `1.0.0`. README,
öffentliche QoL-Bridge und Extender-Fork wurden nicht verändert. CRLF und Diff-Prüfung bestanden.

## Aktueller Übergabestand: besetzte Treppen, gemischte Starts und Platzvergabe

Diese Reparatur baut auf dem Cursor-Build `9AF6787D27D3137FD72DAF50E90A1BFAD59F3FE98CDED2C4FFCE9D6AEABE3FCF`
auf. Die unten folgenden älteren Übergaben bleiben historische Belege. Insbesondere die
bisherige Erklärung des direkten Klickadapters wird durch die folgenden Nachweise ersetzt.
Ziel bleibt Script Extender **1.42.0**, Modversion **1.0.0**; README und öffentliche QoL-Bridge
werden nicht geändert. Die alternative Umsetzung wurde ausdrücklich mit dem Nutzer besprochen.

### Befund des Tests vom 05.09.2026, 16:09 bis 16:12

- Die Cursorbedingung `!occupiedByLivingUnit || hostileUnitTarget || hostileBuildingTarget`
  ließ eine freundliche lebende Einheit auf einer Treppe die zusätzliche Bewegungsprüfung
  verhindern. Begehbare Strukturziele nehmen jetzt auch bei freundlicher Belegung an der
  gerichteten Regionsprüfung teil. Waffen-/Angriffsentscheidungen bleiben nativ.
- `SelectOwnerSafeGroupMoatMode` kehrte zurück, sobald `vanillaFirstMoat != lead` war.
  Damit wurde gerade die Kombination Boden-/Inselführer und späteres Moat-Mitglied nicht
  qualifiziert. Diese Gruppe kann jetzt den notwendigen gemeinsamen Moat-Zweig erreichen;
  bei einem Moat-Führer bleibt der geeignete native gemeinsame Unit-Zielzweig erhalten.
- Die Klickzeilen um 16:11:29.602, 31.546, 33.092, 35.982 und 51.221 bestätigen eine
  freundliche Verbindung zwischen Region 3 und 1. Fehlende `move-command`-Zeilen beweisen
  jedoch NICHT, dass der Auftrag nicht eingereiht wurde: `FlushCommandDiagnostics` konnte
  frühe schnelle Abbrüche ohne als moat-relevant markierten Plan vollständig verwerfen.
  Gruppen mit grabfähigen Einheiten und null Unit-Aufrufen werden nun ebenfalls ausgegeben.
  Die konkrete Abbruchstelle dieser historischen Klicks bleibt ohne damalige Marker offen;
  der nachgebildete native Zweig bestätigt den oben genannten Codefehler.
- Der Befehl `commandSeq=55` um 16:12:08.340 hat 62 Einheiten und ein Moat-Ziel in PCL 0;
  `floodCalls=0` passt zum gemeinsamen Zielzweig. Dessen Code weist jedem Mitglied exakt
  dieselben Koordinaten zu. Ein erfolgreicher Builder bestätigt keine räumliche Verteilung.

### Hashgebundene native Verträge und Eingriffstiefe

Die installierte DLL wurde erneut gegen CURRENT.json und die Datensätze geprüft:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Die reproduzierbare Prüfung liegt unter
`_inspect/MoveMoatRegressionTests/Validate-PlacementContracts.py` (PE/Capstone, nur lesend).

- `0x195E30` ruft `0x23990` mit Chore-Opcode `0x11` VOR dem Start-Moat-Test auf.
  Der Dispatchtabelleneintrag führt nach `0x10AE0`, beim Ausführen weiter nach
  `0x196100 -> 0x11B520`. Die nachfolgende Startprüfung gehört zur unmittelbaren
  Klickrückmeldung, nicht zum Nachweis einer ausgeführten Bewegung. Das vorübergehende
  Löschen des echten Moat-Terrainflags wurde entfernt. Der Rückmeldungsadapter nutzt
  Regionsverbindungen und darf keine verschachtelte Befehls-/Arbeits-/Unit-Prüfung übernehmen.
- `0x11B520` verlangt für die normale Formation eine positive Ziel-PCL. Der alternative
  Zweig `0x118E00` schreibt je Unit `manager + id*0x490 + 0x934/0x936` und ruft bei
  `0x118FB9` die echte Unit-Bewegung `0x196280` auf. Diese Felder entsprechen bei
  `GameUnit`-Basisoffset `0x65C` genau `r_AttackMoveToTargetTileX/Y` (`0x2D8/0x2DA`).
  Die verwalteten Offsets werden beim Start zusätzlich geprüft.
- Die native spätere Entzerrung `0x181890` prüft die Belegung des aktuellen Feldes und
  ruft auf Boden `0xF03C0` bei `0x18195E`, auf Strukturen separat `0xF0710` auf.
  Bei einem gefundenen Feld folgt `0x196280` bei `0x18198E`. `0xF03C0` verlangt unter
  anderem eine nichtnull Boden-PCL, die native Belegungs-/Sperrsicht `0x51D75F0` und ein
  freies Unit-Feld. Sein verwendeter Ausgabevertrag ist X/Y/Tile bei `pathManager+0x44/0x48/0x4C`.
  Der Aufrufer verwendet außerdem die laufende native Unit-ID `0x9302C4`; diese muss zur
  gebundenen Unit passen. Struktur- und Teleport-Sonderzweige werden nicht ersetzt.

Es kommen drei Funktionsanfang-Detours hinzu, kein Hook auf `0x196280`. Vollständige,
mindestens 14 Bytes überdeckende Instruktionspräfixe sind:

| Einstieg | Ende exklusiv | Präfixinstruktionen |
| --- | --- | --- |
| `0x118E00` | `0x118E0F` | drei `mov [rsp+8/10h/18h], rbx/rbp/rsi` |
| `0x181890` | `0x18189F` | zwei Sicherungen von RBX/RSI, `push rdi`, `sub rsp,30h` |
| `0xF03C0` | `0xF03D1` | Sicherung von EDX, `push rbx/rsi/rdi`, `sub rsp,40h`, `inc [rcx+A0h]` |

Die Prüfung kontrolliert die vollständigen Präfixbytes, Endadressen, eindeutige Patterns,
Funktionsdatensätze und Aufrufketten. MonoMod führt die Originalinstruktionen im Trampolin
aus; kein eigener Ersatzblock verwendet Scratchregister. Win64: RCX/RDX/R8/R9 enthalten
die ersten Argumente, die übrigen Gruppenargumente liegen im Stack; Return ist RAX bzw.
void. Stack/Shadow-Space und nichtflüchtige GPR/XMM-Register werden über den Delegate-ABI
erhalten. Es gibt keinen benötigten eingehenden Flagwert. Die beobachteten Rücksprünge
verwenden eigene Tests/Loads, nicht vom ersetzten Suchaufruf erzeugte Flags. Die gespeicherten
Register werden vor Clobbern im Originalpräfix gelesen. Die vollständigen Call-/Byteangaben
gibt das Prüfscript aus; die kanonische Baseline bleibt unverändert.

### Platzierung, Sucharbeit und Rücksetzung

`MoatPlacementSearch` ergänzt nur fehlende individuelle Ziele im synchron gebundenen
gemeinsamen Gruppenzweig. Native Unit-Reihenfolge und Gruppen-/Patrol-Wegpunkte bleiben
erhalten. Die nächste freie Position wird deterministisch in Breitensuche gesucht;
freundliche Moats UND benachbarter erreichbarer Boden sind erlaubt. Gerichtete Kanten,
Höhen, Tore, fremde Moats, aktuelle Belegung und native Reservierungssicht werden geprüft.
Fehlender freier Platz erzeugt keinen Ersatzauftrag: der ursprüngliche Zielpunkt durchläuft
weiter die individuelle native Bewegung und den bestehenden vollständigen Pfadaudit.

Unterschiedliche Ziele teilen einen fortsetzbaren gerichteten Regionsnachweis: Erreicht die
Unit den Anker und der Anker den Kandidaten, ist die Verbindung bewiesen. Der Anker wird
NICHT als Bewegungswegpunkt eingesetzt. Andere gerichtete Verbindungen bleiben durch eine
eigene Regionsentscheidung möglich. Es werden keine Kosten, Pfadpuffer oder Unit-Pläne
zwischen Einheiten geteilt. Cursor- und Platzierungs-Suchknoten haben getrennte Zähler.

Reservierungen bleiben bei einem Suchdaten-Neuaufbau erhalten. Epoch/Tick, Global-ID,
Spieler und Aufrufkontext verhindern eine Übernahme durch andere Units oder Arbeitszyklen.
Bei Abbruch werden eigene Zielfeldänderungen zurückgesetzt, fremde spätere Schreibzugriffe
bleiben erhalten. Veränderte Pre-Argumente werden bei der ersten nativen Verwendung nochmals
gelesen: nachdem Vanilla sie bereits in Register kopiert hat, dürfen sie nicht nachträglich
auf das Klickziel zurückgeschrieben werden. Die eigenen Zielfelder folgen dann dem tatsächlichen
Argument; die alte Platzreservierung wird freigegeben. Bereits native Pfade behalten ihren
separaten Puffer-, Kanten- und Eigentümeraudit samt Rollback.

Automatische Entzerrung nutzt ausschließlich Vanillas bestehenden Aufruf für eine tatsächlich
überlappende geeignete Unit auf freundlichem Moat. Kein neuer Tick-Scan und keine Arbeit durch
Auswahländerung. Der Freifeldersatz gilt einmal pro gebundenem Aufruf; verschachtelte Aufrufe
übernehmen keinen fremden Suchkontext. Daten werden höchstens innerhalb desselben Ticks und
derselben Karten-/Topologierevision wiederverwendet; Belegung wird immer neu gelesen.

**Alternative bei unzureichendem Spielergebnis:** statt der begrenzten eigenen Platzvergabe
Vanillas Formations- und Freifeldsuche gezielt um Moat-PCL-Ausnahmen erweitern. Dieser Weg
verlangt mehr Eingriffe in gemeinsame native Suchpuffer und Strukturzweige. Er ist ausdrücklich
als nächste Alternative festgehalten, aber nicht als paralleler Runtime-Fallback eingebaut.

### Tatsächliche Prüfungen und noch offene Spielabnahme

- Gesamter Runtime-Code gegen lokalen SE-Tag `v1.42.0` und installierte Release-Assembly
  `1.42.0` semantisch geprüft. Der Roslyn-Prüfer meldet die bereits bekannte CS1701-Zuordnung
  zwischen BepInEx/mscorlib-Versionen, keine Quellfehler; er erzeugt keine Spiel-Modassembly.
- 161.320 Runtime-/Platzierungsassertionen, zusätzlich 6.792 unabhängige Pfadsuchassertionen
  und 1.469.340 gerichtete Cursorgraph-Vergleiche bestanden. Platzierung und Entzerrungsadapter
  werden als tatsächliche Produktionskomponenten mit simulierter nativer Aufrufreihenfolge
  geprüft. 100 kleine gerichtete Karten vergleichen Kandidaten und Ankerbeweise unabhängig.
- Die Produktionssimulation liefert bei 1/5/20/27/29/120 Einheiten entsprechend viele
  unterschiedliche freie Ziele. Im 120er-Korridorfall: 119 Kandidatenknoten, 117 zusätzliche
  Verbindungsknoten, rund 24,6 ms und 406.168 Bytes für den gemessenen Simulationsabschnitt
  einschließlich Unit-Plänen, Pfadveröffentlichungen und Testprüfungen. Das ist keine
  Ingame-Laufzeit; der erste kalte Regionsnachweis verursacht zusätzliche Arbeit.
- Belegte Strukturen, veränderte Argumente, Fehl-/Skip-Rückgaben, fehlendes Post, verschachtelte
  Unit-Aufrufe, Global-ID-Wechsel, laufende Schritte und Reservierungen nach Revision geprüft.
  Bestehende Fremdpuffer-, Eigentümer-, Fill-, Struktur- und Cursorregressionen bestehen weiter.
- Cursor bleibt ohne Wegsuche. Die vollständige Auswahl-/Cursor-Simulation allokiert für
  100 Abfragen 52.000 Bytes unabhängig von 1/120/1.000 ausgewählten Units; der isolierte
  Regionsadapter bleibt bei wiederholten unveränderten Abfragen ohne neue Allokationen/Knoten.

Neue Runtime-Marker: `placement hooks installed`, `stage=placement`, `placementBatches`,
`placementSlots`, `placementRollbacks`, `unstackCalls` und `unstackMoves`. Eine positive native
Entzerrungsrückgabe ist noch keine beobachtete Ankunft. Im Spiel stehen besetzte Treppen ohne
Bodenalternative, beide gemischten Gruppenführer, Moat-Verteilung einschließlich Platzmangel,
spätere Entzerrung, Queue/Patrol, KI, Dig/Fill, Angriffe und Host/Client noch zur Abnahme aus.
Lagfreiheit und tatsächliche Bewegung dieser neuen Version sind daher noch nicht bestätigt.

Abschluss dieser Reparatur: Nach der Modellunterbrechung wurde der gespeicherte Stand erneut geprüft
und die vollständige Regression erfolgreich wiederholt. Native Vertragsprüfung und CRLF-Kontrolle
sind bestanden. Am 05.09.2026 um 18:00:24 wurde `build.bat /nopause` einmal direkt erhöht
ausgeführt: **0 Warnungen, 0 Fehler**, Build und Installation erfolgreich. Lokale und installierte
Mod-DLL stimmen per SHA-256 überein: `32F875CF79656F049BC025149871EDCB109656A55F38BFC452059F627B693787`.
Modversion bleibt `1.0.0`; README und Script Extender wurden nicht verändert. Die oben genannte
Spielabnahme bleibt offen; diese Prüfungen ersetzen keinen Ingame-/Host-Client-Nachweis.

## Aktueller Übergabestand: Cursoranbindung korrigiert, 5. September 2026

Dieser Abschnitt ersetzt insbesondere die vorherigen Aussagen zur funktionierenden
Auswahlquelle. Die damalige Testattrappe hatte denselben falschen Namespace wie der Mod;
die Modellmessungen bewiesen deshalb keine Anbindung an die echte Spielauswahl.

### Ursachen und native Belege

Die Logzeilen vom 05.09.2026 um 15:27:28.076 und 15:27:33.077 melden `selected=0`, obwohl
Einheiten ausgewählt waren. Gesucht wurde `CrusaderDE.EngineInterface`; die Spielklasse
heißt **EngineInterface im globalen Namespace**. In der installierten Assembly-CSharp liegt
`selectedChimps` als **private static int[]** vor. Der SE-1.42.0-Wrapper liest daraus die
Game-IDs bei `i * 2`. Sein `using CrusaderDE` benennt nicht den Namespace dieser Klasse.
Der funktionierende Gebäudeadapter erhielt seine Unit-ID direkt.

Die installierte verwaltete Spielassembly hat SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`, die native DLL weiterhin
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Beide stimmen mit der
zugehörigen Baseline überein. Im Standard-Unit-Angriffszweig von `0x8C5F0` prüft Vanilla die
PCLs von Angreifer und tatsächlichem Unit-Ziel. Bei Bedarf folgen `0x196870` und `0xE2CA0`
mit physischem Ziel-Tile und Start-Tile (`useCache=1`). Eine eigene Acht-Nachbar-Auswahl
gehört dort nicht zum Cursorvertrag. Die Waffen-/Befehlsentscheidung bleibt in der nativen
Fallunterscheidung. Gebäude `0xB70C0` und der spezielle `0xB72C0`-Zweig bleiben getrennt.

Zweiter bestätigter Fehler: Die direkte Cursorprüfung sperrte mit `0x10000100` pauschal
Strukturziele vor der gerichteten Erreichbarkeitsprüfung. Außerdem verwarf die Knotenbildung
Strukturendpunkte ohne ausgehende Maske, obwohl eine gültige eingehende Kante sie erreichen kann.

Die fünf Gruppenbefehle im letzten Lauf erreichten alle Builder: 1/1, 19/19, 1/1, 4/4 und
20/20 Unit-Aufrufe/Builder, jeweils ohne Vertragsablehnung. 53 von 54 Bewegungsabschlüssen
melden `path-completed-at-target`; keine Zeile meldet `ownerSafetyViolation=True`.
Der Vor-Builder-Recovery-Zähler blieb null, diese besondere Recovery wurde damit nicht
im Spiel nachgewiesen. Getesteter vorheriger Mod-Build:
`5D60272E0B2F108E28CF13F0041026B272950790A64649EDE660B38A4BCDA465`.

### Implementierte Reparatur

- Auswahlfeld einmalig in Assembly **Assembly-CSharp**, globalem Typ **EngineInterface**
  auflösen; statischen `int[]`-Vertrag und private/publicized Sicht berücksichtigen.
  Fehlende Quelle und leere Auswahl sind getrennt. Ein Bindungsfehler erlaubt keine
  Mod-Freigabe. Wiederholte Abfragen behalten ihre Arrays und Identitätstoken.
- Pauschalen Cursor-Strukturblock entfernen; niedrige Sperrflags und Zielverfügbarkeit
  bleiben. Gerichtete Strukturendpunkte dürfen ausschließlich eingehende Kanten besitzen;
  das erzeugt keine ausgehenden Wege. Höhen, Portalzustände und Besitzer bleiben geprüft.
- Unit-Angriffscursor an das tatsächliche Unit-Ziel binden. Dessen Belegung ist kein
  Bewegungsziel-Verbot. ID, Global-ID, Standort, Leben und Feindbeziehung bleiben geprüft;
  Sprite-Tile und physischer Tile bleiben getrennt. Tatsächliche Angriffs-/Arbeitsannäherungen
  behalten ihre bisherigen Suchen.
- E9D90/E9FF0, Gebäudevorschau, Sondertypen und Befehlsausführung behalten ihre Verträge.
  Keine neuen Hooks, keine Änderung am Extender oder an der öffentlichen QoL-Bridge.
- `cursor-decision` unterscheidet fehlende Auswahlquelle, ungültiges Bewegungs-/Angriffsziel,
  fehlende Verbindung und native/ungebundene Konsumenten. Details erscheinen einmal je
  Kategorie und Karte; `cursor-performance` enthält zusätzlich aggregierte Kategorien.

### Tatsächliche Tests und Abschluss

Die Standalone-Prüfung liest direkt die Metadaten der installierten Spielassembly:
Assemblyname, globaler Typ, Feldname, Attribute und Signatur. Die simulierte Assembly hat
denselben Namen und ein globales EngineInterface mit privatem Auswahlfeld. Der neue Test
beginnt in der tatsächlichen Produktionsmethode `ObserveCursorTilePairFallbackSelection`
und durchläuft Gruppenaufnahme und Scope-Erstellung bis zur E2CA0-Antwort und Auswahl der
nativen positiven/negativen Cursorfortsetzung. Der Scope wird nicht manuell vorgegeben.

Geprüft: Boden ohne Umweg, zwei freundliche Moats mit konsistent getrennten nativen PCLs,
feindliche Sperren, gerichtete Strukturendpunkte, unzulässige Höhe, blockierte/fremde und
wieder erlaubte Portale, belegtes Unit-Angriffsziel mit Sprite-Versatz, bewegte/tote/freundliche
Ziele, ID-Wiederverwendung und gemischte/nativ spezielle Auswahlen. Die vollständigen bisherigen
Bewegungs-/Fill-/Pufferregressionen bleiben Teil des Laufs.

Vollständiger Cursorablauf mit 1/120/1.000 Einheiten: jeweils 100 Abfragen ohne Pfadsuche und
mit konstant 52.000 temporären Bytes (520 je Aufruf, unabhängig von der Gruppengröße).
Die isolierte wiederholte Auswahl-/Regionsabfrage bleibt bei 0 temporären Bytes. Das sind
Desktop-Modellwerte, keine gemessenen Unity-/Mono-Framezeiten. Geprüft werden 14 Runtime-Dateien
und 113 tatsächliche Member: 3.364 Runtime-Assertions, 6.792 unabhängige Suchprüfungen und
1.469.340 unabhängige Graphvergleiche. Genaue Ergebnisse:
`_inspect/MoveMoatRegressionTests/latest-regression-results.txt` und `latest-native-contract.txt`.

Version **1.0.0**, Ziel/Referenzen **Script Extender 1.42.0**, README unverändert.
Buildabschluss dieser Cursorreparatur: `build.bat /nopause` am 05.09.2026 um 15:53:22
einmal direkt erhöht ausgeführt; Build und Installation erfolgreich, **0 Warnungen/0 Fehler**.
DLL, PDB und info.json sind lokal/installiert bytegleich. Neue Mod-DLL SHA-256:
`9AF6787D27D3137FD72DAF50E90A1BFAD59F3FE98CDED2C4FFCE9D6AEABE3FCF`. API-/Metadaten-/Native- und CRLF-Prüfungen bestanden.
Die erneute Spielabnahme muss Bewegung, Unit-Angriff und Treppe/Torhaus jeweils über eigenen
Moat ohne Bodenalternative bestätigen: passender Cursor, ausführbarer Befehl und spätere
Bewegung beziehungsweise Angriff. Die bereits beobachteten Bewegungen stammen vom vorherigen Build.

## Historischer Übergabestand: Cursorregionen, Vor-Builder-Abbruch und Fill, 5. September 2026

Dieser Abschnitt ersetzt die entsprechenden Annahmen des folgenden historischen Stands.
Insbesondere ist eine positive native Regionsantwort allein **kein** Beweis, dass Vanilla
anschließend einen Builder erreicht. Die Reparatur ist im Code umgesetzt; ein neuer Spieltest
mit diesem Build steht noch aus.

### Ausgangslauf und zusätzliche native Befunde

Der untersuchte letzte BepInEx-Abschnitt enthält unter anderem Gruppen mit 18 Buildern für
20 Unit-Aufrufe und 74 Buildern für 80 Aufrufe. Erfolgreiche veröffentlichte Pfade erklären
nicht die fehlenden Aufrufe. Der bisherige Zähler `modeCalls` zählt Modusfreigaben und durfte
nicht als Anzahl aller nativen Modusaufrufe gelesen werden. Die neuen Zähler trennen dies.
Im Fill-Abschnitt bis `2026-09-05 13:00:48.131` wurden 291 Fallback-Auswahlen ohne einen einzigen
geprüften Annäherungskandidaten beobachtet (`checkedApproachTiles=0`, `searchBuilds=0`), bei
Moat-Datensätzen im Bereich 950 und höher. Die Grenze 799 war fälschlich auf Datensatz-IDs
angewendet worden; sie beschreibt ausschließlich Kartenkoordinaten.

Alle folgenden Adressen beziehen sich auf die kanonische installierte `CrusaderDE.dll`,
SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
`CURRENT.json`, das Datenbankmanifest, die semantische Baseline und die installierte Datei
wurden abgeglichen. Rohbaseline und Script Extender wurden nicht verändert.

- Cursor `0x8C5F0` verwendet `0x196870` und danach `0xE2CA0`. Letztere Funktion kann selbst
  `0xD9C40` mit 400.000 Suchknoten anstoßen. Erst nach ihrem Originalaufruf schnell zu prüfen,
  verhindert diesen Aufwand nicht. Die gebundene Cursorantwort muss vorher erfolgen.
- `0x196870` prüft die Auswahltypen; seine native positive Spezialauswahl bleibt erhalten.
  Insbesondere erhalten Assassinen durch den Mod keine Moat-Fähigkeit. Gemischte Gruppen
  teilen Entscheidungen nach Bodenregion beziehungsweise Spezialknoten und Grabfähigkeit.
- Gebäudevorschau `0xB70C0` hat den Cursor als belegten Caller. Ihre native Kandidaten- und
  Höhenprüfung bleibt erhalten. Ihre `0xE2CA0`-Abfrage verwendet `(Start, Kandidat, useCache=0)`,
  die direkte Cursorvorschau `(Ziel, Start, useCache=1)`. Beide Kontexte sind ausdrücklich getrennt.
- `0xE9D90` ist eine Struktur-Austrittsprobe; `0xE9FF0` schreibt zusätzlich unter anderem
  Austrittsdaten bei Pfadkontext `+0x34/+0x4C`. Beide liefern wieder unveränderte native
  Rückgaben und Ausgaben. Die bisherigen pauschalen Bool-Freigaben sind entfernt.
- `0xE2610` kann in seiner zweiten Phase einen blockierten Portalweg positiv melden.
  Der Vorprüfadapter sichert/restauriert deshalb `+0xC0`, `+0xC4` und `+0x98`; nur positiv
  **ohne** Blockierungskennzeichen gilt als normale Bodenfreigabe.
- Die native Fill-Auswahl `0x69D60` ruft `0x6C490` vor Auswahl und Reservierung auf.
  Dessen Höhenprüfung ist an die Höhe des Moat-Kontaktfelds gebunden, nicht an die Höhe
  der entfernt stehenden Einheit. Reservierungsschritt bleibt nativ einmalig 20.

### Umsetzung und Kostenverhalten

`CursorRegionGraph.cs` speichert gerichtete Verbindungen mit Referenzzählern und eine
wiederverwendete rückwärtige Erreichbarkeitsmenge je Ziel. Er enthält keine Pfadkosten,
Vorgänger, Pfadkodierung oder Unit-Puffer. `CursorConnectivity.cs` bindet native Boden-PCLs,
freundliche Moat-Felder und gerichtete Strukturübergänge an diesen Graphen. Die Moat- und
Strukturkanten benutzen die bestehenden geprüften Traversierungsregeln einschließlich Höhe
und Diagonalecken; fremder Moat verbindet keine Regionen. Normale unterschiedliche PCLs
werden nicht allein wegen benachbarter Felder verbunden: Dafür gelten die nativen Portale.

Ein Ziel in derselben normalen Bodenregion wird ohne Aufbau der zusätzlichen Moat-Topologie
beantwortet. Sonst erfolgt ein vollständiger Aufbau bei Bedarf pro Spieler/Karten- bzw.
Regionsgeneration. Lokale Änderungen aktualisieren die betroffenen Randkanten. Beobachtet
werden die nativen Maskenänderungen, Moat-Schreiben/Löschen und vollständigen Maskenneubauten;
Portalzustände und die Bündnismatrix werden aktuell gelesen. Grundlegende PCL- oder
Diplomatieänderungen verwerfen den betroffenen Gesamtstand. Native Kartenlayer bleiben unverändert.

Die Auswahl wird ohne `GetSelectedChimps()`-Kopie und ohne Sortierung aus derselben
verwalteten Auswahlquelle gelesen, die der geprüfte 1.42.0-Wrapper benutzt. Wiederverwendete
Arrays vergleichen Game-ID, Global-ID, Position, Spieler, Typ und Lebenszustand. Ein anderer
Tribe allein löst weder einen Kartenaufbau noch eine Wegsuche aus. Bei unveränderter Auswahl
bleiben Puffer und kompakter Revisionstoken bestehen. Gruppenquellen werden nach Region und
Fähigkeit zusammengefasst; ein wiederverwendeter Prüfscope verhindert eine neue Scope-Allokation
pro Quelle. Gebäudediagnosen verwenden begrenzte numerische Schlüssel, keine Vollsignaturen
oder Detailstrings bei jeder Vorschau. Ziele und Angriffsidentitäten werden weiterhin aktuell
geprüft; die native Cursorlogik entscheidet nach der Erreichbarkeitsantwort über die Befehlsart.

Gebundene direkte und Gebäude-Cursorabfragen beantworten jetzt positive **und negative**
Erreichbarkeit vor dem nativen Flächensuchaufruf. Tatsächliche Arbeits-/Angriffskonsumenten
behalten ihren nativen Vertrag. Rein diagnostische Angriffsausgaben lösen keine zusätzlichen
Wegsuchen aus; eine umgedrehte Suche gilt nicht mehr als Beleg der Vorwärtsrichtung.
Die 2.000-Schritte-Grenze betrifft nur später veröffentlichte Wege, nicht die Cursorregionen.

`NativeMovementRecovery.cs` erfasst den gemeinsamen Portalfehler vor dem Builder. Es führt
höchstens einen Übergang je gebundenem Unit-Aufruf aus, nur mit aktuellem Start/Ziel,
verfügbarer Zielfläche und vollständig auditiertem, kodierbarem freundlichen Moat-Pfad.
Der Pfad wird für den Builder behalten. Es entstehen keine Ersatzbefehle oder Retry-Schleifen.
Vanilla initialisiert selbst den individuellen Puffer und setzt den ursprünglichen Auftrag fort.
Beide Builder bleiben an Unit/Global-ID, Besitzer, Start, Endpunkt und eigenen Puffer gebunden;
bei gescheiterter Veröffentlichung greifen Kantenprüfung und Rollback. Die beiden vor dem
Übergang veränderten Unit-Regionsfelder werden bei negativer nativer Rückgabe zurückgesetzt.
Spezielle Arbeits-, Angriffs- und Probe-Kontexte werden nicht vom allgemeinen Regionshandler
übernommen. Alte Builderkoordinaten sind vor dem Builder kein Zielkontext.

Fill benutzt die zentrale Datensatzgrenze `1..63999`, zusätzlich `id < aktueller Bestand`,
Kapazität höchstens 64000 und passende Datensatzkoordinaten. Die verbesserte Auswahl läuft
in **einem** nativen Durchlauf: ungeeignete Annäherungen werden schon in `0x6C490` ausgeschlossen.
Das wiederholte Ausschließen über vorübergehend veränderte Reservierungen ist entfernt.
Auswahl und Resolver teilen ihre fortsetzbare Suche, spätere Arbeitszyklen erhalten frische
Daten. Belegung, Besitzer und Arbeitsziel werden erneut gelesen. Die exakt gebundene terminale
Fill-Kontaktregel bleibt die einzige feindliche Arbeitsausnahme. Ohne QoL-Provider ist die
verbesserte Fill-Auswahl aktiv; der vorhandene Providervertrag bleibt kompatibel.

### Neuer Maschinenvertrag

Die Beobachter verwenden reguläre Win64-Funktionsdetours mit exakten eindeutigen 40-Byte-
Eingangssignaturen und geprüften Unwind-Funktionsgrenzen:

| RVA | Vertrag | Kopierter Mindestbereich | Funktionsende |
| --- | --- | --- | --- |
| `D90D0` | `void(manager, int y, int tile)` | 15 Bytes | `D9185` |
| `59210` | `ulong(manager, byte owner, uint x, uint y, int mode, byte replace)` | 15 Bytes | `59371` |
| `61E70` | `void(manager, uint moatId)` | 16 Bytes | `61ECC` |
| `DAA50` | `void(manager)` | 15 Bytes | `DAAB8` |

Der Inline-Übergang überschreibt exakt `0x19664B..0x196659`, 14 Bytes:
`33 C0 8B D6 48 89 05 8E 70 F1 05 49 8B CF`.
Original: `xor eax,eax; mov edx,esi; mov [rip+0x5F1708E],rax; mov rcx,r15`.
Kein Sprung landet im Blockinneren. Ungültige Ziele verlassen den nativen Aufruf anders
(`0x19676C`); ein bereits fehlgeschlagener Builder ebenfalls (`0x196734`).

Am Hook ist RSP 16-Byte-ausgerichtet. RSI, RDI, RBP und R12-R15 sind lebender nichtflüchtiger
Zustand; der verwaltete Win64-Aufruf erhält ihn. Flüchtige GPRs und Flags werden auf beiden
Fortsetzungen nicht benötigt; die vollständige Funktion verwendet keine XMM/YMM-Werte.
Der Adapter reserviert `0x30` Bytes einschließlich Shadow-Space. Er liest den nativen Start
bei neuem `RSP+0xA0/+0xA8`, bevor er Zielargumente bei `+0x20/+0x28` schreibt. RCX=R15,
EDX=ESI, R8D/R9D=Start, Argument 5/6=R14D/EBP. Nach Rückkehr wird RSP restauriert.
Bei Ablehnung werden alle vier Originalinstruktionen einschließlich RIP-relativer Ausgabe
verlegt ausgeführt; bei Freigabe geht es zu Vanillas Pufferinitialisierung `0x196585`.
Die vollständigen 73 erzeugten Adapterbytes werden in den Tests mit Iced assembliert und
zusätzlich mit Capstone geprüft. Es wird dabei kein nativer Code ausgeführt.
Unit-Daten beginnen bei Slot `+0x65C`; die Regionsfelder liegen relativ dazu bei
`0x900-0x65C` und `0x8EC-0x65C`. Kein weiterer Entry-Hook bei `0x196280` wird installiert.

### Tatsächlich ausgeführte Prüfungen und verbleibende Abnahme

Die Regression verwendet alle 14 Runtime-Quelldateien für die semantische Prüfung, den
vollständigen Produktions-Suchkern und Cursorgraphen sowie den vollständigen Cursoradapter
gegen simulierte native Daten. Zusätzlich werden 105 tatsächliche Runtime-Member in der
Aufrufsimulation kompiliert. Die wichtigsten Ergebnisse:

- 3.333 Assertions für Unit-Kontexte, beide Pufferzweige, Vor-Builder-Abbruch und Rollback,
  Slotwiederverwendung, veränderte Events, fehlendes Post, Arbeitsauswahl und Cursoradapter.
  Der Gruppenablauf wird auch für 120 verschiedene Unit-IDs simuliert.
- 6.792 unabhängige Suchprüfungen einschließlich gerichteter Kanten, Profilkonflikte,
  Längengrenzen und Terrainänderungen; 1.469.340 unabhängige Graph-Erreichbarkeitsvergleiche
  nach Hinzufügen/Entfernen gerichteter Verbindungen.
- Tatsächlicher E2CA0-Adapter: positive und negative Cursorantwort rufen die native Suche
  nicht auf; ungebundene und echte verschachtelte Angriffskonsumenten behalten sie.
  Die Gebäudeprüfung testet die umgekehrten Argumente und Wiederherstellung des Scopes.
- Produktionsadapter mit 1/120/1.000 ausgewählten Einheiten: jeweils 1.000 unveränderte
  Auswahl-/Cursorabfragen ohne neue Suchknoten, ohne Pfadsuche und mit **0 temporären Bytes**.
  Rund 1/4/23 ms für alle 1.000 Abfragen im .NET-Testprozess, nicht pro Abfrage.
  Die zusätzliche vollständige Gruppenqualifikation benötigt in allen drei Fällen konstant
  20.800 Bytes für 100 Aufrufe (208 Bytes je Aufruf), nicht einen Scope je Einheit.
- Tatsächliche Fill-Datensatzleser mit 799/800/32768/63999, ungültige Grenzen, Kontakt- statt
  Arbeiterhöhe, belegte Annäherungen, genau ein Kandidatenscan/eine Reservierung sowie eine
  erneute Auswahl nach Besitzeränderung. Ein Ziel hinter freundlichem Moat wird ausgewählt.
- Native Hash-, Pattern-, Instruktions-, ABI- und Registerprüfung erfolgreich. Der separate
  Roslyn-Referenztest meldet nur CS1701 zur alten BepInEx/MonoMod-mscorlib-Referenzfamilie;
  keine mod-eigenen Compilerwarnungen oder Fehler.

Die Rohresultate liegen unter `_inspect/MoveMoatRegressionTests/latest-regression-results.txt`
und `latest-native-contract.txt`; `latest-recovery-stub.bin` enthält den geprüften Adapter.
Die Suchleistungsmodelle zeigen weniger Knoten, aber keine garantierte Zeit- oder
Allokationsverbesserung für jeden Lauf. Insbesondere bleibt die exakte Abkürzungssuche bei
schwierigen Aufträgen potenziell teuer. Es wird weder Lagfreiheit noch eine Spielbeschleunigung
allein aus diesen Desktop-Testwerten abgeleitet. Der initiale Topologieaufbau und reale
Portal-/Terrainänderungen müssen ebenfalls im Spiel gemessen werden.

Der lokale Extender-Checkout ist weiterhin sauber `v1.42.0`, Commit
`171d68e155a8f98c5f8c4ee154d9af154c9a2443`, lokale Referenz-SHA-256
`80465F8E3658484CE2E7DEAD5B5C2C1118D4BA154C89CFEC1B6B55B456B221A0`.
Die inzwischen installierte Release-DLL ist ebenfalls **1.42.0**, aber nicht mehr bytegleich:
SHA-256 `27DB3535D9747E3C0532EB5B09A2821AE6C4C29AC30733D01246FFFD2421BEB4`.
Alle Runtime-Quellen wurden gegen beide API-Oberflächen geprüft. Der unveränderte Buildtreiber
bevorzugt die lokale 1.42.0-Referenz. Modversion **1.0.0**, README, Extender und öffentliche
QoL-Bridge bleiben unverändert.

Die Runtime bleibt durch die statische Plugin-Referenz und langlebige Events verwurzelt.
`Application.onBeforeRender` liefert auch bei pausierter Simulation begrenzte aggregierte
Cursormarker. `nativeModeEntries`, `preBuilderFailures`, `preBuilderRecovered`, begrenzte
`unit-no-builder`-Details und nach Ursache aggregierte Ablehnungen ergänzen die vorhandenen
Builder-, Veröffentlichungs- und späteren Bewegungsmarker.

**Spielabnahme offen:** 1/5/20/27/29/120 Einheiten, Starts auf Moat, gemischte Gruppen,
Shift-Queue, Patrol, KI, Angriffe, Dig/Fill-Folgezyklen, Strukturen/Tore, pausierte Vorschau,
Terrain-/Bündniswechsel und Host/Client. Die nativen Zwischenabbrüche und der Inline-Übergang
wurden statisch und simuliert geprüft, noch nicht mit diesem Code im Spiel beobachtet.
Erst tatsächliche spätere Bewegung und neue Arbeitszyklen bestätigen die Reparatur.

Buildabschluss dieser Runde: Am 05.09.2026 um 15:04:38 wurde nach allen Prüfungen einmal
die vorhandene `build.bat /nopause` direkt erhöht ausgeführt. Build und Installation
erfolgreich, **0 Warnungen, 0 Fehler**. DLL, PDB und info.json sind lokal/installiert jeweils
bytegleich. SHA-256 der Mod-DLL: `5D60272E0B2F108E28CF13F0041026B272950790A64649EDE660B38A4BCDA465`.
Die installierte Extender-DLL blieb unverändert. Alle geprüften Textdateien verwenden CRLF;
keine nackten LF oder versehentlichen wörtlichen Zeilenumbruchsequenzen. README unverändert.
Es wurde noch kein neuer Spielprozess und damit kein Laufzeiterfolg dieses Builds bestätigt.

## Historischer Übergabestand: gemeinsamer Suchkern, 5. September 2026


Der lokale Script Extender wurde erneut geprüft: sauberer Checkout `v1.42.0`, Commit
`171d68e155a8f98c5f8c4ee154d9af154c9a2443`. Die lokale Referenz-DLL und die installierte
`000shcdese/SHCDESE.dll` sind bytegleich, SHA-256
`80465F8E3658484CE2E7DEAD5B5C2C1118D4BA154C89CFEC1B6B55B456B221A0`.
Die Informationsversion enthält genau diesen Commit; die Assembly-/Bootstrap-Version `1.0.0`
ist kein Nachweis einer abweichenden Extender-Releaseversion. Die vollständige semantische
Quellprüfung verwendet diese lokalen 1.42.0-Referenzen, ohne eine Mod-DLL zu erzeugen.
Der Nutzer hat den früheren Buildstopp aufgehoben und nach Abschluss der Prüfungen den
vorhandenen Buildtreiber einschließlich Installation autorisiert. Das konkrete Buildresultat
wird unten im Abschnitt „Abschluss dieser Umsetzung“ festgehalten.

### Ursachen und endgültige Architektur dieser Reparaturrunde

Die unten belegten 29 Modusfreigaben bei nur einem zugeordneten Builder waren kein Problem
fehlender Gruppenbefehle: Das gemeinsame Klickziel wurde mit den individuellen Formationszielen
verwechselt. Zusätzlich war der Rekonstruktionszweig `0xE32B0` nicht vollständig erfasst.
Vanilla behält deshalb Gruppenbildung, Formationen, Queue, Patrol und Arbeitsfortsetzung;
der Mod erzeugt weiterhin keine Ersatzbefehle.

- `UnitMovementContext.cs` bindet die veränderbaren `OnUnitMoveHere`-Pre-Argumente erst bei
  Verwendung. Pro Aufruf bleiben Game-ID, Global-ID, Spieler, nativer aktueller/nächster Start
  und Auftragsziel individuell. Ein fremder Arbeits-Handoff wird weder geerbt noch verbraucht.
  Zwischenziele erhalten einen lokal qualifizierten Builderplan. Verschachtelte Aufrufe,
  SkipOriginal ohne Post, Befehlsabschluss, Karten- und Tickwechsel bereinigen die Kontexte.
- `MovementSearchContext.cs` verbindet diese Identität mit der Regionsprüfung und dem
  zweiten Builder. Die zusätzliche native PCL-Vorprüfung benutzt das bestehende originale
  Trampolin, schützt vor Rekursion und setzt alle belegten Seiteneffekte zurück. Eine positive
  Regionsantwort verschiebt eine nötige exakte Suche bis zum tatsächlichen Builderfehlschlag;
  ein negatives Ergebnis verbietet niemals eine freundliche Moat-Verbindung.
- `MovementPathPublication.cs` führt den nativen Builder einmal aus. Ein gültiger Vanilla-Pfad
  bleibt erhalten; notwendige Erweiterungen verwenden anschließend den vorhandenen Suchkern
  direkt. Der zusätzliche native Moat-Retry wurde entfernt. Beide Ausgaben `0xF4930` und
  `0xE32B0` verwenden denselben Vertrag für Start, Ziel, Unit-Puffer und Veröffentlichung.
  Mod-beeinflusste native Pfade sowie alle E32B0-Ausgaben gebundener Digger werden auditiert.
  Ablehnungen setzen Puffer, Länge und veränderte Steuerwerte zurück; auch ein nicht unterstützter
  Rekonstruktionsmodus darf keinen positiven Rückgabewert für einen verworfenen Pfad behalten.
- Der frische Pfadaudit prüft Global-ID, Spieler, exakten nativen Start, eigenen Puffer,
  Endpunkt, Höhen, sämtliche Kanten und feindliche Diagonalecken. Bei geänderter Kante oder
  Eigentümerbeziehung werden auch die Suchfelder vor einem Ersatz verworfen.
  Die einzige feindliche Arbeitsausnahme bleibt exakt ein gebundenes Fill-Arbeitsfeld als
  vorletzter Knoten. Der verwaltete Ersatz kann einen freundlichen Präfix mit genau diesen
  beiden letzten Kontaktkanten bilden; der vollständige Pfad wird anschließend erneut auditiert.
- `WeightedMoatPublication.cs` dekodiert den nativen Weg einmal. Boden-/Moatkanten liefern
  anschließend alle Profilkosten. Eine Suche mit allen Kostenbedingungen ersetzt die früheren
  wiederholten Profil-Suchen und -Audits. Veröffentlichung verlangt weiterhin mindestens
  40 Ticks Gewinn im tatsächlichen Profil und strikt positive Ersparnis in allen belegten
  plausiblen Profilen. Nicht kalibrierte Strukturwege werden nicht gewichtet ersetzt.

### Suchverfahren und gemeinsame Arbeit

`MoatSearchKernel.cs` enthält keine Unit-IDs, nativen Puffer oder Spiel-APIs. Der tatsächliche
Produktionskern wird auch in den Tests kompiliert. Skalare A* verwendet deterministische
Gleichstände mit größerem bereits zurückgelegtem Preis. Nur rechnerisch unmögliche
Verbesserungen werden ausgeschlossen; es gibt kein Zeit- oder Suchknotenbudget.

Verfehlt die skalare beste Route die Profil-, Moat- oder Längenbedingungen, erhält die
Verfeinerung alle nicht dominierten Boden-/Moatkanten-Kombinationen pro Feld und Moat-Zustand.
Eine fortsetzbare Rückwärtssuche testet bei gerichteten Kanten immer die ursprüngliche
Vorwärtskante. Gleiches Ziel nutzt den Baum direkt; unterschiedliche Formationsziele erhalten
zulässige Landmarkenschranken aus genau berechneten Entfernungen und der offenen Suchfront.
Ein Kostenstopp ist niemals ein Unerreichbarkeitsbeweis.

Terrain und Eigentümerklassifikation werden nur im exakten synchronen Suchkontext geteilt.
Gewichtete und ungewichtete Erreichbarkeit haben getrennte Suchfelder, damit die spätere
Abkürzungssuche die notwendige Qualifikation nicht verdrängt. Kostenäquivalente Anfragen nutzen
bestehende Felder. Heap, Labels und paginierte Distanz-/Vorgängerarrays werden wiederverwendet;
die Seiten werden erst bei Bedarf angelegt. Diese Array-Seiten ersetzen Dictionary-Zugriffe
im heißen Rückwärtssuchpfad. Die Projektkonfiguration optimiert auch den Debug-Build, dessen
Symbole für Diagnose weiterhin erhalten bleiben.

Ein fehlender Cacheeintrag bedeutet „unbekannt“. Exakte positive und negative Endpunktentscheidungen
werden wiederverwendet; pauschale negative PCL-Paar-Schlüssel sind entfernt. Topologische
Erreichbarkeit ist von der nativen Grenze von 2.000 Wegschritten getrennt: Eine positive
Erreichbarkeitsentscheidung bleibt auch dann positiv, wenn die Veröffentlichung später
`no-encodable-route` meldet. Unit-Pläne und kodierte Ausgaben werden nicht gruppenweit geteilt.

Dig-/Fill-Auswahl und Resolvervalidierung teilen die bestehende Vorwärts-Erreichbarkeitskarte
vom exakten Start. Auch Gebäude-/Angriffskandidaten verwenden ihren eigenen synchronen Scope
und setzen dieselbe Suche fort. Ein gefundener Bodenweg beantwortet „Moat notwendig?“ sofort
negativ; andernfalls wird die offene Suche soweit nötig fortgeführt. Negative Arbeitsendpunkte
werden mitgespeichert. Belegung und Arbeitsobjekt bleiben unmittelbare Prüfungen, native
Reihenfolge und Distanzvergleich unverändert. Der nächste Arbeitszyklus erhält frische Daten.
Feindliche Diagnosezustände werden ausschließlich bei ausdrücklicher Cursorunterscheidung
berechnet, nicht bei normalen Arbeitsanfragen.

Der Vergleich mit `ImprovedHunters` (`HunterPclReachability`/`HunterActiveTargetReachability`)
und `BugfixesAndQoL` bestätigt den Nutzen grober nativer PCL-Antworten, liefert aber keine
Aussage über die schnellste zusätzliche Moat-Route. Hunters dokumentiert in
`UpdateToNewDLL.md` die Deaktivierung seiner früheren unbeschränkten verwalteten A*-Suche
wegen möglicher Spielstillstände. Diese Suche wird nicht übernommen. Die nativen Floods
`0xDA590`/`0xDAFD0` verändern gemeinsame Pfaddaten; sie sind keine reine Regionsvorprüfung.
Die vorhandene öffentliche Fill-Bridge zu QoL, README und Modversion `1.0.0` bleiben unverändert.

### Hashgebundener nativer Vertrag des ergänzten Hooks

Die installierte kanonische Spiel-DLL und `CURRENT.json` stimmen mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` überein.
Die semantische Baseline und die direkt disassemblierten installierten Bytes belegen:
`0x11B520 -> 0x196280` mit Formationszielen, alternativ gemeinsame Ziele aus `0x118E00`.
`0x18E1E0` ist eine Pfadprobe. Auf `0x196280` bleibt ausschließlich der bestehende
Script-Extender-Hook; MoveMoatTest subscribiert `OnUnitMoveHere`.

`0x196280` benutzt den aktuellen Start nur bei `PathPlanStateBitFlags == 0 && MovingRelevant == 8`,
sonst den nächsten Schritt. Das Ziel wird vor dem Builder gegebenenfalls nativ ersetzt.
Der eigene Ausgabepuffer liegt bei UnitManager + `0xB4FE78 + unitId * 1000`.
Der alternative Zweig ruft `0xE32B0 -> 0xE1640` direkt auf und umgeht `0xF4930`.

Der neue Funktionsdetour hat ABI `int (IntPtr pathManager)`, Windows x64. Die komplette
56-Byte-Funktion `[0xE32B0,0xE32E8)` wird zur Laufzeit als eindeutiges Pattern und als exakte
Bytefolge validiert. `NativeContracts.py` prüft dieselben Bytes unabhängig an der installierten
DLL sowie Instruktionsgrenzen und den relativen Call nach `0xE1640`.

Register-/Stackvertrag: `push rbx` (2 Byte), `sub rsp,0x30` (4),
`mov r9d,[rcx+0x10]` (4), `xor eax,eax` (2), `mov r8d,[rcx+0x0C]` (4).
RCX bleibt für alle folgenden Adressierungen erhalten; erst danach wird es nach RBX gesichert,
EDX liest `[rcx+8]`. EAX nullt den sechsten Stackparameter und `[rcx+0x155F68]`, danach liest
EAX `[rcx+0x14]` für den fünften Parameter. Der Call bei `0xE32D7` darf flüchtige Register
verändern; die Rückgabe liest deshalb `[rbx+0x155F68]`. Epilog: `add rsp,0x30; pop rbx; ret`.
Es existiert kein eigener Scratchregister-Ersatzblock. MonoMod versetzt den vollständigen
instruktionsweisen Prolog ins Trampolin; ein 5-Byte-Sprung benötigt 6, ein 14-Byte-Sprung
16 Originalbytes. Diese Bereiche enthalten keine RIP-relativen Operanden. RBX, Stackausgleich,
Alignment und EAX-Rückgabe bleiben erhalten; Eingangsflags werden hier nicht benötigt.

Die optionale Regionsvorprüfung `0xE2610` verwendet die durch `0x196280` belegte Reihenfolge
Spieler, Quell-PCL, Ziel-PCL, Modus. Modus ist ein SHORT bei `GameUnit+0x35C`.
Die Veränderungen an Pfadkontext `+0xC0`, `+0xC4` und `+0x98` werden im finally zurückgesetzt.
Die unveränderten Extender-1.42.0-/QoL-Quellen enthalten keinen konkurrierenden E32B0-Hook.

### Reproduzierbare Prüfungen dieser Umsetzung

Aus dem Workspace-Root:

    dotnet run --project _inspect/MoveMoatRegressionTests/MoveMoatRegressionTests.csproj --no-restore -- .
    & 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' _inspect/MoveMoatRegressionTests/NativeContracts.py

Die Suite prüft den gesamten Runtime-Quelltext semantisch gegen die lokalen 1.42.0-Referenzen.
Zusätzlich führt sie 72 tatsächliche Runtime-Member sowie den vollständigen neuen Suchkern in
simuliertem nativen Speicher aus. Die verbleibenden Spiel-APIs sind Fixtures; dies ist kein
Ersatz für einen Spieltest. Ergebnis: **1.554 Assertions** für Unit-Kontexte, beide Builder,
Regionen, Puffer und Arbeitsauswahl; **6.792 Assertions** gegen eine unabhängige Referenzsuche.
11 Runtime-Quelldateien sind syntaktisch und semantisch geprüft.

Abgedeckt sind tatsächliche Pre/Mode/Region/Builder/Post-Ketten, IDs und Formationsoffsets,
native Zwischenziele, gemeinsame Ziele, Änderungen der Pre-Argumente, Verschachtelung,
Skip/fehlendes Post, Rückgaben ohne Builder, Starts auf Moat und nächste Bewegungsschritte,
fremde Puffer/Handoffs, Global-ID-Wiederverwendung, Besitzerwechsel, Rollback, terminaler Fill,
verbündete/feindliche Felder und Diagonalecken. Arbeitsprüfungen wiederholen positive und
negative Endpunkte einschließlich neuer Auswahl nach Terrainänderung. Die unabhängige
Referenz erhält kleine gerichtete Karten, Profilkonflikte und Längenbeschränkungen;
Erreichbarkeit über 2.000 Schritte und die exakte Puffergrenze werden getrennt geprüft.

Modellkarte 220x150, frische Suchinstanzen je Gruppengröße, identische optimale Wegkosten:

| Units | verbesserte Einzelsuche: Knoten | gemeinsames Feld: Knoten | einzeln ms | gemeinsam ms | Allokation einzeln / gemeinsam |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 8.111 | 7.516 | 10,28 | 7,86 | 3.409.336 / 1.218.872 B |
| 5 | 40.242 | 38.374 | 77,83 | 35,75 | 3.412.088 / 4.630.232 B |
| 20 | 164.492 | 143.786 | 102,13 | 58,91 | 3.422.408 / 4.657.048 B |
| 27 | 224.280 | 196.442 | 67,59 | 65,25 | 3.427.224 / 4.661.864 B |
| 29 | 241.778 | 211.241 | 72,42 | 67,67 | 3.428.600 / 4.663.240 B |

Diese Einzelmessung enthält JIT-/Aufwärmeffekte; insbesondere kleine und große Gruppen sind
zeitlich nicht direkt vergleichbar. Die Knotenwerte sind deterministisch. Bei 29 Zielen spart
Sharing gegenüber der bereits verbesserten Einzelsuche 12,6 % Knoten. Gegenüber dem früheren
Modellstand mit 318.665 Knoten ergeben Gleichstandsregel und Sharing zusammen rund 34 %.
Die Array-Seiten senkten die gemeinsame Allokation des 29er-Falls von rund 6,37 auf 4,66 MB.
Das gemeinsame Feld braucht trotzdem mehr Speicher als die Einzelsuche; es wird innerhalb
der Runtime wiederverwendet. Keine dieser Zahlen ist eine gemessene Spielbeschleunigung.

Suchläufe/-knoten und gemeinsame Feldtreffer werden tatsächlich gezählt. Commanddiagnosen
trennen native Unit-Aufrufe, Rückgaben ohne Builder, bereits angekommene Units, Builder,
veröffentlichte Pfade und Ablehnungsgründe. Die zusätzlich als `Total` gekennzeichneten
Such-/Regionszähler sind kumulativ. Pro Gruppenbefehl entstehen höchstens drei ausführliche
gewichtete Veröffentlichungstexte; der Pfadaudit erzeugt keine Knoten-Dictionaries mehr.
Bestehende spätere Bewegungs- und Arbeitsmarker bleiben erhalten.

### Abschluss dieser Umsetzung

Am 5. September 2026 um 12:56:12 Uhr wurde nach den abschließenden Regressionen,
Quellkontrollen, der nativen Vertragsprüfung und CRLF-/Diff-Kontrolle **einmal** die vorhandene
`MoveMoatTest/build.bat /nopause` direkt erhöht ausgeführt. Ergebnis: Exitcode 0,
**0 Fehler, 0 Warnungen**, Build und Installation erfolgreich. Die Compilerzeile bestätigt
lokale Extender-1.42.0-Referenzen und `/optimize+` bei weiterhin vorhandenen Debugsymbolen.

Lokales Paket und installierte Ausgabe wurden anschließend verglichen: DLL, PDB und
`info.json` sind jeweils bytegleich. SHA-256 der neuen `MoveMoatTest.dll`:
`A3E46F3B85130F92DC7EF69DFB918040DBF698B1B578586BCBF9B7C473DB92E6`.
Version bleibt einheitlich `1.0.0`; README, Bridge und Extender-Fork wurden nicht geändert.
Alle betroffenen Textdateien haben CRLF, keine nackten LF oder versehentlich eingefügten
wörtlichen Backslash-r/Backslash-n-Zeichenfolgen; `git diff --check` ist sauber.
Die abschließenden Testausgaben liegen in
`_inspect/MoveMoatRegressionTests/latest-regression-results.txt` und
`_inspect/MoveMoatRegressionTests/latest-native-contract.txt`.

Noch offen ist die Spielabnahme mit 1, 5, 20, 27 und 29 Units, allen Formationstypen,
gemischten Gruppen, Shift-Queue, Patrol, KI, Folgebewegung nach Kämpfen, Sprite-/Gebäudeangriffen,
Treppen/Rampen/Wällen sowie wiederholten Dig-/Fill-Arbeitszyklen. Host und Client müssen mit
identischen Paketen geprüft werden. Erst tatsächliche spätere Bewegungs-/Arbeitsmarker nach
Startup-Cleanup bestätigen den Laufzeiterfolg. Die Runtime bleibt statisch im Plugin verwurzelt;
es wurde kein OnDestroy-Teardown oder MonoBehaviour-Update-Laufzeitpfad eingeführt.

Die folgenden Abschnitte dokumentieren ältere Messungen und Zwischenstände; deren Retry- und
Cachebeschreibungen werden durch die oben beschriebene Umsetzung ersetzt.

## Historischer Zwischenstand: Formationsziele und native Unit-Aufrufe

**Historischer Buildstopp, inzwischen aufgehoben.** Der Nutzer hatte den Build dieser Reparatur
ausdrücklich ausgesetzt, weil er vorübergehend die lokale Script-Extender-Version
ändert. Version bleibt `1.0.0`; README und Script Extender wurden nicht bearbeitet.
Der weiter unten dokumentierte Build um 01:32 Uhr gehört zum vorherigen Quellstand.

### Restfehler im Spieltest des vorherigen Builds

Der Logabschnitt vom 5. September 2026, 01:34:36–01:35:59 Uhr, zeigt weiterhin
abgewiesene Gruppenpfade, jetzt mit korrekten Unit-IDs. Beispiele aus
`BepInEx/LogOutput.log`:

| commandSeq | aktive Units | modeCalls | builderCalls | contractRejections |
| --- | ---: | ---: | ---: | ---: |
| 1 | 20 | 20 | 1 | 19 |
| 17 | 29 | 29 | 1 | 28 |
| 25 | 27 | 27 | 27 | 0 |

`builderCalls` zählt hier zugeordnete Mod-Builder, nicht sämtliche nativen Aufrufe.
Bei Command 1 lautet das Klickziel `(391,357)`, während die gewichtete Diagnose für
Folgeunits beispielsweise `(391,359)`, `(390,359)` und `(389,359)` erfasst.
Die verbliebenen Pending-Pläne benutzten das Klickziel. Der exakte Zielvergleich
lehnte deshalb richtige Unitpuffer mit richtigen Formationszielen ab.
Die unabhängige gewichtete Veröffentlichung half einigen Units trotzdem; das
erklärte die wechselnde Zahl tatsächlich bewegter Gruppenmitglieder.

Die Fill-Bündelung zeigt im selben Lauf eine deutliche Verbesserung: untersuchte
Arbeitsauswahlen verwenden `searchBuilds=1`, mit beispielsweise 3,710–7,839 ms
Suchzeit statt der zuvor beobachteten 285–317 ms. Das ist ein Logbefund dieses
Laufs, keine allgemeine Laufzeitgarantie. Diese Bündelung bleibt erhalten.

### Hashgebundene native Evidenz und Extender-Vertrag

Die installierte `CrusaderDE.dll`, `CURRENT.json` und die verwendete Baseline
stimmten bei der Prüfung mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
überein. Einstieg: `tools/semantic/query.ps1 function <RVA>` unter
`_inspect/CrusaderDE-Native-Baseline`; bei mehreren Ergebnissen ausschließlich
den Datensatz dieses Hashes verwenden. Die Funktionsnamen sind teilweise
automatisch zugeordnet; maßgeblich ist die nachfolgend beschriebene Aufruf-/Datenflussevidenz.

- `0x11B520` verteilt Formationsziele und ruft für einzelne Units direkt `0x196280`
  mit deren Zielpaar auf. Bereits passende native Zustände können einen Aufruf
  überspringen; ausgewählte Unitanzahl ist daher kein Sollwert für Builderaufrufe.
- `0x118E00` verwendet dagegen gemeinsame Zielkoordinaten. Das erklärt den
  erfolgreichen Zweig mit vielen zugeordneten Buildern im selben Spieltest.
- `0x18E1E0` ist eine Probe mit temporärem Pfadpuffer, kein universeller Einstieg
  in die tatsächliche Bewegung. Die bisherige Bezeichnung `CentralMovementPlan`
  und der Marker `plannerCalls` sind historisch; sie erfassen Gruppenaufrufe
  nicht vollständig. Die vorhandene Probe bleibt für ihre bisherigen Aufrufer erhalten.
- `0x196280` erhält fünf Argumente einschließlich Unit-ID, Unit-Ziel und
  Buildervariante. Es kann ein natives Zwischenziel wählen und bindet den
  Unitpuffer, bevor es `0xF4930` beziehungsweise den alternativen Builder aufruft.
- Der Script Extender besitzt bereits den Hook auf `0x196280`. Verwendet wird
  ausschließlich `UnitR3EventHooks.OnUnitMoveHere`, kein überlappender Detour.
  Der Vertrag wurde auch im dekompilierten, damals installierten `SHCDESE.dll`
  geprüft: Pre ist veränderbar; Post enthält die ursprünglichen Eingaben;
  `SkipOriginalFunction` erzeugt kein Post. Lokale Referenz und installierte DLL
  hatten dabei SHA-256 `6D4C919ECF6B4AE0EE329081CBBE66BD074202492D45B5AEE74B713B9AF1C57D`.
  Das ist eine Analyseprovenienz, keine Vorgabe für die vorübergehende Extender-Version.

### Reparatur im Quellcode

- Jeder tatsächliche Unit-Aufruf bekommt einen eigenen Eventkontext. Pre-Argumente
  werden erst bei der Modusprüfung als Unit-Ziel gebunden; Änderungen anderer
  Subscriber werden berücksichtigt. Klickziel, Unit-Ziel und Builder-Endpunkt
  bleiben getrennt. Nicht grabfähige Units benötigen keinen Suchplan.
- Ein nativer Zwischenendpunkt erhält einen lokalen Plan mit exakt geprüftem
  Start und Ziel sowie erneuter Erreichbarkeitsqualifikation. Er überschreibt
  weder den Unit-Auftrag noch den äußeren Arbeits-/Angriffskontext. Ein nicht
  qualifizierter Zwischenendpunkt benutzt beim nativen Builder dessen ursprünglichen
  Modus, auch ohne aktiven Gruppenbefehl.
- Puffereigentümer, tatsächlicher Unitpointer und nativer Startvertrag bleiben
  strikt. Die Ausgabelänge wird vor dem Retry geprüft, nicht vor der nativen
  Initialisierung des Builders. Abgewiesene Retries behalten den vollständigen Rollback.
- Verschachtelte Unit-Aufrufe besitzen getrennte Frames. Übersprungene Frames
  werden vor Verwendung entfernt; Post stellt den Elternframe wieder her.
  Die synchrone LIFO-Reihenfolge dient als Zuordnung, da die unveränderten
  Post-Argumente eines geänderten Kindaufrufs sogar dem Elternziel gleichen können.
  Fehlendes Post wird spätestens bei Befehlsende,
  Befehlswechsel, Tick- oder Kartenwechsel bereinigt. Neue Aufrufe erhalten eigene
  Pläne. Der bestehende Arbeits-Handoff wird nach seinem Builder weiterhin verbraucht.
- Der echte Owner-Audit setzt seine Kanten-/Besitzklassifikation je Pfad zurück.
  Ein neuer Test deckte auf, dass sonst frühere Such- oder Playerklassifikationen
  einen gültigen Weg als `enemy-moat-diagonal-corner` ablehnen konnten.
  Feindliche Durchquerungen bleiben verboten; die enge terminale Fill-Kontaktregel
  bleibt ausdrücklich getestet. Die gemeinsame Arbeits-Erreichbarkeitskarte wird
  durch diesen Klassifikationsreset nicht neu aufgebaut.

Neue aggregierte Move-Felder: `unitMoveCalls`, `unitMoveCompleted`, `unitMovePositive`,
`unitMoveWithoutF4930`, `unitMoveAbandoned`, `builderIntermediateTargets` und
`contractReasons`. Ohne beobachteten `F4930` kann ein nativer Frühabbruch, ein
bereits erreichtes Ziel oder ein anderer Builder vorliegen. Eine positive
Unit-Rückgabe ist noch kein Bewegungsnachweis. Ablehnungsdetails mit Klick-,
Unit- und Builderziel sind auf die ersten zwölf Fälle begrenzt; Zähler laufen weiter.

### Prüfstand und ausstehende Abnahme

Der eigenständige Runner unter `_inspect/MoveMoatRegressionTests` kompiliert die
ausgewählten tatsächlichen Runtime-Methoden gegen simulierte Spiel-APIs, einschließlich
Builder-Wrapper, Retry und echtem Owner-Audit. Er baut oder installiert keine Mod-DLL.
Der alte Test mit gleichen Gruppenzielen und manuell gesetztem `activePlan` hatte
den nativen Formationsablauf nicht abgedeckt; diese Testlücke ist jetzt geschlossen.

Abgedeckt sind gemeinsame und unterschiedliche Ziele für 1, 5, 20, 27 und 29 Units,
echte Pfadbytes, Zwischenziele, ungültige/fremde Puffer und Unitpointer, native
Next-Tile-Starts, Starts auf Moat, gemischte Fähigkeiten, wechselnde Spieler beim
Audit, veränderbare Pre-Argumente, verschachtelte gleiche/verschiedene Units,
Skip/fehlendes Post und Kontextwechsel. Arbeitsübergabe, terminaler Fill-Kontakt,
feindliche/unerreichbare Endpunkte, Terrainänderungen und Rollbacks werden ebenfalls geprüft.

Ergebnis dieser Reparaturrunde: **1.363 Assertions bestanden**, 60 tatsächlich
extrahierte Runtime-Member kompiliert und geprüft; Syntaxprüfung aller sechs
Runtime-Quelldateien erfolgreich. Aufruf:

    dotnet run --project _inspect/MoveMoatRegressionTests/MoveMoatRegressionTests.csproj -- .

Die vier bearbeiteten Textdateien wurden abschließend auf CRLF und verbliebene
nackte LF geprüft; `git diff --check` ist Teil der statischen Abschlusskontrolle.

**Offen:** vollständiger Mod-Build gegen die später festgelegte Extender-Version
und Spieltest dieses neuen Quellstands. Erst danach Gruppenbewegung einschließlich
wiederholter Befehle, Shift-Queue, Patrol sowie Angriffe, Dig, Fill und Strukturwege
im Spiel bestätigen. Spätere Bewegungs-/Tickmarker und weitere Arbeitszyklen müssen
den Laufzeiterfolg belegen; Event-Rückgaben oder bestandene Fixturetests genügen nicht.

## Vorheriger Reparaturstand: Build vom 5. September, 01:32 Uhr

Die letzte Optimierung (`5f4e696b`, Vergleichsbasis `ce67cd30`) verursachte zwei
Regressionen. Der Lauf vom 5. September, 01:07–01:08 Uhr, belegt beide:

- Bei einem Move mit 20 grabfähigen Units wurden 20 Builder aufgerufen, aber nur ein
  Fallback ausgeführt. Nach `mode unit=2` wurde weiter `builder unit=1` protokolliert.
  Ein bereits qualifizierter `pendingPlan` der ersten Unit wurde für weitere Units
  wiederverwendet. Der neue Puffervergleich lehnte deren abweichenden Unitpuffer korrekt ab.
- Beim Fill wurden innerhalb einer Arbeitszielauswahl wiederholt zielgerichtete Suchen
  gestartet. Deren Cache existierte nur im Move-Command, nicht bei automatischen
  Arbeitsfolgezielen. Das Log zeigt ungefähr 480.000 besuchte Knoten und 285–317 ms
  Suchzeit je Unit-Auswahl. Die reine gewichtete Pfadoptimierung war dort deutlich kürzer.

Der reparierte Quellstand enthält folgende Änderungen:

- Mode-Freigaben gehören immer zur konkreten Unit. Ein passender aktiver Plan hat Vorrang;
  andernfalls wird der passende ausstehende Plan verwendet. Ein fremder äußerer Plan darf
  einen passenden Arbeitsplan nicht verdecken. Zentral übergebene Formationstargets bleiben
  erhalten, verschachtelte Planneraufrufe stellen ihren vorherigen Kontext wieder her.
- Der Builder wählt seinen Plan anhand des tatsächlichen Unitpuffers und Zielpaars.
  Vor dem Retry wird zusätzlich der native Startvertrag geprüft: aktuelles Tile bei
  `r_PathPlanStateBitFlags == 0 && r_MovingRelevant == 8`, sonst `r_NextTilePositionX2/Y2`.
  Ein Cachemiss im Modepfad wird berechnet; die reine Diagnose darf weiterhin nur nachsehen.
  Beide Zugriffe verwenden dieselbe Implementierung und denselben Cache-Schlüssel.
- Dig-/Fill-Auswahlen verwenden eine lazily aufgebaute Boden-/Friendly-Erreichbarkeitskarte
  für ihren exakten Spieler und Start. Positive und negative Endpunktentscheidungen werden
  innerhalb der Auswahl geteilt. Die unmittelbar zugehörige Resolver-/Builderübergabe
  erhält denselben Suchkontext. Neue Auswahlen bauen frisch auf, auch bei unverändertem
  Start. Tickwechsel, andere Units/Starts und ersetzte Suchkarten dürfen keine alten
  Endpunktentscheidungen einschleusen. Belegung und Arbeitsobjekt werden live nachgeprüft.
- Eine Suchkarte wird erst nach vollständig erfolgreicher Traversierung als Cache freigegeben.
  Ein abgebrochener Aufbau bleibt ungültig. Feindliche Wege werden nur bei ausdrücklich
  angeforderter konservativer Cursorunterscheidung zusätzlich berechnet.
- Negative oder fehlerhafte Retries stellen die 1000 Pufferbytes, den Ausgabepointer,
  die Länge, Route-Variante und den Moatmodus wieder her. Das gilt auch bei einer
  Audit-Exception. Positive Vanilla-Builderpfade werden weiterhin genau einmal ausgeführt.
  Owner-Audit und eng begrenzter terminaler Fill-Kontakt bleiben erhalten.
- Routine-Mode-/Pipeline-Details werden bei Gruppen- und Arbeitsauswahlen nicht mehr
  vorab formatiert. Aggregierte Such-/Pfadzähler und relevante Ergebnisse bleiben sichtbar.

Neue beziehungsweise ergänzte Logfelder:

- Move: `targetedSearches` zählt Qualifikationen, `targetedSearchPasses` die tatsächlichen
  Boden-/Friendly-Suchdurchläufe; außerdem `contractRejections` und `fallbackRollbacks`.
- Arbeitsauswahl: `searchBuilds`, `endpointQueries`, `endpointCacheHits`, `expanded`,
  `searchMs`, `elapsedMs`. Erwartet wird normalerweise ein Kartenaufbau je Auswahl,
  unabhängig von der Zahl ihrer Kandidaten. Eine verschachtelte fremde Suche kann einen
  erneuten Aufbau notwendig machen.
- Die ersten drei Puffer-/Kontextabweichungen erscheinen zusätzlich als
  `stage=fallback-contract-rejected`; weitere Fälle werden im Command gezählt.

Die erneute Codeprüfung und die automatisierten Regressionstests sind abgeschlossen:
**309 Assertions erfolgreich**, Syntaxprüfung aller sechs Runtime-Quelldateien.
Der eigenständige Runner unter `_inspect/MoveMoatRegressionTests` extrahiert mit Roslyn
48 tatsächliche Runtime-Member und kompiliert sie zusammen mit dem unveränderten
`WeightedMoatRoutePlanner` gegen simulierte native Grids und API-Adapter. Er prüft
27 Gruppenmitglieder, getrennte Puffer, Formationstargets, verschachtelte Arbeitskontexte,
positive/negative Caches, Belegungswechsel, Terrainänderungen, Tickablauf, feindliche Moats,
Start auf Moat, Audit-/Retry-Exceptions und Wiederanlauf nach einem Suchfehler.

Aufruf aus dem Workspace-Root:

    dotnet run --project _inspect/MoveMoatRegressionTests/MoveMoatRegressionTests.csproj -- .

Build und Installation wurden am 5. September 2026 um 01:32 Uhr einmal über
`MoveMoatTest/build.bat /nopause` abgeschlossen: **0 Warnungen, 0 Fehler**.
Die installierten Dateien `MoveMoatTest.dll`, `MoveMoatTest.pdb` und `info.json`
stimmen per SHA-256 mit dem lokalen Buildpaket überein. DLL-Hash dieses Reparaturbuilds:

`7B03FB1789C84BBCC43EDDAB8EB8ACF7ACD194CBA6A63E8280DE92A7F8607122`

Diese Tests führen das Spiel nicht aus und belegen keine Ingame-Latenz oder vollständige
native Hookintegration. Die Gruppen-/Fill-Wiederholung im Spiel und der Multiplayer-Test
stehen weiterhin aus. Die historischen erfolgreichen Spieltests weiter unten sind keine
Abnahme dieses neuen Reparaturstands. Modversion bleibt während der Testphase `1.0.0`.

## Ziel und aktueller Vertrag

`MoveMoatTest` erlaubt ausgewählten Bodeneinheiten, fertige eigene oder verbündete Burggräben
als reguläre Wegkante zu benutzen. Der Mod bleibt Vanilla-first: Positive Vanilla-Ergebnisse
werden übernommen; ein Fallback greift nur, wenn Vanilla an einer Burggraben-Grenze scheitert
oder ein nachweislich schnellerer freundlicher Burggrabenweg veröffentlicht werden kann.

Die Fähigkeit ist absichtlich auf dieselben Unittypen begrenzt, die Vanillas Command 6
(`DigMoatTileId`) pro Unit akzeptiert. Maßgeblich ist der Inline-Switch in `0x11E960`, bestätigt
durch den Auswahlhelper `0x191C00` und dessen Call bei `0x8D3CE`:

- Bogenschütze (`CHIMP_TYPE_ARCHER`)
- Speerträger (`CHIMP_TYPE_SPEARMAN`)
- Pikeniere (`CHIMP_TYPE_PIKEMAN`)
- Streitkolbenkämpfer (`CHIMP_TYPE_MACEMAN`)
- Ingenieure (`CHIMP_TYPE_ENGINEER`)
- arabische Sklaven (`CHIMP_TYPE_ARAB_SLAVE`)
- Eunuchen (`CHIMP_TYPE_BEDOUIN_EUNUCH`)
- Plänkler (`CHIMP_TYPE_BEDOUIN_SKIRMISHER`)
- Sappeure (`CHIMP_TYPE_BEDOUIN_SAPPER`)
- Demolierer (`CHIMP_TYPE_BEDOUIN_DEMOLISHER`)

Assassinen, Armbrustschützen, Schwertkämpfer, Ritter und Belagerungsgeräte erhalten keinen
Moat-Fallback. Das historische Feld `unit+0x170` ist keine bestätigte Capabilityquelle.

Eigene und verbündete fertige Moats sind begehbar. Feindliche fertige Moats dürfen Arbeitsziel
zum Zuschütten sein, aber niemals Traversierungskante. Wasser, massive Gebäude und ungültige
Mauer-/Strukturkanten bleiben Vanilla.

## Native Bindung

Alle RVAs in diesem Dokument gelten ausschließlich für:

`CrusaderDE.dll` SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

Die installierte DLL und `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` müssen vor einer
Wiederverwendung übereinstimmen. Bei einem Update sind Pattern, vollständige Entrybytes,
Callziele, ABI und interne Kontrollflüsse erneut zu prüfen; andernfalls bleibt der Mod
fail-closed.

Wichtige Stellen:

| RVA | bestätigte Bedeutung |
| --- | --- |
| `0x11E960` | Tribe-Command-Dispatcher; Command 6/7 und per-Unit-Grabfilter |
| `0x191C00` | Auswahl enthält mindestens eine grabfähige Unit |
| `0x196840` | Unit steht aktuell auf einem fertigen Moat |
| `0x196870` | Auswahlarten-/Cursor-Gate vor genaueren Tileprüfungen |
| `0xE2CA0` | Tilepaarprüfung; ruft bei Regionsgrenzen den nativen Suchhelper auf |
| `0xE2610` / `0xE7C40` | frühe Regions-/Gruppenprüfungen; keine universellen booleschen Gates |
| `0x11B520` | gemeinsamer MoveHere-Gruppenpfad |
| `0x117BC0` / `0x119F90` | Gruppen-Moatmodus und Gruppeniterator |
| `0x196280` | per-Unit-Pfadanforderung und Bindung des echten Unit-Pfadpuffers |
| `0xF4930` | zentraler finaler Unit-Builder |
| `0xDAFD0` | nativer Moat-/Gruppenbuilder mit achtgerichtetem Tilegraph |
| `0xE1640` / `0xE4E90` | Rekonstruktion und nibble-codierter Pfad, maximal 2000 Schritte |
| `0xDBC60` | Annäherungssuche für `AttackUnit` |
| `0xB70C0` | Cursor-Erreichbarkeit eines Gebäude-Footprints |
| `0xDA020` / `0x123090` | Gebäude-Annäherung und Kandidatenverbrauch |
| `0x69D60` / `0x6AF60` / `0x6C490` | Auswahl und Auflösung von Dig-/Fill-Arbeitszielen |
| `0x1853F0` / `0x1976C0` | Wiederaufnahme gespeicherter Bewegung nach einem Kampf |
| `0x19B260` | wirksame Unitgeschwindigkeit und Moat-Verlangsamungsphase |
| `0x18410C` | Dispatchanker zur dynamischen Ermittlung möglicher `r_SpeedBonus`-Werte |
| `0x107160` | native Sonderstrukturprüfung für Treppen-/Mauer-/Rampenkanten |

`MoveMoatTest` detourt weder `0xD9C40` noch `0xDA590`. Die gemeinsame Installation mit
`BugfixesAndQoL` verwendet dessen Reflection-Bridge für die Moat-Arbeitszielhooks; im bestätigten
Lauf meldete `BugfixesAndQoL 1.0.126` `hookOwner=MoveMoatTest`. Es darf nur einen Owner dieser
Hookgruppe geben.

## Warum mehrere dünne Adapter nötig sind

Vanilla besitzt keinen einzelnen globalen Schalter „diese Unit darf Moats überqueren“. Cursor,
Gruppenregionsprüfung, Entity-Annäherung, Command-Zuweisung, Arbeitszielauswahl und finaler Builder
können jeweils vorher abbrechen. Ein frühes positives Cursorergebnis erzeugt noch keinen Pfad.

Die Lösung besteht deshalb aus einer gemeinsamen Traversierungsregel und schmalen Vanilla-first-
Adaptern an den Stellen, die den zentralen Builder sonst nicht erreichen würden. Entity-, Hover-
und Zielkontext wird nur zur sicheren Bindung verwendet; Vanilla bestimmt weiterhin Zielentity,
Annäherungstiles, Arbeitsreihenfolge und Formation. Es werden keine Commands künstlich aufgeteilt
oder erneut ausgegeben.

## Gemeinsame Traversierungsregel

Die owner-sichere Probe verwendet denselben achtgerichteten Kantenvertrag wie der gewichtete
Planer und ist an `0xDAFD0`/`0xE1640` angeglichen. Sie prüft Richtungs- und Bewegungsmasken, Höhe,
Diagonalbedingungen, StructureGrid und die native Sonderstrukturprüfung. Path-Regionen werden
nur noch für native Rückgabewerte und Diagnose verwendet, nicht als Traversierungs-Whitelist.

Die frühe Befehlsqualifikation flutet nicht mehr für jede Unit die vollständige Karte. Sie sucht
zielgerichtet zuerst ohne Moat und nur nach einem Fehlschlag mit eigenen beziehungsweise
verbündeten Moats. Eine feindliche Route wird ausschließlich dort berechnet, wo ein negativer
Cursorbefund sie wirklich unterscheiden muss. Starts auf einem freundlichen fertigen Moat werden
direkt als moatgebundener Start behandelt.

Innerhalb eines synchronen Gruppenbefehls werden boolesche Entscheidungen für gewöhnliche Ziele
nach Spieler, Startregion und Zielregion geteilt. Region `0`, Moat-, Struktur-, reservierte und
Arbeitsendpunkte bleiben an das exakte Tile gebunden. Formationsoffsets derselben Region lösen
somit keine vollständige Suche je Unit aus. Rekonstruierte Pfade werden nie regionsweise geteilt;
sie bleiben stets an das konkrete Start-/Zielpaar gebunden. Alle Command-Caches enden mit dem
synchronen Command.

## Funktionierende Bereiche

Folgende Fälle wurden in Editor und teilweise in Skirmish praktisch bestätigt:

- wiederholter normaler Move durch einen eigenen fertigen Moat;
- notwendige und optionale Moat-Routen sowie mehrere Bodenregionen;
- direktes Ziel auf einem eigenen Moat und Start auf einem eigenen Moat;
- Shift-Move-Queues mit notwendigen Moat-Wegpunkten an mehreren Queuepositionen;
- Patrol über Moat;
- `AttackUnit` einschließlich wiederholter Befehle und Sprite-Hover;
- `AttackBuilding` hinter eigenem Moat einschließlich vollständigem Gebäudesprite;
- kürzestes gültiges Gebäude-Annäherungsfeld statt eines festen Hovertiles;
- begehbare, reservierte Gebäude-Endpunkte;
- Post-Combat-Fortsetzung eines gespeicherten Moat-Move-Ziels;
- gemischte Gruppen vor, auf und hinter einem Moat;
- Gruppen aus grabfähigen und ungeeigneten Units; nur grabfähige Units erhalten den Fallback;
- direkte sowie automatische Folgeziele beim Ausheben und Zuschütten;
- feindlicher Moat als Fill-Arbeitsobjekt, aber nicht als erlaubte Wegkante;
- Treppen, Rampen und begehbare Wall-Top-Ziele hinter einem eigenen Moat;
- normales Assassinen-Mauerklettern ohne Moat bleibt Vanilla;
- KI-gesteuerte grabfähige Units nutzen ihren eigenen Moat;
- mehrere Befehle und mehrere Units pro Spielstart ohne den früheren Einmal-Effekt.

Die Gebäudeoptimierung reduzierte einen früheren großen KI-Fall von bis zu 16 vollständigen
Reachability-Suchen pro Unit auf ungefähr eine Karte je Unit und Zielregion. Gemessene
Gebäudephasen lagen anschließend ungefähr bei 10 ms (`0xDA020`) und höchstens 2,5 ms
(`0x123090`) statt einer Pause von rund 2,2 Sekunden.

Ein späterer siebenminütiger KI-Lauf zeigte dennoch, dass die allgemeine Qualifikation noch zu
teuer war: 16.521 MoveMoat-Zeilen (rund 8,5 MB), 634 vollständige Kartenaufbauten, bis zu 66.265
geprüfte Strukturkanten je Aufbau und ungefähr 1,3 Sekunden synchrone Modarbeit bei zehn
grabfähigen Units. In den auswertbaren Move-Befehlen summierte sich die Modzeit auf rund 32
Sekunden. Logging verstärkte die Pausen, Hauptursache war aber die Vollkartensuche pro Unit.

Die vorangegangene Optimierung ersetzte diese Vollkartensuchen im normalen
Commandpfad durch die oben beschriebene zielgerichtete, regionsweise geteilte Qualifikation.
`0x196840` liefert nur Vanillas Aussage, ob die konkrete Unit gerade auf einem fertigen Moat
steht, und startet keine eigene Suche mehr. Normale AI-/Bodenbefehle, leere Queue-Snapshots,
unveränderte Flood-Aufrufe und wiederholte Tick-/Stallzustände werden nicht mehr einzeln geloggt.
Ein Performanceeintrag entsteht bei Moat-Eingriff oder einem messbar langsamen Command.
Die dabei eingeführten Gruppen-/Fill-Regressionen und ihre Reparatur stehen im neuen
Abschnitt am Anfang dieses Dokuments.

Der letzte Strukturtest endete für 65 beobachtete Pfade am Ziel; 35 Moat-Eintritte und 35
Moat-Austritte wurden protokolliert. Es gab keine MoveMoat-Exception.

## Gewichtete Wegwahl

Der zentrale Publisher läuft am echten Unit-Pfadpuffer im `0xF4930`-Detour und ist nicht von
Move-, Attack-, Combat- oder Arbeits-Commands abhängig. Er kann daher auch automatische Dig-/Fill-
Folgewege optimieren.

Das Kostenmodell liest die Runtimefelder jeder konkreten Unit; es enthält keine feste
Unit-Geschwindigkeitstabelle. Berücksichtigt werden `r_CurrentSpeed`, `r_CurrentSpeed2`,
`r_SpeedBonus`, zusätzliche Teilschritte/Verzögerung und die Moatphase aus `0x19B260`. Eine
Moatkante verwendet den bestätigten stabilen Delay-Aufschlag `+6`. Strukturpfade werden mangels
kalibrierter Strukturkosten nicht durch den gewichteten Publisher ersetzt.

Ein Kandidat wird nur veröffentlicht, wenn:

- er mindestens eine eigene oder verbündete Moatkante verwendet;
- jede Kante und der nibble-codierte Roundtrip gültig sind;
- er unter jedem aus dem nativen Handler dekodierten plausiblen `SpeedBonus` strikt schneller
  als Vanillas Pfad bleibt;
- er im beim Builder erfassten tatsächlichen Runtimeprofil mindestens 40 Ticks spart.

Die frühere Regel verlangte 40 Ticks Ersparnis unter jedem theoretischen Profil. Der Fill-Test
vom 4. September zeigte deshalb innerhalb derselben Gruppe unterschiedliche Wege: 21
Fill-Auswertungen blieben trotz schnellerem Shadow-Pfad unveröffentlicht; darunter wurden 11
ausdrücklich allein wegen der alten Profilregel zurückgewiesen. Weitere 11 Fill-Pfade wurden
veröffentlicht. Bei den betroffenen Macemen war der tatsächlich erfasste `SpeedBonus` 0. Die
Regel wurde deshalb präzisiert: Alternative Profile
dürfen den Kandidaten weiterhin niemals langsamer machen, die volle Sicherheitsmarge gilt aber
für den konkreten Runtime-Snapshot. Damit soll die beobachtete unnötige lange Wegwahl beseitigt
werden, ohne einen unter irgendeinem bekannten Profil schlechteren Pfad zu veröffentlichen.

## Owner-Sicherheit veröffentlichter Fallbackpfade

Positive Vanilla-Builderpfade bleiben unverändert. Nur ein durch den Mod nach einem echten
Vanilla-Nuller erzeugter nativer Retry wird anschließend am tatsächlichen nibble-codierten
Unit-Pfad vollständig auditiert. Eigene und verbündete Moat-Tiles sind zulässig; ein fremder
Moat, eine fremde reine Diagonalecke oder ein ungültiger Owner macht den Retry unsicher.

Beim Zuschütten darf ausschließlich das exakt gebundene feindliche Arbeits-Moat einmal als
terminaler Arbeitskontakt vorkommen: Es muss der vorletzte Pfadknoten direkt vor Vanillas
Annäherungstile sein und darf weder wiederholt noch als Durchgang verwendet werden. Findet der
Audit einen anderen fremden Moat, berechnet der Mod nur dann einen exakten owner-sicheren
Ersatzpfad zum unveränderten Vanilla-Ziel. Der Ersatz wird über denselben 1000-Byte-Unitpuffer,
Längenvertrag und Decode-Roundtrip veröffentlicht. Scheitert irgendeine Prüfung, werden Puffer,
Länge und Builderzustand auf den Stand vor dem Retry zurückgesetzt.

`ownerSafetyViolation` bezeichnet damit nur noch einen vom Mod veröffentlichten Pfad mit fremdem
Nicht-Ziel-Moat oder ungültigem Owner. Ein unveränderter positiver Vanilla-Pfad wird nicht mehr
als Modverletzung klassifiziert.

## Verworfen oder ersetzt

- Globale Bytepatches im Cursordispatcher verursachten falsche Mauer-/Klettercursor und wurden
  vollständig entfernt.
- Der Assassin-Sonderpfad über `pathManager+0x88` und eine Reflection-Routenbrücke erzeugte zwar
  kombinierte Probewege, Vanilla konsumierte sie aber nicht zuverlässig. Assassinen sind nach
  dem finalen Capabilityvertrag ohnehin ausgeschlossen; dieser Sondercode wurde entfernt.
- Eine auf Start-/Zielregion beschränkte Managed-BFS schnitt gültige Zwischenregionen ab und
  wurde durch die regionsunabhängige Tilegraph-Probe ersetzt.
- Das nachträgliche Erfinden fehlender Gebäude-Kontexttiles (`candidate+4`) war falsch. Der
  Fallback greift jetzt früher in `0xDA020` ein, sodass Vanilla vollständige Kandidatenpaare
  erzeugt.
- Gebäude-Hover darf nicht von gelegentlich auf `(0,0)` springenden Cursor-X/Y-Globals abhängen.
  Maßgeblich sind Vanillas Hover-Building-ID, ein gültiges Mouse-Tile und das nächstgelegene echte
  StructureGrid-Tile desselben Gebäudes.
- Hochfrequente Shadow-, Per-Tick-, Stall-, leere Queue- und gewöhnliche Bodenwegdiagnosen wurden
  entfernt. Beibehalten sind verwendete Fallbacks/gewichtete Veröffentlichungen, Rollbacks,
  Exceptions, Ownerverletzungen und aggregierte langsame Commands.

## Noch offen beziehungsweise erneut zu bestätigen

- Der reparierte Unit-Kontext, der zielgerichtete Command-Cache, die gebündelte Arbeitszielsuche
  und der abschließende Retry-Pfadaudit benötigen einen
  gezielten Performance- und Fill-Wiederholungstest. Erwartet wird höchstens eine Qualifikation je
  Start-/Zielregion statt einer Suche je Formationsoffset oder Unit.
- Der früher als `ownerSafetyViolation=True` gemeldete Fill-Fall ist mit dem alten Log allein nicht
  eindeutig: Vanilla darf beim Zuschütten das feindliche Arbeitsobjekt berühren. Der neue Audit
  unterscheidet genau diesen einmaligen terminalen Kontakt von echter Durchquerung und rollt nur
  letztere zurück beziehungsweise ersetzt sie owner-sicher.
- Verbündete Moats verwenden denselben Allianzfilter wie eigene Moats, wurden aber nicht in allen
  Befehls- und Gruppenvarianten praktisch wiederholt.
- Multiplayer wurde architektonisch vorbereitet (`NetworkMode: 1`, deterministische Daten,
  kein eigenes Netzwerkprotokoll), aber ein Host-/Client-Smoke-Test mit identischen Modversionen
  und Desyncbeobachtung steht noch aus.
- `ForceAttackBuilding` konnte nicht in jedem Aufbau gezielt ausgelöst werden; der gemeinsame
  Gebäudeweg unterstützt den Commandwert, die vollständige praktische Abnahme ist offen.

## Empfohlener kurzer Abschlusstest

1. Gruppen mit 1, 5, 10 und 27 grabfähigen Units zunächst über normalen Boden und danach über
   einen zwingenden freundlichen Moat schicken. Normale Befehle dürfen keinen Fallback auslösen;
   gleiche Startregionen sollen Cachetreffer statt proportionaler Suchen zeigen.
2. Mehrere grabfähige Units gleichzeitig feindliche Moats zuschütten lassen. Ein terminaler
   Zielkontakt muss als zulässig erscheinen; jeder fremde Durchgang muss ersetzt oder vollständig
   zurückgerollt werden.
3. Optionalen kurzen Moat, gemischte Gruppen, Shift-Queue, Patrol, `AttackUnit`,
   `AttackBuilding`, Dig und Treppe regressionsprüfen.
4. KI-Skirmish mindestens zehn Minuten schnell vorlaufen lassen. Ziel sind typische Commands
   deutlich unter 50 ms, 27 gleiche Startregionen möglichst unter 100 ms, keine harten Pausen,
   keine Exceptions und keine tausenden Diagnosezeilen.
5. Danach Host und Client mit identischen Paketen starten, eigenen sowie feindlichen Moat testen
   und auf Desync/Exceptions achten.

## Update-Reihenfolge

Bei einer neuen Spiel- oder Extender-DLL:

1. installierten DLL-Hash gegen `CURRENT.json` prüfen;
2. zuerst `0x196280 -> 0xF4930`, echten Unit-Pfadpuffer und `0xDAFD0 -> 0xE1640` wiederfinden;
3. Command-6-Switch `0x11E960` und Auswahlhelper `0x191C00` erneut abgleichen;
4. Cursor-/Entitypfade `0x196870`, `0xE2CA0`, `0xB70C0`, `0xDBC60`, `0xDA020`, `0x123090`
   validieren;
5. Arbeitszielkette `0x69D60`, `0x6AF60`, `0x6C490` und Bridge-Ownership prüfen;
6. Geschwindigkeitsvertrag `0x19B260` und Unit-Handlerdispatch erneut dekodieren;
7. erst danach Hooks installieren; jede nicht vollständig validierte Gruppe bleibt Vanilla.

README und Modversion wurden während dieser Testphase bewusst nicht geändert.
