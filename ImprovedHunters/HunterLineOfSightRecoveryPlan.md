# ImprovedHunters: Sichtlinie, Erreichbarkeit und Vanilla-Pfadfortsetzung

Stand: `2026-08-17`

Aktueller Quellstand: `1.1.45`; letzter ingame getesteter Stand: `1.1.44`

## Zweck dieses Dokuments

Dieses Dokument ist die Übergabe- und Arbeitsgrundlage für einen neuen Chat.
Es trennt bewusst:

1. das fachliche Ziel,
2. bestätigte Vanilla- und Native-Erkenntnisse,
3. den bereits implementierten Stand,
4. noch ausstehende Abnahmetests,
5. danach zu bearbeitende Verbesserungen und
6. verworfene Ansätze, die nicht erneut eingeführt werden dürfen.

Die frühere Idee einer vollständig eigenen Recovery-State-Machine mit eigenen
Bewegungsbefehlen ist **nicht mehr der aktuelle Lösungsweg**. Die Tests haben
gezeigt, dass Vanilla bereits korrekt um Hindernisse navigiert, sobald es einen
Pfad zur Beute angenommen hat. ImprovedHunters soll diesen Vanilla-Pfad
erhalten, unerreichbare Beute früh aussortieren und Vanillas regulären Angriff
wieder freigeben, sobald die Sichtlinie frei ist.

## Fachliches Ziel

Ein Jäger soll jede aktivierte Beuteart auch hinter Gebäuden, Mauern, Toren,
Türmen oder Geländeanstiegen berücksichtigen, wenn ein gültiger Fußweg zur
Beute besteht. Er soll Vanillas Weg um das Hindernis weiterlaufen, an der ersten
geeigneten Position mit freier Sichtlinie angreifen und anschließend Vanillas
normalen Kadaver-, Einsammel- und Fleischabgabeprozess verwenden.

Vollständig unerreichbare Beute soll für den jeweiligen Jäger bereits vor der
Distanzrangfolge verworfen werden. Sie darf näher liegende, aber erreichbare
Beute nicht verdrängen. Die Erreichbarkeit muss regelmäßig erneut geprüft
werden, damit ein geöffnetes Tor oder eine entfernte Mauer das Tier wieder
zulässt.

Die Jägerhütte soll keine Sichtlinienausnahme mehr besitzen. Sie soll wie andere
Gebäude die Ziel- und Schussfreigabe blockieren, weil ein bereits gestarteter
Pfeil sonst physisch in der Hütte hängen bleiben kann.

Das Ziel gilt für Reh, Ziege, Hase, Kamel, Huhn und Kuh, soweit der betreffende
`Hunt...`-Schalter aktiv und der Typ im Runtime-Eligibility-Pfad tatsächlich
freigeschaltet ist.

## Aktueller Status in Kurzform

| Teilproblem | Stand | Nächster Schritt |
| --- | --- | --- |
| Verborgene Beute in Vanillas Zielsuche verfügbar machen | Implementiert; Vanilla erhält einen validierten Kandidaten, wenn seine vollständige Suche wegen Sicht `0` liefert | Regression nach PCL-Einbau |
| Vanillas angenommenen Hindernispfad im Nahbereich weiterlaufen | Implementiert und mit einem sowie mehreren Jägern bestätigt | Endgültige Abnahme und Produktionsbereinigung |
| Bei freier Sicht wieder Vanilla angreifen lassen | In den bisherigen Hindernistests bestätigt | Mit PCL-Filter erneut prüfen |
| Jägerhütte als normalen Sichtblocker behandeln | Seit `1.1.41` implementiert; Ingame laut Beobachtung funktionsfähig | Gezielter Regressionstest |
| Unerreichbare Beute früh verwerfen | Seit `1.1.44` produktiv über PCL-Vorfilter implementiert und im gemischten Test bestätigt | Kurzer Regressionstest mit `1.1.45` |
| „Kein Wild“ bei ausschließlich unerreichbarer Beute | Im `1.1.44`-Test wie erwartet beobachtet | Kurzer Regressionstest mit `1.1.45` |
| Geänderte Erreichbarkeit zeitnah erkennen | Öffnen eines Zugangs wurde mit Ein-Sekunden-Cache bestätigt | Kurzer Regressionstest mit `1.1.45` |
| Während des Anmarschs neu unerreichbares Ziel verwerfen | Seit `1.1.45` über den persistenten Scan und Vanillas State-1-Identitätsfehlerpfad implementiert | Ausgewähltes Reh während des Anmarschs einschließen und Zielwechsel prüfen |
| Mehrere Jäger | Nach Entfernung einer künstlichen globalen Diagnosegrenze bestätigt | Kurzer Regressionstest nach Kernabnahme |
| Unpassend langsames Schleichen bei langem Umweg | Beobachtet, bewusst nachgelagert | Restpfadlänge und Vanilla-Bewegungsstufen analysieren |
| Erfolgreicher Schuss ohne sichtbares Einsammeln bei bewegter Herde | Offen; nicht durch das PCL-Logging verursacht | Isolierter Ein-Reh-Test mit separatem Logging |
| Alle sechs Beutearten | Architektur gemeinsam, aber noch nicht vollständig ingame abgenommen | Typmatrix nach Kernabnahme |
| Multiplayer | Soll unterstützt werden, derzeit absichtlich fail-closed | Eigener Chore ab Script Extender `1.50.0` |

## Aktuelle Lösungskette

Der aktuelle Lösungsweg verwendet möglichst vollständig Vanillas eigene
Ziel-, Pfad-, Bewegungs- und Angriffslogik:

1. `OnHunterQueryTarget` validiert Jäger und Kandidat über stabile Slot- und
   Global-IDs sowie Beutetyp, Besitzer, Alive-State und aktive Einstellungen.
2. `HunterPclReachability` prüft bereits vor der kostenbasierten Rangfolge, ob
   Jäger- und Beutetile in für diesen Spieler verbundenen Path Connection
   Layers liegen.
3. Nur ein belastbares PCL-Ergebnis `0` verwirft den Kandidaten. Ein positives
   Ergebnis beweist noch keinen detaillierten Weg und lässt Vanilla deshalb
   vollständig autoritativ.
4. Dieselbe PCL-Prüfung läuft nochmals beim konkreten Vanilla-Kandidaten-
   Handoff. Damit kann auch ein leerer oder veralteter Ranking-Cache keinen
   bekannten unerreichbaren Kandidaten über einen allgemeinen Fallback wieder
   zulassen.
5. Liefert Vanillas vollständige State-0-Zielsuche trotz eines gültigen
   verborgenen Kandidaten `0`, stellt der begrenzte Zielsuche-Fallback genau
   diesen Kandidaten bereit. Vanilla schreibt danach selbst Zielidentität,
   Reservierung, Pfad und AI-State und ruft selbst `MoveHere` auf.
6. Ist `MoveHere` erfolgreich, folgt der Jäger Vanillas eigenem Hindernispfad.
7. Während State `1` prüft der vorhandene persistente Scan das aktive Ziel über
   denselben PCL-Cache erneut. Bei bestätigtem PCL-Ergebnis `0` macht er nur die
   gespeicherte Ziel-Global-ID ungültig und gibt die Reservation
   identitätsgesichert frei. Im nächsten Update stoppt und verwirft Vanilla den
   alten Auftrag über seinen eigenen Identitätsfehlerpfad und sucht selbst neu.
8. Sobald Vanilla bei nativer Distanz `<= 28` trotz noch aktivem Pfad wegen
   blockierter Sicht vorzeitig angreifen würde, lässt die Pfadfortsetzung für
   genau diesen Update-Aufruf Vanillas bereits vorhandene Distanz-29-Stufe
   laufen. Der Mod gibt dabei keinen Move aus und schreibt weder Ziel-, Pfad-,
   Order- noch AI-State-Felder.
9. Sobald die native Sichtprobe positiv wird, endet dieser Eingriff sofort.
   Vanilla greift regulär an.
10. Nach einem echten Projektilspawn übernimmt ausschließlich die bestehende
   identitätsgesicherte Projektilkompensation. Sie kann bei einem feststeckenden
   Pfeil `DamageUnitRanged` verwenden; `KillUnit` bleibt ausgeschlossen.

Diese Kette ist der gegenwärtig bevorzugte Produktionsansatz. Eine eigene Suche
nach Schusstiles oder eigene Bewegungs-State-Machine ist nur wieder zu erwägen,
wenn ein reproduzierbarer Fall trotz positiver PCL-Verbindung, angenommenem
Vanilla-Pfad und funktionierender Pfadfortsetzung nicht lösbar ist.

## Bestätigte Vanilla-Semantik

### Zielsuche und Sicht

- Die Hunter-Zielsuche läuft über Unit-Slots, Alive-State, Typ, Reservierung,
  Distanz und danach Sicht.
- Der öffentliche `OnUnitHunterQueryTarget`-Detour greift vor den späteren
  Vanilla-Distanz- und Sichtprüfungen ein. Das Zulassen eines Tierstyps allein
  überspringt die Sichtprüfung daher nicht.
- Vanillas Zielsuche verwendet Manhattan-Distanz in zwei Pässen:
  zunächst Kandidaten mit Distanz `> 20`, danach bei fehlendem Ziel Kandidaten
  mit Distanz `> 5`.
- Bei Distanz `< 54` muss der gemeinsame Sichtwrapper einen Wert `1..432`
  liefern. `0` verwirft den Kandidaten.
- Ab Distanz `54` kann die frühe Zielsuche die Sichtprüfung überspringen. Der
  spätere direkte Hunter-Orderpfad prüft die Sicht dennoch erneut.
- Der gemeinsame Sichtwrapper arbeitet mit Weltkoordinaten, Unit-Höhen,
  Tileflags und Hindernishöhen. Eine Bresenham-Tilelinie oder reine
  Gebäudeliste ist kein zuverlässiger Ersatz.

### Vanillas Distanz-28-Fehler

- Für native Distanz `> 28` lässt State `1` den bereits angenommenen
  Vanilla-Pfad weiterlaufen. Dadurch kann Vanilla schon selbst lange Wege um
  Mauern und Gebäude navigieren.
- Bei nativer Distanz `<= 28` versucht Vanilla abhängig von seinen Pfadfeldern
  den direkten Angriff. Ist die Sicht noch blockiert, führt der Fehlschlag zu
  State `6`, Rückkehrtimer `20` und dem Rückweg zur Jägerhütte.
- Im Lauf mit `1.1.38` wurden `41` direkte State-1-Angriffsresultate erfasst:
  `40` Fehlschläge, alle bei Distanz `<= 28`, und ein erfolgreicher Angriff.
- Im aussagekräftigsten Fall lief der Jäger `44,383 s` auf Vanillas eigenem
  Pfad bis Fortschritt/Länge `59/61`. Exakt bei Distanz `28` und weiterhin
  blockierter Sicht brach er ab und kehrte um.
