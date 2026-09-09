# Vorplatzierte Gebäude und Vanilla-KI – aktueller Wissensstand

Stand: 9. September 2026  
Untersuchte native Version: `CrusaderDE.dll` mit SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`  
Script Extender: 2.3.0  
Laufzeitbeleg: `BepInEx\logs\Log_047.log`, zwei Kartenstarts mit ausschließlich UU-ImGUI, Script Extender und `PreplacedTest`.

## Gesicherte Erkenntnisse

### 1. Turmruinen lösen die AIV-Startsperre aus

Auf der Karte „Brother's Strife“ wechselte `crushed_building_delay` ausschließlich für KI-Spieler 8 von `0` auf `1`. Der Wechsel war bereits bei `allocate-spec.pre` sichtbar, also vor dem ersten Scheduler-Aufruf und vor dem ersten AIV-Bauversuch. Spieler 7 blieb bei `0` und begann sofort zu bauen.

Zum Aktivierungszeitpunkt waren die Kartenobjekte bereits eingelesen. Darunter befanden sich fünf zerstörte Turmrecords (`STRUCT_TOWER1_DESTROYED` bis `STRUCT_TOWER5_DESTROYED`) mit Besitzer 4 sowie zahlreiche besitzerlose `STRUCT_RUINS`. Diese Records lagen nicht im später festgelegten AIV-Bereich der betroffenen KI. Zwischen Kartenstart und Timeraktivierung wurde kein passendes Schadens-, Bulldoze- oder Delete-Ereignis beobachtet. Vanilla setzt den Timer daher während der frühen Karten-/KI-Initialisierung und nicht als Folge eines normalen, vom Script Extender sichtbaren Kampfereignisses.

Der native KI-Scheduler behandelt den voraktivierten Wert anschließend wie einen echten Gebäudeverlust: Der Zähler steigt bei den Schedulerdurchläufen von `1` bis zum konfigurierten Grenzwert `50`; währenddessen wird die AIV-Ausführung für diesen Spieler übersprungen. Danach fällt die Sperre weg und die KI baut normal, einschließlich externer Wirtschaftsgebäude. Die Ruinen verzögern den Start, blockieren ihn in diesem Lauf aber nicht dauerhaft.

Die Zuordnung „grüne Turmruinen → Spieler 8“ wird durch den kontrollierten Unterschied der beiden KIs und das ausschließlich bei Spieler 8 aktivierte Delay gestützt. Die Gebäuderecords tragen in der sehr frühen Ladephase allerdings noch Kartenbesitzer 4 beziehungsweise Besitzer 0. Die genaue Vanilla-Funktion, die daraus den Timer von Spieler 8 setzt, ist noch nicht identifiziert; ein Schadenscallback war es in diesem Lauf nicht.

### 2. Eine dichte eigene Mauer blockiert die externe Wirtschaft vor der Platzierungsprüfung

Auf der zweiten Testkarte waren beide KI-Startbereiche von eigenen Mauern umgeben. Spieler 7 besaß vier eigene, intakte Torhäuser; Spieler 8 besaß kein Tor. Beide KIs konnten zunächst weder Farmen noch Minen, Steinbrüche oder Holzfäller außerhalb der Mauer platzieren.

Die Fehlschläge erfolgen vor `FUN_1800C3BF0`, vor dem Platzierungsvalidator und vor dem gemeinsamen Bauaufruf `FUN_18006D580`. Für die gescheiterten Wirtschaftszweige wurden keine Wirtschaftskontext-Aufrufe von `FUN_1800E2610`, keine Platzierungserreichbarkeitsablehnung und kein Bauaufruf beobachtet. Eine Korrektur ausschließlich an `0xC3BF0` wäre deshalb unvollständig.

Der konkrete Abbruch liegt in den vorgelagerten 160×160-Wirtschaftsraster-Suchen. Bei der ersten Holzfällersuche von Spieler 7 (`FUN_180058020`) wurden nur fünf, um die Burg initialisierte Grobrasterzellen besucht. Acht unmittelbar orthogonal angrenzende Zellen blieben unbesucht. Dasselbe Muster trat bei Spieler 8 auf. Alle besuchten und alle acht angrenzenden Zellen lagen jeweils im selben PCL wie die eigene Burg; PCL-Unerreichbarkeit war daher nicht die Ursache.

Alle acht nicht aufgenommenen Nachbarzellen hatten im relevanten Rohfeld `byte+04` den Wert `25`. Der Vanilla-Code von `FUN_180058020` nimmt eine Nachbarzelle nur in die Suche auf, wenn `byte+04 < 16` und `byte+13 == 0` gilt. `byte+13` war hier `0`, aber `25 < 16` ist falsch. Die fünf Startzellen werden vor der normalen Nachbarprüfung initialisiert und beweisen deshalb nicht, dass Wert 25 passierbar wäre. Damit ist unmittelbar belegt, warum die Suche nach fünf Zellen endet und keinen Kandidaten liefert.

Die Farm-Suche `FUN_1800575B0` hat eine verwandte, aber eigene Expansionsgrenze `byte+04 < 17`. Die Rohfeldbedeutung von `byte+04` ist noch nicht fachlich benannt; gesichert sind bislang nur Offset, Laufzeitwert und die nativen Vergleichsbedingungen.

### 3. Funktionierende Torhäuser für Einheiten reparieren das Wirtschaftsraster nicht

Die vier Torhäuser von Spieler 7 waren konsistente `STRUCT_GATE_INNER`-Gebäuderecords mit gültigen Gatehouse- und Portalrecords. Das Portalmodell enthielt gültige PCL-Verbindungen, und nach Nutzerbeobachtung konnte die KI ihre Einheiten durch die eigenen Tore zu Angriffen schicken.

Trotzdem endete ihre Wirtschaftsraster-Suche genauso nach fünf Zellen wie bei der vollständig geschlossenen KI. Damit verwenden Einheitenbewegung und externe KI-Wirtschaftsplatzierung unterschiedliche Zugänglichkeitsmodelle. Die Rasterexpansion scheitert bereits an der Zellklassifikation und erreicht keine Portalentscheidung. Ein Fix, der nur freundliche Tore in `0xE2610` oder `0xC3BF0` freigibt, kann diesen Fall nicht beheben.

### 4. Ein späterer Mauerdurchbruch aktualisiert die Wirtschaftssuche nicht zuverlässig

Auch nach Kampf- und Maueränderungen wiederholten die Holzfällersuchen das Muster „fünf besuchte Startzellen, acht unbesuchte Nachbarn, kein Kandidat“. In einem nach dem beobachteten Durchbruch ausgeführten Versuch stieg die Suchgeneration erneut, das Ergebnis blieb `(-1,-1)` und der Cooldown wurde wieder auf `5` gesetzt. Die maßgebliche Zellklassifikation rund um die Burg blieb somit für die Rasterexpansion gesperrt.

Das Log enthält keinen passenden Building-Delete-Record für die bloßen Mauersegmente von Spieler 8. Mauersegmente werden offenbar zumindest teilweise in Tile-/Routingdaten statt als normale Building-Slots geführt. Ein vollständiger Fix darf seine Invalidierung daher nicht nur an Building-Delete- oder Bulldoze-Ereignisse knüpfen.

### 5. Die Zerstörung eines vorplatzierten Torhauses aktiviert denselben crushed-Timer

Später wurde das vorplatzierte Torhaus Building-ID 21, Global-ID 6614, Spieler 7, durch Spieler 8 zerstört. Der letzte native Schadensaufruf reduzierte die Gesundheit von `4` um `6`, setzte den Record auf `MarkedForDeletion` und änderte `crushed_building_delay` unmittelbar von `0` auf `1`. Dieser Pfad ist eindeutig einem tödlichen Schadensaufruf zugeordnet.

Damit gibt es mindestens zwei Timerquellen:

- eine frühe, noch nicht auf eine einzelne Funktion zurückgeführte Aktivierung beim Laden der Turmruinen;
- die normale Aktivierung durch die spätere tödliche Beschädigung eines eigenen Gebäudes.

Für einen Vorplatzierungsfix muss ausdrücklich entschieden werden, ob auch der spätere Verlust eines vorplatzierten, tatsächlich genutzten Torhauses vom Delay ausgenommen werden soll. Das ist keine rein technische Gleichsetzung mit dem fehlerhaften Kartenstart.

## Nicht als Ursache bestätigt

- Der Vanilla-Platzierungsvalidator ist nicht die Ursache der Mauerfälle; er wird für die gescheiterten externen Suchen nicht erreicht.
- `FUN_1800C3BF0` ist nicht die erste Sperre der Mauerfälle; sie bleibt für spätere Kandidaten relevant, wird hier aber zuvor umgangen.
- Fehlende oder feindliche Portalrecords erklären den Torhausfall nicht. Die eigenen Portalrecords sind vorhanden, und Einheiten nutzen sie erfolgreich.
- Der AIV-100×100-Bereich erklärt weder den Ruinen-Startdelay noch das Fehlen externer Wirtschaftsbauten. Die problematischen Ruinen und Wirtschaftsziele liegen außerhalb dieses Bereichs.
- Der native Holzsuch-Cooldownwert `-1` bedeutet nicht „Diagnose nicht verfügbar“. Der Code behandelt Werte `< 1` als suchbereit; `-1` ist ein echter Vanilla-Bereitschafts-/Startwert.
- Große PCL-Deltas bedeuten nicht automatisch eine große Topologieänderung. Vanilla nummeriert PCLs nach Bauänderungen häufig großflächig neu; viele bisher protokollierte Änderungen waren reine ID-Neunummerierungen bei unveränderten Rasterbytes.

## Konsequenzen für einen vollständigen Fix

Ein sicherer Fix muss derzeit mindestens drei getrennte Verträge behandeln:

1. Die frühe Initialisierung darf Kartenruinen oder andere Baseline-Objekte nicht als frischen KI-Gebäudeverlust in `crushed_building_delay` übernehmen. Die Korrektur muss vor dem ersten Schedulerdurchlauf erfolgen und anhand stabiler Baseline-Identitäten beziehungsweise der verursachenden Initialisierungsfunktion begrenzt sein.
2. Die 160×160-Wirtschaftsraster-Metadaten müssen nach dem Einlesen vorplatzierter Mauern und Tore sowie nach Tile-basierten Mauerdurchbrüchen korrekt aufgebaut oder invalidiert werden. Freundliche, funktionsfähige Tore müssen die für Wirtschaftssuchen maßgebliche Zellverbindung herstellen. Eine pauschale Ignorierung von Mauern wäre falsch, weil eine wirklich dichte Mauer ohne Tor weiterhin blockieren soll.
3. Nach erfolgreicher Rastersuche müssen die nachgeschalteten PCL-/Platzierungsprüfungen weiterhin echte Isolation, feindliche Tore und ungültige Bauflächen ablehnen. Nur bei nachweisbar vollständiger freundlicher Portalroute dürfte ein fehlerhaft negatives Ergebnis korrigiert werden.

Eine zusätzliche Herausrechnung vorplatzierter Gebäude aus KI-Sollzählungen bleibt als möglicher eigener Fehlerpfad relevant, ist durch `Log_047.log` aber nicht als Ursache der hier beobachteten Startsperre oder Mauerblockade belegt. Vor einem Gameplay-Patch an `FUN_1800B8270` ist daher ein isolierter Zähltest nötig.

Der vorhandene „replace buildings“-Code in `BugfixesAndQoL` muss bei der späteren Umsetzung einbezogen werden. Er behandelt nach bisherigem Stand vor allem Ersatzbauten nahe der Startposition; die hier belegte vorgelagerte Sperre externer Wirtschafts-Rastersuchen liegt außerhalb dieses Schwerpunkts und wird dadurch nicht automatisch behoben.

## Nächste offene Belege

- Die exakte frühe Vanilla-Schreibstelle ermitteln, die beim Laden der Turmruinen `crushed_building_delay` für Spieler 8 auf `1` setzt.
- Die Erzeuger und Aktualisierer von Wirtschaftsraster `byte+04` identifizieren und klären, welcher Wert an eigenen Toren beziehungsweise nach einem Mauerdurchbruch erwartet wäre.
- Für Farm-, Ressourcen- und Nahbereichssuche die jeweiligen Expansions- und Kandidatenprädikate getrennt verifizieren; sie sind ähnlich, aber nicht identisch.
- Einen kontrollierten Lauf mit offener Karte, dichter Mauer ohne Tor und identischer Mauer mit genau einem eigenen Tor vergleichen, einschließlich der Rohwerte beider Torseiten.
- Die globale KI-Gebäudezählung mit isolierten, außerhalb des AIV-Bereichs vorplatzierten Produktionsgebäuden gesondert testen.
