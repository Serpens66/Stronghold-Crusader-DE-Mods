# Plan: Robuste Sichtlinien-Recovery für Jäger

## Status und Abgrenzung

Dieses Dokument beschreibt die geplante Korrektur für ein Vanilla-Problem:
Jäger berücksichtigen fußläufig erreichbare Beute hinter Sichtblockern entweder
gar nicht erst als Ziel oder brechen den Angriff noch vor dem Schuss ab, wenn
Vanillas interne Sicht-/Geometrieprüfung keine gültige Schusslinie findet.
Das Problem tritt unter anderem hinter Kornspeichern und Holzfällerhütten sowie
auf bestimmten Höhenverläufen ohne Gebäude auf. Jägerhütten bilden in Vanillas
Sichtprüfung eine Sonderbehandlung: Jäger können über sie hinweg schießen,
obwohl ein Pfeil anschließend physisch an der Hütte hängen bleiben kann.

Der Plan ändert deshalb weder allgemein die Sichtblockade von Gebäuden noch die
Projektilkollision. Er ergänzt ausschließlich für automatisch jagende Jäger eine
kontrollierte Bewegung zu einer erreichbaren Position, von der Vanillas eigene
Sichtprüfung den Schuss akzeptiert und der reale Pfeilkorridor frei ist. Das
gilt für jeden im Mod aktivierten Beutetyp: Reh, Ziege, Hase, Kamel, Huhn und
Kuh. Die eigentliche Zielpräferenz, der Angriff, der Fernkampfschaden, der
Kadaverzustand, das Einsammeln und die Fleischabgabe bleiben Vanilla
beziehungsweise bei der bereits vorhandenen Improved-Pathfinding-Auswahl.

Die in diesem Dokument genannten nativen Adressen beziehen sich auf die
kanonische installierte `CrusaderDE.dll`:

- Steam Build ID: `24651686`
- Dateigröße: `3.450.880` Byte
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

## Ziele

1. Ein fußläufig erreichbares, aktiviertes Beutetier auch dann berücksichtigen,
   wenn Vanillas Sichtprüfung es bereits innerhalb der Zielsuche verwirft und
   daher noch keine stabile native Zielzuweisung existiert.
2. Einen Jäger nach einem nachweislich sichtblockbedingten Zielabbruch zu einer
   sinnvollen, erreichbaren Schussposition bewegen.
3. Den Bewegungsauftrag so lange kontrolliert verfolgen, dass Vanillas sofortige
   neue Zielwahl ihn nicht wieder überschreibt.
4. Nach der Bewegung Vanillas reguläre Zielabfrage und Schusslogik wieder
   übernehmen lassen.
5. Kornspeicher, Holzfällerhütten, Mauern, Tore, Türme, Geländeanstiege und
   weitere Vanilla-Sichtblocker abdecken, ohne pauschal durch Gebäude zu
   schießen.
6. Für Reh, Ziege, Hase, Kamel, Huhn und Kuh dieselbe Recovery-Infrastruktur
   und denselben zentralen Eligibility-Pfad verwenden; die jeweilige
   `Hunt...`-Option bleibt die fachliche Freigabegrenze.
7. Jägerhütten weiterhin korrekt als Vanilla-Sichtausnahme erkennen, für neue
   Hunter-Angriffe aber wie jeden physischen Pfeilblocker behandeln: Der Jäger
   bewegt sich vor dem Schuss zu einer freien Bahn. Die vorhandene
   `DamageUnitRanged`-Kompensation bleibt nur Sicherheitsnetz für bereits
   gestartete oder trotz Prüfung kollidierende Pfeile.
8. Bei einer geänderten Spiel-DLL zuerst den bekannten RVA-Pfad und danach eine
   eindeutige semantische Pattern-Auflösung verwenden. Nur die Recovery wird
   deaktiviert, wenn beides nicht zuverlässig validiert werden kann.
9. Keine unsicheren Inline-Diagnosehooks an den früheren Crashstellen
   `0x18EE14`, `0x130171` oder `0x12FF53` erneut einführen.

## Nichtziele

- Keine globale Entfernung der Gebäudesichtblockade.
- Kein Teleportieren des Jägers.
- Kein künstlicher Schuss und kein `KillUnit` vor einem echten Projektilspawn.
- Keine Änderung der allgemeinen Reichweite oder der normalen Tierzielwahl.
- Keine künstliche Erhöhung der Beutereservierung und kein dauerhaftes
  Festhalten eines Beutetiers während der Recovery.
- Keine Änderung an manuellen `AttackUnit`-Befehlen anderer Fernkampfeinheiten.
- Keine neue Lobbyoption; die Recovery bleibt an `EnableMod`,
  `ImprovedPathfinding` und die zum jeweiligen Beutetyp gehörende
  `Hunt...`-Option gebunden.

## Analysierter Istzustand

### Relevante Produktionsdateien

