# Pech vor Treppen und KI-Gebäudeabriss in SHCDE

Stand: 01.11.2026. Sammlung der bisherigen Untersuchung; noch kein implementierter Fix.

Nachträge vom selben Tag: Abschnitt 15 enthält die bestätigte Neuspiel-/Save-Reproduktion und den Bericht über Auftreten ohne Laden. Abschnitt 16 ist der historische Kernplan, Abschnitt 17 dessen erweiterte Prüfung. Abschnitt 18 enthält den damaligen Vergleich mehrerer Fixansätze und die Nutzervorgabe, keine beschädigten Savegames zu reparieren. Abschnitt 19 ergänzt die vertiefte Prüfung von Variante B nach der anschließenden Nutzerpräferenz für diesen Ansatz. Frühere Befunde bleiben als historische, hashgebundene Nachweise erhalten.

## 1. Ergebnis und Evidenzgrenzen

Pech gilt im untersuchten Vanilla-Code nicht pauschal als erhöhtes Terrain. Seine Platzierung senkt die gespeicherte Feldhöhe um 4. Vor einer von der AIV erzeugten untersten Treppenstufe kann dadurch ein vorher erlaubter Höhenunterschied von 16 auf 20 anwachsen. Bei einer vollständigen Neuberechnung der Übergänge zwischen Feldern wird die Verbindung dann verworfen.

Zusätzlich besteht im untersuchten Baupfad eine Aktualisierungslücke: Wird Pech erst nach der Treppe gebaut, bleiben vorhandene Übergangsverbindungen zunächst erhalten. Eine Neuberechnung der zusammenhängenden Weggebiete allein entfernt diese Verbindung nicht, weil sie die vorhandenen Übergangsmasken verwendet. Beim Laden berechnet Vanilla vorher auch die Übergangsmasken vollständig neu. Dadurch können ein Neuspiel und ein geladenes Spiel mit gleichem Burggrundriss unterschiedliche Ergebnisse liefern.

Mit den Daten des übergebenen Saves und originalen Vanilla-Funktionen wurde isoliert nachgestellt:

| Zustand | Gebäudereichbarkeit der fünf Vergleichsgebäude | Drei untersuchte normale Wege | Neu zum Abriss markierte Gebäude |
|---|---|---|---:|
| Vor dem entscheidenden Pechstreifen | erreichbar | gefunden | nicht separat gemessen |
| Tatsächliche native Pechplatzierung, danach nur Weggebiete neu berechnet | erreichbar | gefunden | 0 |
| Danach Übergänge vollständig neu berechnet, anschließend Weggebiete und Verbindungsdaten aktualisiert | getrennt | nicht gefunden | 25 |

Die isolierten Aufrufe sind kein vollständiger Spielablauf. Sie beweisen die genannten Zustandsübergänge unter den rekonstruierten Eingaben. Der Zustand des zuletzt beschriebenen, funktionierenden Neuspiels wurde nicht direkt aus diesem Neuspiel erfasst.

### Evidenzarten

- **Nutzerbeobachtung:** vom Nutzer berichtetes Verhalten im laufenden Spiel; kein eigener protokollierter Live-Test.
- **Dateibefund:** unmittelbar aus den übergebenen Save-/AIV-Dateien ausgelesen.
- **Statischer Codebefund:** Daten- und Kontrollfluss der hashgebundenen Vanilla-DLL.
- **Isoliertes natives Experiment:** Aufruf originaler Maschinenfunktionen in einem privaten Speicherabbild eines separaten Python-Prozesses mit rekonstruierten Save-Daten.
- **Offen / Hypothese:** noch nicht direkt nachgewiesene Übertragung auf weitere Spielzustände oder mögliche Fixgestaltung.

## 2. Ausgangsbeobachtungen des Nutzers

- Ein Custom-Lord reißt Gebäude seiner äußeren Burg ab, wenn Pech vor einer entscheidenden Treppe liegt.
- Ohne das betreffende Pech werden die Gebäude nicht mehr abgerissen.
- Das Savegame test2_pitchstrais.sav zeigt den Abriss laut Nutzer sowohl mit Vanilla als auch mit den im Save gespeicherten Modsettings und mit Standard-Modsettings.
- Einheiten können laut Nutzer über Pech auf eine Treppe und zurück laufen; dies wurde auch nach dem Laden berichtet. Diese Beobachtung darf nicht durch das Ergebnis einer isolierten Wegsuche ersetzt werden.
- Mit test_wolf_pitchstairs.aivjson konnte der Nutzer den Fehler nicht reproduzieren.
- Auch ein neu gestartetes Spiel mit Castillo de Burgalimar Pitch Test.aivjson zeigt laut Nutzer den Fehler nicht, obwohl diese Burg mindestens sehr ähnlich zum problematischen Save ist.

Der spätere Dateivergleich zeigt für die zweite AIV sogar Übereinstimmung aller 204 Nicht-Bergfried-Bauschritte mit den vorbereiteten Bauschritten im Save. Das ist eine Aussage über den Bauplan, nicht über identische Laufzeitdaten oder identische Ausgangskarten.

## 3. Quellen, Identitäten und Werkzeuge

### 3.1 Native Referenz

Kanonische DLL:

    E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll

SHA-256:

    FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2

Dateigröße zum Dokumentationszeitpunkt: 3.451.392 Bytes.

Die installierte DLL wurde gegen CURRENT.json, das Datenbankmanifest und die binary_hash-Zuordnung der verwendeten Funktionsdatensätze abgeglichen. Die historische Workspace-DLL wurde nicht als aktuelle Analysequelle verwendet.

Baseline-Einstiege:

- [CURRENT.md](<D:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/_inspect/CrusaderDE-Native-Baseline/CURRENT.md>)
- [CURRENT.json](<D:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/_inspect/CrusaderDE-Native-Baseline/CURRENT.json>)
- [DATABASE_INFO.json](<D:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/DATABASE_INFO.json>)
- [Semantische Abfrageoberfläche](<D:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/_inspect/CrusaderDE-Native-Baseline/tools/semantic/query.ps1>)

Die im Manifest referenzierte SQLite-Datenbank und die semantischen Exporte liefern Pseudocode, Caller, Callees und Datenreferenzen. Viele generisch benannte Funktionen sind dort formal noch mit confidence=candidate geführt. Die hier beschriebene Bedeutung beruht zusätzlich auf der nachvollzogenen Verarbeitung und, wo angegeben, den isolierten Aufrufen. Diese Dokumentation verändert keine Vertrauensstufen im Baseline-Index.

Alle folgenden Adressen sind RVAs relativ zur oben identifizierten DLL. VA bei bevorzugter ImageBase 0x180000000 = ImageBase + RVA; im privaten Versuchsabbild wurden Relokationen berücksichtigt. Die Adressen sind keine versionsunabhängigen APIs.

### 3.2 Übergebene Dateien

Basisverzeichnis:

    C:\Users\Serpens66\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition

| Datei relativ zum Basisverzeichnis | Größe in Bytes | SHA-256 |
|---|---:|---|
| Saves\test2_pitchstrais.sav | 4.254.397 | 38c6ac5e6b1e16d1d6fc8e5c8df85f304d17ab08924fe750b85f10eff78ca92f |
| ExtendedLords\Wolf\test_wolf_pitchstairs.aivjson | 72.432 | 8ecc884a0e1ab2437079d6b1b57ffe0eb4449c8d014439f474a1997ed5250c03 |
| ExtendedLords\Wolf\Castillo de Burgalimar Pitch Test.aivjson | 19.825 | e7e1bcabfdc5a31f0b97a5fd039bc30b32adf3000bb93c7842c948daef27a401 |

Die Identitäten wurden beim Erstellen dieser Datei erneut geprüft. Sie kennzeichnen die untersuchten Versionen, keine dauerhaft unveränderlichen Dateien.

Verwendeter Save-Parser:

[scde_map_parser.py](<D:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/Helpers/MapParser/Reference/scde_map_parser.py>)

Die Python-Klasse MapFile.parse wurde direkt mit den Save-Bytes aufgerufen. Die Einordnung der CLI als Kartenparser schließt dieses direkte Parsen des vorliegenden Saves nicht aus.

Python:

    D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe

## 4. Höhen und Feldmerkmale

### 4.1 Pech

Die native Platzierung bei RVA 0x59380 schreibt sinngemäß:

    neue Feldhöhe = max(0, bisherige Feldhöhe - 4)
    Logikflags |= 0x8

Sie erzeugt außerdem einen Pechdatensatz mit Besitzer, Position und Zustand, aktualisiert Verwaltungsdaten, verändert weitere Feldbits und markiert das Feld als geändert. Der direkte Pfad berechnet keine Übergangsmaske neu.

Für das entscheidende Feld im Save:

| Eigenschaft | Wert |
|---|---|
| Weltposition | (428, 479) |
| Gepackter Feldindex | 217787 |
| Default-Höhe | 8 |
| Gespeicherte Höhe mit Pech | 4 |
| Logikflags | 0x8008 |
| PCL im gespeicherten Zustand | 90 |

Das Flag 0x8 allein erklärt den Fehler nicht: Sein isoliertes Entfernen bei weiterhin gespeicherter Höhe 4 beseitigte den Fehler im nativen Experiment nicht.

### 4.2 AIV-Treppe

Die AIV-Wand-/Treppenplatzierung bei RVA 0x6CB20 verwendet für die fünf Stufen Default-Höhe plus feste Aufschläge:

| AIV-itemType | Aufschlag | Höhe bei Default 8 |
|---:|---:|---:|
| 181 | 80 | 88 |
| 182 | 64 | 72 |
| 183 | 48 | 56 |
| 184 | 32 | 40 |
| 185 | 16 | 24 |

Die Bezeichnungen sind AIV-Mapperwerte. Sie sind nicht mit nativen Gebäudetypen oder allgemeinen Terrain-IDs gleichzusetzen. Für itemType 186 wurde in diesem Vanilla-Erzeugungspfad kein entsprechender Höhenzweig gefunden; daraus folgt keine allgemeine Aussage über andere Erzeuger.

Bei (428, 478), dem entscheidenden Treppenfuß, enthält der Save Höhe 24, Default-Höhe 8 und Logikflags 0x900. Sein gepackter Feldindex ist 217144.

Die Treppenplatzierung selbst aktualisiert das Pathfinding lokal. Wird auf demselben Feld vorhandenes Pech ersetzt, enthält der Pfad auch das Entfernen des Pechflags mit einer Höhenkorrektur um 4. Das ist vom hier untersuchten Pech auf dem Nachbarfeld vor dem Treppenfuß zu unterscheiden.

### 4.3 Manuell gebaute Treppen

Der manuelle Bauweg unterscheidet sich:

    0x7A3B0 -> 0x6A4C0 -> 0x6F230

Die Ermittlung bei 0x6A4C0 sucht die maximale Höhe einer passenden benachbarten Wand. 0x6F230 bildet die erste Stufe unterhalb dieser Höhe und reduziert die Höhe im Verlauf um jeweils 16.

Beispiel bei geeigneter Ausgangswandhöhe 98:

    82, 66, 50, 34, 18

Dann liegt zwischen Pechhöhe 4 und unterster Stufe 18 nur ein Unterschied von 14. Ein solcher manueller Aufbau kann daher funktionieren, während eine AIV-Stufe auf Höhe 24 nach vollständiger Neuberechnung abgetrennt wird.

Das war eine bedingte Erklärung für fehlgeschlagene manuelle Reproduktionen. Sie erklärt nicht allein die später ausdrücklich übergebenen AIV-Burgen.

## 5. Nativer Ablauf und relevante Funktionen

### 5.1 Erzeugung und Aktualisierung

| RVA | Rolle im untersuchten Ablauf |
|---|---|
| 0x51790 | AIV-Ausführung; Pechgruppe über Mapperwert 99 wird feldweise durch den allgemeinen Bau-Dispatcher verarbeitet. |
| 0x6D580 | Allgemeiner Bau-Dispatcher; interner Fall 0x44 ruft die Pechplatzierung auf. Der gemeinsame Abschluss markiert Umgebung und Weggebiete als geändert und verkürzt den Aktualisierungs-Countdown. |
| 0x59380 | Pechdatensatz erzeugen, Feldhöhe um 4 senken und Pechflag setzen; kein direkter Aufruf der Übergangsberechnung. |
| 0x6CB20 | AIV-Wände und -Treppen mit festen Höhenaufschlägen erzeugen; lokale Pathfinding-Aktualisierung. |
| 0xE0850 | c_game_update_pathfinding_pot; im AIV-Treppenbau verwendeter Aktualisierungseinstieg. |
| 0xE3B90 | Umgebung durch geometrische Nachbarschaftssuche als geändert markieren; keine Neuberechnung der Übergangsmasken in dieser Funktion. |
| 0xD95A0 | Countdown für die anstehende Weggebietsaktualisierung verkürzen; berechnet selbst keine Übergänge neu. |

Der gemeinsame Abschluss des Pechbaus ruft 0xE3B90 mit einem Umgebungsradius auf, setzt das Dirty-Feld des Pathfinding-Managers und ruft 0xD95A0 auf. Aus der Markierung einer Umgebung folgt deshalb nicht automatisch, dass ihre Übergangsmasken bereits mit der neuen Höhe berechnet wurden.

### 5.2 Zwei getrennte Ebenen der Wegdaten

1. **Übergangsmasken:** Pro Feld acht Richtungsbits, die erlaubte Übergänge zu Nachbarfeldern beschreiben.
2. **PCL / Weggebiete:** Zusammenhängende Gebiete, die anhand dieser Übergänge geflutet werden; zusätzliche Verbindungsdaten erlauben unter anderem Beziehungen über Tore.

| RVA | Rolle |
|---|---|
| 0xD86F0 | Übergangsmaske eines Feldes berechnen; liest Höhe, Logikflags und relevante Gebäudeinformationen. Im hier betroffenen Zweig gilt ein Höhenfenster von ±16. |
| 0xDAA50 | Vollständiger Durchlauf der Übergangsberechnung über die gepackten Kartenfelder; anschließend gegebenenfalls diagonale Nachbearbeitung. |
| 0xE04B0 | Nachbearbeitung vorgemerkter diagonaler Übergänge. |
| 0xE49D0 | PCL neu bilden; verwendet die vorhandenen Richtungsbits. Ruft nicht als allgemeinen ersten Schritt 0xDAA50 auf. |
| 0xC5040 | Gebäudebezogene Aktualisierung innerhalb der PCL-Neuberechnung. |
| 0xC4BF0 | Tore während der Gebietsermittlung temporär umschalten; torbezogene Änderungen sind nicht mit einer vollständigen Kartenneuberechnung gleichzusetzen. |
| 0xF4540 | Verbindungsdatensätze aktualisieren. |
| 0xE2610 | Im untersuchten Aufrufpfad Erreichbarkeit zwischen PCL für einen Spieler ermitteln. |
| 0x71670 | Lade-Normalisierung: unter anderem 0xDAA50, danach 0xE49D0 mit erzwungener Aktualisierung sowie weitere Gebäude-/Verbindungsaktualisierungen. |

Eine unterschiedliche PCL-Nummer allein beweist keine Unerreichbarkeit: Verbindungen zwischen Gebieten und deren Spieler-/Torbedingungen gehören zur Prüfung dazu. Die konkreten Nummern werden neu vergeben und sind keine stabilen Identitäten.

### 5.3 Gebäudereichbarkeit und Abrissmarkierung

| RVA | Rolle |
|---|---|
| 0xC90E0 | Vorhandenes Zugangsfeld prüfen: Höhe, Terrain, Bergfried-PCL, Verbindung zwischen PCL und typabhängige Ausnahmen; bei Bedarf Zugang erneut suchen. |
| 0xBACC0 | Vorbereitung der erneuten Zugangssuche. |
| 0xBB3D0 | Zugangsfelder am Gebäudeumfeld suchen; berücksichtigt Höhe, Terrain und Weggebietsverbindung. Enthält mehrere Suchzweige und typabhängige Unterschiede. |
| 0xC8F50 | Gebäude eines Spielers auf Erreichbarkeit prüfen und bei Ergebnis 0 oder 2 den Zustand 3 setzen; gegebenenfalls auf zugehörige Gebäudegruppe übertragen. |
| 0x55E10 | Nachgelagerte Behandlung des betroffenen KI-Baubereichs; im untersuchten Ablauf temporäre Vermeidung des groben Baugebiets. |

Rückgaben von 0xC90E0 im untersuchten Vertrag:

- 1: gültiger erreichbarer Zugang.
- 2: geometrisch geeigneter Zugang gefunden, aber im betreffenden Suchzweig keine passende Verbindung zum Bergfriedbereich.
- 0: kein entsprechender Zugang gefunden.

0xC90E0 prüft ein gespeichertes Zugangsfeld zunächst direkt und verwendet anschließend 0xBACC0 / 0xBB3D0 als Fallback. Die Terrainprüfung verwendet unter anderem die Maske 0x50501481; bestimmte Vegetationsfälle und Gebäudetypen besitzen Sonderbehandlung. Die PCL-Flutung verwendet eine eigene Maske 0x4A5014B1. Diese Masken sind keine austauschbaren allgemeinen Begehbarkeitsregeln.

Die Abrissprüfung überspringt bestimmte Gebäudetypen über die Tabelle bei RVA 0x2E4FE0. Bei Ergebnis 0 oder 2 setzt sie Zustand 3. Bei nichtnulliger Gruppenzuordnung kann sie dieselbe Zustandsänderung auf weitere aktive Gebäude derselben Gruppe übertragen.

**Zustand 3 wurde im Experiment als Abrissmarkierung gemessen. Der anschließend tatsächlich ausgeführte Abriss wurde im isolierten Prozess nicht bis zum terminalen Spielzustand simuliert.** Dass die Gebäude im Spiel verschwinden, ist zusätzlich eine Nutzerbeobachtung.

### 5.4 Normale Einheitenwege

Die untersuchten normalen Wegsuchpfade umfassen 0xF4930, 0xF3060, 0xF32B0, 0xDAAC0 und 0xDA590. Sie verwenden ebenfalls vorhandene Übergangsdaten. Eine gültig gebliebene alte Übergangsmaske kann daher auch normale Einheitenwege ermöglichen, obwohl die neue rohe Höhendifferenz bei einer Neuberechnung abgelehnt würde.

Die isolierten Vergleichswege wurden direkt über 0xDA590 geprüft. Das ist kein vollständiger Nachweis jeder Bewegungsentscheidung einer realen Einheit einschließlich bestehender Routen, Startzustand und späterer Aktualisierungen.

### 5.5 Relevante Speicherverträge

Alle Werte gelten ausschließlich für die oben identifizierte DLL und die analysierten Strukturen.

| RVA / Offset | Bedeutung im Versuch |
|---|---|
| 0x405EDB0 | Im Versuch verwendete Basis des Tile-Managers; am Anfang außerdem zeilenabhängige Nachbardeltas. |
| Tile-Basis +0x898400 = 0x48F71B0 | Feldlogik, uint32 je gepacktem Feld. |
| Tile-Basis +0xD7E5A0 = 0x4DDD350 | Gespeicherte Feldhöhe, Byte je Feld. |
| Tile-Basis +0xDCCAC0 = 0x4E2B870 | Default-Höhe, Byte je Feld. |
| 0x4B6AA50 | Gebäudebelegung, 16 Bit je Feld. |
| 0x4A7FAF0 | Änderungsmarkierungen; Pechbau/Umgebungsmarkierung schreibt hier unter anderem 2. |
| 0x50EC690 | PCL, 16 Bit je Feld. |
| 0x51890D0 | Übergangsmaske, Byte je Feld. |
| 0x60AD660 | Im Versuch verwendete Pathfinding-Managerbasis. |
| Pathfinding-Basis +0x6C | Dirty-Feld für Gebietsermittlung. |
| Pathfinding-Basis +0x2024 | Verbindungsdatensätze mit Stride 0x204. |
| 0x37ED4CC | Countdown für die PCL-Neuberechnung. |
| 0x64CCBB0 | Gebäudemanagerbasis. |
| Gebäudemanager +0x50 | Im Versuch gesetzte Gebäude-Suchgrenze. |
| Gebäudemanager +0x5C + ID*0x32C | Beginn des zu Save-Sektion 4013 passenden Gebäuderecords. Record 0 bleibt als Slot erhalten. |
| Gebäuderecord +0xD0 / +0xD2 | Zustand / nativer Gebäudetyp, jeweils 16 Bit. |
| Gebäuderecord +0xEE / +0xF0 | Im Save ausgelesene Gebäudeanker X/Y, jeweils 16 Bit. |
| Gebäuderecord +0xFE / +0x100 | Gespeichertes Zugangsfeld X/Y, jeweils 16 Bit. |
| Gebäuderecord +0x2A8 | Gruppenzuordnung, die bei der Abrissmarkierung berücksichtigt wird. |
| 0x379AF10 | Spielerressourcenbasis; Stride 0x583C. |
| Spielerrecord +0xA0 | Gepacktes Referenzfeld für die Bergfried-PCL-Prüfung. |

Offsets relativ zum Save-Record dürfen nicht ungeprüft als Offsets relativ zum Gebäudemanager oder zu einem Extender-Strukturzeiger verwendet werden.

## 6. Savegame test2_pitchstrais.sav

### 6.1 Ausgelesene Sektionen und Erweiterungsdaten

| Sektion | Für die Untersuchung verwendete Daten |
|---:|---|
| 3003 | Logikflags der 320800 gepackten Felder, uint32. |
| 3005 | Gespeicherte Feldhöhen. |
| 3045 | Default-Höhen. |
| 3012 | Gebäudebelegung der Felder. |
| 3021 | Gespeicherte PCL. |
| 4013 | 4000 Gebäuderecords mit jeweils 812 Bytes (0x32C). |
| 2022 | Neun Spielerrecords mit jeweils 22588 Bytes (0x583C). |
| 1107 | Neun native AIV-Slots mit jeweils 0x6D98 Bytes; vorbereitete Bauschritte und Feldpuffer. |
| 1031 | 200 Verbindungsrecords mit jeweils 516 Bytes (0x204). |
| 1017 | Ergänzende Verwaltungswerte; ausgelesene Folge 1, 5, 6, 1, 0, 0, 0. |

Im angehängten ZIP-/Moddatenbereich wurden außerdem ausgewertet:

- _SE_ModData_extendedaivbuffer-savedata.msgpack: Erweiterungspuffer mit buffers[1].buffer für die AIV-Daten des untersuchten Lords.
- _SE_ModData_APIShared-SavegameModSettings.msgpack: gespeicherte Modsettings, Schema 3; Mod-GUID-bezogene Datensätze enthalten nochmals MessagePack-kodierte Werte.

