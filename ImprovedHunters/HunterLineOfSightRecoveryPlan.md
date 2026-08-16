# Plan: Robuste Sichtlinien-Recovery für Jäger

## Status und Abgrenzung

Dieses Dokument beschreibt die geplante Korrektur für ein Vanilla-Problem:
Jäger wählen erreichbare Hühner aus, brechen den Angriff aber vor dem Schuss ab,
wenn Vanillas interne Sicht-/Geometrieprüfung keine gültige Schusslinie findet.
Das Problem tritt unter anderem hinter Kornspeichern und Holzfällerhütten sowie
auf bestimmten Höhenverläufen ohne Gebäude auf. Jägerhütten bilden in Vanillas
Sichtprüfung eine Sonderbehandlung: Jäger können über sie hinweg schießen,
obwohl ein Pfeil anschließend physisch an der Hütte hängen bleiben kann.

Der Plan ändert deshalb weder allgemein die Sichtblockade von Gebäuden noch die
Projektilkollision. Er ergänzt ausschließlich für automatisch jagende Jäger eine
begrenzte Bewegung zu einer erreichbaren Position, von der Vanillas eigene
Sichtprüfung den Schuss akzeptiert. Die eigentliche Zielwahl, der Angriff, der
Fernkampfschaden, der Kadaverzustand, das Einsammeln und die Fleischabgabe
bleiben Vanilla.

Die in diesem Dokument genannten nativen Adressen beziehen sich auf die
kanonische installierte `CrusaderDE.dll`:

- Steam Build ID: `24651686`
- Dateigröße: `3.450.880` Byte
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

## Ziele

1. Einen Jäger nach einem nachweislich sichtblockbedingten Zielabbruch zu einer
   sinnvollen, erreichbaren Schussposition bewegen.
2. Den Bewegungsauftrag so lange kontrolliert verfolgen, dass Vanillas sofortige
   neue Zielwahl ihn nicht wieder überschreibt.
3. Nach der Bewegung Vanillas reguläre Zielabfrage und Schusslogik wieder
   übernehmen lassen.
4. Kornspeicher, Holzfällerhütten, Geländeanstiege und weitere Vanilla-
   Sichtblocker abdecken, ohne pauschal durch Gebäude schießen zu lassen.
5. Jägerhütten als Vanilla-Sichtausnahme beibehalten und einen dort eventuell
   steckenbleibenden Pfeil weiterhin über die vorhandene
   `DamageUnitRanged`-Kompensation behandeln.
6. Bei einer geänderten Spiel-DLL zuerst den bekannten RVA-Pfad und danach eine
   eindeutige semantische Pattern-Auflösung verwenden. Nur die Recovery wird
   deaktiviert, wenn beides nicht zuverlässig validiert werden kann.
7. Keine unsicheren Inline-Diagnosehooks an den früheren Crashstellen
   `0x18EE14`, `0x130171` oder `0x12FF53` erneut einführen.

## Nichtziele

- Keine globale Entfernung der Gebäudesichtblockade.
- Kein Teleportieren des Jägers.
- Kein künstlicher Schuss und kein `KillUnit` vor einem echten Projektilspawn.
- Keine Änderung der allgemeinen Reichweite oder der normalen Tierzielwahl.
- Keine künstliche Erhöhung der Hühnerreservierung und kein dauerhaftes
  Festhalten eines Huhns während der Recovery.
- Keine Änderung an manuellen `AttackUnit`-Befehlen anderer Fernkampfeinheiten.
- Keine neue Lobbyoption; die Recovery bleibt wie bisher an
  `EnableMod`, `HuntChicken` und `ImprovedPathfinding` gebunden.

## Analysierter Istzustand

### Relevante Produktionsdateien

| Datei / Stelle | Aktuelle Aufgabe | Festgestelltes Problem oder Anschlussstelle |
| --- | --- | --- |
| `ImprovedHuntersRuntime.RunNativeScan` | 100-ms-Takt für Unit-Zustände, Projektilkompensation, Reservierungsbereinigung und Idle-Requery | Geeigneter persistenter Takt für Recovery-Fortschritt; Reihenfolge muss so geändert werden, dass Recovery vor dem allgemeinen Idle-Requery läuft. |
| `ImprovedHuntersRuntime.TrackHunterTargetState` | Erkennt Zielwechsel/-verlust, setzt 30-s-Cooldown und gibt Reservierung `2` frei | Startet derzeit nur einen einmaligen Move und verwirft anschließend den Recovery-Zustand. Ein im selben Scan bereits neu zugewiesenes Ziel bleibt aktiv. |
| `ImprovedHuntersRuntime.OnHunterQueryTarget` | Filtert und priorisiert Vanilla-Kandidaten | Muss während einer aktiven Bewegung neue Tierzuweisungen für genau diesen Jäger unterdrücken und während der Wiederaufnahme nur das geplante Ziel zulassen. |
| `ImprovedHuntersRuntime.RequeryIdleHuntersNearPrey` | Setzt wartende Jäger mit Ziel `0` von AI-State `6` auf `0` | Darf eine aktive Recovery nicht parallel zurücksetzen; soll nur in der Recovery-Phase `Reacquire` gezielt verwendet werden. |
| `ImprovedHuntersRuntime.TryReleaseAbortedPreyReservation` | Entfernt verwaiste Reservierung `2` mit Slot-/Global-ID-Prüfung | Bleibt notwendig. Das Log meldet derzeit auch nach einem Recovery-Move pauschal `cooldownSeconds=30`; diese Aussage muss den wirklichen Recovery-Ausgang abbilden. |
| `HunterLineOfSightRecovery.TryRecoverAfterTargetAbort` | Zählt drei gebäudeblockierte Abbrüche, sucht einen Kandidaten und ruft einmal `MoveToTile` auf | Reagiert nur auf Gebäude, ignoriert Gelände, besitzt keine persistente Phase und verifiziert weder Move-Rückgabe noch Fortschritt oder Ankunft. |
| `HunterLineOfSightRecovery.HasBuildingFreeFiringLine` | Bresenham-Linie ohne Gebäude | Ist nur ein physischer Sicherheitsfilter. Sie bildet Vanillas Sichtsemantik, Untertilekoordinaten, Höheninterpolation und Tileflags nicht nach. |
| `HunterVisibilityDiagnostic` | Verhaltenneutrale Korrelation von Waiting-State, Terrain, Gebäuden und Projektilen | Für die Einführungsphase weiterverwenden; nach Abnahme durch eine kleinere separate Recovery-Diagnose ersetzen und anschließend entfernen. |
| `HunterQueryActorWorkaround` | Rekonstruiert wegen Script-Extender-Issue 123 die echte Jäger-ID | Muss vor jeder Recovery-Queryentscheidung weiter ausgeführt werden. Bei nicht auflösbarem Actor bleibt Vanilla unverändert. |
| Projektilpfad in `ImprovedHuntersRuntime` | Verfolgt echte Jägerpfeile und ruft bei Stillstand/Nähe/Delete-Pre `DamageUnitRanged` auf | Ist ein getrenntes Problem nach dem Schuss und bleibt bestehen. Er darf keine pre-shot Recovery auslösen oder ersetzen. |

