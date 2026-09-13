# LordAttackChecks

## Zweck und Ausgangsbeobachtung

Dieses Dokument sammelt bestätigte Vanilla-Befunde zum KI-Angriffsverhalten nach einem Burgdurchbruch.

- Vor dem DE-Winter-Update lief laut Fehlerbericht nahezu die gesamte Belagerungsarmee zum gegnerischen Lord.
- Aktuell greift standardmäßig nur eine kleine Gruppe den Lord an. Die übrigen Gruppen zerstören nacheinander normale Gebäude wie Holzfällerhütten und Apfelfarmen.
- Sind keine passenden Gebäude mehr vorhanden, folgen die Gruppen vorberechneten Angriffs-/Sammelvektoren.
- `Aggressive Siege` beziehungsweise „Aggressive AI“ vergrößert die Lord-Gruppe und verteilt die übrigen Gruppen eher auf verschiedene Gebäude.

## Analysebasis

- Installierte `CrusaderDE.dll`: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Der Hash stimmt mit `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` überein.
- Historische Workspace-Vergleichs-DLL: SHA-256 `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`.
- Semantische Baseline: Script Extender 2.6.0, Commit `2cee24e33b5a5d81d1c275efabc714ac59917b7b`.

Die Baseline führt viele Funktionen noch als `candidate`. Die nachfolgend beschriebenen RVAs, Bytes, Aufrufe und Datenflüsse wurden zusätzlich direkt im Disassembly geprüft.

## Begriffe und Zahlen

- Ein **Tribe** ist eine vollständige, gemeinsam befehligte Einheitengruppe. Seine Rolle legt die Aufgabe fest, nicht eine konkrete Truppengattung wie Schwertkämpfer. Die tatsächliche Zusammensetzung hängt von AI und AIC ab.
- Eine **Rolle** ist Vanillas numerische Aufgabenkategorie für einen Tribe, zum Beispiel `192 = SiegeWallTribe` (Mauerangriffsgruppe). Ein **Slot** ist ein reservierter Platz für einen Tribe dieser Rolle.
- Ein **State** ist eine nummerierte Phase des KI-Angriffs-Zustandsautomaten.
- Eine **RVA** ist der Offset einer nativen Funktion oder Variable innerhalb der geladenen `CrusaderDE.dll`.
- Gebäude-, Unit- und Tribe-IDs bezeichnen konkrete Spielobjekte; Gebäudetypen wie `3` und `32` bezeichnen dagegen eine Gebäudeklasse.

## Relevante Funktionen

| RVA | Bestätigte Rolle |
| --- | --- |
| `0x3C2E0` | Zustandsautomat des KI-Angriffs |
| `0xCF360`, `0xCF400` | Zugangs-/Erreichbarkeitsprüfungen |
| `0xCF020` | erneuert Angriffsdaten und Angriffsvektoren |
| `0x3B450` | verteilt Angriffstribes nach einem Durchbruch |
| `0x187E60` | validiert den Ziel-Lord und liefert seine 1-basierte Unit-ID |
| `0x30E90` | behält oder wählt das Gebäudeziel eines Tribes |
| `0x2C620` | strikt prioritätsbasierte Gebäudeauswahl |
| `0x2C710` | entfernungsgewichtete Gebäudeauswahl |
| `0x11B520` | bewegt einen vollständigen Tribe |
| `0x199C00` | erzeugt Befehlsmodus 9 (Gebäude angreifen) mit Building-ID/UID |

## Zustandsfluss

1. State 4 (**Sturmangriff auf die Burg**) prüft über `0xCF360`, ob ein Zugang zum Ziel besteht. Bei Erfolg folgt State 6 (**Burg durchbrochen**), andernfalls State 5 (**Mauern und Durchbruch angreifen**).
2. State 5 (**Mauern und Durchbruch angreifen**) bearbeitet Befestigungen und prüft erneut über `0xCF360` und `0xCF400`. Ein gefundener Zugang führt zu State 6 (**Burg durchbrochen**).
3. State 6 (**Burg durchbrochen; Truppen hineinschicken**) ruft `0xCF020` und danach `0x3B450` auf.
4. `0x3B450` iteriert gespeicherte 1-basierte Tribe-IDs, prüft deren UID und verteilt jeden gültigen Tribe auf Lord, Gebäude oder Angriffsvektor.
5. State 6 (**Burg durchbrochen**) kann die Zuteilung wiederholt ausführen und bestehende Befehle erneut setzen.

Ein fehlgeschlagener Lord-Bewegungsauftrag lässt `0x3B450` mit Fehler zurückkehren. Erst wenn anschließend beide Erreichbarkeitsprüfungen scheitern, fällt die FSM nach State 5 (**Mauern und Durchbruch angreifen**) zurück. Rückzug und Auflösung erfolgen über getrennte spätere Zustände.

Kurzübersicht der gesamten FSM:

