# LordAttackChecks

## Ergebnis

- Das heutige Verhalten ist real: Ohne aggressive Option erhält nur der erste geeignete vollständige Tribe einen Bewegungsauftrag zum Lord. Fast alle weiteren Tribes greifen normale Gebäude an.
- Diese Ein-Tribe-Begrenzung ist **keine Winter-Update-Änderung**. Die echte Vor-Winter-DLL enthält denselben Kontrollfluss.
- Holzfällerhütte (`Gebäudetyp 3`) und Apfelfarm (`Gebäudetyp 32`) waren bereits vor dem Update Gebäudeziele. Burgkerne waren auch damals nicht in den drei Prioritätslisten enthalten.
- Neu sind `Aggressive Siege`/„Aggressive AI“, die Aufteilung von `use_improved_sieging` in Bits und kleine Anpassungen für neue Beduinen-Gebäude.
- Innerhalb der vollständig geprüften Lord-/Gebäude-/Erreichbarkeitskette wurde keine Änderung gefunden, die den gemeldeten Standard-Regressionssprung erklärt.

## Analysebasis

- Vor-Winter-DLL aus Depotordner `24816905`: SHA-256 `2A7BD065A00F7A14408C1586BD6F499536CF1AD97E67319EEE28E3881C870C35`.
- Aktuell installierte DLL: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Der aktuelle Hash stimmt mit `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` überein.
- Historische Workspace-Vergleichs-DLL `17F8DD4A…` bleibt eine getrennte Vergleichsquelle und ist nicht die hier geprüfte Vor-Winter-DLL.
- Sämtliche Vor-Winter-Exporte, Ghidra-Daten und Diffs liegen ausschließlich unter `_inspect/CrusaderDE-PreWinter-24816905/2A7BD065A00F7A14408C1586BD6F499536CF1AD97E67319EEE28E3881C870C35/`.
- HD-Vergleich: Ghidra-Programm `Stronghold Crusader.exe`, x86, Image Base `0x400000`. SHA-256 des vorhandenen `.gzf`-Archivs: `9069E32DD2B6FEFFEE39E0B12EA9CF17CE68B7CF93AA8B4C7291969298D4E2A0`; dies ist kein Hash der ursprünglichen EXE.

Automatischer Versionsabgleich: 3.728 `confirmed`, 27 `probable`, 1.243 unveränderte, 2.512 geänderte, 718 entfernte und 723 neue Funktionen. Alle nachfolgenden Kernbefunde wurden zusätzlich direkt im Decompiler und Disassembly geprüft.

## Begriffe

- **Tribe:** vollständige, gemeinsam befehligte Einheitengruppe; nicht ein einzelner Soldat.
- **Rolle:** numerische Aufgabe eines Tribes, zum Beispiel `192 = SiegeWallTribe` (Mauerangriffsgruppe). Sie bezeichnet keine feste Truppengattung wie Schwertkämpfer.
- **Slot:** reservierter Speicherplatz für einen Tribe einer bestimmten Rolle.
- **State:** nummerierte Phase des KI-Angriffs-Zustandsautomaten.
- **RVA:** Offset einer Funktion oder Variable innerhalb der jeweiligen DLL. Alte und aktuelle RVAs dürfen nicht vertauscht werden.
- **ID/UID:** ID bezeichnet das Spielobjekt; die UID bestätigt, dass der Slot noch dasselbe Objekt enthält.

## Geprüfte Funktionskette