### Befunde aus dem letzten Lauf

- Drei Recovery-Moves wurden ausgegeben, aber es entstanden null passende
  Projektile und damit auch keine `Vanilla ranged compensation`-Aufrufe.
- Es wurden sechzehn Zielabbrüche protokolliert. Der Fehler liegt somit vor dem
  Schuss; die Projektilkompensation konnte in diesem Lauf nicht eingreifen.
- Beim Move für Jäger `1` um `16:50:04.215` wurde eine Position für Huhn `98`
  geplant. Noch im selben Zeitfenster zeigte der native Zielzustand bereits auf
  Huhn `156`. Der Jäger blieb anschließend am Ausgangsort, während Vanilla Ziele
  weiter wechselte.
- Dasselbe Muster trat bei Jäger `100` um `16:50:14.824` auf: Der Move wurde für
  Huhn `161` geplant, unmittelbar danach war Huhn `156` das native Ziel.
- Zwölf Waiting-Beobachtungen lagen bereits innerhalb Distanz `20` und hatten
  auf der einfachen Tilelinie kein Gebäude. Trotzdem wurde nicht geschossen.
  Die Terrainbereiche lagen dabei beispielsweise bei `80-140` oder `80-170`,
  während Jäger und Huhn jeweils Elevation `80` hatten.
- Daraus folgen zwei getrennte Fehler im bisherigen Workaround:
  1. Der einmalige Move wird durch Vanillas Ziel-/AI-Verarbeitung überholt.
  2. „Kein Gebäude auf der Bresenham-Linie“ ist kein Beweis für eine freie
     Vanilla-Schusslinie.

### Historie des KillUnit-Fallbacks und des Test-Mods

- Der frühere `KillUnit`-Fallback griff erst, nachdem ein echtes Projektil
  erzeugt worden war. Er löste daher den heute reproduzierten pre-shot
  Sichtabbruch nicht.
- Bei Schüssen über Jägerhütten konnte Vanilla den Schuss zulassen, der Pfeil
  aber physisch an der Hütte hängen bleiben. Der damalige Fallback tötete das
  Huhn im nicht einsammelbaren Zustand `0x6F`; der Jäger wählte danach weitere
  Beute.
- Version `1.1.27` ersetzte dies korrekt durch den öffentlichen nativen
  Fernkampfschadenspfad `DamageUnitRanged`. Dieser Pfad soll unverändert als
  post-shot Absicherung bestehen bleiben.
- Der Test-Mod bestätigte außerdem, dass der interne Helper bei RVA `0xA06F0`
  sowohl aus der Hunter-Query als auch aus der späteren Ordererteilung genutzt
  wird. Ein Ergebnis `<= 0` führt im Hunterpfad zur Ablehnung des Angriffs.
- Frühere „verhaltenneutrale“ Inline-Hooks waren nicht sicher: Ein Hookfenster
  hatte native Seiteneinstiege, ein späterer Helper-Hook führte beim ersten
  echten Blockadefall zu einem CTD. Die neue Lösung darf diese Hookstrategie
  nicht wiederverwenden.

## Native Sicht- und Geometriesemantik

### Bereits statisch bestätigt

| RVA | Rolle |
| ---: | --- |
| `0x18AF00` | Vanilla-Hunter-Zielsuche |
| `0x18B052` | Aufruf des gemeinsamen Geometriehelpers innerhalb der Zielsuche |
| `0x18E950` | Allgemeine Unit-Orderroutine |
| `0x18ED1A` | Aufruf desselben Geometriehelpers im direkten Hunter-Zielpfad |
| `0x18ED1F` | Kopiert den Helper-Rückgabewert von `EAX` nach `EDX` |
| `0x18ED23` | Verzweigt bei Ergebnis `<= 0` in den Ablehnungspfad |
| `0xA06F0` | Wrapper für die native Linien-/Geometrieprüfung |
| `0x9E350` | Kernroutine der Linien-/Geometrieprüfung |
| `0x6B990` | Von der Kernroutine verwendete Höhen-/Hindernisabfrage; Semantik noch vollständig zu benennen |

Der Wrapper `0xA06F0` ruft `0x9E350` zunächst in einer Orientierung auf und
versucht bei Rückgabewert `0` die umgekehrte Orientierung. Die Kernroutine:

- arbeitet mit Weltkoordinaten und nicht nur mit Tilekoordinaten,
- verwendet die Unit-Höhen-/Bounds-Felder um `GameUnit + 0xB2..0xB8`,
- liest native Tileflags, unter anderem Masken `0x400300` und `0x400200`,
- fragt Hindernis-/Höhenwerte über `0x6B990` ab,
- vergleicht Hindernishöhen gegen eine entlang der Linie interpolierte Höhe,
- gibt `0` bei einer blockierten Linie und einen positiven Fortschritts-/
  Längenwert bei einer akzeptierten Linie zurück.

