# Zinnen-Testmod: Ingame-Prüfung (Schachbrett für alle Zinnen, Stand 25.11.2026)

APIShared wird nur über bestehende öffentliche APIs verwendet. Ein dauerhafter lokaler Hook bleibt: Der Grafik-Hook zeichnet niedrige Zinnen, die Vanilla als Sonderstück zeichnen würde, als gerades Stück. Die Aktivierung ist eine Host-Einstellung und wird beim Beginn einer Partie oder Editor-Sitzung festgehalten. Unter dem Hauptschalter gibt es den Host-Schalter „Zinnenhöhen abwechselnd anpassen“ (Standard an). Die Blockade unbetretbarer Formen und ihr Vorschau-Hook sind seit 25.11.2026 entfernt.

## Regeln dieser Fassung

- **Höhenanpassung verwaltet neu gebaute Zinnen menschlicher Spieler und der KI** (KI über native 0x6CB20, SE-Event `OnAIBuildWall`), einschließlich vorgebauter KI-Burgen (Lobby-Option `advopt_pre_build`; ihre 0x6CB20-Aufrufe laufen vor dem Missionsstart und werden bei `Started` übernommen), im Editor die manuellen Bauten aller Slots. Karten-, Spielstand- und ältere Mauern bleiben genau wie geladen. Kartenersteller schalten die Höhenanpassung aus, um wie in Vanilla zu bauen.
- **Höhen:** Jede verwaltete, unbeschädigte Zinne folgt Vanillas Schachbrett der Karte: Bei ungerader Summe x+y ist sie hoch, sonst niedrig. Das gilt für gerade Stücke, Endstücke, Ecken, Verzweigungen, Kreuzungen, Doppelreihen, Blöcke und einzelne Zinnen. Ausnahme: Zinnen ohne Achsnachbarn, aber mit diagonalen Nachbarn (diagonale Reihen, auf dem Bildschirm waagerecht) wechseln nach ungerader bzw. gerader x-Koordinate, weil die Kartenparität eine ganze Diagonale gleich hoch machen würde.
- **Zeichnung:** Vanilla zeichnet jede Form außer einem geraden Mittelstück (genau zwei gegenüberliegende Achsnachbarn) als Sonderstück und setzt sie dabei hoch (0x65EF0). Niedrige verwaltete Zinnen bekommen deshalb über den Grafik-Hook das gerade Bild: entlang der Achse mit mehr Zinnen-Nachbarn, bei Gleichstand entlang Richtung 0. Die Zuordnung gilt nur für die beim Festlegen gesehenen Achsnachbarn.
- **Grund** (nativer Audit vom 22.11.2026, Spielnachweis vom 25.11.2026): Hohe Zinnen sind für alle Einheiten unbegehbar (0xD86F0). Leitern und Assassinen brauchen eine niedrige Zielkachel. Von einer niedrigen Zinne zur diagonal benachbarten niedrigen Zinne gehen Einheiten auch, wenn beide Kacheln dazwischen hoch sind. Damit bleibt jede Form im Schachbrett überquerbar.
- Gespeichert wird nur, welche Kacheln der Mod gesetzt hat, nicht ihre Höhe.

## Einstieg und Nachweis

1. **Vorbereitung:** Spiel nach der Installation neu starten und in den Modoptionen **Crenelated Wall Test** aktivieren. Editor und normale Partie sind unterstützt; Änderungen gelten ab der nächsten Sitzung.
2. **Startmarker:** Im aktuellen Spielstartabschnitt von `BepInEx/LogOutput.log` müssen erscheinen:
   - `CRENEL_READY`, `CRENEL_GRAPHICS_READY`, `CRENEL_SESSION` und `CRENEL_RUNTIME_AFTER_CLEANUP`,
   - nach dem ersten erfolgreichen Bau `CRENEL_HOOK_CONFIRMED`,
   - bei der ersten gerade gezeichneten niedrigen Zinne `CRENEL_GRAPHICS_CONFIRMED`.
