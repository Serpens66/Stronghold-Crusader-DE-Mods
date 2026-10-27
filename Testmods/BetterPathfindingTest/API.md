# Experimentelle Bereitschafts- und Anfrage-API

Stand: 27.10.2026, Mod 0.3.0, `NavigationApi.ContractVersion == 1`.
Namespace `BetterPathfindingTest.Api` in `BetterPathfindingTest.dll`. Kein APIShared-
Vertrag und keine Freigabe für Bewegungsübernahme. Ein Verbraucher muss die Testmod-
Assembly referenzieren und seine Abhängigkeit deklarieren; es wurden keine anderen
Mods angeschlossen.

## Bereitschaft und Kontext

`GetStatus()` verwendet den aufgenommenen lokalen Spieler. `GetStatus(playerId)`
und `PrepareContext(playerId)` verwenden Spieler 1 bis 8. Letzteres registriert die
Vorbereitung und bestätigt noch keine Bereitschaft. Gleiche Navigationsdaten können
sich einen internen Index teilen; ihre öffentlichen Spieleridentitäten bleiben getrennt.

| Zustand | Bedeutung |
|---|---|
| `NotLoaded` | Keine aktive Kartensitzung |
| `Initializing` | Aufnahme oder Vorbereitung ausstehend |
| `Ready` | Vollständig vorbereitet für den angegebenen erfassten Stand |
| `Updating` | Neuere Aufnahme wird abgeglichen; neue Aufträge warten auf Bereitschaft |
| `Unsupported` | Inaktiver normaler Build, nicht unterstützter Vertrag oder Budget |
| `Faulted` | Sitzung/Kontext fehlgeschlagen; Fehlergrund beachten |

Statusobjekte sind unveränderlich. Erneut abfragen; ein behaltenes Objekt aktualisiert
sich nicht. Kontext enthält Sitzung, Generation und Spieler. Aufnahmealter, Phase,
Fehlergrund, konservative Speicherbilanz und wartende Batches sind sichtbar.
`PerformanceAccepted` bleibt `false`. Es werden weder aktuelle Live-Kanten zwischen
500-ms-Aufnahmen noch Frametiming oder Sicherheitsfreigabe für Bewegung zugesichert.

## Auftrag und Ergebnis

    using BetterPathfindingTest.Api;

    var status = NavigationApi.GetStatus(1);
    if (!status.IsReady) return;
    var requests = new[] { new NavigationRequest(42, 100, 110, 230, 240) };
    var submitted = NavigationApi.SubmitBatch(status.Context, requests, out var ticket);
    if (submitted != NavigationSubmission.Queued) return;
    // Später erneut abholen, niemals auf einem Spielthread wartend spinnen:
    var result = NavigationApi.Poll(ticket);
    if (result.State == NavigationBatchState.Pending) return;
    // Bei Completed: IDs zuordnen, State prüfen, nur passende aktuelle Generation nutzen.
    // Nach Verbrauch aller gewünschten Schritte:
    NavigationApi.Release(ticket);

Dieses verkürzte Beispiel zeigt den Vertrag; der Aufrufer muss das Ticket zwischen
seinen späteren Aufrufen behalten und auf jedem terminalen Pfad freigeben.
Öffentliche Aufrufe führen keine native Suche aus. Eingabefelder werden validiert
und kopiert; danach darf der Aufrufer sein Array wiederverwenden. Batches umfassen
1 bis 10.000 Paare mit innerhalb des Batches eindeutigen `long`-IDs. Die Ergebnisfolge
entspricht der Eingabe, die IDs bleiben auch bei interner Bündelung erhalten.

Koordinaten sind x/y im aufgenommenen Rechteck. Außerhalb liegende Koordinaten,
doppelte IDs und Schrittgrenzen außerhalb 0 bis 2.000 ergeben `InvalidInput`.
Vorhandene Rechteckkoordinaten ohne gültigen Kartenknoten ergeben `InvalidEndpoint`
im einzelnen Routenergebnis. Der Standard erlaubt 2.000 Schritte. Kosten und
Schrittzahl zählen trockene gerichtete Übergänge mit Kosten 1 pro Schritt.