Damit ist erklärt, warum der aktuelle Gebäude-Bresenham-Test Geländeanstiege
nicht erkennt und warum die optische Gebäudehöhe allein keine verlässliche
Aussage liefert.

### Vor der Verhaltensänderung noch bytegenau zu bestätigen

1. Exakte acht Argumente von `0xA06F0` an beiden Hunter-Aufrufstellen:
   Kontextzeiger, Start-X/Y/Höhe, Ziel-X/Y/Höhe und Modusflag.
2. Exakte Formeln aus `r_CurrentWorldPositionX/Y`, `r_HeightElevation` und dem
   signierten Feld bei `+0xB8`. Die Disassembly zeigt für Hunter und Ziel
   unterschiedliche Zuschläge (`30` und `26`); Richtung und Bedeutung müssen
   anhand beider Call-Sites festgelegt werden.
3. Sichere Umrechnung einer hypothetischen Kandidaten-Tileposition in dieselben
   Welt-/Höhenwerte. Der In-Tile-Offset des Jägers darf nicht geraten werden.
4. Bedeutung des letzten Modusarguments und die im Hunterpfad tatsächlich
   verwendete Konstante.
5. Bedeutung des positiven Rückgabewerts und die exakte Vergleichssemantik
   (`0`, `< 0`, `<= 0`) an jeder relevanten Call-Site.
6. Seiteneffekt des Helpers auf `Kontext + 0xC`. Ein direkter Probeaufruf ist
   nur zulässig, wenn dieser Scratch-Zustand im gewählten persistenten
   Game-Thread-Callback nachweislich sicher ist.
7. Bestätigung an mindestens drei Laufzeitkontrollen:
   freie Linie mit Schuss, Vanilla-freie Linie über eine Jägerhütte und
   blockierte Linie an Kornspeicher/Holzfällerhütte beziehungsweise Gelände.

### Geplante native Auflösung

Eine neue Datei `HunterNativeVisibilityProbe.cs` kapselt ausschließlich die
Auflösung und den validierten Aufruf. Sie installiert keinen Hook.

Auf dem Referenzhash:

1. Direktes bekanntes RVA `0xA06F0` und den bekannten Hunter-Call-Site-RVA
   verwenden.
2. Nur die lokalen Bytes, Instruktionsgrenzen, zwei Core-Aufrufe, den
   konditionalen zweiten Aufruf und die dekodierte RIP-relative Kontextadresse
   semantisch validieren.
3. Keine vollständige Pattern-Suche starten.

Bei einem abweichenden Hash:

1. Nur ausführbare PE-Sektionen durchsuchen.
2. Einen ausreichend langen semantischen Wrapper-Pattern verwenden, der beide
   Aufrufe derselben Corefunktion und den Retry nur nach Ergebnis `0` enthält.
3. Zusätzlich die Hunter-Call-Site eindeutig auflösen und daraus
   Kontextadresse, Helperziel, Feldloads, Konstanten und Argumentreihenfolge
   dekodieren.
4. Genau einen semantisch gültigen Treffer verlangen. Fehlende oder mehrere
   gültige Treffer deaktivieren ausschließlich die Sichtlinien-Recovery.
5. Hash, RVA, Pattern, Dekodierung und Validierungsregeln in
   `UpdateToNewDLL.md` dokumentieren.

Falls ein direkter Probeaufruf wegen des Scratch-Seiteneffekts nicht sicher
nachgewiesen werden kann, wird nicht auf eine Näherungsformel zurückgefallen.
Dann ist vor der Verhaltensimplementierung zwischen einem exakt portierten
Managed-Helper und einem kleinen geprüften Script-Extender-Wrapper zu
entscheiden. Die aktuelle Gebäude-/Terrain-Min-Max-Näherung ist kein zulässiger
produktiver Fallback.

## Zielarchitektur

### Dateien und Verantwortlichkeiten

| Datei | Geplante Verantwortung |
| --- | --- |
| `HunterNativeVisibilityProbe.cs` | Hash/RVA/Pattern-Auflösung, semantische Validierung und begrenzte native Sichtprobe ohne Hook. |
| `HunterLineOfSightRecovery.cs` | Persistente Recovery-State-Machine, Kandidatensuche, Pfadprüfung, Bewegung, Fortschritt und Wiederaufnahme. |
| `HunterLineOfSightRecoveryDiagnostic.cs` | Begrenzte, selbstvalidierende Phasenlogs; separat entfernbar. |
| `ImprovedHuntersRuntime.cs` | Eventverdrahtung, Query-Policy, Zielübergänge, Reservierungsfreigabe, Scanreihenfolge und bestehende Projektilkompensation. |
| `HunterVisibilityDiagnostic.cs` | Nur während Diagnose-/A/B-Phase behalten; nach bestätigter Recovery entfernen statt als parallelen Fallback fortzuführen. |
| `UpdateToNewDLL.md` | Native Adressen, Muster, Signatur, Strukturfelder, Auflösungs- und Updateaudit. |
| `info.json` / Pluginversion | Version und Changelog nach erfolgreicher Implementierung und Tests. |

### RecoveryPlan pro Jäger

Jeder aktive Plan wird mindestens mit folgenden stabilen Daten geführt:

- Jäger: Unit-Slot, Global-ID, Besitzer, Starttile.
- Bevorzugtes Huhn: Unit-Slot, Global-ID, zuletzt bekannte Tile-/Weltposition.
- Aktuell nativ zugewiesenes Huhn, falls es während des Starts bereits vom
  bevorzugten Ziel abweicht.
- Ziel-Tile-ID, Tilekoordinaten, erwartete native Sichtprobe und Pfadlänge.
- Phase, Erstellungszeit, Ablaufzeit und Zeitpunkt des letzten Fortschritts.
- Letzte Jägerposition, letzte Distanz zum Ziel und Anzahl der Move-Ausgaben.
- Anzahl der Neuplanungen, Stalls, überschriebenen Orders und Query-Sperren.
- Synchron korrelierter `OnUnitMoveHere(Post)`-Rückgabewert.
- Letzter passender Projektilzeitpunkt.