| State | Bedeutung |
| --- | --- |
| 0 | untätig; Angriff auswählen oder auf Verbündete warten |
| 1 | Angriff vorbereiten und Belagerungspfad bestimmen |
| 2 | Truppen sammeln und Belagerung aufbauen |
| 3 | zur Burg vorrücken |
| 4 | Sturmangriff auf die Burg |
| 5 | Mauern und Durchbruch angreifen |
| 6 | Burg durchbrochen; Truppen hineinschicken |
| 7 | Ziel-Lord verschwunden; Übergangswartezeit |
| 8 | Angriff abbrechen und zurückziehen |
| 9 | Angriffsgruppen auflösen und FSM zurücksetzen |

## Ursache in `RVA 0x3B450`

Die Routine verarbeitet vollständige AI-Tribes, keine einzelnen Soldaten. Ihre statische Tabelle enthält Rolle und maximale Slotanzahl:

| Rolle | Max. Tribes | Verständliche Bedeutung |
| --- | ---: | --- |
| 192 | 8 | `SiegeWallTribe`: Mauerangriffsgruppen |
| 190 | 2 | `SiegeReserveTribe`: Belagerungsreserve |
| 12 | 1 | interne Belagerungsrolle; genaue Zusammensetzung unbekannt |
| 11 | 1 | interne Belagerungsrolle; genaue Zusammensetzung unbekannt |
| 186 | 3 | `SiegeCoverTribe`: Deckungs-/Unterstützungsgruppen |
| 189 | 1 | interne Angriffsrolle; genaue Bedeutung unbekannt |
| 15 | 2 | `SiegeStormTribe`: Sturmgruppen |
| 17 | 1 | interne Angriffsrolle; genaue Bedeutung unbekannt |
| 18 | 1 | Belagerungsingenieure; wird hier übersprungen |
| 14 | 1 | Tunnelgräbergruppe; wird hier übersprungen |
| 13 | 1 | defensive Belagerungswelle; wird hier übersprungen |

Rolle 186 (`SiegeCoverTribe`, Deckungs-/Unterstützungsgruppe) darf außerdem nicht in den Lord-Zweig. Die Bezeichnungen beschreiben den Auftrag des Tribes, nicht dessen Einheitentypen.

Vereinfachter Kontrollfluss:

```text
lordQuota = 0

if siege_wall_tribes > 2
   and (GlobalMoreAggressiveSiege != 0
        or (use_improved_sieging & 2) != 0):
    lordQuota = ceil(siege_wall_tribes / 2)

lordCommandSucceeded = false

for each eligible tribe role and valid tribe slot:
    sendToLord =
        not lordCommandSucceeded
        or (role == SiegeWallTribe and slotIndex < lordQuota)

    if sendToLord and targetLordAlive and role != SiegeCoverTribe:
        if moveWholeTribeToLordCoordinates fails:
            return failure
        lordCommandSucceeded = true
    else if assignOrRetainBuildingTarget fails:
        moveWholeTribeTowardPreparedAttackVector

return success
```

Ohne aggressive Wirkung ist `lordQuota` null. Nach dem ersten erfolgreichen Lord-Auftrag ist `lordCommandSucceeded` wahr; fast alle weiteren Tribes werden deshalb zu `0x30E90` weitergeleitet. Der Merker bedeutet lediglich „ein Lord-Auftrag war erfolgreich“, wirkt aber wie „es greifen bereits genügend Gruppen den Lord an“.

Das ist die unmittelbar belegte Ursache des aktuellen Verhaltens.

## Gebäudeziele und Sammelpunkt-Fallback

`0x30E90` behält ein vorhandenes Gebäudeziel, solange Building-ID, UID und Zerstörungszustand gültig sind. Andernfalls wird neu gewählt:

- `0x2C620` wählt das erste vorhandene Gebäude der höchsten verfügbaren Tabellenpriorität.
- `0x2C710` minimiert `Distanz vom Tribe-Leader + 2 * Prioritätsindex`.
- Ein Treffer erzeugt über `0x199C00` den internen Befehlsmodus 9 (**Gebäude angreifen**) für den gesamten Tribe.

Die Auswahl verwendet drei Tabellen mit jeweils 47 Gebäudetypen. Für die Beobachtung relevant:

- Gebäudetyp `3`: Holzfällerhütte.
- Gebäudetyp `32`: Apfelfarm.
- Apfelfarmen und Holzfällerhütten stehen in den Tabellen weit vorne.
- Keeps, Gatehouses, Burgtürme, Keep-Türen und Mauern fehlen.

`0x2C620` schaltet den synchronisierten Zufallszustand nicht weiter. Mehrere nacheinander verarbeitete Tribes verwenden daher häufig dieselbe Tabelle und dasselbe erste passende Gebäude. Das erklärt das kollektive serielle Abarbeiten.

Findet `0x30E90` kein Gebäude, wählt `0x3B450` einen von `0xCF020` vorberechneten Angriffsvektoren. Rolle 186 (`SiegeCoverTribe`, Deckungs-/Unterstützungsgruppe) beginnt vier Einträge später. Liegt der Tribe nicht bereits nahe genug am Punkt, bewegt `0x11B520` ihn dorthin. Dieser Fallback wählt den Lord nicht erneut und passt zum beobachteten Sammelpunktverhalten.

## Vanilla-Optionen

### `Improved Siege`