- `GameUnit +0xF6` ist der beobachtete Pfadfortschritt, `+0xF8` die Pfadlänge
  und `+0xF2=2` der aktive Pfadstatus. Ein erneuter `MoveHere`-Aufruf setzt den
  Fortschritt zurück und ist daher keine Fortsetzung.
- Die aktuelle Lösung ändert im sicheren Hookfenster ausschließlich den
  temporären Distanzregisterwert `RDI` von höchstens `28` auf `29`, wenn
  State `1`, Zielidentität, aktiver unvollständiger Pfad und blockierte native
  Sicht zusammenpassen.
- Die Fortsetzung endet bei freier Sicht, Pfadende, Kontextwechsel, mehr als
  `60 s` kontinuierlicher Fortsetzung oder `3 s` ohne Pfadfortschritt. Ein
  echter Grenzabbruch erhält einen Retry-Cooldown von `5 s`.
- Die frühere globale Grenze von zwei Zielidentitäten pro Karte war nur eine
  Diagnosebegrenzung und verursachte das scheinbar positionsabhängige Versagen
  weiterer Jäger. Seit `1.1.40` gibt es höchstens einen unabhängigen Zustand pro
  Jäger und keine globale Identitätsgrenze. Danach funktionierten alle
  gleichzeitig getesteten Jäger.

### PCL-Erreichbarkeit

- `GamePlayerManagerAPI.GetNextReachablePCLToDestinationForPlayer` verwendet
  Vanillas spielerabhängige Path Connection Layers einschließlich dynamischer
  Verbindungen wie Toren.
- Vanillas `MoveHere` ruft dieselbe Funktion vor der detaillierten
  Pfaderzeugung auf. Ein PCL-Rückgabewert `0` führt dort unmittelbar zum
  Fehlschlag.
- Der aktuelle Aufruf verwendet exakt die Eingaben des Jägers:
  `r_ControllableForPlayerId`, den Modus aus `GameUnit +0x35C`, Quell-PCL des
  aktuellen Jägertiles und Ziel-PCL des aktuellen Beutetiles.
- Im Kalibrierungslauf `1.1.43` wurden `53` Kandidatenprobes und `10` exakte
  PCL/`MoveHere`-Korrelationen erfasst. `4/4` positive PCL-Ergebnisse stimmten
  mit `MoveHere=1` und `6/6` Nullergebnisse mit `MoveHere=0` überein. Es gab
  keine Fehlkorrelation und keinen PCL-Fehler; Eingaben und Zeitfenster stimmten
  in allen zehn vergleichbaren Fällen.
- Der reale Jägermodus war in den Tests stets `0`. Die zusätzlich geprüften
  Modi `0`, `2` und der live ausgelesene Modus lieferten in allen beobachteten
  Fällen dasselbe Ergebnis. Produktiv wird trotzdem ausschließlich der reale
  Modus verwendet.
- Der erste Warm-up-Aufruf dauerte etwa `170 us`; normale Aufrufe lagen
  überwiegend bei `1..3 us`, im Mittel bei ungefähr `4,43 us`. Ingame war keine
  Suchverzögerung sichtbar.
- Ein PCL-Ergebnis `0` ist eine zuverlässige konservative Ausschlussbedingung.
  Ein positives Ergebnis beweist nur die grobe Verbindung; Vanillas
  detaillierte `MoveHere`-Pfaderzeugung bleibt danach maßgeblich.
- Version `1.1.44` cached nur identische Jäger-/Beuteidentitäten und identische
  Spieler-, Modus-, Quell-PCL- und Ziel-PCL-Eingaben für höchstens eine
  Sekunde. Geänderte Eingaben umgehen den Cache sofort. Unveränderte
  Verbindungen werden spätestens nach einer Sekunde erneut nativ geprüft.
- Seit `1.1.45` verwendet auch der vorhandene persistente 100-ms-Scan diese
  Abfrage für das aktive Ziel eines State-1-Jägers. Ein bestätigtes Nullergebnis
  löscht ausschließlich dessen gespeicherte Ziel-Global-ID. Dadurch gelangt
  `HunterUpdate` in Vanillas eigenen Identitätsfehlerpfad, stoppt dort den alten
  Auftrag und führt selbst die nächste Zielsuche aus. Der Mod schreibt dabei
  weder AI-State noch Pfad-/Orderfelder und gibt keinen eigenen Move aus.
- Eingabe-, API- oder Nativefehler sind fail-open: Der Kandidat bleibt für
  Vanilla verfügbar. Ein technischer Fehler darf kein erreichbares Tier
  fälschlich entfernen.

### Jägerhütten-Ausnahme

- Gebäudetyp `7` ist `STRUCT_HUNTERS_HUT`.
- Der gemeinsame Höhenhelper leitete diesen Typ im hindernisbewussten Modus
  über einen Sonderfall, der nur Geländehöhe zurückgab.
- Vanillas normale Gebäudehöhentabelle enthält für Typ `7` bereits die
  Blockerhöhe `40`, genau wie für die Holzfällerhütte.
- `HunterHutVisibilityPatch` ändert am auditierten Dispatch genau einen Wert
  von Sonderfall `0` auf normalen Gebäudehöhenfall `3` und stellt den
  Originalwert konfliktgesichert wieder her.
- Der Benutzer bestätigte ingame, dass die Jagdhütte danach offenbar normal
  blockiert. Ein gezielter Regressionstest bleibt Teil der Endabnahme.
- Eine allgemeine eigene Projektilbahnprüfung wird nicht parallel entwickelt.
  Sie wird nur wieder geöffnet, wenn nach dem Patch eine positive native Sicht
  reproduzierbar zu einer Kollision an einem anderen Gebäude führt.

## Beobachtete Ingame-Ergebnisse

