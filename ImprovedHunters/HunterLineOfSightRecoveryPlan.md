# ImprovedHunters: Übergabeplan für Jagd, Erreichbarkeit und Beuteauswahl

Stand: `2026-08-18`

Aktueller implementierter Quellstand: `1.1.60`. Zuletzt ingame geprüft ist
`1.1.59`: Die begrenzte Tracker-Retention funktioniert, aber freie Sicht wird
teilweise erst sehr nah positiv und eine positive Freigabe wird durch Vanillas
nachgeschaltetes `+0xF4`-Bewegungstor weiter verzögert. `1.1.60` trennt beide
Ursachen mit verhaltensneutralen Geometrie-, Kernrichtungs- und Gate-Logs;
Paket E ist bis zur erneuten Ingame-Abnahme ausdrücklich **nicht abgenommen**.

## Übergabe in Kurzform

Die Pakete A und B sind abgeschlossen. Paket E besitzt im Quellstand `1.1.60`
die Teilbausteine für Restweg, Vanilla-Geschwindigkeitsstufen, PCL und den
aktiven Sicht-Snapshot am Tile-Angriffspfad. Der `1.1.57`-Test bestätigte die
frühe Freigabe bis Tile-Distanz `28`, widerlegte aber die verbliebene
Fortschritts-Rücksprungheuristik. Ursache, Korrektur und Pflichttests stehen im
Paket-E-Abschnitt ab „`1.1.60`-Diagnose nach dem `1.1.59`-Test“.

Verbindliche Reihenfolge:

1. Paket E separat entwickeln und abnehmen.
2. Paket F für unreservierte Kadaver separat entwickeln und abnehmen.
3. Paket D als gemeinsame Beutetyp-, Lebend-/Kadaver- und Mehrfachjägermatrix
   durchführen.
4. Erst danach Paket C ausführen und alle abgenommenen Bausteine in die
   endgültige Produktionsstruktur überführen.
5. Erst wenn E, F, D und C vollständig funktionieren, kann der sichtbare
   Jagdsprint als nicht blockierendes Optionalpaket untersucht werden.
6. Multiplayer bleibt bis Script Extender `1.50.0` ein deaktivierter Chore.

Die getrennte Entwicklung ist absichtlich gewählt: Bewegung und Kadaverwahl
sollen jeweils beobachtbar, leicht entfernbar und unabhängig testbar sein,
bevor eine Produktionsbereinigung ihre Struktur festlegt.

Grundarchitektur, die nicht erneut ersetzt werden soll:

> PCL `0` verwirft früh; PCL positiv lässt Vanilla planen; ein angenommener
> Vanilla-Pfad wird im blockierten Nahbereich fortgesetzt; bei freier Sicht
> übernimmt Vanilla Angriff, Kadaver, Einsammeln und Fleischabgabe.

## Fachliches Endziel

Ein Jäger soll jede aktivierte Beuteart auch hinter Gebäuden, Mauern, Toren,
Türmen oder Geländeanstiegen berücksichtigen, wenn ein gültiger Fußweg besteht.
Er soll Vanillas Weg um das Hindernis weiterlaufen, an der ersten geeigneten
Position mit freier Sichtlinie angreifen und danach Vanillas normalen
Kadaver-, Einsammel- und Fleischabgabeprozess verwenden.

Vollständig unerreichbare Beute wird vor der Kostenrangfolge verworfen. Sie
darf nähere, aber erreichbare Beute nicht verdrängen. Die Erreichbarkeit wird
regelmäßig erneut geprüft, damit geöffnete Zugänge Tiere wieder zulassen und
ein während des Anmarschs blockiertes Ziel rechtzeitig gewechselt wird.

Bereits tote, noch verwertbare und unreservierte Beutetiere sollen an derselben
kostenbasierten Rangfolge wie lebende Tiere teilnehmen. Sie erhalten einen
kleinen Bonus von ungefähr fünf Sekunden, weil Anschleichen und Schuss
entfallen. Der Bonus ist kein absoluter Kadavervorrang: Fleischmenge,
Annäherungsweg, Rückweg und Erreichbarkeit bleiben maßgeblich.

Die Jägerhütte besitzt keine Sichtlinienausnahme mehr. Sie blockiert wie andere
Gebäude, weil ein bereits gestarteter Pfeil sonst physisch in der Hütte hängen
bleiben kann.

Zieltypen sind Reh, Ziege, Hase, Kamel, Huhn und Kuh, soweit der jeweilige
`Hunt...`-Schalter aktiv und der Typ im Runtime-Eligibility-Pfad freigeschaltet
ist. Für Kühe besteht noch ein dokumentierter Widerspruch.

## Status und bereits getroffene Entscheidungen

| Bereich | Status | Entscheidung beziehungsweise Folgearbeit |
| --- | --- | --- |
| Verborgene erreichbare Beute | Implementiert und ingame bestätigt | Als stabile Grundlage erhalten |
| PCL-Vorfilter für unerreichbare Beute | Seit `1.1.44` produktiv und bestätigt | Keine eigene Detailpfadsuche ergänzen |
| Neu unerreichbares aktives Ziel | Seit `1.1.45` implementiert und in den Logs bestätigt | Vanilla selbst neu suchen lassen |
| Distanz-28-Pfadfortsetzung | `1.1.56` bestätigte ingame die Fortsetzung des gültigen Vanilla-Pfades ohne vorzeitigen State-6-Abbruch; Paket E insgesamt bleibt offen | Alle sieben Paket-E-Tests und die maschinelle Logabnahme durchführen; keine eigenen Moves oder AI-States |
| Freie Sicht und echter Angriff | `1.1.59` bestätigt stabile Tracker-Retention; positiv sichtbare Ziele werden jedoch erst nach Vanillas `+0xF4`-Tor direkt angegriffen, während manche optisch freien Annäherungen bis in kurze Distanz Wrapper `0` liefern | Mit `1.1.60` Sichtgeometrie, beide Kernrichtungen und Gate-Aufschub getrennt messen; erst danach positionsgebundene Aktualisierung und gegebenenfalls validierten Gate-Handoff implementieren |
| Jägerhütte als Sichtblocker | Seit `1.1.41` aktiv und ingame plausibel | In Paket D kurz regressieren |
| Schuss, Kadaver, Pickup und Abgabe | Mehrfach vollständig beobachtet | Paket B abgeschlossen; kein Pickup-Fix |
| Langsames Schleichen auf langem Umweg | Restweg und Vanilla-Geschwindigkeitswahl sind bestätigt; `1.1.50` belegt wiederholte `Query -> State 0 -> MoveHere`-Resets als Ursache der Sitz-/Warteanimation | Normale Vanilla-Locomotion erhalten; die noch offene State-1-Angriffslücke beheben und danach Geschwindigkeit plus Animation regressieren |
| Unreservierte vorhandene Kadaver | Noch nicht unterstützt | Danach separat als Paket F entwickeln |
| Alle sechs Beutearten | Gemeinsame Architektur, noch keine vollständige Typmatrix | Paket D nach E und F |
| Produktionsbereinigung | Bewusst zurückgestellt | Paket C zuletzt |
| Sichtbarer Sprint zum lebenden Jagdziel | Optional; State `1` verwendet bereits die schnellste bestätigte Jagdstufe, Kadaverlauf nutzt einen anderen State-2-Locomotionpfad | Erst nach allen Pflichtpaketen rein beobachtend kalibrieren; blockiert keinen Abschluss |
| Echter Multiplayer | Fail-closed | Eigener Chore ab Script Extender `1.50.0` |

### Paket A: abgeschlossen

Folgende Fälle sind durch die Spieltests und die anschließende Logprüfung
abgedeckt:

- Mehrere nahe, vollständig eingeschlossene Rehe und ein weiter entferntes,
  erreichbares Reh waren gleichzeitig vorhanden. Der Jäger wählte sofort das
  erreichbare Reh; die eingeschlossenen Tiere hielten ihn nicht an der Hütte.
- Nachdem alle Tiere unerreichbar waren, blieb der Jäger kontrolliert an der
  Hütte und „Kein Wild“ trat wie erwartet auf.
- Nach Öffnung eines Zugangs wurde das Tier nach Ablauf des kurzen PCL-Caches
  beim nächsten Vanilla-Suchlauf innerhalb weniger Sekunden wieder gewählt.
- Ein bereits ausgewähltes Tier wurde während des Anmarschs vollständig
  eingeschlossen. `1.1.45` erkannte das aktive Ziel über den persistenten Scan,
  invalidierte nur die gespeicherte Ziel-Global-ID, gab die Reservation
  identitätsgesichert frei und ließ Vanilla regulär neu suchen. Die geprüften
  Kartenstarts zeigten Zielwechsel ohne Jägerauflösung.

Der vorherige Fehler ist damit erklärt und korrigiert: In `1.1.44` lief Jäger
`1/370` noch bis zur neuen Mauer, weil sein Ziel Reh `16/294` nach der Auswahl
unerreichbar geworden war. Der Pfad erreichte `47/50`; anschließend wurde der
Jäger beim fehlgeschlagenen Auftrag gelöscht. Ein neuer Jäger sah dasselbe Reh
danach mit Ziel-PCL `10` und PCL-Ergebnis `0`.

Paket A gilt als bestanden. Die Fälle bleiben Regressionen für Paket D, aber
blockieren Paket E nicht mehr.

### Paket B: abgeschlossen

Normale und hindernisgestützte Jagd haben Schuss, echten Kadaver, Einsammeln
und Fleischabgabe mehrfach vollständig durchlaufen.

Ein einzelner abweichender Herdentest belegt keinen Pickup-Fehler:

- Jäger `1/369` speicherte Reh `10/287` als Ziel und erzeugte Pfeil `25/440`.
- Beim Projectile-Delete bestand dieses gespeicherte Ziel die Vorprüfung auf
  Slot, Global-ID, `IsAlive` und Gesundheit über `0`.
- Reh `10/287` wurde später mit unveränderter Identität weiter als lebend
  geprüft; es gab keinen Delete oder Recreate seines Slots.
- Falls der Pfeil sichtbar ein Reh tötete, traf er daher sehr wahrscheinlich
  ein anderes bewegtes Herdentier. Der tatsächliche Trefferempfänger wurde in
  diesem Lauf nicht protokolliert und ist nachträglich nicht bestimmbar.

Entscheidung: kein Pickup-Fix und kein weiterer Pflicht-Test. Nur bei erneutem
Auftreten wird eine rein beobachtende Diagnose in einer eigenen Datei ergänzt.
Sie muss tatsächlichen Trefferempfänger, Vorher-/Nachherzustand und spätere
Kadaverabholung verfolgen, ohne Vanilla-Schaden, Projektilkompensation oder
Zielwahl zu beeinflussen.

## Aktuelle produktive Lösungskette

1. `OnHunterQueryTarget` validiert Jäger und Kandidat anhand stabiler Slot- und
   Global-IDs sowie Beutetyp, Besitzer, Alive-State und aktiver Einstellungen.
2. `HunterPclReachability` prüft vor der Kostenrangfolge, ob Jäger- und
   Beutetile in für diesen Spieler verbundenen Path Connection Layers liegen.
3. Nur ein belastbares PCL-Ergebnis `0` verwirft den Kandidaten. Ein positives
   Ergebnis beweist keinen Detailpfad und lässt Vanilla autoritativ.
4. Dieselbe PCL-Prüfung läuft beim konkreten Vanilla-Kandidaten-Handoff erneut,
   damit ein leerer oder veralteter Cache keinen bekannten Nullfall zulässt.
5. Liefert Vanillas vollständige State-0-Suche trotz eines gültigen verborgenen
   Kandidaten `0`, stellt der begrenzte Fallback genau diesen Kandidaten bereit.
   Vanilla schreibt Ziel, Reservierung, Pfad und AI-State selbst und ruft
   `MoveHere` selbst auf.
6. Bei erfolgreichem `MoveHere` folgt der Jäger Vanillas Hindernispfad.
7. Während State `1` prüft der persistente Scan das aktive Ziel über denselben
   PCL-Cache. Bei PCL `0` werden nur Ziel-Global-ID und passende Reservation
   invalidiert. Vanillas Identitätsfehlerpfad stoppt den Auftrag und sucht neu.
8. Will Vanilla bei nativer Distanz `<= 28` trotz aktivem Pfad wegen blockierter
   Sicht vorzeitig angreifen, lässt die Pfadfortsetzung für diesen Update-Aufruf
   Vanillas vorhandene Distanz-29-Stufe laufen. Der Mod gibt keinen Move aus und
   schreibt keine Ziel-, Pfad-, Order- oder AI-State-Felder.
9. Sobald die native Sichtprobe positiv wird, endet der Eingriff. Vanilla greift
   regulär an.
