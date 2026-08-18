# ImprovedHunters: aktueller Entwicklungsplan

Stand: `2026-08-18`

Aktueller Quell- und Teststand: `1.1.61` auf Steam Build `24651686`.
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
- Paket E ist aktiv. Restweg-Geschwindigkeit, blockierte Pfadfortsetzung und der
  schnelle Sicht-/Angriffshandoff funktionieren. Offen ist der Abbruch nach
  einem Schuss, wenn das ursprüngliche Ziel weiterlebt.
- Danach folgen Paket F für unreservierte Kadaver, Paket D als gemeinsame
  Beutetyp-/Mehrjägermatrix und zuletzt Paket C für die Produktionsbereinigung.
- Ein sichtbarer Jagdsprint ist optional. Echter Multiplayer bleibt bis Script
  Extender `1.50.0` deaktiviert.

Verbindliche Reihenfolge: `E → F → D → C`.

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

Unterstützte Zieltypen sind Reh, Ziege, Hase, Kamel, Huhn und perspektivisch
Kuh. `HuntCow` existiert in den Einstellungen, während die Runtime Kühe derzeit
noch ausschließt; Paket D muss diese Entscheidung vereinheitlichen.

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

## Paket E: Restweg, Sichtübergabe und Nach-Schuss-Weiterverfolgung

### Bereits funktionierender Stand

#### Erreichbarkeit und Zielannahme

- `HunterPclReachability` prüft Kandidaten vor der Kostenrangfolge und nochmals
  am konkreten Handoff. Der Cache gilt höchstens eine Sekunde und nur bei
  identischen Eingaben.
- Ein positives PCL lässt Vanilla planen. Liefert Vanillas vollständige Suche
  trotz eines gültigen verborgenen Kandidaten kein Ziel, stellt der begrenzte
  Fallback genau diesen Kandidaten bereit. Vanilla ruft anschließend selbst
  `MoveHere` auf.
- Ein aktives Ziel mit neuem PCL `0` wird identitätsgesichert invalidiert;
  Vanilla sucht neu. Technische PCL-Fehler sind fail-open.

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

### Offener Paket-E-Fehler

Nach einem angenommenen, aber für das reservierte Ziel nicht tödlichen Schuss
beendet Vanilla die Jagd, obwohl dasselbe Ziel weiterlebt, erlaubt und
PCL-erreichbar ist. Die sichtbare Reaktion und der Angriffshandoff waren bereits
korrekt; auch eine zu große Entfernung ist bei fünf Tiles ausgeschlossen.

Der Log zeigt die Folge `attackResult=1 → State 10 → neue Zielsuche → kein
MoveHere → State 6`, lokalisiert aber noch nicht:

- welcher Branch State `10` verlässt,
- weshalb der erlaubte Fallbackkandidat bei `best=none` nicht übernommen wird,
- welcher konkrete State-6-Writer in diesem Lauf ausgeführt wird.

Ob der Pfeil nichts oder ein anderes Tier trifft, ändert die Paket-E-Anforderung
nicht: Solange das ursprüngliche Ziel gültig weiterlebt, muss derselbe Jäger es
weiterverfolgen. Die Übernahme eines versehentlich getöteten anderen Tiers ist
separat Paket F.

### Verbindlicher nächster Arbeitsschritt

1. Eine separat entfernbare, rein beobachtende Nach-Schuss-Diagnose ergänzen.
   Ab `attackResult=1` müssen Projektilerzeugung und -ende, Hunter-State und
   -Order, Zielslot/Global-ID, Ziel-Alive-State und Gesundheit, Reservation,
   PCL, Zielquery-Rückgabe, Fallbackkandidat, `MoveHere`-Aufruf/-Ergebnis und
   jeder State-6-Writer korreliert werden.
2. Nativ den Pfad von State `10` zur neuen Query analysieren. Insbesondere den
   Grund für `allowed=True, fallback=True, best=none`, den ausbleibenden
   `MoveHere` und den tatsächlich ausgeführten State-6-Writer bestimmen.
3. Neue Diagnosehooks vor Installation vollständig validieren. Während der
   Ursachenanalyse keine Register-/Flagmutation, keinen Schaden, Target-Transfer,
   Move oder AI-State-Write ergänzen.
4. Danach den kleinsten vorhandenen Vanilla-Wiederaufnahmepfad korrigieren. Ist
   das ursprüngliche Ziel mit derselben Identität noch lebend, aktiviert,
   eigenreserviert und PCL-erreichbar, muss Vanilla es unmittelbar erneut
   verfolgen, statt State `6` zu wählen.
5. Ist das Ziel tot, ungültig, fremdreserviert oder PCL-getrennt, darf die
   Korrektur nicht greifen. Cooldown, Projektilkompensation und Pickup sind keine
   Ersatzkorrekturen.

### Paket-E-Abnahme

