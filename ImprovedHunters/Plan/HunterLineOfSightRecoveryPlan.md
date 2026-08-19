# ImprovedHunters: aktueller Entwicklungsplan

Stand: `2026-08-18`

Aktueller Quellstand: `1.1.67`; letzter Ingame-Test: `1.1.66` auf Steam Build
`24651686`.
SHA-256 der auditierten `CrusaderDE.dll`:
`33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`.

Abgeschlossene Pakete, historische Zwischenstände und verworfene Ansätze stehen
in [HunterLineOfSightRecoveryCompletedWork.md](HunterLineOfSightRecoveryCompletedWork.md).
Vollständige RVA-, Byte-, Resolver- und Updateprüfungen stehen in
[UpdateToNewDLL.md](UpdateToNewDLL.md).

## Aktueller Status

- Paket A, die PCL-basierte Erreichbarkeitsprüfung, ist abgeschlossen.
- Paket B, Vanillas normale Kette aus Schuss, Zielkadaver, Pickup und Abgabe,
  ist abgeschlossen.
- Paket E ist aktiv, aber `1.1.66` ist nicht abnahmefähig. Die atomare
  Nach-Schuss-Komponente wurde erstmals vollständig initialisiert und der
  Path-complete-Fallback berechnete einen nachweislich veralteten Zielpfad
  einmal erfolgreich neu. Die frühe State-9-zu-State-0-Umleitung trifft jedoch
  unmittelbar danach auf den transienten Beute-Reservierungswert `1`; ihre
  eigene Revalidierung verwirft deshalb jede Fortsetzung. Vanilla wählt dann
  wiederholt andere Rehe. Zusätzlich setzt ein normales `MoveHere` das
  Drei-Versuche-Budget selbst bei unveränderter Zielidentität zurück. Diese
  beiden Regressionen müssen vor der restlichen Paket-E-Matrix stabilisiert
  werden.
- Unmittelbar nach Abschluss von Paket E folgt eine verpflichtende
  bidirektionale Geschwindigkeits-Nachprüfung. Danach folgen Paket F für
  unreservierte Kadaver, Paket D als gemeinsame Beutetyp-/Mehrjägermatrix und
  zuletzt Paket C für die Produktionsbereinigung.
- Ein sichtbarer Jagdsprint ist optional. Echter Multiplayer bleibt bis Script
  Extender `1.50.0` deaktiviert.

Verbindliche Reihenfolge:
`E → Geschwindigkeits-Nachprüfung → F → D → C`.

## Fachliches Ziel

Ein Jäger soll jede aktivierte Beuteart hinter Gebäuden, Mauern, Toren, Türmen
oder Gelände berücksichtigen, wenn ein gültiger Fußweg besteht. Er folgt
Vanillas Weg, verwendet auf langen Restwegen Vanillas passende
Geschwindigkeitsstufe, greift an der ersten belastbaren freien Sichtposition an
und nutzt danach Vanillas Kadaver-, Pickup- und Abgabekette.

Unerreichbare Tiere dürfen erreichbare Kandidaten nicht verdrängen. Ein während
des Anmarschs unerreichbar gewordenes Ziel muss kontrolliert verworfen werden.
Nach einem nicht tödlichen Schuss muss dasselbe weiterhin gültige Ziel verfolgt
werden, statt den Jäger zur Hütte zurückzuschicken.

Paket F ergänzt zwei Kadaverfälle:

- vorhandene unreservierte Kadaver nehmen an der normalen Kostenrangfolge teil;
- tötet der Pfeil des Jägers versehentlich ein anderes reservationsfreies
  Beutetier, übernimmt der Jäger unmittelbar genau diesen Kadaver.

Unterstützte Zieltypen sind Reh, Ziege, Hase, Kamel und Huhn.

## Verbindliche Architektur

1. PCL `0` verwirft einen Kandidaten oder ein aktives Ziel. Ein positives PCL
   beweist nur grobe Verbindung; Vanillas `MoveHere` bleibt für den Detailpfad
   autoritativ.
2. Vanillas Zielsuche, Zielzuweisung, Reservation, Pfaderzeugung, Bewegung,
   Angriff und Pickup bleiben die regulären Ausführungspfade.
3. Der Mod darf vorhandene Vanilla-Vergleichswerte oder Flags nur am kleinsten
   validierten Übergang temporär beeinflussen. Keine eigene Jagd-State-Machine,
   kein eigener Recovery-Move und keine direkten Speed-, Animations-, Pfad-,
   Order- oder AI-State-Schreiboperationen.
4. Alle Zustände sind an Hunter- und Zielslot plus Global-ID, Spieler,
   Kartengeneration und die jeweils relevante Pfadgeneration gebunden.
5. Native Hooks werden exact-hash oder über semantisch eindeutige Resolver
   aufgelöst und vor Installation vollständig auf Instruktionsspanne,
   eingehende Ziele, RIP-relative Operanden, Branches, Calls, Register und Flags
   geprüft. Abweichungen deaktivieren nur das betroffene Feature und lassen
   Vanilla unverändert.
6. Beobachtung, Korrektur und Vanilla-Aufruf besitzen getrennte Fehlerpfade.
   Vanilla läuft exakt einmal.
7. Verhaltensänderungen bleiben in echtem Multiplayer und im Karteneditor
   fail-closed.

## Getrennte Feature-Schalter

Die Produktionsstruktur und Lobby-Modsettings trennen die Verhaltensbereiche
seit `1.1.67` ausdrücklich:

- `ImprovedTargetSelection` steuert ausschließlich Kostenrangfolge,
  Distanz-/Fleischertragsbewertung, initiale PCL-Erreichbarkeit und den
  State-0-Handoff eines ausgewählten lebenden Kandidaten. Die grundlegende
  Freigabe einer Tierart durch ihren jeweiligen `Hunt...`-Schalter bleibt davon
  unabhängig.
- `ImprovedPathfinding` steuert aktive PCL-Neuprüfung, Sichtbehandlung,
  Pfadfortsetzung, Moving-Target-Replan, Nach-Schuss-Fortsetzung und die
  Geschwindigkeitswahl aus dem tatsächlichen Restweg.
- `AllowDeadTargets` ist die Sicherheitsgrenze für Paket F. Eigene regulär
  erlegte und bereits reservierte Kadaver bleiben unabhängig davon Teil von
  Vanillas normaler Pickupkette.
- `ReliableHunterProjectiles` steuert ausschließlich die validierte erneute
  Anwendung von Vanillas Fernkampfschaden für einen eindeutig korrelierten
  Jägerpfeil.
- Tierartspezifische Kompatibilitätskorrekturen folgen ausschließlich dem
  jeweiligen `Hunt...`-Schalter. Insbesondere hängen automatisches und
  manuelles Hühner-Targeting, Neutralisierung und Kornspeicherlimit gemeinsam
  an `HuntChicken`; bei deaktivierter Hühnerjagd ist das eingestellte Limit
  wirkungslos.