10. Nach echtem Projektilspawn bleibt nur die identitätsgesicherte
    Projektilkompensation. Sie darf bei einem feststeckenden Pfeil
    `DamageUnitRanged`, aber niemals `KillUnit` verwenden.

Eine eigene Schusstilesuche oder Bewegungs-State-Machine wird nur neu erwogen,
wenn ein reproduzierbarer Fall trotz positiver PCL-Verbindung, angenommenem
Vanilla-Pfad und funktionierender Pfadfortsetzung ungelöst bleibt.

## Ausgangsbasis der heutigen Beuterangfolge

Paket F muss die bestehende Auswahl erweitern und darf keine parallele
Kadaverliste einführen. Der aktuelle Code in `ImprovedHuntersRuntime.cs`
verwendet:

- `HunterHutWorkCost = 600`,
- `BestTargetToleranceCost = 80`,
- Standard-Behandlungskosten `100`, Hase und Huhn `80`, Kamel `120`,
- in der groben Sortierung
  `600 + handling + granaryRoundTrip + heuristicDistance * 10 * 2`,
- in der endgültigen Bewertung
  `CycleCost = 600 + handling + granaryRoundTrip + approach * 2`,
- `approach = ChebyshevDistance * 10`,
- eine normalisierte Fleisch-pro-`CycleCost`-Entscheidung und danach die
  bestehende Nahbesten-Toleranz.

Der heutige Kandidatensnapshot akzeptiert nur lebende, verfügbare Beute. Für
einen gültigen Kadaver soll dieselbe Rechnung um einen kleinen Zeitvorteil
reduziert werden. `50` Kostenpunkte sind nur ein Ausgangswert: Zuerst muss
kalibriert werden, ob die bestehende Konvention tatsächlich ungefähr zehn
Kostenpunkten pro Sekunde entspricht.

Der Kadaverbonus muss in grober Sortierung und endgültiger `CycleCost`
identisch wirken. Fleischmenge, Granary-Rückweg, Annäherung, Toleranz und
PCL-Gates bleiben erhalten.

## Bestätigte Vanilla- und Native-Semantik

### Zielsuche und Sicht

- Die Hunter-Zielsuche prüft Unit-Slots, Alive-State, Typ, Reservierung,
  Distanz und danach Sicht.
- Der öffentliche `OnUnitHunterQueryTarget`-Detour liegt vor Vanillas späteren
  Distanz- und Sichtprüfungen. Das Zulassen eines Typs überspringt die Sicht
  daher nicht.
- Vanilla verwendet Manhattan-Distanz in zwei Pässen: zuerst Kandidaten mit
  Distanz `> 20`, danach bei fehlendem Ziel Kandidaten mit Distanz `> 5`.
- Bei Distanz `< 54` muss der gemeinsame Sichtwrapper `1..432` liefern. `0`
  verwirft den Kandidaten.
- Ab Distanz `54` kann die frühe Suche die Sichtprüfung überspringen; der
  spätere direkte Hunter-Orderpfad prüft Sicht erneut.
- Der gemeinsame Sichtwrapper nutzt Weltkoordinaten, Unit-Höhen, Tileflags und
  Hindernishöhen. Eine Bresenham-Tilelinie oder Gebäudeliste ist kein
  gleichwertiger Ersatz.

### Distanz-28-Fehler und Pfadfortsetzung

- Für native Distanz `> 28` läuft State `1` Vanillas angenommenen Pfad weiter.
- Bei Distanz `<= 28` versucht Vanilla abhängig von den Pfadfeldern direkt
  anzugreifen. Blockierte Sicht führt zu State `6`, Rückkehrtimer `20` und
  Rückweg zur Hütte.
- Im Lauf `1.1.38` wurden `41` direkte State-1-Angriffsresultate erfasst:
  `40` Fehlschläge bei Distanz `<= 28` und ein Erfolg.
- Der aussagekräftigste Jäger lief `44,383 s` bis Pfadfortschritt `59/61` und
  brach exakt bei Distanz `28` mit weiterhin blockierter Sicht ab.
- `GameUnit +0xF6` ist beobachteter Pfadfortschritt, `+0xF8` Pfadlänge und
  `+0xF2=2` aktiver Pfadstatus. Ein erneutes `MoveHere` setzt Fortschritt zurück
  und ist keine Fortsetzung.
- Die heutige Lösung ändert im sicheren Hookfenster nur den temporären
  Distanzregisterwert `RDI` von höchstens `28` auf `29`, wenn State `1`, stabile
  Zielidentität, aktiver unvollständiger Pfad und blockierte native Sicht
  zusammenpassen.
- Fortsetzung endet bei freier Sicht, Pfadende, Kontextwechsel, mehr als `60 s`
  kontinuierlicher Fortsetzung oder `3 s` ohne Fortschritt. Ein echter
  Grenzabbruch erhält `5 s` Retry-Cooldown.
- Die frühere globale Grenze von zwei Zielidentitäten war nur Diagnose und
  verursachte scheinbar positionsabhängiges Versagen. Seit `1.1.40` existiert
  höchstens ein unabhängiger Zustand pro Jäger; Mehrjägertests funktionieren.

### PCL-Erreichbarkeit

- `GamePlayerManagerAPI.GetNextReachablePCLToDestinationForPlayer` verwendet
  spielerabhängige Path Connection Layers einschließlich dynamischer Tore.
- Vanillas `MoveHere` verwendet dieselbe Funktion. PCL `0` führt dort direkt
  zum Fehlschlag.
- Der aktuelle Aufruf nutzt `r_ControllableForPlayerId`, den Modus aus
  `GameUnit +0x35C`, Quell-PCL des aktuellen Jägertiles und Ziel-PCL des
  aktuellen Beutetiles.
- In `1.1.43` wurden `53` Kandidatenprobes und `10` exakte
  PCL/`MoveHere`-Korrelationen erfasst: `4/4` positiv entsprach `MoveHere=1`,
  `6/6` Null entsprach `MoveHere=0`; keine Fehlkorrelation oder PCL-Exception.
- Der reale Jägermodus war in den Tests `0`. Modi `0`, `2` und live gelesener
  Modus ergaben dort dasselbe Resultat; produktiv wird nur der reale Modus
  verwendet.
- Warm-up lag bei etwa `170 us`, normale Abfragen überwiegend bei `1..3 us`,
  Mittelwert ungefähr `4,43 us`. Ingame war keine Verzögerung sichtbar.
- PCL `0` ist ein konservativer Negativbeweis. PCL positiv ist nur eine grobe
  Verbindung; Vanillas detaillierter `MoveHere`-Pfad bleibt maßgeblich.
- Der Cache gilt nur für identische Jäger-/Beuteidentitäten und identische
  Spieler-, Modus-, Quell- und Ziel-PCL-Eingaben, höchstens eine Sekunde.
  Eingabeänderungen umgehen ihn sofort.
- Der persistente Scan läuft ungefähr alle `100 ms`. Für aktive Ziele führt ein
  bestätigtes PCL `0` über die Ziel-Global-ID in Vanillas eigene Neusuche.
- API-, Eingabe- oder Nativefehler sind fail-open. Ein technischer Fehler darf
  kein erreichbares Tier fälschlich entfernen.

### Jägerhütten-Ausnahme

- Gebäudetyp `7` ist `STRUCT_HUNTERS_HUT`.
- Der gemeinsame Höhenhelper leitete Typ `7` im hindernisbewussten Modus über
  einen Sonderfall, der nur Geländehöhe zurückgab.
- Vanillas normale Gebäudehöhentabelle enthält für Typ `7` Blockerhöhe `40`,
  wie für die Holzfällerhütte.
- `HunterHutVisibilityPatch` ändert am auditierten Dispatch einen Wert von
  Sonderfall `0` auf normalen Gebäudehöhenfall `3` und stellt das Original
  konfliktgesichert wieder her.
- Eine eigene allgemeine Projektilbahnprüfung wird nicht parallel entwickelt.
  Sie wird nur bei einem reproduzierbaren anderen Gebäudekollisionsfall neu
  geöffnet.

## Verbindliche nächste Arbeitspakete

### Paket E: Bewegungsgeschwindigkeit nach tatsächlicher Reststrecke

Dies ist der nächste Schritt. Die Korrektur wird in einer eigenen, leicht
entfernbaren Datei mit getrenntem Beobachtungs- und Verhaltenspfad entwickelt.
Paket C darf die Struktur vorher nicht festschreiben.

Hypothese: Vanilla wählt Lauf- oder Schleichstufe anhand Luftlinien- oder
Manhattan-Distanz in `EDI`. Bei kurzer Luftlinie und langem Hindernisweg
schleicht der Jäger deshalb unangemessen lange.

Analyse und Entwicklung:

1. Zuerst rein beobachtend kalibrieren, ob `+0xF8 - +0xF6` die tatsächlich
   verbleibenden Pfadschritte ausreichend repräsentiert.
2. Falls nicht, Vanillas gespeicherte Wegsegmente identifizieren und Restlänge
   ohne globale Scratchmutation bestimmen.
3. Vorhandene Vanilla-Distanzstufen, Animationen und Geschwindigkeiten gegen
   reale Restweglängen protokollieren.
4. Erst nach der Kalibrierung ausschließlich die passende vorhandene
   Vanilla-Stufe auswählen. Keine direkten Speed- oder Animationsfelder
   schreiben und keinen neuen Move ausgeben.
5. Die letzte Annäherung vor freier Schusslinie muss Vanillas beabsichtigte
   Schleichbewegung erhalten.
6. Beobachtungsfehler dürfen die Vanilla-Stufe nicht ändern. Mod-/Option-Aus,
   ungültiger Kontext und Multiplayer bleiben wirkungslos.

#### Bestätigte Vanilla-Analyse und Implementierung 1.1.46/1.1.47

Die Analyse bezieht sich auf die kanonische installierte DLL mit SHA-256
`33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`.
Alle folgenden Code- und Manageradressen sind RVAs dieser DLL; `0xB4FE78` ist
ausdrücklich ein managerrelativer Feldoffset.

- `HunterUpdate` beginnt bei `0x12FC20`. Die Distanzfunktion bei `0x79C0`
  liefert exakt `abs(dx) + abs(dy)` und hält das Ergebnis im State-1-Pfad in
  `EDI`. Sie berücksichtigt den gespeicherten Umweg nicht.
- Die erste Stufenentscheidung liegt bei `0x130063` (`cmp edi,40`). Die
  vollständige Vanilla-Leiter wählt `r_CurrentSpeed` `1`, `2`, `3`, `4`, `6`,
  `8` oder `10` für Distanzen `>40`, `37..40`, `35..36`, `33..34`, `31..32`,
  `29..30` beziehungsweise `<=28`. Sämtliche zugehörigen Vanilla-Schreibpfade
  liegen hinter diesem Vergleich.
- `MoveHere` bei `0x196230` schreibt Pfadstatus `2` nach öffentlich `+0xF2`,
  Fortschritt `0` nach `+0xF6` und die Pfadlänge nach `+0xF8`. Das Interop-Layout
  definiert `+0xF8` korrekt als `UInt32`; ältere lokale Diagnose- und
  Runtime-Reads verwendeten dort `UInt16` und werden in `1.1.46` typkorrigiert.
- Die generische Bewegung lädt bei `0x18576C` die native
  `GameUnitManager`-Basis an Modul-RVA `0x67E7400`. Ab `0x18580F` wendet sie
  darauf den managerrelativen Pfadpuffer-Offset `0xB4FE78` an. Der effektive
  Pfadpuffer liegt in dieser DLL somit bei Modul-RVA `0x7337278`, nicht bei
  `0xB4FE78`. `MoveHere` ab `0x196554` bestätigt dieselbe Adressform beim
  Schreiben des Pfades. Pro Unit stehen `0x3E8` Bytes und damit höchstens 2000
  Vierbit-Richtungen zur Verfügung. Gerade Richtungen sind orthogonal, ungerade
  diagonal; gerade Pfadindizes verwenden das Low-Nibble, ungerade das
  High-Nibble.
- `+0xF8 - +0xF6` ist daher die Zahl verbleibender Richtungseinträge, aber nicht
  Vanillas Manhattan-Einheit. Die vergleichbare Restmetrik ist
  `orthogonale Schritte + 2 * diagonale Schritte`.
- Die Bewegung kann `+0xF6` bereits beim Laden eines Segments erhöhen. `1.1.46`
  addiert deshalb bewusst keine unbewiesene In-flight-Korrektur. Die dekodierte
  Restmetrik ist dadurch im Zweifelsfall um höchstens ein Segment zu klein und
  bleibt eine konservative Untergrenze. Das Feld bei öffentlich `+0x3F0`, das
  die beobachtete Fortschrittserhöhung mitsteuert, wird zur Kalibrierung
  mitprotokolliert.