| Datei / Stelle | Aktuelle Aufgabe | Festgestelltes Problem oder Anschlussstelle |
| --- | --- | --- |
| `ImprovedHuntersRuntime.RunNativeScan` | 100-ms-Takt für Unit-Zustände, Projektilkompensation, Reservierungsbereinigung und Idle-Requery | Geeigneter persistenter Takt für Recovery-Fortschritt; Reihenfolge muss so geändert werden, dass Recovery vor dem allgemeinen Idle-Requery läuft. |
| `ImprovedHuntersRuntime.TrackHunterTargetState` | Erkennt Zielwechsel/-verlust, setzt 30-s-Cooldown und gibt Reservierung `2` frei | Startet derzeit nur einen einmaligen Move und verwirft anschließend den Recovery-Zustand. Ein im selben Scan bereits neu zugewiesenes Ziel bleibt aktiv. |
| `ImprovedHuntersRuntime.OnHunterQueryTarget` | Filtert und priorisiert Vanilla-Kandidaten | Muss während einer aktiven Bewegung neue Tierzuweisungen für genau diesen Jäger unterdrücken und während der Wiederaufnahme nur das geplante Ziel zulassen. |
| `ImprovedHuntersRuntime.TryGetTargetSelectionForHunter` / `TryGetPathCost` | Wählt aktivierte Beute bereits pfadbasiert aus | Muss auch den Vor-Zielzuweisungs-Trigger speisen. Der aktuelle Pfad wird von der Jägerhütte statt zwingend von der tatsächlichen Jägerposition bewertet und darf nicht ungeprüft als Recovery-Route wiederverwendet werden. |
| `ImprovedHuntersRuntime.IsRuntimeHuntingEnabled` | Zentrale Runtime-Freigabe für Beutetypen | Schließt `CHIMP_TYPE_COW` derzeit trotz `HuntCow`, Tooltip und Fleischwert ausdrücklich aus. Dieser Widerspruch muss vor der Generalisierung beseitigt und mit einem gemeinsamen Eligibility-Test abgesichert werden. |
| `ImprovedHuntersRuntime.RequeryIdleHuntersNearPrey` | Setzt wartende Jäger mit Ziel `0` von AI-State `6` auf `0` | Darf eine aktive Recovery nicht parallel zurücksetzen; soll nur in der Recovery-Phase `Reacquire` gezielt verwendet werden. |
| `ImprovedHuntersRuntime.TryReleaseAbortedPreyReservation` | Entfernt verwaiste Reservierung `2` mit Slot-/Global-ID-Prüfung | Bleibt notwendig. Das Log meldet derzeit auch nach einem Recovery-Move pauschal `cooldownSeconds=30`; diese Aussage muss den wirklichen Recovery-Ausgang abbilden. |
| `HunterLineOfSightRecovery.TryRecoverAfterTargetAbort` | Zählt drei gebäudeblockierte Hühnerabbrüche, sucht einen Kandidaten und ruft einmal `MoveToTile` auf | Ist hart auf `HuntChicken`/`CHIMP_TYPE_CHICKEN` begrenzt, startet erst nach einer Zielzuweisung, ignoriert Gelände, besitzt keine persistente Phase und verifiziert weder Move-Rückgabe noch Fortschritt oder Ankunft. |
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
| `0x18AF96` | Kandidatentyp-Prüfung und Anker des öffentlichen `OnUnitHunterQueryTarget`-Detours; dieser kann nur die frühe Typentscheidung ersetzen. |
| `0x18B052` | Aufruf des gemeinsamen Geometriehelpers innerhalb der Zielsuche |
| `0x18B057..0x18B05E` | `dec eax; cmp eax, 0x1AF; ja reject`: Nur ursprüngliche Helperwerte `1..432` gelten in der Zielsuche als Sichttreffer. |
| `0x18E950` | Allgemeine Unit-Orderroutine |
| `0x18ED1A` | Aufruf desselben Geometriehelpers im direkten Hunter-Zielpfad |
| `0x18ED1F` | Kopiert den Helper-Rückgabewert von `EAX` nach `EDX` |
| `0x18ED23` | Verzweigt bei Ergebnis `<= 0` in den Ablehnungspfad |
| `0x196230` | Native `c_game_unit_issueorder_movehere`-Routine hinter `MoveToTile`; validiert Koordinaten, Unit-/Tilezustand und stößt Vanillas eigentliche Bewegungs-/Pfadplanung an. |
| `0x79C0` | Reine Distanzroutine der Hunter-Zielsuche; liefert `abs(x1-x2) + abs(y1-y2)` in Tilekoordinaten. |
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

### Neu bestätigte Reihenfolge der Zielsuche

Die lokale Disassembly der kanonischen DLL und der Script-Extender-Detour
bestätigen folgenden Ablauf:

1. `0x18AF00` läuft über die Unit-Slots und prüft Alive-State, Typ,
   Reservation und weitere Kandidatenfelder.
2. Der öffentliche `OnUnitHunterQueryTarget`-Detour ist am Typ-Load bei
   `0x18AF96` verankert. Er kann zusätzliche Tierarten zulassen, überspringt
   aber nicht die danach folgenden Vanilla-Prüfungen.
3. Vanilla prüft anschließend Distanz und bei `0x18B052` die native Sichtlinie.
4. Ein Ergebnis außerhalb `1..432`, insbesondere `0`, verwirft den Kandidaten,
   bevor `0x18AF00` ihn als bestes Ziel zurückgeben kann.
5. Der spätere direkte Hunter-Orderpfad ruft denselben Wrapper bei `0x18ED1A`
   erneut auf und lehnt dort Werte `<= 0` ab.

Damit reicht eine reine Recovery nach `TrackHunterTargetState` nicht aus. Für
den Benutzerfall „Kein Wild“ ist zusätzlich ein Vor-Zielzuweisungs-Trigger
erforderlich: Ein wartender Jäger ohne natives Ziel muss aus der gemeinsamen
Beutekandidatenmenge ein konkretes Tier wählen und zu einer freien
Schussposition bewegt werden können, obwohl Vanilla dieses Tier wegen der
Sichtprüfung nie als Ziel zurückgegeben hat.

An beiden Hunter-Call-Sites ist außerdem bereits bytegenau belegt:

- `RCX = 0x182F79680` ist der gemeinsame Sichtkontext.
- `EDX/R8D/R9D` erhalten Hunter `+0xB2`, `+0xB4` und
  `+0xB6 + signed(+0xB8) + 30`.
- Die Stackargumente 5 bis 7 erhalten Beute `+0xB2`, `+0xB4` und
  `+0xB6 + signed(+0xB8) + 26`.
- Im direkten Orderpfad erscheinen dieselben Unitfelder wegen des
  UnitManager-Headers `0x65C` als `+0x70E/+0x710/+0x712/+0x714`; die
  zurückverfolgten Stackwerte ergeben exakt dieselben beiden Endpunkte.
- `0xA06F0` reicht diese sechs Werte zunächst in derselben Orientierung an
  `0x9E350` weiter, setzt dessen achtes Argument auf `0` und vertauscht die
  Endpunkte nur beim ersten Rückgabewert `0`.

Der Wrapper besitzt damit die native Signatur
`int(context, startX, startY, startHeight, endX, endY, endHeight)`. Rizin/Ghidra
erkennt wegen des Windows-x64-Shadow-Stacks fälschlich ein achtes
Wrapperargument; die Instruktionen und beide Caller belegen nur sieben. Das
achte Argument existiert ausschließlich am Core-Aufruf und wird vom Wrapper
selbst auf `0` gesetzt.

Die Kernroutine `0x9E350` rastert höchstens `1000` Schritte. Bei einem erkannten
Hindernis liefert sie `0`, beim Erreichen des Endpunkts den positiven diskreten
Schrittzähler; dies ist kein euklidischer Distanzwert. Der Wrapper gibt den
ersten positiven Wert zurück oder versucht nach `0` die umgekehrte Richtung.

Das erste Coreargument wird in der gesamten Routine nur geladen, um an
`context + 0xC` zu schreiben. Es gibt keine Leseverwendung und keinen Zugriff
auf ein anderes Feld dieses Objekts. Für die Recovery ist daher ein eigener
nullinitialisierter, mindestens `16` Byte großer Probe-Kontext vorgesehen. So
wird Vanillas globales Objekt bei `0x182F79680` nicht verändert. Dieser statische
Befund muss noch durch eine verhaltenneutrale Game-Thread-A/B-Probe bestätigt
werden; der globale Kontext darf produktiv nicht als Scratchpuffer dienen.

### Neu bestätigte Distanzpässe der Zielsuche