| Aufgabe | Vor Winter | Aktuell | Befund |
| --- | ---: | ---: | --- |
| Angriffs-FSM | `0x3A510` | `0x3C2E0` | `confirmed`, normalisierter Code identisch |
| Angriffsdaten/-vektoren erneuern | `0xCCC40` | `0xCF020` | `confirmed`, identisch |
| Erreichbarkeit prüfen | `0xCCF80`, `0xCD020` | `0xCF360`, `0xCF400` | direkt geprüft, identisch |
| Angriffstribes verteilen | `0x39700` | `0x3B450` | direkt geprüft; aggressive Erweiterung neu |
| Ziel-Lord auflösen | `0x185690` | `0x187E60` | `confirmed`, identisch |
| Gebäudeziel zuweisen/halten | `0x30BB0` | `0x30E90` | direkt geprüft; aggressive Erweiterung neu |
| normale Gebäudeauswahl | `0x2C5A0` | `0x2C620` | `confirmed`, Code identisch |
| aggressive Gebäudeauswahl | nicht vorhanden | `0x2C710` | neu |
| vollständigen Tribe bewegen | `0x118F10` | `0x11B520` | `confirmed`, identisch |
| Gebäudeangriff befehlen | `0x197420` | `0x199C00` | direkt geprüft, identisch |
| Tribe-Rolle zuweisen | `0x29450` | `0x294B0` | `confirmed`, identisch |
| passenden/kleinsten Tribe wählen | `0x2B850` | `0x2B8D0` | `confirmed`, identisch |
| angreifbares Gebäude registrieren | `0x29130` | `0x29190` | `confirmed`, identisch |

Auch die für diesen Pfad verwendete Rollen-Zuordnung sowie die 11 Paare aus Rolle und maximaler Slotanzahl sind bytegleich. Eine neue Aufteilung der Armee in mehr Tribes ist in dieser Kette daher nicht belegt.

## Zustandsfluss

1. State 4 (**Burg stürmen**) prüft den Zugang zum Ziel. Bei Erfolg folgt State 6, sonst State 5.
2. State 5 (**Mauern und Durchbruch angreifen**) bearbeitet Befestigungen und prüft den Zugang erneut.
3. State 6 (**Burg durchbrochen; Truppen hineinschicken**) erneuert die Angriffsvektoren und verteilt die Tribes auf Lord, Gebäude oder einen Vektorpunkt.
4. Dabei werden gespeicherte 1-basierte Tribe-IDs und ihre UIDs geprüft. Der Auftrag gilt jeweils für den ganzen Tribe.
5. State 6 kann erneut ausgeführt werden und bestehende Aufträge erneut setzen.

| State | Leserfreundliche Bedeutung |
| ---: | --- |
| 0 | untätig; Angriff wählen oder auf Verbündete warten |
| 1 | Angriff vorbereiten und Belagerungspfad bestimmen |
| 2 | Truppen sammeln und Belagerung aufbauen |
| 3 | zur Burg vorrücken |
| 4 | Burg stürmen |
| 5 | Mauern und Durchbruch angreifen |
| 6 | Burg durchbrochen; Truppen hineinschicken |
| 7 | Ziel-Lord verschwunden; kurz warten |
| 8 | Angriff abbrechen und zurückziehen |
| 9 | Angriffsgruppen auflösen und zurücksetzen |

## Verteiler nach dem Durchbruch

Die Rollentabelle ist vor und nach dem Update identisch:

| Rolle | Max. Tribes | Bedeutung |
| ---: | ---: | --- |
| 192 | 8 | `SiegeWallTribe`: Mauerangriffsgruppen |
| 190 | 2 | `SiegeReserveTribe`: Belagerungsreserve |
| 12 | 1 | interne Belagerungsrolle; genaue Aufgabe nicht bestätigt |
| 11 | 1 | Ingenieursgruppe |
| 186 | 3 | `SiegeCoverTribe`: Deckungs-/Unterstützungsgruppen |
| 189 | 1 | interne Angriffsrolle; genaue Aufgabe nicht bestätigt |
| 15 | 2 | `SiegeStormTribe`: Sturmgruppen |
| 17 | 1 | interne Angriffsrolle; genaue Aufgabe nicht bestätigt |
| 18 | 1 | spezielle Belagerungs-/Zelt-Ingenieursgruppe; hier übersprungen |
| 14 | 1 | interne Belagerungsrolle; hier übersprungen |
| 13 | 1 | interne Belagerungsrolle; hier übersprungen |

