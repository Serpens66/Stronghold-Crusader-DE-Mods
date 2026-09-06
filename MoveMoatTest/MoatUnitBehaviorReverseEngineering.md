# MoveMoatTest – aktueller technischer Stand

Stand: 6. September 2026

## Zweck und Funktionsumfang

`MoveMoatTest` erlaubt ausschließlich den Unittypen, die in Vanilla Burggräben ausheben
können, fertige eigene oder verbündete Burggräben als Weg zu benutzen. Der Mod erweitert
damit die Wegfindung, ohne Vanilla-Befehle neu auszugeben oder Ziele künstlich zu ersetzen.

Unterstützt werden:

- Bogenschützen (`CHIMP_TYPE_ARCHER`)
- Speerträger (`CHIMP_TYPE_SPEARMAN`)
- Pikeniere (`CHIMP_TYPE_PIKEMAN`)
- Streitkolbenkämpfer (`CHIMP_TYPE_MACEMAN`)
- Ingenieure (`CHIMP_TYPE_ENGINEER`)
- arabische Sklaven (`CHIMP_TYPE_ARAB_SLAVE`)
- Eunuchen (`CHIMP_TYPE_BEDOUIN_EUNUCH`)
- Plänkler (`CHIMP_TYPE_BEDOUIN_SKIRMISHER`)
- Sappeure (`CHIMP_TYPE_BEDOUIN_SAPPER`)
- Demolierer (`CHIMP_TYPE_BEDOUIN_DEMOLISHER`)

Assassinen, Armbrustschützen, Schwertkämpfer, Ritter und Belagerungsgeräte erhalten keine
Moat-Freigabe. Feindliche oder ungültige Moats bleiben unpassierbar. Ein feindlicher Moat darf
nur als exakt gebundenes terminales Fill-Arbeitsobjekt berührt, aber niemals durchquert werden.

Der aktuelle Mod deckt direkte Bewegung, `AttackUnit`, Gebäudeangriffe, Queue, Patrol,
Wiederaufnahme nach Kampf sowie Dig-/Fill-Folgebewegungen ab. Starts auf einem eigenen oder
verbündeten fertigen Moat sind gültig. Gewöhnliche, feindliche und ungültige Region-0-Starts
bleiben fail-closed; Vanillas `targetRegion=0`-Sentinel wird weiterhin unterstützt.

## Einstellungen und Laufzeitmodi

`EnableMod` und `RouteMode` sind `[SyncHostOnly]`. Das Manifest verwendet `NetworkMode: 1`,
weil die Wegfindung spielrelevant und im Multiplayer auf allen Teilnehmern identisch sein muss.
Das gemeinsame Preset-, Rollen- und Trail-System wird unverändert verwendet.

Es gibt zwei Modi:

- Modus 0, **Individuelle Wege – genau**: Behält die individuelle, geschwindigkeitsgewichtete
  Wegwahl. Er darf auch einen optionalen freundlichen Moatweg veröffentlichen, wenn dieser für
  das konkrete Runtimeprofil ausreichend schneller und für alle plausiblen Profile niemals
  schlechter als Vanillas Route ist.
- Modus 1, **Nur notwendige Moatwege – schnell**: Ist Standard und Resetwert. Er sucht nur dann
  durch freundliche Moats, wenn das Ziel ohne Moat nicht erreichbar ist. Bei einem erreichbaren
  Bodenziel bleibt die Verarbeitung vollständig bei Vanilla; es gibt keine optionale Moatsuche,
  keine Profilbewertung und keine gewichtete Veröffentlichung.

Andere gespeicherte Zahlenwerte werden konservativ als Modus 0 behandelt. Ein unveränderlicher
`MovementOptionsSnapshot` bindet Aktivierung und Modus an den Beginn eines synchronen Befehls.
Verschachtelte Move-/Attack-Aufrufe behalten deshalb den begonnenen Zustand.

## Required-only-Vertrag