3. **Baumarker:** Nur im Diagnose-Build (`build.bat /devdiag`) protokollieren Bauten `CRENEL_BUILD` mit `checked = changed + unchanged`.
4. **Fehlermarker:** `CRENEL_CALLBACK_ERROR` oder ein fehlender Start- bzw. Tickmarker zusammen mit dem Logabschnitt melden.

## Prüffälle

| Versuch | Erwartung | Worauf achten |
|---|---|---|
| Gerade Reihe Stück für Stück bauen (der Exploit) | Exaktes Schachbrett, auch an den Endstücken. | Kein Stück bleibt hoch; alle vier Richtungen, alle Kamerarotationen. |
| Dieselbe Reihe ziehen | Gleiches Ergebnis wie Stück für Stück. | Auch in Gegenrichtung ziehen. |
| Einzelne Zinne, dann Nachbar | Beide nach Schachbrett, auch die einzelne. | Die einzelne niedrige Zinne als gerades Stück gezeichnet. |
| Ecke, T-Kreuzung, Kreuz, Rahmen | Alle Stücke nach Schachbrett, Ecken eingeschlossen. | Optik der niedrigen Ecken und Verzweigungen in allen Kamerarotationen. |
| Doppelreihe, 2×2, größerer Block, Treppe | Gebaut (keine Sperre), Schachbrett. | Assassine bzw. Leitertruppe geht diagonal von niedrig zu niedrig. |
| Leiter, Assassine, Belagerungsturm an Stück-für-Stück-Mauer | Andocken **und** Betreten der Mauer gelingt. | Einheiten erreichen den Mauergang. |
| Diagonale Reihe (auf dem Bildschirm waagerecht), gezogen und Stück für Stück | Abwechselnd hoch/niedrig nach x, niedrige Stücke betretbar. | Optik der niedrigen Stücke (gerades niedriges Sprite). |
| „X“ aus fünf Zinnen | Mitte und Diagonalstücke unterschiedlich hoch. | Vanilla zeichnet die X-Form je Kamerarotation unschön; Vergleich mit und ohne Mod. |
| Abriss einer Mitte oder eines Eckarms | Überbleibende verwaltete Stücke bleiben im Schachbrett; neue Endstücke niedrig, falls Parität niedrig. | Kurze Verzögerung bis zum nächsten Tick (Neuzeichnungsmeldung) ist zulässig; kein dauerhaftes Flackern. |
| Beschuss und Wiederaufbau | Beschädigte Grafik bleibt Vanilla; Wiederaufbau folgt dem Schachbrett. | Projektile und Nahkampf. |
| Karte mit vorgebauten Mauern / alter Save ohne Moddaten | Keine Änderung beim Laden. | Anbauen ändert nur die neuen Stücke. |
| Save mit Modbauten | Verwaltete Kacheln bleiben nach Laden verwaltet. | `CRENEL_SAVE` und `CRENEL_LOAD`. |
| Editor: Mod deaktiviert bauen, speichern, mit Mod laden | Vanilla-Mauern bleiben unverändert. | Auch KI-Slots. |

Die folgenden datierten Abschnitte bis zum 25.11.2026 beschreiben frühere Fassungen (Blockade, Ecken hoch, eckverankerte Läufe) und bleiben als Nachweis unverändert.

## Automatisierte Ingame-Abnahme (22.11.2026, Lauf `d673bb3e`)

Testmod [CrenelatedWallAcceptance](../CrenelatedWallAcceptance/Run.ps1): unsichtbarer Win32-Desktop, minimales Plugin-Layout (Script Extender, Fixes, APIShared 0.62.0, dieser Mod), `4spielerkarte.map` gegen die Ratte, Geschwindigkeit 250. Bauzüge laufen über `CreateWall` und damit über denselben `OnBuildWall`-Hook wie ein Spielerbau; die Vorschau (rote Anzeige) prüft dieser Lauf nicht. Nachweis: `.work/a/crenel-ingame/010` (Log, Player.log, Test-Save).