Rolle 186 (`SiegeCoverTribe`) wird verarbeitet, darf aber nicht in den Lord- oder Gebäudezweig und fällt dadurch auf einen Angriffsvektor zurück.

Vereinfachter aktueller Kontrollfluss:

```text
lordQuota = 0

if siege_wall_tribes > 2
   and (GlobalAggressiveSiege != 0
        or (use_improved_sieging & 2) != 0):
    lordQuota = ceil(siege_wall_tribes / 2)

lordCommandSucceeded = false

for each valid tribe slot:
    sendToLord =
        not lordCommandSucceeded
        or (role == SiegeWallTribe and slotIndex < lordQuota)

    if sendToLord and targetLordAlive and role != SiegeCoverTribe:
        moveWholeTribeToLordCoordinates
        lordCommandSucceeded = true
    else if assignOrRetainBuildingTarget fails:
        moveWholeTribeTowardPreparedAttackVector
```

Ohne aggressive Wirkung ist `lordQuota` gleich `0`. Nach dem ersten erfolgreichen Lord-Auftrag gehen fast alle weiteren Tribes zum Gebäudezweig. Genau dieselbe Ein-Tribe-Sperre existiert vor Winter in `0x39700`; nur die zusätzliche Quote fehlt dort.

## Gebäudeziele und Vektor-Fallback

- Der normale Selektor wählt das erste vorhandene Gebäude mit der höchsten Tabellenpriorität. Mehrere Tribes erhalten dadurch häufig dasselbe Ziel und arbeiten Gebäude seriell ab.
- Der aggressive Selektor minimiert `Distanz vom Tribe-Leader + 2 × Prioritätsindex`. Dadurch können sich Tribes auf verschiedene Gebäude verteilen.
- Ein vorhandenes Ziel bleibt bestehen, solange Building-ID, UID und Zerstörungszustand gültig sind.
- Ein Treffer erzeugt Befehlsmodus 9 (**Gebäude angreifen**) für den vollständigen Tribe.
- Ohne Gebäudeziel folgt der Tribe einem vorberechneten Angriffsvektor. Dieser Fallback versucht den Lord nicht erneut.

Jede Prioritätsliste besitzt 47 verwendete Einträge. Der Versionsunterschied ist klein:

- Liste A: vollständig identisch.
- Liste B: aktuell wurde Beduinen-Stockade (`108`) eingefügt; der alte letzte Eintrag `190` entfiel. `190` ist kein normales platzierbares Bauziel.
- Liste C: aktuell wurde Beduinen-Stockade (`108`) eingefügt; der alte letzte Eintrag Ruinen (`39`) entfiel.
- Holzfällerhütte (`3`) und Apfelfarm (`32`) stehen in beiden Versionen in den Listen.
- Keeps (`40` bis `44`), Gatehouses, Burgtürme, Keep-Türen und Mauern fehlen in beiden Versionen.

## Vanilla-Optionen

### `Improved Siege`

Die gelegentlich „Imprived Siege“ geschriebene Option heißt `Improved Siege`.

- Vor Winter: globale Variable `0x3662F08`; Leser `0x39A50`, `0x3A1D0`, `0x3A2D0`, `0x3ABF0`.
- Aktuell: globale Variable `0x3665FB8`; Leser `0x3B820`, `0x3BFA0`, `0x3C0A0`, `0x3C9C0`.
- Betroffen sind in State 5 die Rollen 11 (**Ingenieure**), 12 (**interne Belagerungsrolle**), 192 (`SiegeWallTribe`) und 190 (`SiegeReserveTribe`).
- Der Gruppenmodus wechselt von `0x3F2` (**normale Befestigungszielsuche**) auf `0x420` (**verbesserte Befestigungszielsuche**).
- Modus `0x420` versucht zuerst die breitere, entfernungsabhängige Auswahl über `0x111330` und danach den Fallback `0x111D90`; `0x3F2` verwendet `0x111620`.
- Die Option verändert weder die Lord-Zuteilung noch den normalen Gebäudezweig nach dem Durchbruch.

