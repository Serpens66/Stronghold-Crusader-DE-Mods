Codeprüfung und Buildabschluss, 2026-10-08

- Öffnungsbutton und Menü verwenden dieselben sechs Icon-Vorlagen. Auswahl- und Configänderungen melden alle sechs Sichtbarkeitsproperties. Die Ressourcenoperationen treffen den tatsächlichen Vanilla-XAML-Knoten; jedes Content enthält genau ein Wurzelelement.
- Gesten starten mit automatischen Reihen. Mausbewegung ändert ausschließlich die achtteilige Richtung; die letzte gültige Richtung bleibt innerhalb der Bewegungsschwelle erhalten. Mausradschritte einschließlich Teilbeträgen werden einmal pro Frame gesammelt, ändern ausschließlich Reihen und bleiben zwischen 1 und Einheitenzahl. Kreis und Vanilla ignorieren Größenänderungen.
- Ganzzahliges lokales Raster vor Drehung: genaue Reihenanzahl, vollständige und eindeutige Plätze, gerade parallele Reihen. Block/Line/Column verteilen gleichmäßig; Wedge besitzt bei mehreren Reihen eine einzelne Spitze und nach hinten nicht abnehmende Breiten.
- Platzsuche läuft bis zu den erreichbaren Sollplätzen einschließlich angrenzender Ersatzplätze. Exakte Ziele werden zuerst für alle Einheiten reserviert; Hindernis-Ersatzplätze können keine späteren exakten Ziele wegnehmen. Gleichstände verwenden Tile-ID. Vorschau und Befehlsausführung nutzen dieselbe Planung.
- Protokoll 6: ushort Rows mit explizitem Formatter, Validierung gegen Einheitenzahl und abgeleitete Width. Rows fließt in Preview-Key und Plan-Hash ein.
- Gegen den Git-Stand und die gesicherten Ausgangsdateien geprüft. Vanilla-Platzsuche, Terrain-/Verbindungsprüfungen, Release-Verbrauch, Kamerahook, Fehlerpfad und native Vertragsprüfung sind abgesehen vom aktualisierten Reihen-Log unverändert. Keine neuen öffentlichen APIShared-Schnittstellen oder Hooks. Vorhandene Rally-, Bridge-, APIShared- und Lokalisierungsänderungen erhalten.
- Native-Baseline FBCB9319, installierter Extender 2.14.0 und Fixes 1.26.0 weiter gültig; bestehende native Audit-Abdeckung verwendet. Installierte Interop-Typen/Offsets/Größen/Strides und echte Spielassembly geprüft. JSON-, Lifecycle-, permanente Hook-, XAML-, CRLF- und Diff-Prüfungen bestanden.

Ausgeführte Tests und Installation:

- Formationssuite: 560.042 Prüfungen, einschließlich 31 Einheiten mit 1/2/3/21/31 Reihen, sämtlicher acht Richtungen und vier Density-Werte; zusätzliche Geometriefälle bis 4.000 Einheiten, Rollen, Release, Wheel, große Vorschau, Hindernis-/Rand-Ersatzplätze und MessagePack-Roundtrip.
- Shift-Queue: 8.999 Prüfungen; BugfixesAndQoL native Tests: 1.326 Prüfungen / 67 Signaturen. Weitere vom Build-Treiber ausgeführte Policy-, Host/Client-, Pathfinding-, Moat-, Rally- und native Backendtests bestanden.
- Beide ausschließlich über erhöhte build.bat-Treiber gebaut und installiert. Null Buildfehler; der Hauptmod meldet weiterhin bestehende Compilerwarnungen (unter anderem doppelt eingebundene Shared-Typen).
- Installierte DLLs/Metadaten stimmen mit lokalen Paketen überein; installiertes HUD-XAML stimmt mit Quelle überein. Versionen unverändert: APIShared 0.4.12, BugfixesAndQoL 1.0.180. README nicht bearbeitet.

Noch im Spiel abzunehmen: tatsächliche Icon-Darstellung, beide Maussteuerungen, Kamera/Objektbefehle/Abwahl, Hindernisse und Kartenränder, Kartenwechsel/Savegame sowie Multiplayer mit identischen aktualisierten APIShared- und Hauptmod-Paketen auf allen Teilnehmern. Die statischen und ausführbaren Tests ersetzen diese Spielabnahme nicht.

Nachweise: final-preflight.log, final-test-source-check.log, comparison-check.log, build-api-final.log, build-bugfixes-final.log; Ausgangsdateien unter before.