Die Zielsuche verwendet vor dem Sichthelper die reine Manhattan-Distanzroutine
bei RVA `0x79C0` mit den aktuellen Tilepositionen von Jäger und Beute:

1. Im ersten Durchlauf werden nur Kandidaten mit Distanz `> 20` betrachtet.
2. Wird kein Ziel gefunden, folgt genau ein zweiter Durchlauf mit Distanz `> 5`.
3. Bei Distanz `< 54` muss der Sichtwrapper einen Wert `1..432` liefern.
4. Ab Distanz `54` überspringt diese frühe Zielsuche den Sichtwrapper und kann
   den Kandidaten anhand der kleinsten Manhattan-Distanz auswählen. Die spätere
   direkte Hunter-Order prüft die Sicht dennoch erneut und kann abbrechen.

Die Grenze `432` entspricht `54 * 8` Raster-/Weltuntereinheiten und passt damit
zur Distanzschwelle, ist aber nicht als eigentliche Schussreichweite zu
interpretieren. Für `Discovering` muss die bestehende Mod-Priorisierung diese
beiden Vanilla-Pässe und den später möglichen Orderabbruch berücksichtigen:
Ein verborgenes Tier kann entweder schon in der Query ohne Zielzuweisung
verworfen werden oder erst nach einer zunächst erlaubten Fernzielauswahl.

### Neu bestätigte Pfad- und Move-Semantik

`GameTileManagerAPI.FindPath` im kanonischen Script Extender ist kein Aufruf
von Vanillas nativer Unit-Pfadplanung. Es handelt sich um einen verwalteten
A*-Pathfinder über Tilekoordinaten. Er sperrt Gebäude und
`TilePropertyMasks.ImpassableMask` und verhindert diagonales Schneiden durch
Ecken, bildet aber nicht nachweislich alle unit-, order- und zustandsabhängigen
Regeln des nativen Jägerpfads ab. Die dafür verwendete Methode
`IsTileWalkableAndUnoccupied` prüft ihrem Namen zum Trotz keine Unitbelegung.

Der echte `MoveToTile`-Befehl führt in der Referenz-DLL zu
`c_game_unit_issueorder_movehere` bei RVA `0x196230`. Die statische Disassembly
zeigt zahlreiche Unit-, Koordinaten-, Tile- und Pfadzustandsprüfungen. Sie gibt
auf allen erkannten Ablehnungspfaden `0` zurück. Bei bereits erreichtem Ziel
oder nachdem die interne Pfaderzeugung mindestens einen Schritt geliefert und
den Path-State auf `2` gesetzt hat, gibt sie `1` zurück. Der Standardpfad
(`unknown == 0`) läuft dabei unter anderem über RVA `0xF4930`; die Routine
arbeitet mit globalem, zuvor aufgebautem Scratch-/Pfadkontext und ist damit
nicht als verhaltenneutrale read-only Probe belegt. Die statisch erkennbare
Bool-Semantik und ihre Zustandsfelder müssen noch einmal im echten Spiel gegen
den synchronen Post-Return und Positionsfortschritt kalibriert werden. Daraus
folgen drei Grenzen:

1. Managed `FindPath` ist nur ein günstiger Kandidaten- und Routenvorschlag.
2. Erreichbarkeit wird erst durch einen nachweislich akzeptierten nativen Move
   und anschließenden Fortschritt bestätigt; alternativ ist zuvor eine exakt
   identifizierte verhaltenneutrale native Pfadabfrage zu isolieren.
3. Ein Managed-Fehlschlag darf ein Tier nicht endgültig als unerreichbar
   klassifizieren, solange nicht belegt ist, dass der native Jägerpfad dieselbe
   Sperrsemantik verwendet. Solche Fälle benötigen eine native Gegenprobe oder
   einen klar diagnostizierten konservativen Abbruch statt „Kein Pfad“ als
   bewiesene Vanilla-Aussage.

### Vor der Verhaltensänderung noch bytegenau zu bestätigen

1. Sichere Umrechnung einer hypothetischen Kandidaten-Tileposition in dieselben
   Welt-/Höhenwerte. Der In-Tile-Offset des Jägers darf nicht geraten werden.
2. Die noch unbekannte fachliche Bedeutung des fest auf `0` gesetzten achten
   Corearguments benennen; für die originalgetreue Probe bleibt es unabhängig
   davon zwingend `0`.
3. Den privaten 16-Byte-Kontext in einem persistenten Game-Thread-Callback
   verhaltenneutral gegen den Vanilla-Aufruf prüfen; weder parallele Verwendung
   noch Aufruf aus einem beliebigen Thread zulassen.
4. Bestätigung an mindestens drei Laufzeitkontrollen:
   freie Linie mit Schuss, Vanilla-freie Linie über eine Jägerhütte und
   blockierte Linie an Kornspeicher/Holzfällerhütte beziehungsweise Gelände.
5. Bestätigung, dass derselbe Koordinatenaufbau für alle sechs aktivierbaren
   Beutetypen gilt oder Dokumentation jedes typabhängigen Sonderfalls.
6. Exakte Hunter-Schussdistanz und Grenzsemantik aus Orderpfad und
   Projektilspawn bestimmen. Die Querygrenzen `5/20/54` sind jetzt belegt, aber
   keine Schussreichweiten; die bisherigen Kandidatengrenzen `3..20` bleiben
   bis dahin nur Hypothese und dürfen nicht als produktive Wahrheit
   festgeschrieben werden.

### Physische Pfeilbahn ist eine eigene Prüffrage

Vanillas Sichthelper ist nicht zugleich ein Beweis für eine kollisionsfreie
Pfeilbahn; die bestätigte Jägerhütten-Ausnahme zeigt das direkt. Auch ein
einfacher Tile-Bresenham-Test beweist keine freie Bahn an Gebäudeecken,
mehrteiligen Footprints, Mauern, Toren, Türmen oder unterschiedlichen Höhen.
Vor der produktiven Kandidatenwahl ist deshalb der native Projektilbewegungs-
beziehungsweise Kollisionspfad eines `ArcherArrow` gezielt zu untersuchen.

Bevorzugt wird eine verhaltenneutrale, aufrufbare native Kollisionsprobe mit
Vanillas echten Weltkoordinaten und Höhen. Falls sie nicht sicher isolierbar
ist, muss ein konservativer Managed-Supercover-Korridor anhand der nachweislich
relevanten Tileflags, vollständigen Gebäudefootprints und Höhenregeln gegen
echte Pfeilflüge validiert werden. Ein Kandidat darf nur verwendet werden,
wenn sowohl die Hunter-Sichtprobe als auch diese physische Schussbahnprüfung
frei melden. Jägerhütten werden bei neuen Kandidaten nicht ausgenommen.

### Geplante native Auflösung

