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

Ein Hinweis zu fehlendem Burggraben oder Zugbrücke darf erst erscheinen, wenn der ExtraFeatures-KI-Höhenpatch nachweislich **deaktiviert** ist, Kandidat und endgültige Drehung eindeutig sind und die Höhe der betroffenen Zellen **zum nativen Bau-Frame** über 12 belegt ist. Die bisherigen Kartenhöhen-Zähler sind dafür kein Beleg: AIV-Bau-Frames können die Höhe bereits vorher verändern. Deshalb liefert der aktuelle Offline-Worker für `BuildTimeHeightProvenByRotation` noch keinen positiven Wert; ein sicherer Höhenhinweis erscheint bis zur Rekonstruktion oder passenden Laufzeitaufnahme nicht. Aktivierter oder unbekannter Patchzustand erzeugt ebenfalls keinen Hinweis.

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

## Belege und fortlaufende Quellen

- [CastlePlanner-Forschungsstand](../../CastlePlanner/AIVPlacement_SOFORTSPAWN_FORSCHUNGSSTAND.md)
- [Native-AIV-Lobby-Baseline](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md)
- [Archiv und Umzug](RELOCATION.md); Rohdaten, Oracle-Korpora und Prüfsummen liegen in den Unterordnern dieses Findings-Verzeichnisses.

Die nächste Sichtprüfung erfordert nur eine Lobbyöffnung mit dem bekannten Crossing-Startkontakt und Wolf Default 7 / Sentinel Default 2. Der veröffentlichte Tooltip wird aus dem Log mit der Darstellung verglichen. Für die geometrische Praxisfarbe ist kein Kartenstart nötig; ein Kartenstart wird erst für eine konkret offene Bauhöhen- oder Record-Wirkung angefordert.

## Lokale Abnahme vom 25.09.2026

CastlePlanners `build.bat` bestand 92 von 92 Tests und installierte das Plugin bei beendetem Spiel. Die SHA-256-Hashes von `CastlePlanner.dll` (`A956F154…`) und `CastlePlanner.AIVPlacement.Core.dll` (`B8D7C43D…`) stimmen zwischen lokalem Build und Installation überein. Die bestehenden archivierten Crossing- und Reed-Sea-Startkontakte bleiben in den synthetischen Tests konservativ. Eine neue UI-Darstellung ist damit noch nicht sichtbar bestätigt; das BepInEx-Log wurde seit dem Build nicht durch einen Spielstart aktualisiert.

## Geometrie-Build vom 25.09.2026

Der neue CastlePlanner-Build bestand 93/93 Tests; lokale und installierte
Hashes stimmen überein: `CastlePlanner.dll` `D49892488C09CAB7CFBAC0930666896177240E65A6BEBA8451E00EEB90164207`,
`CastlePlanner.AIVPlacement.Core.dll` `0A7F2B2C1781DBE02A9817F9771A09C1774E9B6415489BBC2F1563842CD79FD0`.
Der gemeinsame Native-Fit-Kern `AIVPlacement.Core.dll` wurde nicht geändert;
sein installierter Hash ist `9F6E0165F602A187F95C933BF9C212A470B8E9BD803C6894B7D550ACFBADB12F`.
Die später erkannte installierte `Thasos.map` hat denselben Hash wie die
fehlende Kopie `v_Thasos.map`; beide Oracle-Korpora wurden mit abgeleiteten,
hashgeprüften Manifesten erneut ausgeführt: 12 exakte, 60 bewusst graue,
null abweichende Fälle. Details: [ThasosAliasOracle](ThasosAliasOracle/RESULTS.md).
Der geometrische Abzug ist durch einen eigenständigen Grenzfalltest geprüft.

## Praxis-Tooltip nach der Lobbyprobe