`HunterRemainingPathSpeedRecovery.cs` setzt einen separaten, exakthashgebundenen
Hook vor `0x130063`. Nur wenn alle Identitäts-, State-, Pfad-, Längen-,
Richtungs-, Sicht- und Modusguards bestehen, die native Sichtprobe exakt `0`
liefert und die Restmetrik eine schnellere vorhandene Vanilla-Stufe auswählt,
wird `RDI` vorübergehend auf höchstens `41` angehoben. Der relocatete Vergleich
und alle Speed-/Animationsschreibvorgänge bleiben Vanilla-Code. Positive Sicht
lässt `RDI` unverändert und bewahrt damit Vanillas letzte langsame Annäherung und
den echten Angriff.

Beobachtung und Verhalten besitzen getrennte Fehlerpfade. Die Logs enthalten
Hunter-/Zielidentität, `UInt32`-Pfadlänge, Fortschritt, orthogonale und diagonale
Schritte, Zählinvariante, direkte und dekodierte Distanz, alte und ausgewählte
Vanilla-Stufe, Sichtresultat, `+0x3F0` und die tatsächliche Registermutation.
Versuche enden nach 60 Sekunden, drei Sekunden ohne Fortschritt oder bei
Kontext-/Sichtwechsel; ein No-progress-Stopp sperrt nur dieselbe Identität fünf
Sekunden. Zustände bleiben pro Hunter getrennt.

Die Restwegmessung und Geschwindigkeitsauswahl sind mit `1.1.47` ingame
bestätigt. Offen ist die erneute Abnahme desselben Maueraufbaus mit `1.1.49`:
Dabei muss insbesondere die Nahbereichs-Neusuche das eigene reservierte Ziel
über Vanillas regulären State-0-/`MoveHere`-Pfad fortsetzen, ohne Kontrollfälle
oder die langsame Annäherung im echten Schussbereich zu verändern.

Der erste Paket-E-Test mit `1.1.46` platzierte einen Jäger bei direkter Distanz
`10` nahe am Wild, erzwang durch eine Mauer aber einen Vanilla-Pfad der Länge
`72`. Die vorhandene Distanz-28-Fortsetzung setzte `RDI` weiterhin auf `29`,
während der Pfadfortschritt langsam von `0` auf `24` stieg. Package E meldete
zweimal `invalid-packed-path-direction`, aber keine erfolgreiche Beobachtung,
keine Registermutation und keinen Callbackfehler. Damit sind Szenario,
Pfadlänge und Fortschrittsfelder bestätigt; der Fail-open-Pfad funktionierte.
Die ungültigen Nibbles entstanden ausschließlich dadurch, dass `1.1.46`
`0xB4FE78` fälschlich als Modul-RVA behandelte und fremden Speicher las.

Version `1.1.47` bezieht die Basis über
`GameUnitManagerAPI.Instance.GetUnitManager().Pointer`, addiert erst danach den
managerrelativen Offset `0xB4FE78` und validiert bei passendem DLL-Hash den
Managerzeiger zusätzlich gegen Modul-RVA `0x67E7400`. Eine Abweichung lässt
Package E deaktiviert und Vanillas Verhalten unverändert. Der nachfolgende
Ingame-Test bestätigte gültige Richtungen, Restkosten und tatsächliche
Stufenauswahlen.

Der Test von `1.1.47` bestätigte gültige Pfaddekodierung und die beabsichtigten
Vanilla-Geschwindigkeitsstufen. Zwei Läufe brachen jedoch bei Pfadfortschritt
`55/68` beziehungsweise `51/68` vor dem direkten Angriffsaufruf ab. In beiden
Fällen begann unmittelbar danach eine neue Zielsuche mit Ziel `17/319`,
`pclUnreachable=False`, `cooldown=False`, aber `best=none` und ohne akzeptiertes
`MoveHere`. Der dritte Lauf erhielt nach Bewegung des Rehs einen 26 Schritte
langen Pfad und erreichte bei `7/26` den erfolgreichen direkten Angriff.

Die erneute Vanilla-Analyse erklärt den Übergang: Der zweite Aufruf der
Distanzhilfe schreibt den maximalen Weltkoordinatenabstand nach dem Scratchfeld
bei `0x1834A8F5C`. `HunterUpdate` vergleicht ihn bei RVA `0x130019` mit `20`.
Bei höchstens `20` springt der exklusive Nahbereichsbranch über `0x130022` zum
Zielquery-Aufruf bei `0x12FF2E`, also noch vor dem Geschwindigkeitshook. Eine
Nullrückgabe wird bei `0x12FF33` getestet und führt ab `0x12FF53` zu State `6`,
Timer `20` und Hüttenrückkehr. Der direkte Angriffsresultat-Hook wurde bei den
beiden Abbrüchen folgerichtig nicht erreicht.

Während des laufenden Auftrags trägt das eigene Reh Vanillas Reservierung `2`.
Die allgemeine Zielrangfolge akzeptiert absichtlich nur unreservierte Beute und
lieferte deshalb `best=none`; das eigene noch gültige Ziel ging bei der
Nahbereichs-Neusuche verloren. Version `1.1.48` erweitert den vorhandenen
exakthashgebundenen Zielsuche-Fallback ausschließlich für diesen Branch. Nur
wenn Hunter-State `1`, Slot und Global-ID, lebende Beute, Konfiguration,
Event-Policy, PCL, Cooldown, Kadaverstatus und Reservierung `2` übereinstimmen
und kein anderer lebender Jäger dieselbe Identität führt, darf `RAX` von `0`
auf die bestehende Ziel-ID gesetzt werden. Der Kandidat bleibt höchstens zwei
Sekunden für Vanillas unmittelbar folgenden State-0-Aufruf erhalten. Vanilla
selbst schreibt State, Ziel, Reservierung und Pfad und ruft `MoveHere` auf; der
Mod erzeugt keinen eigenen Bewegungsauftrag. Identitätsfehler-Neusuchen,
fremde Reservierungen und alle fehlgeschlagenen Guards bleiben unverändert.

Der erste Ingame-Test von `1.1.48` verwarf diese konkrete Hookplatzierung. Der
letzte Logabschnitt lädt alle Hooks erfolgreich und registriert den neuen Jäger
am `2026-08-17 18:23:47.641`; danach endet der Prozess hart, ohne Managed-
Exception und ohne den Bestätigungsmarker des State-1-Refresh-Hooks. Ursache
ist nicht die Kandidatenvalidierung, sondern die durch den Inline-Detour
überschriebene Kontrollflussstruktur. `X64InlineHook` benötigt einen absoluten
14-Byte-Sprung und dekodiert dafür ab `0x130022` ganze Instruktionen:

- `0x130022..0x130027`: `mov edx,[0x18092F2C4]`, sechs Byte;
- `0x130028..0x13002C`: `jmp 0x12FF2B`, fünf Byte;
- `0x13002D..0x130033`: `movsxd rbx,[0x18092F2C4]`, sieben Byte.

Damit wird die 18-Byte-Spanne `[0x130022,0x130034)` ersetzt. Vanillas direkt
davorliegendes `jg 0x13002D` bei `0x130020` bleibt jedoch erhalten. Bei einem
Scratch-Abstand größer `20` springt es elf Byte in den Detour hinein, konkret
in das eingebettete Acht-Byte-Ziel des absoluten Sprungs, und führt Daten als
Code aus. Der Crash beim Loslaufen ist damit vollständig erklärt. Ein Hook darf
nicht nur anhand seiner Startinstruktion bewertet werden; alle eingehenden
Sprungziele innerhalb seiner tatsächlich dekodierten Überschreibspanne müssen
vor Installation ausgeschlossen werden.

Erster Teil der Alternative: Den Hook an `0x130022` vollständig entfernen und
den Branch-Kontext stattdessen an der Vergleichsinstruktion `0x130019` erfassen.
Der dortige 14-Byte-Hook dekodiert exakt 15 Byte bis ausschließlich `0x130028`:
`cmp [scratch],20` (sieben Byte), `jg 0x13002D` (zwei Byte) und das nur im
Nahbereich ausgeführte `mov edx,[...]` (sechs Byte). Das Sprungziel `0x13002D`
bleibt außerhalb der überschriebenen Spanne. Der Callback läuft vor den
relokierten Originalinstruktionen und markiert den Refresh nur, wenn derselbe
Scratchwert `<=20` ist; Vanilla führt anschließend Vergleich, `jg` und Query-
Sprung unverändert aus.

Die vollständige Sprungzielprüfung zeigt jedoch einen zweiten, bislang latent
unsicheren Detour: Der 14-Byte-Hook ab `0x12FF33` dekodiert 18 Byte bis
`0x12FF45`. Ein späterer Vanilla-Pfad springt bei `0x13058B` direkt nach
`0x12FF3E` und damit ebenfalls in das Innere dieser Spanne. Dieser Pfad wurde im
ersten Crashtest nicht erreicht, darf aber nicht im Code verbleiben. Die sichere
Alternative kombiniert deshalb Query und Ergebnisbeobachtung in der exakt 14
Byte langen Spanne `[0x12FF2E,0x12FF3C)`: `call 0x18AF00` (fünf Byte),
`test eax,eax` (zwei Byte) und Vanillas `movsxd rax,[0x18092F2C4]` (sieben
Byte). Das anschließende `je 0x12FF53` sowie das fremde Sprungziel `0x12FF3E`
bleiben unangetastet. Der Callback läuft nach den drei relokierten
Originalinstruktionen, sieht deshalb Vanillas echtes Zero-Flag und die erneut
geladene Hunter-ID und ändert bei vollständig validiertem eigenen Ziel nur das
Zero-Flag von gesetzt auf gelöscht. Dadurch nimmt das unveränderte `je` nicht
den Hüttenpfad, sondern Vanillas State-0-Writer; der schon bewährte State-0-
Fallback übergibt das Ziel anschließend an Vanillas vollständige Ziel-,
Reservierungs- und `MoveHere`-Sequenz. `RAX`, Bewegung, Pfad und AI-State werden
im State-1-Callback nicht geschrieben.

Version `1.1.49` setzt diese Alternative um. Der Context-Hook beginnt bei
`0x130019`, fordert explizit die validierte 15-Byte-Spanne an und liest vor den
relokierten Originalinstruktionen den Weltabstand aus RVA `0x34A8F5C`. Nur bei
einem Wert von `0..20` wird der eigene Reservierungs-Kandidat vorbereitet. Die
Hunter-ID stammt nicht mehr aus einer Registerannahme, sondern exakt aus RVA
`0x92F2C4`, die Vanillas Instruktion bei `0x130022` unmittelbar danach selbst
als Query-Akteur lädt. Vor dem Commit dekodiert der Mod die drei
Hookinstruktionen und den anschließenden Query-Sprung. Der kombinierte Query-
und Ergebnis-Hook muss exakt als 14-Byte-Spanne `[0x12FF2E,0x12FF3C)` mit
außerhalb liegendem Fehlerbranch und Ziel `0x12FF53` dekodieren. Der Mod
validiert beide RIP-relativen Adressen, das Query-Ziel `0x18AF00` und durchsucht
die vollständige HunterUpdate-Spanne
`[0x12FC20,0x1313D2)` nach direkten Calls oder Sprüngen von außerhalb in das
Innere beider Hookspannen. Jede Abweichung deaktiviert den gesamten Fallback vor
der Installation; Vanilla bleibt dann unverändert.

Der Ingame-Lauf mit `1.1.49` bestätigt den sicheren Hookbetrieb, zeigt aber eine
neue sichtbare Regression im Spezialfall. Die normale Restwegkorrektur läuft mit
passender Animation und Geschwindigkeit. Eine falsche Sitz-/Warteanimation trat
erst nach der Nahbereichs-Neusuche beziehungsweise während der anschließenden
Distanz-29-Pfadfortsetzung auf. Damit ist die frühere Vermutung zu verwerfen,
Geschwindigkeit und Animation der normalen State-1-Fernstufe voneinander zu
trennen: Eine schnelle Bewegung mit Schleichanimation wäre fachlich falsch und
der unauffällige Normalfall belegt, dass diese Vanilla-Stufe als Ganzes korrekt
ist.

Der Log grenzt den problematischen Übergang auf
`Near-Refresh -> Query/MoveHere -> Distanz-29-Fortsetzung` ein. Im beobachteten
Lauf meldete der Refresh `vanillaQueryReturnedZero=False`; der neue
ZF-clear-Fallback mutierte daher nichts. Nach vollständiger Blockierung meldete
die aktive Beute `active-target-pcl-disconnected`, Vanilla fand keinen
erreichbaren Ersatz, der Hunter erschien später in State `7` und wurde auf
demselben Slot gelöscht und als Bauer neu erzeugt. ImprovedHunters besitzt
keinen Hunter-Delete- oder Hunter-zu-Bauer-Writer; diese Rückwandlung ist
Vanillas Folgezustand nach erfolgloser Neusuche.