Der schnelle Modus führt die folgenden Schritte aus:

1. Gewöhnliche, strukturfreie Start- und Zielfelder derselben positiven nativen PCL-Region
   gelten unmittelbar als über Boden erreichbar.
2. Moat-, Region-0-, Struktur- und ungültige Endpunkte sowie unterschiedliche positive Regionen
   verwenden die konservative Topologieprüfung und nötigenfalls eine exakte Bodensuche.
3. Ist Boden erreichbar, wird keine weitere Modsuche gestartet.
4. Ist Boden nicht erreichbar, wird die kürzeste ungewichtete, kodierbare Feldroute gesucht, die
   ausschließlich fertige eigene oder verbündete Moats als zusätzliche Kanten verwendet.
5. Nur eine vollständig an die konkrete Unit und ihren echten Puffer gebundene Route darf
   veröffentlicht werden.

Entscheidungen und Suchfelder können innerhalb desselben gebundenen Befehls wiederverwendet
werden. Der Schlüssel umfasst Spieler, Unit-/Global-ID, Start, Ziel, Tick, Kartenepoche,
Terrainrevision, reservierten Endpunkt und Arbeitsziel. Jede Abweichung invalidiert den Cache.

`0xF4930` wird weiterhin genau einmal pro tatsächlichem Builderaufruf ausgeführt. Der Mod ersetzt
nicht den nativen Builder, sondern kann dessen Ergebnis nur im nachgewiesenen notwendigen Fall
am echten Unitpuffer sicher ergänzen. Vor und nach einer Veröffentlichung werden geprüft:

- lebende Unit, 1-basierte Game-ID, Global-ID und Spieler;
- unveränderte Start-/Ziel-, Tick-, Epochen- und Revisionsbindung;
- exakt der zu dieser Unit gehörende 1000-Byte-Pfadpuffer;
- jede gerichtete Kante, Höhe, Diagonale, Bewegungsmaske und Strukturregel;
- Owner jedes verwendeten Moats;
- maximale Länge von 2000 Kanten und nibble-kodierter Encode-/Decode-Roundtrip;
- Ausgabelänge, Route-Variante und erforderlicher PathManager-Zustand.

Schlägt eine Prüfung fehl, werden Puffer, Länge und Zustandsfelder vollständig zurückgesetzt.
Vanilla bleibt dann die einzige wirksame Ausgabe. Exceptions, Ownerverletzungen,
Publikationsfehler und Rollbacks werden stets unmittelbar protokolliert.

## Exakter Modus und Kostenmodell

Modus 0 liest die tatsächlichen Bewegungsfelder der konkreten Unit. Das Modell berücksichtigt
`r_CurrentSpeed`, `r_CurrentSpeed2`, `r_SpeedBonus`, zusätzliche Teilschritte und die Moatphase
aus der nativen Bewegungsroutine. Eine Moatkante verwendet den bestätigten Delay-Aufschlag `+6`.
Strukturpfade werden mangels kalibrierter Strukturkosten nicht durch eine optionale gewichtete
Route ersetzt.

Eine optionale Route wird nur veröffentlicht, wenn sie mindestens eine freundliche Moatkante
enthält, vollständig auditiert werden kann, im tatsächlichen Runtimeprofil mindestens 40 Ticks
spart und unter allen aus dem nativen Handler abgeleiteten plausiblen Profilen strikt nicht
schlechter bleibt.

## Traversierung, Ziele und Platzierung

Vanilla besitzt keinen einzelnen Schalter für Moat-Traversierung. Cursorprüfung,
Gruppenregionsprüfung, Angriffsnäherung, Formation, Arbeitszielauswahl und finaler Builder können
unabhängig voneinander abbrechen. Der Mod verwendet deshalb mehrere schmale Adapter mit einer
gemeinsamen owner-sicheren Traversierungsregel.

