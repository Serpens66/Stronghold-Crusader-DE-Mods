# Ingame Free Castle: Safety-Gate-Ergebnis

Stand: 2026-08-23

## Ergebnis

Der geplante Ingame-Spawn an einem bereits vorhandenen Keep wird nicht in
CastlePlanner 0.6.11 umgesetzt. Das vorgeschriebene Sicherheits-Gate ist vor
einem produktiven Lauf fehlgeschlagen. CastlePlanner 0.6.10 und dessen
funktionierender Kartenstart-Spawn bleiben unverändert.

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

## Konsequenz

Die Settings-, HUD-, Savegame-, Dateiübertragungs- und Chore-Änderungen werden
nicht aktiviert, weil sie ohne einen sicheren atomaren Ingame-Spawnpfad keine
nutzbare Funktion ergeben. Es wird weder ein Managed-Fallback noch ein
experimenteller Runtime-Hook ausgeliefert.

Eine spätere Neubewertung ist erst sinnvoll, wenn der Script Extender oder die
native Engine eine vollständige, nach Kartenstart sichere AIV-Transaktion mit
festem vorhandenem Keep-Anker und eindeutigem Gesamtergebnis bereitstellt.