Version `1.1.50` ergänzte deshalb ausschließlich read-only Diagnose an bereits
validierten Callbacks. Der Test belegt die Ursache: Vor dem ersten Refresh lief
der Hunter mit aktivem Pfad und normaler Vanilla-Locomotion. Bei Welt-Maximalabstand
`<=20` sprang `HunterUpdate` von `0x130022` zur Query bei `0x12FF2E`. Ein
Nicht-Null-Ergebnis schrieb State `0`; der folgende `MoveHere` setzte Pfadfortschritt
und Animationsframe auf den normalen Startwert `657` zurück. Weil die neue Query
in kurzen Abständen weitere Ziele lieferte, wurde dieser Startframe wiederholt,
bevor Vanilla in die reguläre Bewegungsanimation wechseln konnte. Die sichtbare
Sitz-/Warteanimation war daher kein falscher Speed-/Animationswert der normalen
Distanzstufe, sondern ein Order-Reset-Loop.

Die erneute statische Analyse der kanonischen DLL bestätigt: Der zweite Aufruf
von `0x79C0` überschreibt den Maximalabstand-Scratch bei RVA `0x34A8F5C`; innerhalb
von `HunterUpdate` liest ihn danach nur das `cmp ... ,20` bei `0x130019`. Die
nächste Distanzberechnung überschreibt ihn wieder. Der vorhandene sichere Hook
deckt exakt `[0x130019,0x130028)` ab, lässt das originale Fernziel `0x13002D`
außerhalb und führt Callback, `cmp`, `jg` und `mov` in dieser Reihenfolge aus.
Flags vorab zu ändern wäre wirkungslos, weil das relocierte `cmp` sie neu setzt;
der Hookkontext besitzt außerdem keinen Instruction-Pointer für eine sichere
direkte Umleitung.

Version `1.1.51` verwendet deshalb keinen neuen Hook und schreibt weder Speed
noch Animation, Bewegung, Pfad, Order oder AI-State. Am bestehenden Hook wird
für den blockierten Spezialfall ausschließlich der Vergleichs-Scratch `20 -> 21`
gesetzt. Die unveränderte Vanilla-Verzweigung nimmt dadurch `0x13002D`, behält
den aktiven Pfad und erreicht die normalen Speed-/Locomotion-Stufen. Der spätere
Hook bei `0x1300EA` darf RDI nur noch nach einem einmaligen, identitätsgebundenen
Ticket auf Vanillas Distanz `29` setzen. Damit gehören Refresh-Unterdrückung und
Angriffsvermeidung garantiert zum selben `HunterUpdate`.

Der anschließende Test zeigte eine zweite Vanilla-Grenze. Beim Eintritt in den
Nahbereich lieferte der von Zielsuche und direktem Angriff gemeinsam verwendete
Wrapper RVA `0xA06F0` ein positives Ergebnis, obwohl eine diagonal gebaute Mauer
die physische Pfeilbahn blockierte. Frühere Pfeile waren in derselben Geometrie
sichtbar an der Mauer abgeprallt. Die statische Analyse erklärt diese
Abweichung: Der Wrapper ruft den Sichtkern RVA `0x9E350` zuerst vom Jäger zum
Ziel auf und gibt jedes positive Ergebnis sofort zurück. Nur bei einem
Vorwärtsergebnis `0` wird derselbe Kern mit vertauschten Endpunkten erneut
aufgerufen. Ein positives Wrapper-Ergebnis beweist daher nur einen erfolgreichen
gerichteten Rasterlauf; besonders an Diagonal- und Eckgeometrie ist es kein
hinreichender Nachweis einer physisch freien Pfeilbahn.

Die tatsächliche Pfeilbewegung besitzt einen separaten Pfad. Der gemeinsame
Flugschritt bei RVA `0x9EF20` aktualisiert ein lebendes Projektil und ruft die
große Kollisionsroutine RVA `0x9C730` auf. Diese Routine liest und verändert
Projektile, Trefferziele und weitere Managerzustände. Sie ist weder eine
zustandslose Sichtabfrage noch eine sichere oder günstige Vorabprüfung und darf
nicht mit einem künstlichen Projektil aus dem Hunter-Hook aufgerufen werden.

Version `1.1.52` ergänzt daher keinen Hook und keine eigene Speed-, Animations-
oder Bewegungssteuerung. Im bereits validierten Nahbereich wird zunächst wie
bisher der Vanilla-Wrapper aufgerufen. Ergibt er `0`, sind intern bereits beide
Richtungen fehlgeschlagen und es entsteht ohne Zusatzkosten das Pfadticket.
Ergibt er positiv, wird der hash- und bytevalidierte Sichtkern zusätzlich
explizit vorwärts und rückwärts mit getrennten privaten, bewachten Kontexten
aufgerufen. Nur zwei positive Kernresultate gelten als Kandidat für freie Sicht:
Der `<=20`-Refresh wird dann ebenfalls über den kurzlebigen Vergleichs-Scratch
übersprungen, aber es wird bewusst **kein** Pfadticket erzeugt. Dadurch erreicht
der unveränderte spätere Distanzpfad Vanillas direkten Angriff. Bei einem
Richtungswiderspruch bleibt der bestehende Pfad dagegen aktiv.

Das begrenzte Log nennt `wrapperResult`, `coreHunterToPreyResult`,
`corePreyToHunterResult`, `wrapperMatchingDirection` und eine der Klassifikationen
`blocked-wrapper-both-directions`, `blocked-directional-disagreement` oder
`visible-attack-handoff`. `physicalArrowCollisionPreflight=False` hält fest,
dass dies absichtlich noch kein vollständiger physischer Pfeiltest ist. Falls
beide Kernrichtungen an der Diagonalmauer positiv bleiben und der Pfeil dennoch
kollidiert, ist die bidirektionale Näherung widerlegt; dann muss eine neue,
nachweislich zustandslose Kollisionsabfrage gefunden werden. Die analysierte
Live-Projektilroutine ist dafür keine sichere Alternative.

Der `1.1.52`-Test ist für die blockierte Geometrie bereits eindeutig. In zwei
getrennten Anläufen meldete der Wrapper an derselben Diagonalmauer positiv,
während nur die Richtung Beute zum Jäger positiv war: `wrapperResult=18`,
`coreHunterToPreyResult=0`, `corePreyToHunterResult=18` beziehungsweise
`16/0/16`. Die Klassifikation `blocked-directional-disagreement` bereitete das
Pfadticket vor, der `<=20`-Refresh wurde als `ContinueExistingPath` übersprungen
und RVA `0x1300EA` konsumierte das Ticket. Damit erkennt die bidirektionale
Prüfung genau den beobachteten Vanilla-Fehlschussfall. Keine Callbackfehler,
Exceptions oder harten Abbrüche traten auf.

Noch nicht abgenommen ist die Gegenrichtung: Im Log fehlt
`visible-attack-handoff`. Zwei spätere Vanilla-Angriffsbeobachtungen erreichten
zwar den direkten Angriff, endeten aber jeweils mit `attackResult=0`. Außerdem
gab es einzelne `pcl-cache-unavailable`-Ticks; im ersten Lauf ließ einer davon
bei Weltabstand `14` Vanillas Query zu und wechselte das Ziel von `17/319` auf
`44/440`. Der Lauf mit `1.1.54` reproduzierte den Fehler eindeutig: Ein positiver
Cachetreffer um `21:41:17.407` wurde bis `21:41:18.355` verwendet, während der
Inline-Hook um `21:41:18.405` nahezu exakt an der Ein-Sekunden-Grenze
`pcl-cache-unavailable` meldete. Der 100-ms-Scan hatte den Eintrag bis dahin nur
gelesen; Cachetreffer verlängerten dessen Ablaufzeit nicht. Die anschließende
Vanilla-Query erkannte dasselbe Ziel zwar wieder als PCL-erreichbar, lieferte
wegen Reservation `2` jedoch `best=none` und setzte den Jäger in State `6`.

Version `1.1.55` trennt deshalb den allgemeinen Ein-Sekunden-Auswahlcache vom
aktiven Zielzustand. Der persistente Scan fragt ein unverändertes aktives Ziel
höchstens einmal pro Sekunde nativ ab und veröffentlicht einen separaten, zwei
Sekunden lesbaren Snapshot. Geänderte Identität beziehungsweise Spieler-,
Modus-, Quell-PCL- oder Ziel-PCL-Eingaben erzwingen sofort eine neue Abfrage.
Der Inline-Hook liest ausschließlich diesen Snapshot und protokolliert Quelle
und Alter; ein bestätigtes PCL `0` bleibt ein harter Abbruch. Das längere
Lesefenster beeinflusst die Reaktionszeit nicht: Die aktive Neuprüfung bleibt
auf ungefähr eine Sekunde begrenzt und invalidiert ein getrenntes Ziel über den
bereits bestätigten Vanilla-Pfad.

Der test-only Reh-Freeze aus `1.1.53` ist als ungeeignet widerlegt. Im zweiten
Test setzte er ab `21:28:37.759` für fünf lebende Rehe am selben Ausgangspunkt
`SkipOriginalFunction=True`; danach blieb unter anderem Ziel `17/319` intern
lebendig, war ingame jedoch unsichtbar und Vanillas direkter Angriff endete mit
`attackResult=0`. Für diese zweite Herde erschien bis zum Prozessende kein
`OnUnitDelete`. Bei der ersten eingefrorenen Herde wurden dagegen die Slots
`55`, `15`, `59` und `4` kurz darauf tatsächlich gelöscht. Der Testlauf darf
daher weder als Freisicht- noch als Angriffsabnahme gewertet werden.

Die native Analyse der installierten DLL mit SHA-256
`33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`
zeigt die Ursache: Der vom Script Extender als `OnUnitMovement` angebotene
Handler an RVA `0x1801E0` ist kein isolierter Koordinatenschritt. Er übernimmt
Next-Tile-Felder in die aktuellen Tile-Felder und pflegt zusätzlich die globale
verkettete Unit-Belegung der Kartenfelder. Sein vollständiges Überspringen lässt
AI/Pfad, Unit-Tile, Belegungsindex und Darstellung auseinanderlaufen. Deshalb
entfernt `1.1.54` die Datei, den Projektverweis und sämtliche Runtime-
Initialisierungs-, Reset-, Status- und Dispose-Verkabelung vollständig. Rehe
laufen wieder ausschließlich über Vanilla; ein zukünftiger Testhelfer darf
diesen Handler nicht überspringen.

Das Ticket verlangt State `1`, dieselbe lebende Zielidentität, Reservation `2`,
keinen fremden Hunter, einen aktiven unvollständigen Pfad, explizit positive
PCL-Erreichbarkeit aus dem höchstens zwei Sekunden lesbaren aktiven Snapshot und
entweder ein Wrapper-Ergebnis `0` oder einen positiven Wrapper mit nicht
beidseitig positivem Kernvergleich. Der Inline-Hook startet selbst keine native
PCL-Abfrage; der bestehende 100-ms-Scan erneuert den aktiven Snapshot höchstens
einmal pro Sekunde außerhalb des Hookkontexts. Nach drei
Sekunden ohne Pfadfortschritt, nach 60 Sekunden Gesamtdauer oder bei abweichender
Identität, PCL, Sicht beziehungsweise Reservation wird kein Ticket ausgegeben.
Vanilla führt dann seine normale Query aus. Der frühere State-1-ZF-Fallback und
seine zweistufige State-0-/`MoveHere`-Übergabe wurden entfernt, damit ein real
unerreichbares reserviertes Ziel nicht erneut denselben Reset-Loop erzeugt.

### Aktueller Übergabestand nach dem `1.1.55`-Test

Paket E ist **offen und im aktuellen Stand nicht funktional**. Die frühere
Einschätzung, der bewegte Freisichtfall reiche bereits für die Abnahme aus, ist
durch den neuesten Lauf widerlegt. Der relevante Kartenstart begann am
`2026-08-17 22:19:15.699`; der erste Auftrag folgte um `22:19:24.977`.

Der Log belegt zwei aufeinanderfolgende Fehlerbilder:

1. Jäger `1/395` nahm Reh `16/410` mit einem gültigen Vanilla-Pfad
   `2/8/0/79` an. Die blockierte Pfadfortsetzung arbeitete zunächst korrekt:
   Sichtklassifikationen meldeten `blocked-wrapper-both-directions` oder
   `blocked-directional-disagreement`, der aktive PCL-Snapshot war jeweils ein
   Treffer und der Pfad schritt bis `60/79` fort.
2. Der letzte protokollierte Nahbereichsbypass lag um `22:19:30.071` bei
   `56/79`. Um `22:19:31.062` erreichte Vanilla ohne neue
   Sichtklassifikation, ohne neues Fortsetzungsticket und ohne PCL-Fehler den
   direkten Angriff: `attackResult=0`, `nativeDistance=9`, `path=2/0/60/79`.
   Der anschließende Vanilla-Fehlerpfad setzte State `6` und ließ den Jäger zur
   Hütte zurückkehren.