Die Regel entspricht dem achtgerichteten nativen Tilegraphen. Sie prüft Richtungsmasken,
Bewegungsmasken, Höhe, Diagonalen, `StructureGrid` und die native Sonderstrukturprüfung für
Treppen, Rampen und Mauerübergänge. PCL-Regionen dienen als schneller Bodenbeweis oder
konservative Vorprüfung, aber nicht als allgemeine Traversierungs-Whitelist.

Angriffs- und Gebäudeziele behalten Vanillas Entityidentität und Zielauswahl. Reservierte
Annäherungsfelder werden unitweise geprüft und verteilt. Bei Dig/Fill bleiben Arbeitsobjekt,
Reservierung und Reihenfolge nativ; die Modroute darf nur den gebundenen terminalen Kontakt
ergänzen. Die öffentliche Bridge `RegisterImprovedMoatFillingProvider` liefert weiterhin `1`,
wenn MoveMoat die zusammengehörige Hookgruppe vollständig besitzt, andernfalls `0`. Es darf nur
einen Owner dieser Hookgruppe geben.

## Script Extender 2.0.2 und Hook-Lebenszyklus

Der Mod verlangt mit `[BepInDependency("000shcdese", "2.0.2")]` mindestens Script Extender
2.0.2 und wird gegen dessen `SHCDESE.dll` sowie `RedBird.Abstractions`, `RedBird.Core` und
`RedBird.X64` kompiliert. Alte MonoMod-/Zhuqiaomon-Hookreferenzen sind nicht mehr Bestandteil
des Projekts oder Pakets.

Alle nativen Hooks werden über RedBird-Transaktionen installiert:

- die zentrale Runtimegruppe gemeinsam in einer Transaktion;
- Connectivity, Recovery und Platzierungsadapter gemeinsam in einer Transaktion;
- Angriffsnäherung, Gebäudecursor und Moat-Arbeitszielauswahl jeweils als atomare Gruppe.

Jede Transaktion verwendet `RollbackAndThrow` und `OwnsHooks=true`. Originaldelegates werden
erst nach einem vollständig erfolgreichen Commit veröffentlicht. Bei einem Fehler wird die
gesamte betroffene Gruppe zurückgerollt; Bridge-Bereitschaft wird erst nach vollständigem Commit
gemeldet. Damit kann kein halb installierter Hookzustand sichtbar werden.

Der Script Extender übergibt beim `LibraryLoaded`-Event den `CrusaderLibraryLoadContext`. Der Mod
bezieht Region, Speicheransicht und Basisadresse ausschließlich aus diesem Kontext. Die
Unitselektion verwendet die 2.0.2-API `GetSelectedChimps()` als `SelectedUnitInfo[]` und liest
die 1-basierten `UnitId`-Werte in gelieferter Reihenfolge. Private Reflection auf Vanillas
`selectedChimps` ist nicht mehr Teil der Runtime.

Die Runtime und ihre Delegates bleiben statisch beziehungsweise durch langfristige
Script-Extender-Subscriptions verwurzelt. Es gibt keinen Hookabbau in `BaseUnityPlugin.OnDestroy`,
weil dieser Callback in SHCDE bereits während des normalen Starts auftreten kann.

## Native Bindung

Alle nachfolgenden RVAs und Maschinenverträge gelten ausschließlich für die kanonische
`CrusaderDE.dll` mit SHA-256:

`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

Die installierte DLL muss vor Build und Installation mit
`_inspect/CrusaderDE-Native-Baseline/CURRENT.json` übereinstimmen. Pattern, erwartete RVAs,
Entrybytes, relative Callziele und Funktionsgrenzen werden vor der Hookinstallation geprüft.
Eine Abweichung verhindert die Installation geschlossen.

Wichtige native Stellen:

| RVA | Bedeutung |
| --- | --- |
| `0x11E960` | Tribe-Command-Dispatcher und Vanilla-Filter für Dig/Fill |
| `0x191C00` | Auswahl enthält mindestens eine grabfähige Unit |
| `0x196840` | konkrete Unit steht auf einem fertigen Moat |
| `0xE2CA0` | gerichtete Tilepaar-Erreichbarkeit |
| `0xE2610` / `0xE7C40` | Regionspaar- und Regionssuche |
| `0x11B520` / `0x117BC0` / `0x119F90` | Gruppenbewegung, Moatmodus und Iterator |
| `0x196280` | per-Unit-Pfadanforderung und echter Unitpuffer |
| `0xF4930` | zentraler finaler Unit-Builder |
| `0xDA590` / `0xDB650` | Ground- und alternativer Builder |
| `0xE1640` / `0xE4E90` | Rekonstruktion und nibble-kodierter Pfad |
| `0xDBC60` | Annäherungssuche für `AttackUnit` |
| `0xB70C0` | Gebäude-Cursorerreichbarkeit |
| `0xDA020` / `0x123090` | Gebäudeannäherung und Kandidatenverbrauch |
| `0x69D60` / `0x6AF60` / `0x6C490` | Auswahl und Auflösung von Dig-/Fill-Zielen |
| `0x1853F0` / `0x1976C0` | Wiederaufnahme nach einem Kampf |
| `0x19B506` / `0x184203` | Moatphase und Bewegungstakt |
| `0x107160` | Sonderstrukturprüfung für Treppen, Mauern und Rampen |

## Logging

Das ausführliche Diagnoseprotokoll wird ausschließlich durch
`DetailedDiagnosticsEnabled` in `MoveMoatPathTest.cs` gesteuert. Die Konstante steht
standardmäßig auf `false` und ist bewusst keine öffentliche Einstellung.

Im normalen Spielbetrieb erscheinen nur einmalige Lade-, Native-Vertrags- und Hookmeldungen
sowie unmittelbar relevante Fehler, Owner-/Pufferverletzungen, Publikationsfehler und
Rollbacks. Befehls-, Cursor-, Tracker-, Milestone-, Folgetick- und Performance-Telemetrie wird
nicht erzeugt.

Für eine gezielte Untersuchung kann die Konstante vor einem neuen Build auf `true` gesetzt
werden. Details sind dann auf drei repräsentative Einträge je Kategorie begrenzt; unterdrückte
Ereignisse werden aggregiert. Required-only verfolgt höchstens acht Units je explizitem Befehl
und höchstens acht gleichzeitig bei ungebundener Hintergrundbewegung.

## Aktuelle Verifikation

Der vollständige eigenständige Regressionstest kompiliert alle Runtimequellen semantisch gegen
die installierten Script-Extender-2.0.2- und RedBird-Assemblies. Der aktuelle Lauf bestand mit:

- 224.445 Runtime-Assertions;
- 18.258 unabhängigen Suchassertions;
- 6.480 unabhängigen Gebäudefelddistanzen;
- 1.469.340 gerichteten Cursor-/Connectivity-Vergleichen;
- Gruppengrößen bis 1.000 und Formationstests bis 156 Units;
- statischer Prüfung der atomaren Commit-/Original-/Rollback-Reihenfolge aller Hookgruppen;
- Prüfung des 73 Byte langen Recovery-Inline-Adapters durch Assemblierung und Dekodierung;
- vollständiger Quellkompilierung von 20 Runtime-Dateien und 180 extrahierten Runtime-Membern.

Geprüft sind unter anderem beide Modi, Default/Reset, Presets, Trail-/Client-Sperren,
Same-PCL-Fast-Proof, notwendige eigene/verbündete Moatwege, Region-0-Starts, feindliche Moats,
verschachtelte Attack-/Move-Caches, Puffer- und Ownerbindung, Queue, Patrol, Post-Combat,
Gebäudeangriff, Dig/Fill, Rekonstruktion, Strukturkanten und Rollback.

Der geprüfte Stand wurde am 6. September 2026 genau einmal über
`MoveMoatTest/build.bat /nopause` gegen Script Extender 2.0.2 gebaut und installiert: null
Warnungen, null Fehler. Lokales und installiertes Paket stimmen für alle sechs Dateien per
SHA-256 überein; die Mod-DLL hat den Hash
`176E1EB2F68F0E7DE28A9FBDD4412AE7A48047ECEE694BF3F967C7B71C25BB45`. Das Paket enthält keine
RedBird-, Zhuqiaomon- oder MonoMod-Assembly.

Die automatisierten Prüfungen führen keine Hooks im Spielprozess aus. Nach der Installation ist
deshalb ein neuer 2.0.2-Editor-Smoke-Test erforderlich; anschließend bleibt ein Host-/Client-Test
mit identischer Mod- und Extenderinstallation als praktische Multiplayer-Abnahme sinnvoll.

## Fehlgeschlagene Implementierungsansätze

- Ein globaler Bytepatch im Cursordispatcher erzeugte falsche Mauer- und Klettercursor. Er wurde
  vollständig entfernt und durch kontextgebundene Adapter ersetzt.
- Ein Assassin-Sonderpfad über `pathManager+0x88` und eine Reflection-Routenbrücke erzeugte
  Probewege, die Vanilla nicht zuverlässig konsumierte. Assassinen sind nicht moatgrabfähig;
  der Sonderpfad wurde entfernt.
- Eine auf Start- und Zielregion beschränkte Managed-BFS schnitt gültige Zwischenregionen ab.
  Die aktuelle Suche arbeitet auf dem gerichteten Tilegraphen.
- Das nachträgliche Erfinden eines Gebäude-Kontexttiles als `candidate+4` war falsch. Der Mod
  greift früher ein und lässt Vanilla vollständige Kandidatenpaare erzeugen.
- Gebäude-Hover über flüchtige Cursor-X/Y-Globals war instabil, weil diese zeitweise auf
  `(0,0)` springen. Maßgeblich sind jetzt Hover-Building-ID, gültiges Mouse-Tile und echtes
  `StructureGrid` desselben Gebäudes.
- Vollkartensuchen pro Unit und hochfrequentes Einzellogging verursachten starke Pausen. Der
  Required-only-Modus verwendet Same-PCL-Beweise, befehlsgebundene Caches und standardmäßig
  minimales Logging.
- Gemeinsame Gruppenwege reduzierten zwar modseitige Sucharbeit, ließen aber native
  Einzelbuilder und erhebliche Diagnosekosten bestehen. Die zusätzliche Komplexität brachte
  keinen ausreichenden Gesamtnutzen und wurde vollständig entfernt.
- Ein beobachteter direkter Ersatz des nativen `0xF4930`-Builders ließ sich nicht mit einem
  vollständigen Puffer- und Zustandsvertrag absichern. Der Builder bleibt deshalb einmal pro
  tatsächlichem Aufruf aktiv.

## Update- und Abnahmereihenfolge

Bei einer neuen Spiel- oder Script-Extender-Version:

1. Installierte `CrusaderDE.dll` gegen `CURRENT.json` prüfen.
2. Alle verwendeten Pattern, RVAs, Entrybytes, Callziele und Inline-Adapter erneut validieren.
3. Script-Extender-, RedBird- und Auswahl-API-Verträge gegen die tatsächlichen Assemblies
   kompilieren.
4. Atomare Hookgruppen, Rollbacks, Lifecycle und Bridge-Ownership statisch prüfen.
5. Vollständige Regressionen sowie `git diff --check` und CRLF-Prüfung ausführen.
6. Erst danach genau einmal über `MoveMoatTest/build.bat /nopause` bauen und installieren.
7. Lokale und installierte DLL vergleichen und anschließend Move, Attack, Gebäude, Queue,
   Patrol, Post-Combat, Dig/Fill sowie Host/Client im Spiel prüfen.

Modversion und README bleiben während dieser Testphase unverändert.