Folgende Beobachtungen sind für die weitere Arbeit als bestätigt zu behandeln:

- Außerhalb der nativen Distanz `28` läuft ein Jäger bereits in Vanilla einen
  langen korrekten Weg um Hindernisse zur Beute.
- Wird seine Sicht während dieses Anmarschs blockiert, kehrt er im Vanilla-
  Fehlerfall beim Eintritt in den Nahbereich zur Hütte um.
- Die Distanz-29-Fortsetzung ließ einen bereits innerhalb `28` gestarteten
  Jäger ohne eigenen Mod-Move um mehrere Hindernisse laufen. Bei freier Sicht
  schoss Vanilla erfolgreich.
- Nach Entfernung der globalen Zwei-Identitäten-Grenze funktionierten alle im
  Mehrjägertest eingesetzten Jäger unabhängig.
- Bei ausschließlich unerreichbaren Rehen wartet der Jäger neben der Hütte.
  Vor `1.1.44` meldete er dabei nicht „Kein Wild“ und nahe unerreichbare Rehe
  wurden über mehrere Suchintervalle nacheinander verworfen.
- Im `1.1.43`-Test begann die Suche im erreichbaren Fall unverzögert und der
  Jäger lief korrekt los. Im unerreichbaren Fall blieb er wie zuvor an der
  Hütte. Das bestätigt die Geschwindigkeit und Korrelation der PCL-Abfrage,
  noch nicht die produktive Vorfilterung von `1.1.44`.
- Im `1.1.44`-Abnahmetest wählte der Jäger trotz mehrerer näherer vollständig
  eingeschlossener Rehe sofort das weiter entfernte erreichbare Reh. Nach dem
  zusätzlichen Blockieren aller Tiere folgten „Kein Wild“ und nach Öffnen eines
  Zugangs die erneute Suche innerhalb weniger Sekunden wie erwartet.
- Wurde das bereits ausgewählte erreichbare Reh erst während des Anmarschs neu
  eingeschlossen, lief der Jäger mit seinem alten Auftrag weiter bis zur neuen
  Mauer und wurde beim Scheitern der unerreichbaren Zielposition aufgelöst. Der
  Loglauf ordnete dies Jäger `1/370` und Reh `16/294` zu: Auswahl mit PCL `1`,
  Pfadfortschritt bis `47/50`, danach Unit-Delete; ein neuer Jäger sah dasselbe
  Reh anschließend mit Ziel-PCL `10` und PCL-Ergebnis `0`. Dieser Befund ist der
  Anlass für die aktive State-1-Zielprüfung von `1.1.45`.
- Rehe bewegten sich während mehrerer Tests. Ein Jäger kann dadurch zu einer
  inzwischen veralteten Position laufen. Die heutige Vanilla-Pfadfortsetzung
  hält kein eigenes statisches Schusstile und reagiert bei freier Sicht wieder
  über Vanilla; dennoch bleibt bewegte Beute ein Regressionstest.
- In einem Lauf schoss der Jäger erfolgreich auf Reh `10/287`, sammelte den
  Kadaver sichtbar aber nicht ein und suchte ungefähr drei Sekunden später ein
  anderes Reh. Beim Projectile-Delete hatte sich Zielidentität oder Zielzustand
  bereits geändert, weshalb die identitätsgesicherte Kompensation nicht
  eingriff. Da sich viele Rehe gleichzeitig bewegten, ist noch offen, ob dies
  Vanilla, ein Kadaver-/Reservierungsübergang oder ein bestehender Modpfad ist.
  Die PCL-Diagnose selbst änderte kein Ziel und ist nicht die Ursache.
- Bei kurzer Luftlinie, aber langem notwendigen Umweg bewegt sich der Jäger
  auffällig langsam. Vermutlich wählt Vanilla die Schleich-/Bewegungsstufe nach
  Luftlinienentfernung statt nach tatsächlich verbleibender Pfadlänge.

## Aktuelle Dateien und Verantwortlichkeiten

| Datei | Aktueller Status |
| --- | --- |
| `src/HunterPclReachability.cs` | Produktiver konservativer PCL-Vorfilter und aktive State-1-Zielprüfung mit Ein-Sekunden-Cache, Statistiken und Fail-open-Verhalten. |
| `src/HunterPclReachabilityDiagnostic.cs` | Separat entfernbares temporäres Kalibrierungslogging für Modi, PCL/`MoveHere`-Korrelation und die State-1-Zielinvalidierung von `1.1.45`. Nach deren Abnahme entfernen. |
| `src/HunterNativeVisibilityProbe.cs` | Validierte native Sichtprobe ohne eigenen Inline-Hook; Voraussetzung der Pfadfortsetzung. |
| `src/HunterHutVisibilityPatch.cs` | Produktive, validierte Ein-Byte-Korrektur der Jägerhütten-Ausnahme. |
| `src/HunterTargetSearchFallbackDiagnostic.cs` | Enthält derzeit sowohl den verhaltensändernden State-0-Kandidaten-Handoff als auch temporäre Beobachtungslogs. Nach Kernabnahme Produktionslogik und entfernbares Logging sauber trennen und Datei passend umbenennen. |
| `src/HunterVanillaPathContinuationDiagnostic.cs` | Enthält derzeit die funktionierende Distanz-29-Pfadfortsetzung samt begrenztem Diagnosezustand. Nach Kernabnahme produktiven Hook/Guards von entfernbaren Detailinformationen trennen und Datei passend umbenennen. |
| `src/HunterVisibilityDiagnostic.cs` | Breite ältere Diagnose; nach Abschluss der gezielten Abnahme entfernen, sofern keine noch benötigten Marker darin verbleiben. |
| `src/HunterLineOfSightRecovery.cs` | Stillgelegter fail-closed Adapter der verworfenen Managed-A*-Recovery. Nach erfolgreicher Produktionsbereinigung entfernen statt als parallelen Fallback behalten. |
| `src/ImprovedHuntersRuntime.cs` | Eventverdrahtung, Eligibility, Zielrangfolge, PCL-Gates, Fallback-Handoff, Reservierungsbereinigung, Pfadfortsetzung und Projektilkompensation. |
| `UpdateToNewDLL.md` | Maßgebliche Detailquelle für Hash, RVA, Bytepattern, Auflösungsstrategie und Updateaudit. |

