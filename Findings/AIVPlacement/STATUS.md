# AIVPlacement: Stand der Lobby-Prognose

Stand: 25.09.2026. Native-Basis: installierte `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

## Bereits funktionsfähig

- CastlePlanner wertet Karte und AIVJSON offline aus und veröffentlicht vier Fit-Drehungen sowie eine Vanilla-Autoauswahl nur bei eindeutigem Ergebnis. Ein ungeklärter Eingangszustand bleibt `NotEvaluable`.
- Die BugfixesAndQoL-Liste übernimmt Fit-Status und Tooltip, ohne bei reinen Statuswechseln die Zeilen, X-Schaltflächen oder Scrollposition neu aufzubauen. Die veröffentlichte Zeichenfolge und der wirksame ExtraFeatures-KI-Zustand werden nun nur bei Änderungen geloggt. Das Log belegt die Übergabe, nicht die sichtbare Darstellung.
- Der Native-Startkontakt mit verknüpften Gebäuderecords kann einen früheren Keep entfernen. Der kurze Hinweis nennt diese Möglichkeit nur für einen im Vanilla-Selector tatsächlich möglichen Kandidaten und dessen Drehung. Ein erfolgreicher Bau beziehungsweise tatsächlicher Verlust wird daraus nicht behauptet.
- Die ältere, nur bei zwei eindeutig ausgewählten Lords mögliche Core-Überschneidungsanzeige wird durch die gesonderte Praxisbewertung ersetzt.
- Die ursprüngliche Kartenhöhe von projiziertem Burggraben und Zugbrücke wird je Drehung ermittelt. Der Fit-Score bleibt unabhängig von ExtraFeatures.

## Geometrische Praxisbewertung (in Prüfung)

CastlePlanner berechnet zusätzlich zum unveränderten Vanilla-Fit die geplanten
Core-Footprints aller aktiven KI-Kandidaten für vier Drehungen. Feste Gebäude
einschließlich Keep, Turm, Tor und Zugbrücke zählen für einen symmetrischen
Abzug. Eine von mehreren Lords geteilte Zelle zählt für jeden Lord nur einmal;
eine schon im Ausgangs-Fit blockierte Zelle wird nicht erneut abgezogen.
Mauern und Burggräben erhalten bei geplanter Flächenüberschneidung nur einen
Hinweis. Unbekannte Mapper oder fehlende AIV-Daten gelten nicht als freie
Fläche, sondern lassen die Praxisfarbe grau.

Bei nicht eindeutigem Vanilla-Selector nutzt die Anzeige je Drehung eine
untere und obere Abzugsgrenze aus Schnitt- und Vereinigungsmengen der
möglichen Gegenpläne. Nur eine über alle berücksichtigten Möglichkeiten
gleiche Einstufung erhält eine gemeinsame Hauptfarbe. Bei späteren KI-Spielern
mit „Completed Castles“ bleibt der exakte Vanilla-Fit grau; ein zusätzlich
angezeigter Wert ist ausdrücklich **geometrische Schätzung auf der
normalisierten Ausgangskarte**. Er ist keine Vorhersage der tatsächlich
gebauten Gebäude und fließt nicht in Vanillas Autoauswahl ein. Native-Basis:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

## Bedingungen für den Höhenhinweis

Der Lobby-Tooltip prüft die projizierten Burggraben- und Zugbrückenzellen **jeder angezeigten AIV-Drehung** gegen die Höhe der gewählten Karte. Bei Höhe über 12 und nachweislich deaktivierter ExtraFeatures-KI-Höhenfreigabe nennt er die betroffenen Drehungen. Das gilt auch bei mehrdeutiger Autoauswahl und für spätere KIs mit „Completed Castles“; die Meldung behauptet keine eindeutige Vanilla-Auswahl und verändert weder Praxisfarbe noch Vanilla-Fit. Bei aktivem oder unbekanntem Patchzustand erscheint sie nicht. Die tatsächlichen Tile-Schichten jedes späteren Bau-Frames werden damit weiterhin nicht vollständig rekonstruiert; der Hinweis ist eine kartenbezogene Bauwarnung.

## Offene Baukonflikte

| Fall | Aktueller Nachweis | Für eine sichere weitere Aussage nötig |
| --- | --- | --- |
| Früherer Keep kann verschwinden | Nativer Kontakt-Test einschließlich verbundener Records; Vanilla-Reproduktion | Vollständiger Folgezustand nach möglicher Löschung, damit spätere Fits wieder freigegeben werden können |
| Zwei geplante AIV-Gebäude belegen dieselben Zellen | Alle verfügbaren Kandidaten und Drehungen werden als geometrische Footprints verglichen; der feste Gebäudeabzug bleibt getrennt vom Native-Fit | Für tatsächliche Baukonflikte weiterhin Sequenzbelege; unbekannte Footprints bleiben grau |
| Gebäude wird tatsächlich überbaut oder gelöscht | Einzelne native Frame-Traces | Sequenzielle Konstruktor-, Räumungs- und Record-Wirkungen aller möglichen vorherigen Auswahlen rekonstruieren; erst dann tatsächliches Überbauen behaupten |
| Burggraben oder Zugbrücke fehlt wegen Höhe | Native Baugrenze und ExtraFeatures-KI-Patch bekannt; projizierte Ausgangshöhen vorhanden | Zellweise Höhe unmittelbar vor jedem betroffenen Bau-Frame unter allen möglichen früheren Frames/Zuständen belegen |
| Spätere KI bei „Completed Castles“ | Fit und Sofortbau-Traces archiviert | Vollständige fitrelevante Tile- und Record-Wirkung für jeden möglichen früheren Sofortbau |
| Abbruchzweige des Startkonstruktors | Native Zweige statisch erfasst | Gezielte Traces der noch unbelegten Abbruchausgänge, ohne aus einem Fehlergrund allein einen Abbruch abzuleiten |

Bis zur jeweiligen Klärung bleibt der exakte Vanilla-Fit grau oder ein sicherer Bauhinweis aus. Die neue Hauptfarbe beschreibt die gekennzeichnete geometrische Praxisbewertung, nicht den garantierten späteren Bau jedes Gebäudes.

## Quellen und Prüfstand

- [Versuche und Rohdaten](EXPERIMENTS.md) enthalten die zusammengefassten Messreihen, den SHA-256 des verlustfreien Archivs und die Wiederherstellungsanleitung.
- [Historische Native-Analyse](HISTORY.md) ordnet den alten AICastlePlanner-Bericht und den früheren CastlePlanner-Forschungsstand ein.
- Die [aktuelle Native-Baseline](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md) bleibt für Adressen und Vanilla-Verträge maßgeblich.

Die letzte lokale CastlePlanner-Abnahme bestand 94/94 Tests. Das Plugin und seine englischen und deutschen Sprachdateien wurden installiert; die Hashes des lokalen Pakets und der Installation stimmten überein. Die sichtbare Höhenmeldung benötigt noch eine Lobbyprobe mit deaktivierter ExtraFeatures-KI-Höhenfreigabe. Ein Kartenstart ist dafür nicht erforderlich. Alte Build-Hashes und Einzelberichte sind im Evidenzarchiv erhalten.