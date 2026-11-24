# Zinnen-Testmod: Ingame-Prüfung (Schachbrett-Fassung, Stand 22.11.2026)

APIShared wird nur über bestehende öffentliche APIs verwendet. Zwei dauerhafte lokale Hooks bleiben: Der Grafik-Hook ermöglicht niedrige gerade Endstücke, der Vorschau-Hook färbt unzulässige Bauzüge rot. Die Aktivierung ist eine Host-Einstellung und wird beim Beginn einer Partie oder Editor-Sitzung festgehalten. Seit 24.11.2026 gibt es unter dem Hauptschalter zwei getrennte Host-Schalter: „Zinnenhöhen abwechselnd anpassen“ (Standard an) und „Unbetretbare Zinnenformen blockieren“ (Standard aus).

## Regeln dieser Fassung

- **Höhenanpassung verwaltet neu gebaute Zinnen menschlicher Spieler und der KI** (KI über native 0x6CB20, SE-Event `OnAIBuildWall`), einschließlich vorgebauter KI-Burgen (Lobby-Option `advopt_pre_build`; ihre 0x6CB20-Aufrufe laufen vor dem Missionsstart und werden bei `Started` übernommen), im Editor die manuellen Bauten aller Slots. Karten-, Spielstand- und ältere Mauern bleiben genau wie geladen. Kartenersteller schalten die Höhenanpassung aus, um wie in Vanilla zu bauen.
- **Blockade** gilt nur für Bauzüge menschlicher Spieler (Editor: alle manuellen Bauten). Ist die Höhenanpassung aus, sagt die Blockade reine Vanilla-Höhen vorher (Endstücke und Einzelstücke hoch); dann stoppt sie z. B. eine Stück für Stück gebaute Reihe beim vierten Stück.
- **Höhen:** Gerade Stücke und Endstücke folgen Vanillas Schachbrett: Bei ungerader Summe x+y ist die Zinne hoch, sonst niedrig. Diagonale Reihen (auf dem Bildschirm waagerecht) bestehen aus Einzelstücken ohne geraden Nachbarn; ihre Stücke wechseln hoch/niedrig nach ungerader bzw. gerader x-Koordinate, die niedrigen zeichnet der Grafik-Hook als gerades niedriges Stück. Ecken, Kreuzungen und allein stehende Zinnen bleiben hoch wie in Vanilla.
- **Mit eingeschalteter Blockade wird der ganze Bauzug abgelehnt** (rote Vorschau und keine Ausführung), wenn er eines davon neu erzeugt:
  - ein gefülltes 2×2,
  - eine Kette aus drei zusammenhängenden Ecken/Kreuzungen,
  - eine zusammenhängende Folge von vier hohen Zinnen.
- **Grund** (nativer Audit vom 22.11.2026): Hohe Zinnen sind für alle Einheiten unbegehbar (0xD86F0). Leitern und Assassinen brauchen eine niedrige Zielkachel. Truppen aus Belagerungstürmen brauchen eine niedrige Kachel unter den fünf Kacheln vor der Turmfront.
- Gespeichert wird nur, welche Kacheln der Mod gesetzt hat, nicht ihre Höhe.

## Einstieg und Nachweis

1. **Vorbereitung:** Spiel nach der Installation neu starten und in den Modoptionen **Crenelated Wall Test** aktivieren. Editor und normale Partie sind unterstützt; Änderungen gelten ab der nächsten Sitzung.
2. **Startmarker:** Im aktuellen Spielstartabschnitt von `BepInEx/LogOutput.log` müssen erscheinen:
   - `CRENEL_READY`, `CRENEL_GRAPHICS_READY`, `CRENEL_PREVIEW_READY`, `CRENEL_SESSION` und `CRENEL_RUNTIME_AFTER_CLEANUP`,
   - nach dem ersten erfolgreichen Bau `CRENEL_HOOK_CONFIRMED`,
   - beim ersten niedrigen Endstück `CRENEL_GRAPHICS_CONFIRMED`.
3. **Bau- und Ablehnungsmarker:**
   - Erfolgreiche Bauten protokollieren `CRENEL_BUILD` mit `checked = changed + unchanged`.
   - In der Vorschau blockierte Züge erzeugen keinen Bauauftrag.
   - Ablehnungen in der Ausführung protokollieren `CRENEL_REJECT` und `CRENEL_REJECT_VERIFIED` mit `added=0` und unverändertem Steinbestand.
4. **Fehlermarker:** `CRENEL_CALLBACK_ERROR` oder ein fehlender Start- bzw. Tickmarker zusammen mit dem Logabschnitt melden.

## Prüffälle