`SubmitBatch` unterscheidet `Queued`, `NotReady`, `Unsupported`, `InvalidInput`,
`NoCapacity` und `StaleContext`. Es gelten höchstens 32 wartende Batches und ein
begrenzter Pool auch für abgeschlossene, noch nicht freigegebene Ergebnisse.
`NoCapacity` erlaubt einen späteren Versuch nach Freigabe anderer Aufträge.

`Poll` liefert `Pending`, `Completed`, `Cancelled`, `Stale`, `Failed` oder `Unknown`.
`Completed` muss pro Route ausgewertet werden: `Found`, `Unreachable`, `Incomplete`
oder `InvalidEndpoint`. **Ein unvollständiges Ergebnis beweist keine Unerreichbarkeit.**
Warte- und Berechnungszeit stehen getrennt im Batchresultat. Das Auftragsbudget
beträgt 120 s; nicht fertige Aufträge werden ausdrücklich als fehlgeschlagen gemeldet.

## Präfix, weitere Schritte und Besitz

Gefundene Routen enthalten optimale Kosten, Schrittzahl, Handle und bis zu acht
vorbereitete Richtungen. Richtungswerte 0 bis 7 bedeuten N, NE, E, SE, S, SW, W, NW
in Kartenkoordinaten (x nach rechts, y nach unten). `SubmitSlices` nimmt bis zu 10.000
Abschnittswünsche eines gemeinsamen Spieler-/Sitzungs-/Generationskontexts an:

    var desired = new[] { new NavigationSliceRequest(route.Handle, 8, 128) };
    var accepted = NavigationApi.SubmitSlices(desired, out var stepsTicket);

Offset zählt ab null; Count ist 0 bis 2.000 und wird am Wegende gekürzt. `HasMore`
kennzeichnet verbleibende Schritte. Für den ganzen Weg Offset 0 und Count 2.000
anfordern. Auch diese Aufträge später pollen und ihr eigenes Ticket freigeben.
Beim aktuellen Backend entstehen vollständige Wege bereits bei der ersten Anfrage;
die Abschnittsausgabe spart vorerst nur öffentliche Übertragung und Kopien.

- `Release(ticket)` gibt Ergebnisreservierung und sämtliche **diesem Routenbatch**
  gehörenden Handles frei. Ein freigegebener Abschnittsauftrag gibt seine eigenen
  Kopien frei, nicht die ursprünglichen Routenhandles.
- `Cancel(ticket)` bricht logisch ab und verwirft Routen. Das kleine terminale
  Ergebnis bleibt bis `Release` abrufbar. Ein laufender Worker prüft Abbruch zwischen
  Arbeitspaketen. `Release` kann auch einen noch wartenden/laufenden Auftrag abbrechen.
- Ergebnisse sind unveränderliche, verwaltete Momentaufnahmen. Nach `Release` vom
  Aufrufer behaltene Objekte bleiben dessen Speicherbesitz; der Dienst hält sie nicht
  mehr. Freigegebene Tickets liefern `Unknown`, fremde/alte Sitzungen `Stale`.
- Geänderte Navigationsdaten ersetzen die Generation und invalidieren Ergebnisse
  und Handles des betroffenen Spielers. Unveränderte Aufnahmen behalten sie.
  Vor einer Verwendung erneut `IsReady` und Kontextgleichheit prüfen; bereits
  abgeholte Objekte können ihre eigene spätere Ungültigkeit nicht verändern.

Freundliche Gräben, Assassinen, ungleiche Kostenprofile, Ressourcenbedingungen und
terminale Ausnahmen besitzen noch keinen öffentlichen Anfragevertrag. Sie dürfen
nicht als durch eine Bodenanfrage mitabgedeckt behandelt werden. Die API ist im
normalen Build vorhanden, der Dienst jedoch ausgeschlossen: Status und Einreichung
melden `Unsupported`. Kein stiller Ersatz durch einen anderen Suchkern.

Getrennt kompilierte Verbraucherprüfungen: `tests/client/ClientProbe.cs`,
`tests/Test-ServiceApi.ps1` und `tests/Test-Artifact.ps1`. Funktions-/Messnachweise und
fehlende Runtime-Abdeckung stehen in [RESULTS.md](RESULTS.md).

Wenn der Editor keinen lokalen Simulationsspieler meldet, wird Perspektive 1
automatisch als Diagnosekontext vorbereitet; der Status nennt diese Spieler-ID.
Die rohe Metadatei bewahrt die tatsächlich gemeldete lokale ID.