Eine neue Datei `HunterNativeVisibilityProbe.cs` kapselt ausschließlich die
Auflösung und den validierten Aufruf. Sie installiert keinen Hook.

Auf dem Referenzhash:

1. Direktes bekanntes RVA `0xA06F0` und den bekannten Hunter-Call-Site-RVA
   verwenden.
2. Nur die lokalen Bytes, Instruktionsgrenzen, zwei Core-Aufrufe, den
   konditionalen zweiten Aufruf, das konstante achte Coreargument sowie die
   Sieben-Argument-Caller semantisch validieren. Die RIP-relative globale
   Kontextadresse dient nur der Signaturprüfung; der Probeaufruf verwendet den
   privaten Kontext.
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
- Bevorzugte Beute: Unit-Slot, Global-ID, Typ, zuletzt bekannte Tile-/
  Weltposition und die zugehörige `Hunt...`-Freigabe.
- Aktuell nativ zugewiesene Beute, falls sie während des Starts bereits vom
  bevorzugten Ziel abweicht.
- Aktuelles natives Bewegungsziel samt Zweck (`ApproachPrey` oder belegtes
  `FiringTile`), erwartete native Sichtprobe und optionale Managed-Pfadlänge.
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
| `Discovering` | Einen wartenden Jäger mit Ziel `0` nach einem abgeschlossenen Suchversuch beziehungsweise im gedrosselten Idle-Scan mit der gemeinsamen Beute-Eligibility korrelieren. Nur aktivierte, lebende und unreservierte Beute berücksichtigen; Managed-Pfadkosten dürfen ranken, aber Kandidaten nicht endgültig ausschließen. | Sicht oder physische Bahn von der aktuellen Position blockiert: direkt zu `Planning`; beide frei: Vanilla-Requery; kein gültiger Kandidat: keine Recovery. |
| `Observing` | Wiederholte Zielabbrüche einer bereits stabil zugewiesenen Beute sammeln und die tatsächliche native Linie prüfen. | Nach bestätigtem blockierten pre-shot Abbruch zu `Planning`; die bisherige feste Dreierzahl wird nur beibehalten, wenn Logs zeigen, dass ein einzelner eindeutiger Abbruch nicht genügt. |
| `Planning` | Primär Vanillas Bewegungsziel auf die aktuelle Beuteposition vorbereiten; Managed-Route nur zum Ranking und für Diagnose verwenden. Einen alternativen Schuss-Anulus erst nach belegter nativer Beute-Erreichbarkeit inkrementell prüfen. | Valides Annäherungsziel zu `MovePending`; endgültige native Ablehnung beziehungsweise vollständig ausgeschöpfter belegter Alternativraum zu Abbruch mit normalem Cooldown. |
| `MovePending` | In-Flight-Korrelation setzen, `MoveToTile` genau einmal ausgeben und synchronen Move-Pre/Post-Event erfassen. | Akzeptierter beziehungsweise plausibel gestarteter Move zu `Moving`; eindeutige Ablehnung zu Neuplanung/Abbruch. |
| `Moving` | Neue Tierquerys dieses Jägers blockieren, dem von Vanilla angenommenen Weg in Richtung Beute folgen, Positionsfortschritt prüfen und eine überschriebene/stagnierende Order begrenzt neu ausgeben. | Innerhalb bestätigter Schussreichweite am ersten Punkt mit freier nativer Sicht und physischer Bahn zu `Revalidate`; bei Zielbewegung zu `Planning`; bei nativer Ablehnung oder Timeout zu Abbruch. |
| `Revalidate` | Jäger/Beute erneut validieren, native Sicht von der tatsächlichen Position und die physische Pfeilbahn prüfen. | Beide Linien frei und natives Ziel vorhanden: `AwaitProjectile`; ohne natives Ziel: `Reacquire`; weiterhin blockiert: `Planning`. |
| `Reacquire` | Nur die exakte weiche Zielidentität in `OnHunterQueryTarget` zulassen und Vanillas bestehende Idle-Requery auslösen. | Native Zielzuweisung zu `AwaitProjectile`; reserviertes/totes/verschwundenes Ziel zu Abbruch oder neuer normaler Zielwahl. |
| `AwaitProjectile` | Keine Move-Order mehr ausgeben; Vanilla schießen lassen. | Passendes Projektil beendet die pre-shot Recovery erfolgreich; erneuter pre-shot Abbruch führt begrenzt zurück zu `Planning`. |
| `Completed/Cancelled` | Alle Query-Sperren und temporären Zustände entfernen. | Kein weiterer Eingriff; normale Vanilla-/Modlogik läuft. |

Wichtige Regel: Die Recovery hält die bevorzugte Beute nur logisch fest. Eine
native Reservierung `2` wird freigegeben, sobald kein lebender Jäger mehr exakt
dieses Slot-/Global-ID-Ziel führt. Das verhindert, dass Vanillas vor dem
öffentlichen Queryevent liegender Reservierungsfilter die Beute dauerhaft
aussortiert. Übernimmt ein anderer Jäger die Beute, wird der Plan verworfen oder
mit einem neuen Ziel aufgebaut; es wird kein Reservation-Bypass ergänzt.

## Kandidatensuche

### Native Annäherung und vollständiger Fallback

Der bisherige Radius `8` um den Jäger und der endgültige Abbruch nach acht
Pathchecks können ein erreichbares Tier hinter einer längeren Mauer dauerhaft
übersehen und widersprechen damit dem Ziel. Die Suche wird deshalb so
aufgebaut:

1. Die bisher vermischten Teile von `TryGetTargetSelectionForHunter` in eine
   gemeinsame Beute-Eligibility und eine getrennte Kosten-/Erreichbarkeitsstufe
   zerlegen. Kein eigener Hühner-Sonderpfad. Ein Managed-Pathfind-Fehlschlag
   darf eine sonst gültige Beute für die Discovery nicht endgültig entfernen.
2. Managed-`FindPath` von der tatsächlichen Jägerposition darf verfügbare Beute
   günstig ranken und eine erwartete Route für Diagnose liefern. Der alte
   Fünf-Sekunden-Cache und ein nur von der Jägerhütte gerechneter Pfad reichen
   dafür nicht; auch ein frischer Managed-Pfad ist kein Vanilla-Beweis.
3. Primär `MoveToTile` auf die aktuelle Beuteposition beziehungsweise eine erst
   bytegenau validierte native Approach-Semantik ausgeben. Return `1` plus
   Fortschritt bestätigt, dass Vanilla selbst einen Weg angenommen hat. Während
   der Annäherung wird im 100-ms-Scan der erste Standort verwendet, der innerhalb
   der bestätigten Schussreichweite sowohl native Sicht als auch freie physische
   Pfeilbahn besitzt. Damit folgt der Jäger Vanillas Weg um den Blocker und hält
   so früh wie möglich zum Schuss an.