| Versuch | Erwartung | Worauf achten |
|---|---|---|
| Gerade Reihe Stück für Stück bauen (der Exploit) | Exaktes Schachbrett, auch an den Endstücken. | Kein Stück bleibt hoch; alle vier Richtungen, alle Kamerarotationen. |
| Dieselbe Reihe ziehen | Gleiches Ergebnis wie Stück für Stück. | Auch in Gegenrichtung ziehen. |
| Einzelne Zinne, dann Nachbar | Erst hoch (Einzelstück), dann beide nach Schachbrett. | Das erste Stück muss niedrig werden, wenn seine Parität niedrig ist. |
| Leiter, Assassine, Belagerungsturm an Stück-für-Stück-Mauer | Andocken **und** Betreten der Mauer gelingt. | Einheiten erreichen den Mauergang. |
| Turm vor Ecke/T-Kreuzung | Truppen kommen über eine niedrige Nachbarkachel hinein. | Lange und kurze Arme testen. |
| Zweite parallele Reihe / 2×2 per Einzelklick | Rot, kein Bau, kein Steinverlust. | Auch an Karten- oder fremden Mauern. |
| Diagonale Reihe vor diagonaler Mauer (auf dem Bildschirm waagerecht), gezogen und Stück für Stück | Erlaubt; abwechselnd hoch/niedrig entlang der Diagonale, niedrige Stücke betretbar. | Optik der niedrigen Stücke prüfen (gerades niedriges Sprite). |
| Treppe mit 1er-Stufen | Rot beim dritten Eckstück. | Nachricht „three special“. |
| Treppe mit 2er-Stufen, kurzes U (3 breit) | Je nach Kartenposition erlaubt oder rot („four high“). | Um eine Kachel verschoben wiederholen. |
| Lange L-, T-, Kreuz- und Ringformen | Erlaubt, höchstens drei Hohe in Folge. | Beide Paritäten (um eine Kachel verschieben). |
| Abriss einer Mitte oder eines Eckarms | Überbleibende verwaltete Stücke werden wieder Schachbrett; neue Endstücke niedrig, falls Parität niedrig. | Kurze Verzögerung durch den begrenzten Tick-Scan ist zulässig; kein dauerhaftes Flackern. |
| Beschuss und Wiederaufbau | Beschädigte Grafik bleibt Vanilla; Wiederaufbau folgt dem Schachbrett. | Projektile und Nahkampf. |
| Karte mit vorgebauten Mauern / alter Save ohne Moddaten | Keine Änderung beim Laden. | Anbauen ändert nur die neuen Stücke. |
| Save mit Modbauten | Verwaltete Kacheln bleiben nach Laden verwaltet. | `CRENEL_SAVE` und `CRENEL_LOAD`. |
| Editor: Mod deaktiviert bauen, speichern, mit Mod laden | Vanilla-Mauern bleiben unverändert. | Auch KI-Slots. |
| KI baut ihre Burg | KI-Mauern bleiben Vanilla. | Keine KI-Bauverbote. |
| Knapper Steinbestand | Ein Zug wird abgelehnt, wenn die ganze Linie verboten wäre, auch wenn nur ein Teil bezahlbar ist. | Bewusst konservativ. |

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
- **Vorgebaute KI-Burg (Wolf, `advopt_pre_build=1`):** `CRENEL_AI_PREBUILT calls=117; adopted=117; changed=4`. Nach 400 Ticks Render-Pflege haben alle 39 verbliebenen KI-Zinnen die Zielhöhe (17 niedrig). Von den 117 beim Start gesetzten Zinnen trugen eine Sekunde später nur noch 39 das Zinnenbit; die übrigen 78 KI-Mauerkacheln wandelt Vanilla beim Burgaufbau um (Ursache nicht untersucht). Der Tick-Abgleich entfernt sie aus der Verwaltung.
- **Manuell gebaute KI-Burg (`advopt_pre_build=0`, normale Mauerkosten):** 27 Zinnen übernommen, nach der Render-Pflege alle richtig (13 niedrig).
- **Hinweis für Testaufbauten:** In einem früheren Lauf (Mauerkosten-Multiplikator 0) entstanden trotz vieler 0x6CB20-Aufrufe keine KI-Mauern; die Ursache ist nicht belegt. Der KI-Test setzt die Multiplikatoren vorsichtshalber auf 1 zurück.

- **Abriss und Zerstörung sofort (24.11.2026, Lauf `b41b0605`, Nachweis `.work/a/crenel-ingame/046`):** Abriss (`OnWallBulldoze`) und Zerstörung durch Beschuss (`OnBuildingTileTakeDamage`, nur wenn die Kachel danach keine Zinne mehr ist) richten die verwalteten Nachbarn direkt im Event aus und veröffentlichen neue Endstücke, bevor der Renderer sie hoch machen kann. Bloße Treffer bleiben beim Tick-Abgleich, der als Sicherheitsnetz erhalten bleibt. Editor-Regression mit Blockade grün (Abriss, Beschuss, Wiederaufbau, Kartenersteller).