- Der Fleischertrag `-1` ist nur für Reh und Ziege zulässig und lässt den
  tatsächlichen Ertrag Vanilla. Für die Zielbewertung werden dabei die
  bisherigen Vanilla-Schätzwerte `6` beziehungsweise `4` verwendet. Hase,
  Kamel und Huhn bleiben auf den Bereich `0..100` begrenzt.

Die vier Feature-Schalter sind Verhaltensgrenzen und dürfen einander nicht
implizit aktivieren. Die State-0-Hooks der Zielauswahl und die State-1-Hooks der
Pfadfindung werden derzeit aus Sicherheitsgründen weiterhin in einer atomaren
nativen Hook-Transaktion installiert: Alle Instruktionsspannen werden geprüft,
bevor irgendein Teil geschrieben wird. Ihre Callbacks und Zustände sind dennoch
getrennt gegated. Falls sich bei einem künftigen Spielupdate nur eine der beiden
Hookgruppen nicht mehr validieren lässt, muss die Transaktion vor einer
Teilaktivierung sauber getrennt werden; es darf keinen ungeprüften Teilverbund
geben.

## Paket E: Restweg, Sichtübergabe und Nach-Schuss-Weiterverfolgung

### Bereits funktionierender Stand

#### Zielauswahl-Vorstufe und aktive Erreichbarkeit

- Unter `ImprovedTargetSelection` prüft `HunterPclReachability` lebende
  Kandidaten vor der Kostenrangfolge und nochmals am konkreten Handoff. Der
  Cache gilt höchstens eine Sekunde und nur bei identischen Eingaben.
- Ein positives PCL lässt Vanilla planen. Liefert Vanillas vollständige Suche
  trotz eines gültigen verborgenen Kandidaten kein Ziel, stellt der begrenzte
  Zielauswahl-Fallback genau diesen Kandidaten bereit. Vanilla ruft anschließend
  selbst `MoveHere` auf.
- Unter `ImprovedPathfinding` wird ein aktives Ziel mit neuem PCL `0`
  identitätsgesichert invalidiert; Vanilla sucht neu. Technische PCL-Fehler sind
  fail-open. Damit ist die aktive Neuprüfung Paket E zugeordnet, während die
  initiale Kandidatenprüfung zur Zielauswahl gehört.

#### Geschwindigkeit nach tatsächlicher Reststrecke

- Vanillas direkte Hunter-Distanz ist Manhattan-Distanz und enthält keinen
  Hindernisumweg.
- Der gepackte Vanilla-Pfad wird read-only dekodiert. Vergleichbare Restkosten
  sind `orthogonale Schritte + 2 * diagonale Schritte`.
- Bei blockierter Sicht darf der exakthashgebundene Hook ausschließlich eine
  vorhandene schnellere Vanilla-Distanzstufe auswählen. Der relocatete
  Vanilla-Code schreibt Geschwindigkeit und Animation selbst.
- Positive Sicht lässt die direkte Distanz unverändert und erhält Vanillas
  langsame letzte Schussannäherung.
- Der zustandsgebundene Eingriff endet nach `60 s`, nach `3 s` ohne
  Pfadfortschritt oder bei Kontextwechsel; ein echter No-progress-Stopp sperrt
  nur dieselbe Identität für `5 s`.
- Pfadstatus, Fortschritt und Länge sind `+0xF2`, `+0xF6` und `+0xF8`. Die
  vollständige Pfadpufferadressierung ist in `UpdateToNewDLL.md` dokumentiert.

#### Blockierte Sicht und Angriffshandoff

- Der aktive Sichttracker prüft dasselbe Ziel außerhalb der Inline-Hooks. Im
  Nahbereich bis Tile-Distanz `30` beträgt das Sollintervall `250 ms`, weiter
  entfernt eine Sekunde. Der beobachtete 100-ms-Scantakt führte im letzten Lauf
  zu ungefähr `303–311 ms` zwischen Nahproben.
- Ein Snapshot ist höchstens zwei Sekunden regulär lesbar. Die konservative
  Fortsetzung eines bekannten Blockiert-Zustands bleibt zusätzlich an denselben
  streng validierten Live-Kontext und einen aktiven unvollständigen Pfad
  gebunden.
- Proben bleiben in Vanillas vorübergehender Reservationsphase `1` erlaubt,
  wenn alle übrigen Identitäts-, State-, Pfad- und Kartenguards stimmen.
  Strikte Inline-Entscheidungen verlangen weiterhin die eigene Reservation `2`.
- Ein positiver Snapshot ist an die exakten Hunter- und Zieltiles gebunden.
  Bewegt sich eine Unit, bleibt der Angriff bis zur nächsten Probe gesperrt.
- Ein bekannter blockierter Snapshot fällt beim Ablauf nicht in einen
  Fehlangriff, solange derselbe Live-Kontext und aktive Pfad fortbestehen.
- Am Tile-Distanzvergleich `0x1300EA` setzt der Mod bei bestätigter Blockierung
  ausschließlich das temporäre Distanzregister auf Vanillas Wert `29`. Dadurch
  läuft Vanillas vorhandener Pfad weiter.
- Bei frischer, positionsgleicher positiver Sicht endet der Distanz-Override.
  Falls das nachgeschaltete Pfadzustandstor den Angriff noch verzögern würde,
  löscht der Hook bei `0x130110` ausschließlich das gespeicherte Zero Flag und
  lässt Vanillas originale Angriffssequenz laufen.

### Relevante Native-Semantik

| RVA beziehungsweise Feld | Aktuelle Bedeutung |
| --- | --- |
| `0x79C0` | Hunter-Manhattan-Distanz `abs(dx)+abs(dy)` |
| `0x12FC20` | Beginn `HunterUpdate` |
| `0x130019` | Welt-Nahbereichsvergleich gegen `20`; der Nahpfad kann eine neue Zielquery auslösen |
| `0x12FF2E` | Hunter-Zielquery im State-1-Nahpfad |
| `0x12FF53` | Fehlerfolge einer Nullquery zu State `6`, Timer `20` und Hüttenrückweg |
| `0x1300EA` | Tile-Distanzvergleich gegen `28`; Hookspanne `[0x1300EA,0x1300FD)` |
| `0x130110` | Nachgeschaltetes `+0xF4`-/Pfadzustandstor; Hookspanne `[0x130110,0x130124)` |
| `0x13013D` / `0x130149` | Direkter Angriff und Beobachtung seines Rückgabewerts |
| `0x130171` | Bekannter State-6-/Timer-20-Writer nach direktem Angriffsfehler |
| `0x1304D1` / `0x1304D6` | Primäre State-10-Zielquery nach Sicht-/Zielguard und ihr Ergebnis; Hookspanne `[0x1304D6,0x1304E5)` |
| `0x130577` / `0x13057C` | Sekundäre State-10-Zielquery und ihr Ergebnis; Hookspanne `[0x13057C,0x13058B)` |
| `0x12FF58` | Gemeinsamer State-6-/Timer-20-Writer nach einer Nullquery aus State 10 |
| `0x18AF00` | Vanilla-Hunter-Zielsuche |
| `0x196230` | `c_game_unit_issueorder_movehere` |
| `0xA06F0` / `0x9E350` | Sichtwrapper und Sichtkern; im Nahbereich werden beide Kernrichtungen bewertet |
| `0x9EF20` / `0x9C730` | Lebender Projektilschritt und zustandsverändernde Kollision; keine sichere Vorab-Sichtprüfung |