Unit-Slot allein ist nie ausreichend. Jede Verwendung validiert zusätzlich die
Global-ID, den Unittyp und den Alive-State. Slot-Wiederverwendung beendet den
Plan ohne Schreibzugriff auf die neue Unit.

### Phasenmodell

| Phase | Verhalten | Übergang |
| --- | --- | --- |
| `Observing` | Wiederholte Zielabbrüche sammeln und die tatsächliche native Linie prüfen. | Nach konfigurationsintern drei blockierten pre-shot Abbrüchen zu `Planning`. |
| `Planning` | Kandidaten erzeugen, physische Gebäudelinie filtern, native Sicht prüfen und höchstens eine begrenzte Zahl mit `FindPath` testen. | Erfolgreicher Kandidat zu `MovePending`, sonst Abbruch mit normalem Cooldown. |
| `MovePending` | In-Flight-Korrelation setzen, `MoveToTile` genau einmal ausgeben und synchronen Move-Pre/Post-Event erfassen. | Akzeptierter beziehungsweise plausibel gestarteter Move zu `Moving`; eindeutige Ablehnung zu Neuplanung/Abbruch. |
| `Moving` | Neue Tierquerys dieses Jägers blockieren, Positionsfortschritt prüfen und eine überschriebene/stagnierende Order begrenzt neu ausgeben. | Am Kandidaten oder bei bereits freier aktueller Linie zu `Revalidate`; bei Zielbewegung zu `Planning`; bei Timeout zu Abbruch. |
| `Revalidate` | Jäger/Huhn erneut validieren, native Sicht von der tatsächlichen Position prüfen und physischen Gebäudekorridor prüfen. | Freie Linie mit vorhandenem nativen Ziel zu `AwaitProjectile`; ohne natives Ziel zu `Reacquire`; weiterhin blockiert zu `Planning`. |
| `Reacquire` | Nur die exakte weiche Zielidentität in `OnHunterQueryTarget` zulassen und Vanillas bestehende Idle-Requery auslösen. | Native Zielzuweisung zu `AwaitProjectile`; reserviertes/totes/verschwundenes Ziel zu Abbruch oder neuer normaler Zielwahl. |
| `AwaitProjectile` | Keine Move-Order mehr ausgeben; Vanilla schießen lassen. | Passendes Projektil beendet die pre-shot Recovery erfolgreich; erneuter pre-shot Abbruch führt begrenzt zurück zu `Planning`. |
| `Completed/Cancelled` | Alle Query-Sperren und temporären Zustände entfernen. | Kein weiterer Eingriff; normale Vanilla-/Modlogik läuft. |

Wichtige Regel: Die Recovery hält das bevorzugte Huhn nur logisch fest. Eine
native Reservierung `2` wird freigegeben, sobald kein lebender Jäger mehr exakt
dieses Slot-/Global-ID-Ziel führt. Das verhindert, dass Vanillas vor dem
öffentlichen Queryevent liegender Reservierungsfilter das Huhn dauerhaft
aussortiert. Übernimmt ein anderer Jäger das Huhn, wird der Plan verworfen oder
mit einem neuen Ziel aufgebaut; es wird kein Reservation-Bypass ergänzt.

## Kandidatensuche

### Zweistufiges Verfahren

1. Günstige Managed-Vorauswahl:
   - maximal Radius `8` um die aktuelle Jägerposition,
   - innerhalb der bestehenden Schussdistanz `3..20`,
   - gültiges, begehbares und unbesetztes Tile,
   - keine Gebäude-Tiles auf dem physischen Schusskorridor, einschließlich
     Jägerhütten,
   - deterministische Sortierung nach Bewegungsdistanz, Schussdistanz und
     Tile-ID.
2. Teure Validierung nur für die besten Kandidaten:
   - native Sichtprobe mit exakt Vanillas Koordinaten-/Höhensemantik,
   - höchstens acht `FindPath`-Prüfungen,
   - maximal eine begrenzte Zahl nativer Probes pro Jäger und Scan.

Die physische Gebäudelinie und die native Sichtprobe beantworten verschiedene
Fragen:

- Die native Probe entscheidet, ob Vanilla den Angriff von dort beginnen darf.
- Die Gebäudeprüfung vermeidet Positionen, bei denen ein echter Pfeil danach
  sicher an einem Gebäude kollidieren würde.

Für die aktuelle Linie darf eine Jägerhütte die pre-shot Recovery nicht
auslösen, wenn Vanillas native Probe sie akzeptiert. Bleibt der Pfeil dort
stecken, übernimmt weiterhin ausschließlich die post-shot
`DamageUnitRanged`-Kompensation. Für neue Kandidaten werden Jägerhütten dennoch
wie jedes Gebäude aus dem physischen Pfeilkorridor ausgeschlossen.

### Zielbewegung

Hühner können sich während der Recovery bewegen. Deshalb:

- Zielposition bei jedem Scan erneut über Slot plus Global-ID lesen.
- Bei geändertem Zieltile den bisherigen Kandidaten nicht blind weiterverwenden.
- Höchstens alle `500 ms` neu planen, sofern die tatsächliche Linie am
  aktuellen Jägerstandort nicht inzwischen frei ist.
- Anzahl der Neuplanungen pro Recovery begrenzen, beispielsweise auf `4`.
- Nach Überschreitung das Ziel mit normalem Abbruchcooldown verlassen, damit
  kein permanentes Hin-und-her entsteht.

Die konkreten Intervalle sind interne Konstanten und werden durch Tests und
Laufzeitlogs kalibriert; sie werden nicht als neue Nutzereinstellung exponiert.

## Bewegungsauftrag und Fortschrittskontrolle

### Move-Rückgabe erfassen