Wichtig: Der Benutzer verlangte, neues Ingame-Diagnoselogging in eigenen
Dateien unter ImprovedHunters abzulegen, damit es danach leicht entfernt werden
kann. Neue Diagnose für den Schuss-/Einsammelübergang muss diese Regel ebenfalls
befolgen. Bei der Produktionsbereinigung dürfen notwendige Verhaltenshooks
nicht zusammen mit temporärem Logging entfernt werden.

## Sicherheitsgrenzen und nicht erneut zu verwendende Ansätze

### Kein synchrones Managed-A*

`GameTileManagerAPI.FindPath` ist ein verwalteter A*-Pathfinder über bis zu
`800 x 800` Tiles. Seine Open-Set-Operationen sind linear und er besitzt kein
hartes Expansions-, Zeit- oder Abbruchbudget. Beim ersten unerreichbaren
Beutefall fror dadurch der Spielthread ein.

Deshalb gilt dauerhaft:

- kein synchroner `GameTileManagerAPI.FindPath`-Aufruf in Zielrangfolge,
  Recovery oder Schusstilesuche,
- die Chebyshev-Kosten bleiben nur Heuristik,
- PCL `0` ist der schnelle Negativfilter,
- PCL positiv bleibt Vanillas detaillierter Pfaderzeugung überlassen.

### Keine eigene isolierte Move-/AI-State-Rekonstruktion

Die Versuche `1.1.31` bis `1.1.35` zeigten:

- Ein eigener `MoveHere` kann zwar `1` und einen gültigen Pfad liefern, wird
  aber durch Vanillas Hunter-State-Writer unmittelbar überschrieben.
- Das nachträgliche Schreiben von Ziel, Reservierung und AI-State `1` bildet
  den atomaren Vanilla-State-0-Erfolgspfad nicht korrekt nach.
- Eine längere Querysperre band den Jäger an veraltete Beutepositionen, führte
  zu fehlender Rücklaufanimation und erneutem Hin-und-her.

Darum keine eigene Move-Ausgabe, kein direktes Setzen von AI-State und kein
ungeprüftes Leeren oder Setzen der Targetfelder als Recovery-Lösung.

### Keine unsicheren Diagnosehooks

- Frühere Inline-Hooks an ungeeigneten Fenstern beziehungsweise im gemeinsamen
  Sichthelper verursachten CTD oder einen unsicheren Callbackkontext.
- Die früheren Crashstellen `0x18EE14`, `0x130171` und `0x12FF53` dürfen nicht
  erneut gehookt werden.
- Neue Hooks benötigen semantische Bytevalidierung, sicheres Hookfenster,
  Referenzhashbindung oder eindeutigen Resolver und einen getrennten
  Fehlerpfad.

### Kein KillUnit-Fallback

Der alte `KillUnit`-Fallback erzeugte nach einem steckengebliebenen Pfeil den
nicht einsammelbaren Zustand `0x6F`. Seit `1.1.27` wird ausschließlich Vanillas
`DamageUnitRanged` mit echter Projektil- und Zielidentität verwendet. Das ist
ein post-shot Sicherheitsnetz und keine pre-shot Sichtlinienlösung.

## Nächste Arbeitspakete in verbindlicher Reihenfolge

### Paket A: Version 1.1.45 – PCL-Vorfilter und aktives Ziel abnehmen

Die Fälle A1 bis A3 liefen im `1.1.44`-Test wie erwartet. Mit `1.1.45` werden
sie kurz als Regression wiederholt; der neue Kernfall ist A4. Vor weiteren
Verhaltensänderungen müssen dessen Logs maschinell geprüft werden.

#### A1: Gemischte Erreichbarkeit

Aufbau:

- ein Jäger,
- mehrere nähere, vollständig eingeschlossene Rehe,
- genau ein weiter entferntes, aber fußläufig erreichbares Reh,
- alle Kandidaten möglichst innerhalb der normalen Zielsuche, insbesondere das
  erreichbare Reh innerhalb von `54` Tiles,
- die unerreichbaren Rehe näher als das erreichbare Reh.

Hier müssen erreichbare und unerreichbare Tiere gleichzeitig existieren, weil
genau die Priorisierung geprüft wird. Zufall wird reduziert, indem es nur ein
erreichbares Tier gibt.

Erwartung:

- Der Suchbeginn ist unverzögert.
- Alle PCL-getrennten Rehe werden bereits vor der Distanzrangfolge verworfen.
- Der Jäger wählt sofort das weiter entfernte erreichbare Reh.
- Es gibt keine mehrsekündige Kette echter `MoveHere=0`-Versuche für die
  eingeschlossenen Tiere.
- Der Jäger läuft Vanillas Weg um die Hindernisse und schießt bei freier Sicht.

#### A2: Ausschließlich unerreichbare Beute

Aufbau:

- frischer Kartenstart oder sauberer Reload,
- ein Jäger,
- nur vollständig eingeschlossene Rehe.

Erwartung:

- Sämtliche Kandidaten werden in einer Suche als PCL-getrennt abgelehnt.
- Es wird kein eigener Move ausgegeben und kein unerreichbarer Kandidat über
  den Fallback wieder zugelassen.
- Der Jäger erhält kein Ziel und gerät nicht ins Pendeln.
- Optimal und als offenes Abnahmekriterium zu prüfen: Vanilla erreicht seinen
  echten „Kein Wild“-Sprachpfad. Falls die Meldung trotz eindeutig leerer
  Zielsuche ausbleibt, muss zunächst geklärt werden, ob sie an einen anderen
  Vanilla-Zustand oder Timer gebunden ist; die Meldung nicht künstlich
  abspielen, bevor dieser Pfad verstanden ist.

#### A3: Zugang wird geöffnet

Aufbau:

- direkt nach A2 einen Zugang durch Toröffnung oder Mauerabriss schaffen,
- keine anderen erreichbaren Tiere hinzufügen.

Erwartung:

- Die PCL-Verbindung wird spätestens nach Ablauf des Ein-Sekunden-Caches neu
  geprüft. Der tatsächliche Suchbeginn kann durch Vanillas Suchrhythmus einige
  Sekunden später liegen.
- Der Jäger darf beim nächsten Suchlauf das nun erreichbare Reh wählen und
  loslaufen.
- Ein früheres Nullergebnis darf nicht als Fünf-Minuten-Sperre fortbestehen.

#### A4: Aktives Ziel wird während des Anmarschs unerreichbar

Aufbau:

- ein Jäger,
- mindestens zwei zunächst erreichbare Rehe,
- das vom Jäger ausgewählte Reh erst nach Beginn seines Anmarschs vollständig
  einschließen,
- mindestens ein anderes Reh erreichbar lassen; den Fall ohne Alternative
  anschließend getrennt wiederholen.

Erwartung:

- Der persistente Scan erkennt das aktive Ziel spätestens nach Ablauf des
  Ein-Sekunden-Caches als PCL-getrennt.
- Das Log enthält `active target PCL requery` mit
  `outcome=vanilla-requery-armed`, den passenden stabilen Identitäten und einer
  plausiblen Reservationsänderung.
- Der Mod setzt nur die gespeicherte Ziel-Global-ID auf `0`; er schreibt keinen
  AI-State, keinen Pfad oder Auftrag und gibt keinen eigenen Move aus.
- `HunterUpdate` stoppt den alten Auftrag über Vanillas Identitätsfehlerpfad und
  führt selbst die nächste Zielsuche aus.
- Mit Alternative wechselt der Jäger zeitnah zum anderen erreichbaren Reh. Ohne
  Alternative endet die Suche kontrolliert; der Jäger darf nicht an der neuen
  Mauer aufgelöst werden.

#### Log-Gate für Paket A

Vor Interpretation müssen mindestens folgende Punkte geprüft werden:

- Initialisierung von `HunterPclReachability` ohne Fehler,
- relevante `pclUnreachable=True`-Kandidaten mit `allowed=False`,
- kein konkreter Kandidaten-Handoff für bekannte PCL-Nullfälle,
- keine PCL-/Callbackfehler,
- Cachetreffer und native Queries plausibel,
- nach Öffnung des Zugangs ein neues positives Ergebnis,
- bei A4 genau eine erfolgreiche aktive Invalidierung je betroffener
  Jäger-/Zielidentität und anschließend Vanillas reguläre neue Zielsuche,
- keine unerklärten `MoveHere=0`-Ketten für bereits PCL-getrennte Tiere,
- keine ImprovedHunters-Exception und kein Freeze.

Gate: Erst wenn A1 bis A3 als Regression und A4 mit sowie ohne alternatives Ziel
erfüllt sind, wird die PCL-Diagnosedatei entfernt und der vollständige
Erreichbarkeitspfad als abgenommen betrachtet.

### Paket B: Schuss-, Kadaver- und Einsammelübergang isolieren

Erst nach Paket A. Der Test benötigt:

- genau einen Jäger,
- genau ein Reh,
- keine weitere auswählbare Beute,
- möglichst stationäre beziehungsweise kontrolliert begrenzte Beute,
- zunächst einen normalen freien Schuss, danach bei Bedarf denselben Fall nach
  einer Hindernisannäherung.

Falls vorhandene Logs Angriff, Projektil, Zielzustand, Kadaver und Einsammeln
nicht vollständig korrelieren, wird eine neue separat entfernbare Datei wie
`HunterShotPickupDiagnostic.cs` angelegt. Sie soll nur beobachten und pro
stabiler Jäger-/Beute-/Projektilidentität erfassen:

1. State-1-Angriffsresultat,
2. Projektilslot und Global-ID,
3. Ziel-Alive-State, Gesundheit, AI-/Kadaverzustand und Reservation vor/nach
   Treffer beziehungsweise Projectile-Delete,
4. Grund, warum `PendingHunterShotIntent` kompensiert oder bewusst nicht
   kompensiert,
5. Zielwechsel des Jägers,
6. Beginn und Abschluss der Kadaverabholung,
7. Fleischereignis beziehungsweise Abgabe.

Diagnosefehler dürfen Projektilkompensation, Vanilla-Schaden und Zielwahl nicht
beeinflussen. Erst nach der Logauswertung wird entschieden, ob überhaupt ein
Fix nötig ist.

Gate: Ein einzelnes getötetes Reh wird entweder korrekt eingesammelt und
abgeliefert oder der erste abweichende Vanilla-/Modzustand ist exakt belegt.

