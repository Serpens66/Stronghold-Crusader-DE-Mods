# Zinnen-Testmod: Ingame-Prüfung (Schachbrett-Fassung, Stand 22.11.2026)

APIShared wird nur über bestehende öffentliche APIs verwendet. Zwei dauerhafte lokale Hooks bleiben: Der Grafik-Hook ermöglicht niedrige gerade Endstücke, der Vorschau-Hook färbt unzulässige Bauzüge rot. Die Aktivierung ist eine Host-Einstellung und wird beim Beginn einer Partie oder Editor-Sitzung festgehalten.

## Regeln dieser Fassung

- **Verwaltet werden nur neu gebaute Zinnen menschlicher Spieler**, im Editor die manuellen Bauten aller Slots. Karten-, KI- und ältere Mauern bleiben genau wie geladen. Kartenersteller deaktivieren den Mod, um wie in Vanilla zu bauen.
- **Höhen:** Gerade Stücke und Endstücke folgen Vanillas Schachbrett: Bei ungerader Summe x+y ist die Zinne hoch, sonst niedrig. Ecken, Kreuzungen und Einzelstücke bleiben hoch wie in Vanilla.
- **Abgelehnt wird der ganze Bauzug** (rote Vorschau und keine Ausführung), wenn er eines davon neu erzeugt:
  - ein gefülltes 2×2,
  - eine Diagonalkette aus drei Einzelzinnen,
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
| Diagonale aus drei Zinnen | Rot. | Zwei diagonale Zinnen bleiben erlaubt. |
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

## Multiplayer

Host und Client verwenden denselben Testmod; nur der Host stellt die Aktivierung ein.
- Abwechselnd auf beiden PCs Linien, Anschlüsse und verbotene Formen bauen; auf beiden Seiten müssen dieselben Entscheidungen und Höhen entstehen.
- Einen Multiplayer-Save nach einem kompletten Neustart prüfen.

**Offen:** Ob ein Resync die Moddaten (verwaltete Kacheln) mitüberträgt, ist ungeklärt. Bei einem Resync die Höhen beider Peers vergleichen.

Die automatisierten Tests sind Modell- und Codecprüfungen. Grafik, Einheitengang, Andocken und Netzwerkgleichheit gelten erst nach diesen Ingame-Versuchen als bestätigt.