`GameUnitManagerAPI.MoveToTile` verwirft den nativen `Int64`-Rückgabewert. Der
Script Extender veröffentlicht denselben Aufruf jedoch bereits über
`UnitR3EventHooks.OnUnitMoveHere` mit `Pre` und `Post` sowie `ReturnValue`.

Geplantes Vorgehen:

1. Unmittelbar vor `MoveToTile` einen In-Flight-Datensatz mit Jägeridentität,
   Zielkoordinate und Zeitstempel setzen.
2. Nur synchron passende `OnUnitMoveHere`-Events konsumieren.
3. Im Post-Event den Rückgabewert dem RecoveryPlan zuordnen.
4. Die Erfolgssemantik zunächst diagnostisch gegen Positions-/Pfadzustand
   kalibrieren; nicht ungeprüft `!= 0` als Erfolg annehmen.
5. In-Flight-Datensatz in einem `finally`-Pfad löschen, damit ein Event- oder
   Nativefehler keine späteren Vanilla-Moves fälschlich zuordnet.

### Fortschritt und Orderüberschreibung

- Der 100-ms-Scan prüft aktuelle Position, Distanz zum Recovery-Tile,
  `r_PathPlanStateBitFlags`, `r_MovingRelevant`, letzten Befehl und natives Ziel.
- Jede Positionsänderung oder sinkende Distanz aktualisiert
  `LastProgressAt`.
- Bleibt der Jäger trotz akzeptiertem Move beispielsweise `750 ms` ohne
  Fortschritt oder zeigen die Orderfelder wieder einen Angriff statt Bewegung,
  gilt die Recovery-Order als überschrieben/stagnierend.
- `MoveToTile` darf dann frühestens nach einem Mindestabstand erneut ausgegeben
  werden, insgesamt höchstens drei Mal.
- Ein Gesamt-Timeout von ungefähr fünf bis acht Sekunden beendet die Recovery.
- Ankunft wird nicht nur über exakte Tilegleichheit festgestellt. Wenn die
  tatsächliche native Sicht bereits vorher frei und der physische Korridor
  gebäudefrei ist, wird sofort zu `Revalidate` gewechselt.

Die Recovery schreibt nicht direkt in unbekannte Target-/Pathfelder. Bereits
vorhandene, im Mod erprobte AI-State-Schreibzugriffe (`0x2BC`/`0x2C4`) werden
nur für die eng begrenzte `Reacquire`-Phase verwendet und vorab gegen den
vollständigen Vanilla-Übergang geprüft.

## Zielwahl während und nach der Bewegung

### Integration in OnHunterQueryTarget

Nach der durch `HunterQueryActorWorkaround` korrigierten Jäger-ID und vor der
normalen kostenbasierten Auswahl erhält die Recovery eine Queryentscheidung:

- `Moving`, `MovePending`, `Planning`: bekannte aktivierte Beutetiere für genau
  diesen Jäger ablehnen, damit Vanilla nicht laufend einen neuen Angriff über
  den Move legt.
- `Reacquire`: nur Slot und Global-ID des geplanten lebenden, unreservierten
  Huhns erlauben; alle anderen Beutetiere dieses Jägers vorübergehend ablehnen.
- `AwaitProjectile`: nur das aktuell validierte Ziel zulassen, bis ein Projektil
  erscheint oder das kurze Zeitfenster ausläuft.
- Kein aktiver Plan oder nicht auflösbarer Actor: bestehende Zielwahl unverändert.

Die Policy muss vor jeder Entscheidung die aktuelle Global-ID des Kandidaten
lesen. Das Queryevent enthält nur die Unit-ID; ein wiederverwendeter Slot darf
nicht als geplantes Huhn gelten.

### Bereits im selben Scan neu zugewiesenes Ziel

Der letzte Lauf zeigt, dass beim Erkennen des alten Abbruchs bereits ein anderes
Huhn im nativen Zielfeld stehen kann. Ein solcher Zustand wird nicht durch
ungeprüftes Leeren von `+0x39A/+0x39C` korrigiert.

Empfohlene sichere Regel:

1. Ist das neue native Ziel ein gültiges Huhn, übernimmt der RecoveryPlan diese
   Identität und plant die Position dafür neu.
2. Ab Aktivierung blockiert die Query-Policy weitere Zielwechsel.
3. Verschwindet das native Ziel während der Bewegung, bleibt die letzte stabile
   Identität als weiches Ziel erhalten.
4. Ist das Ziel bei Ankunft noch nativ gesetzt und die Linie frei, werden keine
   Zielfelder geschrieben; die Move-Wiederholung endet und Vanilla darf den
   Angriff fortsetzen.
5. Ist das Zielfeld `0`, wird die Reservierung sicher freigegeben und über
   `Reacquire` Vanillas Query für genau das weiche Ziel ausgelöst.

Erst wenn Laufzeitdaten zeigen, dass dieser Ablauf wegen eines dauerhaft
falschen nichtnull Zielfeldes nicht funktioniert, darf ein eigener
Target-Clear-Übergang erwogen werden. Davor müssen alle Writer und späteren
Leser von `+0x39A/+0x39C`, AI-State, Reservation und Pathstate vollständig
untersucht werden. Ein bloßes Nullschreiben ist nicht Teil dieses Plans.

## Änderungen in ImprovedHuntersRuntime

### Initialisierung

1. `HunterNativeVisibilityProbe` vor `HunterLineOfSightRecovery` initialisieren.
2. Recovery nur verfügbar melden, wenn Probe, Hunter-only-Zielschutz und die
   für Queryentscheidungen notwendige Actorauflösung verfügbar sind.
3. `OnUnitMoveHere` Pre/Post abonnieren.
4. Bei Teilfehlern nur die Recovery deaktivieren; Granary-Limit,
   Neutralspawn, automatische Hunter-only-Zielwahl, manuelle Angriffe und
   Projektilkompensation getrennt weiter betreiben.

### Scanreihenfolge

Geplante Reihenfolge innerhalb `RunNativeScan`:

1. bestehende Despawn-/Gesundheits- und Projektilkompensation,
2. Unitarray und Identitäten erfassen,
3. Reservierungen und bestehende Zielübergänge beobachten,
4. aktive RecoveryPlans fortschreiben und gegebenenfalls Move ausgeben,
5. sichere Diagnose ausführen,
6. allgemeines `RequeryIdleHuntersNearPrey` nur für Jäger ohne aktiven Plan,
7. übrige bestehende Verarbeitung.

### TrackHunterTargetState refaktorieren

Die Methode soll einen strukturierten Übergang statt nur `bool
recoveryMoveIssued` liefern, beispielsweise:

- `NoChange`
- `NormalAbort`
- `RecoveryObserved`
- `RecoveryStarted`
- `RecoveryRetargeted`
- `RecoveryCancelled`

Darauf basieren:

- 30-s-Abbruchcooldown nur bei normalem oder endgültig gescheitertem Abbruch,
- kein Cooldown während einer laufenden Recovery,
- Reservierungslog mit tatsächlichem Cooldownzustand,
- korrektes Aktualisieren von `activeHunterTargets`, auch wenn im selben Scan
  bereits ein neues natives Ziel existiert.

### Reset- und Fehlerpfade

Alle Recovery- und Probe-Caches werden gelöscht bei:

- Kartenstart,
- `EnableMod` aus,
- `HuntChicken` aus,
- `ImprovedPathfinding` aus,
- Dispose beziehungsweise echter Prozessbeendigung.

Ein Plan endet außerdem sofort bei:

- totem/gelöschtem Jäger,
- Jäger-Slot-Wiederverwendung,
- totem/gelöschtem Huhn,
- Huhn-Slot-Wiederverwendung,
- Reservierung durch einen anderen lebenden Jäger,
- nicht mehr gültigem Pfad oder dauerhaft bewegtem Ziel,
- nativer Probe- oder Move-Ausnahme.

## Zusammenspiel mit der Projektilkompensation

Die pre-shot Recovery und die post-shot Kompensation bleiben strikt getrennt:

1. Solange kein passendes Projektil existiert, darf nur die Recovery bewegen.
2. `RecordProjectileSpawn` beendet sofort Query-Sperre und Move-Wiederholungen
   für diesen Jäger/Ziel-Plan.
3. Der bestehende `PendingHunterShotIntent` übernimmt ab diesem Zeitpunkt.
4. Nur ein echtes, identitätsgeprüftes `ArcherArrow`-Projektil kann
   `DamageUnitRanged` auslösen.
5. `KillUnit` bleibt ausgeschlossen.
6. Bei einer Jägerhütte darf Vanilla normal schießen; bleibt der Pfeil hängen,
   greift ausschließlich der bestehende stalled/near/delete-Mechanismus.

## Diagnose und Invarianten

### Separate Datei

`HunterLineOfSightRecoveryDiagnostic.cs` enthält alle neuen temporären
Phasenlogs. Dadurch kann die Diagnose nach der Abnahme entfernt werden, ohne
die State-Machine oder den nativen Resolver umzuschreiben.

### Begrenzte Marker

Jeder Marker erhält den vorhandenen Millisekunden-Zeitstempel und stabile
Identitäten:

- `probe-resolved`: Hashpfad, Helper-RVA, Core-RVA, Kontext-RVA,
  Patternstrategie und Validierungsergebnis.
- `blocked-observed`: Jäger/Huhn, native Probe, Gebäude-/Terrainkontext,
  Abbruchzähler.
- `planned`: Ausgang, Ziel, Kandidat, native Probe, physischer Korridor,
  Pfadlänge und Kandidatenanzahl.
- `move-issued`: Generation, Koordinaten und Versuch.
- `move-result`: korreliertes Pre/Post, nativer Rückgabewert und erste
  Pathfelder.
- `progress`: nur bei Positionsänderung oder gedrosselt bei Stillstand.
- `order-overridden`: erwartete Bewegung gegenüber beobachteten Feldern/Ziel.
- `replanned`: Grund `target-moved`, `line-still-blocked`, `path-invalid` oder
  `order-stalled`.
- `arrived`: tatsächliche Position und Distanz.
- `revalidated`: native Sicht und physischer Gebäudekorridor.
- `query-suppressed` beziehungsweise `preferred-query-accepted`.
- `projectile-confirmed`: terminaler Erfolg der pre-shot Recovery.
- `cancelled`: eindeutiger terminaler Grund und Cooldownentscheidung.

### Selbstvalidierende Zähler

Pro Karte werden begrenzt und periodisch folgende Summen geprüft:

- `plansStarted == plansActive + plansCompleted + plansCancelled`
- `movesIssued == movePostsMatched + movePostsMissing`
- `plansCompleted == projectileConfirmed + vanillaTargetResumedWithoutYetObservedProjectile`
- `queriesHandled == queriesSuppressed + preferredQueriesAccepted`
- keine doppelte aktive Planidentität für denselben Jäger-Slot/Global-ID.

Abweichungen werden einmalig als Warnung protokolliert und deaktivieren im
Zweifel die Recovery, nicht Vanillas Angriffspfad.

## Performancegrenzen

- Ein Plan pro Jäger.
- 100-ms-Scan als vorhandener Takt; keine neue Coroutine oder kurzlebige
  `BaseUnityPlugin.Update`-Abhängigkeit.
- Kandidatenradius zunächst `8` und maximal acht `FindPath`-Aufrufe.
- Native Probes nur nach günstiger Vorauswahl und mit globalem Budget pro Scan.
- Kurzer Cache für identische Start-/Zielweltkoordinaten und Höhen, höchstens
  ungefähr `250 ms`; Änderung von Tile, Höhe, Global-ID oder Gebäudezustand
  invalidiert ihn.
- Keine LINQ-/Stringallokationen im normalen hot path; Beschreibungen nur beim
  tatsächlich ausgegebenen begrenzten Log erzeugen.
- Move-Events synchron und ohne lang laufende Suche behandeln; Kandidatenplanung
  bleibt im bestehenden Scan.

## Fehlersicherheit und DLL-Kompatibilität