Unter den zuvor ausgelesenen Werten war modeInaccessibleProtection=1. Dies wurde als verbesserte Erreichbarkeitsbehandlung und nicht als pauschales Verbot jedes Abrisses eingeordnet. Die Vanilla-Gegenprobe ist unabhängig davon; die drei vom Nutzer berichteten Konfigurationen zeigten denselben Abriss. Daraus folgt kein vollständiger Kompatibilitätstest aller Modfeatures oder Kombinationen.

### 6.2 AIV-Zuordnung und Koordinaten

Untersuchter nativer AIV-Slot 1:

- Besitzer: 1.
- Gespeicherte AIC-Zuordnung: 31.
- Rotation: 0.
- Maximale Schrittzahl: 205.
- Layout-Ursprung X/Y: 349/448.
- Gespeicherte Keep-Referenz X/Y: 392/491.

Die Keep-Referenz im AIV-Kontext ist von den tatsächlich für die Erreichbarkeitsprüfung verwendeten Weltfeldern zu unterscheiden.

Für die unrotierte Zuordnung dieser Burg gilt bei AIV-Offset n:

    Spalte = n % 100
    Zeile = n // 100
    Welt-X = 349 + Spalte
    Welt-Y = 547 - Zeile

AIV-Offsets sind somit keine gepackten Weltfeldindizes.

| Rolle | Weltposition | Feldhöhe | Gespeicherte PCL |
|---|---|---:|---:|
| Im Wegvergleich verwendetes Feld im Bergfriedbereich | (425, 493) | 8 | 90 |
| Pech direkt vor dem kritischen Treppenfuß | (428, 479) | 4 | 90 |
| Kritischer Treppenfuß, AIV-Offset 6979 | (428, 478) | 24 | 90 |
| Zugangsfeld des Vergleichsgebäudes 212 außen | (382, 521) | 8 | 89 |

Die gespeicherten PCL sind eine Momentaufnahme. Insbesondere liegt das äußere Vergleichsfeld bereits im Save in einer anderen PCL. Daraus allein lässt sich der Abriss nicht ableiten; die Verbindungsdaten gehören dazu. Bei vollständiger isolierter Neuberechnung wurden im damaligen Versuch andere Nummern vergeben, unter anderem 119 für den Bergfriedbereich, 104 für einen Hauptburgbereich und 103 außen. Diese Zahlen sind versuchsspezifisch und keine erwarteten Konstanten.

### 6.3 Untersuchte Gebäude und neue Abrissmarkierungen

Für die detaillierten Erreichbarkeitsvergleiche wurden unter anderem IDs 2, 27, 128, 174, 176, 182, 183, 185, 187, 188, 189, 195, 199, 212, 213, 214 und 217 betrachtet. Die fünf wiederholt verglichenen Gebäude waren 128, 199, 212, 213 und 214.

Im Originalzustand nach vollständiger isolierter Wegneuberechnung wurden folgende 25 IDs durch den Vanilla-Prüflauf neu von ihrem vorherigen Zustand in Zustand 3 überführt:

| ID | Nativer Gebäudetyp | Gebäudeanker X/Y | Gespeichertes Zugangsfeld X/Y |
|---:|---:|---|---|
| 31 | 24 | (415, 502) | (417, 507) |
| 32 | 53 | (415, 507) | (417, 512) |
| 128 | 108 | (384, 468) | (386, 473) |
| 129 | 57 | (389, 468) | (391, 473) |
| 146 | 58 | (384, 473) | (386, 478) |
| 151 | 56 | (389, 473) | (391, 478) |
| 180 | 27 | (398, 465) | (399, 468) |
| 196 | 17 | (397, 469) | (396, 472) |
| 197 | 17 | (397, 473) | (396, 476) |
| 198 | 17 | (397, 477) | (399, 481) |
| 199 | 17 | (411, 510) | (412, 509) |
| 200 | 17 | (407, 510) | (408, 509) |
| 201 | 17 | (403, 510) | (404, 509) |
| 202 | 17 | (399, 510) | (400, 509) |
| 203 | 70 | (387, 510) | (387, 509) |
| 209 | 22 | (409, 498) | (411, 503) |
| 210 | 22 | (407, 481) | (409, 486) |
| 211 | 37 | (361, 493) | (365, 502) |
| 212 | 7 | (381, 518) | (382, 521) |
| 213 | 7 | (405, 518) | (406, 521) |
| 214 | 7 | (408, 518) | (409, 521) |
| 215 | 18 | (405, 464) | (407, 468) |
| 216 | 18 | (401, 464) | (403, 468) |
| 218 | 25 | (366, 474) | (368, 479) |
| 219 | 59 | (366, 479) | (368, 484) |

Die Tabellenwerte zu Anker und Zugang stammen aus dem ursprünglichen Save. Zugangssuchen im Experiment können gespeicherte Zugangsfelder ändern; die Tabelle behauptet nicht, dass diese Felder nach jedem Aufruf unverändert blieben. Alle 25 Records hatten im ausgelesenen Save Zustand 2.

## 7. Isolierte Gegenproben zur Ursache

### 7.1 Versuchsaufbau und Grenzen

Die originale DLL wurde als privates Speicherabbild aufgebaut: PE-Header und Sektionen kopiert, DIR64-Relokationen angewendet, relevante Save-Sektionen an die zuvor geprüften Datenadressen geladen. Nachbaroffsets, Zeilentabellen und gültige Kartenkoordinaten wurden rekonstruiert. Die Versuche verwendeten 320800 gepackte Kartenfelder und die Originaltabellen der DLL.

Geladen wurden insbesondere Feldlogik, Höhen, Default-Höhen, Gebäudebelegung, PCL, Gebäude-, Spieler- und Verbindungsrecords. Nicht alle weiteren Spielsysteme wurden rekonstruiert; beispielsweise standen bestimmte Vegetations-/Schadensdaten nicht vollständig wie im laufenden Spiel bereit. Die entscheidende untersuchte Burgstelle enthält keine Bäume. Die Aussagekraft wird durch die kontrollierten Änderungen desselben rekonstruierten Zustands gestützt.

Die verwendeten Funktionen arbeiteten ausschließlich im privaten Prozessabbild. Das laufende Spiel, die installierte DLL, das Savegame und die AIV-Dateien wurden dadurch nicht verändert. Die Versuchsprogramme wurden über die Shell an Python übergeben; diese Dokumentation enthält die Methode und Ergebnisse, aber keinen separat gespeicherten vollständigen Testtreiber oder ein vollständiges Laufzeitabbild.

### 7.2 Pechflag und Höhe getrennt verändert

Vor jeder Gegenprobe wurden die wesentlichen Ausgangsdaten wiederhergestellt. Danach:

    0xDAA50 -> 0xE49D0(force=1) -> 0xF4540
    Gebäudereichbarkeit prüfen
    normale Wege über 0xDA590 prüfen
    0xC8F50 für Spieler 1 ausführen

Drei normale Vergleichswege:

1. (425, 493) -> (382, 521): Bergfriedbereich zum äußeren Gebäudezugang.
2. (428, 479) -> (428, 478): Pechfeld zur untersten Stufe.
3. (428, 478) -> (428, 479): Rückrichtung.

| Gegenprobe am Feld (428, 479) | Höhe während Übergangsberechnung | Höhe bei nachfolgenden Prüfungen | Pechflag bei nachfolgenden Prüfungen | Ergebnis für IDs 128/199/212/213/214 | Normale Wege | Neue Abrissmarkierungen |
|---|---:|---:|---|---|---|---:|
| Original | 4 | 4 | gesetzt | jeweils 2 | alle 0 | 25 |
| Nur Flag 0x8 entfernt | 4 | 4 | entfernt | jeweils 2 | alle 0 | 25 |
| Nur für die Übergangsberechnung Höhe 8; danach Höhe 4 wiederhergestellt | 8 | 4 | gesetzt | jeweils 1 | alle 1 | 0 |
| Einzelnes Pechfeld entfernt: Höhe 8 und Flag gelöscht | 8 | 8 | entfernt | jeweils 1 | alle 1 | 0 |

0 und 1 in der Wegspalte sind die Rückgaben der untersuchten normalen Wegsuche.

Die dritte Gegenprobe ist besonders aussagekräftig: Es genügte, die Übergangsverbindungen mit korrigierter Höhe zu berechnen. Bei der späteren Gebäudereichbarkeitsprüfung durfte das reale Feld wieder Höhe 4 und sein Pechflag besitzen. Für diesen Save trennt die falsche Übergangsberechnung den Bergfriedbereich ab; es handelt sich nicht nur um eine direkt am Gebäude scheiternde Höhenprüfung.

Das Entfernen eines einzigen entscheidenden Pechfeldes genügte in dieser Gegenprobe für die untersuchten Verbindungen. Daraus folgt keine allgemeine Regel, dass jede Burg durch genau ein Feld repariert werden kann.

## 8. Erste Testburg: test_wolf_pitchstairs.aivjson

Die Datei enthält 799 Frames und sechs unterste Treppenstufen mit itemType 185. Framezahlen sind hier und im Folgenden 1-basiert.

| Treppenfuß, AIV-Offset | Spalte/Zeile | Frame | Befund des Blueprint-Vergleichs |
|---:|---|---:|---|
| 4255 | (55, 42) | 732 | Pech vor der Treppe, aber mögliche alternative Verbindung über die seitliche Lagerkante. |
| 4336 | (36, 43) | 737 | Umgebung durch Gebäude/Pech geprägt; kein hier nachgewiesener freier Ersatzweg. |
| 3131 | (31, 31) | 742 | Umgebung unter anderem durch Ölschmelze/Galgen geprägt; nicht allein als freier Zugang zu werten. |
| 5021 | (21, 50) | 747 | Zugang im Blueprint durch Pech betroffen. |
| 5071 | (71, 50) | 752 | Zugang im Blueprint durch Pech betroffen. |
| 7548 | (48, 75) | 758 | Vorfeld 7648 ist Mapperwert 98, kein Pech; möglicher Zugang mit normaler Bodenhöhe. |

### Alternative bei Offset 4255

- Direkt davor liegt Pech bei Offset 4355.
- Das seitliche Nachbarfeld 4254 liegt an einem Vorratslager mit Mapperwert 52 und Anker 4450.
- Das betrachtete Lager belegt die Spalten 50 bis 54 und die Zeilen 40 bis 44.
- Für die betrachtete Lagerkante ergibt sich eine begehbare Höhe 18. Zur AIV-Stufe auf Höhe 24 beträgt der Unterschied nur 6.

### Alternative bei Offset 7548

- Das Vorfeld 7648 ist als Mapperwert 98 / Killing Pit angelegt, nicht als Pechfeld.
- Bei normaler Bodenhöhe 8 beträgt der Unterschied zur untersten AIV-Stufe 24 genau 16.
- Links daneben bei Offset 7547 liegt dagegen Pech.

Diese Alternativen erklären, warum Pech vor einzelnen Treppen die Burg nicht zwangsläufig vollständig abtrennt. Die Datei wurde als Blueprint untersucht; kein vollständiger Live-Bauablauf dieser ersten Testburg wurde mitgeschnitten. Ob die alternativen Verbindungen zu jedem Zeitpunkt tatsächlich gebaut, frei und passend verbunden sind, bleibt eine Laufzeitfrage.

## 9. Zweite Testburg: Castillo de Burgalimar Pitch Test.aivjson

### 9.1 Vergleich mit dem Save

Die Datei enthält 205 Frames. Beim Vergleich mit den im Save vorbereiteten nativen AIV-Schritten wurden die Typzuordnung, die Anzahl der Felder und die übersetzten Weltfeldmengen für alle 204 Nicht-Bergfried-Schritte verglichen. Ergebnis: keine Abweichung.

Die Reihenfolge der Felder innerhalb einer Gruppe wird nativ teilweise umgeordnet. Deshalb wurden die Feldmengen beziehungsweise sortierten Koordinaten verglichen. Der Bergfried in Schritt 1 wird gesondert erzeugt und war von diesem Vergleich ausgenommen; der native Schritt 0 ist ein Verwaltungs-/Dummy-Eintrag.

Das belegt dieselbe vorbereitete Baufolge für diese Schritte. Es belegt keine Identität der gesamten Karte, der Bauzeitpunkte, der Einheiten oder der zwischengespeicherten Wegdaten.

### 9.2 Drei Treppen

| Fuß-Offset | Weltposition im Save | Treppenframes | Zugangsverhältnisse |
|---:|---|---|---|
| 6979 | (428, 478) | 19–23 | Entscheidender Zugang aus dem Bergfriedbereich; vor den bodenseitigen Nachbarn liegt später Pech. |
| 8043 | (392, 467) | 193–197 | Mehrere freie Bodennachbarn, liegt aber außerhalb der entscheidenden inneren Abtrennung. |
| 3716 | (365, 510) | 198–202 | Mehrere freie Bodennachbarn, ebenfalls außerhalb der entscheidenden inneren Abtrennung. |

Kritische Treppe bei 6979:

- Höhere Stufen: 6978, 6977, 6976 und 6975.
- Bodenseitige Nachbarn vor der Stufe: 6878, 6879 und 6880; alle im späten Pechstreifen enthalten.
- Weitere Nachbarn sind die höhere Stufe bei 6978 und Wände bei 6980, 7078, 7079 und 7080.
- Der Pechstreifen 6874 bis 6880 wird erst in Frame 203 gebaut.
- Dieser Streifen entspricht im Save Welt-X 423 bis 429 bei Welt-Y 479.

Treppe bei 8043:

- Höhere Stufen: 8042 bis 8039.
- Freie Bodennachbarn im Blueprint: 7942, 7943, 7944 und 8044.
- Wände auf der anderen Seite: 8142 bis 8144.

Treppe bei 3716:

- Höhere Stufen: 3715 bis 3712.
- Freie Bodennachbarn im Blueprint: 3717, 3815, 3816 und 3817.
- Wände auf der anderen Seite: 3615 bis 3617.

Zusätzliche Orientierung im Bauplan: Tor mit Mapperwert 146 bei Offset 3342 in Frame 37; Tor mit Mapperwert 144 bei Offset 2343 in Frame 153. Die letzten Frames 204 und 205 setzen Mapperwert 26 an den Gruppen 4967/5066/5165/5264/5363 beziehungsweise 6669/6770/6871/6972/7073. Sie ändern nicht die Feststellung, dass der entscheidende Pechstreifen nach der Treppe entsteht.

## 10. Neuspiel gegenüber Laden: gesondert nachgestellte Abfolge

### 10.1 Warum der Unterschied möglich ist

Die Baufolge dieser Burg liefert folgende zeitliche Konstellation:

1. Treppe wird früh gebaut. Ohne Pech ist 8 -> 24 erlaubt; die Verbindung wird berechnet.
2. Pech wird viel später davor gebaut. Die Höhe sinkt auf 4, die vorherige Übergangsmaske bleibt im untersuchten Baupfad jedoch erhalten.
3. Die PCL-Neuberechnung verwendet diese alte Maske. Sie erhält damit zunächst die Verbindung.
4. Beim Laden wird die Übergangsmaske aus Höhe 4 und 24 neu berechnet. Die Verbindung entfällt.
5. Die anschließende Erreichbarkeitsprüfung findet die betroffenen Gebäude nicht mehr vom Bergfriedbereich aus erreichbar und markiert sie zum Abriss.

Es wurde nicht bewiesen, dass ausschließlich Laden eine entsprechende vollständige Neuberechnung auslösen kann oder dass die alte Verbindung in jedem Neuspiel unbegrenzt bestehen bleibt. Andere Bau- oder Aktualisierungsvorgänge können relevant sein.

### 10.2 Experiment mit tatsächlicher nativer Pechplatzierung

Auf Basis des Save-Abbilds wurde nur der entscheidende Streifen auf den Zustand vor dem Pechbau zurückgesetzt: Höhe 8 und Pechflag entfernt für Welt-X 423 bis 429 bei Y=479. Die übrige Burg blieb im rekonstruierten Save-Zustand.

Dann wurden Übergänge, PCL und Verbindungsdaten berechnet. Anschließend wurde für alle sieben Felder die originale Pechplatzierung 0x59380 aufgerufen, gefolgt von der Umgebungsmarkierung 0xE3B90, dem Setzen des Dirty-Felds und 0xD95A0. Die native Platzierung lieferte sieben erfolgreiche Pechdatensatz-IDs.

Dies ist eine gezielte Rekonstruktion des Unterschieds vor/nach dem Pechbau, keine Simulation aller 205 Bauframes.

| Messgröße | Vor Pech | Nach Pech und PCL-Neuberechnung | Nach vollständiger Übergangsneuberechnung wie beim Laden |
|---|---|---|---|
| Höhe (428, 479) | 8 | 4 | 4 |
| Übergangsmaske (428, 479) | 0x7D | 0x7D | 0x7C |
| Übergangsmaske (428, 478) | 0x78 | 0x78 | 0x40 |
| Erreichbarkeit IDs 128/199/212/213/214 | jeweils 1 | jeweils 1 | jeweils 2 |
| Normale Wege aus Abschnitt 7.2 | 1 / 1 / 1 | 1 / 1 / 1 | 0 / 0 / 0 |
| Neu zum Abriss markiert | nicht separat gemessen | 0 | 25 |

Unmittelbar nach den sieben tatsächlichen Pechplatzierungen waren über das gesamte betrachtete Übergangsmaskenarray **0 Einträge verändert**. Erst der zusätzliche vollständige Durchlauf entfernte die entscheidenden Verbindungen.

Vor den beiden Abrissprüfläufen wurden die ursprünglichen Gebäuderecords wiederhergestellt, damit eine frühere Zustandsänderung die Zählung nicht verfälscht.

Die 25 IDs nach vollständiger Neuberechnung stimmen mit Abschnitt 6.3 überein.

## 11. Korrekturen früherer Erklärungen

1. **„Pech erhöht Terrain“:** Für die gespeicherte Feldhöhe trifft das Gegenteil zu; sie wird um 4 gesenkt.
2. **„Die Höhendifferenz allein macht die Stelle jederzeit unpassierbar“:** Zu pauschal. Vorhandene Übergänge können nach dem Pechbau weiter gültig gespeichert sein. Entscheidend ist, wann welche Wegdaten neu berechnet wurden.
3. **„Der Abriss ist nur eine fehlerhafte direkte Gebäudereichbarkeitsprüfung“:** Der isolierte Nachweis zeigt bereits eine Trennung im zugrunde liegenden Wegenetz. Auch die normale Wegsuche scheitert nach vollständiger Neuberechnung.
4. **„Manuell andere Treppenhöhen erklären alle fehlenden Reproduktionen“:** Das erklärt geeignete manuelle Beispiele, nicht automatisch AIV-Burgen. Bei der ersten Test-AIV kommen alternative Zugänge hinzu, bei der zweiten insbesondere die Baufolge und die unterschiedliche Neuberechnung.
5. **„Ein gleicher fertiger Bauplan müsste dasselbe Ergebnis liefern“:** Unvollständig. Vorbereitete Bauschritte können übereinstimmen, während gespeicherte beziehungsweise neu berechnete Übergänge voneinander abweichen.
6. **„25 Gebäude wurden im Offline-Test abgerissen“:** Gemessen wurde ihre neue Markierung mit Zustand 3. Der terminale Abriss im laufenden Spiel wurde dort nicht ausgeführt.

## 12. Vorläufige Folgerungen für einen Fix

Es wurde kein Fix implementiert oder installiert.

- Nur die Abrissmarkierung zu unterdrücken kann die falsche Trennung des Wegenetzes bestehen lassen. Dann würden weitere Systeme weiterhin unterschiedliche oder falsche Erreichbarkeit sehen.
- Nur zusätzliche Übergangsneuberechnungen nach Pechbau auszulösen würde den vorhandenen Höhenkonflikt auch im Neuspiel unmittelbar wirksam machen. Das allein ist keine Verhaltenskorrektur.
- Die Gegenprobe bestätigt für diesen Save den Nutzen einer korrigierten logischen Höhe während der Übergangsberechnung. Anschließend konnten die ursprüngliche gespeicherte Pechhöhe und das Pechflag wiederhergestellt sein, ohne die untersuchten Erreichbarkeitsprüfungen scheitern zu lassen.
- Daraus folgt noch kein fertig spezifizierter allgemeiner Patch. Vor einer Umsetzung müssen die relevanten Höhenleser, Erzeuger, lokalen und vollständigen Neuberechnungen sowie mögliche Sonderfälle konsistent bewertet werden.
- Eine globale Änderung der gespeicherten Pechhöhe wurde nicht umfassend auf Darstellung, Brand-/Pechlogik, Bewegung und weitere Terraininteraktionen geprüft. Sie ist deshalb nicht als fertige Lösung belegt.
- Die frühere Frage, ob lediglich „drei gefundene Stellen“ geändert werden sollten, ist mit einer bloßen Anzahl von Fundstellen nicht beantwortet. Der Nachweis reicht inzwischen über direkte Gebäudeprüfungen hinaus bis zur Erzeugung und Aktualisierung der Übergangsdaten. Die minimal vollständige Patchmenge bleibt offen.

## 13. Offene Fragen und sinnvolle nächste Nachweise

- Der tatsächliche interne Wegzustand des zuletzt genannten funktionierenden Neuspiels wurde nicht erfasst. Die nachgestellte Aktualisierungslücke ist eine konkrete Erklärung, aber noch kein direkter Zustandsvergleich genau dieser beiden laufenden Partien.
- Die berichtete erfolgreiche Einheitenbewegung auch nach dem Laden ist weiterhin nicht vollständig erklärt. Der isolierte vollständige Neuaufbau sperrte beide Richtungen der konkreten Vergleichsstelle. Für die Live-Beobachtung fehlen unter anderem die exakte Stelle, Einheit, vorhandene Route und der Wegzustand zum Bewegungszeitpunkt.
- Die Versuche enthalten nicht alle Systeme und Daten des laufenden Spiels. Die konkreten PCL-Nummern und die Auswahl der 25 Gebäude gelten für den beschriebenen rekonstruierten Zustand.
- Alternative Zugänge der ersten Testburg wurden aus dem Blueprint abgeleitet. Ihre tatsächliche Nutzung und zeitliche Verfügbarkeit wurden nicht live protokolliert.
- Weitere Auslöser einer vollständigen oder lokalen Übergangsneuberechnung und deren zeitliche Wirkung sind für eine endgültige Fixentscheidung noch zu berücksichtigen.
- Ein späterer Live-Vergleich sollte bei unverändertem Grundriss vor und nach dem Laden Feldhöhen, Übergangsmasken, PCL, Verbindungsdaten, Gebäudereichbarkeit und tatsächliche Bewegung gemeinsam erfassen.
- Jeder zukünftige native Patch benötigt erneut den Hashabgleich und die üblichen Hook-/Kompatibilitätsprüfungen. Die hier dokumentierten RVAs sind Analysebefunde, keine Freigabe zur ungeprüften Anwendung auf eine andere DLL.