| Prüfung | Ergebnis |
|---|---|
| 42 Zinnen (Reihe und Spalte Stück für Stück, gezogene Reihe, L, Reste von 2×2/Diagonale/Treppe) nach Vanillas Render-Durchlauf | Alle Höhen wie Regel (`HEIGHTS_PASS`), auch auf einem zweiten Startplatz mit anderer Parität |
| 2×2, Diagonale aus drei, Treppe mit 1er-Stufen | Jeweils abgelehnt mit passendem Grund, kein Steinverlust |
| Assassine auf niedriges Endstück und niedrige Mitte einer Stück-für-Stück-Mauer | Steht danach auf der Zielkachel (`CLIMB_PASS`) |
| Assassine auf hohe Mitte | Vanilla lehnt den Bewegungsbefehl ab |
| Speichern und Laden | `CRENEL_SAVE`/`CRENEL_LOAD` mit 42 Kacheln; nach dem Laden alle Flags wie vor dem Speichern; Anbau danach regelkonform |
| Kontrolle: KI-Zinnen Stück für Stück (vom Mod nicht verwaltet) | Alle 7 hoch, davon 3 entgegen der Parität: Vanillas Exploit tritt ohne Mod auf |

Nicht abgedeckt: rote Vorschau mit echter Maus, Leitern, Belagerungstürme, Abriss und Beschuss, Editor, Multiplayer. Zinnen lassen sich nur neben einer Mauer- oder Zinnenkachel bauen (0x5C580); der Test setzt deshalb tragende Steinmauern.

## Leitern und Belagerungsturm (22.11.2026, Lauf `13e32903`)

Gleicher Testmod, zusätzlich eine eigene Angriffsreihe in zwei getrennten Abschnitten (Leiter und Turm, getrennte Stützmauern, damit kein Weg über den Mauergang den anderen Nachweis verfälscht). Alle Prüfungen des ersten Laufs bestanden erneut; 53 verwaltete Zinnen. Nachweis: `.work/a/crenel-ingame/021`.

| Prüfung | Ergebnis |
|---|---|
| Leiter an niedrige Zinne einer Spielermauer | Vorher lehnt Vanilla den Bewegungsbefehl eines Speerträgers auf die Zinne ab. Nach `AttachLadderToWall` steht die Leiter direkt vor der Zinne, und der Speerträger erreicht sie (`ENTER_PASS ladder`). |
| Belagerungsturm vor dem Turmabschnitt | Vorher ist keine niedrige Zinne des Abschnitts erreichbar. Vier Ingenieure bemannen den Turm, er dockt mit 9 Kacheln an. Unter den Wandkacheln vor ihm liegen nach Schachbrett niedrige Zinnen, und ein Speerträger erreicht über den Turm eine davon (`ENTER_PASS siege-tower`). |

Aufbau: Die Angreifer gehören einem freien Spielerplatz ohne KI, weil die KI neu erzeugte eigene Einheiten sofort in ihre Trupps übernimmt. Leitern brauchen ohnehin einen anderen Besitzer als die Mauer. Nach den Belagerungsprüfungen werden die Angreifer entfernt, weil Vanilla Bauen neben feindlichen Truppen verweigert.

Weiter nicht abgedeckt: rote Vorschau mit echter Maus, Abriss und Beschuss, Editor, Multiplayer, Leitern und Türme der echten KI-Belagerung (hier per Befehl gesteuert).

## Editor: Vorschau, Abriss, Beschuss, Slots, Kartenersteller (22.11.2026, Lauf `d33717ce`)

Szenario `crenel-editor` desselben Testmods (`Run.ps1 -Scenario crenel-editor`), `4spielerkarte.map` im Editor, Burgmodus (`MEMode` 1), keine Gegner. Mauerzüge laufen über Vanillas UI-Linienweg `EngineInterface.PlaceMapperItem` mit Maus gedrückt, gezogen und losgelassen. Der Bulldozer läuft über den normalen Editor-Eingabeweg mit emulierter Kartenmaus (Harmony auf `getMouseMapTilePosition`/`getMouseStateForEngine`, keine Windows-Eingaben). Den Vorschau-Status liest der Test an TileManager+`0x204E6E4`. Nachweis: `.work/a/crenel-ingame/035`.