3. Erst nach diesem Abbruch wurde Ziel `16/410` beim späteren Zielwechsel mit
   dem 30-Sekunden-Target-Cooldown belegt. `cooldown=True` ist damit eine Folge,
   nicht die Ursache des ersten Abbruchs. Der Cooldown verschärft die Situation,
   weil das weiterhin erreichbare erste Ziel vorübergehend nicht neu gewählt
   werden kann.
4. Ab `22:19:40.429` wählte der Jäger wiederholt Reh `17/319`. `MoveHere`
   akzeptierte jedes Mal einen gültigen kurzen Pfad, doch ungefähr 22 bis 26 ms
   später versuchte Vanilla bereits bei Pfadfortschritt `1` direkt anzugreifen.
   Alle beobachteten Aufrufe endeten mit `attackResult=0`. Die folgende
   State-6-Rückkehr und Neusuche ungefähr alle 0,85 Sekunden erzeugten das
   sichtbare Hin-und-her vor der Hütte.

Der PCL-Cache ist in diesem Lauf nicht die Ursache. Die blockierte erste
Fortsetzung verwendete gültige `active-target-snapshot`-Treffer; die aggregierte
Diagnose meldete `activeSnapshotMisses=0`. Am entscheidenden Angriffstick wurde
die PCL-Entscheidung nicht wegen Ablauf oder Miss verworfen. Die Lücke liegt vor
der Sicht-/Fortsetzungsentscheidung.

### Korrigierte Vanilla-Ursache

`HunterUpdate` verwendet zwei verschiedene Distanzmetriken:

- Der bisher maßgebliche Refresh-Hook bei RVA `0x130019` prüft den von der
  zweiten `0x79C0`-Abfrage gelieferten Maximalabstand der Weltkoordinaten
  `+0x70E/+0x710` gegen `20`.
- Der spätere State-1-Bewegungs- und Angriffspfad behält in `EDI` das Ergebnis
  der ersten `0x79C0`-Abfrage über die Tile-Koordinaten `+0x71C/+0x71E`. Bei RVA
  `0x1300EA` wird dieser Wert gegen `28` geprüft; Werte `<=28` erreichen den
  direkten Angriff bei `0x13013D`.

Die Logs bestätigen die zweite Metrik unmittelbar. Bei Jäger-Tile `387,354`
und Beute-Tile `378,354` betrug `nativeDistance=9`; bei `389,346` zu `393,344`
betrug sie `6`. Beides entspricht der Manhattan-Distanz der Tile-Koordinaten.
Die Weltkoordinaten können währenddessen außerhalb des bisherigen
`0..28`-Vorbereitungsfensters liegen. Der direkte Angriff ist deshalb auch in
einem `HunterUpdate` erreichbar, in dem der Hook bei `0x130019` keine
Sichtentscheidung und kein einmaliges Ticket vorbereitet hat.

Die bisherige Annahme „Refresh-Unterdrückung und Angriffsvermeidung gehören
durch das Einzelticket garantiert zum selben Update“ ist nur für Updates wahr,
die den Welt-Nahbereichsrefresh tatsächlich durchlaufen. Sie deckt nicht den
gesamten Tile-Distanz-28-Angriffszweig ab und muss ersetzt werden.

### Verbindliche Implementierungsempfehlung für den nächsten Chat

1. Keinen eigenen Move, keine Animation, keine Speedfelder, keinen AI-State und
   keinen neuen Recovery-Pfad setzen. Der bestehende validierte Hook bei
   `0x1300EA` bleibt die kleinste geeignete Eingriffsstelle.
2. Den Tile-Distanz-28-Zweig zur maßgeblichen Angriffs-/Fortsetzungsentscheidung
   machen. Bei derselben lebenden Hunter-/Beuteidentität, State `1`, Reservation
   `2`, aktivem unvollständigem Vanilla-Pfad, positivem aktivem PCL-Snapshot und
   blockierter Sicht darf ausschließlich `EDI` auf Vanillas Wert `29` gesetzt
   werden. Dadurch wählt Vanilla selbst seine vorhandene Bewegungs- und
   Locomotionstufe und erreicht den direkten Angriff in diesem Update nicht.
3. Die Freigabe bei `0x1300EA` darf nicht länger ausschließlich von dem
   kurzlebigen Einzelticket des Welt-Nahbereichsrefreshs abhängen. Der Hook bei
   `0x130019` bleibt nötig, um bei blockierter Sicht Vanillas zerstörerische
   Zielquery zu überspringen, ist aber nicht mehr die alleinige Autorisierung
   für die spätere Pfadfortsetzung.
4. Einen getrennten, identitätsgebundenen aktiven Sicht-Snapshot einführen.
   Der vorhandene persistente Native-Scan soll ein unverändertes aktives Ziel
   außerhalb des Inline-Hooks höchstens einmal pro Sekunde mit Wrapper plus
   bidirektionalem Kern prüfen. Ein Ziel-, Global-ID-, Spieler-, Karten- oder
   relevanter Zustandswechsel erzwingt sofort eine neue Probe. Ein Snapshot darf
   höchstens ungefähr zwei Sekunden lesbar bleiben. Die vom Benutzer akzeptierte
   Reaktionsverzögerung bis ungefähr eine Sekunde ist dem häufigen nativen
   Sichtaufruf im Inline-Hook vorzuziehen.
5. Für ein neu angenommenes Ziel darf ein noch ausstehender erster Snapshot den
   bekannten zerstörerischen Angriff nicht sofort freigeben. Empfohlen ist ein
   klar begrenzter `visibility-pending`-Zeitraum bis zur ersten, sofort
   angeforderten Scanprobe, in dem ein gültiger aktiver Pfad über Vanilla
   fortgesetzt wird. Ist die Sichtprobe dauerhaft nicht verfügbar oder fehlerhaft,
   muss der Fall gedrosselt und explizit geloggt werden; keine unbegrenzte
   unsichtbare Sondersteuerung einführen.
6. `blocked-wrapper-both-directions` und
   `blocked-directional-disagreement` wählen Pfadfortsetzung. Nur zwei positive
   Kernrichtungen wählen `visible-attack-handoff` und lassen den
   Distanz-28-Angriffspfad unverändert. Die Live-Projektil-Kollisionsroutine
   `0x9C730` bleibt als Vorabprüfung verboten.
7. Snapshotaktualisierung, Entscheidung bei `0x130019`, Entscheidung bei
   `0x1300EA` und späteres Vanilla-Angriffsergebnis getrennt kapseln und loggen.
   Ein Diagnosefehler darf die Verhaltenskorrektur nicht verhindern; Vanilla
   darf pro Update nur einmal laufen.
8. Der 30-Sekunden-Abbruchcooldown ist erst nach Behebung der eigentlichen
   Angriffslücke neu zu bewerten. Er war im aktuellen Test nicht die Ursache.
   Ihn jetzt als Symptomkorrektur zu ändern würde den fehlerhaften Übergang nur
   verdecken.

Das neue begrenzte Log soll für stabile Hunter-/Beuteidentitäten mindestens
folgende Werte nennen: `tileAttackDistance`, aktuellen oder zuletzt erfassten
`worldRefreshDistance`, Sicht-Snapshotstatus und -alter, Wrapper- und beide
Kernresultate, PCL-Snapshotstatus und -alter, Reservation, Pfadzustand,
Pfadfortschritt/-länge sowie eine eindeutige Aktion
`continue-vanilla-path`, `allow-vanilla-attack`, `visibility-pending` oder einen
expliziten Ablehnungsgrund. Wiederholungen gleicher Entscheidungen drosseln,
Zustandswechsel und den ersten echten Callback immer loggen.

### Noch ausstehende Paket-E-Tests

Nach der Implementierung sind alle folgenden Tests Pflicht; ein einzelner
bewegter Freisichtlauf genügt nicht:

1. **Reproduktion der aktuellen Distanzmetrik-Lücke:** Ein Jäger erhält bei
   kurzer Tile-Luftlinie einen langen, aber erreichbaren Weg um eine diagonale
   Mauer. Das Reh darf sich zufällig bewegen. Solange die Sicht blockiert ist,
   muss der Pfad auch dann fortschreiten, wenn `tileAttackDistance<=28`, aber
   kein Welt-Nahbereichsrefresh unmittelbar vorausging. Es darf kein
   `attackResult=0`, State-6-Abbruch, neuer `MoveHere` oder Target-Cooldown für
   dieselbe laufende Identität entstehen.
2. **Blockiert zu sichtbar:** Läuft dasselbe Reh dem Jäger entgegen oder kommt
   der Jäger um die Ecke, muss der aktive Sicht-Snapshot spätestens nach ungefähr
   einem Probeintervall auf `visible-attack-handoff` wechseln. Danach bleibt
   `EDI` unverändert, Vanillas direkter Angriff liefert ein positives Ergebnis,
   ein echtes Projektil entsteht und Tod, Kadaverweg, Pickup sowie Abgabe laufen
   normal. Gerade dieser zufällige Bewegungsfall ist gültig und muss nicht durch
   einen Reh-Freeze künstlich stabilisiert werden.
3. **Freier Kontrollfall:** Ein ohne Hindernis erreichbares Reh wird ohne
   Rückkehrloop und ohne merklich mehr als ungefähr eine Sekunde zusätzliche
   Verzögerung angegriffen. Keine blockierte Klassifikation darf einen dauerhaft
   sichtbaren Fall festhalten.
4. **Dauerhaft blockiert, aber erreichbar:** Über mindestens zwei Sichtproben
   müssen Identität und Pfad stabil bleiben und der Fortschritt zunehmen. Keine
   wiederholte Animationsrücksetzung auf MoveHere-Startframe `657`, keine
   Sitzanimation und kein Pendeln vor der Hütte.
5. **Nachträglich vollständig unerreichbar:** PCL `0` muss weiterhin über den
   bestätigten aktiven-Ziel-Pfad invalidieren. Nach
   `active-target-pcl-disconnected` darf keine Distanz-29-Fortsetzung für die
   alte Identität mehr erfolgen; Vanilla sucht oder kehrt regulär zurück.
6. **Restweg und Animation:** Kurze Luftlinie mit langem Restweg verwendet
   weiterhin die bereits bestätigten schnelleren Vanilla-Stufen. Der Wechsel
   der Stufen erzeugt weder Ruckeln noch falsche Animation oder übernatürliche
   Geschwindigkeit; die letzte sichtbare Schussannäherung bleibt langsam.
7. **Mehrere Jäger und Lebenszyklus:** Mindestens zwei Jäger besitzen getrennte
   Sicht-, PCL- und Pfadzustände. Mod-Aus, Kartenneustart und Ziel-/Slotwechsel
   hinterlassen keine Snapshots oder Tickets der alten Identität.

Maschinelle Logabnahme: erwartete Initialisierungs- und Callbackmarker müssen
vorhanden sein; kein `Improved Hunters ... failed`, keine Callbackexception,
kein harter Prozessabbruch und kein `TEST-ONLY deer freeze`-Marker. Für jeden
unterdrückten Angriff muss eine vorherige oder gültig gecachte blockierte
Sichtentscheidung derselben Identität existieren. Für jeden freigegebenen
Angriff muss ein frischer beidseitig positiver Snapshot und anschließend ein
positives Vanilla-Angriffsergebnis vorliegen.

Gate: Paket E bleibt offen. Erst wenn die Distanzmetrik-Lücke implementiert ist
und alle obigen Abnahmepunkte bestehen, darf Paket F begonnen werden.

### Implementierter Folgestand `1.1.56` – Ingame-Abnahme ausstehend

`1.1.56` setzt die oben verbindlich festgelegte Richtung um, ist aber noch kein
bestandener Paket-E-Stand:

- `HunterActiveTargetVisibilitySnapshot.cs` erfasst pro Jäger genau eine an
  Hunter-/Beute-Global-ID, Spieler, Kartengeneration, State, Reservation und
  Pfadidentität gebundene aktive Sichtbeobachtung. Der persistente Native-Scan
  prüft ein unverändertes Ziel höchstens einmal pro Sekunde; der Snapshot bleibt
  höchstens zwei Sekunden lesbar.
- Ein neues Ziel erhält bis zur ersten sofort angeforderten Scanprobe ein auf
  zwei Sekunden begrenztes `visibility-pending`. Danach bleibt ein fehlender
  oder fehlerhafter Snapshot fail-open; die Sondersteuerung wird nicht
  unbegrenzt fortgesetzt.
- Der Hook bei `0x1300EA` benötigt weiterhin State `1`, Reservation `2`, einen
  aktiven unvollständigen Pfad und einen positiven aktiven PCL-Snapshot. Er
  entscheidet nun ohne Welt-Refresh-Ticket: blockiert oder pending setzt nur
  `RDI` auf `29`; ein frischer beidseitig positiver Snapshot lässt Vanillas
  Angriff unverändert laufen.