Die neue Lobbyprobe zeigte für Sentinel je Drehung 80–86 % Praxiswert,
aber eine graue Hauptfarbe. Ursache war die alte Praxis-Klassifikation,
die bei positivem Prozentwert zusätzlich Vanillas `Impossible` für einen
früh blockierten Bau-Frame berücksichtigte. Die Praxisfarbe folgt künftig
allein dem Prozentwert; `Impossible` bleibt separat beim Vanilla-Fit.
Der Tooltip beginnt mit der Praxiszahl, fasst identische Drehungen zusammen,
verzichtet auf Zell-Abzüge und setzt den Vanilla-Fit darunter. Bei späteren
KIs mit Sofortspawn heißt die Praxiszahl weiterhin **geometrische Schätzung**;
der Vanilla-Fit bleibt ohne bewiesenen Eingangszustand ungeklärt. Eine
räumliche Trennung allein ist kein vollständiger Native-Schreibnachweis.

## Abnahme der Praxisdarstellung vom 25.09.2026

Der korrigierte CastlePlanner-Build bestand 94/94 Tests und wurde bei beendetem
Spiel installiert. Die lokalen und installierten SHA-256-Hashes stimmen überein:
`CastlePlanner.dll` `6090835101864F03FF24CE7E534833230921F592AD09028F937793BBCE733DAF`,
`CastlePlanner.AIVPlacement.Core.dll` `3358057B3F2892294A2E88E5D474FC2E8E657F181FADA4A9E52F3568882698FA`
und `AIVPlacement.Core.dll` `7C09FBB96993D23A044C649899EE6BA0A69F60ECE16013919D95FE92EBBB8344`.
Der synthetische Test deckt Sentinels 80–86 %, einen entfernten 100-%-Fall,
Prozentspannen, unbekannte Geometrie und eine Sofortbau-Schätzung ab.
Die sichtbare Lobbyfarbe und Tooltip-Darstellung müssen nach der Installation
noch einmal ohne Kartenstart geprüft werden.

## Praxisfarbe und Tooltip nach der englischen Lobbyprobe

Die Probe mit 99–100 % zeigte grau, weil die frühere Regel unterschiedliche
Farbklassen möglicher Drehungen sperrte. Die Praxis-Hauptfarbe ist nun
vorsichtig: nur sichere 100 % sind grün, 1–99 % einschließlich 99–100 % gelb,
0 % rot; unbekannte Geometrie und eine Mischung aus 0 % und positiven Werten
bleiben grau. Native-Zwischenergebnisse bekommen bis zur fertigen
Praxisbewertung keinen farbigen Punkt.

Der Tooltip beginnt mit genau einer lokalisierten Praxis-Drehungszeile.
Darunter folgen der getrennte Vanilla-Gesamtwert, belegte Bauhinweise und
zuletzt die ausdrücklich benannte automatische AIV- und Drehungsauswahl.
Ein vollständiger Vanilla-Fit erscheint als 100 %; native Abbrüche behalten
ihre eigene Aussage. Die bei englischer Spielsprache zuvor deutschen
Praxis-Sätze kommen nun aus den en-US/de-DE-Sprachdateien. Ein erkannter
Sprachwechsel rendert vorhandene Ergebnisse ohne erneuten Fit.

CastlePlanners `build.bat` bestand 94/94 Tests und installierte bei beendetem
Spiel. Lokaler Build und Installation stimmen überein: `CastlePlanner.dll`
`6BFE71B3934CC0544F5CFBF6B07AB875DF4B75822A6DF5C831DD1765D52D2A88`,
`CastlePlanner.AIVPlacement.Core.dll`
`ACE146AE5F043BA520D22BA5A6223149E7DBC57AF7042D025DBC6F5CB3EAC14D`.
Die Thasos-Oracle-Korpora blieben bei 12 exakten, 60 bewusst grauen und null
abweichenden Fällen. Die sichtbare Darstellung und der Rotationswechsel sind
noch im Spiel zu prüfen; dafür genügt eine Lobbyöffnung ohne Kartenstart.
