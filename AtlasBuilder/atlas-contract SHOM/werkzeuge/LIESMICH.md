# Werkzeuge

Zwei Sorten. Die erste ist für dich, die zweite ist Beleg.

## Allgemein brauchbar

`durchlauf.py`
: Prüft das Paket von Anfang bis Ende an echten Spieldaten. Erzeugt zehn
  absichtlich zu große Vorlagen, füllt den Rest auf, baut, prüft und verdirbt
  am Schluss die Pivots, damit sichtbar ist, dass die Prüfung sie fängt.

```bash
python werkzeuge/durchlauf.py ".../Stronghold Crusader Definitive Edition_Data"
```

`tabelle_erzeugen.py`
: Erzeugt `atlas_contract/loader_groups.json` neu. Braucht ein Dekompilat des
  Spiels: `Enums.cs`, `SpriteMapping.cs` und `spriteLoader.SpriteLoad`. Ohne
  Dekompilat läuft es nicht.

## Belege aus unserer Messung

Die Dateien mit dem Präfix `messung_` und `nachweis_` sind die Skripte, mit
denen die Zahlen in [ANKER.md](../ANKER.md) entstanden sind. Sie enthalten
**fest verdrahtete Pfade auf unsere Installationen und auf unseren Bauordner**
und laufen bei dir nicht ohne Anpassung. Sie liegen bei, damit die Zahlen
nachvollziehbar sind und nicht nur behauptet.

| Datei | Was sie misst |
|---|---|
| `messung_gleiche_grafik.py` | Gleiche Grafik in beiden Spielen, gleicher Pivot? |
| `messung_baum_anker.py` | Abstand des Ankers über der sichtbaren Unterkante, Crusader |
| `messung_sh1_baeume.py` | Dasselbe für Stronghold 1 |
| `messung_beweisblatt.py` | Erzeugt `anker_beweis.png` |
| `messung_alle_gruppen.py` | Ankerregel je Frame über alle ausgelieferten Gruppen |
| `messung_policy_treue.py` | Hält jede Gruppe die Regel, die ihr Profil deklariert? |
| `messung_offene_drei.py` | Die Gruppen mit umgesetzten Indizes |
| `nachweis_gegen_produktion.py` | Extraktion gegen unsere eingefrorenen Hashes |

Wer sie benutzen will, ändert oben die drei Konstanten für Quellspiel,
Zielspiel und Atlasordner.