- Der Hook bei `0x130019` konsumiert denselben Snapshot nur noch für das
  Überspringen der zerstörerischen Welt-Nahbereichsquery. Er ist nicht mehr die
  Autorisierung des späteren Tile-Distanz-Zweigs.
- Nach einem erfolgreichen Vanilla-`MoveHere` wird ein noch frischer positiver
  PCL-Auswahlcache ohne neue native PCL-Abfrage in den aktiven Snapshot
  überführt. Dadurch besitzt auch der erste Angriffstick vor dem nächsten
  100-ms-Scan die vorgeschriebene positive PCL-Autorisierung.
- Das begrenzte Log nennt Tile-Angriffsdistanz, letzten Welt-Refresh samt Alter,
  Sicht- und PCL-Snapshotstatus/-alter, Wrapper und beide Kernrichtungen,
  Reservation, Pfadstand sowie die eindeutige Aktion.

Nächster Schritt ist ausschließlich die bereits oben definierte siebenstufige
Ingame- und Logabnahme von Paket E. Paket F bleibt bis dahin gesperrt.

### Aktueller Korrekturstand `1.1.57` nach dem `1.1.56`-Test

Der `1.1.56`-Lauf ab eigenem Mod-Zeitstempel `2026-08-17 22:51:11.496`
bestätigte den ersten Teil der Korrektur: Der Jäger setzte seinen gültigen
Vanilla-Pfad fort und brach die Jagd nicht mehr am früheren Distanzübergang ab.
Er lief jedoch bis zur bei `MoveHere` festgelegten alten Position der bewegten
Beute, statt bei zwischenzeitlich freier Sicht Vanillas Angriff zu überlassen.

Die Sichtprobe selbst erkannte den Übergang. Im Lauf entstanden 219 aktive
Sicht-Snapshots, davon 51 `visible-attack-handoff` und 168
`blocked-wrapper-both-directions`. Für Ziel `61/460` war die Sicht von
`22:52:25.451` bis `22:52:26.573` beidseitig positiv und ab `22:52:26.674`
wieder blockiert. Gleichzeitig wechselte `GameUnit +0xF4` während desselben
Pfades unter anderem `4 -> 5 -> 7 -> 1 -> 4 -> 6`.

`VisibilityInputs.Equals(...)` und `GetHashCode()` behandelten dieses laufende
Locomotion-Unterfeld irrtümlich als Pfadidentität. Dadurch ersetzte nahezu jeder
100-ms-Scan den Tracker, forderte sofort eine weitere Probe an und lieferte dem
Tile-Hook zwischen Probe und Entscheidung erneut `new-identity-pending`. Bis
zum gemeinsamen Diagnoselimit wurden 241 Tile-Entscheidungen erfasst: 233
`visibility-pending`, acht `continue-vanilla-path` und kein
`allow-vanilla-attack`. Das Limit `600/600` war bereits um `22:52:14.992`
erreicht; die konkrete Tile-Entscheidung für Ziel `61/460` wurde daher nicht
mehr geloggt, der identische Resetpfad ist aber im Code und in den vorherigen
Entscheidungen direkt belegt. PCL war positiv; Callback- oder Scanfehler traten
nicht auf.

`1.1.57` behält `+0xF4` ausschließlich als Diagnosewert. Die stabile
Sichtidentität umfasst weiterhin Hunter-/Beuteslot und Global-ID, Beutetyp,
Spieler, Kartengeneration, AI-State, Pfadzustand, Pfadlänge und Reservation.
Der vorwärts laufende Pfadfortschritt ersetzt den Tracker nicht; nur ein
Rücksprung gegenüber dem zuletzt beobachteten Fortschritt erzwingt zusätzlich
eine sofortige neue Probe als Pfadneustart. Damit bleiben Ein-Sekunden-Takt,
Zwei-Sekunden-Lesefenster und der sichtbare Snapshot über normale
Locomotion-Unterstufen hinweg erhalten.

Paket E bleibt **offen**. Nächster Pflichtschritt ist ein erneuter Ingame-Lauf
des bewegten Übergangsfalls. Erwartet werden höchstens ungefähr eine Sichtprobe
pro Sekunde bei unverändertem Ziel, mindestens ein `allow-vanilla-attack` mit
frischem beidseitig positivem Snapshot sowie anschließend ein positives
Vanilla-Angriffsergebnis. Danach bleiben die übrigen siebenstufigen Paket-E-
Szenarien und die maschinelle Logabnahme erforderlich.

### Explizite Pfadgeneration `1.1.58` nach dem `1.1.57`-Test

Der `1.1.57`-Lauf ab eigenem Mod-Zeitstempel `2026-08-17 23:16:25.697`
bestätigte, dass die Reichweite nicht hart verkürzt wurde: Der Tile-Hook
protokollierte 86 `allow-vanilla-attack` zwischen Distanz `1` und `28`, und
positive Vanilla-Angriffsergebnisse traten unter anderem bei `20`, `15`, `10`
und `9` auf. Der freie Fall Ziel `69/472` wurde bei Tile-Distanz `28`
beidseitig positiv erkannt und an Vanilla freigegeben.

Die Freigabe blieb jedoch nicht stabil. Bereits 91 ms später meldete derselbe
Hunter-/Ziel-/Pfadlängenkontext wieder `new-identity-pending`. Insgesamt traten
147 solche Pending-Neuanfänge auf; 72 von 84 aufeinanderfolgenden
Snapshotintervallen derselben protokollierten Pfadgruppe lagen unter 900 ms.
Ziel `69/472` blieb mindestens bis Pfadfortschritt `19/20` beidseitig sichtbar,
ohne dass ein korrelierter positiver Angriff, Schuss- oder Projektilmarker
folgte. Der Jäger konnte deshalb trotz bereits erkannter Freisicht weiter in
Richtung der alten Zielposition laufen.

Nach Entfernung von `+0xF4` aus der Identität blieb in
`GetOrReplaceTracker(...)` nur `PathProgress < LastPathProgress` als möglicher
Resetgrund bei unveränderten stabilen Feldern. Der Lauf beweist damit, dass
auch `GameUnit +0xF6` innerhalb desselben Vanilla-Pfades nicht zuverlässig
monoton beobachtbar ist. Ein Rücksprung dieses Rohwerts ist kein belastbarer
Pfadneustartbeweis.

`1.1.58` verwendet deshalb die bereits validierte State-0-`MoveHere`-
Ergebnisstelle als einzige zusätzliche Pfadgrenze. Jeder erfolgreiche
Vanilla-`MoveHere` erhöht pro Jäger eine verwaltete Generation. Der nächste
Scan oder Hookzugriff ersetzt den alten Tracker mit dem expliziten Status
`new-path-generation-pending`; `+0xF4` und `PathProgress` bleiben reine
Diagnosewerte. Ziel-/Global-ID-, Typ-, Spieler-, Karten-, AI-State-,
Pfadzustands-, Pfadlängen- und Reservationswechsel bleiben unabhängige stabile
Identitätsgrenzen.

Jede geplante Sichtprobe trägt ihre Pfadgeneration. Vor dem nativen Aufruf, im
Fehlerpfad und vor dem Snapshot-Commit wird geprüft, dass Tracker und aktuelle
Generation noch übereinstimmen. Ein während der Probe erfolgreich angenommener
neuer Pfad kann daher keinen alten Sichtwert erben. Es gibt keinen neuen nativen
Hook, keine zusätzliche PCL- oder Sichtabfrage im Inline-Hook und weiterhin
keine eigene Bewegung, Animation, Speed-, Pfad- oder AI-State-Schreiboperation.

Für auswertbare Folgelogs besitzen Welt-Refresh und Tile-Hook nun getrennte
Drosselungssignaturen. `visibilityPathGeneration` erscheint in beiden
Entscheidungslogs. Erwartet wird pro erfolgreichem `MoveHere` genau ein
Generationswechsel und danach ohne weiteren `MoveHere` weder
`new-path-generation-pending` noch mehr als ungefähr eine Sichtprobe pro
Sekunde. Ein dauerhaft sichtbares Ziel muss wiederholt die Freigabe des
Distanz-Overrides behalten; seit `1.1.60` wird getrennt ausgewiesen, ob Vanillas
nachgeschaltetes Angriffstor bereits bereit ist oder den Angriff noch vertagt.

### Begrenzte Tracker-Retention `1.1.59` nach dem `1.1.58`-Test

Der `1.1.58`-Lauf ab eigenem Mod-Zeitstempel `2026-08-17 23:37:46.127`
bestätigte die neue Pfadgrenze: Fünf erfolgreiche Vanilla-`MoveHere` erzeugten
genau fünf Generationen, innerhalb eines angenommenen Pfades blieb die
Generation stabil. Trotzdem entstanden 172 `new-tracker-pending`- und 352
`visibility-pending`-Entscheidungen bei nur 14
`allow-vanilla-attack`-Entscheidungen.

Die verbleibende Ursache lag im Bereinigungspass des persistenten Scans. Ein
Jäger, dessen vollständiger State-1-/Pfad-/Ziel-/Reservationskontext in einem
einzigen 100-ms-Scan nicht erfassbar war, fehlte sofort in `activeHunterIds`;
der zugehörige Tracker wurde noch in demselben Scan gelöscht. Der Tile-Hook
validierte denselben Kontext häufig wenige Millisekunden später und legte dann
wieder einen leeren Tracker an. Deshalb konnte eine positive Sichtfreigabe vor
Vanillas maßgeblichem Angriffsschritt verschwinden.

Der bewegte Übergangsfall Ziel `4/305` belegt die Wirkung: Bei Tile-Distanz `2`
und Welt-Distanz `9` war der Tracker pending. 463 ms später wurde das inzwischen
wieder entfernte Reh bei Tile-Distanz `10` beidseitig sichtbar, aber bereits
100 ms danach begann erneut `new-tracker-pending`. Am vollständigen Pfad
`58/58` lieferte Vanillas direkter Angriff bei Distanz `18` das Ergebnis `0`.
Der freie Kontrollfall Ziel `40/437` wurde bei Distanz `12` positiv freigegeben,
griff wegen wiederholter Pending-Neuanfänge aber erst 1,5 Sekunden später bei
Distanz `8` erfolgreich an.

`1.1.59` aktualisiert die letzte vollständige Validierung sowohl im
persistenten Scan als auch bei einem vollständigen Inline-Hook-Zugriff. Ein
einzelner Scanfehler löscht den Tracker nicht mehr. Erst wenn zwei Sekunden lang
weder Scan noch Hook denselben vollständig gültigen Kontext bestätigen, wird
er entfernt. Ein ungültiger Live-Kontext kann den erhaltenen Snapshot nicht
lesen, weil `TryGetObservation(...)` vor dem Trackerzugriff weiterhin alle
Hunter-, Ziel-, Pfad- und Reservationsbedingungen prüft. Echte stabile
Identitätswechsel und eine neue akzeptierte `MoveHere`-Generation ersetzen den
Tracker unverändert sofort.

Der erste zurückgehaltene Scanfehler nennt nun seinen konkreten Capture-Grund;
eine wirkliche Ablaufbereinigung nennt Abwesenheits- und Validierungsalter. Es
gibt weiterhin keinen eigenen Move, keine Reichweiten-, Cooldown-, Pfad-,
Animations-, Speed- oder AI-State-Schreiboperation. Pflichtschritt bleibt die
erneute Ingame-Abnahme des bewegten Übergangs und des freien Kontrollfalls.

### `1.1.60`-Diagnose nach dem `1.1.59`-Test

Der `1.1.59`-Lauf ab Mod-Zeitstempel `2026-08-17 23:58:48` bestätigt die
Retention: `new-tracker-pending` sank von 172 auf 2 und `visibility-pending`
von 352 auf 13. Acht erste Scanlücken wurden begrenzt erhalten; alle nannten
`prey-reservation-1`, während der maßgebliche Inline-Hook denselben Zielpfad
mit Reservation `2` validierte. Der Rohwert ist damit phasenabhängig und darf
nicht ungeprüft als neue stabile Identität oder als Schreibziel behandelt
werden.

Die Angriffsfolge zeigt zwei getrennte Verzögerungen. Alle sieben direkten
Vanilla-Angriffsaufrufe fanden erst bei `+0xF4 == 0` statt. Jede vorherige
positive Sichtfreigabe lag dagegen bei einem noch positiven `+0xF4`-Wert. Das
bisherige Logwort `allow-vanilla-attack` bedeutete daher nur, dass der Mod den
Distanzwert nicht mehr auf `29` anhob; es bewies keinen Angriff in demselben
Update. Zusätzlich lieferten mehrere optisch als frei beobachtete Annäherungen
über Sekunden Wrapper `0` und wurden erst bei sehr kurzer Tile-Distanz positiv.
Die bisherigen Änderungslogs enthielten weder die exakten Probeendpunkte noch
explizite Kernrichtungen im Wrapper-Nullfall, sodass Geometrie, Scanphase und
tatsächliche Sichtsemantik nicht sicher getrennt werden konnten.