### `Aggressive Siege` / „Aggressive AI“

- Vor Winter nicht vorhanden.
- Aktuell: globale Variable `0x3665FBC`; einzige Gameplay-Leser `0x3B450` und `0x30E90`.
- Bei `siege_wall_tribes > 2` beträgt die Lord-Quote `ceil(siege_wall_tribes / 2)`.
- Die Quote gilt nur für die maximal acht Slots der Rolle 192 (`SiegeWallTribe`), nicht für einzelne Soldaten oder die ganze Armee.
- Der Gebäudezweig verwendet den neuen entfernungsgewichteten Selektor `0x2C710`.
- Wiederholtes State 6 setzt Aufträge erneut; eine ausdrücklich durch Verluste ausgelöste Nachbesetzungslogik existiert hier nicht.

### AIC-Feld `use_improved_sieging` bei Offset `0x2F4`

- Vor Winter wurde das gesamte Integer-Feld als Boolean gelesen: jeder Wert ungleich null aktivierte `Improved Siege`.
- Aktuell ist es eine Bitmaske: Bit 0 aktiviert die Teilwirkung von `Improved Siege`, Bit 1 die Teilwirkung von `Aggressive Siege`.
- Globale Lobbyoptionen und die individuelle AIC-Einstellung sind getrennte Quellen; der jeweilige Zweig verknüpft sie per logischem ODER.
- Die geprüften eingebauten Vor-Winter-Werte sind 0 oder 1. Dadurch bleibt deren bisherige `Improved Siege`-Bedeutung erhalten.

## Vergleich: HD-Vanilla und aktuelle DE

### Verständlicher Befund

- HD-Vanilla und die aktuelle DE verteilen die Truppen nach einem Durchbruch grundsätzlich gleich: Der erste geeignete vollständige Tribe greift den Lord an; fast alle folgenden Tribes erhalten Gebäudeziele.
- Beide Versionen behalten ein noch gültiges Gebäudeziel bei. Andernfalls wählen sie über eine von drei Prioritätslisten ein neues Ziel. Dadurch verhalten sich die übrigen Belagerungstruppen wie Überfalltruppen und arbeiten häufig dasselbe Wirtschaftsgebäude nacheinander ab.
- Rolle 186 (`SiegeCoverTribe`, Deckungsgruppe) darf weder den Lord noch ein Gebäude angreifen und folgt einem vorberechneten Angriffsvektor. Die Rollen 13, 14 und 18 werden in diesem Verteiler übersprungen.
- Findet der Gebäudezweig kein Ziel, folgt der Tribe ebenfalls einem Angriffsvektor. Weder HD noch DE versuchen in diesem Fallback erneut den Lord.
- Damit ist das Verhalten „nur ein Tribe zum Lord“ bereits in HD-Vanilla vorhanden und keine Eigenheit der DE.

### Zugehörige Funktionen

| Aufgabe | HD-Vanilla (VA) | Aktuelle DE (RVA) |
| --- | ---: | ---: |
| Angriffs-FSM | `0x4D49E0` | `0x3C2E0` |
| Angriffsvektoren berechnen | `0x4568B0` | `0xCF020` |
| Tribes nach dem Durchbruch verteilen | `0x4D30E0` | `0x3B450` |
| lebenden Ziel-Lord bestimmen | `0x5377F0` | `0x187E60` |
| Gebäudeziel halten oder zuweisen | `0x4CF920` | `0x30E90` |
| normales Gebäudeziel auswählen | `0x4CDB20` | `0x2C620` |
| entfernungsgewichtetes Gebäudeziel | nicht vorhanden | `0x2C710` |
| vollständigen Tribe bewegen | `0x5263A0` | `0x11B520` |
| Gebäudeangriff als Befehl 9 einreihen | `0x537160` | `0x199C00` |
| angreifbares Gebäude registrieren | `0x4CDA50` | `0x29190` |