Wichtig: `attackResult=1` beweist nur, dass Vanillas Angriffsbefehl angenommen
wurde. Es beweist weder einen Treffer noch den Tod des beabsichtigten Ziels.

### Ergebnis des `1.1.61`-Tests

Freie Kontrollkarte ab `2026-08-18 10:49:33`:

- Vier Ziele wurden bei Tile-Distanzen `28`, `17`, `12` und `17` angegriffen.
- Zwischen letzter frischer positiver Sichtprobe und `attackResult=1` lagen
  ungefähr `26 ms`, `20 ms`, `24 ms` und `24 ms`.
- Angriffe mit `pathFieldF4=3` und `8` bestätigen, dass das frühere F4-Warten
  beseitigt ist. Der Lauf belegt keine verbliebene modseitige Schussverzögerung.

Blockierte Bewegungskarte ab `10:51:51`:

- Ziel `17/319` erhielt um `10:52:04.377` einen Pfad der Länge `79`.
- Der Pfad lief fast neun Sekunden bis `61/79`, auch bei Tile-Distanzen `<=28`
  und `rawProbeReservation=1`, ohne Fehlangriff oder vorzeitigen State `6`.
- Die erste positive Probe um `10:52:13.317` führte nach `1 ms` trotz
  `pathFieldF4=7` zu `attackResult=1`.
- Das Ziel `17/319` lebte danach weiter. Um `10:52:14.357` war es weiterhin
  `allowed=True`, `fallback=True`, PCL-erreichbar und ohne Cooldown, doch
  `best=none`; in diesem Update entstand kein neuer akzeptierter `MoveHere`.
- Um `10:52:15.372` lief der Sichttracker wegen `hunter-state-6` aus. Der Jäger
  kehrte zur Hütte zurück.
- Um `10:52:21.301` nahm derselbe Jäger dasselbe Ziel `17/319` erneut an. Das
  widerlegt Zielwechsel, Tod, Global-ID-Wechsel und dauerhafte Unerreichbarkeit
  als Ursache des ersten Abbruchs.
- Beim zweiten Versuch führte die positive Probe um `10:52:23.772` nach `6 ms`
  trotz `pathFieldF4=4` zum Angriff. Zielslot `17` wurde um `10:52:25.646`
  gelöscht; der Benutzer bestätigte den normalen Pickup.
- Es gab keine Improved-Hunters-Callbackexception, keinen
  `snapshot-expired`-Fehlangriff und keinen harten Prozessabbruch.

### Ursache des Paket-E-Fehlers und Korrekturen in `1.1.63/1.1.64`

Nach einem angenommenen, aber für das reservierte Ziel nicht tödlichen Schuss
beendet Vanilla die Jagd, obwohl dasselbe Ziel weiterlebt, erlaubt und
PCL-erreichbar ist. Die sichtbare Reaktion und der Angriffshandoff waren bereits
korrekt; auch eine zu große Entfernung ist bei fünf Tiles ausgeschlossen.

Die erneute Sichtblockade war der Auslöser der primären State-10-Query, aber
nicht der eigentliche Defekt. Der validierte Pfad ist:

`attackResult=1 → State 9 → State 10 → Sicht-/Zielguard → Query 0x1304D1 →`
`Nullergebnis → State-6-Writer 0x12FF58`.

Vanillas Query `0x18AF00` akzeptiert nur Beute mit Reservation `0`. Das
ursprüngliche Ziel `17/319` trug weiterhin korrekt die eigene Reservation `2`
und konnte deshalb nativ nicht zurückgegeben werden. Die Mod-Rangfolge
verwendete ebenfalls nur freie Beute, meldete deshalb `best=none` und erlaubte
den Eventkandidaten lediglich fail-open (`fallback=True`). Ohne einen
`TargetSelection`-Bestwert wurde er nicht für den bestehenden State-0-Fallback
vorgemerkt. Damit blieb das Queryergebnis null und Vanilla schrieb State `6`.

Der `1.1.62`-Test bestätigte den geplanten Handoff noch nicht. Die Komponente
brach bereits bei der Initialisierung fail-closed ab:

`Hunter State-10 query targets changed: primary=0x5BD2FBE, secondary=0x5BC8A64`.

Für den relativen `CALL` wurde irrtümlich Patternoffset `0x0B`, also die Adresse
des Opcodes `E8`, als Anfang des vier Byte großen Displacements übergeben. Das
Displacement beginnt erst bei Offset `0x0C`; die nächste Instruktion liegt bei
Offset `0x10`. Deshalb wurde keiner der beiden Ergebnis-Hooks installiert und
das beobachtete Spielverhalten entsprach weiterhin `1.1.61`.

`1.1.63` verwendet für beide Call-Ziele Offset `0x0C` und validiert sie damit
korrekt gegen Query-RVA `0x18AF00`. Die Call-Instruktionen selbst bleiben bei
`0x1304D1` und `0x130577`, die Ergebnis-Hooks unverändert bei `0x1304D6` und
`0x13057C`.

Der nun aktivierbare Handoff korreliert einen erfolgreichen direkten Angriff
mit beiden State-10-Queryausgängen. Bleiben Hunter und Ziel in exakt derselben Slot-/
Global-ID, das Ziel lebend, aktiviert, eigenreserviert, nicht fremdgezielt,
ohne Cooldown und PCL-erreichbar, wird ausschließlich das temporäre
Query-Rückgaberegister auf die Ziel-ID gesetzt. Vanillas relocatetes `TEST`
wählt dadurch seinen bestehenden State-0-Pfad. Die unmittelbar folgende
State-0-Query erhält genau einmal denselben eigenreservierten Kandidaten; danach
setzt Vanilla selbst Ziel, Reservation, Pfad und `MoveHere`. Kein AI-State-,
Order-, Pfad-, Speed- oder Animationsfeld wird vom Mod geschrieben.

Ist das Ziel tot, ungültig, fremdgezielt, deaktiviert, im Cooldown oder durch
PCL `0` getrennt, bleibt das Queryergebnis unverändert. Projektilmarker,
Zielgesundheit, Reservation, Querypfad, State-0-Handoff und `MoveHere`-Ergebnis
werden begrenzt und millisekundengenau protokolliert.

Ob der Pfeil nichts oder ein anderes Tier trifft, ändert die Paket-E-Anforderung
nicht: Solange das ursprüngliche Ziel gültig weiterlebt, muss derselbe Jäger es
weiterverfolgen. Die Übernahme eines versehentlich getöteten anderen Tiers ist
separat Paket F.

### Ergebnis des `1.1.63`-Tests und Korrektur in `1.1.64`

Der `1.1.63`-Test bestätigte die erfolgreiche Initialisierung und Ausführung
beider State-10-Hooks. Der spät ansetzende Handoff war dennoch nicht stabil:

- Nach dem Schuss auf Ziel `17` um `12:08:06.602` wählte der State-10-Hook um
  `12:08:07.610` zunächst State `0`. Bereits um `12:08:07.619` scheiterte die
  erneute Übergabe an der zu strikten Annahme, Zielpaar und Reservation müssten
  während des nativen Zustandswechsels unverändert `17/Global-ID` und `2`
  bleiben. Vanilla nahm unmittelbar Ziel `58` an. Das erklärt den sichtbaren
  Sitz-/Wartemoment und die nicht fortgesetzte ursprüngliche Jagd.
- Bei der späteren ersten Jagd erreichte der Jäger um `12:08:17.765` das Ende
  des alten Pfads (`path=2/0/22/22`), während das Reh inzwischen weitergelaufen
  war. Der direkte Angriff lieferte `attackResult=0`; Vanilla schrieb daraufhin
  State `6`. Dasselbe Ziel wurde um `12:08:21.526` erneut angenommen. Ursache
  war damit kein sinnvolles Versuchslimit, sondern ein fehlender Replan nach
  einem inzwischen veralteten, vollständig abgearbeiteten Pfad.

`1.1.64` setzt früher und an den kleinsten stabilen nativen Übergängen an:

- Nach der vollständigen Schusssequenz in State `9` wird der unmittelbar
  folgende Writer bei RVA `0x13023C` für ein weiterhin gültiges Ziel von State
  `10` auf State `0` umgelenkt. Dadurch entfällt die Sitz-/Warteanimation; eine
  Projektilende-Kontrolle ist nicht erforderlich. Das Log nennt ausdrücklich
  `State10SitPrevented=True` und `projectileEndWait=False`.
- Nach `attackResult=0` werden Vanillas vorbereitete State-6-Werte am Writer
  RVA `0x130171` nur bei identischer, lebender und PCL-erreichbarer Beute auf
  State `0` neutralisiert. Vanilla führt anschließend seine normale Query- und
  `MoveHere`-Neuberechnung aus.
- Die State-0-Übergabe akzeptiert die nachweislich transienten Kombinationen
  aus unverändertem oder bereits auf `0/0` freigegebenem Hunter-Ziel sowie
  Reservation `2` oder `0`. Eine andere Zielidentität oder Fremdreservation
  bleibt strikt ausgeschlossen und wird mit dem konkreten Feldwert geloggt.

Es gibt kein fixes Zeitlimit pro Jagd und keine 60-Sekunden-Grenze mehr für
Pfadfortsetzung, Restweg-Geschwindigkeit oder die Korrelation eines angenommenen
`MoveHere`. Stattdessen gelten folgende Grenzen:

- Solange sich Pfadfortschritt oder Pfadlänge ändern, darf eine beliebig lange
  Route weiterlaufen.
- Nur drei Sekunden ohne jeden Pfadfortschritt gelten als lokaler Stillstand;
  danach pausiert ausschließlich die betreffende Korrektur fünf Sekunden. Dies
  ist kein Jagd- oder Streckenzeitlimit.
- Pro Jäger und Zielidentität sind höchstens drei recovery-auslösende direkte
  Angriffe zulässig. Erfolgreiche `MoveHere`-Neuberechnungen zählen nicht als
  eigener Versuch. Beim dritten Fehl-/Nichttödlich-Schuss bleibt Vanillas
  Abbruchpfad bestehen und das Ziel erhält den vorhandenen begrenzten Cooldown,
  damit keine Endlosschleife entsteht.
- Ein unabhängig angenommener normaler Vanilla-`MoveHere` startet ein neues
  Versuchsbudget. Kartenwechsel und Identitätswechsel löschen alten Zustand.

### Ergebnis des `1.1.64`-Tests und Resolverkorrektur in `1.1.65`

`1.1.64` testete die neue Laufzeitlogik noch nicht. Beim Start um
`12:44:50.581` brach die gesamte atomare Nach-Schuss-Komponente fail-closed ab:

`Hunter State-9 completion State-10 writer reference RVA 0x13023C failed local byte validation`.

Die eigentliche State-9-Hookspanne `[0x13023C,0x130253)` war korrekt. Ihr
23-Byte-Code endet mit dem State-Writer
`66 42 89 B4 29 18 09 00 00`. Das Pattern enthielt zusätzlich die nachfolgende
Instruktion und erwartete dort fälschlich `42 C7 84 ...`; die kanonische DLL
enthält `42 89 AC 29 08 09 00 00`. Weil die Auflösung vor der Transaktion
fail-closed endete, wurden weder der State-9- noch der Fehlangriff-Hook und auch
keiner der beiden State-10-Fallbackhooks installiert.

Der Logverlauf entsprach deshalb weiterhin Vanilla:

- Ziel `17/319`: `attackResult=1` um `12:45:59.614`, danach keine
  `recoveryAttempt`-/`State10SitPrevented`-Marker; um `12:46:00.564` wählte
  Vanilla Ziel `58/457`.
- Ziel `58/457`: alter Pfad bei `29/29` abgeschlossen, Ziel wieder 13 Tiles
  entfernt, `attackResult=0` um `12:46:07.315`; ohne Fehlangriff-Hook folgte
  State `6` um `12:46:09.373`.
- Die erneute Jagd auf `58/457` endete nach `attackResult=1` normal in State `2`
  und Unit-Löschung/Pickup um `12:46:12.868`; dies war kein weiterer Abbruch.

`1.1.65` begrenzt beide neuen Resolverpatterns exakt auf 23 Bytes. Iced prüft
zusätzlich alle beteiligten Register, Immediate-Werte, Speicherdisplacements,
die exakte Hooklänge und jeweils die erste Instruktion unmittelbar hinter der
Span. Die bekannte vollständige HunterUpdate-Branchprüfung und die atomare
Vier-Hook-Transaktion bleiben verpflichtend.

### Ergebnis des `1.1.65`-Tests und Korrektur in `1.1.66`

Der Start um `12:57:15.670` zeigte, dass auch `1.1.65` die atomare
Nach-Schuss-Komponente noch fail-closed deaktivierte. Die Bytes und
Instruktionslängen waren korrekt; Iced klassifiziert bei `imul r64,r64,imm32`
den dritten Operanden jedoch als `Immediate32to64`, nicht als `Immediate32`.
`1.1.66` korrigiert ausschließlich diese beiden semantischen Prüfungen. Die
MOV-Immediates `20/6/1` bleiben unverändert `Immediate32`.

Der anschließende Lauf belegte zusätzlich einen von Schuss und Zeitlimit
unabhängigen Bewegungsfehler:

- Ziel `17/319` wurde um `12:58:26.925` mit Anker `381,351` und Pfadlänge `78`
  durch Vanilla-`MoveHere` angenommen.
- Um `12:58:36.253` stand der Jäger bei `385,357`, das Reh bereits hinter ihm
  bei `393,356`. Der alte Anker lag zehn Manhattan-Schritte, das lebende Ziel
  nur neun Schritte entfernt; die Richtungsvektoren zeigten mit Skalarprodukt
  `-26` gegeneinander und das Ziel hatte sich zwölf Tiles vom Anker entfernt.