### Paket C: Kernlösung in Produktionsstruktur überführen

Nach erfolgreicher Verhaltensabnahme:

1. Verhaltenslogik aus `HunterTargetSearchFallbackDiagnostic.cs` in eine
   passend benannte Produktionsklasse verschieben; nur temporäre Marker in
   einer separaten Diagnosedatei behalten.
2. `HunterVanillaPathContinuationDiagnostic.cs` entsprechend in produktiven
   Hook/Guards und leicht entfernbares Detail-Logging trennen.
3. `HunterPclReachabilityDiagnostic.cs` entfernen.
4. Die breite alte `HunterVisibilityDiagnostic.cs` entfernen, sofern Paket B
   keinen konkreten Marker daraus benötigt.
5. Den stillgelegten `HunterLineOfSightRecovery.cs`-Adapter und seine Runtime-
   Anschlüsse entfernen; keinen alten Fallback parallel behalten.
6. Namen, Logs und Initialisierungszusammenfassung von „diagnostic“ auf den
   tatsächlichen Produktionsstatus korrigieren.
7. `UpdateToNewDLL.md`, Changelog und Versionsnummer aktualisieren.

Gate: Deaktivierter Mod, deaktiviertes `ImprovedPathfinding`, deaktivierter
Beutetyp, Kartenwechsel und echter Multiplayer führen zu keinerlei
verhaltensänderndem Hookpfad. Singleplayer-Skirmish und -Trail behalten die
abgenommene Funktion.

### Paket D: Beutetyp- und Mehrfachmatrix

Die gemeinsame Infrastruktur anschließend jeweils für Reh, Ziege, Hase,
Kamel, Huhn und Kuh prüfen:

- sichtblockiert, aber erreichbar,
- vollständig unerreichbar,
- Zugang wird später geöffnet,
- freier Kontrollfall,
- echtes Projektil, korrekter Kadaver, Einsammeln und Fleischabgabe,
- mehrere Jäger und mehrere Tiere ohne gemeinsame Reservierung oder
  gegenseitiges Zielüberschreiben.

Offener Codepunkt: `ImprovedHuntersViewModel` kennt `HuntCow`, aber
`ImprovedHuntersRuntime.IsRuntimeHuntingEnabled` schließt
`CHIMP_TYPE_COW` derzeit ausdrücklich aus. Dieser Widerspruch muss vor dem
Kuh-Abnahmetest bewusst entschieden und beseitigt werden, wenn Kuhjagd zum
unterstützten Ziel gehören soll.

### Paket E: Bewegungsgeschwindigkeit nach tatsächlicher Reststrecke

Erst bearbeiten, wenn Pakete A bis D stabil sind.

Hypothese: Vanilla wählt die Lauf-/Schleichstufe anhand der Luftlinien-
beziehungsweise Manhattan-Distanz in `EDI`. Bei kurzer Luftlinie, aber langem
Hindernisweg schleicht der Jäger deshalb unangemessen lange.

Analyseplan:

1. Kalibrieren, ob `+0xF8 - +0xF6` die verbleibenden Pfadschritte ausreichend
   repräsentiert.
2. Falls nicht, Vanillas gespeicherte Wegsegmente identifizieren und deren
   Restlänge ohne globale Scratchmutation bestimmen.
3. Die vorhandenen Vanilla-Distanzstufen und ihre Animationen/Geschwindigkeiten
   für Restweglängen beobachten.
4. Nur die passende vorhandene Vanilla-Stufe auswählen; keine direkten Speed-
   oder Animationsfelder schreiben.
5. In der letzten Annäherung vor einer freien Schusslinie Vanillas beabsichtigte
   Schleichbewegung erhalten.

Gate: Ein langer Umweg wird zügig zurückgelegt, ohne Ruckeln, fehlende
Animation, Pfadresets oder unnatürlich schnelles Verhalten im eigentlichen
Schussnahbereich.

## Multiplayer-Chore ab Script Extender 1.50.0

Multiplayer-Unterstützung bleibt verbindliches Endziel, wird aber erst gebaut,
wenn der kanonische Script Extender mindestens Version `1.50.0` erreicht hat.
Bis dahin bleiben PCL-Filter, Kandidaten-Handoff, Jägerhüttenpatch und
Pfadfortsetzung in echtem Multiplayer fail-closed, soweit sie
simulationsrelevant sind.

Der spätere Chore heißt **„Hunter-Recovery-Multiplayer-Synchronisation“** und
umfasst:

1. Mit `Shared/GameModeHelper.cs` echten Host, Client, Singleplayer-Skirmish,
   Trail und Multiplayer-Save sicher unterscheiden. `IsNetworkedEnvironment()`
   allein ist kein Multiplayerbeweis.
2. Anhand der APIs von mindestens `1.50.0` bestimmen, ob Zielentscheidung und
   Distanzstufenwahl von einer Autorität repliziert oder lockstep auf allen
   Peers deterministisch ausgeführt werden müssen.
3. Keine zweite Multiplayer-Zielwahl entwickeln. Die vorhandene Pipeline mit
   stabilen Slot-/Global-IDs synchronisierbar machen.
4. Bei eigener Nachricht einen expliziten `IMessagePackFormatter<T>` mit
   stabilen numerischen Keys verwenden; keine Contractless-Serialisierung.
5. Host-/Client-, Save/Load-, Reconnect-, Zielwechsel- und Desync-Tests mit
   mehreren Jägern durchführen.