HD ist ein 32-Bit-x86-Programm, DE eine 64-Bit-x64-DLL. Adressen und Instruktionsbytes sind deshalb nicht übertragbar; verglichen wird der Kontrollfluss.

### Codevergleich des Lord-Limits

HD-Vanilla, vereinfacht:

```text
lordCommandSucceeded = false

for each valid tribe slot:
    if lordCommandSucceeded or targetLordMissing or role == SiegeCoverTribe:
        assignOrRetainBuildingTargetOrUseAttackVector
    else:
        if moveWholeTribeToLordCoordinates fails:
            return failure
        lordCommandSucceeded = true
```

Aktuelle DE, vereinfacht:

```text
lordQuota = aggressiveSiege and configuredSiegeWallTribes > 2
    ? ceil(configuredSiegeWallTribes / 2)
    : 0

for each valid tribe slot:
    sendToLord =
        not lordCommandSucceeded
        or (role == SiegeWallTribe and slotIndex < lordQuota)

    if sendToLord and targetLordAlive and role != SiegeCoverTribe:
        moveWholeTribeToLordCoordinates
        lordCommandSucceeded = true
    else:
        assignOrRetainBuildingTargetOrUseAttackVector
```

Ohne `Aggressive Siege` ist `lordQuota` gleich `0` und der DE-Code entspricht funktional dem HD-Code. Die äquivalente Ein-Tribe-Sperre ist unterschiedlich kompiliert:

```asm
HD  VA  0x4D31CD: 75 49    jne Gebäudezweig
DE  RVA 0x3B5DB:  74 12    je  Lord-Zweig
```

Die Sprungrichtung ist wegen der umgekehrten Blockanordnung verschieden; beide Branches verhindern nach dem ersten erfolgreichen Lord-Auftrag weitere normale Lord-Aufträge.

### Tatsächliche Unterschiede

- HD durchsucht drei Gebäudeprioritätslisten mit je 46 Einträgen; die aktuelle DE verwendet je 47. DE kennt zusätzliche Gebäudetypen, darunter Beduinen-Stockade (`108`), die HD nicht besitzt.
- HD besitzt weder die Vanilla-Optionen `Improved Siege` und `Aggressive Siege` noch das AIC-Feld `use_improved_sieging` bei Offset `0x2F4`.
- HD verwendet in den geprüften State-5-Pfaden ausschließlich Gruppenmodus `0x3F2` (**normale Befestigungszielsuche**). Modus `0x420` (**verbesserte Befestigungszielsuche**) existiert dort nicht.
- Nur die aktuelle DE kann über `Aggressive Siege` mehrere Rolle-192-Tribes zum Lord schicken und für die übrigen Tribes den entfernungsgewichteten Gebäudeselektor `0x2C710` verwenden.

### UCP ist nicht HD-Vanilla

UCPs Option `ai_attackwave` enthält für den Lord-Angriff den Teilpatch `ai_attackwave_lord_edit`. Er sucht den HD-Code über ein AOB-Muster und ersetzt bei VA `0x4D31CD` genau den bedingten Sprung `75 49` durch `90 90` (**zwei NOPs; keine Operation**).

HD-Vanilla:

```text
if lordCommandSucceeded:
    useBuildingOrVectorFallback
else if targetLordMissing or role == SiegeCoverTribe:
    useBuildingOrVectorFallback
else:
    moveWholeTribeToLord
    lordCommandSucceeded = true
```

HD mit UCP:

```text
if targetLordMissing or role == SiegeCoverTribe:
    useBuildingOrVectorFallback
else:
    moveWholeTribeToLord
```

Der entfernte Sprung ist ausschließlich die Abfrage „wurde bereits ein Tribe zum Lord geschickt?“. Dadurch versucht jeder im Verteiler verarbeitete und geeignete Tribe den Lord-Auftrag, nicht nur der erste.