- Trotzdem blieb `inFlightCorrection=none-conservative`. Der Jäger lief bis
  `path=78/78`, State `0` wählte um `12:58:38.945` Ziel `59/459`, und Ziel
  `17/319` wurde anschließend als `target-changed` freigegeben. Es griff weder
  ein Versuchslimit noch ein fixes Jagdzeitlimit.

`1.1.66` speichert nach jedem erfolgreichen Vanilla-`MoveHere` Zielanker,
Pfadlänge und eine lokale akzeptierte Pfadgeneration. Eine Neuberechnung wird
nur für dieselbe lebende Slot-/Global-ID mit eigener Reservation und positiver
PCL-Prüfung vorbereitet, wenn alle folgenden Bedingungen gleichzeitig gelten:

- aktive blockierte State-1-Route, keine nur ausstehende Sichtprobe;
- mindestens sechs Tiles Chebyshev-Verschiebung vom akzeptierten Zielanker;
- lebendes Ziel und alter Anker liegen aus Sicht des Jägers in
  entgegengesetzten Richtungen (`dot < 0`);
- das lebende Ziel ist nicht weiter entfernt als der alte Anker;
- noch kein Replan für diese akzeptierte Pfadgeneration und höchstens drei
  Moving-Target-Replans in derselben Kette.

Am bereits vollständig auditierten Query-Ergebnis-Span
`[0x12FF2E,0x12FF3C)` laufen Vanillas `CALL/TEST/current-hunter-load` zuerst.
Nur wenn die Query null lieferte, wird anschließend ausschließlich ZF gelöscht,
damit Vanilla seinen State-0-Zweig nimmt. Die bestehende State-0-Query gibt
dieselbe Identität einmalig zurück und Vanilla berechnet mit `MoveHere` selbst
den neuen Pfad. Falls die Nahbereichsquery nicht mehr erreicht wird, greift beim
vollständig abgearbeiteten alten Pfad derselbe State-0-Handoff als Fallback.
Erfolgreiches `MoveHere` eröffnet eine neue Pfadgeneration; der Moving-Replan
setzt das unabhängige Nach-Schuss-Versuchsbudget nicht zurück. Es gibt weiterhin
kein fixes Zeitlimit pro Jagd und keinen direkten Ziel-, Reservations-, Pfad-,
Order-, AI-State-, Speed- oder Animationsfeldschreibzugriff.

### Ergebnis des `1.1.66`-Tests und neue Ursachenlage

Der Lauf ab `13:39:56.900` bestätigt, dass alle für `1.1.66` vorgesehenen
Hooks und Resolver einschließlich der beiden korrigierten Iced-Prüfungen
vollständig initialisiert wurden. Es gab keine ImprovedHunters-
Callbackexception und keinen harten Fehler. Die 21 allgemeinen
`not a valid .NET assembly`-Zeilen sind normales BepInEx-Debugrauschen beim
Überspringen nativer Script-Extender-DLLs und keine Modfehlermeldungen.

Die beobachtete starke Verschlechterung ist reproduzierbar und hat eine klare
Kette:

1. Sieben direkte Angriffe wurden von Vanilla angenommen (`attackResult=1`).
   Das bedeutet nur, dass der Angriffsbefehl akzeptiert wurde, nicht dass das
   Projektil traf. Bei allen sieben Beobachtungen war das Ziel noch lebend und
   hatte `targetHealth=2500`.
2. Siebenmal wurde die Nach-Schuss-Beobachtung mit eigener Reservation `2`
   vorbereitet. Der State-9-Hook validierte dieselbe lebende Identität mit PCL
   und übersprang siebenmal erfolgreich den Sitz-/State-10-Übergang
   (`State10SitPrevented=True`). Die unerwünschte Sitzpause selbst ist damit
   technisch entfernt.
3. Jeweils ungefähr 9 bis 12 ms später befand sich der Jäger in State `0`, die
   Beute aber im Reservationszustand `1`. Alle sieben einmaligen Handoffs
   scheiterten deshalb mit `identityValidation=prey-reservation-1`; es gab
   keine erfolgreiche `State-0 continuation prepared`- oder
   `target supplied`-Kette.
4. Direkt danach lehnte Vanillas normale Zielquery die bisherige Beute ab und
   nahm ein anderes Reh an. Dadurch schoss derselbe Jäger in kurzer Folge auf
   Ziel `17/319`, `61/460` und `63/462`, statt sein ursprüngliches Ziel stabil
   weiterzuverfolgen. Die frühe Sitzunterdrückung hat somit einen
   Vanilla-Zwischenzustand offengelegt, den die bisherige Revalidierung nicht
   sicher zuordnen kann.

Konkrete erste Sequenz: Ziel `17/319` wurde um `13:41:29.371` mit Reservation
`2` vorgemerkt; um `13:41:29.670` übersprang der Hook State `10`; um
`13:41:29.680` scheiterte der State-0-Handoff an Reservation `1`; um
`13:41:29.681` nahm Vanilla bereits Ziel `61/460` an. Dasselbe Muster wiederholt
sich für alle weiteren Schüsse bis `13:42:16.109`.

Der Reservationswert `1` darf nicht allein aufgrund dieses Logs pauschal als
„eigene Reservation“ akzeptiert werden. Der Fehlergrund beweist zwar, dass
Jägerzustand, lebende Slot-/Global-Identitäten, Kadaverflag und die bis dahin
geprüften Zielfelder gültig waren. Der aktuelle Marker zeigt aber noch nicht,
ob die Target-Felder zu diesem Zeitpunkt weiterhin exakt auf dieselbe Beute
zeigen oder bereits gemeinsam auf `0/0` geleert waren, und er beweist nicht,
welcher Jäger Reservation `1` besitzt. Eine allgemeine Freigabe könnte daher
Mehrjäger- oder Slot-Wiederverwendungsfehler erzeugen.

Unabhängig davon ist das Versuchslimit fehlerhaft zurückgesetzt worden:

- Beim Wechsel `17/319 → 61/460`, `61/460 → 63/462` und späteren Zielwechseln
  setzte ein erfolgreiches normales Vanilla-`MoveHere` den Zähler jeweils von
  `1/3` zurück.
- Noch eindeutiger wurde das Budget um `13:41:36.435` für
  `63/462 → 63/462` und um `13:42:15.997` für `61/460 → 61/460` zurückgesetzt.
  Damit kann dieselbe exakte Beuteidentität das Drei-Versuche-Limit derzeit
  unbegrenzt umgehen.

Die neue Pfadkorrektur war dagegen nicht die Ursache dieser schnellen
Zielwechsel. Für `63/462` war der alte Pfad um `13:42:01.700` vollständig
abgelaufen, während sich das Reh 27 Tiles vom gespeicherten Anker entfernt
hatte. Der Path-complete-Fallback erzwang dieselbe Slot-/Global-ID genau einmal;
Vanillas State-0-Query und `MoveHere` akzeptierten einen neuen Pfad zum aktuellen
Zielanker. Der anschließende Angriff trug korrekt
`attackSource=MovingTargetReplan`. Der frühere Nahbereichs-Replan wurde in
diesem Lauf nicht ausgelöst, sodass nur der Path-complete-Fallback praktisch
belegt ist. Der Fehlangriffspfad blieb ebenfalls ungetestet, weil kein
`attackResult=0` vorkam. Die State-10-Query-Hooks konnten wegen der absichtlichen
frühen State-9-Umleitung erwartungsgemäß nicht laufen.