| Prüfung | Ergebnis |
|---|---|
| Gezogene und Stück für Stück gebaute Reihe | Vorschau grün, gebaut, Höhen nach Regel |
| Schließende Kachel eines 2×2 | Vorschau rot (`CRENEL_PREVIEW_CONFIRMED`), nach dem Loslassen nichts gebaut |
| Zweite Reihe entlang einer bestehenden | 4 von 5 Kacheln rot, nichts gebaut |
| Bauten für Slot 2 | Vom Mod verwaltet (`CRENEL_BUILD owner=2`), Höhen nach Regel |
| Bulldozer auf die Mitte einer Reihe | Zinne entfernt; die neuen Endstücke haben die Regelhöhe |
| Beschuss durch Streitkolbenkämpfer von Slot 2 | Schaden steigt bis zur Zerstörung (3 Zinnen); übrige Höhen nach Regel; Wiederaufbau folgt der Regel |
| Kartenersteller: Mod aus, Stück für Stück bauen, Karte speichern, mit Mod laden | Vanilla-Zinnen (2 von 5 dauerhaft hoch) bleiben beim Laden unverändert; Anbau verwaltet nur die neue Kachel |

Hinweise aus dem Aufbau: Eine Zinne braucht eine tragende, nicht stark beschädigte Mauer daneben. Sumpf (`IsSwamp`) ist nicht bebaubar. Neben Mauern eines anderen Slots verweigert Vanilla das Bauen.

## Multiplayer

Host und Client verwenden denselben Testmod; nur der Host stellt die Aktivierung ein.
- Abwechselnd auf beiden PCs Linien, Anschlüsse und verbotene Formen bauen; auf beiden Seiten müssen dieselben Entscheidungen und Höhen entstehen.
- Einen Multiplayer-Save nach einem kompletten Neustart prüfen.

**Offen:** Ob ein Resync die Moddaten (verwaltete Kacheln) mitüberträgt, ist ungeklärt. Bei einem Resync die Höhen beider Peers vergleichen.

Die automatisierten Tests sind Modell- und Codecprüfungen. Grafik, Einheitengang, Andocken und Netzwerkgleichheit gelten erst nach diesen Ingame-Versuchen als bestätigt.

## Diagonale Reihen (22.11.2026, Läufe `f6204c35`, `13fbdad4`, `0a4991c0`)

Vanilla-Messung im Editor mit ausgeschaltetem Mod (`Run.ps1 -Scenario crenel-vanilla-topology`): Die native Nachbartabelle kennt als gerade Nachbarn nur die Kachelachsen. Diagonale Zinnen sind deshalb Einzelstücke, die Vanilla hoch setzt. Eine gezogene Diagonale war nur begehbar, weil Vanilla die Wege vor dem optischen Hochsetzen berechnet; Stück für Stück gebaut war sie nicht begehbar, und auf der anderen Parität ist sie schon beim Platzieren ganz hoch. Mit Mod: Diagonale Reihen werden nicht mehr abgelehnt, wechseln hoch/niedrig, und alle niedrigen Stücke waren für Assassinen betretbar (Editor `DIAGONAL_PASS` 4/4). Gefecht und Editor-Abnahme sind vollständig bestanden. Nicht geprüft: das Aussehen der niedrigen Diagonalstücke.

## Getrennte Schalter, Bau-Reihenfolge und KI-Burgen (24.11.2026, Läufe `4f4c49d6` und `e0091dd7`)

Testmod-Szenarien `crenel-heights` (Editor und KI) und `crenel-ai` (nur KI), Höhenanpassung an, Blockade aus. Nachweise in `.work/a/crenel-ingame/042` und `044`.