Unverändert bleiben:

- die Prüfung, ob der Ziel-Lord lebt;
- der Ausschluss von Rolle 186 (`SiegeCoverTribe`, Deckungsgruppe);
- das Überspringen der Rollen 13, 14 und 18;
- Tribe-ID/UID-Prüfung und Auftrag für den vollständigen Tribe;
- die vorhandene Bewegungs- und Erreichbarkeitslogik;
- Gebäude- beziehungsweise Angriffsvektor-Fallback für ungeeignete Tribes oder einen fehlenden Lord.

Das übergeordnete UCP-Modul ändert getrennt davon bereits vor dem Durchbruch die Angriffswellen. Mit dem Standardwert 7 verwendet es nacheinander viermal Mauerziele, zweimal Befestigungsziele und einmal normale Gebäudeziele. Außerdem überspringt es laut UCP-Quellkommentar die Prüfungen „Mauerteil bereits belegt“ und „Mauerteil bereits gebrochen“. Diese Änderungen stammen nicht aus `ai_attackwave_lord_edit`.

UCP und DEs `Aggressive Siege` sind daher nicht gleich: UCP entfernt das Lord-Limit vollständig. DE begrenzt zusätzliche Lord-Aufträge auf `ceil(configuredSiegeWallTribes / 2)` der Rolle 192 und verteilt die übrigen Tribes weiterhin entfernungsgewichtet auf Gebäude. Der UCP-Lord-Patch ist das passendere Vorbild für die empfohlene DE-Anpassung „alle geeigneten Tribes zum Lord“.

## Historische Schlussfolgerung

Die Vor-Winter-DLL und HD-Vanilla widerlegen die Annahme, die Ein-Tribe-Sperre sei mit dem Winter Update oder erst mit der DE eingeführt worden. UCPs `ai_attackwave_lord_edit` entfernt sie gezielt; ein entsprechend modifiziertes HD-Spiel kann daher das erinnerte Verhalten „gesamte Armee zum Lord“ erklären.

Statisch gesichert ist deshalb nur:

- Der aktuelle Verteiler erklärt das beobachtete Gebäude-Abarbeiten.
- Er ist nicht die historische Ursache des gemeldeten Winter-Regressionssprungs.
- Die tatsächlich neuen Lord-Angriffsänderungen sind additive aggressive Optionen, nicht eine Verschlechterung des Standardzweigs.

Für einen historischen Ursachennachweis wäre ein reproduzierbarer Laufzeitvergleich genau desselben Szenarios und derselben AIC mit beiden DLLs erforderlich. Dabei sollten pro State-6-Aufruf Spieler, Tribe-ID, Rolle, Slot, Tribe-Größe, Optionswerte, Lord-Zweig, Gebäudeziel und Fallbackkoordinate protokolliert werden.

## Empfohlene Verhaltensanpassung

Falls das gewünschte Ergebnis ausdrücklich „alle geeigneten Tribes greifen nach dem Durchbruch den Lord an“ lautet, bleibt der kleinste aktuelle Eingriff ein schaltbarer Ein-Byte-Patch in `0x3B450`:

```asm
0x3B5D9  test ebp, ebp
0x3B5DB  je   0x3B5EF
```

`74 12` wird zu `EB 12`. Dadurch durchläuft jeder gültige geeignete Tribe zunächst Vanillas Lord-Zweig; Lord-Prüfung, UID-Prüfung, Ausschluss von `SiegeCoverTribe`, Bewegungsfunktion und Fehlerpfad bleiben erhalten. Das ist eine bewusste Verhaltensverbesserung nach UCP-Vorbild, **keine Wiederherstellung des statisch vorgefundenen Vor-Winter-Codes**.

Vor dem Schreiben sind vollständiger DLL-Hash, eindeutiges AOB-Umfeld und Originalbytes zu prüfen. In Multiplayerpartien muss die Einstellung hostverwaltet und bei allen Teilnehmern identisch sein.