1. Exakter DLL-Hash: direktes RVA, nur lokale semantische Validierung, keine
   Vollsuche.
2. Abweichender Hash: eindeutige Suche in ausführbaren PE-Sektionen plus
   Dekodierung und semantische Prüfung.
3. Nicht eindeutig: Recovery deaktivieren und einmal klar loggen.
4. Kein nativer Bytepatch und kein Inline-Hook für die Recovery.
5. Probe, Vanilla-Aufruf, Recovery-Entscheidung und Diagnose in getrennten
   `try`-Pfaden; ein Logformatierungsfehler darf keinen Move oder Vanilla-
   Querypfad verändern.
6. Mod aus beziehungsweise Hühnerjagd/verbesserte Wegfindung aus: keine Query-
   Sperre, kein Probeaufruf, kein Move und keine Recovery-Rohfeldänderung.
7. Fällt der Hunter-only-Automatikschutz aus, wird die Recovery ebenfalls
   deaktiviert, damit keine zusätzlichen neutralen Hühnerinteraktionen in einen
   ungesicherten Zustand geraten.
8. Der temporäre Issue-123-Workaround bleibt klar als entfernbar markiert. Nach
   einem Script-Extender-Fix muss geprüft werden, ob `HunterUnitId` zuverlässig
   ist; erst dann kann der Workaround entfernt werden.

## Umsetzungsreihenfolge mit Prüfgates

### Phase 1: Native Signatur abschließen

1. Beide Hunter-Call-Sites vollständig disassemblieren/dekompilieren.
2. Acht Argumente, Rohfelder, Konstanten, Kontext und Returnsemantik benennen.
3. Referenz-RVAs und semantische Pattern festlegen.
4. Resolvertests für Referenzhash, eindeutigen Fallback, fehlenden und
   mehrdeutigen Treffer schreiben.
5. `UpdateToNewDLL.md` zunächst um die belegten Analyseergebnisse ergänzen.

Gate: Kein produktiver Probeaufruf, solange Argumente oder Scratch-Seiteneffekt
unklar sind.

### Phase 2: Verhaltenneutrale Probe

1. `HunterNativeVisibilityProbe.cs` implementieren.
2. Nur tatsächlich beobachtete Hunter/Huhn-Paare prüfen; keine Bewegung und
   keine Queryänderung.
3. Ergebnisse mit drei bekannten Ingame-Fällen vergleichen.
4. Prüfen, dass Jägerhütten nativ sichtbar, Kornspeicher/Holzfällerhütten und
   die reproduzierten Höhenlinien nativ blockiert gemeldet werden.
5. Stabilität mit mehreren Jägern testen; keine Hooks installieren.

Gate: Probeergebnis und beobachteter Vanilla-Schuss/Abbruch müssen für alle
Kontrollfälle übereinstimmen. Sonst keine Recovery aktivieren.

### Phase 3: State-Machine isoliert implementieren

1. Aktuellen einmaligen Recovery-Code durch das Phasenmodell ersetzen.
2. Uhr, Probe, Pathfinder, Move-Ausgabe und Log als kleine testbare Adapter
   injizieren beziehungsweise kapseln.
3. Reine Zustands- und Kandidatentests ohne Spielprozess ausführen.
4. Noch keine allgemeine Query-Sperre aktivieren.

Gate: Alle terminalen Zustände, Timeouts, Slot-Reuse- und Reentranzfälle sind
deterministisch getestet.

### Phase 4: Runtime-Integration

1. MoveHere-Pre/Post-Korrelation abonnieren.
2. Scanreihenfolge ändern.
3. `TrackHunterTargetState` auf strukturierte Outcomes umstellen.
4. Query-Policy und Recovery-spezifisches Requery ergänzen.
5. Generisches Idle-Requery für aktive Pläne sperren.
6. Reservierungs- und Cooldownlogs korrigieren.
7. Projektilspawn als terminalen Recovery-Erfolg verdrahten.

Gate: Ingame bewegt sich ein einzelner Jäger bei Kornspeicher-,
Holzfällerhütten- und Geländeblockade, bleibt bis zur freien Linie auf dem Move
und schießt anschließend über Vanilla.

### Phase 5: Mehrfachfälle und Bereinigung

1. Mehrere Jäger und mehrere Hühner testen.
2. Zielbewegung, Abriss des Blockers, fehlenden Weg und Reservierungswechsel
   testen.
3. Alte breite `HunterVisibilityDiagnostic` nach bestätigter neuer Diagnose
   entfernen; keinen parallelen alten Fallback behalten.
4. Version erhöhen, Changelog und `UpdateToNewDLL.md` finalisieren.
5. Alle statischen Tests, Preset-/XAML-/Locale-Audits nur dann erneut ausführen,
   wenn Einstellungen oder UI betroffen sind; ansonsten die Runtime-/Native-
   Tests und CRLF-Prüfung ausführen.
6. Nach sämtlichen Prüfungen genau einmal `ImprovedHunters\build.bat /nopause`
   direkt und erhöht starten; der Build übernimmt Installation und Artefakte.

## Testmatrix

### Automatisierte State-Machine-Tests

- Limit vor Trigger: 0, 1, 2 und 3 blockierte Abbrüche.
- Abbruchfenster überschritten.
- Passendes Projektil innerhalb des Zwei-Sekunden-Fensters verhindert
  pre-shot Recovery.
- Native Probe frei trotz Gebäude-Metadaten: keine Recovery.
- Native Probe blockiert ohne Gebäude: Recovery wegen Gelände.
- Keine Kandidaten, keine Route und maximal acht Pathchecks.
- Deterministischer Tie-Break bei gleichwertigen Kandidaten.
- Move-Pre/Post passt, fehlt, ist verschachtelt oder gehört zu Vanilla/einer
  anderen Unit.
- Move wird überschrieben, stagniert, macht Fortschritt oder erreicht vorzeitig
  eine freie Linie.