`1.1.60` protokolliert deshalb jede höchstens einmal pro Sekunde ausgeführte
aktive Probe separat und begrenzt auf 240 Einträge. Der Eintrag enthält Hunter-
und Beute-Tile, Weltkoordinaten, native Höhenendpunkte, Manhattan-Tile-Distanz,
Chebyshev-Weltdistanz, Wrapper, beide explizit aufgerufenen validierten
Kernrichtungen, Pfad, Reservation und Generation. Der Wrapper bleibt für die
Klassifikation autoritativ; die zusätzlichen Kernaufrufe ändern keinen
Sichtwert. Das Tile-Log unterscheidet nun
`release-distance-override-vanilla-attack-gate-ready` von
`release-distance-override-vanilla-attack-gate-deferred`.

Die fachlich beste Korrekturrichtung ist **kein** pauschales Verkürzen aller
Timer und kein Schreiben von `+0xF4`. Wenn die neuen Daten zeigen, dass eine
korrekte sichtbare Probe wegen Bewegung veraltet, muss der Snapshot an seine
Probe-Tilepositionen gebunden und im Angriffsnahbereich begrenzt häufiger
erneuert werden. Nur wenn danach eine frische, positionsgleiche positive Probe
reproduzierbar an `+0xF4` hängen bleibt, darf ein eigener vollständig
span-/kontrollflussvalidierter Handoff am nativen Gate untersucht werden, der
in Vanillas unveränderte direkte Angriffssequenz führt. Liefert dagegen bereits
der Wrapper samt beiden Kernen auf freier Geometrie reproduzierbar `0`, ist
zuerst die native Höhen-/Sichtsemantik zu korrigieren; ein Gate-Bypass würde
sonst nur Fehlschüsse und den belegten State-6-/Hüttenpfad beschleunigen.

### Paket F: Unreservierte Kadaver als Abholkandidaten

Paket F folgt auf E und wird ebenfalls separat entwickelt und abgenommen.

Fachliche Regel:

- Tote, noch verwertbare Tiere mit Reservation `0` dürfen Kandidaten sein.
- Tote und lebende Tiere verwenden dieselbe Fleisch-pro-Zykluskosten-Rangfolge.
- Ein gültiger Kadaver erhält einen kalibrierten Bonus von ungefähr fünf
  Sekunden. Es gibt keine zweite pauschal vorrangige Kadaverliste.
- Der Bonus gilt identisch in grober Sortierung und endgültiger `CycleCost`.
- Ein naher oder ähnlich teurer Kadaver wird wahrscheinlicher gewählt; ein weit
  entfernter Kadaver darf gegen deutlich günstigere lebende Beute verlieren.

Analyse und Entwicklung:

1. Native Corpse-/AI-Zustände, Kadaverflag, Alive-State, Gesundheit,
   Verwertbarkeit und Reservation für abholbare, reservierte und verbrauchte
   Kadaver kalibrieren. Slot und Global-ID bilden gemeinsam die Identität.
2. Den frühesten Vanilla-Pfad bestimmen, der einen vorhandenen Kadaver
   reservieren, anlaufen und aufnehmen lässt. Vor dieser Validierung keinen
   AI-State schreiben, keinen Recovery-Move und keinen künstlichen
   Fleischgewinn erzeugen.
3. Nur aktivierte Beutetypen im normalen Suchradius und mit positiver
   PCL-Erreichbarkeit in den bestehenden Zielcache aufnehmen.
4. `50` Bonuspunkte erst verwenden, wenn ungefähr zehn Kostenpunkte pro Sekunde
   bestätigt sind; andernfalls den äquivalenten Wert aus nativen Zeitstufen
   ableiten.
5. Reservation und Identität unmittelbar vor Vanilla-Handoff erneut prüfen.
   Mehrere Jäger dürfen denselben Kadaver nicht übernehmen.
6. Verschwindet der Kadaver, wird fremdreserviert oder PCL-getrennt, über
   Vanillas Zielverlustpfad neu suchen. Keinen Jäger löschen und keinen langen
   Identitäts-Cooldown setzen.
7. Pickup und Abgabe vollständig über Vanilla laufen lassen. Die bestehende
   Kurzzeitkadaver-Erhaltung nur nach belegter Semantik verändern.
8. Technische Kadavererkennungsfehler dürfen keinen unbekannten oder
   unverwertbaren Kadaver künstlich auswählbar machen.

Abnahme:

- Kadaver und lebendes Tier mit ähnlichen Gesamtkosten: Der Bonus erhöht
  reproduzierbar die Auswahlchance des Kadavers.
- Weiter Kadaver und deutlich näheres lebendes Tier: Das lebende Tier gewinnt.
- Nur ein erreichbarer unreservierter Kadaver: Der Jäger holt ihn ohne neuen
  Schuss ab.
- Unerreichbarer Kadaver: Er wird verworfen und nach Öffnen eines Zugangs
  zeitnah wieder zugelassen.
- Zwei Jäger und ein Kadaver: Genau ein Jäger reserviert und holt ihn.
- Kadaver verschwindet oder wird fremdreserviert: Der Jäger sucht kontrolliert
  neu.
- Fleischabgabe und Kadaverbereinigung bleiben Vanilla-konform.

Gate: Gemeinsame Kostenentscheidung, kalibrierter weicher Bonus, keine
Doppelreservation und vollständiger Vanilla-Pickup-/Abgabepfad.

### Paket D: gemeinsame Beutetyp- und Mehrfachmatrix

Erst nach E und F wird die gemeinsame Infrastruktur für Reh, Ziege, Hase,
Kamel, Huhn und Kuh geprüft:

- freier Kontrollfall,
- sichtblockiert, aber erreichbar,
- vollständig unerreichbar,
- Zugang wird später geöffnet,
- aktives Ziel wird nach Auswahl unerreichbar,
- bewegte Beute,
- lange tatsächliche Reststrecke und passende Vanilla-Bewegungsstufe,
- lebende Beute und unreservierte Kadaver in derselben Rangfolge,
- echtes Projektil, korrekter Kadaver, Pickup und Fleischabgabe,
- mehrere Jäger und mehrere Tiere beziehungsweise Kadaver ohne gemeinsame
  Reservierung oder gegenseitiges Zielüberschreiben,
- Jägerhütte und andere Gebäude als Sichtblocker.

Offener Codepunkt: `ImprovedHuntersViewModel` kennt `HuntCow`, aber
`ImprovedHuntersRuntime.IsRuntimeHuntingEnabled` schließt `CHIMP_TYPE_COW`
derzeit ausdrücklich aus. Vor dem Kuhtest muss bewusst entschieden und im Code
vereinheitlicht werden, ob Kuhjagd unterstützt wird. Das fachliche Ziel dieses
Plans schließt Kühe ein.

Gate: Alle unterstützten Typen zeigen dieselbe Semantik; typabhängige
Behandlungskosten und Fleischmengen beeinflussen nur die vorgesehene Rangfolge.

### Paket C: zuletzt in Produktionsstruktur überführen

Paket C beginnt erst nach Abnahme von E, F und D. Es ist keine neue
Verhaltensentwicklung, sondern die Integration bereits belegter Bausteine.

1. Fallback-Verhalten aus `HunterTargetSearchFallbackDiagnostic.cs` in eine
   passend benannte Produktionsklasse verschieben; nur temporäre Marker in
   einer separaten Diagnosedatei behalten.
2. `HunterVanillaPathContinuationDiagnostic.cs` und die Reststreckenlogik in
   produktive Hooks/Guards und entfernbares Detail-Logging trennen.
3. Kadaverkandidaten und Bonus in die gemeinsame produktive Rangfolge
   integrieren; keinen parallelen Auswahlpfad behalten.
4. `HunterPclReachabilityDiagnostic.cs` entfernen.
5. `HunterVisibilityDiagnostic.cs` entfernen, sofern kein abgenommener Marker
   daraus benötigt wird.
6. Den stillgelegten `HunterLineOfSightRecovery.cs`-Adapter und seine
   Runtime-Anschlüsse entfernen; keinen alten Fallback parallel behalten.
7. Namen, Logs und Initialisierungszusammenfassung von „diagnostic“ auf den
   Produktionsstatus korrigieren. Irreführende Felder wie den heutigen
   `orderTargetGlobal`-Marker korrigieren oder entfernen.
8. Deaktivierungs-, Kartenwechsel- und Fail-open-Pfade nochmals auditieren.
9. `UpdateToNewDLL.md`, Changelog und Versionsnummer aktualisieren.
10. Alle Codeprüfungen und CRLF-Kontrollen vornehmen und danach genau einmal
    `ImprovedHunters\build.bat /nopause` ausführen.

Gate: Mod-Aus, `ImprovedPathfinding`-Aus, deaktivierter Beutetyp, Kartenwechsel
und echter Multiplayer führen zu keinem verhaltensändernden Hookpfad.
Singleplayer-Skirmish und -Trail behalten alle in A, B, E, F und D bestätigten
Funktionen.

### Optionalpaket: sichtbarer Jagdsprint nach Abschluss aller Pflichtpakete

Dieses Paket ist ausdrücklich kein Bestandteil der Paket-E-Abnahme und wird
erst begonnen, wenn `E → F → D → C` vollständig funktioniert und regressiert
ist. Ein Verzicht darauf lässt das fachliche Kernziel abgeschlossen.

Bisherige Erkenntnisse aus der kanonischen DLL und dem `1.1.49`-Log:

- Die State-1-Distanzleiter besitzt keine schnellere bestätigte Jagdstufe als
  den Wert `1` für Distanzwerte über `40`. Die Restwegkorrektur wählte bei
  dekodierter Restmetrik `109` bereits `selectedDistance=41`, `routeSpeed=1`
  und beobachtete anschließend `currentSpeedBefore=1`. Ein noch größerer
  künstlicher Wert in `RDI` ändert die Stufe daher nicht.
- Die numerisch kleineren Werte der bestätigten Leiter sind die schnelleren
  Bewegungsstufen. Eine unvalidierte Stufe `0` darf nicht ausprobiert oder
  direkt in ein Unitfeld geschrieben werden.
- Der optisch schnelle Lauf zum erlegten Tier liegt im separaten Hunter-State
  `2`. Der Writer ab RVA `0x1306E8` wählt den bestätigten Speedwert `2` bei
  `0x130721`, verwendet aber bei `0x13070D` zusätzlich die anderen
  Locomotion-/Animationssteuerwerte `0x101` beziehungsweise `0x689`. Der ferne
  State-1-Jagdpfad schreibt dagegen Speedwert `1` und Steuerwert `1` ab
  `0x130068`. Die sichtbare Laufanimation beweist deshalb noch keine höhere
  tatsächliche Felder-pro-Sekunde-Geschwindigkeit.
- Den State `2` für lebende Beute vorzutäuschen wäre fachlich falsch: Dieser
  Pfad enthält Kadaver-, Pickup- und Folgezustandssemantik. Auch ein direktes
  Kopieren seiner Steuerwerte in State `1` ist ohne Kalibrierung nicht sicher.

Vorgehen, falls das Optionalpaket später gewünscht bleibt:

1. State `1` mit Speedwert `1` und den echten State-2-Kadaverlauf rein
   beobachtend anhand Pfadfortschritt und Weltposition pro Zeit vergleichen.
2. Neben Speedwert, AI-State und Pfadidentität die Locomotion-, Animations- und
   Advance-Control-Felder vor, während und nach beiden Läufen protokollieren.
3. Einen echten State-1-kompatiblen Vanilla-Laufmodus suchen. Nur wenn dessen
   Semantik und Rückkehr zur Schussannäherung belegt sind, darf der kleinste
   stabile Vanilla-Übergang ausgewählt werden.
4. Kein eigener Move, kein AI-State-Wechsel, keine direkte Speedstufe `0` und
   kein bloßes Kopieren von State-2-Feldern. Neue Hooks unterliegen dem
   vollständigen Überschreibspannen- und Inbound-Branch-Audit.

Gate: Nur eine messbar höhere tatsächliche Reisegeschwindigkeit bei korrekter
Animation, unveränderter Ziel-/Pfadidentität und sauberer Rückkehr zu Vanillas
langsamer Schussannäherung rechtfertigt eine Implementierung. Andernfalls bleibt
die bestehende schnellste State-1-Stufe unverändert.

## Multiplayer-Chore ab Script Extender 1.50.0

Multiplayer-Unterstützung bleibt Endziel, wird aber erst gebaut, wenn der
kanonische Script Extender mindestens `1.50.0` erreicht. Bis dahin bleiben
simulationsrelevante PCL-Filter, Kandidaten-Handoff, Jägerhüttenpatch und
Pfadfortsetzung in echtem Multiplayer fail-closed.