### Verbindlicher nächster Arbeitsschritt

1. Zuerst einen stabilen Zwischenstand herstellen: Die frühe
   State-9-zu-State-0-Verhaltensmutation im nächsten Build fail-closed
   deaktivieren, solange Reservation `1` nicht sicher der ausstehenden
   Nach-Schuss-Identität zugeordnet werden kann. Das stellt vorübergehend
   Vanillas State-10-/Sitzpfad wieder her, verhindert aber die neue schnelle
   Zielwechselkette. Decoderfix, reine Diagnose und der separat funktionierende
   Path-complete-Moving-Target-Fallback können aktiv bleiben.
2. Am frühesten gemeinsamen State-0-Einstieg für genau einen ausstehenden
   Nach-Schuss-Handoff vor und nach dem Vanilla-Übergang protokollieren:
   vollständige Hunter-Target-Slot-/Global-Felder, Beute-Reservation,
   Jägerzustand, ausstehende Identität sowie alle lebenden Jäger, die dieselbe
   Beute referenzieren. Zusätzlich den Writer beziehungsweise Vanilla-Übergang
   bestimmen, der zwischen State `9` mit Reservation `2` und State `0` mit
   Reservation `1` liegt.
3. Reservation `1` nur dann eng begrenzt zulassen, wenn Laufzeitbelege zeigen,
   dass `Reservation 1 + exakte unveränderte Hunter-Target-Identität + kein
   anderer lebender Jäger auf dieser Beute` der reguläre eigene
   State-9-zu-State-0-Zwischenzustand ist. Die Ausnahme darf nur den einmaligen
   ausstehenden Nach-Schuss-Handoff betreffen, niemals die allgemeine Zielwahl,
   einen Moving-Target-Replan oder bereits geleerte Target-Felder.
4. Das Versuchslimit unabhängig korrigieren: Ein erfolgreiches normales
   `MoveHere` darf das Nach-Schuss-Budget nur bei einer tatsächlich anderen
   Slot-/Global-Zielidentität zurücksetzen. Eine erneute Annahme derselben
   Identität und ein Moving-Target-Replan eröffnen kein neues Budget. Nach dem
   dritten zulässigen Versuch bleibt Vanilla zuständig; es gibt weiterhin kein
   fixes Zeitlimit.
5. Danach zuerst einen engen Stabilitätstest ausführen: ein nicht tödlicher
   Schuss, dieselbe Identität wird genau einmal wieder über State `0` und
   Vanilla-`MoveHere` angenommen, kein Zwischenziel und kein Hüttenrückweg. Im
   selben Test darf eine erneute Annahme derselben Identität den Zähler nicht
   zurücksetzen; der dritte Versuch muss die Fortsetzung beenden.
6. Erst nach diesem Gate den Path-complete- und Nahbereichs-Moving-Replan, den
   echten `attackResult=0`-Pfad, tödlichen Treffer, Mehrjägerfall,
   Slot-Wiederverwendung und eine lange fortschreitende Route erneut testen.
   Nach vollständiger Paket-E-Abnahme folgt zwingend die bidirektionale
   Geschwindigkeits-Nachprüfung und erst danach Paket F.

### Paket-E-Abnahme

1. Bewegten Herden-/Fehlschussfall mindestens dreimal wiederholen. Überlebt das
   ursprüngliche Ziel gültig und erreichbar, verfolgt derselbe Jäger dieselbe
   Slot-/Global-ID ohne Hüttenrückweg weiter.
2. Pro Nach-Schuss-Übergang genau eine Vanilla-konforme Wiederaufnahme; kein
   Requery-/`MoveHere`-Resetloop und kein künstlicher langer Cooldown. Eine
   fortschreitende Route darf unabhängig von ihrer Gesamtdauer weiterlaufen.
3. Eine erneute Vanilla-Annahme derselben Slot-/Global-Zielidentität setzt das
   Versuchslimit nicht zurück; nach dem dritten zulässigen Versuch bleibt
   Vanilla ohne modseitige Endlosschleife zuständig.
4. Tödlicher Treffer des reservierten Ziels: unveränderter Kadaver, Pickup und
   Fleischabgabe.
5. Vollständig blockiert, aber erreichbar, mindestens zehn Sekunden: stabiler
   Pfad ohne Fehlangriff oder State `6`.
6. Aktives Ziel nachträglich unerreichbar: PCL `0` beendet die alte
   Zielidentität und jede Fortsetzung.
7. Zwei Jäger, Kartenneustart und Slot-Wiederverwendung: keine vermischten
   Sicht-, PCL-, Pfad- oder Nach-Schuss-Zustände.
8. Mod beziehungsweise `ImprovedPathfinding` aus: Distanz- und Gate-Hook ändern
   weder Register noch Flags.
9. Abschließend je einen freien und einen Blockiert-zu-sichtbar-Fall
   regressieren; keine Callbackexception oder harter Prozessabbruch.

Gate: Paket E ist fertig, wenn ein weiterhin gültiges Ziel nach einem nicht
tödlichen Schuss zuverlässig weiterverfolgt wird und alle Regressionen bestehen.

## Verpflichtende Geschwindigkeits-Nachprüfung nach Paket E

Diese Nachprüfung beginnt unmittelbar nach der vollständigen Paket-E-Abnahme
und muss vor Paket F abgeschlossen werden. Sie öffnet Paket E nicht erneut,
sondern regressiert gezielt die entfernungsabhängige Auswahl der vorhandenen
Vanilla-Geschwindigkeitsstufen.

- Die Geschwindigkeitsentscheidung muss in jedem gültigen Bewegungsupdate aus
  der aktuellen relevanten Entfernung beziehungsweise dem aktuellen dekodierten
  Restweg abgeleitet werden; eine einmal gewählte langsamere Stufe darf nicht
  als einseitig sinkender Zustand erhalten bleiben.
- Verringert sich die Entfernung zum Ziel, darf der Jäger wie vorgesehen in
  Vanillas langsamere Annäherungsstufen wechseln.
- Erhöht sich die Entfernung oder der verbleibende Weg wieder, beispielsweise
  weil ein Reh vom Jäger wegläuft, muss der Jäger erneut in die zur nun längeren
  Strecke passende schnellere Vanilla-Stufe wechseln können.
- Abnahmefall: Dasselbe lebende Ziel bewegt sich während einer Jagd erst auf den
  Jäger zu und danach deutlich von ihm weg. Logs müssen aktuelle direkte
  Distanz, dekodierte Restkosten, gewählte Vanilla-Stufe und beide
  Richtungswechsel zeigen; es darf kein Ziel-, Pfad- oder AI-State-Schreibzugriff
  des Mods hinzukommen.
- Zusätzlich einen Sichtwechsel sowie einen Hindernisumweg regressieren, damit
  die Beschleunigung weder von einem veralteten Snapshot noch von einer nur
  monoton fallenden Restwegannahme verhindert wird.