- **Beliebige Reihenfolge (Editor):** Gerade Reihe, 2×2-Block und L wurden Stück für Stück in zufälliger Reihenfolge gebaut, Lücken später geschlossen, außerdem eine Reihe in umgekehrten Hälften gezogen. Keine Ablehnung, der 2×2-Block steht. Alle 30 Zinnen haben die Zielhöhe (12 niedrig; 2×2 und Ecke hoch).
- **Diagonale Reihen** mit ausgeschalteter Blockade: 40 Zinnen richtig, alle 4 niedrigen Stücke für Assassinen betretbar.
- **Vorgebaute KI-Burg (Wolf, `advopt_pre_build=1`):** `CRENEL_AI_PREBUILT calls=117; adopted=117; changed=4`. Nach 400 Ticks Render-Pflege haben alle 39 verbliebenen KI-Zinnen die Zielhöhe (17 niedrig). Von den 117 beim Start gesetzten Zinnen trugen eine Sekunde später nur noch 39 das Zinnenbit; die übrigen 78 KI-Mauerkacheln wandelt Vanilla beim Burgaufbau um (Ursache nicht untersucht). Der Tick-Abgleich entfernt sie aus der Verwaltung. **Korrektur 25.11.2026:** Die „78 umgewandelten“ Kacheln waren ein Zählfehler des Tests (Schleife nur über x/y < Kartengröße; die Burg lag teils jenseits davon). Alle platzierten Mauern bleiben stehen (Lauf `199b6441`).
- **Manuell gebaute KI-Burg (`advopt_pre_build=0`, normale Mauerkosten):** 27 Zinnen übernommen, nach der Render-Pflege alle richtig (13 niedrig).
- **Hinweis für Testaufbauten:** In einem früheren Lauf (Mauerkosten-Multiplikator 0) entstanden trotz vieler 0x6CB20-Aufrufe keine KI-Mauern; die Ursache ist nicht belegt. Der KI-Test setzt die Multiplikatoren vorsichtshalber auf 1 zurück.

- **Abriss und Zerstörung sofort (24.11.2026, Lauf `b41b0605`, Nachweis `.work/a/crenel-ingame/046`):** Abriss (`OnWallBulldoze`) und Zerstörung durch Beschuss (`OnBuildingTileTakeDamage`, nur wenn die Kachel danach keine Zinne mehr ist) richten die verwalteten Nachbarn direkt im Event aus und veröffentlichen neue Endstücke, bevor der Renderer sie hoch machen kann. Bloße Treffer bleiben beim Tick-Abgleich, der als Sicherheitsnetz erhalten bleibt. Editor-Regression mit Blockade grün (Abriss, Beschuss, Wiederaufbau, Kartenersteller).

## Neuzeichnungsmeldung statt Tick-Abgleich (24.11.2026)

Der periodische Abgleich (bis zu 256 verwaltete Zinnen pro Tick) ist entfernt. Der Grafik-Hook meldet jede Neuzeichnung einer verwalteten Zinne; der nächste Tick prüft nur diese Kacheln. Grundlage ist der statische Audit aller Schreiber von Mauer-/Zinnenbits (Baseline `CRENELATED_WALLS.md`, Abschnitt vom 24.11.2026). Spielnachweise (25.11.2026, Nachweise in `.work/a/crenel-ingame`):

- `crenel-editor` (Lauf `95b81a0c`, 047): bestanden, auch Abriss, Zerstörung durch Streitkolbenträger und Wiederaufbau.
- `crenel-heights` Editor-Teil (Lauf `682010f4`, 048): beliebige Reihenfolge und Diagonalen bestanden. Vorgebaute KI-Burg (117 Zinnenaufrufe) bestanden: 39 Zinnen auf Zielhöhe.
- `crenel-acceptance` (Lauf `5a7c4d25`, 054): bestanden (Höhen, Assassinen, Leiter, Belagerungsturm, Speichern/Laden, KI-Kontrollreihe).
- `crenel-ai` mit fester Burgvorlage (Lauf `f32744d3`, 055): vorgebaute KI-Burg bestanden (117 Zinnenaufrufe, 59 Zinnen auf Zielhöhe, 27 niedrig); manuell gebaute KI-Burg mit 60 Minuten Friedenszeit bestanden (16 übernommene Zinnen auf Zielhöhe, 8 niedrig). Lauf `682010f4` davor hing, weil die KI ohne Friedenszeit den menschlichen Lord tötete. Die Friedenszeit setzt der Test über `MPsetupData.peacetime` (die Gefechtslobby zeigt die Option nicht) und prüft den nativen Friedensstatus.