- Globale Variable: `RVA 0x3665FB8`.
- Gameplayrelevante Leser: `0x3B820`, `0x3BFA0`, `0x3C0A0`, `0x3C9C0`.
- Die Leser laufen in State 5 (**Mauern und Durchbruch angreifen**) für die noch nicht genauer benannten internen Belagerungsrollen 11 und 12 sowie `SiegeWallTribe` und `SiegeReserveTribe`.
- Der interne Gruppenverhaltenscode wechselt von `0x3F2` (normale Zielsuche) auf `0x420` (verbesserte Belagerungszielsuche).
- Modus `0x420` versucht über `0x111330` zuerst eine erweiterte, entfernungsabhängige Tor-/Turmzielsuche und verwendet bei Fehlschlag `0x111D90`.
- Modus `0x3F2` verwendet ausschließlich `0x111620`.

`Improved Siege` beeinflusst damit die Befestigungsbearbeitung in State 5 (**Mauern und Durchbruch angreifen**). Es ändert weder die Lord-Quote in `0x3B450` noch die Gebäudeauswahl in `0x30E90`.

### `Aggressive Siege` / „Aggressive AI“

- Globale Variable: `RVA 0x3665FBC`.
- Einzige gameplayrelevante Leser: `0x3B450` und `0x30E90`.
- Bei `siege_wall_tribes > 2` setzt `0x3B450` die Quote `ceil(siege_wall_tribes / 2)`.
- Die Quote wird nur gegen die maximal acht Slots der ersten Rolle `SiegeWallTribe` geprüft. Sie ist keine Anzahl einzelner Soldaten und keine Quote der gesamten Armee.
- Die wiederholte Ausführung von State 6 (**Burg durchbrochen**) kann Aufträge erneut setzen. Eine ausdrücklich durch Verluste ausgelöste Nachbesetzung ist nicht belegt.
- `0x30E90` wechselt von `0x2C620` zu `0x2C710`.
- `0x2C710` wählt anhand der Tribe-Leader-Position und schaltet den synchronisierten Zufallszustand pro Auswahl weiter. Dadurch können Tribes unterschiedliche Gebäude erhalten.

### AIC-Feld `use_improved_sieging`

Das Feld am Offset `0x2F4` ist eine Bitmaske:

- Bit 0 wirkt in den geprüften Pfaden von State 5 (**Mauern und Durchbruch angreifen**) wie die globale Option `Improved Siege`.
- Bit 1 wirkt in `0x3B450` und `0x30E90` wie die globale Option `Aggressive Siege`.

Globale Optionen und individuelle AIC-Einstellung sind getrennte Quellen und im jeweiligen Branch per logischem ODER verbunden.

## Historischer Abgleich

Der entsprechende SHC-HD-Code `sendUnitsToAttackBreachedCastle` besitzt ebenfalls die Begrenzung auf zunächst einen Lord-Tribe. UCPs Patch `ai_attackwave_lord_edit` entfernt genau diesen Branch und beschreibt die Wirkung als „when a breach happens, send most troops to enemy lord“. Das bestätigt die semantische Zuordnung des DE-Branches.

Die Ursache des aktuellen Verhaltens ist damit bestimmbar. Welche Änderung das sichtbare Verhalten beim Winter Update auslöste, bleibt ohne echte Vor-Winter-DLL offen.

## Empfohlener Fix

Der kleinste und vertragsgetreueste Eingriff liegt bei `RVA 0x3B5DB`:

```asm
0x3B5D9  test ebp, ebp
0x3B5DB  je   0x3B5EF
```

Originalbytes:

```text
85 ED 74 12
```

Empfohlene Änderung:

```text
85 ED EB 12
```

Der Wechsel `JE` zu `JMP` führt jeden gültigen, geeigneten Tribe zunächst durch Vanillas Lord-Zweig. Er erhält dabei:

- die Prüfung auf einen lebenden Ziel-Lord,
- die Tribe-ID-/UID-Prüfung,
- den Ausschluss von `SiegeCoverTribe`,
- Vanillas vollständige Tribe-Bewegung über `0x11B520`,
- Vanillas Rückgabewert und Fehlerbehandlung,
- Gebäude- und Vektorverhalten, wenn kein lebender Lord existiert.

Die Umsetzung sollte ausschließlich als schaltbarer Ein-Byte-Patch erfolgen und vor dem Schreiben den vollständigen DLL-Hash, ein eindeutiges umgebendes AOB-Muster und die erwarteten Bytes `74 12` prüfen. In Multiplayerpartien muss die Einstellung auf allen Teilnehmern identisch und hostverwaltet sein.

## Offene Nachweise

- Eine echte Vor-Winter-DLL ist nötig, um die einführende Änderung historisch zu bestimmen.
- Ein gezielter Laufzeittrace sollte pro Aufruf von State 6 (**Burg durchbrochen**) Tribe-ID, Rolle, Slot, Lord-Zweig, Gebäudeziel und Fallbackkoordinate protokollieren.
- Die sichtbare Zuordnung einzelner Fallbackpunkte zum Lagerfeuer muss dynamisch bestätigt werden.