4. Vor der Implementierung ist im Spiel zu prüfen, ob `MoveToTile` ein durch das
   Tier belegtes Ziel-Tile als erreichbares Annäherungsziel annimmt. Falls nicht,
   muss die darunterliegende native Nearest-Approach-/Path-End-Semantik
   identifiziert werden; ein beliebiges erreichbares Schusstile beweist nicht,
   dass der spätere Kadaver fußläufig erreichbar ist.
5. Erst wenn native Beute-Erreichbarkeit unabhängig belegt ist, darf ein
   alternativer begehbarer Anulus innerhalb der bytegenau bestätigten
   Hunter-Schussdistanz deterministisch und inkrementell geprüft werden. Jeder
   Schusskandidat benötigt einen von `MoveHere` angenommenen Weg sowie beide
   freien Linienprüfungen.
6. Teure Pfade und Probes pro 100-ms-Scan budgetieren, aber den Suchcursor über
   Scans fortsetzen. „Kein erreichbares Schusstile“ darf erst gemeldet werden,
   wenn der relevante endliche Kandidatenraum vollständig geprüft wurde, nicht
   nur die ersten acht heuristischen Treffer.

Die physische Gebäudelinie und die native Sichtprobe beantworten verschiedene
Fragen:

- Die native Probe entscheidet, ob Vanilla den Angriff von dort beginnen darf.
- Die gesondert validierte physische Bahnprüfung vermeidet Positionen, bei
  denen ein echter Pfeil danach an Gebäude, Mauer, Tor, Turm, Ecke oder Gelände
  kollidieren würde.

Eine Jägerhütte darf trotz positiver nativer Sichtprobe die pre-shot Recovery
auslösen, sobald die physische Bahnprüfung sie als Pfeilblocker meldet. Für die
aktuelle Linie und für neue Kandidaten wird sie wie jedes andere physische
Hindernis ausgeschlossen. Die post-shot-`DamageUnitRanged`-Kompensation bleibt
für schon gestartete oder trotz Prüfung kollidierende Pfeile bestehen, ist aber
nicht mehr der geplante Normalpfad durch die Hütte.

### Zielbewegung

Alle Beutetiere können sich während der Recovery bewegen. Deshalb:

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
3. Im Post-Event den Rückgabewert der bei RVA `0x196230` aufgelösten
   `c_game_unit_issueorder_movehere`-Routine dem RecoveryPlan zuordnen.
4. Die statisch belegte Bool-Semantik (`0` abgelehnt, `1` angenommen oder Ziel
   bereits erreicht) diagnostisch gegen Positions-/Pfadzustand kalibrieren.
   `1` allein beweist noch keinen späteren Fortschritt und keine Ankunft.
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

- Noch kein aktiver Plan: Meldet die schnelle, verhaltenneutrale physische
  Bahnvorprüfung für den aktuellen Hunter/Kandidaten einen Blocker, wird dieser
  Kandidat für den laufenden Query-Burst abgelehnt und als möglicher
  `Discovering`-Start vorgemerkt. Pfadsuche und vollständige Planung laufen
  anschließend im 100-ms-Scan, niemals im nativen Candidate-Loop.
- `Moving`, `MovePending`, `Planning`: bekannte aktivierte Beutetiere für genau
  diesen Jäger ablehnen, damit Vanilla nicht laufend einen neuen Angriff über
  den Move legt.
- `Reacquire`: nur Slot, Global-ID und Typ der geplanten lebenden,
  unreservierten Beute erlauben; alle anderen Beutetiere dieses Jägers
  vorübergehend ablehnen.
- `AwaitProjectile`: nur das aktuell validierte Ziel zulassen, bis ein Projektil
  erscheint oder das kurze Zeitfenster ausläuft.
- Kein aktiver Plan oder nicht auflösbarer Actor: bestehende Zielwahl unverändert.

Die Policy muss vor jeder Entscheidung die aktuelle Global-ID und den Typ des
Kandidaten lesen. Das Queryevent enthält nur die Unit-ID; ein wiederverwendeter
Slot darf nicht als geplante Beute gelten.

### Bereits im selben Scan neu zugewiesenes Ziel

Der letzte Lauf zeigt, dass beim Erkennen des alten Abbruchs bereits eine andere
Beute im nativen Zielfeld stehen kann. Ein solcher Zustand wird nicht durch
ungeprüftes Leeren von `+0x39A/+0x39C` korrigiert.

Empfohlene sichere Regel:

1. Ist das neue native Ziel gültige aktivierte Beute, übernimmt der
   RecoveryPlan diese Identität und plant die Position dafür neu.
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

## Multiplayer-Autorität und Determinismus

Multiplayer-Unterstützung bleibt ausdrückliches Ziel dieser Recovery, wird aber
nicht zusammen mit der ersten Singleplayer-Implementierung gebaut. Sie wird als
eigener nachgelagerter Chore **„Hunter-Recovery-Multiplayer-Synchronisation“**
umgesetzt, sobald der kanonische Script Extender mindestens Version `1.50.0`
erreicht hat. Bis dahin muss die Recovery in echtem Multiplayer fail-closed
deaktiviert bleiben; die Singleplayer-Implementierung darf keine lokale
simulationsrelevante Teilfunktion auf Clients ausführen.

Die vorherige Trennung ist keine dauerhafte Einschränkung: Datenmodell,
State-Machine, Zielidentitäten, Taktquelle und Adaptergrenzen werden von Anfang
an so angelegt, dass der spätere Synchronisations-Chore ohne parallele zweite
Recovery-Implementierung ergänzt werden kann.

Ein zusätzlicher `MoveToTile`-Befehl ist simulationsrelevant. Vor der
Multiplayer-Runtime-Integration muss deshalb anhand der Script-Extender-Quelle und eines
Host-/Client-Diagnoselaufs geklärt werden, ob dieser Befehl nur auf der
Simulationsautorität ausgegeben und repliziert wird oder in der Lockstep-
Simulation auf allen Teilnehmern deterministisch entstehen muss.

- Den Spielmodus bei Kartenstart über `Shared/GameModeHelper.cs` erfassen; weder
  `IsNetworkedEnvironment()` noch eine lokale `gameMembers`-Liste allein als
  Multiplayerbeweis verwenden.
- Bei Einzelautorität darf nur diese Instanz Recovery-Moves und etwaige
  AI-State-Requerys ausgeben; Clients beobachten nur replizierten Zustand.