Gate: Vor Abschluss dieses Chores bleibt echter Multiplayer deaktiviert.
Danach gehören Host, Client und Multiplayer-Saves zum unterstützten
Funktionsumfang und dürfen weder doppelte Moves noch abweichende Ziele erzeugen.

## Native Referenzdaten

Alle folgenden Werte beziehen sich auf die kanonische installierte DLL:

- Steam Build ID: `24651686`
- Dateigröße: `3.450.880` Byte
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

| RVA | Bedeutung |
| ---: | --- |
| `0x79C0` | Hunter-Manhattan-Distanz |
| `0xE2610` | Native playerabhängige PCL-Erreichbarkeitsprüfung hinter der Script-Extender-API |
| `0xA06F0` | Gemeinsamer Sichtwrapper |
| `0x9E350` | Sicht-/Geometriekern |
| `0x6B990` | Höhen-/Hindernishelper |
| `0x6B9F8` | Gebäudetyp-Switch |
| `0x6BAC4` | Dispatch-Tabelle; erster Eintrag ist die Jägerhütte |
| `0x2E7C60` | Normale Gebäude-Blockerhöhen; Typ `7` besitzt Wert `40` |
| `0x18AF00` | Vanilla-Hunter-Zielsuche |
| `0x18AF96` | Typprüfung und öffentlicher Query-Detouranker |
| `0x18B052` | Sichtwrapper-Aufruf in der Zielsuche |
| `0x18E950` | Allgemeine Unit-Orderroutine / direkter Hunter-Angriffspfad |
| `0x196230` | `c_game_unit_issueorder_movehere` |
| `0x12FC20` | `HunterUpdate` |
| `0x1300EA` | Vergleich der nativen Distanz mit `28` im State-1-Pfad |
| `0x13013D` | Direkter Angriffsaufruf |
| `0x130171` | Sichtfehlschlag zu State `6` und Hüttenrückkehr |

Die vollständigen Bytepattern, Callerprüfungen, Strukturfelder,
Auflösungsstrategie und Updateprozedur stehen in `UpdateToNewDLL.md`. Bei
abweichendem DLL-Hash dürfen diese RVAs nicht ungeprüft verwendet werden:

1. Auf Referenzhash direktes RVA plus lokale semantische Bytevalidierung.
2. Auf abweichendem Hash nur eindeutige Suche in geeigneten PE-Sektionen und
   vollständige semantische Validierung.
3. Bei fehlendem oder mehrdeutigem Treffer nur das betreffende Feature
   deaktivieren und Vanilla aktiv lassen.

## Gesamt-Abnahmekriterien

Die Singleplayer-Kernfunktion ist fertig, wenn:

- nahe unerreichbare Beute eine weiter entfernte erreichbare Beute nicht mehr
  verdrängt,
- ausschließlich unerreichbare Beute keinen Move, kein Pendeln und keinen
  Dauersuchzyklus erzeugt,
- nach Öffnen eines Zugangs dieselbe Beute zeitnah erneut zugelassen wird,
- ein während des Anmarschs neu unerreichbares aktives Ziel über Vanillas eigene
  Suche gewechselt oder kontrolliert verworfen wird, ohne den Jäger aufzulösen,
- ein erreichbarer Jäger Vanillas Weg auch innerhalb Distanz `28` bis zur freien
  Sicht weiterläuft,
- bei freier Sicht sofort Vanillas echter Angriff und ein echtes Projektil
  folgen,
- Jägerhütten wie normale Gebäude blockieren,
- Kadaver einsammelbar bleiben und Fleisch korrekt abgegeben wird,
- mehrere Jäger unabhängig funktionieren,
- Reh, Ziege, Hase, Kamel, Huhn und nach Aufhebung der Runtime-Sperre Kuh die
  gleiche Semantik besitzen,
- kein synchrones Managed-A*, kein eigener Recovery-Move und kein direkter
  AI-State-Nachbau zurückkehrt,
- Mod-/Option-Aus, Kartenwechsel und nicht unterstützter Multiplayer keinerlei
  Restzustände oder wirksame Hooks hinterlassen,
- Logs Millisekunden-Zeitstempel, stabile Identitäten, begrenzte Wiederholungen
  und überprüfbare Zählinvarianten besitzen,
- CRLF-, statische Code- und Native-Resolver-Prüfungen erfolgreich sind und der
  abschließende Build genau einmal über `ImprovedHunters\build.bat /nopause`
  ausgeführt wird.

Das vollständige plattformübergreifende Ziel ist erst nach dem Multiplayer-
Chore ab Script Extender `1.50.0` erreicht.

## Arbeitsregel für den nächsten Chat

Der nächste Chat soll **nicht** erneut mit einer allgemeinen Schusstilesuche
oder eigenen Bewegungs-State-Machine beginnen. Er soll in dieser Reihenfolge
arbeiten:

1. Version `1.1.45` mit Paket A4 testen, A1 bis A3 kurz regressieren und die Logs
   auswerten.
2. Nur bei bestandenem vollständigem Erreichbarkeits-Gate Paket B isoliert
   untersuchen.
3. Danach Produktions-/Diagnosecode trennen und Altpfade entfernen.
4. Erst anschließend Beutetypmatrix und Bewegungsgeschwindigkeit bearbeiten.
5. Multiplayer bis Script Extender `1.50.0` als geplanten, aber noch
   deaktivierten Chore behandeln.

Jede neue Hypothese muss gegen die bestätigte Grundarchitektur geprüft werden:
**PCL `0` verwirft früh; PCL positiv lässt Vanilla planen; ein angenommener
Vanilla-Pfad wird im blockierten Nahbereich fortgesetzt; bei freier Sicht
übernimmt Vanilla den Angriff.**