Gate: Die Nachprüfung ist fertig, wenn Verlangsamung und erneute Beschleunigung
für dieselbe Zielidentität reproduzierbar der jeweils aktuellen Entfernung oder
Reststrecke folgen. Erst danach beginnt Paket F.

## Paket F: unreservierte Kadaver

Paket F beginnt erst nach Paket E und erweitert die bestehende Zielauswahl; es
darf keine parallele Pickup-State-Machine einführen.

Der gesamte Paket-F-Pfad wird ausschließlich durch `AllowDeadTargets`
freigeschaltet. Er darf weder `ImprovedTargetSelection` noch
`ReliableHunterProjectiles` stillschweigend mitaktivieren oder voraussetzen.
Bei ausgeschalteter verbesserter Lebendziel-Auswahl darf Paket F gemeinsame
reine Bewertungshelfer wiederverwenden, aber keine lebenden Vanilla-Ziele neu
priorisieren. `ReliableHunterProjectiles` bleibt ausschließlich die davon
unabhängige Korrektur eines nachweislich ausgebliebenen Vanilla-Treffers.

### Normale Kadaverwahl

- Tote, verwertbare Beutetiere mit Reservation `0` dürfen Kandidaten sein.
- Lebende Beute und Kadaver verwenden dieselbe Fleisch-pro-Zykluskosten-
  Berechnung aus `HunterTargetSelectionFeature`; die Paket-F-Steuerung selbst
  bleibt in einer eigenen Feature-Datei.
- Aktuelle Basis: `HunterHutWorkCost=600`, `BestTargetToleranceCost=80`,
  Behandlungskosten `100`, Hase/Huhn `80`, Kamel `120` und
  `CycleCost = 600 + handling + granaryRoundTrip + approach * 2` mit
  `approach = ChebyshevDistance * 10`.
- Ein gültiger Kadaver erhält in grober und endgültiger Bewertung denselben
  kleinen Zeitbonus. `50` Kostenpunkte sind erst nach Kalibrierung als ungefähr
  fünf Sekunden zu verwenden. Der Bonus ist kein absoluter Kadavervorrang.
- PCL, Fleischmenge, Rückweg, Nahbesten-Toleranz und aktive Beutetypen bleiben
  Teil derselben Entscheidung.

### Kausaler Fremdtreffer-Transfer

Tötet das Projektil eines Jägers ein anderes aktiviertes Beutetier als sein
reserviertes Ziel, wechselt der Schütze unmittelbar auf genau diesen Kadaver,
wenn er gültig, verwertbar, PCL-erreichbar und von keinem Jäger reserviert ist.
Dieser kausale Transfer hat Vorrang vor der normalen Kostenrangfolge.

Voraussetzungen:

1. Tatsächlichen Damage-/Impact-Empfänger kausal mit Projektil und Schützen-
   Slot/Global-ID verbinden. Ein zeitlich benachbarter Unit-Delete genügt nicht.
2. Hunter-, Projektil- und Kadaveridentität sowie Reservation `0` unmittelbar
   vor dem Handoff live validieren.
3. Den frühesten Vanilla-Pfad für Kadaverreservation, Anlauf und Pickup nutzen.
   Keine eigene Bewegung oder künstliche Fleischgutschrift.
4. Die Reservation des ursprünglichen Ziels nur in der belegten Vanilla-
   Reihenfolge freigeben. Der Jäger darf nie zwei Ziele halten.
5. Ist der Kadaver fremdreserviert, ungültig oder unerreichbar, findet kein
   Transfer statt; Paket E verfolgt das ursprüngliche gültige Ziel weiter.

Abnahme:

- nur ein erreichbarer unreservierter Kadaver: Vanilla-Pickup und Abgabe;
- Kadaver gegen lebende Beute: kalibrierter Bonus, aber kein absoluter Vorrang;
- unerreichbarer oder verschwindender Kadaver: kontrollierte Neusuche;
- zwei Jäger und ein Kadaver: genau eine Reservation;
- eigener Pfeil tötet anderes reservationsfreies Tier: unmittelbarer Transfer,
  Pickup und Abgabe durch den Schützen;
- anderes Tier ist bereits fremdreserviert: kein Diebstahl, ursprüngliche Jagd
  wird fortgesetzt;
- nahezu gleichzeitige Treffer: höchstens ein Jäger übernimmt den Kadaver.

Gate: kausaler Fremdtreffer-Transfer, gemeinsame Kostenrangfolge, keine
Doppelreservation und vollständiger Vanilla-Pickup-/Abgabepfad.

## Paket D: gemeinsame Abnahmematrix

Nach E und F werden Reh, Ziege, Hase, Kamel und Huhn mit derselben Matrix
geprüft:

- frei sichtbar;
- sichtbar blockiert, aber erreichbar;
- vollständig unerreichbar und später geöffneter Zugang;
- aktives Ziel wird nach Auswahl unerreichbar;
- bewegte Beute und nicht tödlicher Schuss;
- lange Reststrecke und passende Vanilla-Geschwindigkeitsstufe;
- lebende Beute und Kadaver in gemeinsamer Rangfolge;
- Projektil, Zielkadaver, Pickup und Abgabe;
- kausaler Fremdkadavertransfer;
- mehrere Jäger ohne gemeinsame Reservation oder Zustandsvermischung;
- Jägerhütte und andere Gebäude als Sichtblocker.

## Paket C: Produktionsbereinigung

Erst nach E, F und D:

1. Verhalten aus den `*Diagnostic.cs`-Dateien in passend benannte
   Produktionsklassen verschieben, soweit es tatsächlich produktives Verhalten
   und keine untrennbare Hookdiagnose ist. Bestehende Diagnose- und
   Validierungslogik bleibt in eigenen Diagnosedateien; sie wird nicht allein
   wegen des Dateinamens in Featureklassen verschoben.
2. Stillgelegten Managed-A*-Adapter `HunterLineOfSightRecovery.cs` entfernen,
   ohne einen alten Fallback parallel zu behalten.
3. Breite alte Diagnose, ungenutzte DTOs, Hooks und Logs entfernen.
4. Gemeinsame Guards für Slot/Global-ID, Modus, Settings und Beutetypen
   vereinheitlichen.
5. `UpdateToNewDLL.md`, Changelog und Version auf den bereinigten Stand bringen.

## Optional und Multiplayer

Ein optisch deutlicherer Jagdsprint darf erst nach E, F, D und C rein
beobachtend kalibriert werden. Er blockiert keinen Pflichtabschluss und darf
keine eigene Speed-/Animationssteuerung einführen.

Echter Multiplayer bleibt fail-closed, bis Script Extender `1.50.0` die nötige
Synchronisationsgrundlage bietet. Danach Hostautorität, Snapshot-/
Reservationseigentum, Join-in-progress, Reconnect und Disconnect-Cleanup separat
prüfen.

## Dateien und Verantwortlichkeiten

