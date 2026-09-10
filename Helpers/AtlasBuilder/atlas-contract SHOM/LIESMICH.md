# Für Serp: was hier drin ist

Alles, was wir zum Thema Atlanten und Anker haben, in einem Paket. Entstanden
aus der Arbeit an einer Konversion von Stronghold 1 DE nach Crusader DE, rund
150 Gruppen und 76.386 Frames.

Nimm daraus, was dir passt. Die acht Zeilen der Ankerformel helfen schon allein.

## In dieser Reihenfolge

**1. [ANKER.md](ANKER.md)** — warum Texturen verrutschen und schwarze Nähte
entstehen, und die zwei Regeln, die das beheben. Das ist die Antwort auf
Smokelots Screenshots. Mit Messungen und einem Bild.

**2. [EINBAU.md](EINBAU.md)** — die konkreten Stellen in
`atlas_builder/core.py`, mit den Codeblöcken zum Einsetzen. Vier Eingriffe,
zusammen etwa dreißig Zeilen.

**3. [BEFUNDE_SCRIPT_EXTENDER.md](BEFUNDE_SCRIPT_EXTENDER.md)** — was wir am
Loader von SHCDE-SE 1.39 gemessen haben, mit Fundstellen im Dekompilat. Deckt
die sieben Punkte aus Issue #162 ab und hat am Ende eine Wunschliste für den
Extender selbst.

**4. [README.md](README.md)** — das Python-Paket: die vier Befehle, die
Gruppentabelle, was geprüft ist.

## Was du sofort benutzen kannst, ohne etwas einzubauen

```bash
pip install pillow unitypy
python -m atlas_contract pruefen <gruppe> \
    --spiel ".../Stronghold Crusader Definitive Edition_Data" \
    --atlas ".../Override/Atlas/<gruppe>"
```

Das liest nur die fertigen Dateien. An Smokelots bestehendem Mod zeigt es in
einem Durchlauf, welche Frames verrutscht sind und um wie viele Pixel.

## Was geprüft ist

| Prüfung | Ergebnis |
|---|---|
| Einheitentests, ohne Spiel | 51 bestanden |
| Extraktion gegen unsere eingefrorenen Hashes | 14 von 17 Slots bytegleich |
| Durchlauf mit echten Spieldaten | bestanden |
| Unsere 149 ausgelieferten Gruppen | 76.386 Frames, 0 Regelverstöße |

Zu den 3 abweichenden Slots und was dahintersteckt: siehe README, Abschnitt
„Was geprüft ist". Die Kurzfassung: unser Vergleichsmanifest stammt von einem
älteren Spielstand, und die betroffene Textur wurde seither neu kodiert.

## Grenzen

Alle Werte stammen aus dem Dekompilat von SHCDE 2.80 gegen SHCDE-SE 1.39. Ein
Spiel- oder Extender-Update macht sie ungültig, bis die Gruppentabelle neu
erzeugt ist. Dafür braucht es ein Dekompilat; melde dich, dann erzeugen wir sie.

Das Paket ändert nichts am Shader. Die sieben Vegetationsgruppen brauchen
weiterhin einen Eingriff zur Laufzeit, solange der Atlas-Loader
`Unlit/TeamColour` baut, wo Vanilla `Unlit/Foliage` benutzt. Der Prüfbefehl
weist darauf hin.