- Bei Lockstep-Ausführung müssen Beuteauswahl, Kandidatenreihenfolge,
  Tie-Breaks, Probe-Caches und Abbruchgrenzen auf allen Peers identisch sein.
  Verhaltensübergänge dürfen dann nicht von lokalem `Stopwatch`-Timing abhängen,
  sondern benötigen einen gemeinsamen Map-Tick beziehungsweise deterministische
  Scan-Generationen. `Stopwatch` bleibt nur für Diagnose und Drosselung ohne
  Simulationsentscheidung zulässig.
- Host und Client dürfen niemals unterschiedliche Recovery-Ziele reservieren
  oder konkurrierende Move-Orders für denselben Jäger erzeugen.
- Singleplayer-Skirmish, Singleplayer-Trail, echter Host, echter Client sowie
  Multiplayer-Save/Load erhalten getrennte Abnahmetests und Diagnoselogs.

Gate: Vor Script Extender `1.50.0` beziehungsweise vor abgeschlossenem
Synchronisations-Chore wird keine verhaltensändernde Multiplayer-Recovery
freigegeben. Singleplayer-Skirmish und -Trail werden unabhängig davon
unterstützt. Nach Erreichen der Mindestversion ist Multiplayer kein optionaler
Dauerzustand, sondern eine noch abzuschließende unterstützte Zielplattform.

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

### Vor-Zielzuweisungs-Trigger

`TrackHunterSearchQuery` erkennt bereits neue Query-Bursts anhand einer
Zeitlücke, und `OnHunterQueryTarget` kennt nach der Actor-Korrektur Jäger und
Kandidatenidentität. Diese Daten werden erweitert, ohne den Queryhook selbst
mit Pfadsuchen zu belasten:

1. Pro Jäger den Beginn, den letzten Callback und die vom gemeinsamen
   `TargetSelection` bevorzugte Slot-/Global-ID-/Typ-Identität eines
   Query-Bursts erfassen.
2. Erst im 100-ms-Scan und nach einer kurzen Ruhephase des Bursts prüfen, ob der
   Jäger weiterhin im Waiting-State mit Ziel `0` steht. Damit läuft keine
   Planung innerhalb des nativen Candidate-Loops.
3. Die Beute erneut vollständig validieren. Einen frischen Managed-Pfad von der
   tatsächlichen Jägerposition nur als Ranking-/Diagnosewert erfassen, nicht als
   alleinige Recovery-Grenze.
4. Ist die aktuelle native Sicht oder die physische Pfeilbahn blockiert,
   `Discovering -> Planning` starten. Sind beide frei, nur den normalen
   Vanilla-Requery zulassen. Ob Vanilla wirklich einen Fußweg annimmt,
   entscheidet anschließend der korrelierte native MoveHere-Rückgabewert samt
   Fortschrittsprüfung.
5. Ein dedizierter Cooldown pro Jäger/Beute verhindert, dass unveränderte
   erfolglose Query-Bursts ohne Pause denselben vollständigen Suchraum neu
   aufbauen.

Falls Laufzeitlogs zeigen, dass der Query-Burst vor `0x18B052` nicht eindeutig
genug abgegrenzt werden kann, darf der gedrosselte Idle-Scan dieselbe
`TargetSelection` direkt verwenden. Auch dann gelten Ziel `0`, Waiting-State,
frische Identität und aktivierter Beutetyp als Pflichtbedingungen; eine
Managed-Pfadbewertung ist nur Präferenz, die native Move-Annahme bleibt Gate.

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
- die zum Plan gehörende `Hunt...`-Option aus,
- `ImprovedPathfinding` aus,
- Dispose beziehungsweise echter Prozessbeendigung.

Ein Plan endet außerdem sofort bei:

- totem/gelöschtem Jäger,
- Jäger-Slot-Wiederverwendung,
- toter/gelöschter Beute,
- Beute-Slot-Wiederverwendung oder Typwechsel,
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
6. Eine Jägerhütte blockiert neue Schussfreigaben über die physische Bahnprobe.
   Für einen bereits gestarteten und dort hängenbleibenden Pfeil greift weiterhin
   ausschließlich der bestehende stalled/near/delete-Mechanismus.

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
- `blocked-observed`: Jäger/Beute einschließlich Typ, native Probe,
  Gebäude-/Terrainkontext,
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
- Pfadwegpunkte zuerst, danach vollständige inkrementelle Anulus-Suche; keine
  endgültige Radius-8- oder Acht-Kandidaten-Grenze.
- Managed-`FindPath`-, native Sicht- und physische Bahnprobes nur nach günstiger
  Vorauswahl und mit globalem Budget pro Scan; Suchcursor bleiben erhalten.
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
6. Mod aus, verbesserte Wegfindung aus oder die konkrete Beuteart aus: keine
   Query-Sperre, kein Probeaufruf, kein Move und keine Recovery-
   Rohfeldänderung für diesen Plan.
7. Fällt der Hunter-only-Automatikschutz aus, wird die Recovery ebenfalls
   deaktiviert, damit keine zusätzlichen neutralen Hühnerinteraktionen in einen
   ungesicherten Zustand geraten.
8. Der temporäre Issue-123-Workaround bleibt klar als entfernbar markiert. Nach
   einem Script-Extender-Fix muss geprüft werden, ob `HunterUnitId` zuverlässig
   ist; erst dann kann der Workaround entfernt werden.

## Umsetzungsreihenfolge mit Prüfgates

### Phase 1: Native Signatur abschließen

Statisch abgeschlossen sind beide Hunter-Call-Sites, die sieben
Wrapperargumente, das feste achte Coreargument, die Hunter-/Beute-Rohfelder,
der diskrete Returnwert, die Query-Distanzpässe und die ausschließlichen
Schreibzugriffe auf `context + 0xC`.

Verbleibende Schritte:

1. Die jetzt bestätigte Reihenfolge Typ-Hook -> Distanz -> Sicht -> bestes Ziel
   in Resolver-/Regressionstests festhalten.
2. Referenz-RVAs und semantische Pattern festlegen.
3. Resolvertests für Referenzhash, eindeutigen Fallback, fehlenden und
   mehrdeutigen Treffer schreiben.
4. Den privaten Sichtkontext zunächst verhaltenneutral im Game-Thread gegen
   beobachtete Vanilla-Ergebnisse validieren.
5. Den nativen `ArcherArrow`-Kollisionspfad analysieren und eine physische
   Bahnprobe oder belegte konservative Alternative festlegen.
6. `c_game_unit_issueorder_movehere` bei Referenz-RVA `0x196230`, seine
   semantische Signatur, Rückgabewerte und die darunterliegende native
   Pfadannahme gegen Laufzeitwerte kalibrieren. Falls eine verhaltenneutrale
   native Pfadabfrage verwendet werden soll, muss sie separat identifiziert und
   auf Seiteneffekte geprüft werden.
7. `UpdateToNewDLL.md` zunächst um die belegten Analyseergebnisse ergänzen.