| Datei | Verantwortung |
| --- | --- |
| `src/ImprovedHuntersViewModel.cs` und `ImprovedHuntersSettings.xaml` | Gespeicherte Host-Schalter, Ertragswerte, Resetwerte und Modsettings-Bindings |
| `src/HunterPclReachability.cs` | Gemeinsamer nativer PCL-Aufruf und initialer Kandidaten-PCL-Filter der Zielauswahl |
| `src/HunterActiveTargetReachability.cs` | Pfadfindungsseitige aktive PCL-Neuprüfung und pfadgebundene Erreichbarkeitssnapshots |
| `src/HunterTargetSelectionFeature.cs` | Beutecache, Kostenrangfolge, Ertragsbewertung, initiale PCL-Filterung und Zielquery-Ereignisse unter `ImprovedTargetSelection` |
| `src/HunterProjectileRecoveryFeature.cs` | Optionale korrelierte Vanilla-Fernkampfschadenswiederholung unter `ReliableHunterProjectiles`; kein Paket-F-Kadavertransfer |
| `src/HunterAnimalCompatibilityFeature.cs` | An den jeweiligen Tierartschalter gebundene Kadaver- und Kamelkompatibilität |
| `src/HunterChickenCompatibilityFeature.cs` | An `HuntChicken` gebundene Hühnerneutralisierung, Kornspeicherlimit und geladene Hühneridentitäten |
| `src/HunterNativeVisibilityProbe.cs` | Validierte Wrapper-/Kernsichtprobe |
| `src/HunterActiveTargetVisibilitySnapshot.cs` | Reservations-1-Proben, 250-ms-Nahbereich, positions- und pfadgebundene Snapshots |
| `src/HunterRemainingPathSpeedRecovery.cs` | Restwegdekodierung und Auswahl vorhandener Vanilla-Stufen |
| `src/HunterTargetSearchFallbackDiagnostic.cs` | Verborgener Kandidatenfallback, State-0-Zielquerydiagnose und atomare gemeinsame Hook-Installation |
| `src/HunterMovingTargetPathfindingDiagnostic.cs` | Unter `ImprovedPathfinding` gegateter State-1-Handoff und Moving-Target-Replan-Diagnose |
| `src/HunterVanillaPathContinuationDiagnostic.cs` | Distanz-28-Fortsetzung und Zero-Flag-only-Angriffshandoff |
| `src/HunterHutVisibilityPatch.cs` | Jägerhütte als normaler Sichtblocker |
| `src/ImprovedHuntersRuntime.cs` | Gemeinsame Initialisierung, Events, Feature-Gates und lebenszyklusübergreifende Koordination; kein erneut zusammengeführtes Featureverhalten |
| `src/HunterPostShotContinuationDiagnostic.cs` | State-9-/State-10-/Fehlangriffshandoffs, identitäts-/PCL-gesicherte einmalige Übergabe an Vanillas State-0-/`MoveHere`-Kette und Versuchslimit |
| spätere `src/HunterDeadTargetFeature.cs` | Sämtliche durch `AllowDeadTargets` aktivierten Paket-F-Pfade einschließlich normaler Kadaverwahl und kausalem Fremdkadavertransfer |
| spätere Paket-F-Trefferdiagnose | Tatsächlichen Damage-Empfänger kausal mit Projektil und Schütze verbinden; Diagnose getrennt vom Paket-F-Verhalten halten |
| `UpdateToNewDLL.md` | Vollständige Native- und Updatequelle |

## Sicherheits- und Prüfregeln

- Kein synchrones Managed-A*. Die frühere Hauptthread-Pfadsuche konnte das Spiel
  einfrieren.
- Die Live-Projektilkollision `0x9C730` niemals als künstliche Vorab-Sichtprobe
  aufrufen; sie verändert Zustand.
- Kein `KillUnit`-Fallback. Falls Projektilkompensation nötig ist, ausschließlich
  identitätsgesichertes Vanilla-`DamageUnitRanged` am echten Projektilpfad.
- Kein Überspringen von `OnUnitMovement`; dadurch desynchronisieren Unit-Tile,
  Kartenbelegung, AI und Darstellung.
- Mod-/Option-Aus, Kartenwechsel und nicht unterstützte Modi dürfen keine
  aktiven Zustände hinterlassen.
- Logs besitzen Millisekunden-Zeitstempel, stabile Identitäten, begrenzte
  Wiederholung und überprüfbare Invarianten.
- Vor einem Build: statische Code-/Resolverprüfung, relevante Tests und CRLF-
  Kontrolle. Danach genau einmal `ImprovedHunters\build.bat /nopause` direkt
  aus PowerShell ausführen.

## Arbeitsanweisung für einen neuen Chat

Unmittelbarer Übernahmeauftrag ist die Stabilisierung von Paket E. `1.1.66`
gilt als fehlgeschlagener Ingame-Test und darf nicht einfach erneut durch die
breite Abnahmematrix geschickt werden. Der nächste Chat soll die folgenden
Punkte implementieren, statisch prüfen, anschließend genau einmal bauen und
zuerst nur mit dem beschriebenen engen Einzelzielfall testen.

1. Diesen Plan vollständig lesen. Historie nur bei Bedarf im Archiv und tiefe
   Native-Details in `UpdateToNewDLL.md` nachschlagen.
2. Paket A und B nicht erneut öffnen, sofern keine konkrete Regression belegt
   ist.
3. In Paket E direkt beim Abschnitt „Ergebnis des `1.1.66`-Tests und neue
   Ursachenlage“ beginnen. Nicht erneut Sichtlatenz, Restweg, F4 oder die
   frühere Ursache der State-10-Nullquery untersuchen.
4. Vor weiteren breiten Tests die frühe State-9-Umleitung stabilisieren:
   zunächst fail-closed deaktivieren, Reservation `1` am State-0-Einstieg
   eigentumssicher diagnostizieren und nur bei vollständigem Identitätsbeleg
   als einmaligen Nach-Schuss-Zwischenzustand zulassen. Parallel darf das
   Versuchslimit nur bei einer tatsächlich anderen Zielidentität zurückgesetzt
   werden. Die direkten Codeeinstiege liegen in
   `src/HunterPostShotContinuationDiagnostic.cs` bei
   `TrySkipStateTenSitTransition`, `TryPrepareStateZeroContinuation`,
   `TryValidateCandidate` und `ResetAttemptBudgetForIndependentMove`.
5. Danach den engen Einzelzieltest und erst anschließend Nach-Schuss-/
   Fehlangriffshandoff, beide Moving-Target-Replans, Drei-Versuche-Limit, langen
   fortschreitenden Weg und die vollständige Paket-E-Matrix ausführen.
6. Nach vollständiger E-Abnahme zwingend die bidirektionale
   Geschwindigkeits-Nachprüfung abschließen; erst danach Paket F beginnen und
   anschließend `D → C` bearbeiten.
7. Bei jeder neuen Hypothese den kleinsten stabilen Vanilla-Übergang erhalten.
   Diagnose, Korrektur und Vanilla-Aufruf bleiben getrennt. Insbesondere
   Reservation `1` nicht allein aufgrund des bisherigen Logs allgemein
   akzeptieren und den belegten Path-complete-Fallback nicht zusammen mit der
   State-9-Stabilisierung unnötig neu entwerfen.