## 14. Dokumentationsumfang

Diese Datei sichert die bisherigen Beobachtungen, Codebefunde, Dateiidentitäten, Gegenproben und Korrekturen. Die Erstellung und Ergänzung dieser Datei umfasst keine Codeänderung, keinen Build, keine Änderung an den übergebenen Dateien und keine Veröffentlichung. Der folgende Fixplan beschreibt zukünftige Arbeit.

## 15. Nachtrag: bestätigte Reproduktion und weiterer Nutzerbericht

### 15.1 Bestätigung durch den Nutzer

Der Nutzer hat nach der ursprünglichen Dokumentation ausdrücklich bestätigt:

- Beim Neuspiel mit der betreffenden Burg tritt das Problem zunächst nicht auf.
- Wird unmittelbar gespeichert und dieser Stand als Savegame geladen, werden die Gebäude abgerissen.

Damit ist die Verhaltensdifferenz Neuspiel -> Speichern -> Laden jetzt vom Nutzer reproduziert. Die frühere Einschränkung, dass der konkrete interne Übergangsmaskenzustand dieses Neuspiels nicht direkt erfasst wurde, bleibt bestehen. Sie ist nicht mit fehlender Bestätigung des sichtbaren Verhaltens gleichzusetzen.

### 15.2 Bericht über Auftreten ohne Save-Laden

Ein weiterer Nutzer berichtet, dass der Fehler auch ohne Laden eines Savegames auftreten könne. Der Auftraggeber konnte diesen Ablauf bislang nicht selbst reproduzieren.

Einordnung: Dies widerspricht dem bisher nachgewiesenen Mechanismus nicht. 0xD86F0 wird sowohl vom vollständigen Neuaufbau 0xDAA50 als auch vom lokalen Aktualisierungspfad 0xD90D0 aufgerufen. Eine passende lokale Neuberechnung oder ein weiterer vollständiger Neuaufbau während der Partie kann daher grundsätzlich denselben Höhenkonflikt sichtbar machen. Welches konkrete Ereignis den zusätzlichen Bericht ausgelöst hat, ist nicht nachgewiesen.

Der Fix darf deshalb nicht an IsSave, einen Lade-Callback oder eine einmalige Nach-Laden-Reparatur gebunden werden. Er muss die gemeinsame Übergangsberechnung unabhängig vom Aufrufanlass korrigieren.

### 15.3 Quelldatei inzwischen ersetzt

Bei der weiterführenden Planprüfung hatte die Datei am ursprünglichen Save-Pfad einen anderen Inhalt:

| Eigenschaft | Ursprünglich analysierter Stand | Bei der Planprüfung vorgefundener Stand |
|---|---|---|
| SHA-256 | 38c6ac5e6b1e16d1d6fc8e5c8df85f304d17ab08924fe750b85f10eff78ca92f | 8a5a4b21c4aa9578fd97f05267ce5ebe22db7b81b260fae04e7ef751d95b91b5 |
| Dateigröße | 4.254.397 Bytes | 3.955.002 Bytes |

Die alten Gebäude-IDs waren im neuen Stand nicht entsprechend belegt. Eine erneut angesetzte isolierte Gegenprobe auf diesem veränderten Stand war daher kein gültiger Vergleich mit dem historischen Regressionsfall. Ihre Ergebnisse werden nicht als Fixnachweis gewertet. Die historischen Tabellen werden nicht auf die neue Datei umgeschrieben.

Vor neuen Regressionstests müssen die Eingangsidentitäten und die tatsächlich vorhandenen Gebäude geprüft werden. Der Name des Saves ist keine hinreichende Fixture-Identität. Falls der ursprüngliche Stand nicht mehr vorliegt, ist der jetzt vom Nutzer bestätigte Ablauf mit einer neuen, eindeutig identifizierten Fixture zu erfassen; dafür sind neue erwartete Gebäude und Ergebnisse festzuhalten.

## 16. Fixplan: logische Pechhöhe bei der Übergangsberechnung korrigieren

Status: historischer Kernplan, nicht implementiert. Die erweiterte Prüfung in Abschnitt 17 hat zusätzliche unabhängige Fehler nachgewiesen. Die zwei Hook-Stellen wurden statisch vorbereitet, reichen als Gesamtfix jedoch nicht aus; der Umfang ist gemäß Abschnitt 17.8 zu erweitern.

### 16.1 Ziel und Umfang

Der Fix soll die nachgewiesene pechbedingte Abtrennung verhindern, indem lokale und vollständige Wegneuberechnungen dieselbe fachlich passende Bodenhöhe verwenden. Er soll für neue Partien, geladene Spielstände und spätere Neuberechnungen während einer Partie wirken.

Vorgesehener Eigentümer ist BugfixesAndQoL. Es handelt sich um eine Korrektur des nativen Vanilla-Verhaltens, nicht um einen Fehler des Script Extenders. Kein neuer Testmod als Produktionsabhängigkeit, keine Änderungen am externen Extender-/Fixes-Quellbaum und keine Erweiterung der öffentlichen APIShared-API.

Abnahmekriterien:

- Die betroffene Pech-/Treppenverbindung ist nach vollständiger und lokaler Neuberechnung in beiden Richtungen nutzbar.
- Die Gebäude der betroffenen Burg bleiben tatsächlich bestehen, auch nach wiederholtem Speichern/Laden.
- Die reguläre KI-Erreichbarkeitsprüfung erkennt die relevanten Zugänge als erreichbar; Abriss wird nicht pauschal unterdrückt.
- Tatsächlich unerreichbare Gebäude und echte Hindernisse behalten ihre bisherige Behandlung.
- Feldhöhen, Default-Höhen, Pechflag, Pechbesitz, Brandzustand und Dateiformate werden vom Fix nicht verändert.
- Ohne aktiven Fix gelten Vanillas ursprüngliche Berechnung und Abrissentscheidung.

### 16.2 Gewählter Ansatz und verworfene Alternativen

**Kernfix ausschließlich in 0xD86F0:** Die für den Höhenvergleich gelesene Höhe eines nachweisbar durch Pech abgesenkten Feldes wird lokal im Register korrigiert. Die Berechnung erhält weiterhin Vanillas gesamte Terrain-, Gebäude-, Vegetations-, Diagonal- und Verbindungslogik.

Warum dieser Ansatz:

- Der historische Versuch in Abschnitt 7.2 hat gezeigt, dass eine korrigierte Höhe allein während der Übergangsberechnung für den konkreten Save genügt. Danach waren wieder die ursprünglichen Feldhöhen aktiv; die unveränderten Gebäudereichbarkeitsprüfungen funktionierten trotzdem.
- Der gemeinsame Erzeuger der Übergangsmasken deckt sowohl 0xDAA50 als auch 0xD90D0 ab. Der Fix hängt damit nicht vom noch ungeklärten zusätzlichen Auslöser ohne Save-Laden ab.
- Es sind keine wiederholten Kartenscans, eigenen Wegnetze oder nachträglichen Manipulationen der PCL erforderlich.
- Pech wird normalerweise nach bereits vorhandenen Wegverbindungen platziert. Wenn seine logische Bodenhöhe unverändert bleibt, muss der Fix nicht allein wegen der optischen Absenkung zusätzliche Wegneuberechnungen erzwingen.

Nicht Teil der ersten Umsetzung:

- Keine globale Erhöhung der erlaubten Stufenhöhe von 16 auf 20.
- Keine dauerhafte oder vorübergehende Mutation des globalen HeightGrid zur Laufzeit.
- Kein Entfernen/Neusetzen von Pech oder Verändern seiner AIV-Baufolge.
- Kein Umgehen der Abrissprüfung und kein Zurücksetzen von Zustand 3.
- Kein eigener Hook auf Save-Laden, PCL-Flutung oder normale Einheitenwegabfragen.
- Keine vorsorglichen Änderungen an 0xC90E0 und 0xBB3D0. Für den nachgewiesenen Fall sind diese Verbraucher nicht der notwendige Eingriffspunkt. Separate Fehler ihrer direkten Höhenprüfungen benötigen einen eigenen reproduzierbaren Befund.

Die frühere Frage nach drei zu ändernden Funktionen wird damit konkret beantwortet: Für diesen Fehler ist ein gezielter Eingriff an zwei Höhenlesestellen derselben Funktion vorgesehen. Das ist durch den bisherigen Ursachenvergleich begründet; die vollständige native Umsetzung muss die Abnahmetests noch bestehen.

### 16.3 Exakte Regel für die logische Höhe

Referenzfunktion für Tests und Maschinenstub:

    int NormalizePitchHeight(byte actualHeight, byte defaultHeight, uint flags, bool enabled)
    {
        if (!enabled || (flags & (uint)TilePropertyFlag.PitchTrap) == 0)
            return actualHeight;

        int expectedPitchHeight = Math.Max(0, (int)defaultHeight - 4);
        return actualHeight == expectedPitchHeight ? defaultHeight : actualHeight;
    }

Diese konservative Entscheidung korrigiert ausschließlich die erkennbare Vanilla-Absenkung gegenüber dem zugehörigen Default-Boden. Sie unterstellt nicht für beliebige modifizierte Felder, dass immer 4 addiert werden dürfen.

Verbindliche Details:

- Dieselbe Regel gilt für das Ausgangsfeld und den jeweils verglichenen Nachbarn.
- Bei Default 8 und realer Pechhöhe 4 ergibt sich logische Höhe 8.
- Bei Default-Höhen 0 bis 4 und abgesenkter Höhe 0 wird die echte Default-Höhe verwendet. Ein blindes +4 würde hier falsche Höhen erzeugen.
- Ohne PitchTrap bleibt der gelesene Wert unverändert; das Pechressourcen-Flag IsPitch ist ausdrücklich nicht gemeint.
- Entspricht die Höhe nicht dem erwarteten Vanilla-Abbild, bleibt dieses Feld im Vanilla-Verhalten. Keine geratenen Reparaturen von Feldern, die ein anderer Mod oder eine Kartenoperation anders verändert hat.
- Gebäudetyp-Höhenaufschläge und sämtliche übrigen Branches bleiben an ihrer bisherigen Stelle erhalten.
- Diese Formel ist eine geplante konservative Verallgemeinerung. Der historische Versuch bestätigt den Fall 4 -> 8, nicht bereits alle Grenzfälle dieser Formel.

Die symbolischen Flags sind vor der Implementierung gegen die tatsächlich verwendete Extender-Assembly zu prüfen. Die Konstante 4 ist hier der hashgebundene native Absenkungsvertrag.

### 16.4 Native Eingriffspunkte und Umsetzung

Installationszeitpunkt: einmal bei CrusaderLibrary.LibraryLoaded über die bestehende BFQoL-Hook-Infrastruktur. Eigentümer, Zustandsblock und Hookhandles bleiben bis Prozessende verwurzelt. Die Korrektur erfolgt in zwei rein nativen Inline-Stubs; kein verwalteter Callback pro Feld oder pro Nachbar.

Für DLL-SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2 wurden folgende Stellen vorbereitet:

| Rolle | RVA | Verdrängte Instruktionen | Spanne | Fortsetzung |
|---|---|---|---:|---|
| Ausgangsfeld | 0xD8741 | movzx esi, byte ptr [r8+r10+0x4DDD350]; danach mov ebx, dword ptr [r10+r8*4+0x48F71B0] | 17 Bytes, Längen 9+8 | 0xD8752 |
| Nachbarfeld | 0xD8AB0 | movzx eax, byte ptr [r14+r10+0x4DDD350]; cmp eax, [rsp+0x48]; jl 0xD8ACD | 15 Bytes, Längen 9+4+2 | 0xD8ABF |

Datenquellen sind HeightGrid bei RVA 0x4DDD350, DefaultHeightGrid bei 0x4E2B870 und LogicGrid bei 0x48F71B0. r10 dient an diesen Stellen als native Imagebasis; der Feldindex steht am Ausgangsfeld in r8 und beim Nachbarn in r14.

Emission je Stelle:

1. Die erste verdrängte movzx-Instruktion genau einmal ausführen.
2. Bei aktivem Fix die Normalisierungsregel anwenden; nur das vorgesehene Ergebnisregister ESI beziehungsweise EAX ändern.
3. Alle verwendeten Scratch-Register, Flags und Stacklage wiederherstellen, bevor die übrigen verdrängten Instruktionen ausgeführt werden.
4. Rest des Originalblocks genau einmal ausführen und an die native Fortsetzung zurückkehren. Beim Nachbarn muss der enthaltene jl weiterhin korrekt direkt nach 0xD8ACD verzweigen können.

Besonders wichtig: Die Vergleiche [rsp+0x48] und [rsp+0x4C] müssen dieselbe ursprüngliche Stacklage sehen. Flags für Vanillas bedingte Sprünge dürfen weder vom Guard noch von der Höhenkorrektur stammen. Für jedes Register ist der tatsächliche Liveness-Vertrag über den ganzen Block zu dokumentieren und auszuführen zu testen.

Die gewählten Längen berücksichtigen RedBirds mindestens 14 Bytes große Inline-Überschreibung. Eine Dekodierung der gesamten 1515 Byte großen Funktion ergab für beide Spannen keine direkten internen Sprung-/Callziele im Inneren. Vor Umsetzung zusätzlich externe Einsprünge, Xrefs, angrenzende Hooks, tatsächliche Backend-Spannen und Live-Bytes prüfen; der bisherige Scan ersetzt diese abschließende Installationsprüfung nicht.

Beide Stubs und ihre Aktivierung sind eine atomare Featureeinheit. Ein gemeinsames natives Aktivierungswort bleibt bis zur erfolgreichen Installation beider Hooks null. Alle Register-/Layout-/Spannenverträge und beide Generatoren werden vor dem Commit validiert. Keine halbaktive Korrektur nur einer Bewegungsrichtung.

Eine Auflösung lediglich des Funktionseinstiegs reicht nicht: In UpdateToNewDLL.md sind beide Sites, Fortsetzungen, Feldquellen, Registerverträge, Originalbytes und Grenzen festzuhalten. Die erste Version darf ausschließlich den geprüften DLL-Hash mit Bytevalidierung unterstützen. Bei anderem Hash bleibt das Feature inaktiv; ein Pattern allein darf die unbekannten Datenlayouts nicht freigeben.

### 16.5 Einbindung, Einstellungen und Lebensdauer

Vorgesehene interne Einheiten:

- PitchStairsAccessPolicy: kleine reine Referenzregel für Tests.
- PitchStairsAccessNativeCode: Emission und Byte-/Spanverträge der beiden nativen Stubs.
- PitchStairsAccessRuntime: Installation, NativeSiteRegistry-Eigentum und natives Aktivierungswort.

Die Umsetzung erfolgt innerhalb von BugfixesAndQoL; der neue Code orientiert sich an dessen bestehender TreeBlightDecay-Struktur für zwei permanente native Stubs. Das sind geplante Namen, keine schon vorhandenen Klassen.

Einstellung: EnablePitchStairsAccessFix, bool, Default true, [SyncHostOnly]. Effektive Aktivierung entspricht settings.EnableMod && settings.EnablePitchStairsAccessFix && nativeContractReady. Eigener Host-Schalter im bestehenden Bereich für KI-/Wegfindungsfixes; unabhängige Steuerung vom Modus der Abrissschutzfunktion.

Textvorschlag: „Pech vor Treppen: Erreichbarkeit korrigieren“. Tooltip: „Verhindert, dass Pechflächen den Zugang zu Treppen bei der Wegneuberechnung fälschlich sperren und die KI deshalb erreichbare Gebäude abreißt. Wirkt auch beim Laden von Spielständen.“

Integration in vorhandene Presets, Speicherung, Suchkatalog und Locale-Parität; keine neue Settings- oder Synchronisationsinfrastruktur. Standardempfehlung: aktiviert. Die üblichen Tooltip-/UI-Verträge des Workspace gelten.

Entscheidend für das Ladeproblem:

- Die Hooks müssen vor der nativen Lade-Normalisierung installiert sein.
- Das Aktivierungswort wird synchron bei Änderung der effektiven Hostsettings aktualisiert, nicht erst beim fertigen Missionsstart.
- Im geprüften APIShared-Quellstand ruft MissionLifecycleCapability.Execute für LoadedSave vor original() SavegameModSettings.ApplyBeforeLoad auf. Dessen vorbereitete Presetwerte werden über die regulären Setter angewendet. Diesen vorhandenen Weg nutzen und gezielt testen.
- Ein alter Save ohne die neue Property erhält den vorgesehenen Default true aus dem Defaults-Snapshot. Eine gespeicherte globale Deaktivierung des Mods wird weiterhin respektiert; keine erzwungene Aktivierung fremder/gesperrter Missionssettings.
- Sowohl Laden mit gespeicherten als auch Laden mit aktuellen Einstellungen muss das richtige Aktivierungswort bereits beim ersten 0xD86F0-Aufruf besitzen.
- In MP gilt auf allen Peers derselbe vorbereitete Hostwert. Kein „nur Host“-Hook und kein Abhängen von lokalem Spieler, Pechbesitzer oder UI-Zustand.
- Neue Partie, Save-Laden und Editor werden unterstützt; BFQoL-Modusfreigaben bleiben maßgeblich.

Es ist kein eigener Tick-, Frame-, Terrain- oder Lade-Poller vorgesehen. Für den Kernfix werden auch die vorhandenen OnBuildPitchDitch-/OnRemovePitchDitch-Events nicht benötigt: Er verändert die Entscheidung dort, wo Vanilla ohnehin Übergänge berechnet.

Veröffentlichte Hooks werden bei Einstellungen, Startup-Cleanup, Kartenwechsel oder Fehlern nie zurückgepatcht oder freigegeben. Ausschließlich das logische Aktivierungswort ändert sich. Kein Live-Umschalten während einer laufenden Partie und keine nachträgliche Full-Map-Reparatur als Teil des Einstellungswechsels.

### 16.6 Kompatibilität und Fehlerverhalten

Vor der Umsetzung erneut den aktuellen Arbeitsstand prüfen; die folgenden Aussagen sind eine Quellstandbeobachtung vom 01.11.2026:

- Der Extender besitzt Hooks für Pechbau/-entfernung und lokale Pathfinding-Aktualisierung. Der Fix soll diese Einstiegspunkte nicht zusätzlich belegen.
- BFQoLs UnitCommands besitzt bereits unter anderem Observer bei 0xD90D0 und 0xDAA50. Diese Aufrufketten und deren Cacheinvalidierung bleiben erhalten.
- Die Datei NativeDetourContracts.cs enthält auch einen Vertrag für 0xD86F0. Ein vorhandener Vertragseintrag allein beweist keinen installierten Hook. Die tatsächlichen Registrierungen und Live-Spannen vor dem Commit ermitteln.
- AIEconomyProtectionHook beeinflusst die Abrissentscheidung nach der Erreichbarkeitsprüfung. Die Abnahme muss mit deaktiviertem Abrissschutz erfolgen, damit ein Symptomschutz den Höhenfehler nicht verdeckt.
- In der gezielten Suche im kanonischen externen Fixes-Klon wurde kein entsprechender Pech-/Treppenfix gefunden. Das ersetzt keine erneute Spannenprüfung gegen den tatsächlich geladenen Stand.

NativeSiteRegistry für beide exklusiven Spannen verwenden. Geprüfte unveränderte Image-Bytes und Live-Bytes getrennt validieren. Bei Kollision oder unerwartetem Layout keine alternative globale Höhenmanipulation ausführen.

Vor Veröffentlichung fehlgeschlagene Kandidaten dürfen vollständig zurückgerollt werden. Nach Veröffentlichung bleiben die Hooks dauerhaft bestehen und liefern bei deaktiviertem Feature den Vanilla-Pfad. Fehlerprotokollierung nur außerhalb der Feldschleife, begrenzt und mit Millisekundenzeitstempel.

Kein zusätzlicher RNG-Aufruf, keine pro Feld erzeugten Listen/Objekte und kein managed/native Übergang in den beiden Stubs. Das Feature ändert deterministische Gameplay-Ergebnisse und ist auf allen MP-Peers gleich zu installieren und einzustellen.

### 16.7 Verbindlicher Prüfplan

#### A. Referenzregel und nativer Stub

- Kein PitchTrap: verschiedene Höhen, Default-Abweichungen und das reine Pechressourcen-Flag müssen unverändert bleiben.
- Vanilla-Pech: Default 8 / real 4 sowie weitere Geländeebenen mit jeweils 4 abgesenkter Höhe korrigieren.
- Untere Grenze: Default 0, 1, 2, 3 und 4 bei real 0 korrekt unterscheiden; keine pauschale Addition von 4.
- Obere Grenze: Default 255 / real 251; keine Byteüberläufe.
- Nicht passendes Höhenabbild: reale Höhe unverändert lassen.
- Echter Höhenunterschied oberhalb 16 nach der Normalisierung bleibt gesperrt; keine pauschale Erweiterung des Höhenfensters.
- Beide Maschinenstubs ausführen, mit dem tatsächlich für die Runtime verwendeten Generator/Backend: deaktivierter Guard, kein Pech, passende und unpassende Pechhöhe, beide Ausgänge des verdrängten jl, Fortsetzung bei 0xD8ABF.
- Sentinelwerte für alle relevanten Register, Flags, Stacklage und gegebenenfalls SIMD-Zustand prüfen. Insbesondere ESI/EAX korrekt ändern, alle anderen lebenden Werte erhalten.
- Tatsächliche DisplacedByteCount 17 beziehungsweise 15 und alle Fortsetzungen prüfen. Die erzeugten Bytes nur zu disassemblieren genügt nicht.

#### B. Native Regression und Konsistenz

- Historischen Save nur bei passendem SHA-256 und vorhandenen Records als Fixture verwenden. Andernfalls neuen bestätigten Reproduktionsstand mit eigener Identität und eigener Gebäudeliste erfassen.
- Aktivierter Fix: lokale Neuberechnung vorwärts/rückwärts und vollständiger Durchlauf müssen dieselbe pechbezogene Passierbarkeit ergeben.
- Originale gespeicherte Feldhöhen, Default-Höhen und Logikflags vor/nach dem tatsächlichen Stub-Test vergleichen; kein heimlicher HeightGrid-Schreibzugriff.
- Wenn die historische Fixture vorliegt: IDs 128/199/212/213/214 mit Ergebnis 1, alle drei Wege aus Abschnitt 7.2 erfolgreich, keine der historischen 25 neuen Abrissmarkierungen. Bei neuer Fixture entsprechende tatsächliche IDs und erwartete Zugänge verwenden.
- Deaktivierter Fix: ursprüngliches Verhalten des jeweiligen Reproduktionsfalls wiederherstellen.
- Gleiches Gelände ohne Pech: Masken und relevante Seiteneffekte gegenüber unverändertem Vanilla identisch.
- Mauern, Tore, Zugbrücken, Lagerkanten, diagonale Ecken und echte Geländeabsätze als Negativ-/Kompatibilitätsfälle prüfen. PCL-Nummern nicht als stabile erwartete Identitäten verwenden; Konnektivität und Ergebnisse vergleichen.
- Auch PitchTrap vor bereits vorhandener Treppe, Treppe nach Pech sowie Pechentfernung und späteren erneuten Bau prüfen. Ausschlaggebend ist die jeweils tatsächliche Vanilla-Erzeugung der Höhen und Flags.