Testaufbau-Erkenntnisse:

- Die KI-Kontrollreihe in `crenel-acceptance` war seit der Aufteilung der Schalter veraltet: Neue KI-Bauten werden verwaltet. Die Reihe muss jetzt durchgehend die Zielhöhe haben; das Vanilla-Hängenbleiben weist `crenel-vanilla-topology` nach.
- Eingebaute KI-Lords bekommen bei jedem Start zufällig eine ihrer Standard-Burgvorlagen samt Drehung (`AIVLoader.UploadDefaultAIV`, `preferredAIVs = -1 - rotation`). Die vorgebaute Burg war deshalb verschieden groß (117, 135 oder 186 Zinnenaufrufe). In zwei Läufen (`2fc44ab9`, `bf01f7c2`; 135 bzw. 186 Aufrufe) gab es 20 Ticks nach dem Start keine einzige Mauerkachel mehr auf der Karte, obwohl der Mod beim Start alle Zinnen übernommen hatte; der Mod schreibt nur das Hoch-Bit. Ursache nicht untersucht. Der KI-Test legt deshalb die Burgvorlage fest (`preferredAIVs = 100`: Vorlage 0, 0 Grad) und ordnet die Startburgen fest zu; Herleitung in der Baseline `AIV_LOBBY_SELECTION.md` (Abschnitt vom 25.11.2026). Die Vorbau-Prüfung läuft ohne Friedenszeit; mit Friedenszeit verschwanden die Mauern einer zufälligen Vorlage ebenfalls. **Korrektur 25.11.2026:** Die Mauern verschwanden nicht. Die Testzählung lief nur über x/y < Kartengröße (400), die Burg lag aber bei x 508–557; an allen 540 platzierten Positionen standen weiter KI-Mauern (354 Mauern, 186 Zinnen, Lauf `199b6441`). Die Zählung läuft jetzt über die vollen Achsen (`MAX_WIDTH`/`MAX_HEIGHT`).

## Lasttest und kostenproportionale Aktualisierung (25.11.2026, Lauf `bff6522d`, Nachweis `.work/a/crenel-ingame/058`)

Vorher baute jede Normalisierung die Endstück-Zuordnung und die beobachtete Menge für alle verwalteten Zinnen neu (Kosten proportional zur Gesamtzahl). Jetzt aktualisiert die Runtime nur die Einträge der normalisierten Zinnen in nebenläufigen Wörterbüchern, die der Grafik-Hook ohne Sperre liest; ein Tick ohne Meldung kostet einen Volatile-Lesezugriff, die Zustandsprüfung gemeldeter Zinnen liest die Schichten direkt ohne Allokation.

Szenario `crenel-stress` (Testmod `CrenelatedWallAcceptance`): neue 800x800-Editorkarte, 60 Reihen à 300 verwaltete Zinnen (18.000), Beschuss mit über die SE-API erzeugten Katapultsteinen (`CreateProjectile`, `CatapultRocks`, Positionen in Achtel-Kacheln; Einschlag über 0x9C730 → 0x7EB00 und Splash 0x7DF60). Die Methoden des Mods werden von außen per Harmony gemessen (der Mod enthält keinen Testcode); dieselbe Last läuft mit und ohne Höhenanpassung.

| | Höhenanpassung an | aus |
|---|---|---|
| Einschläge / Ticks | 940 / 3.308 | 907 / 4.175 |
| Mod-Zeit gesamt | 56 ms | 20 ms |
| Mod-Zeit pro Tick, höchstens | 1,36 ms | 0,14 ms |
| Tickabstand p50 / p99 / max | 3,9 / 7,5 / 13,8 ms | 4,0 / 7,4 / 16,5 ms |
| Grafik-Hook-Aufrufe (gesamt / max) | 93.792 / 0,31 ms | 90.946 / 0,09 ms |
| Bau eines 100er-Zinnenbefehls, höchstens | 26 ms | 0,6 ms |

