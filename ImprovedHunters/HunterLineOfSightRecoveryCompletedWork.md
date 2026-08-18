# ImprovedHunters: abgeschlossene Arbeit und historische Erkenntnisse

Stand: `2026-08-18`

Diese Datei archiviert abgeschlossene Pakete, bestätigte Teilbausteine und
wichtige historische Fehlerursachen. Sie enthält **keine aktuelle
Arbeitsanweisung**. Der verbindliche Stand steht in
[HunterLineOfSightRecoveryPlan.md](HunterLineOfSightRecoveryPlan.md); vollständige
Native- und Updateangaben stehen in [UpdateToNewDLL.md](UpdateToNewDLL.md).

Auditbasis:

- Steam Build ID: `24651686`
- `CrusaderDE.dll`: `3.450.880` Byte
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`
- letzter hier berücksichtigter Modstand: `1.1.61`

## Paket A: PCL-Erreichbarkeit – abgeschlossen

Ziel war, vollständig unerreichbare Beute vor der Kostenrangfolge auszuscheiden
und ein während des Anmarschs unerreichbar gewordenes Ziel kontrolliert zu
verwerfen.

Bestätigtes Verhalten:

- Nahe, vollständig eingeschlossene Rehe verdrängten ein weiter entferntes,
  erreichbares Reh nicht.
- Waren alle Tiere unerreichbar, blieb der Jäger kontrolliert an der Hütte und
  „Kein Wild“ erschien wie erwartet.
- Nach Öffnen eines Zugangs wurde das Tier nach Ablauf des kurzen PCL-Caches
  wieder zugelassen.
- Wurde ein aktives Ziel nach der Auswahl eingeschlossen, invalidierte der Mod
  ausschließlich die gespeicherte Zielidentität und passende Reservation.
  Vanilla suchte neu; der Jäger wurde nicht aufgelöst.

Technisch bestätigt:

- `GetNextReachablePCLToDestinationForPlayer` verwendet spielerabhängige Path
  Connection Layers einschließlich dynamischer Tore.
- PCL `0` ist ein belastbarer Negativbeweis. Ein positiver Wert beweist keinen
  Detailpfad; deshalb bleibt `MoveHere` autoritativ.
- Die Kalibrierung ergab `4/4` positive PCL-Ergebnisse mit `MoveHere=1` und
  `6/6` Nullergebnisse mit `MoveHere=0`, ohne Fehlkorrelation.
- Der Kandidatencache gilt höchstens eine Sekunde und nur bei identischen
  Hunter-/Zielidentitäten, Spieler-, Modus-, Quell- und Ziel-PCL-Werten.
- API- oder Eingabefehler sind fail-open. Ein technischer Fehler entfernt kein
  möglicherweise erreichbares Tier.

Historische Ursache des aktiven-Ziel-Fehlers: In `1.1.44` lief ein Jäger bis an
eine neu errichtete Mauer, weil sein bereits gewähltes Ziel erst nach der
Auswahl unerreichbar wurde. `1.1.45` ergänzte die aktive PCL-Neuprüfung und
behob diesen Fall.

## Paket B: normale Schuss- und Pickupkette – abgeschlossen

Normale und hindernisgestützte Jagd haben die vollständige Vanilla-Kette
mehrfach durchlaufen:

`Angriff → echtes Projektil → Zielkadaver → Pickup → Fleischabgabe`.

Ein allgemeiner Pickup-Fix ist daher weder nötig noch zulässig.

In zwei Herdensituationen blieb das reservierte Ziel nach einem positiven
Angriff am Leben. Im `1.1.61`-Lauf war das Ziel `17/319` später erneut auswählbar,
während wahrscheinlich ein anderes Reh starb. Daraus folgen zwei **offene**, in
anderen Paketen behandelte Anforderungen:

- Paket E muss dasselbe lebende Ziel nach einem nicht tödlichen Schuss
  weiterverfolgen.
- Paket F soll einen tatsächlich vom eigenen Pfeil getöteten,
  reservationsfreien Fremdkadaver übernehmen.

Diese Sonderfälle widerlegen nicht die bestätigte normale Paket-B-Pickupkette.

## Abgeschlossene Grundlagen vor Paket E

| Stand | Dauerhafte Erkenntnis beziehungsweise Ergebnis |
| --- | --- |
| `1.1.31` | Synchrones Managed-A* aus der Jagdkorrektur entfernt; es konnte den Hauptthread einfrieren. |
| `1.1.36` | Begrenzter Fallback für gültige verborgene Beute; Vanilla setzt Ziel, Reservation, Pfad und AI-State selbst. |
| `1.1.39/1.1.40` | Blockierten Vanilla-Pfad bei Tile-Distanz `<=28` über Vanillas Distanz-29-Stufe fortsetzen; Zustände pro Jäger statt globaler Zwei-Ziel-Grenze. |
| `1.1.41` | Jägerhütte verwendet normalen Gebäudehöhenfall und blockiert Sicht wie andere Gebäude. |
| `1.1.43/1.1.44` | Native PCL-Semantik kalibriert und als produktiver Kandidatenvorfilter eingebaut. |
| `1.1.45` | Neu unerreichbares aktives Ziel wird identitätsgesichert an Vanillas Neusuche übergeben. |

## Paket E: abgeschlossene Teilbausteine und Herleitung

Paket E ist insgesamt noch offen. Die folgenden Teilprobleme sind jedoch
implementiert und müssen nicht erneut von Grund auf analysiert werden.

### Restweg und Vanilla-Geschwindigkeit (`1.1.46/1.1.47`)

- `0x79C0` liefert `abs(dx)+abs(dy)` und berücksichtigt keinen Hindernisumweg.
- Vanillas State-1-Stufen verwenden die Geschwindigkeitswerte `1`, `2`, `3`,
  `4`, `6`, `8`, `10` für die Distanzbereiche `>40`, `37..40`, `35..36`,
  `33..34`, `31..32`, `29..30`, `<=28`.
- `MoveHere` schreibt Pfadstatus `+0xF2=2`, Fortschritt `+0xF6` und die
  `UInt32`-Pfadlänge nach `+0xF8`.
- Der gepackte Pfad enthält Vierbit-Richtungen. Die vergleichbare Restmetrik ist
  `orthogonal + 2 * diagonal`.
- Der Pfadpuffer-Offset `0xB4FE78` ist managerrelativ, kein Modul-RVA. Der
  anfängliche falsche Basisbezug wurde in `1.1.47` korrigiert.
- Der Hook wählt nur eine vorhandene Vanilla-Stufe; Vanilla schreibt Speed und
  Animation. Positive Sicht erhält die langsame Endannäherung.

### Nahbereichs-Neusuche und sichere Hooks (`1.1.48–1.1.51`)

- Vanillas Welt-Nahbereichsvergleich bei `0x130019` kann vor dem
  Geschwindigkeitspfad eine neue Zielquery auslösen.
- Das eigene Ziel trägt während des Auftrags Reservation `2`; die allgemeine
  Rangfolge akzeptiert unreservierte Beute und kann deshalb `best=none` liefern.
- Der erste Hookversuch ab `0x130022` war unsicher: Der 14-Byte-Detour ersetzte
  tatsächlich 18 Byte, während ein bestehender Branch in das Innere dieser
  Spanne sprang. Der harte Prozessabbruch führte zur heute verbindlichen
  vollständigen Eingangsbranchprüfung.
- Der sichere Vergleichshook beginnt bei `0x130019`, deckt
  `[0x130019,0x130028)` ab und lässt das Sprungziel `0x13002D` außerhalb.
- Wiederholte Nahbereichsquerys setzten `MoveHere`, Pfadfortschritt und
  Animationsframe immer wieder zurück. Die sichtbare Sitz-/Warteanimation war
  ein Order-Resetloop, kein Fehler der normalen Speed-/Animationsstufe.
- Seit `1.1.51` hält ausschließlich die bestehende Vanilla-Verzweigung den
  aktiven Pfad; es gibt keinen eigenen Move oder AI-State-Writer.

### Bidirektionale Sichtentscheidung (`1.1.52`)

- Der Wrapper `0xA06F0` gibt ein positives erstes gerichtetes Kernergebnis
  sofort zurück. An Diagonal- und Eckgeometrie kann deshalb nur eine Richtung
  positiv sein, obwohl die Pfeilbahn blockiert bleibt.
- Im Nahbereich wird ein positives Wrapperresultat mit dem Kern `0x9E350` in
  beiden Richtungen verglichen. Nur zwei positive Richtungen erlauben den
  Angriffshandoff; ein Richtungswiderspruch setzt den Pfad fort.
- Die echte Projektilkollision `0x9C730` verändert Zustand und ist keine
  zulässige Vorab-Sichtprüfung.

### Aktive Snapshots und Pfadgeneration (`1.1.55–1.1.59`)

- `1.1.55` trennte den einsekündigen allgemeinen PCL-Auswahlcache vom aktiven,
  zwei Sekunden lesbaren Ziel-Snapshot. Dadurch konnte eine Cachegrenze einen
  gültigen blockierten Pfad nicht mehr in Vanillas Fehlangriff freigeben.
- `1.1.56` machte den Tile-Distanz-Hook `0x1300EA` zur autoritativen
  Blockiert-/Angriffsentscheidung.
- `1.1.57` entfernte `+0xF4` aus der Snapshotidentität; das Feld ist eine
  Locomotion-Unterstufe und wechselt während desselben Pfades.
- `1.1.58` ersetzte die unzuverlässige Fortschritts-Rücksprungheuristik durch
  eine explizite Pfadgeneration, die nur nach akzeptiertem Vanilla-`MoveHere`
  fortschreitet.
- `1.1.59` behält einen Tracker über kurze, nicht erfassbare Scanübergänge und
  entfernt ihn erst nach zwei Sekunden ohne gültigen Scan oder Hookzugriff.

### Sichtlatenz und Gate-Handoff (`1.1.60/1.1.61`)

Der `1.1.60`-Test trennte zwei Ursachen:

- Freie Sicht wurde nativ rechtzeitig erkannt, aber das nachgeschaltete
  `+0xF4`-/Pfadzustandstor verzögerte den Angriff bis zu ungefähr `1,2 s`.
- Auf langen blockierten Wegen verhinderte Reservationsphase `1` neue Proben;
  nach Snapshotablauf fiel der Code in einen erfolglosen direkten Angriff und
  State `6`.

`1.1.61` korrigierte beides:

- verhaltensneutrale Proben in Reservationsphase `1`;
- `250 ms` Sollintervall bis Tile-Distanz `30`, sonst eine Sekunde;
- positive Snapshots an beide exakten Unit-Tiles gebunden;
- bekannte Blockierung bleibt bei ausstehender Aktualisierung konservativ;
- vollständig validierter Hook `[0x130110,0x130124)` löscht nur beim frischen,
  positionsgleichen positiven Snapshot das Zero Flag des Angriffstors.

Der anschließende Test bestätigte freie Angriffe innerhalb `20–26 ms` nach der
letzten positiven Probe sowie bewegte Blockiert-zu-sichtbar-Angriffe innerhalb
`1–6 ms`, auch bei nicht-null `pathFieldF4`. Ein Pfad der Länge `79` lief fast
neun Sekunden ohne vorzeitigen Fehlangriff bis zur Sichtung.

Damit sind Restweg, blockierte Pfadfortsetzung und Sicht-/Gate-Reaktion
abgeschlossen. Offen bleibt ausschließlich die im aktuellen Plan beschriebene
Nach-Schuss-Weiterverfolgung und die vollständige Paket-E-Regressionsmatrix.

## Entfernte oder verworfene Ansätze

Diese Ansätze dürfen nicht wieder eingeführt werden:

- synchrones Managed-A* oder eine eigene Detailpfadsuche;
- eigene Recovery-Moves, Querylocks oder Jagd-AI-State-Machines;
- direkte Speed-/Animationsschreiboperationen;
- globale Limits für Zielidentitäten;
- ein `KillUnit`-Fallback oder künstliche Kadaver;
- Aufruf der Live-Projektilkollision als Sicht-Vorabprüfung;
- Überspringen von `OnUnitMovement` zum Einfrieren von Rehen; dies
  desynchronisierte Tile, Belegung, Darstellung und AI;
- Hookfreigabe allein anhand der Startadresse oder vermeintlicher 14 Byte ohne
  dekodierte Gesamtspanne und Prüfung eingehender Ziele;
- Verlängerung oder Änderung des 30-Sekunden-Cooldowns als Symptomfix;
- parallele alte und neue Implementierungen als unaufgeforderter Fallback.

## Historische Diagnosemarker

Die folgenden Marker sind nur bei Regressionen hilfreich:

- `blocked-wrapper-both-directions`
- `blocked-directional-disagreement`
- `visible-attack-handoff`
- `visibility-position-changed-refresh-pending`
- `stale-blocked-refresh-pending`
- `accepted Vanilla path generation advanced`
- `released Vanilla Hunter path continuation`
- `state-1 direct-attack observation`

Alte Pflichttestlisten und versionsspezifische Übergabeempfehlungen vor
`1.1.61` sind durch den aktuellen Plan ersetzt. Die vollständige
Versionschronik bleibt zusätzlich im `SerpChangelog` von `info.json` erhalten.