- Ziel bewegt sich einmal, mehrfach oder dauerhaft.
- Ziel stirbt, wird gelöscht, reserviert oder sein Slot wird wiederverwendet.
- Jäger stirbt, wird gelöscht oder sein Slot wird wiederverwendet.
- Bereits im Trigger-Scan abweichendes natives Ziel wird übernommen und neu
  geplant.
- Querys während `Moving` werden unterdrückt; in `Reacquire` wird nur das exakte
  Slot-/Global-ID-Ziel angenommen.
- Mod/Hühnerjagd/verbesserte Wegfindung werden in jeder Phase deaktiviert.
- Mapreset und Dispose entfernen alle Pläne und In-Flight-Moves.
- Cooldown nur bei endgültigem Abbruch, nicht während aktiver Recovery.
- Logzählerinvarianten und Loglimits.

### Native Resolver-/Probe-Tests

- Referenzhash nutzt direkt `0xA06F0` und startet keine Pattern-Vollsuche.
- Lokale Bytes oder Call-Site-Semantik falsch: Referenzpfad lehnt sicher ab.
- Abweichender Hash mit genau einem semantischen Treffer.
- Fehlender beziehungsweise mehrdeutiger Pattern-Treffer.
- Falsche Core-Callziele, Kontextadresse außerhalb gültiger Sektionen oder
  unerwartete Argumentinstruktionen.
- Positive, null und gegebenenfalls negative Returnwerte mit exakter
  signed/unsigned Behandlung.
- Probe-Cache invalidiert bei Position, Höhe, Global-ID und Gebäudeänderung.

### Ingame-Fälle

1. Kornspeicher als Sichtblocker: Jäger bewegt sich, erreicht freie Linie,
   schießt, sammelt ein und liefert Fleisch ab.
2. Holzfällerhütte als Sichtblocker: gleicher vollständiger Ablauf.
3. Jägerhütte als Sichtlinie: kein pre-shot Move allein wegen der Hütte;
   Vanilla darf schießen. Steckt der Pfeil fest, folgt `DamageUnitRanged`, ein
   `0x6E`-Kadaver und anschließend Abholung.
4. Gelände `80 -> 140/170 -> 80` ohne Gebäude: native Probe erkennt Blockade,
   Jäger sucht eine tatsächlich freie Position.
5. Blocker wird während des Wartens abgerissen: Recovery erkennt die freie
   aktuelle Linie, beendet Bewegung und lässt Vanilla sofort fortfahren.
6. Kein erreichbares Schusstile: begrenzter Abbruch ohne Endlosschleife.
7. Zweiter Kornspeicher und mehrere Hühner: keine Zielwechselkaskade.
8. Drei Jäger: keine gemeinsame Reservierung, kein gegenseitiges Überschreiben
   von RecoveryPlans.
9. Huhn wandert während der Bewegung: begrenzte Neuplanung.
10. Huhn wird von anderem Jäger getötet/reserviert: sauberer Planabbruch und
    normale neue Zielwahl.
11. Freie Kontrolllinie ohne Blocker: kein Recovery-Eingriff.
12. Rehe, Ziegen, Hasen und Kamele: bestehende Zielwahl unverändert.
13. Nichtjäger-Fernkampf: keine automatische Hühnerzielwahl; manueller Angriff
    bleibt nach bestehender Policy möglich.
14. `EnableMod`, `HuntChicken` oder `ImprovedPathfinding` aus: Vanilla ohne
    Recovery-Eingriff.
15. Speichern/Laden und Kartenwechsel während beziehungsweise nach einer
    Recovery: keine übernommene Planidentität.

## Abnahmekriterien

Die Umsetzung gilt erst als fertig, wenn alle folgenden Punkte erfüllt sind:

- Ein Jäger bleibt nach einem Recovery-Move nicht mehr in der beobachteten
  Zielwechsel-/Warten-Schleife.
- Kornspeicher, Holzfällerhütte und der gebäudefreie Höhenfall führen bei
  vorhandenem Weg zu einer freien Schussposition und anschließend zu einem
  echten Projektil.
- Jägerhütten bleiben Vanillas Sichtausnahme; die Recovery entfernt diese
  Ausnahme nicht und die Projektilkompensation erzeugt weiterhin den
  einsammelbaren ranged-Kadaverzustand.
- Kein `KillUnit` wird für neue Schüsse verwendet.
- Keine rohe Ziel-ID wird ohne vorher vollständig validierten Vanilla-Übergang
  geleert oder gesetzt.
- Keine Reservation `2` bleibt ohne exakt zugeordneten lebenden Jäger zurück.
- Kein nativer Inline-Hook wird an einer früheren Crashstelle installiert.
- Referenzhash verwendet direkt validierte RVAs; abweichender Hash verwendet
  nur eine eindeutige semantische Pattern-Auflösung.
- Bei Auflösungs-/Probe-/Recoveryfehler bleibt Vanilla aktiv und nur dieses
  Feature wird deaktiviert.
- Logs enthalten Millisekunden-Zeitstempel, stabile Identitäten, begrenzte
  Wiederholungen und erfüllte Zählinvarianten.
- Runtime-Tests, Native-Resolvertests, CRLF-Prüfung und der abschließende einzelne
  Build laufen ohne Fehler oder Warnungen durch.

## Empfohlene Entscheidung

Die empfohlene Umsetzung ist eine persistente, querybewusste Recovery-
State-Machine mit Vanillas nativem Geometriehelper als Wahrheitsquelle und dem
vorhandenen Managed-Pathfinder nur für Erreichbarkeit. Das behebt das
eigentliche Vanilla-Problem, ohne Gebäude allgemein durchsichtig zu machen und
ohne den Angriff oder Kill künstlich vorwegzunehmen.

Der aktuelle einmalige `MoveToTile`-Workaround soll dabei vollständig ersetzt
und nicht als paralleler Fallback behalten werden. Der bestehende
`DamageUnitRanged`-Fallback bleibt dagegen absichtlich bestehen, weil er ein
anderes, erst nach einem echten Schuss auftretendes Vanilla-Problem behandelt.