Danach hatten alle 16.914 unbeschädigten Zinnen die Zielhöhe. Grenzen: Die Messung enthält den Harmony-Aufwand; zerstört wurden nur rund 35 Zinnen (die meisten Einschläge beschädigen nur), der Splash-Zerstörungspfad ohne Event lief also selten; Laden eines Spielstands mit vielen verwalteten Zinnen (Restore, einmalig beim Kartenstart) ist nicht gemessen. Katapulteinheiten wurden im Editor nicht bemannt (Lauf `5bbf59bd`), daher die erzeugten Steine.

## Reihen richten sich nach Ecken (25.11.2026)

Bisher folgten gerade Stücke der festen Kartenparität. Neben einer immer hohen Ecke standen dadurch je nach Lage zwei hohe Zinnen; eine 3er-Endreihe zwischen zwei Ecken war mal H-L-H, mal H-H-H (Screenshots des Nutzers). Jetzt gilt für jeden Lauf (maximale Kette gerader Stücke einer Achse, `WallPolicy.LineRun`):
- Neben einer Ecke oder Verzweigung niedrig, dann abwechselnd.
- Bei Sonderstücken an beiden Enden verankert das Ende mit der kleineren Koordinate; eine gerade Anzahl Stücke endet dann mit einem hohen Stück neben der anderen Ecke (Entscheidung des Nutzers).
- Läufe ohne Sonderstück behalten die Kartenparität.

`Normalize` gleicht dafür ganze Läufe durch die geänderten Kacheln und ihre Achsnachbarn ab; die Blockade-Vorhersage berücksichtigt die verschobenen Läufe ebenso. Regeltests: Rahmen beider Paritäten und Breiten 4 bis 9, nachträglich angebaute und wieder abgerissene Ecke, zufällige Baureihenfolge. Spieltest: Schritt `heights-frame` im Szenario `crenel-heights`.

## Schachbrett für alle Zinnen, Blockade entfernt (25.11.2026)

Nachweis vorab (Szenario `crenel-diagonal-walk` mit erzwungenem Schachbrett, Läufe vom 25.11.2026): Assassinen und Leitertruppen gehen diagonal von niedriger zu niedriger Zinne, auch wenn beide Kacheln dazwischen hoch sind. Sichtprüfung des Nutzers im Editor (`crenel-look`, `crenel-look-corners`, `crenel-look-manual`): Niedrige Band-, Block-, Eck-, T- und Kreuzstücke sehen als gerades Stück gezeichnet stimmig aus.

Damit ersetzt das Schachbrett die Blockade: Jede verwaltete Zinne folgt der Kartenparität (Diagonalstücke der x-Regel), niedrige Sonderstücke werden gerade gezeichnet. Entfernt sind die Blockade samt Einstellung, der Vorschau-Hook (0x7A3B0), die eckverankerten Läufe und die Lauf-Erweiterung in `Normalize`. Die Grafik-Zuordnung speichert die Achsnachbarn; der Hook wählt die Achse aus der nativen Nachbartabelle. `Normalize` veröffentlicht die Zuordnung, bevor es das Hoch-Bit schreibt.

Offline: Regel- und Grafiktests (Schachbrett für alle Formen und Baureihenfolgen, Stabilität über Render-Durchläufe, Zuordnungsschutz bei veränderter Nachbarschaft), 2.298.228 Assertions; Native-Audit und Hook-Fixtures bestanden.

Spielnachweise (Testmod `CrenelatedWallAcceptance`, Hintergrund-Desktop, Nachweise `.work/a/crenel-ingame/094` bis `105`):

