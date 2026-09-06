# Friendly moat movement – technischer Vertrag

Stand: 6. September 2026

## Zweck und Funktionsumfang

BugfixesAndQoL erlaubt ausschließlich den Einheitentypen, die in Vanilla Burggräben ausheben
können, fertige eigene oder verbündete Burggräben als Weg zu verwenden. Feindliche oder
ungültige Gräben bleiben unpassierbar. Ein feindlicher Graben darf nur als exakt gebundenes
terminales Fill-Arbeitsziel berührt, aber niemals durchquert werden.

Unterstützt werden Bogenschützen, Speerträger, Pikeniere, Streitkolbenkämpfer, Ingenieure,
arabische Sklaven, Eunuchen, Plänkler, Sappeure und Demolierer. Assassinen, Armbrustschützen,
Schwertkämpfer, Ritter und Belagerungsgeräte erhalten keine Grabenfreigabe.

Die Runtime deckt direkte Bewegung, `AttackUnit`, Gebäudeangriffe, Queue, Patrol,
Wiederaufnahme nach Kampf sowie Dig-/Fill-Folgebewegungen ab. Starts auf einem eigenen oder
verbündeten fertigen Graben sind gültig. Gewöhnliche, feindliche und ungültige
Region-0-Starts bleiben fail-closed; Vanillas `targetRegion=0`-Sentinel bleibt unterstützt.

## Einstellung und Laufzeitmodi

`FriendlyMoatMovementMode` ist eine `[SyncHostOnly]`-Property im gemeinsamen
BugfixesAndQoL-Presetmodell. Das Manifest verwendet `NetworkMode: 1`, weil die Wegfindung im
Mehrspieler auf allen Teilnehmern übereinstimmen muss.

- `0`, **Aus**: Freundliche Grabenbewegung ist vollständig inaktiv.
- `1`, **Individuelle Wege – genau**: Darf einen vollständig geprüften, optional schnelleren
  freundlichen Grabenweg anhand des konkreten Bewegungsprofils veröffentlichen.
- `2`, **Nur notwendige Grabenwege – schnell**: Standard und Resetwert. Eine Grabenroute wird
  nur gesucht, wenn Vanilla das Ziel nicht über normalen Boden erreichen kann.

Ungültige gespeicherte oder synchronisierte Werte werden als **Aus** normalisiert. Ein
unveränderlicher `MovementOptionsSnapshot` bindet Aktivierung und Modus an den Beginn eines
synchronen Befehls, sodass verschachtelte Move-/Attack-Aufrufe denselben Zustand behalten.

Die separate Einstellung `EnableImprovedMoatFilling` bleibt unabhängig: Sie verbessert die
Auswahl freier Randfelder beim Verfüllen feindlicher Gräben auch dann, wenn die freundliche
Grabenbewegung ausgeschaltet ist. Beide Funktionen verwenden dieselbe atomar installierte
Hookgruppe; es gibt keine Bridge und keine parallele zweite Implementierung.

## Required-only-Vertrag

Der schnelle Modus akzeptiert gewöhnliche, strukturfreie Start- und Zielfelder derselben
positiven nativen PCL-Region unmittelbar als bodenerreichbar. Moat-, Region-0-, Struktur- und
ungültige Endpunkte sowie unterschiedliche positive Regionen verwenden die konservative
Topologieprüfung und nötigenfalls eine exakte Bodensuche.

Ist Boden erreichbar, bleibt die Verarbeitung vollständig bei Vanilla: keine zusätzliche
Moatsuche, keine Profilbewertung und keine gewichtete Veröffentlichung. Ist Boden nicht
erreichbar, sucht die Runtime die kürzeste ungewichtete, kodierbare Feldroute, die nur fertige
eigene oder verbündete Gräben als zusätzliche Kanten verwendet.

Entscheidungen und Suchfelder dürfen nur innerhalb desselben gebundenen Befehls wiederverwendet
werden. Die Bindung umfasst Spieler, Unit-/Global-ID, Start, Ziel, Tick, Kartenepoche,
Terrainrevision, reservierten Endpunkt und Arbeitsziel. Jede Abweichung invalidiert den Cache.

`0xF4930` wird weiterhin genau einmal pro tatsächlichem Builderaufruf ausgeführt. Eine verwaltete
Route darf das Ergebnis nur am echten Puffer der gebundenen Unit ergänzen. Vor und nach jeder
Veröffentlichung werden Unitidentität, Owner, Start/Ziel, Tick, Revision, sämtliche gerichteten
Kanten, Höhen-, Diagonal-, Bewegungsmasken- und Strukturregeln, die maximale Pfadlänge und der
nibble-kodierte Roundtrip vollständig geprüft. Bei einer Abweichung werden Puffer, Länge und
Zustandsfelder zurückgerollt und Vanilla bleibt allein wirksam.

## Exakter Modus

Der exakte Modus berücksichtigt die tatsächlichen Geschwindigkeits-, Bonus- und Moatphasenfelder
der konkreten Unit. Eine optionale Route muss mindestens eine freundliche Moatkante enthalten,
vollständig auditierbar sein, im tatsächlichen Profil mindestens 40 Ticks sparen und unter allen
aus dem nativen Handler abgeleiteten plausiblen Profilen nicht schlechter als Vanilla sein.
Strukturpfade werden mangels kalibrierter Strukturkosten nicht optional ersetzt.