#### C. Abnahme im Spiel

- Bestätigten Ablauf mit Castillo de Burgalimar Pitch Test.aivjson ausführen: Neuspiel -> Burg fertig -> unmittelbar speichern -> laden. Danach lange genug laufen lassen, dass die Vanilla-Abrissprüfung tatsächlich ausgeführt wurde; Gebäudezustand und Fortbestand kontrollieren.
- Mehrere Speicher-/Ladezyklen und Neustart des Spielprozesses.
- Jeweils gespeicherte Einstellungen, aktuelle Einstellungen und alter Save ohne neue Property; Aktivierung vor dem ersten nativen Neuaufbau nachweisen.
- Ohne Save-Laden eine gezielte lokale Neuberechnung an derselben Stelle im Entwicklungstest auslösen und deren tatsächlichen Aufruf bestätigen. Zusätzlich normalen Weiterbau/Terrainänderungen beobachten. Damit wird der gemeinsame Berechnungspfad getestet; der konkrete zusätzliche Nutzerbericht gilt erst mit dessen reproduziertem Auslöser als aufgeklärt.
- Einheitenbewegung in beiden Richtungen mit frisch erteilten Befehlen vor/nach der Neuberechnung prüfen. Vorhandene Routen nicht mit neu berechneten verwechseln.
- Beide übergebenen Testburgen sowie ein Aufbau ohne Pech und ein tatsächlich abgetrennter Aufbau.
- Pech anzünden, abbrennen/entfernen und neu legen; Darstellung und Funktionsfähigkeit kontrollieren. Manuell gebaute Treppen und Editor einbeziehen.
- BFQoL allein mit seinen harten Abhängigkeiten; zusätzlich externer Fixes-Mod und relevante UnitCommands-/MoatMove-Kombinationen. Der Fix muss auch bei abgeschaltetem KI-Abrissschutz wirksam sein.
- Echter MP-Host/Client: gleiche Aktivierung und gleiche Welt-/Wegergebnisse, einschließlich geladenem Save; keine rein lokale Aktivierung. Das ist eine spätere Abnahme, noch kein erbrachter MP-Nachweis.

#### D. Aufwand und Abschluss

Mit identischer Karte den vollständigen Maskenneuaufbau und einen Fall mit vielen lokalen Updates vergleichen. Es darf kein verwalteter Callback pro Feld entstehen. Neue native Prüfkosten und eventuelle Reparaturschleifen messen; Kosten nicht allein aus FPS oder Tickrate ableiten.

Für die Implementierung die vorhandenen einschlägigen Tests erweitern und Runtime-/Hook-/Preset-/Locale-Prüfungen auf die betroffenen Quellen begrenzen. Temporäre Diagnosen ausschließlich über Shared.DevDiag und /devdiag. Vor Abnahme regulären Build und tatsächlichen Spiellauf durchführen; Installation und Versionierung richten sich dann nach dem konkreten Implementierungsauftrag.

Zum Abschluss diese Datei mit Umsetzung, nativen Maschinennachweisen, tatsächlich verwendeten Fixture-Hashes und Ergebnissen aktualisieren. Historische Nachweise erhalten. Die ausreichend gesicherten Vanilla-Erkenntnisse bei der Umsetzung zusätzlich in die Native-Baseline übernehmen; keine Moddetails als Vanilla-Vertrag ausgeben.

### 16.8 Reihenfolge der Umsetzung

1. Neue Policy, beiden NativeCode-Stubs und ausführbare Maschinentests anlegen; Spannen-/Layout-/Registerverträge abschließend auditieren.
2. Nativen Test mit gültiger Regression-Fixture bestehen lassen, bevor Runtime-Integration oder UI als fertig gelten.
3. Permanente BFQoL-Installation und frühe Aktivierung über vorhandene Hostsettings integrieren; atomare Fehlerpfade und Überlappungsschutz prüfen.
4. Einstellung, Presets, Suchkatalog und Locales ergänzen; gespeicherte/aktuelle Settings und fehlende Property testen.
5. Regulärer Build, Spielabnahme einschließlich Speichern/Laden und lokaler Neuberechnung; danach Kompatibilitäts-, MP- und Aufwandstests.
6. Erst nach bestandener Abnahme als behoben kennzeichnen. Dieser Plan allein ist kein Fix- oder Freigabenachweis.

## 17. Erweiterte Vollständigkeitsprüfung: der Kernplan allein reicht nicht

Stand: 01.11.2026, nach erneuter Prüfung auf Bitte des Nutzers. Dieser Abschnitt erweitert und korrigiert den engeren Plan aus Abschnitt 16. Es wurde weiterhin kein Produktionsfix implementiert.

### 17.1 Ergebnisübersicht

| Teilproblem | Nachweis | Durch die zwei Hooks aus Abschnitt 16 allein behoben? |
|---|---|---|
| Pech vor normal erzeugter AIV-Treppe trennt bei der Maskenneuberechnung das Wegenetz | Historischer Savevergleich, native Gegenproben und bestätigte Nutzerreproduktion | Für den nachgewiesenen Maskenfehler vorgesehen; Implementierungsabnahme steht aus. |
| Andere native Wegsuche verwirft dieselbe Verbindung trotz bereits korrekter Masken | Isolierter Vergleich von 0xDA590 und 0xDAFD0 | Nein. |
| AIV-Treppe überbaut Pech auf demselben Feld und entsteht um 4 zu hoch | Originaler nativer Vorbereitungs-/Erzeugungspfad isoliert ausgeführt | Nein; danach ist das Pechflag weg. |
| Manuelle Treppe überbaut Pech auf einem einzelnen Stufenfeld und erhält dort ebenfalls +4 zu viel | Vollständiger manueller Baupfad isoliert ausgeführt | Nein; danach ist das Pechflag weg. |
| Direkte Gebäudeprüfung verwirft einen tiefer liegenden Pechzugang trotz gleicher PCL | Synthetischer Grenzfall mit 0xC90E0 und 0xBB3D0 | Nein; natürlicher Spielaufbau für genau diesen Grenzfall noch offen. |
| BFQoLs verwaltete Moat-Wegprüfung wiederholt die rohe Höhenprüfung | Quellprüfung des tatsächlichen Workspace-Codes | Nein; bei der Integration mitzuziehen. |

Eine Aussage „damit sind alle Probleme mit Pech und Treppen gelöst“ ist somit derzeit nicht haltbar. Der ursprüngliche Kernfix bleibt sinnvoll, ist aber als vollständiger Fix zu eng.

### 17.2 Reproduzierbarer nativer Nachweis

Analysewerkzeug:

[PitchStairsAudit.py](<D:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/_inspect/PitchStairsAudit.py>)

Aufruf aus dem Workspace:

    & 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' '_inspect/PitchStairsAudit.py'

Das Werkzeug prüft vor Ausführung den vollständigen DLL-Hash aus Abschnitt 3. Es baut ein privates PE-Abbild mit Relokationen auf, rekonstruiert die Kartenindizierung und initialisiert die für die Versuche benötigten Feld-, Spieler- und Gebäudedaten explizit. Es liest keinen Save und hängt deshalb nicht vom inzwischen ersetzten test2_pitchstrais.sav ab.

Die Erwartungen sind absichtlich an die unveränderten Vanilla-Bytes dieses Hashs gebunden: Das Werkzeug weist vorhandene Fehler und Konsistenzlücken nach. Es ist noch kein Test der zukünftigen Produktionsstubs. Es verändert weder die installierte DLL noch einen laufenden Spielprozess oder eine Eingangsdatei.

### 17.3 Zweite native Wegsuche: 0xDAFD0

0xDA590 kann die korrigierten Richtungsbits verwenden. 0xDAFD0 liest zusätzlich Ausgangs- und Nachbarhöhen aus HeightGrid und prüft auch bei einem gesetzten Richtungsbit erneut ein Höhenfenster von 16. Für besondere Gebäude enthält der Pfad weitere bestehende Höhenkorrekturen; diese sind nicht durch eine pauschale neue Toleranz zu ersetzen.

Versuch: Zwei-Feld-Korridor, Boden bei (400, 400), Treppenfuß bei (400, 399). Übrige Felder blockiert. Die Masken werden mit logischer Bodenhöhe 8 berechnet. Im Pechfall wird danach die reale Bodenhöhe wieder auf 4 gesetzt; Treppenhöhe bleibt 24. Die beiden Masken bleiben 0x01 und 0x10.

| Zustand | 0xDA590 vorwärts / rückwärts | 0xDAFD0 vorwärts / rückwärts |
|---|---|---|
| Ohne Pech, Höhen 8 und 24 | 1 / 1 | 1 / 1 |
| Pech, Höhen 4 und 24, trotzdem korrekte Masken | 1 / 1 | 0 / 0 |

Damit ist die Beschränkung auf 0xD86F0 durch ein natives Gegenbeispiel widerlegt. Die frühere pauschale Darstellung in Abschnitt 5.4, vorhandene Masken allein erklärten sämtliche untersuchten normalen Wegpfade, war unvollständig.

0xDAFD0 wird tatsächlich aus 0xF4930 sowie aus dem Gruppenbewegungspfad 0x11B520 aufgerufen. Ob dieser Pfad gewählt wird, hängt vom jeweiligen Bewegungsmodus und dessen Fallbacks ab. Das Ergebnis bedeutet nicht, dass jede Einheit immer scheitern muss.

0xE1640 als nachgeschaltete Wegrekonstruktion wurde zusätzlich geprüft. Dort besitzen die Modi unterschiedliche Regeln. Ein vorhandenes Richtungsbit wird im betroffenen normalen Stufenfall vor den eigenen Höhen-Fallbacks akzeptiert; die zusätzlichen Höhenzweige betreffen insbesondere den Modus-2-Fallback ohne Richtungsbit. Nach einer Erweiterung von 0xDAFD0 müssen Suche und Rekonstruktion gemeinsam geprüft werden. Ein pauschaler Austausch aller Höhenleser in 0xE1640 ist aus diesem Test allein nicht abgeleitet.

### 17.4 AIV-Treppe auf vorhandenem Pech

Der relevante Pfad lautet:

    0x51790 -> 0x5CD90 -> 0x6CB20
    0x6CB20 -> 0x77CF0 -> 0xEE640 / 0xEB9A0

Die vorbereitenden Prüfungen entfernen das Pechflag hier nicht. In 0x6CB20 geschieht danach:

    HeightGrid[tile] = DefaultHeightGrid[tile]
    HeightGrid[tile] += Aufschlag der AIV-Stufe
    falls vorher PitchTrap gesetzt:
        PitchTrap löschen
        HeightGrid[tile] += 4

Die letzte Addition ist falsch, weil die Höhe bereits aus DefaultHeight neu gebildet wurde. Es gibt in diesem Wert keine vorherige Pechabsenkung mehr zurückzunehmen.

Isolierter Aufbau: Default-Höhe 8, unterste AIV-Stufe 185, benachbarte höhere Stufe 184 auf Höhe 40 und Boden auf Höhe 8.

| Untergrund des neuen Treppenfußes | Ergebnis Höhe | Flags danach | Übergang zum Boden in beiden Richtungen |
|---|---:|---|---|
| Freier Boden | 24 | 0x8900, kein PitchTrap | erlaubt |
| Zuvor nativ platziertes Pech auf demselben Feld | 28 | 0x8900, kein PitchTrap | gesperrt |

Der Fix aus Abschnitt 16 kann dieses Feld nicht erkennen: Die falsche Höhe gehört jetzt zur Treppe, und das Pechflag wurde bereits gelöscht. Für andere von 0x6CB20 frisch aus DefaultHeight erzeugte Wand-/Stufenvarianten existiert derselbe gemeinsame +4-Zweig; experimentell geprüft wurde hier der unterste Treppenfuß.

Der native Pech-Abbau 0x62EB0 setzt eine Feldhöhe bei vorhandener Wand nicht einfach auf DefaultHeight zurück. Die spätere Bereinigung eines verwaisten Pechdatensatzes ist daher keine allgemeine Reparatur dieser falschen Stufenhöhe.

Beide übergebenen AIV-Dateien wurden nochmals auf eine frühere Pechplatzierung genau auf einem späteren Stufenfeld geprüft. Solche geordneten Pitch->Stair-Überlappungen wurden in diesen Dateiständen nicht gefunden. Dieser zweite Fehler erklärt deshalb nicht automatisch den ursprünglichen Save-Fall; er ist ein zusätzliches, getrenntes Problem.

### 17.5 Manuelle Treppe auf vorhandenem Pech

In 0x6F230 wird eine Stufenhöhe aus der ermittelten Anschlusswandhöhe gebildet. Bei positiver Differenz setzt die Rechnung das Feld bereits effektiv auf:

    Zielhöhe = fortgeschriebene Anschlusswandhöhe - 16

Der gemeinsame Abschluss entfernt anschließend das Pechflag und addiert 4. Auch hier wurde der alte abgesenkte Wert bereits durch die Zielhöhenrechnung ersetzt.

Isolierter Versuch: Anschlusswandhöhe 98, fünf manuell erzeugte Stufen. Nur unter dem ersten neuen Stufenfeld liegt gegebenenfalls Pech. Der vollständige manuelle Erzeuger einschließlich seiner Validierung wurde aufgerufen.

| Ausgangszustand | Erzeugte Höhen | Übergang erste <-> zweite Stufe |
|---|---|---|
| Ohne Pech unter einer Stufe | 82 / 66 / 50 / 34 / 18 | in beiden Richtungen erlaubt |
| Pech nur unter dem ersten Stufenfeld | 86 / 66 / 50 / 34 / 18 | in beiden Richtungen gesperrt |

Die Validierung akzeptierte beide Aufbauten. Das ist ein bestätigter nativer Baufehler, nicht nur eine rechnerische Vermutung.

Ein Fix darf den gemeinsamen +4-Abschluss von 0x6F230 nicht unterschiedslos entfernen: Andere manuelle Wandvarianten addieren ihren Aufschlag noch auf die abgesenkte Höhe und benötigen gegebenenfalls weiterhin die Wiederherstellung. Die Korrektur muss gezielt den bereits auf Zielhöhe gebrachten Treppenzweig unterscheiden.

### 17.6 Direkte Gebäudezugänge bleiben ein eigener Höhenleser

0xC90E0 prüft das gespeicherte Zugangsfeld, 0xBB3D0 sucht Ersatzfelder. Beide verwenden weiterhin rohe Feldhöhen und gegebenenfalls vorhandene Gebäudetyp-Aufschläge. Das ist unabhängig davon, ob 0xD86F0 das Wegenetz bereits korrigiert hat.

Grenzfallmodell:

- Gebäudegrund und gespeicherte Gebäudehöhe: 24.
- Tiefer liegende Zugangsfelder: Default 8; real entweder 8 oder durch Pech abgesenkt 4.
- Bergfriedreferenz und Zugangsfelder erhalten ausdrücklich dieselbe PCL 1.
- Gebäuderecord und Suchumfeld sind kontrolliert gesetzt.

Ergebnis ohne Pech: 0xC90E0 = 1 und 0xBB3D0 = 1. Mit Pech: beide 0. Der Unterschied ist die rohe Höhendifferenz 16 gegenüber 20, nicht eine PCL-Trennung.

Dies weist die unabhängige Ablehnung durch diese Funktionen nach. Es ist noch kein Nachweis, dass ein regulär erzeugtes Gebäude in einer natürlichen Partie genau diese Höhen- und Zugangskonstellation erhält. Deshalb weder als Ursache der historischen 25 Markierungen ausgeben noch als bereits im Spiel reproduzierten Zusatzbug darstellen.

Für das Ziel einer durchgehend konsistenten logischen Pechhöhe müssen diese Leser entweder in die Korrektur einbezogen oder durch entsprechende natürliche Gegen-/Negativtests ausdrücklich abgegrenzt werden. Die frühere Begründung, sie für den historischen Save nicht ändern zu müssen, bleibt richtig; daraus folgt keine allgemeine Unbedenklichkeit.

### 17.7 Zusätzliche Integration in BFQoLs Wegprüfung

[WeightedMoatRoutePlanner.cs](<D:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/BugfixesAndQoL/src/UnitCommands/Movement/WeightedMoatRoutePlanner.cs>) besitzt in HasCompatibleNativeHeight eine verwaltete Nachbildung des Höhenvergleichs:

    currentHeight = heightLayer[currentTile]
    nextHeight = heightLayer[nextTile]
    ... bestehende Gebäudekorrekturen ...
    Math.Abs(nextHeight - currentHeight) <= 16

Auch bei einer normalen Kante ohne Graben verlangt der Aufrufer neben dem Richtungsbit diesen Höhenvergleich. Eine nur native Korrektur könnte deshalb mit dieser aktiven Modprüfung widersprüchliche Antworten erzeugen.

Das ist ein belegter Integrationspunkt im Workspace, kein zusätzlicher Vanilla-Funktionsbefund. Bei der Umsetzung muss die verwaltete Prüfung dieselbe aktivierte Pechhöhenregel verwenden; bei deaktiviertem Feature bleibt sie bei ihrer bisherigen Semantik. Betroffene Caches und Tests ebenfalls einbeziehen.

### 17.8 Konsequenz für den Umsetzungsumfang

Der bisherige Zwei-Hook-Plan ist nur der erste Baustein. Für eine umfassendere Korrektur sind mindestens diese Arbeiten vorzusehen:

1. **Übergangsmasken:** 0xD86F0 wie in Abschnitt 16 korrigieren.
2. **Wegsuche:** Den zusätzlichen Höhenvergleich in 0xDAFD0 mit derselben Pechhöhenregel konsistent machen. Ausgang, Nachbar, relevante Diagonalzweige und bestehende Gebäudeausnahmen gemeinsam prüfen. Nachfolgende Rekonstruktion 0xE1640 als Konsistenzvertrag mittesten.
3. **Bauerzeugung:** Doppeltes +4 bei AIV-Erzeugung 0x6CB20 und gezielt beim manuellen Treppenzweig 0x6F230 verhindern. Hier entsteht eine falsche gespeicherte Stufenhöhe; eine Korrektur nur von Pech-Lesern reicht nicht.
4. **Direkte Gebäudezugänge:** 0xC90E0 und sämtliche relevanten Suchzweige von 0xBB3D0 anhand des dokumentierten Grenzfalls prüfen. Für ein einheitliches logisches Modell die Feldhöhenleser korrigieren; gespeicherte Gebäudeskalare und Typ-Aufschläge nicht blind ersetzen.
5. **Modintegration:** Die entsprechende BFQoL-Höhennachbildung angleichen, ihre Aktivierung und Caches testen.
6. **Bestehende falsche Treppenhöhen:** Nicht pauschal jede Stufe um 4 senken. Das fehlende Pechflag erlaubt keine sichere Herkunftserkennung. Neue falsche Erzeugung verhindern; vorhandene beschädigte Aufbauten erfordern einen separat belegten Reparaturweg, etwa anhand eines eindeutig zugeordneten AIV-Plans.

Die bislang vorbereiteten Spannen aus Abschnitt 16 gelten nur für 0xD86F0. Für die zusätzlichen Stellen sind vollständige Spannen-, Register-, Branch-, Hook- und Seiteneffektaudits vor der Umsetzung erforderlich. Es ist noch kein atomar validierter Gesamt-Hookverbund vorhanden.

Neue Abnahmeszenarien sind insbesondere die beiden Überbaufolgen, der Vergleich beider nativen Wegsuchvarianten trotz korrekter Masken und das Gebäude-Grenzfallmodell. Diese Tests kommen zu den bisherigen Lade-/Nicht-Lade-, MP-, Negativ- und Kompatibilitätsprüfungen hinzu.

Die Prüfung liefert konkrete weitere Probleme und belegt die Unvollständigkeit des engeren Plans. Sie beweist nicht, dass darüber hinaus in jeder Karten-, Editor-, Einheiten- und Modkombination kein weiterer Fehler existiert.

## 18. Fixmöglichkeiten für neue Partien mit aktivem Mod

Stand: 01.11.2026. Dieser Abschnitt dokumentiert den damaligen Ansatzvergleich mit Empfehlung A und ersetzt den engeren Umsetzungsumfang aus Abschnitt 16. Die nativen Befunde in Abschnitt 17 bleiben seine Grundlage. Anschließend hat der Nutzer Variante B bevorzugt; die daraufhin ergänzten Befunde und Anforderungen stehen in Abschnitt 19. Die Empfehlung A in diesem Abschnitt ist damit ein historischer Zwischenstand, keine Festlegung gegen diese Nutzerpräferenz.

### 18.1 Verbindliche Eingrenzung des Nutzers

Es ist keine Reparatur bereits beschädigter Savegames erforderlich. Ziel sind neu gestartete Partien mit aktivem Mod. Bereits in solchen Partien erzeugte Treppen müssen richtig entstehen und anschließend richtig bewertet werden.

Zum Ziel gehört weiterhin das normale Speichern und spätere Laden einer unter aktivem Mod neu begonnenen Partie. Der Fix darf nicht nur bis zum ersten Laden funktionieren. Der Mod bleibt für diese Zusage aktiv; ein Wechsel auf Vanilla während derselben Partie ist kein Abnahmeszenario.

Damit entfallen aus dem vorgesehenen Produktumfang:

- Rekonstruktion historischer Baufolgen oder verlorener Pechflags.
- Reparaturscans nach vermeintlich um 4 erhöhten Treppen.
- AIV-basierte Wiederherstellung bereits beschädigter Aufbauten.
- Save-Migrationen, Reparaturbuttons und zusätzliche gespeicherte Herkunftsdaten.
- Ein pauschales Senken bestehender Treppenhöhen.

Der Hinweis in Abschnitt 17.8 Nummer 6 beschreibt die Informationsgrenze bei Altschäden; er ist nach dieser Nutzervorgabe keine zu implementierende Funktion.

### 18.2 Empfehlung: zwei ergänzende Korrekturen als ein konsistentes Feature

**Empfohlen ist Variante A: gemeinsame logische Pechhöhe für die relevanten Leser plus gezielte Korrektur der beiden Bauerzeuger.**

Dies sind zwei verschiedene Ursachen und deshalb zwei fachliche Korrekturen:

1. Ein korrekt abgesenktes Pechfeld darf bei der Erreichbarkeit nicht wie ein echter zusätzlicher Geländeabsatz wirken.
2. Beim Überbauen von Pech darf die bereits neu berechnete Stufenhöhe nicht noch einmal um 4 erhöht werden.

Beide Korrekturen werden gemeinsam aktiviert. Nur Leser zu ändern lässt die falsche Erzeugung bestehen; nur die Erzeuger zu ändern lässt Pech vor einer normal gebauten Treppe weiterhin bei der Neuberechnung scheitern.

| Ansatz | Bekannte Rechenfälle | Gespeicherte Pechhöhe | Hauptrisiko | Empfehlung |
|---|---|---|---|---|
| A: logische Höhe nur in relevanten Prüfungen, dazu Bauerzeugung korrigieren | Im isolierten Konzeptvergleich erfolgreich | Vanilla-Absenkung bleibt erhalten | Mehrere native Lesestellen müssen vollständig und konsistent eingebunden werden | Erste Wahl |
| B: Pech senkt die gespeicherte Höhe gar nicht mehr; sämtliche zugehörigen Wiederherstellungen anpassen | Im isolierten Konzeptvergleich ebenfalls erfolgreich | Normale Bodenhöhe | Verändert die gemeinsame Eingabe auch für Darstellung, Brand und weitere Systeme; vorplatzierte Kartenobjekte brauchen gesonderte Betrachtung | Alternative mit größerem Wirkungsbereich |
| C: eigenes Raster logischer Höhen für die Prüfungen, dazu Bauerzeugung korrigieren | Dasselbe Rechenmodell wie A; im Test als Eingabe-Proxy verwendet | Vanilla-Absenkung bleibt erhalten | Vollständige Aktualisierung und Cachekonsistenz über alle Feldänderungen | Reserve für einen nachgewiesenen Performancebedarf |

Keine dieser Varianten benötigt eine Reparatur alter Spielstände. Für die beobachteten Fehler ist eine pauschale Erweiterung des Höhenfensters auf 20 weiterhin keine geeignete Alternative.

### 18.3 Variante A: Leseregel und vollständiger fachlicher Umfang

Die konservative Regel aus Abschnitt 16.3 bleibt bestehen:

    wenn Fix aktiv
      und PitchTrap gesetzt
      und tatsächliche Höhe == max(0, Default-Höhe - 4):
          für diesen Vergleich Default-Höhe verwenden
    sonst:
          tatsächliche Höhe verwenden

Wichtig: Die Regel betrifft ein gelesenes Kartenfeld. Sie ist kein allgemeiner Ersatz für gespeicherte Gebäudehöhen, Einheitshöhen oder Gebäudetyp-Aufschläge. Die bestehende Höhenlogik wird um die Pechkorrektur ergänzt, nicht durch eine neue pauschale Begehbarkeitsregel ersetzt.

Verbindlich einbeziehen:

| Funktion | Umfang der Lesekorrektur |
|---|---|
| 0xD86F0 | Ausgangsfeld und gewöhnlicher Nachbarhöhenvergleich zur Maskenerzeugung. |
| 0xDAFD0 | Ausgang, Ziel und beide relevanten Eckfeld-Höhenleser; bestehende Gebäudekorrekturen und Sonderkanten erhalten. |
| 0xE1640 | Die vier Feldhöhenleser im Modus-2-Fallback mit derselben Regel behandeln, damit dessen Höhenentscheidung zur Suchentscheidung passt. Maskenabkürzungen bleiben erhalten. |
| 0xC90E0 | Die beiden Feldhöhenleser für Gebäudegrund und gespeicherten Zugang; Typ-Aufschläge danach wie bisher. |
| 0xBB3D0 | Alle drei Kandidaten-Höhenleser der verschiedenen Suchdurchläufe. Gespeicherte Gebäudehöhe bleibt ein Gebäudeskalar. |
| BFQoL HasCompatibleNativeHeight | Dieselbe aktivierte Regel für die betreffenden Feldhöhen; zusätzlich die Eckprüfungen auf Verwendung desselben Modells kontrollieren. |

Beim maschinellen Durchgang wurden für diesen DLL-Hash folgende 15 Kartenhöhen-Lesestellen ermittelt:

| Funktion | RVAs |
|---|---|
| 0xD86F0 | 0xD8741, 0xD8AB0 |
| 0xDAFD0 | 0xDB1FE, 0xDB2FE, 0xDB391, 0xDB424 |
| 0xC90E0 | 0xC9171, 0xC91BD |
| 0xBB3D0 | 0xBB545, 0xBB74C, 0xBB942 |
| 0xE1640 | 0xE1874, 0xE1880, 0xE18F7, 0xE1970 |

Dies sind **15 Lesestellen, nicht 15 unabhängig installierbare Hooks**. Insbesondere liegen 0xE1874 und 0xE1880 so dicht beieinander, dass zwei gewöhnliche Inline-Hooks überlappen würden. Sie müssen in einem gemeinsam auditierten Ersatzblock behandelt werden. Pro Gruppe vollständige Instruktionen, Verzweigungen, Live-Register und angrenzende fremde Hooks prüfen; die zwei bereits vorbereiteten Spannen aus Abschnitt 16 sind keine Vorlage, die ungeprüft auf alle Stellen übertragen werden darf.

Produktive Umsetzung:

- Eine gemeinsame, getestete Emissionsroutine für die Pechhöhenregel; pro Site nur der geprüfte Adapter für Feldindex, Zielregister und erhaltenen Kontext.
- Reine native Berechnung im Stub, ohne verwalteten Callback pro Feld, ohne Allokationen und ohne RNG.
- Ein gemeinsamer Aktivierungszustand für alle Leser und beide Erzeugerkorrekturen.
- Im verwalteten BFQoL-Pfad dieselbe Referenzregel und derselbe effektive Aktivierungszustand.
- Keine temporäre Normalisierung des globalen HeightGrid. Darstellung und andere Leser dürfen nie eine zwischenzeitlich hochgesetzte Welt sehen.
- Keine zusätzlichen Full-Map-Neuberechnungen im Tick. Lokale und vollständige Vanilla-Aktualisierung erhalten dieselbe korrigierte Semantik, wenn sie ohnehin aufgerufen werden.

Die zusätzliche Korrektur von 0xE1640 erweitert den engeren Test aus Abschnitt 17.3: Dort genügte das Richtungsbit für den einfachen Übergang. Für die vollständige Such-/Rekonstruktionskonsistenz sollen auch die eigenen pechbezogenen Höhenvergleiche des Fallbacks übereinstimmen. Die umfassenden Sonderkanten-/Diagonaltests bleiben Voraussetzung; die vier Rohleser allein beweisen noch keinen fertig auditierten Hook.

### 18.4 Variante A: Korrektur der Erzeuger

#### AIV-Wände und -Treppen bei 0x6CB20

Bei vorhandener Pechfläche wurde die Höhe bereits aus DefaultHeight und Mapper-Aufschlag neu gebildet. Deshalb bleibt im gemeinsamen Bereinigungszweig das Entfernen des Pechflags erhalten, die anschließende erneute Addition von 4 entfällt bei aktivem Fix.

Untersuchte Additionsstelle: 0x6CCED, Originalinstruktion 8 Bytes. Dies ist eine fachliche Zielstelle, keine freigegebene 8-Byte-Hookspanne.

Diese Korrektur betrifft die von diesem Erzeuger frisch aufgebauten Wand-/Stufenvarianten. Sie darf nicht nachträglich das fertig gebaute Feld mit einer geratenen Höhe überschreiben. Die native lokale Wegaktualisierung soll bereits die richtig erzeugte Höhe lesen.

#### Manueller Treppenbau bei 0x6F230

Hier wird das zusätzliche +4 nur für den Treppenfall unterdrückt, dessen Zielhöhe bereits gebildet wurde. Die gemeinsame Wiederherstellung für gewöhnliche Wände bleibt erhalten.

Untersuchte Additionsstelle: 0x6F6B6, Originalinstruktion 9 Bytes. Am untersuchten Punkt liegt der Mapper in SI; der Treppenfall entspricht eMappers.MAPPER_STAIR. Symbol und Wert vor der Runtime-Umsetzung gegen die tatsächlich verwendete Extender-Assembly validieren.

Erwartete Beispiele:

- Treppe über einem Pechfeld: 82/66/50/34/18 statt 86/66/50/34/18.
- Gewöhnliche manuelle Wand über Pech: weiterhin Sollhöhe 98 im geprüften Default-8-Fall.
- Ohne Pech oder mit ausgeschaltetem Fix: bisheriger nativer Ablauf.

Pechflag, übrige Terrainbits, Besitzerzuordnung und native Bereinigung verwaister Pechdatensätze bleiben in den vorhandenen Pfaden. Insbesondere keine pauschale Entfernung von Pechdatensätzen während einer laufenden nativen Iteration als Nebeneffekt des Höhenfixes.

### 18.5 Isolierter Vergleich von Variante A und B

Neues Analysewerkzeug:

[PitchStairsFixCandidates.py](<D:/CDesktopLink/Unterlagen/Mods/Stronghold Crusader DE/Meine Mods/_inspect/PitchStairsFixCandidates.py>)

Aufrufe aus dem Workspace:

    & 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' '_inspect/PitchStairsFixCandidates.py' --mode logical
    & 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' '_inspect/PitchStairsFixCandidates.py' --mode producer

Beide Prozesse prüfen zuerst den bekannten DLL-Hash und verwenden getrennte private Speicherabbilder. Die installierte DLL, das laufende Spiel und Savegames werden nicht verändert.

Methodik und Grenzen:

- A wird für den Vergleich durch eine normalisierte Eingabeebene modelliert, auf die ausschließlich die 15 geprüften Höhenleser im privaten Abbild umgeleitet werden. Die reale Feldhöhe bleibt abgesenkt. Dieses Testmittel ist kein Vorschlag, für A produktiv ein Schattenraster zu führen.
- B wird im privaten Abbild durch Auslassen des Höhenstores bei 0x594CB modelliert. Pechflag und restlicher nativer Erzeugungspfad bleiben aktiv.
- Die redundante Addition im AIV-Erzeuger wird für beide Varianten im privaten Abbild ausgelassen.
- Für A wird beim manuellen Erzeuger in einem privaten Test-Gateway nur für den Treppenmapper die Addition ausgelassen; die gewöhnliche Wand behält sie. Für B entfällt die Wiederherstellung auch bei der gewöhnlichen Wand, weil dort keine Absenkung mehr existiert.
- Die nativen Funktionskörper und Vergleiche werden tatsächlich ausgeführt. Der Vergleich validiert die fachliche Wirkung dieser Änderungen auf die gesetzten Eingaben.
- Die privaten Codeänderungen sind keine produktiven RedBird-Hooks, besitzen keinen getesteten Laufzeit-Aktivierungsvertrag und sind nicht als Patchbytes zur Übernahme gedacht. Insbesondere fehlen vollständige ABI-/Flags-/Spannen-, Performance-, Live-Spiel- und MP-Abnahmen.

| Prüfpunkt | A: logische Höhe | B: gespeicherte Bodenhöhe |
|---|---|---|
| Gespeicherte Höhe nach Pechplatzierung auf Default 8 | 4 | 8 |
| Beide nativen Wegsuchen, beide Richtungen | erfolgreich | erfolgreich |
| Bodenhöhen 0/1/2/3/4/8/40/239, jeweils nächste Stufe +16 | beide Richtungen erfolgreich, Begrenzung bei null berücksichtigt | beide Richtungen erfolgreich |
| Einfache Modus-2-Wegrekonstruktion, beide Richtungen | je 1 Schritt, kein Reparatur-Neuaufbau | je 1 Schritt, kein Reparatur-Neuaufbau |
| Echter Unterschied 20 ohne Pech | weiterhin gesperrt | weiterhin gesperrt |
| AIV-Stufe über Pech | Höhe 24 | Höhe 24 |
| Manuelle Treppe mit Pech unter erster Stufe | 82/66/50/34/18 | 82/66/50/34/18 |
| Gewöhnliche manuelle Wand über Pech | Höhe 98 | Höhe 98 |
| Gebäude-Grenzfall 0xC90E0 / 0xBB3D0 | 1 / 1 | 1 / 1 |

Das bestätigt, dass beide Konzepte die untersuchten Rechenprobleme beseitigen können. Es beweist nicht, dass beide im vollständigen Spiel gleich risikoarm sind oder dass bereits ein fertiger Mod existiert.

### 18.6 Variante B: gespeicherte Pechhöhe nicht mehr absenken

Diese Alternative beseitigt die Uneinigkeit an einer früheren Stelle: HeightGrid enthält auch auf Pech die normale Bodenhöhe. Dann sehen die vorhandenen nativen Leser und BFQoLs entsprechende Nachbildungen grundsätzlich denselben nicht abgesenkten Wert.

Notwendige Änderungen:

1. Bei 0x59380 die Absenkung nicht mehr in HeightGrid schreiben; übrige Pecherzeugung einschließlich Flags, Besitzer, Zustand und RNG beibehalten.
2. In 0x6CB20 die zusätzliche Wiederherstellung entfernen.
3. In 0x6F230 die Wiederherstellung für alle einschlägigen Pechüberbauzweige entfernen, einschließlich gewöhnlicher Wände. Die Einschränkung auf Treppen aus Variante A wäre hier falsch.
4. Weitere native Erzeuger/Bereinigungen auf Annahmen über abgesenkte Pechhöhe prüfen.
5. Vorplatzierte Pechfelder aus einer geladenen Ausgangskarte berücksichtigen: Ein neues Spiel kann Kartendaten übernehmen, ohne für jedes vorhandene Pechfeld den Erzeuger 0x59380 aufzurufen. Dafür ist ein nachgewiesener früher Kartenpfad nötig. Das ist eine Neue-Partie-Frage, keine Reparatur alter Savegames.
6. Darstellung, Einheiten-/Effekthöhen, Brandweitergabe, Editor und alle direkten Höhenverbraucher auf die geänderte gemeinsame Eingabe prüfen. Falls die optische Absenkung erhalten werden soll, ist sie in der Darstellung gesondert nachzubilden.

Vorteil: Weniger Sonderbehandlung in unterschiedlichen Weg- und Gebäudeprüfungen; auch unbekannte direkte Leser erhalten automatisch die normale Bodenhöhe.

Nachteil: Die Wirkung ist wesentlich breiter als der nachgewiesene Erreichbarkeitsfehler. Der private Vergleich prüft weder Darstellung noch alle Brand-/Terrainpfade. Ein einfaches „die vier Höhenpunkte sind unwichtig“ wäre keine ausreichende Begründung.

Damals vorläufige Bewertung: Technisch plausibel und im Rechenvergleich erfolgreich, wegen des breiteren Wirkungsbereichs zunächst zweite Wahl. Nach der anschließenden Nutzerpräferenz für B wurden die offenen Auswirkungen gezielt untersucht; bestätigte Zusatzanforderungen und verbleibende Prüfungen stehen in Abschnitt 19. Der Rechenvergleich aus Abschnitt 18.5 allein ist keine vollständige Abnahme von B.

### 18.7 Variante C: eigenes Raster logischer Höhen

Ein abgeleitetes Raster könnte die gleiche Regel wie A einmal pro geändertem Feld berechnen. Native Weg-/Gebäudeprüfungen und BFQoLs Nachbildung lesen diese Ebene, während die übrige Welt HeightGrid weiter verwendet. Die beiden Erzeuger müssen trotzdem korrigiert werden.

Dies spart wiederholte Normalisierungsarbeit in heißen Prüfpfaden, fügt aber ein dauerhaft konsistent zu haltendes Datenmodell hinzu:

- Initialisierung vor dem ersten benötigten Neuaufbau.
- Aktualisierung bei Pechbau/-entfernung, Treppen-/Wandbau, Terrain- und Default-Höhenänderungen.
- Kein veraltetes Raster zwischen Höhenänderung und der innerhalb desselben nativen Bauaufrufs erfolgenden Wegneuberechnung.
- Vollständiger Neuaufbau beziehungsweise Aktualisierung passend zu Vanillas lokalen und vollständigen Pfaden.
- Richtiger Sitzungswechsel sowie konsistenter Zugriff aus den verwalteten Prüfungen.

Der Eingabe-Proxy des Versuchs zeigt das Rechenmodell, nicht die Korrektheit dieser späteren Aktualisierungskette. Ein globales Raster auf jedem Tick neu zu berechnen wäre keine sinnvolle Abkürzung.

Bewertung: Eine mögliche spätere Optimierung, wenn Messungen einen relevanten Aufwand der direkten nativen Normalisierung zeigen. Für die erste Version ist A ohne zusätzliches Raster leichter vollständig zu begründen.

### 18.8 Gemeinsame Umsetzungsvorgaben für die Empfehlung A

- Umsetzung in BugfixesAndQoL, keine Änderungen am Extender oder externen Fixes-Mod.
- Eine gemeinsame Hostoption EnablePitchStairsAccessFix, Default true; logisch an die vorhandene Host-Aktivierung gekoppelt. Keine unabhängig auseinanderlaufenden Schalter für voneinander abhängige Höhenleser.
- Alle notwendigen nativen Sites und die verwaltete Integration vor der Freigabe vollständig vorbereiten. Eine Teilinstallation darf nicht als aktiver Fix veröffentlicht werden.
- NativeSiteRegistry, unverändertes Ladeabbild, Live-Bytes, tatsächliche Backend-Spannen und alle Kontrollflussziele prüfen.
- Installierte Hooks bleiben dauerhaft; nur ein gemeinsames natives Aktivierungswort ändert sich. Deaktiviert wird Vanilla ausgeführt.
- Aktivierung muss schon vor den nativen Berechnungen beim Neuspiel und beim Laden einer unter aktivem Mod erstellten Partie stimmen. Ein Callback erst nach fertig gemeldetem Missionsstart wäre zu spät.
- Die vorhandene gemeinsame Settings-/Preset-/Save-Ladevorbereitung nutzen; keine neue Save-Erweiterung und keine eigenen Netzwerkpakete.
- Keine allein hostseitige Höhenentscheidung: Auf allen MP-Peers gilt derselbe vorbereitete Hostwert.
- Der Mod benötigt keine historische Bauzustandsspeicherung und keine eigene Reparaturphase.

### 18.9 Abnahme und Reihenfolge

1. **Lesekorrektur vollständig prototypisieren:** Alle 15 Sites und die verwalteten Feld-/Eckprüfungen mit einer Referenzregel abgleichen. Relevante Diagonal-, Graben-, Gebäude- und Negativzweige ausführen, nicht nur den Zwei-Feld-Fall.
2. **Erzeuger gezielt korrigieren:** Alle fünf AIV-Stufen, verschiedene Pechüberlappungen, manuelle Treppen und gewöhnliche Wände prüfen. Das normale Wand-Gegenbeispiel verhindert ein zu breites Entfernen des +4.
3. **Produktive Hookverträge testen:** Richtige verdrängte Blöcke, Register/Flags, ursprüngliche Stacklage, ausgehende und eingehende Sprünge, gemeinsame und benachbarte Hookspannen. Die Test-Gateways dieses Dokuments ersetzen das nicht.
4. **Neue Partie abnehmen:** Beide übergebenen Burgen und gezielte Pech-vor-/Pech-unter-Treppe-Aufbauten. Vorplatzierte Kartenpechfelder sowie Editor berücksichtigen. Prüfen, dass keine Abrissschutzoption den Fehler verdeckt.
5. **Partie speichern und laden:** Wiederholte Zyklen mit aktivem Mod; frisch erteilte Bewegungsbefehle in beiden Richtungen, weiterbestehende Gebäude und tatsächliche KI-Erreichbarkeit prüfen. Keine Reparatur alter beschädigter Saves als Voraussetzung.
6. **Kompatibilität und MP:** Relevante BFQoL-/MoatMove-Pfade, externer Fixes-Mod, echter Host/Client und identische Hostsettings prüfen.
7. **Aufwand messen:** Lokaler und vollständiger Wegneuaufbau ohne verwaltete Aufrufe pro Feld. Rastervariante C nur bei belegtem Bedarf erneut abwägen.
8. **Erst danach als gelöst kennzeichnen:** Der aktuelle Stand ist ein begründeter, in definierten nativen Rechenfällen geprüfter Lösungsvorschlag. Produktionsimplementierung und Spielabnahme stehen aus.

Die damalige Empfehlung war **A: konsistente logische Höhe und korrekt erzeugte Stufen**. Anschließend hat der Nutzer B als bevorzugten Ansatz zur weiteren Prüfung gewählt. Abschnitt 19 hält die dabei gefundenen zusätzlichen Anforderungen fest; eine Produktionsimplementierung ist weiterhin nicht erfolgt. C bleibt eine Optimierungsoption. Keine Reparatur alter beschädigter Savegames gehört zum Auftrag.

## 19. Variante B: vertiefte Prüfung von Nebenwirkungen und Zusatzanforderungen

Stand: 01.11.2026. Der Nutzer bevorzugt B wegen der geringeren Zahl von Eingriffen und hat um Prüfung dieses Ansatzes allein gebeten. Dieser Abschnitt ergänzt insbesondere 18.6. Er ist eine Analyse, kein implementierter Fix und kein vollständiger Freigabenachweis. Die Eingrenzung aus 18.1 gilt weiter: neue Partien mit aktivem Mod einschließlich späterem Speichern/Laden; keine Reparatur alter beschädigter Spielstände.

### 19.1 Quellen und Nachweisgrenzen

Die folgenden RVAs gehören zur kanonischen DLL aus Abschnitt 3.1 mit SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Installierter Hash und CURRENT.json wurden für diese Ergänzung nochmals abgeglichen. Die Funktionsbefunde stammen aus der dort bezeichneten semantischen Baseline; Funktionsabfragen sind über tools/semantic/query.ps1 mit dem Unterbefehl function und dem jeweiligen RVA nachvollziehbar.

Die neuen isolierten Aufrufe liefen wie die bisherigen Experimente in privaten Speicherabbildern separater Python-Prozesse. Installierte DLL, laufendes Spiel und Savegames wurden nicht verändert. Der vorhandene Konzeptvergleich PitchStairsFixCandidates.py bildet weiterhin nur den Umfang aus Abschnitt 18.5 ab: Er enthält die nachfolgend gefundenen Lade-, Darstellungs- und Feuerkorrekturen noch nicht. Die ergänzenden Normalisierungs-, Bewegungs- und Feuerzweigtests wurden separat im privaten Abbild ausgeführt; sie sind keine bereits in diesem Werkzeug enthaltene Regressionstestsuite.

Bestätigte statische Beziehungen und isolierte Ergebnisse werden im Folgenden ausdrücklich von noch offenen Live-Spielprüfungen getrennt. Eine erfolgreiche private Funktionsausführung validiert weder produktive Hookspannen noch den vollständigen Spielablauf.