| Szenario | Ergebnis |
|---|---|
| `crenel-editor` (Lauf `8914deb4`) | Bestanden: Doppelreihe und 2×2 grün gebaut, Slots, Diagonale, Abriss, Beschuss mit Zerstörung, Wiederaufbau, Kartenersteller. |
| `crenel-acceptance` (`aa9a8029`) | Bestanden: Höhen, Assassinen, Leiter, Belagerungsturm, Speichern/Laden, KI-Kontrollreihe. |
| `crenel-heights` (`f1c2e4ac`) | Bestanden; Rahmen `HLHLHLH`/`LHLHLHLH`, Ecken nach Schachbrett; vorgebaute KI-Burg 117 Zinnen. |
| `crenel-ai` (`878262b2`) | Bestanden; vorgebaute KI-Burg 213 Zinnen, manuell gebaute 50 Zinnen. |
| `crenel-diagonal-walk` (`17db8f60`), Höhenregel an | Block und Band stellen sich selbst aufs Schachbrett ein; Assassine ins Blockinnere und ins Band angekommen (diagonaler Weg), Befehl auf hohe Zinne abgelehnt, Leiter-Speerträger auf Kante und weiter ins Blockinnere angekommen. |
| `crenel-stress` (`7247d166`) | 18.000 Zinnen, 4.028 Ticks: Mod-Zeit pro Tick höchstens 1,37 ms (Eckregel vorher 3,37 ms), 100er-Bauzug höchstens 14 ms; alle 16.923 intakten Zinnen auf Zielhöhe. |
| `crenel-look-x` (`cc5c322d`) | X vor einer Stützmauer: Vanilla alle fünf hoch, mit Mod Mitte niedrig und gerade gezeichnet. Nur die Ausgangsrotation ist im Bild; die Karte dreht um die Kartenmitte. |

Testaufbau-Erkenntnisse: Der Beschussschritt im Editor zielte bisher auf die zweite Reihe, weil die Blockade die vordere Doppelreihe ablehnte; jetzt steht die Doppelreihe, das Ziel liegt vorn. Den Wiederaufbau direkt neben gerade getöteten Angreifern zeigt Vanilla rot; nach rund 600 Ticks Wartezeit ist er grün (Ursache vermutlich die Bauregel neben feindlichen Einheiten, nicht nativ geprüft). Die Mitte eines X lässt sich nicht direkt bauen (Zinne braucht einen Achsnachbarn, 0x5C580); der Test setzt eine Hilfsmauer und reißt sie danach ab.

## Bestehende Zinnen angleichen, Review-Korrekturen (25.11.2026)

Neue Host-Optionen unter der Überschrift „Bestehende Zinnen ebenfalls angleichen“ (Standard aus, pro Sitzung eingefroren): beim Laden einer Karte im Editor, beim Laden eines Spielstands, beim Start einer Karte mit vorplatzierten Mauern. Beim Start (`Started`, nach dem Wiederherstellen der Spielstanddaten) durchsucht der Mod einmal die Karte, nimmt alle noch nicht verwalteten Zinnen auf und gleicht sie über denselben Weg an wie neue Bauten. Logmarker `CRENEL_ADOPT_EXISTING kind=editor|save|map; adopted=…; changed=…`.

Korrekturen aus der unabhängigen Prüfung: keine Laufzeitsperre mehr über den Datei-Hash des RedBird-Backends; Fremdpatch-Erkennung über `Shared.NativePatchGuard` mit erneuter Prüfung bei jedem Missions- und Editorstart; ausstehende Neuzeichnungsmeldungen werden beim Sitzungswechsel verworfen; das Schadensereignis normalisiert nur noch, wenn eine Zinne zerstört wurde.

## Multiplayer-Diagnose (Diagnose-Build, 25.11.2026)

`build.bat /devdiag` baut `src/WallRuntime.DevDiag.cs` ein (im normalen Build entfernt). Zeilen mit Simulationstick, je Zeile ein Hash über die verwalteten Zinnen (ID, Mauer-/Zinnen-/Hoch-Bits, Besitzer):
- `CRENEL_MP_STATE tick=…` alle 100 Ticks,
- `CRENEL_MP_REDRAW` bei Korrekturen über die Neuzeichnungsmeldung (z. B. Katapult-Splash),
- `CRENEL_MP_EVENT phase=bulldoze|damage|save-data-load|resync-prepare|resync-complete`,
- `CRENEL_MP_SESSION phase=started|resync-begin` mit Start-Tick und den eingefrorenen Optionen.

Auswertung: Host- und Client-Log nach Tick vergleichen; alle Hashes zum selben Tick müssen gleich sein. Der erste abweichende Tick und die Ereigniszeilen davor zeigen den Pfad.