1. Bewegten Herden-/Fehlschussfall mindestens dreimal wiederholen. Überlebt das
   ursprüngliche Ziel gültig und erreichbar, verfolgt derselbe Jäger dieselbe
   Slot-/Global-ID ohne Hüttenrückweg weiter.
2. Pro Nach-Schuss-Übergang genau eine Vanilla-konforme Wiederaufnahme; kein
   Requery-/`MoveHere`-Resetloop und kein künstlicher langer Cooldown.
3. Tödlicher Treffer des reservierten Ziels: unveränderter Kadaver, Pickup und
   Fleischabgabe.
4. Vollständig blockiert, aber erreichbar, mindestens zehn Sekunden: stabiler
   Pfad ohne Fehlangriff oder State `6`.
5. Aktives Ziel nachträglich unerreichbar: PCL `0` beendet die alte
   Zielidentität und jede Fortsetzung.
6. Zwei Jäger, Kartenneustart und Slot-Wiederverwendung: keine vermischten
   Sicht-, PCL-, Pfad- oder Nach-Schuss-Zustände.
7. Mod beziehungsweise `ImprovedPathfinding` aus: Distanz- und Gate-Hook ändern
   weder Register noch Flags.
8. Abschließend je einen freien und einen Blockiert-zu-sichtbar-Fall
   regressieren; keine Callbackexception oder harter Prozessabbruch.

Gate: Paket E ist fertig, wenn ein weiterhin gültiges Ziel nach einem nicht
tödlichen Schuss zuverlässig weiterverfolgt wird und alle Regressionen bestehen.

## Paket F: unreservierte Kadaver

Paket F beginnt erst nach Paket E und erweitert die bestehende Zielauswahl; es
darf keine parallele Pickup-State-Machine einführen.

### Normale Kadaverwahl

- Tote, verwertbare Beutetiere mit Reservation `0` dürfen Kandidaten sein.
- Lebende Beute und Kadaver verwenden dieselbe Fleisch-pro-Zykluskosten-
  Rangfolge aus `ImprovedHuntersRuntime`.
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

Nach E und F werden Reh, Ziege, Hase, Kamel, Huhn und die bewusst geklärte Kuh
mit derselben Matrix geprüft:

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
   Produktionsklassen verschieben; temporäre Logs getrennt halten.
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
| `src/HunterPclReachability.cs` | Kandidaten- und aktives-Ziel-PCL mit begrenzten Caches |
| `src/HunterNativeVisibilityProbe.cs` | Validierte Wrapper-/Kernsichtprobe |
| `src/HunterActiveTargetVisibilitySnapshot.cs` | Reservations-1-Proben, 250-ms-Nahbereich, positions- und pfadgebundene Snapshots |
| `src/HunterRemainingPathSpeedRecovery.cs` | Restwegdekodierung und Auswahl vorhandener Vanilla-Stufen |
| `src/HunterTargetSearchFallbackDiagnostic.cs` | Verborgener Kandidatenfallback und aktuelle Zielquerydiagnose; in C trennen |
| `src/HunterVanillaPathContinuationDiagnostic.cs` | Distanz-28-Fortsetzung und Zero-Flag-only-Angriffshandoff; in C trennen |
| `src/HunterHutVisibilityPatch.cs` | Jägerhütte als normaler Sichtblocker |
| `src/ImprovedHuntersRuntime.cs` | Events, Eligibility, Rangfolge, Handoffs, Reservationen und Projektilkorrelation |
| neue Nach-Schuss-Diagnose | Nächster Paket-E-Schritt: State 10, Requery, ausbleibender `MoveHere` und State-6-Writer |
| spätere Paket-F-Trefferdiagnose | Tatsächlichen Damage-Empfänger kausal mit Projektil und Schütze verbinden |
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

1. Diesen Plan vollständig lesen. Historie nur bei Bedarf im Archiv und tiefe
   Native-Details in `UpdateToNewDLL.md` nachschlagen.
2. Paket A und B nicht erneut öffnen, sofern keine konkrete Regression belegt
   ist.
3. In Paket E direkt beim Abschnitt „Verbindlicher nächster Arbeitsschritt“
   beginnen. Nicht erneut Sichtlatenz, Restweg oder F4 als Hauptursache verfolgen;
   diese Übergänge sind mit `1.1.61` bestätigt.
4. Zuerst den Nach-Schuss-Pfad `attackResult=1 → State 10 → Query → kein
   MoveHere → State 6` beobachtend lokalisieren. Das weiterhin lebende Ziel
   `17/319` ist der maßgebliche Beleg.
5. Anschließend den kleinsten Vanilla-Wiederaufnahmepfad für genau dieselbe
   gültige Zielidentität korrigieren und die Paket-E-Matrix ausführen.
6. Erst nach vollständiger E-Abnahme Paket F beginnen; danach `D → C`.
7. Bei jeder neuen Hypothese den kleinsten stabilen Vanilla-Übergang erhalten.
   Diagnose, Korrektur und Vanilla-Aufruf bleiben getrennt.