Gate: Kein produktiver Probeaufruf vor erfolgreicher A/B-Validierung des
privaten Kontexts und der World-/Height-Erzeugung für hypothetische Positionen.

### Phase 2: Verhaltenneutrale Probe

1. `HunterNativeVisibilityProbe.cs` implementieren.
2. Tatsächlich beobachtete und im Idle-Fall durch die Discovery vorgeschlagene
   Hunter/Beute-Paare aller aktivierten Typen prüfen; keine Bewegung und keine
   Queryänderung.
3. Ergebnisse mit drei bekannten Ingame-Fällen vergleichen.
4. Prüfen, dass Jägerhütten nativ sichtbar, Kornspeicher/Holzfällerhütten,
   Mauern/Tore/Türme und die reproduzierten Höhenlinien nativ blockiert
   gemeldet werden; physische Bahnresultate getrennt protokollieren.
5. Stabilität mit mehreren Jägern testen; keine Hooks installieren.

Gate: Probeergebnis und beobachteter Vanilla-Schuss/Abbruch müssen für alle
Kontrollfälle übereinstimmen. Sonst keine Recovery aktivieren.

### Phase 3: State-Machine isoliert implementieren

1. Aktuellen einmaligen Recovery-Code durch das Phasenmodell ersetzen.
2. Uhr, Probe, Pathfinder, Move-Ausgabe und Log als kleine testbare Adapter
   injizieren beziehungsweise kapseln.
3. Reine Zustands- und Kandidatentests ohne Spielprozess ausführen.
4. Beide Startwege (`Discovering` ohne Ziel und `Observing` nach Zielabbruch)
   einschließlich inkrementeller Kandidatensuche testen.
5. Noch keine allgemeine Query-Sperre aktivieren.

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

1. Mehrere Jäger und mehrere Beutetiere verschiedener Typen testen.
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

### Phase 6: Späterer Chore für Multiplayer-Synchronisation

Diese Phase beginnt erst, wenn der kanonische Script Extender mindestens
Version `1.50.0` erreicht hat.

1. Verfügbare Autoritäts-, Tick- und Netzwerk-APIs der Mindestversion erneut
   prüfen und die oben festgelegte Host-/Lockstep-Strategie verbindlich wählen.
2. Die vorhandene Recovery-State-Machine synchronisieren; keine zweite
   Multiplayer-spezifische Zielwahl oder Kandidatensuche parallel anlegen.
3. Zielidentität, Recovery-Phase, Move-Ausgabe und Abbruchursache so wenig wie
   möglich und mit explizitem MessagePack-Formatter übertragen, falls die neue
   Script-Extender-API keine geeignetere deterministische Replikation bietet.
4. Host-/Client-, Save/Load-, Reconnect- und Desync-Tests ausführen.
5. Erst nach diesen Prüfungen Multiplayer freischalten und den für diesen Chore
   abschließenden Build einmal über `build.bat /nopause` ausführen.

Gate: Bis Phase 6 abgeschlossen ist, meldet die Recovery im echten Multiplayer
einmal begrenzt den Versions-/Chore-Grund und bleibt vollständig inaktiv. Nach
Abschluss gehört Multiplayer zum unterstützten Funktionsumfang.

## Testmatrix

### Automatisierte State-Machine-Tests

- Limit vor Trigger: 0, 1, 2 und 3 blockierte Abbrüche.
- Abbruchfenster überschritten.
- Passendes Projektil innerhalb des Zwei-Sekunden-Fensters verhindert
  pre-shot Recovery.
- Native Probe frei trotz Gebäude-Metadaten: keine Recovery.
- Native Probe blockiert ohne Gebäude: Recovery wegen Gelände.
- Keine Kandidaten, keine Route sowie inkrementelle Fortsetzung nach
  ausgeschöpftem Pro-Batch-Budget ohne Falschabbruch.
- Langer Mauerzug: gültiger Umweg und Schussposition liegen mehr als acht Tiles
  vom Start entfernt.
- Managed-Pathfinder meldet keinen Weg, native MoveHere-Annahme plus Fortschritt
  bestätigt aber einen Weg: Recovery darf nicht vorzeitig verwerfen.
- Managed-Pathfinder meldet einen Weg, MoveHere liefert `0`: keine Behauptung
  nativer Erreichbarkeit und kein Schuss aus einem nur heuristischen Kandidaten.
- Ein erreichbares Schusstile bei nativ unerreichbarer Beute reicht nicht; der
  Plan endet ohne Angriff. Der alternative Anulus wird erst nach unabhängig
  bestätigter Beute-Erreichbarkeit freigegeben.
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
- Idle-Jäger ohne je zugewiesenes Ziel startet über `Discovering`, wenn die
  pfaderreichbare Beute nur an Vanillas Sichtprüfung scheitert.
- Querys während `Moving` werden unterdrückt; in `Reacquire` wird nur das exakte
  Slot-/Global-ID-/Typ-Ziel angenommen.
- Mod, konkrete Beuteart und verbesserte Wegfindung werden in jeder Phase
  deaktiviert.
- Mapreset und Dispose entfernen alle Pläne und In-Flight-Moves.
- Cooldown nur bei endgültigem Abbruch, nicht während aktiver Recovery.
- Logzählerinvarianten und Loglimits.
- Autoritätsmodus: genau eine wirksame Move-Order; Lockstep-Modus:
  deterministische Ziel-/Tilewahl und tickgleiche Übergänge auf Host und Client.

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
- Wrapper besitzt sieben Argumente; das achte Coreargument ist konstant `0`.
- Zielsuche akzeptiert exakt Helperwerte `1..432`, der direkte Orderpfad nur
  Werte `> 0`.
- Der private mindestens 16 Byte große Kontext wird nur an `+0xC` beschrieben;
  Canarybytes außerhalb bleiben unverändert und Vanillas globaler Kontext wird
  vor/nach der Probe nicht verändert.
- Manhattan-Distanzen `5`, `6`, `20`, `21`, `53` und `54` treffen exakt die
  beiden Querypässe und die belegte Sichthelper-Aufruf-/Bypass-Grenze.
- Referenzhash löst MoveHere direkt bei `0x196230` auf; statisch bekannte
  Rückgaben bleiben exakt `0` oder `1`, der Post-Hook wird mit Path-State und
  Fortschritt korreliert.

### Ingame-Fälle

1. Kornspeicher als Sichtblocker: Jäger bewegt sich, erreicht freie Linie,
   schießt, sammelt ein und liefert Fleisch ab.
2. Holzfällerhütte als Sichtblocker: gleicher vollständiger Ablauf.
3. Jägerhütte als Sichtlinie: native Probe bleibt positiv, die physische
   Bahnprüfung sperrt den Kandidaten jedoch vor dem Schuss; der Jäger bewegt
   sich zu einer freien Bahn und schießt erst dort. Für einen bereits gestarteten
   und dennoch feststeckenden Pfeil folgt weiterhin `DamageUnitRanged`, ein
   `0x6E`-Kadaver und anschließend Abholung.