## Native und Lifecycle-Verträge

Die Verträge gelten ausschließlich für die kanonische `CrusaderDE.dll` mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Pattern, RVAs,
Entrybytes, relative Callziele und Funktionsgrenzen werden vor der Hookinstallation geprüft;
eine Abweichung deaktiviert das Feature geschlossen.

Wichtige Stellen sind der Tribe-Dispatcher `0x11E960`, die Regionsprüfungen `0xE2610` und
`0xE7C40`, die per-Unit-Pfadanforderung `0x196280`, der finale Builder `0xF4930`, die
Angriffsnäherung `0xDBC60`, die Moat-Arbeitsfunktionen `0x69D60`, `0x6AF60` und `0x6C490`
sowie die Kampf-Wiederaufnahme `0x1853F0` und `0x1976C0`.

Die Runtime verwendet RedBird-Transaktionen mit `RollbackAndThrow` und `OwnsHooks=true`.
Originaldelegates werden erst nach vollständigem Commit veröffentlicht. Zentrale Bewegung,
Connectivity/Recovery, Angriffsnäherung, Gebäudecursor und Moat-Arbeitszielauswahl werden jeweils
als atomare Gruppe installiert. Die Runtime wird durch `BugfixesAndQoLRuntime` verwurzelt und
nicht im frühen `BaseUnityPlugin.OnDestroy()` abgebaut.

Der Quellstand nutzt den `CrusaderLibraryLoadContext`, `SelectedUnitInfo[]` und RedBird aus Script
Extender 2.0.2. BugfixesAndQoL deklariert bereits 2.2.0; die abschließende API-Validierung, der
Build und die Installation erfolgen gemeinsam mit der separaten 2.2.0-Migration.

## Logging

Das ausführliche Diagnoseprotokoll wird durch `DetailedDiagnosticsEnabled` in
`FriendlyMoatMovementRuntime.cs` gesteuert und steht standardmäßig auf `false`. Normalerweise
erscheinen nur einmalige Lade-/Vertragsmeldungen sowie Exceptions, Owner- oder Pufferverletzungen,
Publikationsfehler und Rollbacks. Bei aktivierter Diagnose sind Details je Kategorie begrenzt;
Required-only verfolgt höchstens acht repräsentative Units pro explizitem Befehl.

## Fehlgeschlagene Implementierungsansätze

- Ein globaler Bytepatch im Cursordispatcher erzeugte falsche Mauer- und Klettercursor und wurde
  durch kontextgebundene Adapter ersetzt.
- Ein Assassin-Sonderpfad über `pathManager+0x88` und eine Reflection-Routenbrücke erzeugte
  Probewege, die Vanilla nicht zuverlässig konsumierte. Assassinen sind nicht moatgrabfähig.
- Eine auf Start- und Zielregion beschränkte Managed-BFS schnitt gültige Zwischenregionen ab;
  die Suche arbeitet deshalb auf dem gerichteten Tilegraphen.
- Das Erfinden eines Gebäude-Kontexttiles als `candidate+4` war falsch. Die Runtime greift früher
  ein und lässt Vanilla vollständige Kandidatenpaare erzeugen.
- Gebäude-Hover über flüchtige Cursor-X/Y-Globals war instabil. Maßgeblich sind Hover-Building-ID,
  gültiges Mouse-Tile und echtes `StructureGrid` desselben Gebäudes.
- Vollkartensuchen pro Unit und hochfrequentes Einzellogging verursachten starke Pausen.
  Same-PCL-Beweise, befehlsgebundene Caches und minimales Standardlogging ersetzen dies.
- Gemeinsame Gruppenwege reduzierten modseitige Sucharbeit, ließen aber native Einzelbuilder und
  Diagnosekosten bestehen. Die zusätzliche Architektur wurde vollständig entfernt.
- Ein direkter Ersatz von `0xF4930` ließ sich nicht mit einem vollständigen Puffer- und
  Zustandsvertrag absichern; der native Builder bleibt aktiv.

## Verifikation und nächste Abnahme

Die verschobene Regression prüft beide aktiven Modi, den ausgeschalteten Zustand, Presets und
Host-/Client-/Trail-Sperren, Same-PCL-Beweise, notwendige eigene/verbündete Grabenwege,
Region-0-Starts, feindliche Gräben, verschachtelte Befehle, Puffer-/Ownerbindung, Queue, Patrol,
Post-Combat, Gebäudeangriffe, Dig/Fill, Rekonstruktion, Strukturkanten und Rollbacks. Außerdem
werden Native-Hash, atomare Commit-/Rollback-Reihenfolge, Quellkompilierung, CRLF und
`git diff --check` kontrolliert.

In diesem Integrationsschritt wird nicht gebaut oder installiert. Nach der Script-Extender-
2.2.0-Migration folgt genau ein BugfixesAndQoL-Build, anschließend der Vergleich lokaler und
installierter Dateien und erst danach die Entfernung der installierten Standalone-Mod.
