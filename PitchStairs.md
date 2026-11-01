# Pech vor Treppen und KI-Gebäudeabriss in SHCDE

Stand: 01.11.2026. Sammlung der bisherigen Untersuchung; noch kein implementierter Fix.

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

Diese Datei sichert die bisherigen Beobachtungen, Codebefunde, Dateiidentitäten, Gegenproben und Korrekturen. Die Erstellung dieser Datei umfasst keine Codeänderung, keinen Build, keine Änderung an den übergebenen Dateien und keine Veröffentlichung.
