# Pausierte Burgauswahl: Safety-Gate-Status

Stand: 2026-08-23

## Ergebnis

Der unsichere Ingame-Spawn an einem bereits vorhandenen Keep bleibt verworfen.
Die neue Kandidatenimplementierung verwendet stattdessen eine pausierte erste
Kartenladung als Vorschau und importiert die bestätigten AIV-Daten ausschließlich
beim zweiten, frischen Kartenstart vor der Keep-Erzeugung.

Die statischen Prüfungen, Protokolltests und der Build sind bestanden. Das
praktische Gate in Singleplayer und Multiplayer steht noch aus. Bis diese vier
Punkte im Spiel bestätigt sind, bleiben Plugin- und Manifestversion bewusst auf
`0.6.10`; README und Changelog beschreiben weiterhin nur die freigegebene Version.

Untersucht wurde ausschließlich die aktuell installierte kanonische DLL:

- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`
- `ExecuteToPercentage`: Referenz-RVA `0x55F50`
- `TestSpecificCandidate`: Referenz-RVA `0x54DE0`
- gemeinsamer Placement-Validator: RVA `0x7A2D0`

## Blocker

1. Nach dem Kartenstart blockiert der bereits erzeugte Keep-Komplex den
   nativen AIV-Fit. Das ist durch die früheren Post-Start-Versuche und die
   dokumentierte Validator-Kette reproduziert. Ein eng begrenztes temporäres
   Ausblenden der Keep-Belegungszellen könnte nur diesen Fit-Blocker umgehen,
   löst aber die folgenden Probleme nicht.

2. Die native Rotationswahl darf den vorbereiteten Keep-Anker verschieben. Der
   bestehende sichere Kartenstart-Pfad verschiebt den noch nicht erzeugten
   Vanilla-Keep auf diesen Anker. Ein bereits existierender Keep kann ingame
   nicht auf dieselbe Weise verschoben werden, ohne seinen gekoppelten
   Startkomplex und abhängige Spielzustände zu beschädigen.

3. Die native Ausführung ist nicht transaktional. `ExecuteToPercentage` baut
   Frames nacheinander und liefert keinen verlässlichen Gesamterfolg zurück.
   Die dokumentierte Build-Step-Kette kann einzelne Footprint- oder
   mapperabhängige Prüfungen verwerfen, während spätere Konstruktoren trotzdem
   weiterlaufen. Nach einer Teilmutation existiert kein vollständiger Rollback
   für Gebäude, Mauern, Tore, Gräben, Fallen, Tile-Raster, Visuals und
   Interaktionsregistrierungen.

Damit kann die verbindliche Anforderung „Fehlschläge hinterlassen keine
Teilburg und verbrauchen keinen Versuch“ nicht fail-closed garantiert werden.
Ein praktischer Probe-Spawn würde genau den nicht rückrollbaren Zustand
erzeugen können, den das Gate verhindern soll, und liefert deshalb keinen
vertretbaren nächsten Nachweis.

## Noch praktisch zu bestätigen

- `OnStartMap(Pre)` pausiert vor dem ersten Simulationstick, während Keep,
  Kamera, Blueprint-HUD und Steam-Lobby bedienbar bleiben.
- „Keine Burg“ setzt genau dieselbe Partie fort und verlässt die Lobby genau
  einmal.
- Eine bestätigte Burg startet die Karte genau einmal frisch und verwendet
  ausschließlich die feste Rotation `0/2/4/6` ohne alternative Best-Fit-Suche.
- Host und Client erreichen denselben zweiten Kartenstart mit identischen,
  zuvor validierten kanonischen AIV-Rohdaten.

Bei einem Fehler vor dem Commit wird die Vorschaupartie ohne Burgen freigegeben;
bei einem Fehler nach dem Commit wird fail-closed ins Frontend gewechselt. Erst
nach erfolgreicher Laufzeitabnahme wird die Kandidatenimplementierung als
`0.6.11` dokumentiert und freigegeben.