### 19.2 Start und Laden setzen die Pechhöhe erneut herab

**Statischer Codebefund:** 0x804C0 durchläuft die Kartenfelder. Bei gesetztem PitchTrap-Flag und Default-Höhe größer als 3 schreibt die Funktion erneut Default-Höhe minus 4 in HeightGrid. Dieselbe Funktion bereinigt außerdem ein anderes Terrain-/Strukturflag 0x20; der ganze Funktionsaufruf darf deshalb nicht pauschal übersprungen werden.

Untersuchter Aufrufweg:

- 0x804C0 wird von 0x7D510 vor dessen nachfolgenden Versionsprüfungen aufgerufen.
- 0x7D510 ist Teil von 0x71670.
- Aufrufer von 0x71670 sind unter anderem Save-Laden 0x853F0, Neuspiel 0x94350 und Tutorialstart 0x100530.

**Isoliertes natives Experiment:** Im privaten B-Abbild mit den drei Änderungen aus Abschnitt 18.5 besaß ein Pechfeld zunächst HeightGrid 8, DefaultHeight 8 und Flags 0x8008. Der anschließende Aufruf der unveränderten 0x804C0 setzte HeightGrid auf 4 zurück; Default und Flags blieben unverändert.

**Folgerung für B:** Die drei bisher getesteten Erzeugeränderungen reichen nicht. Auch diese Normalisierung muss die B-Höhenregel herstellen, bevor darauf aufbauende Wegdaten berechnet werden. Das ist für das reguläre Laden einer unter aktivem Mod begonnenen Partie notwendig und keine Erweiterung um Altsave-Reparatur.

Ein neues Spiel kann bereits vorhandenes Kartenpech übernehmen. Nur den Minus-4-Store zu überspringen würde bei einem bereits abgesenkten Eingabefeld weiterhin Höhe 4 stehen lassen. Deshalb muss der Startpfad für zulässige Pechfelder tatsächlich normale Bodenhöhe herstellen. Default-Höhen 0 bis 3 und mögliche Sonderkombinationen von Terrain-/Strukturflags müssen ausdrücklich berücksichtigt werden; aus dem bisherigen Größer-3-Zweig darf keine ungeprüfte pauschale Höhenüberschreibung beliebiger Strukturen werden.

### 19.3 Der Renderer besitzt bereits eine Pechkompensation

**Statischer Codebefund:** Im Pechzweig des Tile-Renderers 0x41D60 wird die Höhe aus einer Tabelle ab RVA 0x4065E40 gelesen. Die normale Höhentabelle beginnt bei 0x4065E30. Bei vier Byte pro Eintrag entspricht der Unterschied einer Verschiebung um vier Höhenindizes. Vereinfacht gleicht dies für normalen Boden die gespeicherte Pechabsenkung wieder aus: reale Pechhöhe 4 wird wie normaler Höhenindex 8 ausgewertet.

B ließe die reale Pechhöhe bei 8. Die unveränderte Pechkompensation würde dann Index 12 verwenden. Die Abhängigkeit und Indexänderung sind belegt; deren sichtbare Auswirkung wurde noch nicht im laufenden Spiel gemessen.

**Anforderung bei unveränderter Darstellung:** Den betreffenden Pechzweig an die neue gespeicherte Höhe anpassen. Keine anderen Tabellen oder beliebigen +4-Konstanten allein wegen ihres Zahlenwerts verändern. Vor einer produktiven Anpassung die vorhandenen Zweigbedingungen sowie die Zustände vor, während und nach dem Brennen einbeziehen.

Niedrige Default-Höhen benötigen einen eigenen Grenzfalltest: Vanilla begrenzt die gespeicherte Absenkung bei null. Deshalb ist ein bloßer Wechsel auf die normale Tabellenbasis nicht auf jedem Feld automatisch gleichbedeutend mit vollständig identischer Vanilla-Darstellung. Offen bleiben die Sichtprüfung von Pech, Einheiten und Feuereffekten, Übergängen zu Treppen sowie möglichen Überdeckungen.

### 19.4 Standhöhe und Bewegungswerte von Einheiten ändern sich

**Statischer Codebefund:** 0x19B260 verwendet die gespeicherte Feldhöhe für die Annäherung der Einheit an ihre Standhöhe und für einen Geschwindigkeitswert. 0x184FD0 enthält weitere Standhöhenlogik; 0x182B00 verwendet diese Ergebnisse bei der Aktualisierung der Einheitenposition. Die Änderung von HeightGrid wirkt damit über die Wegfreigabe hinaus.

**Isoliertes natives Experiment:** Ziel war gewöhnlicher Boden mit Höhe 8, ohne Pechflag; der gesetzte Basisgeschwindigkeitswert betrug 15. Verglichen wurden zwei Ausgangsstandhöhen der Einheit, die den Übergang von abgesenktem Pech beziehungsweise B-Pech auf normalen Boden modellieren.

| Ausgangsstandhöhe | Standhöhe nach dem untersuchten Aufruf | Ergebnisfeld r_CurrentSpeed2 |
|---|---:|---:|
| 4, entsprechend abgesenktem Pech | 7 | 18 |
| 8, entsprechend B | 8 | 15 |

Dies bestätigt einen Unterschied der nativen Ergebniswerte. Es ist kein gemessener Laufzeit- oder Geschwindigkeitsvergleich ganzer Bewegungsbefehle. Ohne Auswertung der weiteren Verwendung darf daraus insbesondere kein bestimmter Prozentsatz und keine sichere Aussage „schneller“ oder „langsamer“ abgeleitet werden.

**Einordnung:** Dass B den künstlichen Höhenwechsel entfernt, kann erwünscht sein. Es bleibt eine zusätzliche Verhaltensänderung. Bewegungen über beide Pechränder, auf Treppen und zurück sowie sichtbare Standhöhe und tatsächliche Laufzeiten müssen im Spiel geprüft werden.

### 19.5 Feuer: Pechkette, allgemeine Weitergabe und direkter Schaden unterscheiden

**Pech-zu-Pech-Weitergabe:** 0x7D280 vergleicht die gespeicherten Höhen benachbarter Pechfelder und verlangt einen Unterschied kleiner als 25. Werden beide Höhen gleichmäßig um 4 angehoben, bleibt ihre Differenz gleich. Diese Entwarnung gilt nur unter dieser Bedingung; die Begrenzung niedriger Höhen bei null und gemischte alte/neue Höhenzustände sind gesonderte Fälle. Der Pfad erzeugt Feuereffekte mit der Feldhöhe und stellt nach Ablauf der Pechbrandphase die Default-Höhe wieder her.

**Allgemeine Feuerweitergabe:** Zusätzlich verwendet 0x99420 die rohe Höhe des brennenden Feldes und vergleicht sie mit der Nachbarhöhe aus 0x6B910. Dieser Helfer berücksichtigt gegebenenfalls auch Gebäudekorrekturen. Der erforderliche Höhenunterschied ist ebenfalls strikt kleiner als 25. Gegenüber einem gewöhnlichen Feld hebt sich die Höhenänderung durch B nicht notwendig auf.

**Isolierter nativer Zweigtest:** Der originale Funktionskörper 0x99420 wurde mit kontrollierten Feuerzustands- und Nachbarwerten ausgeführt. Der nachfolgende Aufruf 0x9C120 wurde im privaten Abbild ausschließlich durch einen Marker ersetzt, um den Ausbreitungsversuch zu erfassen, ohne neue Feuerdatensätze zu erzeugen.

| Höhe des brennenden Pechfeldes | Gewöhnliche Nachbarhöhe | Absoluter Unterschied | Aufruf zur Weitergabe versucht |
|---|---:|---:|---|
| 4, Vanilla-Absenkung | 32 | 28 | nein |
| 8, B | 32 | 24 | ja |

Das ist ein bestätigter Unterschied der nativen Zweigentscheidung. Es ist weder eine natürliche Kartenreproduktion noch ein Nachweis, dass im vollständigen Spiel an dieser Stelle tatsächlich ein zusätzliches Objekt brennt. Der echte Folgeaufruf prüft weitere Bedingungen.

**Zufallsfolge:** 0x9C120 ruft den nativen Zufallszahlengenerator 0x7530 bereits vor seinen nachfolgenden Zielprüfungen auf. Ein durch B zusätzlich erreichter Aufruf kann deshalb die Zufallsfolge verändern, selbst wenn später kein Feuer erzeugt wird. Das ist kein Nachweis eines Desyncs bei identischem B auf allen Teilnehmern; es unterstreicht aber die Notwendigkeit derselben Höhenregel auf allen MP-Peers.

**Direkter Brandschaden:** Der untersuchte Schadenspfad 0x99420 -> 0x9E030 -> 0x192400 läuft über die Einheitenliste des betroffenen Feldes. Im direkten Schadenspfad wurde kein Vergleich zwischen Feuerhöhe und Standhöhe der Einheit gefunden. Es gibt daher keinen entsprechenden Befund, dass Einheiten allein durch B gegen Pechfeuer immun würden. Das ist keine vollständige Abnahme sämtlicher Brand- und Einheitensonderfälle.

**Folgerung für B:** Soll das bisherige Brandverhalten erhalten bleiben, muss auch die Höhenentscheidung der allgemeinen Feuerweitergabe gezielt angeglichen werden. Eine solche Angleichung ist noch nicht implementiert oder isoliert als Gesamtfix geprüft. Effekterzeugung mit der geänderten Feldhöhe braucht zusätzlich eine Sichtprüfung. Die zunächst auf zwei Pechfelder beschränkte Entwarnung darf nicht auf alle Feuerpfade übertragen werden.

### 19.6 Konsistenz von Erzeugung, Überbau und Aktivierung

- Bei 0x59380 bleibt neben der unterdrückten Absenkung die übrige Pecherzeugung einschließlich Flag, Besitzer und Zustand erhalten. Die Platzierung darf dadurch keine zusätzlichen RNG-Aufrufe erhalten.
- Bei B entfällt das zugehörige +4 sowohl in 0x6CB20 als auch in allen einschlägigen Pechüberbauzweigen von 0x6F230, einschließlich gewöhnlicher Wände. Die nur auf Treppen beschränkte Korrektur von A wäre hier falsch.
- Pechentfernung 0x62EB0 und das Ende der Pechbrandphase in 0x7D280 enthalten bereits eine Wiederherstellung aus DefaultHeight. Daraus ergibt sich kein notwendiges pauschales zusätzliches Minus-4 für B. Entfernung, Überbau und Abbrennen bleiben dennoch eigene Abnahmeszenarien.
- Vorplatziertes Kartenpech und während der Partie gebautes Pech müssen vor ihrer ersten Verwendung dieselbe Höhenregel erfüllen. Gemischte 4-/8-Zustände dürfen nicht als korrekter B-Betrieb gelten.
- Der gemeinsame Aktivierungswert muss schon vor den nativen Start-/Ladeberechnungen feststehen. Ein später Missionsstart-Callback ist dafür nicht ausreichend. Alle MP-Peers benötigen denselben vorbereiteten Wert; eine allein hostseitige Änderung reicht nicht.
- Alle voneinander abhängigen Änderungen gemeinsam vorbereiten und freigeben. Eine Teilinstallation aus Erzeugerkorrektur, Vanilla-Ladenormalisierung und unveränderter Darstellung ist keine vollständige Variante B.
- Der zugesagte Betrieb bleibt eine neue Partie mit durchgehend aktivem Mod einschließlich ihrer späteren Ladezyklen. Ein Wechsel derselben Partie auf Vanilla ist keine zugesagte Kompatibilität und begründet hier keinen Reparatur- oder Migrationsauftrag.

### 19.7 Bewertung und ergänzte Abnahme für B

B ist weiterhin plausibel und kann mit weniger Sonderfällen in Weg- und Gebäudeprüfungen auskommen. Weniger native Eingriffsstellen bedeuten wegen der gemeinsamen HeightGrid-Eingabe aber nicht automatisch weniger Auswirkungen. Die erfolgreichen Rechenfälle aus 18.5 lösen diese Unterscheidung nicht auf.

Neben den drei bisher getesteten Erzeugeränderungen sind mindestens zwei weitere Bereiche konkret zu bearbeiten: Start-/Ladenormalisierung und Pechdarstellung. Für unveränderte allgemeine Brandweitergabe kommt deren Höhenentscheidung hinzu. Dies ist eine Aufzählung fachlicher Bereiche, keine bereits feststehende Zahl produktiver Hooks. Bewegung und Effekthöhen sind zusätzliche belegte Verbraucher mit noch offener Spielabnahme.

Zusätzlich zu den bestehenden Treppen-/Erreichbarkeitsfällen prüfen:

1. Beide übergebenen Burgen sowie Pech vor und unter Treppen; lokale und vollständige Wegneuberechnung, neue Bewegungsbefehle in beide Richtungen und tatsächlicher Fortbestand der Gebäude.
2. Wiederholtes Speichern/Laden einer mit B begonnenen Partie: HeightGrid bleibt konsistent, vorplatzierte Kartenpechfelder eingeschlossen. Keine Abrissschutzoption darf das Ergebnis verdecken.
3. AIV- und manueller Überbau mit Treppen sowie gewöhnlichen Wänden; Pech entfernen und neu platzieren; Abbrennen und anschließender Neubau.
4. Bewegung, Standhöhe und tatsächliche Laufzeiten beim Betreten und Verlassen von Pech, an Treppen und ohne Höhenwechsel als Gegenprobe.
5. Darstellung von unentzündetem und brennendem Pech, Einheiten und Feuereffekten, einschließlich niedriger Default-Höhen und angrenzender Treppen.
6. Pechketten und allgemeine Brandweitergabe an Höhengrenzen: gewöhnliche Nachbarfelder, Gebäudekorrekturen, niedrige Höhen sowie Unterschiede knapp unter, genau auf und oberhalb der Grenze 25. Versuchte Weitergabe, tatsächlich erzeugte Brände, Schaden und RNG-Folgen auseinanderhalten.
7. Vorplatziertes Kartenpech, Editor und die in diesem Audit erreichten Startpfade; anschließend BFQoL-Wegprüfung, externer Fixes-Mod und echter Host/Client mit identischer Aktivierung.

Die sinnvollste weitere Prüfung von B ist damit kein bloßes Entfernen dreier Instruktionen. Sie muss die normale Feldhöhe über Start, Bau und Laden erhalten, vorhandene Höhenkompensationen berücksichtigen und die zusätzlichen Auswirkungen auf Feuer und Bewegung nachvollziehbar bewerten. Ein Produktivfix oder die Garantie, dass darüber hinaus kein weiterer direkter Höhenverbraucher betroffen ist, liegt noch nicht vor.

## 20. Testmod „PitchStairsTest“: Umsetzung von Variante B

