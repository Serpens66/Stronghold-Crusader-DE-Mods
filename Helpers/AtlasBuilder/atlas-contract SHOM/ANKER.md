# Der Anker: warum Texturen verrutschen

Zwei Regeln, und eine Entscheidung darüber, worauf sich der Anker bezieht. Alle
drei Teile sind gemessen, nicht vermutet.

## Regel 1: Der Pivot ist normalisiert

`Sprite.Create` bekommt den Pivot als Bruchteil der Sprite-Größe. Der absolute
Anker in Pixeln ist also:

```text
Anker = Pivot * eigene Größe
```

Wer Crusaders Pivot unverändert auf ein anders großes Bild schreibt, verschiebt
es um

```text
Pivot * (Quellgröße - Zielgröße)
```

Pixel. Bei uns waren das im Median 50,5 px bei `Tree_Oak`, 33 px bei
`anim_stocks`, 26 px bei `tree_pine`. Bei Bodenkacheln reichen wenige Pixel,
damit die Kachel ihr Feld nicht mehr ganz ausfüllt. Dann sieht man den
schwarzen Untergrund zwischen zwei Kacheln. Das sind Smokelots schwarze Linien:
keine Shader-Ränder, sondern die Naht.

Die Korrektur hält den absoluten Anker fest und rechnet den Pivot auf die neue
Größe um.

## Regel 2: Ein Pivot von exakt 1,0 ist eine Eckkonvention

Regel 1 hält den Anker als **Abstand zur unteren beziehungsweise linken Kante**
fest. Das stimmt bei Fußpunkten von Einheiten und bei Kachelstreifen, deren
Pivot 16,5 px über der Unterkante liegt.

Es ist falsch, sobald der Zielslot seinen Anker an der **oberen oder rechten**
Kante führt, also einen normalisierten Pivot von exakt 1,0 hat. Dann ist nicht
der Abstand nach unten die feste Größe, sondern der nach oben, und der ist null.
Rechnet man dort nach Regel 1, verschiebt sich das Bild um
`Zielhöhe - Quellhöhe`.

Bei uns war das die Ursache verrutschter Zinnen. `anim_castle` führt alle
Vordergrund- und Abschlussstücke mit Pivot (0, 1), und zwar in beiden Spielen.
27 Frames betroffen, Versatz 38 px zu hoch bis 80 px zu tief.

Belegt an drei unabhängigen Messungen: beide Spiele hinterlegen für diese
Frames denselben Pivot; die Silhouetten-Korrelation auf den Frames, deren
Grafik in beiden Spielen fast gleich ist, ergibt genau den Versatz, den die
Eckkonvention vorhersagt; und ein Feldfoto eines Testers zeigt die Maske
`anim_castle 002` mit Korrelation 0,9991 bei Bildschirm-y 467, während der
Anker des Slots bei 449 liegt, also exakt 183 − 165 = 18 px daneben.

Nach der Korrektur, an einem frisch gebauten Atlas nachgemessen:

| Frame | Leinwand | Anker vorher | Anker nachher | Soll |
|---|---|---:|---:|---:|
| `anim_castle 001` | 183x98 | 138 | 98 | 98 |
| `anim_castle 004` | 260x146 | 226 | 146 | 146 |
| `anim_castle 007` | 108x174 | 161 | 174 | 174 |
| `anim_castle 027` | 681x287 | 305 | 287 | 287 |

Alle 27 tragen jetzt den Pivot exakt 1,0.

## Beide Regeln zusammen

```python
CORNER_PIVOT_EPSILON = 1e-6

def reanchored_pivot(bezug_pivot, bezug_groesse, eigene_groesse):
    """Normalisierter Pivot für eine Leinwand abweichender Größe."""
    if abs(bezug_pivot - 1.0) <= CORNER_PIVOT_EPSILON:
        anker = eigene_groesse        # Anker liegt auf der Kante der Quelle
    else:
        anker = bezug_pivot * bezug_groesse
    return anker / eigene_groesse
```

Sind Bezug und eigene Leinwand gleich groß, kommt der Bezugspivot unverändert
zurück. Der Eingriff ändert also nichts an Gruppen, die heute schon stimmen.

Absichtlich nicht angefasst haben wir Pivots außerhalb von [0,1] und alle
Bruchteil-Pivots. Der Eingriff bleibt damit auf die Frames beschränkt, die die
Eckkonvention wirklich benutzen.

## Die Entscheidung: worauf bezieht sich der Anker?

Das ist der Teil, den wir erst nach der Rückfrage an unseren eigenen Bäumen
geprüft haben. Er ist wichtig, weil er je nach Gruppe anders ausfällt.

### Der Pivot gehört zur Zeichnung, nicht zum Slot

