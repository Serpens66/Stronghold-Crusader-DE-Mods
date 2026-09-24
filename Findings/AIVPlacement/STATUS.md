# AIVPlacement: Stand der Lobby-Prognose

Stand: 25.09.2026. Native-Basis: installierte `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

## Bereits funktionsfähig

- CastlePlanner wertet Karte und AIVJSON offline aus und veröffentlicht vier Fit-Drehungen sowie eine Vanilla-Autoauswahl nur bei eindeutigem Ergebnis. Ein ungeklärter Eingangszustand bleibt `NotEvaluable`.
- Die BugfixesAndQoL-Liste übernimmt Fit-Status und Tooltip, ohne bei reinen Statuswechseln die Zeilen, X-Schaltflächen oder Scrollposition neu aufzubauen. Die veröffentlichte Zeichenfolge und der wirksame ExtraFeatures-KI-Zustand werden nun nur bei Änderungen geloggt. Das Log belegt die Übergabe, nicht die sichtbare Darstellung.
- Der Native-Startkontakt mit verknüpften Gebäuderecords kann einen früheren Keep entfernen. Der kurze Hinweis nennt diese Möglichkeit nur für einen im Vanilla-Selector tatsächlich möglichen Kandidaten und dessen Drehung. Ein erfolgreicher Bau beziehungsweise tatsächlicher Verlust wird daraus nicht behauptet.
- Wenn zwei KI-Spieler jeweils einen eindeutig ausgewählten Vanilla-Kandidaten samt Drehung haben, vergleicht CastlePlanner die projizierten Core-Bauzellen. Ein gemeinsamer Tile erhält ausdrücklich nur den Hinweis **geplante Überschneidung**; der tatsächliche sequenzielle Bau ist davon nicht abgeleitet.
- Die ursprüngliche Kartenhöhe von projiziertem Burggraben und Zugbrücke wird je Drehung ermittelt. Der Fit-Score bleibt unabhängig von ExtraFeatures.

## Bedingungen für den Höhenhinweis

Ein Hinweis zu fehlendem Burggraben oder Zugbrücke darf erst erscheinen, wenn der ExtraFeatures-KI-Höhenpatch nachweislich **deaktiviert** ist, Kandidat und endgültige Drehung eindeutig sind und die Höhe der betroffenen Zellen **zum nativen Bau-Frame** über 12 belegt ist. Die bisherigen Kartenhöhen-Zähler sind dafür kein Beleg: AIV-Bau-Frames können die Höhe bereits vorher verändern. Deshalb liefert der aktuelle Offline-Worker für `BuildTimeHeightProvenByRotation` noch keinen positiven Wert; ein sicherer Höhenhinweis erscheint bis zur Rekonstruktion oder passenden Laufzeitaufnahme nicht. Aktivierter oder unbekannter Patchzustand erzeugt ebenfalls keinen Hinweis.

## Offene Baukonflikte

| Fall | Aktueller Nachweis | Für eine sichere weitere Aussage nötig |
| --- | --- | --- |
| Früherer Keep kann verschwinden | Nativer Kontakt-Test einschließlich verbundener Records; Vanilla-Reproduktion | Vollständiger Folgezustand nach möglicher Löschung, damit spätere Fits wieder freigegeben werden können |
| Zwei geplante AIV-Gebäude belegen dieselben Zellen | Core-Footprints eindeutig gewählter Kandidaten werden paarweise verglichen; Hinweis heißt **geplante Überschneidung** | Mehrdeutige Auswahlen und unbewiesene Footprints konservativ behandeln; für tatsächliche Baukonflikte weitere Sequenzbelege |
| Gebäude wird tatsächlich überbaut oder gelöscht | Einzelne native Frame-Traces | Sequenzielle Konstruktor-, Räumungs- und Record-Wirkungen aller möglichen vorherigen Auswahlen rekonstruieren; erst dann tatsächliches Überbauen behaupten |
| Burggraben oder Zugbrücke fehlt wegen Höhe | Native Baugrenze und ExtraFeatures-KI-Patch bekannt; projizierte Ausgangshöhen vorhanden | Zellweise Höhe unmittelbar vor jedem betroffenen Bau-Frame unter allen möglichen früheren Frames/Zuständen belegen |
| Spätere KI bei „Completed Castles“ | Fit und Sofortbau-Traces archiviert | Vollständige fitrelevante Tile- und Record-Wirkung für jeden möglichen früheren Sofortbau |
| Abbruchzweige des Startkonstruktors | Native Zweige statisch erfasst | Gezielte Traces der noch unbelegten Abbruchausgänge, ohne aus einem Fehlergrund allein einen Abbruch abzuleiten |

Bis zur jeweiligen Klärung bleibt eine davon abhängige Fit-Prognose grau oder ein Bauhinweis aus. Die Fit-Farbe selbst beschreibt Vanillas Kandidaten-Fit, nicht den garantierten späteren Bau jedes Gebäudes.

## Belege und fortlaufende Quellen

- [CastlePlanner-Forschungsstand](../../CastlePlanner/AIVPlacement_SOFORTSPAWN_FORSCHUNGSSTAND.md)
- [Native-AIV-Lobby-Baseline](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md)
- [Archiv und Umzug](RELOCATION.md); Rohdaten, Oracle-Korpora und Prüfsummen liegen in den Unterordnern dieses Findings-Verzeichnisses.

Die nächste Sichtprüfung erfordert nur eine Lobbyöffnung mit bekanntem Startkontakt und einem erhöhten Burggraben. Der veröffentlichte Tooltip wird aus dem neuen Log mit der Darstellung verglichen. Ein Kartenstart ist erst für eine konkret offene Bauhöhen- oder Record-Wirkung nötig.

## Lokale Abnahme vom 25.09.2026

CastlePlanners `build.bat` bestand 92 von 92 Tests und installierte das Plugin bei beendetem Spiel. Die SHA-256-Hashes von `CastlePlanner.dll` (`A956F154…`) und `CastlePlanner.AIVPlacement.Core.dll` (`B8D7C43D…`) stimmen zwischen lokalem Build und Installation überein. Die bestehenden archivierten Crossing- und Reed-Sea-Startkontakte bleiben in den synthetischen Tests konservativ. Eine neue UI-Darstellung ist damit noch nicht sichtbar bestätigt; das BepInEx-Log wurde seit dem Build nicht durch einen Spielstart aktualisiert.