Der Chore **Hunter-Recovery-Multiplayer-Synchronisation** umfasst:

1. Mit `Shared/GameModeHelper.cs` Host, Client, Singleplayer-Skirmish, Trail
   und Multiplayer-Save unterscheiden. `IsNetworkedEnvironment()` allein ist
   kein Multiplayerbeweis.
2. Klären, ob Zielentscheidung und Distanzstufenwahl autoritativ repliziert
   oder lockstep deterministisch auf allen Peers ausgeführt werden müssen.
3. Keine zweite Multiplayer-Zielwahl bauen; die bestehende Pipeline mit
   stabilen Slot-/Global-IDs synchronisierbar machen.
4. Für eigene Nachrichten expliziten `IMessagePackFormatter<T>` mit stabilen
   numerischen Keys verwenden; keine Contractless-Serialisierung.
5. Host-/Client-, Save/Load-, Reconnect-, Zielwechsel- und Desync-Tests mit
   mehreren Jägern durchführen.

Gate: Bis zum Abschluss bleibt echter Multiplayer deaktiviert. Danach dürfen
Host, Client und Multiplayer-Saves weder doppelte Moves noch abweichende Ziele
erzeugen.

## Dateien und Verantwortlichkeiten

| Datei | Verantwortung und geplanter Umgang |
| --- | --- |
| `src/HunterPclReachability.cs` | Produktiver PCL-Vorfilter mit Ein-Sekunden-Auswahlcache sowie aktive State-1-Zielprüfung mit getrenntem Ein-Sekunden-Probe-/Zwei-Sekunden-Snapshot, Statistiken und Fail-open-Verhalten |
| `src/HunterPclReachabilityDiagnostic.cs` | Temporäres Kalibrierungslogging; in Paket C entfernen |
| `src/HunterNativeVisibilityProbe.cs` | Validierte native Wrapper-/Kernsichtprobe; wird vom aktiven Sicht-Snapshot außerhalb des Inline-Hooks aufgerufen |
| `src/HunterActiveTargetVisibilitySnapshot.cs` | Seit `1.1.59` an explizite erfolgreiche-`MoveHere`-Generationen gebunden und über einzelne transiente Scanlücken hinweg begrenzt erhalten; `1.1.60` ergänzt pro Ein-Sekunden-Probe die exakte Geometrie und beide Kernrichtungen, ohne die Zwei-Sekunden-Klassifikation oder Verhalten zu ändern; erneute Ingame-Abnahme ausstehend |
| `src/HunterHutVisibilityPatch.cs` | Produktive, validierte Ein-Byte-Korrektur der Jägerhüttenausnahme |
| `src/HunterTargetSearchFallbackDiagnostic.cs` | Verhalten und Diagnose derzeit gemischt; in Paket C trennen und umbenennen |
| `src/HunterVanillaPathContinuationDiagnostic.cs` | `1.1.56` entscheidet am Tile-Distanz-28-Hook anhand aktiver Sicht und PCL; keine Ticket-Abhängigkeit mehr, Ingame-Abnahme ausstehend |
| `src/HunterVisibilityDiagnostic.cs` | Breite ältere Diagnose; in Paket C entfernen, falls nicht mehr benötigt |
| `src/HunterLineOfSightRecovery.cs` | Stillgelegter Managed-A*-Adapter; in Paket C ohne parallelen Fallback entfernen |
| `src/ImprovedHuntersRuntime.cs` | Events, Eligibility, Rangfolge, PCL-Gates, Handoff, Reservierungsbereinigung, Pfadfortsetzung und Projektilkompensation |
| `UpdateToNewDLL.md` | Maßgebliche Quelle für Hash, RVAs, Pattern, Resolver und Updateaudit |

Neue Ingame-Diagnose gehört immer in eine eigene, leicht entfernbare Datei.
Beobachtung und Verhaltensänderung benötigen getrennte Fehlerpfade. Bei der
späteren Bereinigung dürfen notwendige Hooks nicht mit temporären Logs entfernt
werden.

## Sicherheitsgrenzen und verworfene Ansätze

### Kein synchrones Managed-A*

`GameTileManagerAPI.FindPath` ist ein verwalteter A*-Pathfinder über bis zu
`800 x 800` Tiles. Open-Set-Operationen sind linear; es gibt kein hartes
Expansions-, Zeit- oder Abbruchbudget. Der erste unerreichbare Beutefall fror
den Spielthread ein.

Dauerhafte Regeln:

- kein synchrones `FindPath` in Rangfolge, Recovery oder Schusstilesuche,
- Chebyshev-Kosten bleiben nur Heuristik,
- PCL `0` ist der schnelle Negativfilter,
- PCL positiv bleibt Vanillas Detailpfad überlassen.

### Keine eigene Move- oder AI-State-Rekonstruktion

Die Versuche `1.1.31` bis `1.1.35` zeigten:

- Eigene `MoveHere`-Aufrufe können einen Pfad liefern, werden aber von Vanillas
  Hunter-State-Writer überschrieben.
- Nachträgliches Schreiben von Ziel, Reservation und AI-State `1` bildet den
  atomaren State-0-Erfolgspfad nicht korrekt nach.
- Längere Querysperren banden Jäger an veraltete Positionen und verursachten
  fehlende Rücklaufanimation sowie Hin-und-her.

Daher kein eigener Move, kein direktes Setzen des AI-State und kein ungeprüftes
Leeren oder Setzen weiterer Targetfelder als Recovery-Lösung. Die einzige
gezielte Invalidierung ist der bereits bestätigte State-1-Pfad über die
gespeicherte Ziel-Global-ID und passende Reservation.

### Keine unsicheren Diagnosehooks

- Frühere Inline-Hooks in ungeeigneten Fenstern oder im gemeinsamen
  Sichthelper verursachten CTD beziehungsweise unsichere Callbackkontexte.
- Die Crashstellen `0x18EE14`, `0x130171` und `0x12FF53` nicht erneut hooken.
- Neue Hooks benötigen Bytevalidierung, sicheres Hookfenster, Hashbindung oder
  eindeutigen Resolver und einen getrennten Fehlerpfad.

### Kein `KillUnit`-Fallback

Der alte `KillUnit`-Fallback erzeugte nach einem steckengebliebenen Pfeil den
nicht einsammelbaren Zustand `0x6F`. Seit `1.1.27` wird nur Vanillas
`DamageUnitRanged` mit echter Projektil- und Zielidentität verwendet. Das ist
ein Post-shot-Sicherheitsnetz, keine Pre-shot-Sichtlinienlösung.

## Native Referenzdaten

Kanonische installierte DLL:

- Steam Build ID: `24651686`
- Dateigröße: `3.450.880` Byte
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

| RVA | Bedeutung |
| ---: | --- |
| `0x79C0` | Hunter-Manhattan-Distanz |
| `0xE2610` | Spielerabhängige PCL-Erreichbarkeitsprüfung hinter der Script-Extender-API |
| `0xA06F0` | Gemeinsamer Sichtwrapper |
| `0x9E350` | Sicht-/Geometriekern |
| `0x6B990` | Höhen-/Hindernishelper |
| `0x6B9F8` | Gebäudetyp-Switch |
| `0x6BAC4` | Dispatch-Tabelle; erster Eintrag Jägerhütte |
| `0x2E7C60` | Normale Gebäude-Blockerhöhen; Typ `7` hat Wert `40` |
| `0x18AF00` | Vanilla-Hunter-Zielsuche |
| `0x18AF96` | Typprüfung und öffentlicher Query-Detouranker |
| `0x18B052` | Sichtwrapper-Aufruf in der Zielsuche |
| `0x18E950` | Allgemeine Unit-Orderroutine und direkter Hunter-Angriffspfad |
| `0x196230` | `c_game_unit_issueorder_movehere` |
| `0x12FC20` | `HunterUpdate` |
| `0x1300EA` | Vergleich der nativen Distanz mit `28` im State-1-Pfad |
| `0x13013D` | Direkter Angriffsaufruf |
| `0x130171` | Sichtfehlschlag zu State `6` und Hüttenrückkehr |

Vollständige Bytepattern, Callerprüfungen, Strukturfelder, Resolver und
Updateprozedur stehen in `UpdateToNewDLL.md`. Bei abweichendem DLL-Hash:

1. Referenzhash: direktes RVA plus lokale semantische Bytevalidierung.
2. Abweichender Hash: eindeutige Suche nur in geeigneten PE-Sektionen und
   vollständige semantische Validierung.
3. Fehlender oder mehrdeutiger Treffer: nur das betroffene Feature deaktivieren
   und Vanilla aktiv lassen.

## Gesamt-Abnahmekriterien

Die Singleplayer-Kernfunktion ist fertig, wenn:

- nahe unerreichbare Beute eine weiter entfernte erreichbare nicht verdrängt,
- ausschließlich unerreichbare Beute keinen Move, kein Pendeln und keinen
  Dauersuchzyklus erzeugt,
- ein geöffneter Zugang dieselbe Beute zeitnah wieder zulässt,
- ein während des Anmarschs blockiertes Ziel über Vanilla gewechselt oder
  kontrolliert verworfen wird, ohne den Jäger aufzulösen,
- Vanillas Weg auch innerhalb Distanz `28` bis zur freien Sicht weiterläuft,
- freie Sicht sofort zu Vanillas echtem Angriff und Projektil führt,
- Jägerhütten wie normale Gebäude blockieren,
- lange Restwege eine passende Vanilla-Bewegungsstufe verwenden, ohne die
  letzte Schussannäherung zu beschleunigen,
- unreservierte Kadaver dieselbe Rangfolge mit nur ungefähr fünf Sekunden
  kalibriertem Bonus verwenden,
- ein weiter Kadaver gegen deutlich günstigere lebende Beute verlieren kann,
- Kadaver nicht doppelt reserviert werden und Vanilla-Pickup sowie Abgabe
  vollständig funktionieren,
- mehrere Jäger unabhängig arbeiten,
- alle freigeschalteten Beutetypen dieselbe Semantik besitzen,
- kein synchrones Managed-A*, eigener Recovery-Move oder AI-State-Nachbau
  zurückkehrt,
- Mod-/Option-Aus, Kartenwechsel und nicht unterstützter Multiplayer keine
  Restzustände oder aktiven Verhaltenspfade hinterlassen,
- Logs Millisekunden-Zeitstempel, stabile Identitäten, gedrosselte
  Wiederholungen und überprüfbare Invarianten besitzen,
- CRLF-, statische Code- und Native-Resolver-Prüfungen erfolgreich sind und der
  abschließende Build genau einmal über `ImprovedHunters\build.bat /nopause`
  läuft.

Das plattformübergreifende Gesamtziel ist erst nach dem Multiplayer-Chore ab
Script Extender `1.50.0` erreicht.

## Arbeitsanweisung für einen neuen Chat

1. Dieses Dokument und die in der Dateitabelle genannten aktuellen Dateien
   lesen; nicht mit der verworfenen eigenen State-Machine beginnen.
2. Paket A und B als abgeschlossen behandeln. Nur konkrete Regressionen öffnen
   sie erneut.
3. Mit dem Abschnitt „`1.1.60`-Diagnose nach dem `1.1.59`-Test“ beginnen. Die
   Pfadfortsetzung, `MoveHere`-Generation und Tracker-Retention sind bestätigt;
   offen ist die Trennung von nativer Sichtsemantik, Snapshot-Bewegungsalter
   und dem nachgeschalteten Vanilla-`+0xF4`-Tor.
4. Die `1.1.60`-Geometrie-, Kernrichtungs- und Gate-Marker zuerst ingame
   auswerten. Den Sicht-Snapshot nicht ohne Positionsbindung beschleunigen und
   das native Gate nicht ohne vollständige Span-/Kontrollflussvalidierung
   übergehen.
5. Alle sieben Paket-E-Tests durchführen und die Logs anhand der dokumentierten
   Marker, Identitäten, Altersgrenzen und Angriffsresultate maschinell prüfen.
6. Paket E vollständig prüfen und dokumentieren, bevor Paket F begonnen wird.
7. Danach exakt der Reihenfolge `F → D → C` folgen.
8. Den sichtbaren Jagdsprint nur optional nach Abschluss aller Pflichtpakete
   untersuchen; er blockiert weder Paket C noch das fachliche Kernziel.
9. Vor jedem Build alle Code-, Resolver- und CRLF-Prüfungen abschließen; danach
   die lokale `build.bat` einmal direkt mit `/nopause` ausführen.

Bei jeder neuen Hypothese zuerst prüfen, ob der kleinste stabile Vanilla-
Übergang erhalten werden kann. Diagnose, Verhaltenskorrektur und Vanilla-Aufruf
bleiben in getrennten Fehlerpfaden; Vanilla läuft exakt einmal.