Zwei Baumgruppen liegen in beiden Spielen unverändert vor. Dort lässt sich die
Konvention direkt ablesen:

| Gruppe | verglichene Frames | gleiche Größe | gleicher Pivot | Ankerabstand |
|---|---:|---:|---:|---:|
| `tree_apple` | 40 | 40 | 40 | 0,00 px |
| `tree_shrub1` | 25 | 25 | 25 | 0,00 px |

Wo beide Spiele dieselbe Grafik führen, führen sie auch denselben Pivot, bis
auf die letzte Stelle. Crusader hat die Sprite-Datensätze mitsamt Anker aus
Stronghold 1 übernommen.

### Es gibt keinen einheitlichen Slot-Anker

Abstand des Ankers über der sichtbaren Unterkante, an Crusaders eigenen Bäumen
gemessen, je 40 Frames aus der Animation:

| Gruppe | Median | Streuung |
|---|---:|---:|
| `Tree_Oak` | +3,0 px | 1,2 |
| `Tree_Chestnut` | +3,0 px | 1,3 |
| `tree_pine` | +5,5 px | 1,0 |
| `tree_birch` | +8,0 px | 1,0 |
| `tree_shrub1` | +16,0 px | 0,8 |
| `tree_shrub2` | +31,2 px | 0,0 |
| `tree_apple` | +50,0 px | 3,7 |

Innerhalb einer Gruppe ist der Wert sehr stabil, über die Gruppen hinweg reicht
er von 3 bis 50 Pixel. Er hängt daran, wie viel die jeweilige Zeichnung
unterhalb des Stammfußes zeigt, nicht am Slot.

### Daraus folgen zwei Fälle

**Gleiche Zeichnung, andere Leinwand.** Der Zielslot ist der richtige Bezug.
Bodenkacheln fallen immer hierunter, und dort entstehen die schwarzen Nähte.

**Echter Grafiktausch.** Eine Birke aus Stronghold 1 in einem Palmen-Slot
gehört an den Anker der Birke, nicht an den der Palme. Der Bezug ist dann das
Quellsprite. Praktisch heißt das: der Bauer muss auch die Quellinstallation
lesen und den Pivot von dort nehmen, so wie er heute schon die Zielinstallation
liest.

`anker_beweis.png` zeigt den Unterschied an vier Bäumen. Grün ist der Anker aus
der Quelle und sitzt am Stammfuß. Rot wäre die Umrechnung auf den Slot und
liegt daneben.

### Wie unsere Kette das handhabt

Wir wählen die Regel pro Gruppe, und der Prüfer weist jedem der 76.386
ausgelieferten Frames die Regel seiner Gruppe nach:

| Regel | Gruppen | Wer |
|---|---:|---|
| Anker der Quelle | 119 | Einheiten, Gebäudeanimationen, Vegetation |
| Crusader-Leinwand und -Pivot | 18 | Effekte, kleine Gruppen, einige Kachelsätze |
| Anker des Zielslots | 12 | Boden und Mauerwerk |

Die zwölf mit dem Zielanker sind genau die, bei denen es darauf ankommt:
`tile_land8`, `tile_land3`, `tile_land_macros`, `tile_sea8`, `tile_goods`,
`tile_buildings1`, `tile_buildings2`, `tile_castle`, `tile_churches`,
`tile_workshops`, `anim_buildings2`, `anim_castle`.

## Im Paket

`bauen` und `pruefen` kennen alle drei Fälle:

```
--anker ziel                              gleiche Zeichnung, andere Leinwand
--anker quelle --quellspiel <..._Data>    echter Grafiktausch
--anker frei                              Versatz nur melden
```

Unter `quelle` gehen Slots, die als unveränderte Crusader-Kopie mitgehen, nicht
als Fehler durch, sondern werden als erwarteter Mischfall gemeldet.

Wer unsicher ist, lässt einmal `--anker frei` laufen. Der Bericht nennt die
größte Abweichung in Pixeln, und daran sieht man, ob die Frage im konkreten
Fall überhaupt eine Rolle spielt. Bei gleich großer Leinwand liefern alle drei
Regeln dasselbe.

## Und eine Falle daneben

`GameSpriteManagerAPI.CreateReplacementSprite` (Zeilen 113 bis 119) übernimmt
für **Einzelsprites** den normalisierten Pivot des Crusader-Slots. Ein Frame
auf einer anderen Leinwand landet damit bei
`(pivot_x * eigene Breite, pivot_y * eigene Höhe)` und verrutscht lautlos auf
seiner Kachel. Eine Größenprüfung gibt es auf diesem Weg gar nicht.
`LoadTexture` premultipliziert außerdem Alpha, geliefert werden muss also
straight alpha.