4. Gelände `80 -> 140/170 -> 80` ohne Gebäude: native Probe erkennt Blockade,
   Jäger sucht eine tatsächlich freie Position.
5. Blocker wird während des Wartens abgerissen: Recovery erkennt die freie
   aktuelle Linie, beendet Bewegung und lässt Vanilla sofort fortfahren.
6. Kein erreichbares Schusstile: erst nach vollständiger inkrementeller Suche
   begrenzter Abbruch ohne Endlosschleife.
7. Zweiter Kornspeicher und mehrere Hühner: keine Zielwechselkaskade.
8. Drei Jäger: keine gemeinsame Reservierung, kein gegenseitiges Überschreiben
   von RecoveryPlans.
9. Huhn wandert während der Bewegung: begrenzte Neuplanung.
10. Huhn wird von anderem Jäger getötet/reserviert: sauberer Planabbruch und
    normale neue Zielwahl.
11. Freie Kontrolllinie ohne Blocker: kein Recovery-Eingriff.
12. Reh, Ziege, Hase, Kamel, Huhn und Kuh jeweils einzeln: hinter Gebäude und
    Mauer bei vorhandenem Fußweg Recovery, freies Schussfeld, echtes Projektil,
    korrekter Kadaver, Abholung und Fleischabgabe. `HuntCow` muss dabei auch im
    Runtime-Eligibility-Pfad wirksam sein.
13. Nichtjäger-Fernkampf: keine automatische Hühnerzielwahl; manueller Angriff
    bleibt nach bestehender Policy möglich.
14. `EnableMod`, `ImprovedPathfinding` oder die jeweilige `Hunt...`-Option aus:
    Vanilla ohne Recovery-Eingriff für den betroffenen Typ.
15. Speichern/Laden und Kartenwechsel während beziehungsweise nach einer
    Recovery: keine übernommene Planidentität.
16. Späterer Chore ab Script Extender `1.50.0`: echter Multiplayer Host/Client
    und Multiplayer-Save erzeugen keine doppelten Moves, abweichenden Ziele oder
    Desynchronisation; das Diagnoselog benennt die verwendete
    Autoritätsstrategie. Vor Abschluss dieses Chores bleibt die Recovery dort
    nachweislich vollständig deaktiviert.
17. Bewegungsziel auf einem lebenden Tier: MoveHere-Return, Path-State und
    tatsächlicher Annäherungspfad sind kalibriert; eine abweichende native
    Occupancy-/Nearest-Approach-Semantik wird vor Aktivierung implementiert.

## Abnahmekriterien

Die Singleplayer-Erstimplementierung gilt nach Phase 5 als fertig, wenn alle
nicht ausdrücklich dem späteren Multiplayer-Chore zugeordneten Punkte erfüllt
sind. Das vollständige plattformübergreifende Ziel ist erst nach Phase 6
abgeschlossen:

- Ein Jäger bleibt nach einem Recovery-Move nicht mehr in der beobachteten
  Zielwechsel-/Warten-Schleife.
- Kornspeicher, Holzfällerhütte und der gebäudefreie Höhenfall führen bei
  vorhandenem Weg zu einer freien Schussposition und anschließend zu einem
  echten Projektil.
- Dasselbe gilt für Mauern/Tore/Türme und für jeden aktivierten Beutetyp; ein
  mehr als acht Tiles langer notwendiger Umweg wird nicht vorzeitig verworfen.
- Die Fußwegbedingung wird durch Vanillas native Move-Annahme und beobachteten
  Fortschritt belegt, nicht allein durch den Managed-Pathfinder oder ein
  erreichbares Schusstile.
- Ein Jäger kann die Recovery bereits aus „Kein Wild“/Ziel `0` beginnen; ein
  vorheriger nativer Zielabbruch ist keine notwendige Voraussetzung.
- Jägerhütten bleiben in der nativen Sichtprobe als Vanilla-Ausnahme erkennbar,
  werden aber durch die physische Bahnprüfung vor neuen Hunter-Schüssen
  ausgeschlossen. Die Projektilkompensation erzeugt für unvermeidbare
  Restfälle weiterhin den einsammelbaren ranged-Kadaverzustand.
- Kein `KillUnit` wird für neue Schüsse verwendet.
- Keine rohe Ziel-ID wird ohne vorher vollständig validierten Vanilla-Übergang
  geleert oder gesetzt.
- Keine Reservation `2` bleibt ohne exakt zugeordneten lebenden Jäger zurück.
- Kein nativer Inline-Hook wird an einer früheren Crashstelle installiert.
- Referenzhash verwendet direkt validierte RVAs; abweichender Hash verwendet
  nur eine eindeutige semantische Pattern-Auflösung.
- Bei Auflösungs-/Probe-/Recoveryfehler bleibt Vanilla aktiv und nur dieses
  Feature wird deaktiviert.
- Vor Script Extender `1.50.0` und bis zum Abschluss des dedizierten Chores ist
  die Recovery in echtem Multiplayer fail-closed; danach gehören Host, Client
  und Multiplayer-Save zum getesteten unterstützten Funktionsumfang.
- Logs enthalten Millisekunden-Zeitstempel, stabile Identitäten, begrenzte
  Wiederholungen und erfüllte Zählinvarianten.
- Runtime-Tests, Native-Resolvertests, CRLF-Prüfung und der abschließende einzelne
  Build laufen ohne Fehler oder Warnungen durch.

## Empfohlene Entscheidung

Die empfohlene Umsetzung ist eine persistente, querybewusste Recovery-
State-Machine mit Vor-Zielzuweisungs-Discovery, Vanillas nativem
Geometriehelper, einer getrennt validierten physischen Pfeilbahn sowie dem
vorhandenen Managed-Pathfinder als günstiger Routenvorauswahl. Native
Move-Annahme und beobachteter Fortschritt bleiben die Autorität für tatsächliche
Erreichbarkeit. Die Lösung gilt einheitlich für alle aktivierten Beutetypen und
untersucht pfadgeleitet auch Umwege jenseits von acht Tiles. Das behebt das
eigentliche Vanilla-Problem, ohne Gebäude allgemein durchsichtig zu machen und
ohne den Angriff oder Kill künstlich vorwegzunehmen.

Der aktuelle einmalige `MoveToTile`-Workaround soll dabei vollständig ersetzt
und nicht als paralleler Fallback behalten werden. Der bestehende
`DamageUnitRanged`-Fallback bleibt dagegen absichtlich bestehen, weil er ein
anderes, erst nach einem echten Schuss auftretendes Vanilla-Problem behandelt.