Aktualisierung vom 02.11.2026: Der historische Entwicklungs- und Buildstatus dieses Abschnitts wird durch den dokumentierten Spielstart und Ladeversuch in [Abschnitt 21](#21-logauswertung-vom-02112026) ergänzt. Die Aussagen „noch nicht gebaut oder installiert“ und die Buildblockade in 20.4 gelten für den damaligen Stand, nicht für den dort ausgewerteten Lauf.

Stand: 01.11.2026. Implementiert als eigenständiger Testmod unter `Testmods/PitchStairsTest` (GUID `PitchStairsTest_Serp`, Version 0.1.0, NetworkMode 1). Er ist noch nicht im Spiel getestet und noch nicht gebaut oder installiert, siehe 20.4. Nicht committet.

### 20.1 Nutzerentscheidungen

- Variante B als Testmod.
- Betroffene Savegames dürfen korrigiert werden, sofern das kein großer Zusatzaufwand ist. Das geschieht mit demselben Mechanismus, der vorplatziertes Kartenpech behandelt (siehe 20.2, Start-/Ladenormalisierung).
- Das Vanilla-Brandverhalten bleibt erhalten (Abschnitt 19.5).
- Die geänderte Standhöhe und Geschwindigkeit von Einheiten über Pech (Abschnitt 19.4) wird akzeptiert und im Spiel geprüft.

### 20.2 Vollständiger Eingriffsumfang

Grundlage ist ein vollständiger linearer `.text`-Scan derselben DLL mit 462 HeightGrid-Zugriffen und 136 direkten Schreibern. Ergebnis:

- Es gibt genau einen −4-Store (0x594CB).
- Es gibt genau eine Start-/Ladeschleife, die die Absenkung erneut herstellt (0x804C0, Abschnitt 19.2).
- Es gibt genau zwei +4-Rückrechnungen (0x6CCED, 0x6F6B6).
- Zwei Leser kompensieren die Absenkung, beide mit dem Prädikat „PitchTrap und nicht MiscDisplay-Bit 0x2000“. Das sind der Renderer (Abschnitt 19.3) und zusätzlich die Platzierungsprüfung in 0x676F0 (`add esi,4` vor `cmp esi,0x88`); letztere war bisher nicht dokumentiert.
- Bit 0x2000 markiert laut 0x6C510 einen Grundrissrand und hat mit dem Brennen nichts zu tun.

Elf permanente native Stubs bilden eine atomare Einheit; Details in `Testmods/PitchStairsTest/UpdateToNewDLL.md`:

| Bereich | Stelle(n) | Aktiv |
|---|---|---|
| Pechbau | 0x594C1 | Absenkungsstore entfällt |
| AIV-Überbau | 0x6CCED | +4 entfällt |
| Manueller Überbau | 0x6F6AD | +4 entfällt für Treppen und Wände |
| Start/Laden (auch Kartenpech und alte Saves) | 0x80530 in 0x804C0 | Default ≥ 4: Höhe = Default. Default 1..3: freies Pechfeld auf 0 wird auf Default angehoben. |
| Darstellung | 0x43357 | Tabelle +0x7080 statt +0x7090 |
| Platzierungsgrenze | 0x68EAD | `add esi,4` entfällt |
| Feuerweitergabe, brennendes Feld | 0x997B4 | Vergleich mit max(0, h−4) wie in Vanilla |
| Feuerweitergabe, Nachbarn | 0x997DB, 0x99824, 0x9987C, 0x998D1 | Ergebnis von 0x6B910 bei Pech ohne Struktur: max(0, h−4) wie in Vanilla |

Die frühere Idee einer eigenen Normalisierungsschleife vor `call 0xDAA50` (0x71B4B) ist ersetzt. 0x804C0 läuft innerhalb desselben 0x71670 früher und ist der eigentliche Vanilla-Schreiber.

0x6B910 bleibt ungepatcht, weil es mehr als 40 Aufrufer hat. Die Pech-zu-Pech-Kette 0x7D280 bleibt unverändert, weil sich ihre Differenz nicht ändert; Ausnahme sind niedrige Default-Höhen.

### 20.3 Nachweise

- `PitchStairsTest.StubTests.exe` führt für jede Stelle den originalen Vanilla-Block und den Stub im selben Harness aus:
  - deaktiviert bitgenau gleich (Register, Flags, Stack, Ausgang, Speicher);
  - aktiv nur mit den beabsichtigten Abweichungen;
  - zusätzlich die Ausrichtung und das Überschreiben des Home-Bereichs beim Aufruf von 0x6B910 sowie die Site-Bytes in der installierten DLL.
  - Ergebnis am 01.11.2026: `PASS: 2647 pitch-stairs stub checks`.
- Bestanden: `Test-SharedBoundaries.ps1 -ModDirectory Testmods/PitchStairsTest`, `Test-DependencyMetadata.ps1`, `Test-DevDiagnostics.ps1` und `verify.ps1`.
- Vanilla-Befunde ergänzt in `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/PITCH_STAIRS_HEIGHT.md`, Abschnitt „Complete pitch-drop writer and compensation inventory“.

### 20.4 Offen

Historischer Stand vom 01.11.2026: Zur späteren erfolgreichen Laufzeitaktivierung siehe Abschnitt 21, zur implementierten Editor-Normalisierung Abschnitt 20.6. Deren jeweilige Spielabnahme bleibt gesondert zu beurteilen.

- **Build/Installation (Stand 01.11.2026, 22:13):** `build.bat /devdiag /nopause` brach in der APIShared-Vorbereitung ab („Incomplete or mixed bootstrap pair“). Im lokalen APIShared-Checkout (HEAD c490504, nicht committete Netzwerkänderungen) passt `BepInEx/plugins/APIShared_Serp/APIShared.dll` nicht zu `dependency-bootstrap-pair.json`. Die Workspace-Paketprüfung lehnt installierte APIShared-DLLs ab. Das betrifft nicht den Testmod-Code.
- Editorabdeckung: Der Editor-Ladepfad erreicht 0x804C0 nicht. Der DEVDIAG-Scan `PITCH_STAIRS_SCAN` meldet noch abgesenkte Pechfelder je Start.
- Spielabnahme nach Abschnitt 19.7, insbesondere:
  - Neuspiel → Speichern → Laden mit Castillo de Burgalimar Pitch Test.aivjson;
  - Laden eines betroffenen Vanilla-Saves;
  - Darstellung von Pech und Feuer;
  - Bewegung;
  - Feuerweitergabe an Höhengrenzen;
  - MP Host/Client.

### 20.5 Review-Nachtrag (01.11.2026, vor dem ersten Build)

Ergänzende Prüfungen, während der Build auf ein konsistentes APIShared-Paket wartet.

**Zeigerbasierte Zugriffe nachgescannt.** Der Scan aus 20.2 erfasste nur Displacement-Zugriffe. Schleifen über einen Rasterzeiger (wie `mov [r9],al` in 0x804C0) wurden deshalb separat gesucht: alle `lea` auf Height-, Default-, Logic- und MiscDisplay-Basen, absolut oder relativ zum Tile-Manager. Gefunden:

| Funktion | Verhalten | Pechbezug |
|---|---|---|
| 0x80320 | Bit 0x800: Höhe ≤ Default → Default | kein |
| 0x80430 | Grabenfelder: Höhe 0 | kein |
| 0x71390 | Bits 0x30: Höhe = Default | kein |
| 0x7FBE0 | Default = Höhe (mit Wandabzügen) | nur Migration für Kartenversionen < 0x78; unter B konsistent |
| 0x58FC0 | legt Rasterzeiger im Tile-Manager ab (+0x204E830/840/880) | kein nativer Leser |

Es gibt keine weitere Pech-Absenkung und keine weitere Kompensation.

**Weitere Prüfergebnisse:**
- Verwaltete Darstellung: `GameMap` übernimmt Tile-Höhen aus dem Ausgabepuffer des nativen Renderers. Das deckt der Render-Stub ab.
- Extender: `GamePitchManagerAPI.CreatePitch` nutzt den nativen Pechbau. Pechbau löscht laut Extender die MiscDisplay-Bits 0x2000/0x4000, neues Pech erfüllt also das Absenkungsprädikat.
- B-Spielstände bleiben Vanilla-kompatibel: Vanilla senkt beim Laden über 0x804C0 wieder auf Default−4.

**Code-Befunde:**
- Behoben: Der Stub wird im echten Hook aus den Instruktionen gebaut, die RedBird übergibt. Diese werden jetzt gegen den auditierten Block geprüft (Code, Länge, Displacement, Immediate, Sprungziel). Eine Abweichung rollt die Transaktion zurück. Negativtests sind ergänzt; StubTests jetzt `PASS: 2649`.
- Erstmals kompiliert: Runtime, Plugin und DevDiag-Teil (reine `csc`-Prüfung ohne Installation). Ergebnis fehlerfrei, die Warnungen stammen nur aus bestehendem Shared-Code.
- **Offen, Editor:** Der Editor erreicht 0x804C0 nicht. Eine Vanilla-Karte mit Pech behält dort Höhe Default−4. Folgen:
  - Eine manuell über solchem Pech gebaute Wand oder Treppe wird 4 zu niedrig und so in die Karte gespeichert, weil das +4 übersprungen wird.
  - Der Renderer zeichnet solches Pech im Editor 4 zu tief.
  - Vorschlag: einmalige Normalisierung beim Editorstart (EditorCreated/EditorLoaded) nach der Regel von 0x804C0 unter B.
- Bekannte Kleinigkeit: Zwischen dem Ende der Brandphase (0x7D4A0 stellt Default her, Flag bleibt) und der Entfernung im nächsten Durchlauf behandelt die Feuer-Angleichung das Feld noch als abgesenkt.
- Grenze der Savegame-Korrektur: Gebäude, die ein Vanilla-Save schon abgerissen oder zum Abriss markiert hat (Zustand 3), stellt der Mod nicht wieder her. Er verhindert nur neue Markierungen.
- Restrisiko: Die Fire-Neighbor-Stubs rufen 0x6B910 aus einem Stub ohne Unwind-Informationen auf. Das entspricht der üblichen Context-Hook-Praxis und ist nur bei einer SEH-Ausnahme innerhalb des Aufrufs relevant.

### 20.6 Editor-Normalisierung (01.11.2026)

Die offene Editorlücke aus 20.5 ist im Testmod behoben. Ablauf beim Missionsstart `EditorCreated`/`EditorLoaded` (nicht bei Replays), nach der Tile-Manager-Gegenprüfung und nur bei aktivem Fix:

- Auf alle 320 800 Felder wird einmal die Regel des Lade-Stubs angewendet: Pech mit Default ≥ 4 erhält Default; Default 1..3 erhält Default nur bei Höhe 0 ohne Wand und Struktur.
- Die Regel liegt als `NativeCode.NormalizedPitchHeight` vor. StubTests vergleicht sie mit dem nativen Stub für alle Ladefälle (`PASS: 2658`).
- Der Wert `editorRaisedTiles` erscheint im Log `PITCH_STAIRS_SESSION`.
- Wegübergänge werden im Editor nicht neu berechnet. Jeder spielbare Start berechnet sie in 0x71670 neu.
- Die Spielabnahme steht aus: Vanilla-Karte mit Pech im Editor laden, Wand und Treppe darüber bauen, speichern und als Partie starten.

## 21. Logauswertung vom 02.11.2026

### 21.1 Quelle, Testkontext und Zuordnung

Ausgewertet wurde der Host-Prozesslauf vom 02.11.2026, etwa 10:57:20 bis 11:03:54 (Europe/Berlin). Das Log hatte zum Auswertungszeitpunkt 678.708 Bytes; letzte Dateiänderung 11:03:55.

Quelle:

    E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log

SHA-256 des ausgewerteten Logs:

    725265D719174DA5709C1645D6A40AB56A9041903C2AD039F7A779861F845D97

Geladen waren Pitch Stairs Test 0.1.0 (GUID `PitchStairsTest_Serp`, mit DEVDIAG-Ausgaben), MoatMove 0.1.6, BugfixesAndQoL 1.0.208, APIShared 0.23.0 und Script Extender 2.14.3.0. Die nativen Adressen beziehen sich weiterhin auf die kanonische DLL aus Abschnitt 3.1 mit SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.

PitchStairsTest verändert die Pechhöhe und die zugehörigen Bau-, Lade-, Darstellungs-, Platzierungs- und Feuer-Höhenvergleiche. Wartezeit, Feuerflucht und Rückkehr werden vom gleichzeitig aktiven MoatMove gesteuert. Dessen Logbefunde und Empfehlungen stehen im [PathfindingFinalReport.md, Abschnitt 8](PathfindingFinalReport.md#8-feuerbewegung-und-rückkehr--nachtrag-vom-02112026). Die beobachtete fehlende Bewegungsanimation wird deshalb nicht ohne weiteren Nachweis dem Pechhöhenfix zugeschrieben.

### 21.2 Installation im tatsächlichen Spielstart

Um 10:57:20.383 wurden alle elf `PITCH_STAIRS_SITE`-Marker ausgegeben, jeweils mit `method=reference-rva`: Pechabsenkung, AIV- und manueller Überbau, Ladenormalisierung, Darstellung, Platzierungsgrenze, Feuer-Ausgangshöhe und vier Feuer-Nachbarhöhen. RVAs und Spannen entsprechen dem dokumentierten Umfang aus 20.2 und `Testmods/PitchStairsTest/UpdateToNewDLL.md`.

Um **10:57:20.387** folgte `PITCH_STAIRS_READY: 11 permanent native stubs`. Beide nachfolgenden Missionsstarts meldeten `enabled=True` und `ready=True`. Damit wurde die Installation dieses Prozessstarts einschließlich ihrer Nachkontrollen erreicht. Die historische Buildblockade aus 20.4 beschreibt nicht den Zustand dieses Laufs; ein bestimmtes Buildmanifest oder der damalige Buildweg wird durch das Log allein nicht nachgewiesen.

Im gesamten ausgewerteten Log wurden keine modbezogene Error-/Fatal-Meldung und keine Marker `PITCH_STAIRS_UNAVAILABLE`, `PITCH_STAIRS_DISABLED`, `PITCH_STAIRS_SESSION_FAILED` oder `PITCH_STAIRS_SCAN_FAILED` gefunden. Die vorhandenen Archiv-/Lua-Warnungen beim Laden sind für sich kein Beleg für einen Fehler von PitchStairsTest.

### 21.3 Missionsstarts und gemessene Feldhöhen

| Zeitpunkt / Start | Pechfelder im Scan | Weiterhin abgesenkt | Relevante Zähler beim Startbericht |
|---|---:|---:|---|
| 10:58:08.057, Session 1, NewGame; Scan 10:58:08.059 | 0 | 0 | Alle sieben Zähler 0/0 |
| 11:03:20.634, Session 2, LoadedSave; Scan 11:03:20.635 | 173 | 0 | Unterdrückte Absenkungen 52/52; Ladefelder 173/173; beim Laden angepasste Höhen 173/173 |

Beim nativen Laden um 11:03:18.878 nennt das Log ausdrücklich diesen Spielstand:

    C:\Users\Serpens66\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\Saves\test2_pitchstrais2.sav

Dieser Dateiname unterscheidet sich vom historischen `test2_pitchstrais.sav`. Die Startmarker beweisen nicht, dass der geladene Spielstand aus dem zuvor protokollierten Neuspiel erzeugt wurde.

Vollständiger Zählerstand der zweiten Startmeldung, jeweils Delta / Total:

| Zähler | Wert |
|---|---:|
| skippedLowerings | 52 / 52 |
| skippedAivRestores | 0 / 0 |
| skippedManualRestores | 0 / 0 |
| loadPitchTiles | 173 / 173 |
| loadRaisedTiles | 173 / 173 |
| fireSourceAdjusted | 0 / 0 |
| fireNeighborAdjusted | 0 / 0 |

Beide Scans meldeten `firstLoweredTile=-1`; alle vier ausgegebenen Rasterlängen betrugen jeweils **320.800**. Der Scan nach dem Laden fand unter seinen expliziten Pech-/Flag-/Strukturbedingungen kein weiterhin nach Vanilla abgesenktes Feld. Der Ladenormalisierungszähler bestätigt 173 erreichte Fälle und 173 tatsächliche Höhenanpassungen. Das ist kein allgemeiner Beweis beliebiger korrekter Feldhöhen.

Beide Sitzungen waren lokaler Singleplayer-Skirmish, `realMultiplayer=False`, `mapEditor=False`, `editorRaisedTiles=n/a`. Das gleichzeitig ausgegebene `lowLevelNetworked=True` macht daraus keinen echten Mehrspielertest.

### 21.4 Bedeutung und Grenzen der Zähler

Gegen `PitchStairsRuntime.OnMissionStarted`, den Lade-Stub in `NativeCode` und den DEVDIAG-Scan geprüft:

- Die nativen Zähler werden beim Anlegen des Runtime-State nullinitialisiert und nicht pro Mission zurückgesetzt. `reported[]` wird nur bei Missionsstarts fortgeschrieben. Delta bedeutet deshalb seit dem vorherigen Startbericht, einschließlich Aktivitäten der vorigen Partie und des anschließenden Ladens.
- Die 52 unterdrückten Absenkungen sind Ereignisse zwischen den beiden Startmeldungen. Sie sind weder 52 eindeutig identifizierte Kartenfelder noch ausschließlich Ereignisse der zweiten Sitzung oder ihres Ladeprozesses.
- `loadPitchTiles` zählt erreichte aktive Normalisierungsfälle. `loadRaisedTiles` steigt nur bei einem tatsächlichen Schreibzugriff mit vom Zielwert verschiedener Höhe; der Name beweist nicht für jeden Fall eine vorherige Absenkung um genau vier.
- AIV- und manueller Überbau blieben bei null. Der Lauf nimmt diese Teilfunktionen damit nicht ab.
- Beide Feuervergleichszähler blieben bei null. Das belegt keine gezählte einschlägige Höhenanpassung bis zum Bericht; es ist weder ein Erfolgstest der Feuerkorrektur noch ein Beweis, dass keine Feuerfunktion lief.
- Es gibt keinen abschliessenden Zählerbericht beim Missions- oder Prozessende. Die letzten ausgegebenen Totals gelten bis zur Startmeldung um 11:03:20.634, nicht als vollständige Messung aller späteren Aktionen bis 11:03:54.
- Für Darstellung und Platzierungsgrenze gibt es hier keine entsprechenden Laufzeitzähler. READY belegt die Installation, nicht das richtige sichtbare Ergebnis.

### 21.5 Ergebnis und verbleibende Spielabnahme

**Belegt sind die erfolgreiche Aktivierung im laufenden Spiel und die wirksame Ladenormalisierung der untersuchten Pechfelder.** Der Neuspiel-Startpfad samt Diagnose lief ebenfalls; weil beim Start kein Pech vorhanden war, wurde damit vorplatziertes Kartenpech im Neuspiel noch nicht geprüft.

Im Log wird der tatsächliche Abriss oder Fortbestand der betroffenen Gebäude nicht direkt erfasst. Daher folgt daraus allein weder die vollständige Behebung des historischen Abrissfalls noch ein nachgewiesener Neuspiel-Speichern-Laden-Zyklus desselben Saves. Ebenfalls offen bleiben Darstellung, Pechüberbau per AIV und manuell, Platzierungsgrenze, Feuerweitergabe an Höhengrenzen, Editor und echter MP-Host/Client. Die Editorimplementierung aus 20.6 ist von ihrer noch offenen Spielabnahme zu unterscheiden.

Die Nutzerbeobachtungen zur gleitenden Bewegung und ausbleibenden Rückkehr werden als Beobachtungen des kombinierten Testlaufs festgehalten. Der weiter untersuchte MoatMove-Bewegungsweg und die getrennten Wartefristen sind Gegenstand des Nachtrags im PathfindingFinalReport; mit dieser Dokumentation wurden keine Laufzeitkorrekturen vorgenommen.

### 21.6 Nachprüfung und Ergänzungen (02.11.2026, Claude Code)

Geprüft am selben Log (SHA-256 unverändert 725265D7…5D97, Stand 11:29) und an der kanonischen DLL.

**Build des Laufs.** Installiert war der DEVDIAG-Build vom 02.11.2026 04:10:53. Buildrecord: `.work/b/working/637.build.json`. SHA-256 der `PitchStairsTest.dll`: 0318A601A2A1385CECF0F5132AFA0917786FBBD631459CA0AC6E3BB2166B4640. Der Build enthält den damals noch nicht committeten Umbau des Feuer-Nachbar-Stubs auf zwei Aufrufwege ohne Stackeingriff. Anlass war ein Fehler der Zeilenvarianten `0x9987C`/`0x998D1`: Ihr deaktivierter Pfad übernahm nicht die Flags des Aufgerufenen, wie Vanilla es tut. Die Folge war ein von der Stackadresse abhängiges Parity-Flag. Im Spiel war das harmlos, weil `sub edi,eax` die Flags neu setzt. Die Buildblockade aus 20.4 ist damit erledigt.

**Herkunft des geladenen Saves.** `test2_pitchstrais2.sav` wurde zuletzt am 01.11.2026 um 20:44:40 geschrieben, also vor dem ersten Build des Testmods. Der zweite Start lud damit einen von Vanilla geschriebenen, betroffenen Spielstand.

- Die 173 angehobenen Felder sind die Korrektur dieses alten Saves.
- Nutzerbestätigung nach dem Laden: Die Gebäude hinter dem Pech blieben stehen, und die Einheiten erreichten die Treppe.
- Damit ist der Abnahmepunkt „betroffenen Vanilla-Save laden“ erfüllt.
- Ein Zyklus aus Neuspiel, Speichern und Laden mit aktivem Mod fehlt weiterhin.
- Ob die 52 `skippedLowerings` in der ersten Partie oder beim Laden anfielen, ist offen. Der Bericht am Missionsende (unten) trennt das künftig.

**Feuer: offener Befund.** Laut Nutzer brannte in der ersten Partie Pech. Trotzdem blieben `fireSourceAdjusted` und `fireNeighborAdjusted` bei 0. Statisch müsste der Quell-Stub bei `0x997B4` auf Pechfeldern zählen. Die Kette ist in `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/PITCH_STAIRS_HEIGHT.md` unter „Pitch fire path“ belegt:

- Pech erzeugt Feuer vom Typ 9 über `0x7D280 → 0x9C430 → 0x9C450 → 0x9B2B0`.
- Der Handler `0x2DA9C0[9] = 0x99420` wird pro Tick über `0x9F960` aufgerufen.
- Der Pechbau löscht das Bit `0x2000` auf dem Pechfeld.
- Im Brandpfad liegt kein fremder Hook.

Die Ursache ist ungeklärt.

**Folgemaßnahmen im Testmod:**

- `PITCH_STAIRS_SESSION_END` meldet die Zähler beim Missionsende, mit Delta seit dem letzten Bericht.
- Nur im DEVDIAG-Build zählen die Feuer-Stubs zusätzlich ihre Ausführungen sowie Pechfelder mit und ohne `0x2000`. Ausgegeben wird das als `DEVDIAG PITCH_STAIRS_FIRE` bei Start und Ende.
- Die Release-Stubs sind unverändert; StubTests prüft beide Varianten.

**Relevanz.** Für Erreichbarkeit, Abriss und Wegfindung ist der Feuerbefund unerheblich. Er betrifft nur die gewählte Erhaltung der Vanilla-Feuerweitergabe. Greifen die Stubs nicht, wertet die Weitergabe von und zu Pech die Felder 4 höher als Vanilla. Das ändert nur Fälle nahe der Grenze |Δh| = 25, etwa Pech vor Treppe oder Mauer.

**Nächster Spieltest:**

1. Neues Spiel starten, Pech legen und anzünden.
2. Das Pech vollständig abbrennen lassen und die Partie beenden.
3. Das Log sichern.

Danach `fireSourceCalls`, `fireSourcePitch` und `fireSourcePitchEdge` gegen `fireSourceAdjusted` auswerten.

## 22. Feuermessung vom 02.11.2026 und Bedeutung von MiscDisplay 0x2000

### 22.1 Lauf

Host-Prozesslauf vom 02.11.2026, 12:00:49 bis 12:02:00.

- Log: `LogOutput.log`, SHA-256 5D8F4AEE50E5FAA95F568C1DE98ADB2303FF04CD54D7F028C1F52D872796FC1F, 585.564 Bytes. Eine Kopie liegt im Scratchpad der Claude-Code-Sitzung.
- Build: DEVDIAG vom 02.11.2026 11:55, Buildrecord `.work/b/working/638.build.json`.
- Spiel: ein lokales Neuspiel (Session 1, `NewGame`, Singleplayer-Skirmish). Pech wurde gelegt und angezündet. Ende durch Beenden des Programms (`reason=ApplicationExit`).

| Zähler bei `PITCH_STAIRS_SESSION_END` / `DEVDIAG PITCH_STAIRS_FIRE point=end` | Wert |
|---|---:|
| skippedLowerings | 24 |
| fireSourceAdjusted / fireNeighborAdjusted | 0 / 0 |
| fireSourceCalls | 648 |
| fireSourcePitch | 305 |
| fireSourcePitchEdge | **305** |
| fireNeighborCalls | 220 |

Der Brandpfad 0x99420 lief also, 305-mal auf einem Pechfeld. Jedes dieser Felder trug MiscDisplay 0x2000, deshalb hat der Stub nie angepasst. Der Endbericht und die neuen Zähler funktionieren.

### 22.2 Nativer Befund: 0x2000 auf Pech bedeutet „für den lokalen Spieler sichtbar“

Statische Disassembly der kanonischen DLL:

- Der Kacheldurchlauf 0x65830 wird pro Simulationsblock aus 0xCDE60 aufgerufen (0xCE0E1), außerdem aus dem Editorpfad 0x5BC70.
  - Am Kopf jeder Kachel lädt er `r10d = 0xFFFFDFFF` (0x67630).
  - Für Kacheln mit PitchTrap löscht er 0x2000 (0x682D9) und sucht den Pechdatensatz (0x695C0).
- Danach setzt er MiscDisplay `0x800` (0x683A1) und `0x2000` (0x683D8) und eine Pech-Sprite-ID, wenn eine der folgenden Bedingungen zutrifft:
  - ein globales Flag ist gesetzt (0x682FE);
  - der Pechzustand ist mindestens 2, also brennend oder ausgebrannt (0x6830B);
  - der Besitzer des Datensatzes gehört zum Team des lokalen Spielers (0x68316..0x68334, Tabelle 0x37EDF3C, Index ist ein globaler Spielerindex);
  - ein weiteres globales Flag ist gesetzt (0x68336).
- Sonst bleibt 0x2000 gelöscht. Gegnerisches, nicht brennendes Pech ist für den lokalen Spieler verborgen.
- Außer diesem Durchlauf setzt nur der Gebäudeabriss 0x628F0 das Bit 0x2000, nach der Randprüfung 0x6C510 (0x629EF). Die Pechplatzierung 0x59380 löscht es (0x59502). Das ergab ein Scan aller Schreibzugriffe auf das MiscDisplay-Raster mit verfolgten Sofortwerten.

Folgerungen:

1. **Darstellung in Vanilla.** Sichtbares Pech (0x2000) zeichnet der Renderer ohne Ausgleich, also 4 tiefer: ein sichtbarer Graben. Verborgenes Pech zeichnet er mit dem +4-Ausgleich auf Bodenhöhe, damit der Gegner es nicht erkennt. Die Bezeichnung „Footprint-Rand“ aus den Abschnitten 19 bis 21 trifft für Pech nicht zu. Diese älteren Abschnitte bleiben als historischer Stand erhalten.
2. **Feuer.** Vanillas Brandweitergabe liest die rohe Höhe. Diese ist für jedes Pechfeld abgesenkt, unabhängig von 0x2000. Die Feuer-Stubs ließen wegen ihrer Regel „Pech ohne 0x2000“ alles eigene, verbündete und brennende Pech unangepasst. Nur verborgenes gegnerisches Pech wurde angepasst. Unter B wird damit fast jede Feuerweitergabe über Pech mit 4 Einheiten höheren Pechfeldern berechnet als in Vanilla.
3. **Mehrspieler.** 0x2000 hängt auf jedem Rechner vom lokalen Spieler ab. In Vanilla beeinflusst das Bit offenbar nur die Darstellung; sonst würde Vanilla im Mehrspieler desynchronisieren. Die Feuer-Stubs machen daraus eine Spielentscheidung. Bei gegnerischem Pech können Host und Client Feuer deshalb unterschiedlich weitergeben, was einen **Desync** bedeuten würde. Render- und Platzierungsstub betrifft das nicht, denn dort entscheidet Vanilla selbst mit dem Bit, und beide wirken nur auf die Darstellung.
4. **Mauern.** AIV-Überbau (0x6CCE3) und manueller Überbau (0x6F6B1) löschen PitchTrap. Ein Mauerfeld trägt also nie das Pech-Flag.

### 22.3 Korrektur (umgesetzt am 02.11.2026, Spieltest offen)

- **Feuer-Stubs:** Die Bedingung 0x2000 entfällt. In Vanilla abgesenkt ist ein Feld genau dann, wenn PitchTrap gesetzt ist. Beim Nachbarstub bleibt der Ausschluss von Strukturfeldern (eigener Pfad von 0x6B910).
- **Bekannte Grenze:** Das Fenster nach 0x7D4A0 bleibt bestehen. Dort ist die Höhe wiederhergestellt, das Flag aber bis zur Entfernung noch gesetzt.
- **Umgesetzt:**
  - `IsVanillaLoweredPitch` und der DEVDIAG-Scan prüfen 0x2000 nicht mehr.
  - Der DEVDIAG-Zähler heißt jetzt `fireSourcePitchVisible`. Er beobachtet das Bit nur und beeinflusst keine Entscheidung.
  - StubTests: Fälle mit und ohne 0x2000 liefern dasselbe Ergebnis. 5412 Prüfungen bestehen.
  - UpdateToNewDLL.md ist aktualisiert.
- **Darstellung unter B:** Sichtbares Pech erscheint auf Bodenhöhe statt 4 tiefer. Das passt zu den ebenfalls rohen Höhen der Einheiten (0x19B260 u. a.) und der Feuereffekte (0x7D44B, 0x9C8B8). Ein abgesenktes Zeichnen nur des sichtbaren Pechs würde Einheiten und Flammen 4 Einheiten darüber schweben lassen. Das ist eine Nutzerentscheidung; empfohlen wird, die einheitliche Bodenhöhe beizubehalten.

### 22.4 Spieltest der Korrektur (02.11.2026)

Host-Prozesslauf vom 02.11.2026, 12:15:19 bis 12:16:39.

- Log: SHA-256 DD66B6AA08E6A237F0453955A75967256F3570E1D44CBE9F55A2F3CE191BCE8C, 558.793 Bytes. Eine Kopie liegt im Scratchpad der Claude-Code-Sitzung.
- Build: DEVDIAG vom 02.11.2026 12:10, Buildrecord `.work/b/working/643.build.json`.
- Spiel: ein lokales Neuspiel. Pech wurde gelegt und angezündet, Ende durch Beenden des Programms. Keine Error- oder Fatal-Zeilen im Log.

| Zeitpunkt | Ergebnis |
|---|---|
| Start 12:15:39 (NewGame) | Vor dem Startbericht waren 173 Absenkungen unterdrückt. Der Scan fand 173 Pechfelder, davon keines mehr auf Vanilla-Höhe abgesenkt. Das Pech entstand also beim Kartenstart über 0x59380, nicht über die Ladenormalisierung (`loadPitchTiles=0`). |
| Ende 12:16:39 | Weitere 39 Absenkungen unterdrückt, insgesamt 212. Die Feuerzähler stehen in der Tabelle darunter. |

| Feuerzähler | Wert |
|---|---:|
| fireSourceCalls | 847 |
| fireSourcePitch | 363 |
| fireSourceAdjusted | **363** |
| fireSourcePitchVisible | 363 |
| fireNeighborCalls | 284 |
| fireNeighborAdjusted | **125** |

Jede Brandquelle auf Pech wurde jetzt angepasst. Die Nachbar-Anpassung greift ebenfalls. Damit nimmt der Lauf die Korrektur aus 22.3 auf Zählerebene ab. Ob das Feuer an Höhengrenzen genau wie in Vanilla übergreift, ist damit nicht einzeln belegt. Die Stub-Tests prüfen dafür die Rechenregel gegen den Vanilla-Block.

**Darstellung:** Laut Nutzer ist visuell alles in Ordnung. Die einheitliche Bodenhöhe aus 22.3 bleibt bestehen; die Entscheidung ist damit getroffen.

**Weiterhin offen:**
- AIV- und manueller Überbau von Pech: Beide Zähler standen in allen bisherigen Läufen bei 0.
- Editor.
- Zyklus aus Neuspiel, Speichern und Laden mit aktivem Mod.
- Echter Mehrspieler-Test mit Host und Client.

### 22.5 Testplan für die offene Spielabnahme

Die beiden Überbau-Stubs greifen nur, wenn eine Mauer oder Treppe auf ein vorhandenes Pechfeld gebaut wird. Beide prüfen vorher `test al,8`. Die Castillo-AIV hat das in beiden Läufen nicht ausgelöst. Deshalb sind gezielte Handgriffe nötig. Nach jedem Lauf das Log sichern; ausgewertet werden `PITCH_STAIRS_SESSION`, `PITCH_STAIRS_SESSION_END` und `DEVDIAG PITCH_STAIRS_SCAN`.

1. **Manueller Überbau:**
   - Im Neuspiel Pech legen.
   - Darüber ein Mauerstück und eine Treppe bauen.
   - Erwartung: `skippedManualRestores > 0`. Mauer und Treppe sind gleich hoch wie neben dem Pech. Einheiten steigen über die Treppe auf die Mauer.
2. **Speichern und Laden mit aktivem Mod:**
   - Denselben Stand speichern und laden.
   - Erwartung: `loadPitchTiles` ist größer als 0. `loadRaisedTiles` bleibt bei 0 oder sehr klein, weil die Höhen bereits B entsprechen. Der Scan meldet `stillLowered=0`, und die Gebäude bleiben stehen.
3. **Editor:**
   - Eine Vanilla-Karte mit vorplatziertem Pech im Editor öffnen.
   - Erwartung: `editorRaisedTiles` ist größer als 0, und der Scan meldet `stillLowered=0`.
   - Danach im Editor eine Mauer über Pech setzen, die Karte speichern und als Partie starten. Die Mauer muss die normale Höhe haben.
4. **Mehrspieler:**
   - Host und Client mit identischer Modliste, eigenes und gegnerisches Pech, beides anzünden.
   - Erwartung: kein Desync. Auf beiden Rechnern ist `fireSourceAdjusted` gleich `fireSourcePitch`. Das Client-Log liegt unter `\\LENOVO_SERP\…\LogOutput.log`.

### 22.6 Abnahmelauf vom 02.11.2026, 12:53 bis 12:58

**Quelle**
- Log: SHA-256 6C87DA932A3E6FA82B88C8286D274DC72AEC6953E8F8350D0AD957A3CCC4810D, 833.666 Bytes. Eine Kopie liegt im Scratchpad der Claude-Code-Sitzung.
- Build: DEVDIAG `.work/b/working/643.build.json`. Fixes-Mod und BugfixesAndQoL waren mitgeladen.
- Keine Error- oder Fatal-Zeilen. Laut Nutzer sieht visuell alles korrekt aus.

**Lauf**

| Session | Ablauf | Ergebnis |
|---|---|---|
| 1 | `test2_pitchstrais3.sav` geladen, geschrieben am 02.11.2026 12:48:11 mit aktivem Mod (Build 12:10), über `DLL_LoadSaveGame`. Danach Weiterspielen. | Start: 1211 Pechfelder normalisiert, 0 angehoben, Scan `stillLowered=0`. Bis zum Ende: 26 Absenkungen unterdrückt, **7 manuelle Überbauten** unterdrückt, Feuer 332 von 332 Quellen und 100 Nachbarn angepasst. |
| 2 | Neue Editorkarte (`EditorCreated`, 0 Pech). Darin Pech gelegt und überbaut, gespeichert als `Maps\test_pitch.map` (12:57:31). | 93 Absenkungen und **9 manuelle Überbauten** im Editor unterdrückt. `editorRaisedTiles=0`. |
| 3 | `test_pitch.map` im Editor geladen (`EditorLoaded`). | **`loadPitchTiles=84`**, 0 angehoben, Scan 84 Pechfelder und `stillLowered=0`. 84 entspricht 93 gelegten minus 9 überbauten Feldern. |
| 4 | `test_pitch.map` als Partie gestartet (`DLL_LoadMapToPlay`, `NewGame`). | 84 Pechfelder normalisiert, **3 AIV-Überbauten** unterdrückt, Scan 77 Pechfelder und `stillLowered=0`. |

**Abgenommen** sind damit:
- Speichern und Laden mit aktivem Mod: Die Höhen bleiben erhalten, nichts musste angehoben werden.
- Manueller Überbau, im Spiel und im Editor.
- AIV-Überbau.
- Pechbau im Editor.
- Laden einer Editorkarte und Kartenstart aus dem Editor.
- Feuer.
- Optik, laut Nutzer.

Dass der Scan 77 statt 81 Pechfelder fand, also 4 weniger als 84 − 3, ist mit dem Log nicht weiter aufzuschlüsseln. Plausibel ist eine Pechentfernung durch den KI-Aufbau über 0x62EB0. Weiterhin offen ist allein der echte **Mehrspieler-Test** mit Host und Client.

**Berichtigung zum Editor (20.6).** Das Laden einer Karte im Editor läuft über `DLL_LoadSaveGame` mit `bLoadingEditorMap=1` (Extender-Log, Zeile 3704) und damit über 0x71670 → 0x7D510 → 0x804C0. Der Aufrufbaum von 0x804C0 hat nur diese Wurzeln:
- 0x853F0 (DLL_LoadSaveGame),
- 0x84AC0 (DLL_LoadMapToPlay),
- 0x1002A0/0x87C40/0x1054E0.

Nur eine neu angelegte Editorkarte (0x85E90) umgeht die Normalisierung, und sie enthält kein vorhandenes Pech. Die verwaltete Editor-Normalisierung aus 20.6 hatte daher in beiden Editorstarts nichts zu tun. Für keinen Startweg ist sie nachweislich nötig.

**Entfernt (02.11.2026, Nutzerentscheidung):**
- die verwaltete Editor-Normalisierung,
- die Hilfsregel `NativeCode.NormalizedPitchHeight`,
- das Logfeld `editorRaisedTiles`.

Den Editor-Ladeweg deckt der native Lade-Stub bei 0x80530 ab. Der DEVDIAG-Scan meldet weiterhin jeden Startweg, der noch abgesenktes Pech hinterlässt.

### 22.7 Abschluss-Audit der Vanilla-Auswirkungen (02.11.2026)

Gescannt wurden alle Funktionen der `.text`-Sektion, die die Höhe lesen oder schreiben (Raster +0xD7E5A0 bzw. RVA 0x4DDD350) und zugleich Logic-Bit 0x8 prüfen. Die Zuordnung zur Funktion erfolgte über `.pdata`. Ergebnis: 29 Funktionen.

Die meisten Treffer sind `and`-Masken von Gebäude- und Geländeschreibern. Diese Masken behalten Bit 0x8, die Funktionen prüfen Pech also gar nicht. Die übrigen Treffer:

| Funktion | Verhalten | Unter B |
|---|---|---|
| 0x639C0 | Graben über Pech: löscht PitchTrap, setzt Höhe 0 | gleich |
| 0x5D740, 0x6D580 | löschen PitchTrap (Maske …F7), setzen Höhe = Default | gleich |
| 0x10DDC0, 0x17CF50 | Einheiten: suchen nur den Pechdatensatz am Einheitenfeld (Entzünden), ohne Höhenentscheidung | unberührt |
| 0x9C730 | Feuereffekt: entzündet Pech, liest die Höhe nur für die Effekthöhe | Optik vom Nutzer abgenommen |
| 0x65830, 0x7D280 | bekannte Stellen: Platzierungs-Site bzw. Pechkette | wie dokumentiert |

Damit bleibt kein unbekannter pechabhängiger Höhenverbraucher übrig. Die Begründungen für jede gepatchte und jede bewusst nicht gepatchte Stelle stehen jetzt als Kommentar in `Testmods/PitchStairsTest/src/NativeCode.cs` (Klassenkommentar und Site- bzw. Emitter-Kommentare).

Grenzen des Scans:
- Höhenzugriffe, die über einen Zeigerlauf laufen, sind separat erfasst (siehe Baseline „Pointer-walking grid loops“).
- Pech-Prüfungen auf zuvor in ein Register geladene Logic-Werte erfasst der Scan nicht. Die bekannten Fälle dieser Art (0x6CB20, 0x6F230, 0x41D60) sind gepatcht.

## 23. Korrekturen und Startschutz nach vertiefter Prüfung (02.11.2026)

Dieser Abschnitt beschreibt den neuen Arbeitsstand. Die früheren Spielnachweise in Abschnitt 22
gelten weiterhin für ihre damaligen Builds; sie nehmen die folgenden Änderungen nicht ab.
Der ursprüngliche Schluss, ausschließlich ein echter MP-Lauf stehe noch aus, gilt für diesen
überarbeiteten Stand daher nicht. Referenz bleibt der native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

### 23.1 Bestätigte Randfälle und Umsetzung

- Beim Abbrennen stellt Vanilla die Höhe wieder her, bevor es den PitchTrap-Status entfernt.
  Die frühere Feuerkorrektur zog während dieses Fensters erneut vier ab. Eine native Markerkarte
  mit 320800 Bytes zeichnet nun den tatsächlichen Wiederherstellungsschritt auf. Neue Erzeugung
  löscht den Feldmarker. Feuer verwendet bei Marker 1 die wiederhergestellte Höhe.
- Laden löscht die Marker und rekonstruiert ausschließlich belegte terminale Pechrecords auf
  gültigen Feldern mit Default unter vier. Vanilla senkt solche terminalen Höhen beim Laden
  nicht erneut ab. Der native Recordvertrag wird unabhängig vom Extender-Reserved-Feld geprüft.
- Pechketten vergleichen beide lokalen Operanden als max(0,Höhe−4), ohne reale Höhen oder R12
  zu verändern. Das behebt insbesondere Sonderkarten mit Default-Höhen unter vier.
- Die Extender-Tilemanager-Prüfung liegt vor Registry-Claim, Allokation und Veröffentlichung.
  Eine Missionsstartprüfung kann die Regel nicht mehr nach veränderten Kartendaten abschalten.
  Auch eine fehlgeschlagene Abschlusslogausgabe kann eine aktivierte Regel nicht zurücksetzen.
- Die atomare Installation umfasst 14 permanente Sites und 320896 Zustandsbytes. Die drei
  zusätzlichen Sites und alle Speicher-/Registerverträge stehen in
  [UpdateToNewDLL.md](Testmods/PitchStairsTest/UpdateToNewDLL.md).

### 23.2 Multiplayer-Vertrag

Zurückgestellter Entwurf, kein aktiver Produktstand: Die folgenden Absätze dokumentieren den
bearbeiteten MP-Ansatz. Die abschließende Nutzerentscheidung und der aktive Umfang stehen in 23.6.

APIShared erhält einen additiven Startschutz mit synchronen, geordneten Validatoren auf dem
Unity-Thread. Explizites Ready/Lock, Host-Veröffentlichung, Save-Koordinator, der vollständige eingehende
Frontend-Startblock und direkte Lifecycle-Starts prüfen ihn vor den jeweiligen Seiteneffekten.
Unready, Unlock und normale Lobbyupdates bleiben möglich. ExtendedData prüft vor der eigenen
Vorbereitung; BFQoL prüft zusätzlich sämtliche unmittelbaren und verzögerten Host-Veröffentlichungen
unabhängig von der Hookreihenfolge. Eine verweigerte AIV-Fortsetzung verwirft den Versuch, sodass
späte ACKs ihn nicht mehr starten können. Es gibt keine automatische Wiederholung.
Ein bereits veröffentlichter und dann verweigerter eingehender Start bleibt anhand seiner
Steam-Lobby-ID und seines Startmarkers gesperrt, auch nach später übereinstimmenden Berichten.
Erst ein beobachtetes Löschen/Ändern des Markers oder ein Lobbywechsel beendet diesen Versuch;
ein bloß ausgetauschtes Lobbyobjekt mit gleicher ID und gleichem Marker genügt nicht.

Der Testmod registriert vor LibraryLoaded einen langlebigen Validator und die informative,
nicht schaltbare Settingsseite. Die vorhandene SyncPerPlayer-Koordination überträgt pending,
failed:reasoncode oder den exakten Token aus Regelrevision und vollständigem Native-Hash.
Jeder aktuelle menschliche Teilnehmer muss denselben Ready-Token melden; ein frischer
Lobby-Snapshot muss zusätzlich mit den Identitäten der synchronisierten Slots übereinstimmen.
Native Aktivierung setzt die erforderlichen Registrierungen und einen verfügbaren Startschutz
voraus. Die erste vorgesehene APIShared-Version mit diesem Broker ist 0.25.0.

### 23.3 Maschinenprüfungen und verbleibende Abnahme

Die aktualisierten nativen Tests bestehen 11361 Checks. Sie verwenden den tatsächlichen
Iced-/RedBird-Backend und führen die originalen Lade-, Ketten-, Abbrenn-, Entfernungs- und
Neuerzeugungspfade in einem privaten PE-Abbild aus. Grenzprüfungen umfassen Default-Höhen
0,1,2,3,4,8,255, Record-/Kartengrenzen, wiederholte Markerrekonstruktion und deaktivierte
Originalsemantik. Die produktive Peerbericht-Policy besteht zusätzlich 85 Checks.
Der zuletzt gesicherte Broker-Prüfstand besteht 111 isolierte Checks; er ist kein freigegebener
Paketstand und deckt den automatischen Coop-Ready-Übergang noch nicht ab.

Beschuss, Projektiltreffer und Seuche folgen wie entschieden der tatsächlichen B-Bodenhöhe.
Gezielte native Fixtures vergleichen B-Pech mit normalem Boden derselben Höhe: vollständige
Pfeilschadensrechnung an beiden Trefferbandgrenzen, Seuchenzulassung für Nachbarn und Außenring
an der strikten Grenze 120 sowie Fernkampf-Höhenargumente und Korrekturmarker bei ±70.
Der ±70-Marker ist kein pauschales Zielverbot. Sicht-/Zielprädikate im Fernkampf und Audio-/HUD-
Senken sind isoliert; die Seuchenfixture zeichnet die Schadenszulassung auf. Die Fixtures
belegen keine tatsächliche Sichtlinie, Noesis-Darstellung oder Spielabnahme.

Auf ausdrücklichen Nutzerwunsch baut dieser Chat APIShared nicht. Isolierte Broker-/Hooktests,
Quellprüfungen und ein Compile-only-Test des vollständigen Testmodcodes mit einem ausdrücklich
als Testdouble gekennzeichneten Brokervertrag ersetzen keinen vollständigen Paketbuild.
Die vorhandene APIShared-Paketausgabe 0.24.0 enthält den neuen Broker noch nicht; ein geprüfter
Verbraucherbuild benötigt den späteren passenden SDK-Stand aus dem parallel bearbeiteten APIShared.
Es wurde nichts installiert, veröffentlicht oder in einen Hauptmod übernommen.

Offen bleiben der vollständige Build mit passendem SDK, die neuen Lifecycle-/Spielabnahmen
(Neuspiel, Kartenpech, manueller/AIV-Überbau, Editor, wiederholtes Speichern/Laden und Feuer an
Höhengrenzen), aktive APIShared-Seuchenregeln sowie ein echter Host-/Clientlauf mit identischem
Paket und gezielt einseitig verweigerter Initialisierung. Erst danach ist die Hauptmod-Freigabe
begründet. Alte +4-Treppen ohne Pechflag werden weiterhin nicht repariert.

### 23.4 Zusätzliche Schlussprüfung des neuen Codes

Die unabhängige Codeprüfung umfasste noch einmal alle nativen Emitter, Aktivierungs-/Fehlerpfade,
Rosteridentitäten, verwaltete Hookreihenfolgen und verzögerte Veröffentlichungen. Dabei wurden
zusätzliche Randfälle im neuen Startschutz gefunden und vor Abschluss korrigiert:

- Direkte Aufrufe von Fremdthreads verweigern den Übergang, bevor sie Unity-/Lobbyzustand lesen,
  terminale Sperren ändern oder einen vorbereiteten Save abbrechen. Echte Workerthread-Tests
  prüfen diese ausbleibenden Nebenwirkungen.
- Veraltete öffentliche Queries dürfen weder einen Save einer anderen Lobby noch einen anderen
  Save derselben Lobby abbrechen. Absolute Archivpfade behalten ihre Verzeichnisidentität.
- Unready und Unlock bleiben auch dann möglich, wenn eine andere Startschutzvoraussetzung fehlt.
- Eine dauerhaft gesperrte Publikation bleibt über einen eigenen Statusprovider sichtbar;
  später passende Peerberichte dürfen nicht irrtümlich eine automatische Ready-Meldung auslösen.
- Automatisches Coop-Ready durch `FRONT_Multiplayer.doOpen` ruft den Button-Hook nicht auf.
  Ein zusätzlicher Hook an `SetMemberReadyState` wurde untersucht, aber aus dem aktiven Stand
  wieder herausgenommen: Der Coop-Host hat keinen entsprechenden Ready-Button, sodass eine
  abgelehnte automatische Bereitschaft ohne gesonderten Wiederaufnahmepfad hängen bleiben kann.
  Der Entwurf bleibt als Analysepatch erhalten; dieser Teil ist ausdrücklich nicht abgeschlossen.
- BFQoL verwirft den Transferzustand vor Lokalisierung, UI-Ausgabe oder Logging, damit Fehler
  dieser Ausgaben keinen abgelehnten Versuch für späte ACKs aktiv lassen.

Diese zusätzlichen Prüfungen ändern die offenen Build-/Spiel-/MP-Abnahmen aus 23.3 nicht.

### 23.5 Neubewertung des MP-Umfangs auf Nutzerwunsch

Identische Mod-/DLL-Dateien garantieren keinen identischen Erfolg von OS-Speicheroperationen.
Die installierte RedBird-Implementierung benötigt einen ausführbaren 64-KiB-Slab innerhalb
±1,75 GiB des Hookziels. Allokations- oder Speicherschutzfehler können prozessabhängig sein.
ASLR allein verändert dagegen weder RVAs noch Instruktionsspannen und ist kein belegter
Fehlergrund. Die deterministischen Vertragsprüfungen sollten bei identischen Livebytes
gleich ausfallen; eine feste Hooküberschneidung betrifft grundsätzlich beide Installationen.

Ein asymmetrischer Installationsausfall ist damit technisch möglich, für PitchStairs aber
weder auf Host/Client reproduziert noch als wahrscheinlich belegt. Daraus folgt kein Nachweis
eines normalen Desyncs bei beidseitig erfolgreicher Installation. Der allgemeine Broker ist
ein zusätzlicher Fehlerfallschutz mit deutlich größerem Start-/Save-/UI-Umfang als die nativen
Fehlerkorrekturen. Die Freigabe dieses Umfangs wird nach dem neuen Nutzerwunsch gesondert
entschieden. Empfehlung für den kleinsten Risikoumfang: bestätigte native Korrekturen, frühe
atomare Validierung, NetworkMode 1 und echte passende Host-/Clientabnahme priorisieren.
Alle bisherigen Quellen und Nachweise bleiben erhalten; nichts wurde installiert.

### 23.6 Abschließende Nutzerentscheidung: nativen Umfang fertigstellen

Der Nutzer hat nach der Risikobewertung ausdrücklich entschieden, die bestätigten nativen
Korrekturen abzuschließen und den umfassenden MP-Startschutz separat zurückzustellen.
Broker, Peerbericht-/Statusseite und zugehörige Eingriffe in Ready-/Start-/Savewege gehören
daher nicht mehr zum aktiven Quellstand. Die Entwürfe, Patches, Originalpfad-/Hashmanifeste
und der isolierte 111-Check-Nachweis liegen unter `.work/a/pitch-fix/001/deferred-mp`.
Die ungeprüfte zusätzliche AutoReady-Erweiterung liegt separat als Analysepatch vor.
Fremde, parallel bearbeitete APIShared-Änderungen bleiben erhalten.

Aktiv bleiben die 14 hashgebundenen nativen Sites, die Markerkarte, der niedrige Kettenvergleich,
die frühe Tilemanager-Prüfung und die unveränderliche Regel nach erfolgreicher Installation.
Der Plugin-Lifecycle verwurzelt veröffentlichte Kandidaten auch im Fehlerpfad. Testmod 0.1.1
unterscheidet diese korrigierte Regel vom bisherigen 0.1.0; NetworkMode 1 bleibt gesetzt.
Die bisherige APIShared-Mindestversion 0.23.0 genügt weiter. Der Abschlussbuild verwendet den
vorhandenen installierten SDK, baut APIShared nicht und installiert keine Ausgabe.

Die native Maschinenprüfung umfasst weiterhin 11361 Checks; die zusätzlichen Broker- und
Peerbericht-Checks sind nur Nachweise des zurückgestellten Entwurfs. Echte Spiel-/Editor-/Save-
und Host-/Clientabnahme bleiben erforderlich. Die Hauptmod-Übernahme ist nicht Teil dieses Schritts.
