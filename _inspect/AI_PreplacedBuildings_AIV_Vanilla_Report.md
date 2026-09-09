# Vanilla-Analyse: Verzögerter AIV-Aufbau durch vorplatzierte KI-Gebäude

Stand: 2026-09-09

## Kurzfazit

Im aktuellen Vanilla-Code gibt es einen klar belegten, ortsunabhängigen Kopplungsfehler zwischen der Zerstörung eines Gebäudes und dem AIV-Bauplan einer KI:

1. Wird ein Gebäude vollständig zerstört beziehungsweise „gecrusht“, setzt die Zerstörungsroutine für den **Besitzer des zerstörten Gebäudes** einen spielerweiten Verzögerungszähler.
2. Der Hauptscheduler des KI-Bauens prüft diesen Zähler ganz am Anfang.
3. Solange der Zähler den AIC-Wert `crushed_building_delay` noch nicht erreicht hat, kehrt der Scheduler sofort zurück.
4. Dadurch werden in diesem Durchlauf weder der AIV-Fortschritt erhöht noch irgendein AIV-Bauschritt versucht.

Die Sperre enthält **keine Prüfung der Entfernung, der AIV-Fläche, des Gebäudetyps oder der Herkunft des Gebäudes**. Ein vorplatziertes Gebäude weit außerhalb des 100×100-AIV-Bereichs kann daher den Beginn des Burgenbaus verzögern, sobald dieses Gebäude beim Spielstart oder kurz danach über den normalen Zerstörungspfad entfernt wird.

Eine ebenso wichtige Einschränkung: Das bloße, unveränderte Vorhandensein eines lebenden vorplatzierten Gebäudes setzt diesen konkreten Timer nicht. Wenn schon die reine Existenz ohne sichtbare Zerstörung reproduzierbar genügt, ist zusätzlich eine Laufzeitaufzeichnung nötig. Der statische Code belegt dann entweder eine beim Start intern erfolgende Entfernung/Zerstörung oder einen zweiten, indirekten Effekt; er belegt keinen allgemeinen „Gebäude existiert, also AIV warten“-Test.

## Analysebasis und Versionssicherheit

Analysiert wurde die kanonische installierte Datei:

`E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll`

Berechneter SHA-256:

`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

Dieser Hash stimmt exakt mit `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` und der semantischen Baseline `sem/FBCB9319` überein. Die verwendeten RVAs gehören damit zur aktuell installierten DLL und wurden nicht aus einer alten Vergleichsversion übernommen.

Als typisierte Gegenprobe wurde der kanonische Script-Extender-Stand 2.3.0, Commit `a0cd52993b44a6909d4f7f6a92f82fa5888a8e63`, benutzt. Sein `InternalAIC` benennt Offset `0x0074` als `build_rate` und Offset `0x0078` als `crushed_building_delay`.

## Relevanter Vanilla-Ablauf

### 1. Initialisierung und Auswahl des AIV

Der Spielstartpfad in `FUN_180094350` (RVA `0x94350`) richtet für jeden KI-Spieler den AIV-Zustand ein:

- `FUN_180050680` (`0x50680`): AIV-Spezifikation zuweisen/initialisieren
- `FUN_180054ec0` (`0x54EC0`): Ausgangsposition setzen
- `FUN_180054f60` (`0x54F60`) oder `FUN_180054de0` (`0x54DE0`): Kandidat beziehungsweise Rotation wählen und testen
- `FUN_180053d00` (`0x53D00`): den gewählten 100×100-Plan in ausführbare Bauplan-Einträge überführen
- optional `FUN_180055f50` (`0x55F50`): sofortiges Vorbauen bei aktivierter vollständiger Burg

`FUN_180057080` (`0x57080`) bewertet nur belegte Zellen des übersetzten 100×100-AIV-Kandidaten über den Platzierungsvalidator `FUN_18007b060` (`0x7B060`). Ein Gebäude außerhalb dieser übersetzten Fläche kann diese Kandidatenprüfung nicht direkt beeinflussen.

### 2. Normaler schrittweiser AIV-Scheduler

`FUN_180057330` (`0x57330`) ruft für einen aktiven KI-Spieler den Hauptscheduler `FUN_1800539b0` (`0x539B0`) auf.

Der Scheduler arbeitet vereinfacht so:

1. Prüfen, ob ein aktiver AIV-Slot existiert.
2. Den spielerweiten „crushed building“-Zähler prüfen.
3. Falls keine solche Sperre aktiv ist, den normalen `build_rate`-Takt abwarten.
4. Den freigegebenen Baufortschritt (`currentStepGoal`) erhöhen.
5. Die AIV-Schritte ab Schritt 1 bis zum aktuellen Ziel erneut prüfen.
6. `FUN_180051790` (`0x51790`) für jeden freigegebenen Schritt aufrufen und nach dem ersten erfolgreich ausgeführten Schritt abbrechen.

Der zweite Punkt liegt **vor** dem normalen AIV-Takt und vor jeder Ausführung eines Bauplanschritts. Deshalb betrifft die Sperre auch den allerersten Bauversuch.

## Der bestätigte Fehler im Detail

### Setzen der globalen Sperre

`FUN_18007eb00` (`0x7EB00`) ist ein zentraler Gebäude-Schadens-/Zerstörungspfad. Nachdem die Trefferpunkte eines vorhandenen Gebäudes auf null oder darunter gefallen sind, liest die Funktion den Besitzer aus dem Building-Record. Wenn der aufrufseitige Modusparameter `param_8 == 1` ist, führt sie sinngemäß Folgendes aus:

    ownerId = destroyedBuilding.ownerId;
    if (player[ownerId].crushedDelayCounter == 0)
        player[ownerId].crushedDelayCounter = 1;

An dieser Stelle gibt es keine Koordinatenprüfung. Es wird auch nicht geprüft, ob das Gebäude:

- innerhalb der gewählten AIV-Fläche liegt,
- aus dem AIV selbst stammt,
- vom Map-Autor vorplatziert wurde,
- wirtschaftlich oder militärisch ist,
- vor oder nach dem ersten regulären AIV-Schritt entstanden ist.

Der Zustand gehört allein zum Besitzer des zerstörten Gebäudes. Daher erklärt der Code unmittelbar, warum die Position eines betroffenen vorplatzierten Gebäudes keine Rolle spielt.

### Verbrauch der Sperre im AIV-Scheduler

`FUN_1800539b0` liest denselben Spielerzustand (`DAT_18379d8b0 + playerId * 0x583c`) direkt nach der Prüfung des aktiven AIV-Slots:

    if (crushedDelayCounter != 0) {
        crushedDelayCounter++;
        if (crushedDelayCounter < GetCrushedBuildingDelay(playerId))
            return;
        crushedDelayCounter = 0;
    }

`FUN_18002c9c0` (`0x2C9C0`) liefert aus dem aktiven AIC-Datensatz exakt das Feld bei Offset `0x78`. Der Script Extender 2.3.0 benennt dieses Feld als `InternalAIC.crushed_building_delay`. Damit ist die Bedeutung nicht nur aus dem Kontrollfluss geraten, sondern zusätzlich über das aktuelle typisierte AIC-Layout belegt.

Die Folgen des frühen `return` sind eindeutig:

- kein normaler `build_rate`-Fortschritt in diesem Durchlauf,
- kein Erhöhen von `currentStepGoal`,
- kein Aufruf von `FUN_180051790`,
- kein Versuch, auch nur den ersten AIV-Eintrag zu bauen,
- ebenfalls keine nachgelagerten AIV-Wartungsaufrufe am Ende dieses Schedulers.

Der Zähler ist nicht verlängernd: Die Zerstörungsroutine setzt ihn nur, wenn er zuvor null war. Weitere zerstörte Gebäude während derselben aktiven Sperre starten ihn nicht erneut. Nach Erreichen des AIC-Grenzwerts wird er gelöscht und derselbe Scheduler-Aufruf darf weiterlaufen.

### Warum dies bei vorplatzierten Gebäuden besonders auffällt

Ein normaler KI-Aufbau beginnt ohne eigene externe Gebäude. Dann kann vor dem ersten AIV-Bauschritt gewöhnlich auch kein weit entferntes eigenes Gebäude zerstört werden.

Eine Karte mit vorplatzierten KI-Gebäuden schafft dagegen schon zu Beginn zusätzliche, vollständig normale Building-Records dieses Besitzers. Sobald einer dieser Records beim Start, durch Überlappungsbereinigung, Geländezustand, Schaden, Skript-/Szenarioverhalten oder frühe Feinde über den beschriebenen Pfad zerstört wird, erhält die KI dieselbe Verzögerung, die Vanilla eigentlich als Reaktion auf ein im laufenden Spiel verlorenes Gebäude vorsieht. Der AIV-Scheduler kann die Herkunft nicht unterscheiden.

Der statisch bestätigte Fehler ist daher nicht „jedes vorhandene Gebäude erhöht einen AIV-Zähler“, sondern präziser:

> Vanilla wendet `crushed_building_delay` besitzerweit auf den kompletten AIV-Scheduler an und unterscheidet weder vorplatzierte Gebäude noch deren Lage oder den Zeitpunkt vor dem ersten AIV-Bau.

## Weitere gefundene Wechselwirkungen

### Vorplatzierte Gebäude werden nicht in den AIV-Plan übernommen

`FUN_180053d00` erzeugt die ausführbaren Einträge aus den 100×100-AIV-Zellen. Dabei findet keine globale Suche nach bereits vorhandenen Gebäuden desselben Besitzers statt. Ein entferntes vorplatziertes Gebäude gilt somit nicht als bereits erfüllter AIV-Schritt. Auch ein typgleiches Gebäude außerhalb der Sollposition ersetzt keinen geplanten Eintrag.

Das ist kein direkter Starttimer, erklärt aber, weshalb der Plan trotz vorhandener Infrastruktur unverändert von vorne abgearbeitet wird.

### Besitzerweite Gebäudestatistiken sind ebenfalls ortsblind

`FUN_1800b8270` (`0xB8270`) zählt aktive Gebäude nach Besitzer und Typ über den gesamten Building-Manager. Koordinaten werden nicht geprüft. Diese Zählung wird von den zusätzlichen Wirtschaftsentscheidungen des KI-Schedulers verwendet, unter anderem in den Pfaden `0x2DC80`, `0x2DD30`, `0x2DF10`, `0x2E0D0` und `0x2E1E0`.

Vorplatzierte Gebäude können daher typabhängig Grenzwerte wie die gewünschte Zahl von Farmen, Holzfällern, Minen oder Steinbrüchen beeinflussen. Das kann zusätzliche Wirtschaftsgebäude unterdrücken oder die Ressourcenverwendung verändern. Es ist jedoch **nicht** derselbe harte AIV-Startstopp: `FUN_1800539b0` läuft nach diesen Wirtschaftsprüfungen grundsätzlich weiter zur AIV-Logik.

### Tatsächliche Überlappung mit der AIV-Fläche ist ein separater Fall

Liegt ein vorplatziertes Gebäude innerhalb der übersetzten AIV-Fläche, kann es die Kandidatenbewertung beziehungsweise spätere Platzierungsversuche räumlich blockieren. Das ist ein eigener Mechanismus. Er erklärt nicht die Beobachtung mit einem weit außerhalb des 100×100-Bereichs liegenden Gebäude, weil die Kandidatenprüfung nur die vom AIV belegten Zellen testet.

## Klar benannte Probleme

### Problem 1 – Falsche Granularität von `crushed_building_delay` (bestätigt)

Eine einzelne Gebäudezerstörung sperrt den gesamten AIV-Bau des Besitzers. Es gibt keine Bindung an das zerstörte Gebäude, den betroffenen AIV-Schritt oder die Burgregion.

### Problem 2 – Keine Herkunfts- oder Startphasenunterscheidung (bestätigt)

Der Zerstörungspfad unterscheidet nicht zwischen einem regulär von der KI gebauten Gebäude und einem vom Map-Autor vorplatzierten Gebäude. Ebenso fehlt eine Ausnahme für die Phase vor dem ersten erfolgreichen AIV-Bauschritt.

### Problem 3 – Entfernung zur Burg wird ignoriert (bestätigt)

Der Timer wird ausschließlich über die Besitzer-ID adressiert. Ein Gebäude außerhalb des 100×100-AIV-Bereichs löst bei seiner Zerstörung denselben vollständigen Scheduler-Stopp aus wie ein Burggebäude.

### Problem 4 – Keine Bestandsabstimmung zwischen Welt und AIV (bestätigt)

Der vorbereitete AIV-Plan übernimmt bereits vorhandene eigene Gebäude nicht als erfüllte Schritte. Weltbestand und Planfortschritt bleiben getrennte Systeme.

### Problem 5 – Reine Existenz als alleinige Ursache noch nicht belegt (offen)

In der aktuellen Baseline hat der relevante Spielerzähler nur die Zerstörungsroutine als Setzpfad; die übrigen Zugriffe lesen, erhöhen oder löschen ihn. Deshalb wäre die Aussage „jedes lebende vorplatzierte Gebäude setzt direkt den Timer“ mit dem Vanilla-Code nicht vereinbar. Für einen Testfall ohne sichtbare Zerstörung muss zur Laufzeit ermittelt werden, ob das Gebäude intern beim Start kurz entfernt wird oder ob eine andere indirekte Wechselwirkung vorliegt.

## Empfohlener nächster Nachweis

Vor einer Korrektur sollte ein kleiner diagnostischer Hook exakt zwei Stellen protokollieren:

1. Das Setzen von `DAT_18379d8b0` in `FUN_18007eb00`: Spielzeit, Besitzer-ID, Building-Game-ID, Gebäudetyp, Position, vorherige Trefferpunkte und Aufrufer/Modus.
2. Den frühen Rücksprung in `FUN_1800539b0`: Spieler-ID, Zählerstand, aufgelöster `crushed_building_delay`, aktiver AIV-Slot und aktuelles `currentStepGoal`.

Ein A/B-Test sollte dieselbe Karte einmal ohne und einmal mit genau einem vorplatzierten KI-Gebäude außerhalb der AIV-Fläche starten. Damit lässt sich eindeutig feststellen, welches konkrete Start-Ereignis den bestätigten globalen Timer auslöst.

## Richtung für eine spätere Korrektur

Die engste Korrektur wäre, den globalen Crushed-Timer nicht auf vorplatzierte Gebäude während der Initialisierungsphase beziehungsweise vor dem ersten erfolgreichen AIV-Schritt anzuwenden. Dafür muss die Laufzeitdiagnose zuerst eine robuste Unterscheidung liefern; allein Entfernung oder Gebäudetyp wären zu grob.

Eine pauschale Entfernung von `crushed_building_delay` wäre riskanter, weil sie bewusstes Vanilla-Verhalten nach späteren echten Gebäudeverlusten ändert. Ebenso sollte ein Fix deterministisch auf allen Multiplayer-Teilnehmern laufen und darf nicht nur den sichtbaren Clientzustand korrigieren.

## Evidenz- und Vertrauensbewertung

- **Bestätigt:** aktueller DLL-Hash und Baseline-Zuordnung.
- **Bestätigt:** AIV-Initialisierung, 100×100-begrenzte Kandidatenprüfung und schrittweiser Schedulerfluss.
- **Bestätigt:** Setzen des besitzerweiten Zählers bei vollständiger Gebäudezerstörung im entsprechenden Modus.
- **Bestätigt:** früher vollständiger Scheduler-Rücksprung bis zum AIC-Feld `crushed_building_delay`.
- **Bestätigt:** fehlende Orts-, Herkunfts- und Startphasenprüfung an dieser Kopplung.
- **Bestätigt:** globale, ortsunabhängige Besitzer-/Typzählung für zusätzliche Wirtschaftsentscheidungen.
- **Offen:** welches konkrete Start-Ereignis in der beobachteten Karte das vorplatzierte Gebäude durch den Zerstörungspfad führt, falls keine Zerstörung sichtbar ist.

## Verwendete lokale Quellen

- `_inspect/CrusaderDE-Native-Baseline/CURRENT.md`
- `_inspect/CrusaderDE-Native-Baseline/CURRENT.json`
- `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/exports/semantic-decompiled-functions.c`
- `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/sources/type-fields.jsonl`
- `shcde-script-extender/src/SHCDESE.BepInEx/Interop/InternalAIC.cs`
- `CastlePlanner/UpdateToNewDLL.md`
- `ActiveAIVDetector/UpdateToNewDLL.md`
- `BugfixesAndQoL/_inspect/AITowerRuinRebuildNativeAnalysis.md`

