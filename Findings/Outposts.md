# Outposts: Vanilla-Audit und technische Machbarkeit

Stand: 16.09.2026. Untersuchungsgegenstand: Editor-Zusammensetzung, Gebäude-HUD im Spiel, Sammelpunkte je Einheitentyp und Haltung neuer menschlicher Truppen. Es wurde kein Runtime-Mod und keine UI implementiert und kein Spieltest durchgeführt.

## Ergebnis und Reichweite

**Individuelle Truppenanteile sind technisch grundsätzlich umsetzbar, aber keine vorhandene Outpost-Einstellung.** Vanilla wählt aus acht festen Profilen je Fraktion. Ein Prozentmodell benötigt zusätzliche Daten und einen Eingriff in die Outpost-Erzeugung. Ein globaler Austausch der Profiltabelle oder ausschließlich `OnUnitCreate` erfüllt das gewünschte Verhalten nicht vollständig.

**Ein eigenes HUD und Sammelpunkte je Typ sind ebenfalls grundsätzlich machbar.** Der native Gebäudeauswahlpfad sperrt das Outpost-Panel im Spiel; die normalen Kasernen-Sammelpunkte sind spielerweit und nicht pro Gebäude. Beides erfordert eine gezielte Erweiterung. Lord-HUD ist ein hilfreiches UI-Vorbild, aber kein Beweis, dass derselbe Aufruf ein Gebäude korrekt auswählt.

**Noch keine vollständige Implementierungsfreigabe:** Der statische Befund beschreibt die relevanten Vanilla-Pfade und konkrete Ansatzstellen. Einige unten ausdrücklich benannte Verträge sind noch nicht bis zum Laufzeitnachweis geschlossen: insbesondere echte Laufbewegung, beständige Haltung bei Sondertypen, KI-Verwendung neuer Gruppenzusammensetzungen, native Persistenz aller Zusatzfelder und Multiplayer-Roundtrip. Diese Grenzen dürfen bei der späteren Umsetzung nicht durch Annahmen ersetzt werden. Es gibt hier keine validierte Inline-Hookspanne und keine Aussage, dass alle denkbaren Einheiten bereits getestet oder uneingeschränkt kompatibel sind.

Festgelegtes Ziel: Zusammensetzung möglichst für Menschen und KI; Sammelpunkte und Haltung ausschließlich für menschliche Besitzer. KI-Bewegung und KI-Haltungen bleiben Vanilla. Menschliche Sammelpunkte dürfen auch Outpost-Wachen erfassen. Aggressive, Defensive und Hold gelten als Vorgabe für neu erzeugte Einheiten, nicht als ständig wiederholtes Überschreiben späterer Spielerbefehle.

## Provenienz und Belege

| Quelle | Geprüfter Stand |
|---|---|
| Kanonische installierte `CrusaderDE.dll` | `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll` |
| Native SHA-256 | `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` |
| Native Dateigröße | 3.451.392 Byte |
| Installierte Assembly-CSharp SHA-256 | `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789` |
| Installierter Script Extender | FileVersion `2.6.0.0`, ProductVersion `2.6.0` |
| Lokaler Extender-Commit | `2cee24e33b5a5d81d1c275efabc714ac59917b7b` |
| Lokaler Extender-Git-Tree | `9cfb59b7b531553b23709b90b3cc3f10b0615cc1` |
| Referenz-ImageBase | `0x180000000`; VA = ImageBase + RVA, im Prozess ASLR berücksichtigen |

Hash, CURRENT, Manifest und Datensatz-Hash wurden abgeglichen; Commit/Tree entsprechen der Baseline. Einstieg: [CURRENT.md](../_inspect/CrusaderDE-Native-Baseline/CURRENT.md), maschinenlesbar [CURRENT.json](../_inspect/CrusaderDE-Native-Baseline/CURRENT.json). Der Fork und die Rohbaseline blieben unverändert.

Reproduzierbare, hashprüfende Abfrage: [audit.py](../_inspect/OutpostAudit/audit.py). Evidenzpakete liegen in [OutpostAudit/FBCB9319](../_inspect/OutpostAudit/FBCB9319), z. B. [ABB90](../_inspect/OutpostAudit/FBCB9319/ABB90.json) und [Profile](../_inspect/OutpostAudit/FBCB9319/profiles.json). Jedes Paket enthält Hash, Baseline-Vertrauensstufe, RVA/VA, Caller/Callees und vorhandenen Pseudocode sowie ein diagnostisches Disassemblyfenster.

**Methodengrenze:** Ghidras `size` ist die Summe der Funktionskörperbytes, nicht zwingend eine zusammenhängende Endadresse. Bei `0xABB90` fehlt brauchbarer Baseline-Pseudocode; ausgewertet wurde das Disassembly einschließlich Verzweigungen und Sprungtabellen. Sein Code reicht bis einschließlich `RET` bei `0xACDE7`, Tabellen beginnen bei `0xACDE8` und `0xACDFC`. Andere exportierte lineare Fenster werden ausdrücklich nicht als vollständige Funktionsgrenze ausgegeben. Die Pseudocodesignaturen lassen teilweise Register-/Stackargumente weg; maßgeblich bleibt der überprüfte Aufruf.

### Funktions- und Evidenzkarte

`S` bedeutet: die hier beschriebene Teilfunktion ist durch statischen Daten-/Kontrollfluss belegt. `C` bedeutet: ursprünglicher Baseline-Name/Signatur ist weiterhin `candidate`; keine stillschweigende Hochstufung und kein Laufzeitnachweis. `E` bezeichnet einen durch Export/Managed-Aufruf identifizierten Einstieg. Die tatsächliche Metadatenstufe steht zusätzlich in jedem Evidenzpaket.

| RVA | Bedeutung im Audit | Vertrauen |
|---|---|---|
| `0xB47E0` | Gebäudeallokation und Outpost-Startwerte | S/C |
| `0xC60F0` | Gebäudeschleife, NeedsInit/Alive/Deletion, indirekter Typdispatch | S/C |
| `0xABB90` | Gemeinsames Update aller drei Outposts | S/C, Disassembly |
| `0x7530` | Deterministische native Zufallssequenz | S/C |
| `0x81870` | DLL_GameAction, Editoraktionen 1055–1057 | S/E |
| `0xD6BF0` | Profilbit umschalten | S/C |
| `0x89F40` | Gebäudeauswahl und Outpost-Editorpanel | S/C |
| `0x19D960` | HUD-Snapshot, Panel 45 und Outpost-Felder | S/C |
| `0x17FEF0`, `0x19A240` | Unit anlegen; typspezifische Basistabellen initialisieren | S/C |
| `0x119D60` | Outpost-Gruppe anlegen | S/C |
| `0x11D370`, `0x11E820` | Unit zu Gruppe; Mitgliederbitset und erstes belegtes Wort | S/C |
| `0x2E2B0`, `0x2A720` | Gruppenabschluss, menschliche Ausnahme, KI-Zielwahl | S/C |
| `0x8BD60`, `0x90CD0` | Mapper-Platzierung, Sammelpunktprüfung und Chore-Erzeugung | S/C |
| `0x19AF0` | Chore 102: Sammelpunkt serialisieren/anwenden | S/C |
| `0xB8790`, `0xBED20` | Flaggen darstellen; europäisches Rekrutierungsziel bestimmen | S/C |
| `0x196100` | Spieler-Tribe-Bewegung, Fast-Bit vor Unteraufruf verarbeiten | S/C, Disassembly |
| `0x11B520`, `0x118E00` | Tribe-Bewegung und individueller Fallback | S/C |
| `0x196280` | Individueller Bewegungs-/Pfadauftrag | S/C |
| `0x19B260`, `0x11DD10` | Bewegungstiming und späteres Zurücksetzen des Fast-Felds | S/C |
| `0x118660` | Bei fehlender/ungültiger Gruppe teilweise automatische Gruppierung | S/C |
| `0xC4290`, `0xB8310`, `0xC5790` | Löschung markieren, endgültig bereinigen, Managerreset | S/C |
| `0x141DA0`, `0x145030` | Armbrustschütze, Pikenier: eigene Unit-Updates | S/C, relevante Start-/Bewegungszustände |
| `0x16CD70`, `0x174100`, `0x1768A0` | Assassin, Beduinenheiler, Hinterhaltkämpfer | S/C, relevante Start-/Bewegungszustände |

## Typen, Identitäten und Felder

| Variante | Building-Typ | Mapper | Profile | Wache |
|---|---:|---:|---:|---|
| Europäisch | 106 / `0x6A` | 178 | 0–7 | Archer 22 |
| Arabisch | 107 / `0x6B` | 179 | 8–15 | ArabBow 70 |
| Beduinisch | 2 | 53 | 16–23 | Skirmisher 82 |

Alle drei Einträge in der Building-Dispatchtabelle RVA `0x2DEAE0` zeigen auf `0xABB90`. Der indirekte Aufruf liegt bei `0xC64AD`; deshalb ist eine leere direkte Caller-Liste für ABB90 kein Beleg, dass die Funktion unbenutzt ist.

Native Building-Managerbasis: RVA `0x64CCBB0`, Stride `0x32C`. Native Slotadresse: `manager + 0x5C + buildingId * 0x32C`. Der erste echte `GameBuilding` der Extender-Arrayansicht beginnt bei `manager + 0x388`; damit gilt `spanIndex = buildingId - 1`. Slot 0 ist kein echtes erstes Gebäude. Native Allokation durchsucht IDs 1 bis 3999. Identität immer `(buildingId, globalId)`; eine ID allein überlebt Slot-Wiederverwendung nicht.

Folgende Offsets sind **relativ zu GameBuilding**, nicht relativ zum Manager oder zum nativen Slot-Multiplikationsausdruck:

| Offset | Typ | Bedeutung/Schreibpfad |
|---|---|---|
| `0xD2` | 16 Bit | Gebäudetyp |
| `0xD6` | 16 Bit, API ushort; native Leser sign-erweitern | Besitzer |
| `0xD8` | 32 Bit | Global-ID |
| `0x29A` | signed short | Wachenersatz-Zähler, Start 1200 |
| `0x2A7` | Byte | Zielzahl Wachen, Start 6 oder 7 |
| `0x300` | ushort | Profilmaske; Erzeugung liest acht untere Bits |
| `0x302` | short | Aktuelle Tribe-ID, 0 = keine |
| `0x304` | 32 Bit | Global-ID der aktuellen Tribe |
| `0x308` | short | Produktions-/Wartezähler |
| `0x30A` | short | Ziel-Mitgliederzahl aktueller Gruppe |
| `0x30C` | short | Profilindex 0–23 |
| `0x30E` | short | Größenstufe; Editor 0–5 |
| `0x310` | short | Verzögerungs-Countdown; Editor 0–24000 |
| `0x312` | short | Katapult-Sonderflag |
| `0x314` | Byte | Einmalige Initialisierung der Umgebung erfolgt |
| `0x316` | short | Beschleunigung zwischen Gruppen |
| `0x318` | short | Beschleunigung innerhalb einer Gruppe |

Acht Wachenidentitäten sind auf zwei Feldgruppen verteilt: Unit-ID-short bei `0x19E/1A0/1A2/1A4` mit Global-ID bei `0x1A8/1AC/1B0/1B4` sowie Unit-ID bei `0x2E0/2E2/2E4/2E6` mit Global-ID bei `0x2E8/2EC/2F0/2F4`. Herleitung jeweils aus absolutem nativen Slotziel minus `manager + 0x5C`. Vor produktivem Rohzugriff zusätzlich Struktur- und Laufzeitvalidierung erforderlich.

Quelle für die Managed-Layouts: [GameBuilding](../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs), [GameTribe](../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs), [GameUnit](../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs).

## Die 24 Vanilla-Profile

RVA `0x2DD880`, 24 Datensätze mit Stride 52 Byte = 13 signed int32: `Intervall, Minimum, Basisgröße, Zufallsspanne, Typ0…Typ7, Gruppenrolle`. Nicht benutzte Typplätze sind null; die existierenden Profile belegen ihre nichtnull Plätze zusammenhängend. Wiederholte Typen gewichten die Auswahl innerhalb eines Profils.

| Index | Fraktion | Intervall | Minimum | Basis | Spanne | Nichtnull Typplätze | Rolle (Rohwert) |
|---:|---|---:|---:|---:|---:|---|---:|
| 0 | Europa | 250 | 80 | 10 | 10 | Archer 22 | 182 |
| 1 | Europa | 150 | 50 | 15 | 15 | Spear 24 | 184 |
| 2 | Europa | 250 | 100 | 10 | 10 | Mace 26 | 184 |
| 3 | Europa | 250 | 120 | 10 | 10 | Sword 27 | 184 |
| 4 | Europa | 250 | 150 | 5 | 5 | Knight 28 | 184 |
| 5 | Europa | 250 | 100 | 1 | 1 | Catapult 39 | 185 |
| 6 | Europa | 250 | 100 | 10 | 10 | Catapult 39 ×1, Spear 24 ×7 | 185 |
| 7 | Europa | 250 | 120 | 10 | 10 | Catapult 39 ×1, Sword 27 ×7 | 185 |
| 8 | Araber | 250 | 80 | 10 | 10 | ArabBow 70 | 182 |
| 9 | Araber | 250 | 100 | 10 | 10 | ArabSword 75 | 184 |
| 10 | Araber | 100 | 30 | 20 | 20 | Slave 71 | 184 |
| 11 | Araber | 250 | 80 | 10 | 10 | Horseman 74 | 182 |
| 12 | Araber | 250 | 80 | 10 | 10 | Grenadier 76 | 182 |
| 13 | Araber | 150 | 50 | 15 | 15 | Slinger 72 | 182 |
| 14 | Araber | 250 | 100 | 1 | 1 | ArabBallista 77 | 182 |
| 15 | Araber | 250 | 100 | 1 | 1 | Catapult 39 | 185 |
| 16 | Beduinen | 150 | 50 | 10 | 2 | Skirmisher 82 | 182 |
| 17 | Beduinen | 200 | 200 | 6 | 2 | Sapper 84 | 184 |
| 18 | Beduinen | 250 | 250 | 4 | 2 | CamelLancer 78 | 184 |
| 19 | Beduinen | 250 | 250 | 3 | 1 | HeavyCamel 83 | 182 |
| 20 | Beduinen | 250 | 250 | 4 | 2 | Demolisher 85 | 184 |
| 21 | Beduinen | 250 | 250 | 3 | 1 | Eunuch 80 | 184 |
| 22 | Beduinen | 250 | 250 | 1 | 1 | Catapult 39 | 185 |
| 23 | Beduinen | 250 | 250 | 8 | 2 | Catapult 39 ×1, Skirmisher 82 ×7 | 185 |

Die Rollenwerte werden in das Gruppenfeld managerrelativ `+0x67C` / GameTribe-relativ `+0x652` geschrieben. Ihre vollständige KI-Semantik ist hier nicht in vermeintliche Klassen wie „Nahkämpfer“ übersetzt.

Die Profilwahl ist gleichverteilt über die aktivierten Bits, **nicht über erzeugte Units**. Zielgröße ist `(Basis + RNG % Spanne) * (Größenstufe + 1)`, anschließend short-Speicherung. Deshalb ergeben zwei aktivierte Profile nicht 50:50 der tatsächlich erzeugten Units. Wachen und zusätzliche Ingenieure verfälschen diese Rechnung weiter. Profile 6/7 besitzen zudem Katapult-Sonderregeln; Profil 23 hat zwar ähnliche Tabellenplätze, gehört aber nicht zu diesem Indexzweig. Acht Typplätze allein sind daher kein generisches Prozentmodell.

## Editor: Eingabe bis Speicherung im Gebäude

`MainViewModel.ButtonOutpostFunction` schaltet ein Bit der lokalen HUD-Kopie `marry_m_name1` und sendet `GameAction.SetOutpostState` (1055 / `0x41F`) mit ausgewählter Gebäude-ID und Bitindex. Native `DLL_GameAction` prüft, dass die ID dem aktuell ausgewählten Gebäude entspricht; `0xD6BF0` XORt das ushort-Bit `1 << (index & 15)`. Der Erzeuger nutzt nur die unteren acht Bits.

Größe ist GameAction 1056 / `0x420`, Verzögerung 1057 / `0x421`. Die UI liefert Sliderwerte, Managed-Code wandelt float nach int, native Felder speichern short. Größe: 0–5 mit Raster; Verzögerung: 0–24000, Integertrunkierung. Das sind keine Prozentanteile. Der Snapshot `0x19D960` füllt bei Submode 45 Maske, Größe und Verzögerung in die drei `marry_*`-Felder. Die acht Buttons werden aus den Bits aktualisiert.

Quellen: [MainViewModel](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/managed/BC8B6A39/decompiled/CrusaderDE/MainViewModel.cs), [HUD_Buildings.xaml](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/resources/xaml/Assets/GUI/XAMLResources/HUD_Buildings.xaml). Diese Editoraktionen schreiben den ausgewählten Datensatz direkt; sie sind kein schon vorhandenes neues Multiplayer-Protokoll für Prozentkonfigurationen.

## Erzeugung, Wachen und Gruppenabschluss

### Initialisierung und Takt

`0xB47E0` lehnt unter anderem Typ 0, ungeeignete Kartenpositionen oder fehlende freie Slots ab. Erfolgreiche Allokation nullt den Datensatz, vergibt eine neue Global-ID und setzt NeedsInit. Outpost-Startwerte: Profilmaske `0xFF`, Größenstufe 1, Verzögerung 0, Wachen-Sollzahl 6/7, Wachen-Zähler 1200.

`0xC60F0` verarbeitet die Gebäudeslots. NeedsInit 1 wird zu Alive 2; MarkedForDeletion 3 geht in die Bereinigung; nur der normale lebende Zweig ruft die Typfunktion auf. Eine periodische Suche aktualisiert die obere belegte Slotgrenze. Zeiten im Outpost sind Zähler pro erfolgtem Update, keine verlässlich abgeleiteten Sekunden oder Unity-Frames.

`0xABB90` erhält seine Gebäude-ID aus der aktuellen Dispatch-Globalen RVA `0x8F9BE4`; Besitzer wird am Einstieg gelesen. Es meldet das Gebäude an weitere Manager (`0x29190`, `0xCA940`), bearbeitet Darstellung und bricht im Editor (Mode 1) vor der Spawnlogik ab. Weitere Modus-/Startbedingungen können die Simulation unterdrücken. Die Rohbedingungen stehen im Disassembly; ihre nicht abschließend identifizierten globalen Namen sind keine gesicherten Schwierigkeits- oder Pausenbezeichnungen.

Ein einmaliger 16×16-Umgebungsscan weist passenden eigenen Units einen Wert 50 im nativen Unitfeld manager-/slotrelativ `+0xA82` zu. Diese Zahl wird hier nicht ohne belegte Lesersemantik als bestimmter Cooldown benannt. Ein positiver Outpost-Verzögerungswert wird heruntergezählt.

### Wachenpfad

Der Wachenpfad ist **nicht auf KI-Besitzer beschränkt** und bleibt von einer leeren Profilmaske unabhängig. Nach Ablauf des Wachen-Zählers werden acht gespeicherte Unit-ID/Global-ID-Paare geprüft. Die lokale Gültigkeitsprüfung verwendet die Identität; sie ist keine zusätzliche vollständige Alive-/Owner-Prüfung. Unter der Sollzahl wird der fraktionsabhängige Fernkämpfer erzeugt.

Erzeugungsargumente: Unit-Manager, Besitzer int, Farbe short (hier gleich Besitzer), Welt-X/Y short aus Gebäude-Endtile ×8, Höhe short 8, Typ int. `0x17FEF0` liefert eine 1-basierte Unit-ID oder 0. Die Unitinitialisierung zentriert Positionen mit +4 und leitet Tilekoordinaten durch `>>3` ab. Nach erfolgreichem Wachenspawn setzt der Outpost Zustand `0x65`, das normale Rekrutierungs-Sammelpunktfeld `+0x708` auf 0 und `+0xA82` auf 50.

Es folgen bis zu 15 zufällige Positionsversuche im lokalen Bereich (Modulo 14, Offset −7), Karten-/Grenzprüfung, PCL-Prüfung und gegebenenfalls Regionsprüfung `0xE2610` mit Besitzer und der Einheit zugeordnetem Pfadmodus. Ein Tileflag `0x10000000` wird bevorzugt; andernfalls wird ein brauchbarer Kandidat gemerkt. Bewegung erfolgt über `0x196280`. Ohne geeignetes Ziel gibt es keinen garantierten Ortswechsel. Die neue Identität wird in einem freien/ungültigen Wacheneintrag gespeichert.

Wichtig für die Mod: Eine noch lebende, zum menschlichen Sammelpunkt weggeschickte Wache kann weiter als besetzter Wachenslot zählen. „Wache umleiten“ ist nicht automatisch „neue Wache nachproduzieren“. Die Zusammensetzungsstatistik muss diesen separaten Erzeugungspfad ausdrücklich berücksichtigen.

### Reguläre Truppen

Die aktive Tribe wird über ID und Global-ID validiert. Ohne gültige Tribe läuft der Zwischen-Gruppen-Zähler. Dessen Ausgangsschwelle 2000 wird durch das Beschleunigungsfeld reduziert und durch kontextabhängige Minima begrenzt. Bei Maske 0 entsteht keine reguläre Gruppe, Wachen sind davon nicht abgeschaltet.

Ein aktiviertes Profil wird über Vanillas RNG gewählt. `0x7530` liest aus einer nativen Sequenz, erhöht deren Index und wickelt ihn bei 20000 um. UI-Zugriffe dürfen diese Sequenz nicht verbrauchen; ein späteres Prozentmodell benötigt eine deterministische, gespeicherte Auswahlentscheidung.

`0x119D60` allokiert eine Tribe in spielerbezogener Suchfolge (Start `4500 - owner`, Schritt −8), setzt Besitzer, Global-ID und Initialzustand; bei fehlendem Slot kommt 0 zurück. Der Outpost setzt die neue Gruppenhaltung auf 2 = Aggressive, Zielgröße und Profilrolle. Er setzt den Produktionszähler auf 2000 und das Katapultflag auf 1.

Das interne Spawnintervall verwendet Profilintervall minus Beschleunigung, Profilminimum, weitere kontextabhängige Grenzen und ganzzahlig den Faktor `(100 - 15 * Größenstufe) / 100`. Nach einem Intervall wird der Zähler mit RNG %40 neu belegt. Eine positive Verzögerung ist **keine einfache Pause aller Spawns**: Wachen laufen weiter, reguläre Gruppen können teilweise aufgebaut werden, der letzte Abschluss wird aufgehalten. Bei bestimmten KI-/Leergruppenbedingungen werden mehrere Units in einem Durchlauf erzeugt; Profil 6/7 hat zusätzliche Sonderfälle.

Pro primärem Spawn folgen: native Unit-Erzeugung → bei 0 Abbruch → `0x11D370(manager, unitId, tribeId)` → gegebenenfalls Crew → Outpost-Nachinitialisierung. `0x11D370` erhöht Mitgliederzahl, setzt das bitweise Mitgliederfeld, initialisiert bei leerer Gruppe den Leader und schreibt Tribe-ID/Global-ID in die Unit. Es ist kein idempotentes „ensure assigned“: blindes erneutes Aufrufen kann den Zähler verfälschen.

Danach erteilt der Outpost `0x11B520(tribeManager, tribeId, endTileX, endTileY, 0, 0, 0)`. Ein zu früh gesetzter Sammelpunkt kann somit wieder überschrieben werden. Sobald Ziel-Mitgliederzahl erreicht und Verzögerung abgelaufen sind, folgt `0x2E2B0`, die aktive Outpost-Tribe-ID wird gelöscht und die Beschleunigung steigt (zwischen Gruppen +33 bzw. +100, innerhalb +4).

### Mensch/KI und Fehlerpfade

`0x2E2B0` markiert leere Gruppen zur Auflösung. Menschliche Besitzer werden ausdrücklich von der KI-Warteschlange ausgenommen: in einem nativen Modus ist Spieler 1 der menschliche Fall, andernfalls wird das entsprechende Spieler-KI-Feld geprüft. KI-Gruppen werden in eine begrenzte Liste aufgenommen und über `0x2A720` einem gegnerischen Lord zugeordnet; Team-/Lebensstatus und AIC-Priorität fließen ein. Keine allgemeine Behauptung „jede Outpost-Truppe greift automatisch einen Lord an“ für menschliche Besitzer.

Es gibt vorgelagerte Spieler-/Einheitenlimitbedingungen und den harten Allokationsfehler 0. Fehlgeschlagene Primärspawns führen nicht zu einer korrekt fertiggestellten zusätzlichen Unit; erfolgloser Crewspawn rollt die bereits angelegte Maschine nicht zurück. Für ein Prozentmodell dürfen fehlgeschlagene Versuche nicht wie erfolgreich erzeugte Einheiten zählen. Auswirkungen wiederholten Limits auf Zähler, Restgruppe und spätere Freigabe gehören in die Diagnose.

### Belagerungsgerät und Besatzung

Die Sprungtabelle im Outpost wertet den **ursprünglich lokal gewählten Typ** aus, auch wenn ein UnitCreate-Pre-Event den an die Erzeugung übergebenen Typ verändert hat. Dadurch kann ein reiner Event-Typersatz falsche Crew erzeugen oder benötigte Crew auslassen.

| Primärtyp | Zusätzliche Engineer-Units (30) im Outpostzweig |
|---|---:|
| Catapult 39 | 2 |
| Treb 40 | 3 |
| SiegeTower 58 | 4 |
| Ram 59 | 4 |
| Shield 60 | 1 |
| ArabBallista 77 | 2 |

Andere Typen erhalten in diesem Switch keine zusätzliche Crew. Die Ingenieure bekommen nativen Auftrag `+0x9F4 = 0x10` und Geräte-Unit-ID in `+0x9F6`. In diesem Zweig werden sie nicht wie die primäre Unit zur Outpost-Tribe hinzugefügt. Maschinenzahl, Tribe-Mitgliederzahl und Gesamtzahl erzeugter Unit-Datensätze sind folglich unterschiedliche Größen. Für „Prozent der Gesamtmenge“ muss vor einer Umsetzung festgelegt werden, ob automatische Besatzungen mitzählen; die technische Auswahl darf diese Unterscheidung nicht verbergen.

## Fehlende Einheitentypen und Kompatibilitätsgrenzen

Die Unit-Dispatchtabelle RVA `0x321CB0` enthält reguläre Updates für alle fünf offensichtlichen Lücken. `0x19A240(manager, unitId, type)` initialisiert Typ, Pfad-/Verbindungsklasse, Bewegungstiming, Lebenspunkte und weitere Werte aus Typentabellen. Es existiert dort keine Outpost-Profil-Whitelist. Assassin 73 und Ambusher 81 erhalten zusätzlich den typspezifischen Wert 32000 im Feld `+0xA90`.

| Typ | Statischer Befund | Noch nachzuweisen |
|---|---|---|
| XBow 23 | Normale Typinitialisierung; eigener Update `0x141DA0`, Rekrutierungszweig und Zustand 101 | Schießen, Nachladen, Ankunft und alle Haltungen als Outpost-Truppe |
| Pike 25 | Update `0x145030`, normaler Bewegungspfad; weitere Zustände für spezielle Aufträge | Haltung nach Bewegung und Abbruch spezieller Aufträge |
| Assassin 73 | Update `0x16CD70`, Bewegung 101, zusätzliche Tarnungszustände | Tarnung, Wand-/Leiterpfade, Ankunft, KI-Rolle |
| BedHealer 79 | Update `0x174100`; nach Bewegungsende Rückkehr in Zustand 0; dort eigenständige Verwundetensuche `0x18DE10` | Verhalten bei Verwundeten auf dem Weg/am Ziel; sinnvolle Wirkung jeder Haltung |
| Ambusher 81 | Update `0x1768A0`; besondere Warte-/Tarnungszustände und Timer; Bewegungszweig vorhanden | Verstecken/Aufdecken, Haltung, Zielerreichung |

`0x118660` kann bei fehlender oder ungültiger Tribe für bestimmte Besitzer-/Modusbedingungen eine neue Gruppe erzeugen; seine Typklassifizierung enthält auch diese neuen Typen. Das belegt eine vorhandene Gruppeneinordnung, aber nicht die Eignung jeder willkürlich übernommenen Outpost-Rolle 182/184/185 für diese Units.

Die allgemeinen Bewegungszustände können nach Pfadende in Zustand 0 oder in weitere Spezialzustände wechseln; Kampf-/Zielsuche kann anschließend neue Aufträge auslösen. Ein einmal beobachteter korrekter Spawn oder Move-Rückgabewert reicht nicht als Funktionstest.

„Jede Einheit des entsprechenden Typs“ ist für reguläre Fraktionssoldaten plausibel. Ladder 29, Engineer 30, Monk 37 sowie zusätzliche Belagerungsgeräte sind eine getrennte Erweiterung mit eigenen Arbeits-/Besatzungsverträgen. Zivilisten, Tiere, Lords und rein technische Typen sind durch dieses Audit ausdrücklich nicht pauschal als Outpost-Truppen freigegeben. Auch bei bereits in Profilen enthaltenen Sondertypen gelten die vorhandenen Native-Verträge weiter.

## Gebäudeauswahl und HUD

`0x89F40(buildingId, panelMode)` prüft ID, Auswahlzustand und Besitzer-/Modusbedingungen. Für 2/106/107 wird Panel 45 nur bei Editor-Modus 1 geöffnet. Im Spiel speichert der Zweig eine Building-ID/Global-ID-Auswahlreferenz und kehrt zurück, ohne den normalen Gebäude-Panelmodus zu aktivieren. Das ist die native Ursache des fehlenden Outpost-Gebäude-HUDs.

Der erfolgreiche reguläre Gebäudeweg wechselt über `0xA110` in Modus 16 und aktualisiert Auswahlfelder. Der Extender-Helfer `GetSelectedBuildingId` kann eine vorherige Gebäudeauswahl behalten; ein eigenes HUD darf daher nicht allein auf diesen Wert reagieren. Aktueller Klick, Identität, Besitzer, tatsächlicher Modus und spätere Deselektion müssen zusammenpassen. Ein globales Vortäuschen des Editormodus wäre kein gezielter Outpost-HUD-Weg.

Das spätere HUD kann aus eigenen Konfigurationsdaten Unitbilder, Prozentwerte, ausgewählten Typ, dessen Sammelpunkt und Haltung anzeigen. Die Bildressourcen/Mapper existieren für reguläre rekrutierbare Truppen; eine vollständige verbindliche Asset-Zuordnung aller gewünschten Typen wurde hier noch nicht erstellt. Das ist UI-Arbeit der späteren Phase.

Im untersuchten Eventangebot gibt es Gebäude-Spawn-/Delete-/Platzierungsereignisse, aber kein nachgewiesenes dediziertes Outpost-Auswahlereignis. Eine UI-Integration braucht deshalb einen gezielten Auswahladapter; Datenänderungen dagegen gehören in native Simulationsevents/OnTick beziehungsweise synchronisierte Commands, nicht in Rendercallbacks.

## Kasernen-Sammelpunkte: vollständiger relevante Befehlspfad

1. `HUD_Buildings.xaml` ruft `ButtonCreateTroopCommand` mit Assembly-Point-Mappern auf. Die zugehörigen Hoveraktionen liefern Beschreibungstext, keinen Bewegungsauftrag.
2. `MainViewModel.ButtonCreateTroop` erkennt Mapperbereiche und startet über `EditorDirector.placeBuildingInteraction` die Kartenplatzierung; der Name „EditorDirector“ bedeutet hier nicht, dass die Funktion nur im Editor benutzt wird. Native Mapperaktivierung führt zu `0x8BD60`.
3. `0x90CD0` verarbeitet Platzierung/Vorschau, Grenzen und Erreichbarkeit vom Rekrutierungsgebäude. Vorschau zeichnet; Commit legt Chore 102 (`0x66`) an und beendet die Platzierung.
4. Handler `0x19AF0`, über Chore-Tabelle RVA `0x2C7A30` bestätigt, schreibt/liest fünf Payloadbytes: signed byte Kategorieindex, short Tile-X, short Tile-Y. Anwender ist der Chore-Sender aus dem nativen Kontext, nicht einfach der aktuelle UI-Spieler.
5. Gespeichert wird in Spielerfeldern. Europa-Mapper 332–338 → Slots 0–6; Araber 360–366 → 10–16; Beduinen 391–398 → 50–57; weitere Kategorien existieren für Ingenieure, Tunnelbauer und Mönche. Der Klick auf passende eigene Rekrutierungsgebäude kann die Koordinaten zurücksetzen.
6. `0xB8790` stellt passende Flaggen dar. Die Rekrutierungsroutinen lesen die Ziele; etwa `0xBED20` sucht um das Ziel erreichbare/freie Positionen, andernfalls Gebäude-Ausgangspositionen, und gibt globale Ziel-Tiles zurück. Kein geeignetes Ziel beziehungsweise bereits erreichte Position ergibt keinen neuen Weg.
7. Die normalen Unit-Updates lesen das Rekrutierungsflag manager-/slotrelativ `+0x708`, löschen es, wechseln in Zustand 105 und bewegen die Unit. **Der Outpost setzt dieses Flag selbst auf 0.** Sein separater Gruppenbefehl ist daher entscheidend.

Rally-Feldbasen (RVA, jeweils plus `player * 0x583C`): Europa `0x379D0B0 + slot*4`, Araber `0x379E720 + (index-10)*4`, Beduinen `0x379ECD4 + (index-50)*4`. Ein Koordinatenpaar belegt vier Byte. Das sind **Spieler-/Kategoriedaten**, keine Zuordnung zu einem individuellen Outpost. Vanilla-Chore 102 kann deshalb nicht unverändert mehrere Outposts mit unterschiedlichen Zielen derselben Unit abbilden.

## Tribe-Bewegung, Laufmodus und Haltung

`GameTribeManagerAPI.IssueMoveHereCommand` führt zum nativen `0x11B520` mit `(manager, tribeId, tileX, tileY, patrol, newOrder, moveType)`. Tiles sind keine Weltkoordinaten. Die Funktion führt Gruppen-/Pfad-/Mitgliederprüfungen durch, setzt individuelle Bewegungszustände und kann über `0x118E00` auf Einzelbewegung zurückfallen. Nicht jeder Erfolg der Gruppenfunktion bedeutet, dass jedes Mitglied tatsächlich einen gültigen neuen Weg bekam. NeedsInit-Units werden nicht automatisch wie bereits lebende Units behandelt.

**Widerspruch zum naiven Fast-Aufruf:** Der öffentliche Enumwert `TribeMoveType.Fast = -255` reicht am direkten Ziel `0x11B520` statisch nicht als Nachweis des Laufmodus. Dort wird das betreffende Argument im untersuchten Pfad nur auf null/ungleich null geprüft. Der vorgelagerte Spielerwrapper `0x196100` prüft hingegen Bit 7 des niedrigen Bytes seines sechsten Arguments, setzt das Tribe-Feld native `+0x56C` (GameTribe `+0x542`) und entfernt dieses Bit vor dem Unteraufruf. `-255` hat als niedriges Byte `0x01`.

`0x19B260` liest dieses Feld und umgeht bei gesetztem Wert die Anpassung an das langsamste Gruppenmitglied; Gelände-/Einheitentiming bleibt erhalten. Das ist ein belegter Zusammenhang mit Formationstempo, keine pauschale Zusage eines zusätzlichen Lauftempo-Bonus für jeden Typ. `0x11DD10` kann das Feld bei späteren Gruppenaktionen wieder auf 0 setzen. Der individuelle Fallback transportiert das siebte Argument nicht gleichwertig weiter.

Vor Umsetzung deshalb den normalen UI-Laufbefehl mit dem direkten API-Aufruf vergleichen (gleiche Unit, gleiches Gelände, gleiche Formation), einschließlich Ankunft und Anschlussbefehl. Den bestehenden MoatMove-Detour bei `0x196100` berücksichtigen; keine konkurrierenden Detours blind installieren. Kein raw Feldwert wird hier als fertiger Ersatz empfohlen.

Haltungen: `Hold=0`, `Defensive=1`, `Aggressive=2`, ushort. `GameTribeManagerAPI.SetStance` schreibt direkt `r_TribeStance` (GameTribe `+0x60A`, native `+0x634`), ohne eigenes Multiplayer-Chore. Outpost-Neugruppen beginnen mit Aggressive. Die Haltung gehört der Tribe; unterschiedliche Typziele innerhalb einer gemischten Tribe verlangen geeignete getrennte Gruppierung oder eine andere nachgewiesene Befehlsorganisation. Eine Haltung im UnitCreate-Post zu setzen wäre zeitlich zu früh, wenn erst danach die Outpost-Zuordnung erfolgt.

Die sichere fachliche Grenze liegt nach vollständiger Outpost-Nachverarbeitung und nach erreichter Unit-Bereitschaft. Ein bloßes Entnehmen aus einer noch in Produktion befindlichen Gruppe kann jedoch deren Mitgliederzählung und Abschluss verändern. Zeitpunkt, Gruppenablösung und Wachen-Zuordnung müssen deshalb vor einem Runtime-Patch gemeinsam geklärt werden. Spätere eigene Spielerbefehle haben Vorrang; keine fortlaufende Haltungs-/Zielerzwingung.

Quellen: [GameTribeManagerAPI](../shcde-script-extender/src/SHCDESE.BepInEx/API/GameTribeManagerAPI.cs), [TribeMoveType](../shcde-script-extender/src/SHCDESE.BepInEx/Interop/Enums/TribeMoveType.cs), [TribeStance](../shcde-script-extender/src/SHCDESE.BepInEx/Interop/Enums/TribeStance.cs).

## Löschung, Eigentümerwechsel und Persistenz

`GameBuildingManagerAPI.SetOwner` schreibt nur das Besitzerfeld. Es überträgt nicht nachweislich die bestehende Outpost-Tribe, Wachen oder Modkonfiguration. ABB90 liest den aktuellen Besitzer für neue Spawns, validiert seine bisherige Tribe aber über ID/Global-ID. Ein nackter Besitzerwechsel ist daher kein vollständiger Übernahmevertrag. Für Moddaten Besitzerwechsel erkennen, alte menschliche Aufträge nicht auf neue KI-Besitzer anwenden und existierende Gruppen gesondert prüfen.

`0xC4290` markiert ein Gebäude zur Löschung; der Post-Event dieses Aufrufs ist **noch nicht die endgültige Slotfreigabe**. `0xC60F0` führt später `0xB8310` aus. Dieser Weg schließt gegebenenfalls die aktuelle Gebäudeauswahl, bereinigt Tile-/Spielerverweise und nullt den `0x32C`-Datensatz. Bemerkenswert: Die unfertige Outpost-Tribe wird dort für Typ 106/107 bei passender Global-ID an `0x2E2B0` weitergereicht; **Typ 2 fehlt in diesem Branch**. Daraus folgt keine nachgewiesene Löschung aller beduinischen Resttruppen; deren weiterer Lebenslauf ist ein eigener Diagnosefall. Die Wachenliste ist ebenfalls kein Beleg für automatische Vernichtung aller Wachen beim Abriss.

Ein später neu belegter Slot erhält eine neue Global-ID. Konfiguration, HUD-Auswahl und ausstehende Befehle müssen bei falscher Identität verfallen. Mapreset `0xC5790` nullt die nativen Gebäuderecords; reine ID-Dictionaries dürfen nicht über Missionen hinweg weiterleben.

Die vorhandene [ModSaveDataAPI](../shcde-script-extender/src/SHCDESE.BepInEx/API/ModSaveDataAPI.cs) registriert Save-/Load-/Unload-Callbacks und speichert Bytes in `_SE_ModData_`-Archiveinträgen. [GameMapArchiveManagerAPI](../shcde-script-extender/src/SHCDESE.BepInEx/API/GameMapArchiveManagerAPI.cs) unterscheidet Editor-Map und Save, ruft Save-Handler vor Archivserialisierung und Load-Handler nach Archiv-/Metadaten-/Timerlesen auf. Das ist ein geeigneter vorhandener Persistenztransport für Zusatzkonfiguration.

Wichtig: `null`/leere Save-Daten werden übersprungen und bedeuten nicht automatisch „alten Eintrag löschen“. Ein ausdrücklich leeres, versioniertes Datenobjekt verhindert die Wiederbelebung alter Konfiguration. Pointer und transienter UI-Zustand gehören nicht in Save-Daten. Editor-Konfiguration und laufende Produktions-/Verteilungsrestwerte sind getrennte Sachverhalte; ein Save muss die letzteren für deterministische Fortsetzung enthalten.

Die tatsächliche Roundtrip-Stabilität aller hier aufgelisteten nativen Outpost-Felder, Identitäten nach Editor-Neuladen sowie Verteilung der Zusatzarchive beim Multiplayer-Maptransfer wurde **nicht feldweise durch einen Save/Load-Test bewiesen**. Der vorhandene Archivvertrag ersetzt diesen Nachweis nicht. Relevante Lebenszyklusquellen: [Editor lifecycle](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/EDITOR_MAP_LIFECYCLE.md), [Mission lifecycle](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MISSION_LIFECYCLE.md). Restore ist nicht schon allein mit einem niedrigen nativen Load-Post gleichzusetzen; Datenrestauration und spielbereite Mission auseinanderhalten.

## Multiplayer und deterministische Prozentwerte

UI setzt Änderungswünsche ab; Simulation ändert Daten auf allen Peers tickgleich. Das vorhandene Chore-System ist dafür relevant, nicht ein lokaler Unity-Callback. [Chore-System-Audit](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/CHORE_SYSTEM.md) und `GameNetworkAPI.SendPacketToAllEx2(..., viaChore: true)` sind vorhandene Bausteine.

Vor einem zukünftigen Send prüfen: Registrierung/Packet-Hook bereit, serialisierbar, ChoreManagerVA ungleich 0, vollständige Payload inklusive zweibytegroßer Packet-ID höchstens 1200 Byte. Andernfalls kann die API auf nicht ticksynchronen Steam-Transport fallen. Die Packet-ID wird von der API vorangestellt. Empfang prüft Senderberechtigung, Building-ID/Global-ID, Besitzer, Fraktion, erlaubte Typen, Prozent-/Gewichtsbereich und Kartenkoordinaten. Ein normales Send-Return beweist nur lokales Einreihen, nicht Ausführung auf allen Peers.

Für Anteile sind ganzzahlige Gewichte oder feste Unterteilungen von 100 sinnvoller als unterschiedliche Float-Rundung. Die zentrale fachliche Entscheidung lautet: Zufallswahrscheinlichkeit je erfolgreichem Spawn oder langfristig möglichst genaue Mengenverteilung mit Restakkumulatoren. Beides ist programmierbar; keines entspricht direkt den Vanilla-Profilbits. Hier wird diese spätere Designentscheidung nicht stillschweigend getroffen.

Für die gewünschte Gesamtmenge müssen Wachen und Besatzung ausdrücklich eingeordnet werden. Unverändert weiterlaufende fraktionsfeste Wachen verhindern z. B. „100 % Pikenier“ über alle tatsächlich erzeugten Soldaten. Fehlgeschlagene Spawns, Limits und Restakkumulatoren müssen peeridentisch behandelt und im Save fortgesetzt werden. Die KI darf die neue Zusammensetzung erhalten, aber nicht die menschlichen Sammelpunkt-/Haltungsänderungen.

## Workspace-Vorbilder und ihre Grenzen

| Quelle | Nutzbarer Befund | Nicht daraus ableiten |
|---|---|---|
| [LordUnitControlsFeature](../BugfixesAndQoL/src/LordUnitControlsFeature.cs) | Eigener Lord ausgewählt → `TroopsSelectedGameAction(true)` aktiviert Truppen-HUD; Auswahl-/Modusprüfungen | Dasselbe öffnet kein verifiziertes Outpost-Gebäude-HUD |
| [UnitHudPresentationCapability](../APIShared/src/UnitHudPresentationCapability.cs) | Zentrale Erweiterung der Unit-HUD-Darstellung und Kategoriezuordnung | Keine schon existierende Gebäudeauswahl-Capability |
| [HunterChickenCompatibilityFeature](../ImprovedHunters/src/HunterChickenCompatibilityFeature.cs) | Gebäudespezifischer Spawnkontext plus UnitCreate Pre/Post; stabile Unitidentitäten | Outpost hat nicht automatisch dasselbe Kontext-Event; Zeit-/Positionsheuristik allein ist keine sichere Ursprungszuordnung |
| [ImprovedHuntersRuntime](../ImprovedHunters/src/ImprovedHuntersRuntime.cs) | Bestehende spezifische Events, Lifecycle-Anbindung, spätere native Zustände beachten | Jäger-spezifische Trigger und Aufräumpfade ungeprüft übernehmen |
| [FastMovementScheduler](../Testmods/MoatMove/src/FastMovementScheduler.cs) | Nativer Spieler-Move-Wrapper, OnTick, Save-Ladebereitschaft, Befehlsreihenfolge | „Fast“ in jeder dortigen Pfadsuche bedeutet automatisch denselben UI-Laufmodus |
| [FastCommandQueue](../Testmods/MoatMove/src/FastCommandQueue.cs) | ID+Global-ID, deterministische Reihenfolge, Teilgruppen, explizit leeres Saveobjekt | Fertige Outpost-Gruppenaufteilung |
| [GatehouseAutomationRuntime](../ExtraFeatures/src/GatehouseAutomationRuntime.cs) | ModSaveData-Registrierung und gebäudebezogene Automatisierung | Native Outpost-Lebenszyklen seien identisch |
| [AiFlagDiseaseTracker](../ExtraFeatures/src/AiFlagDiseaseTracker.cs), [RandomEventsRuntime](../RandomEvents/src/RandomEventsRuntime.cs) | Weitere reale Save-Handler und Wiederherstellungsmuster | Ohne eigene Prüfung multiplayerkorrekte Outpost-Persistenz |

`OnUnitCreate` kann Typ/Erzeugungsargumente ändern, enthält aber keine Outpost-ID. Der Post-Aufruf liegt innerhalb der Unit-Erzeugung und damit vor Outpost-Gruppenzuordnung, Crew und erneutem Move. Außerdem bildet der vorhandene Post-Event die ursprünglichen Argumente ab; die tatsächlich erzeugte Unit am Rückgabe-ID/Global-ID-Paar prüfen. Wachen, Haupttruppen und Ingenieure brauchen unterschiedliche Herkunftsrollen. Das ist der wesentliche Unterschied zum bloßen Typersatz.

## Konkrete Ansatzstellen und Bewertung je Feature

Die folgenden Punkte sind fachliche Anschlussstellen, **keine freigegebenen Detours**. Vor einem nativen Runtime-Patch sind installierte RedBird-Implementierung, tatsächliche Überschreibspanne, eingehende Sprünge, Register-/Flags-/SIMD-Lebendigkeit und Kompatibilität mit vorhandenen Hooks eigens zu auditieren.

| Wunsch | Bewertung | Erforderliche Erweiterung / verbleibende Grenze |
|---|---|---|
| Individuelle Fraktionssoldaten | Grundsätzlich ja | Auswahl vor Unitinitialisierung mit sicherem Outpost-Kontext; volle Folgeinitialisierung und KI-Rolle erhalten |
| Prozent der Gesamtmenge | Ja als neues Modell | Eigene Daten, deterministische Mengenentscheidung; Wachen/Crew und Fehlschläge einbeziehen |
| Mensch und KI | Architektur unterstützt gemeinsame Zusammensetzung | Menschliche Sonderbefehle strikt aus KI-Zweig halten; neue Typen in KI-Rollen testen |
| Ingame-Gebäude-HUD | Ja mit Auswahl-/HUD-Erweiterung | `0x89F40` und nachgelagerter tatsächlicher Panel-/Auswahlzustand; keine bloße Lord-Aufrufkopie |
| Bilder und Prozentanzeige | Managed/UI-seitig plausibel | Typ→Bild-Liste und ViewModel später erstellen; Daten jetzt schon unabhängig von UI modellierbar |
| Sammelpunkt je Outpost/Typ | Ja, neue Speicherung/Befehle nötig | Vanilla-Spielerfelder/Chore 102 haben falschen Gültigkeitsbereich |
| Auch Wachen umleiten | Möglich, aber separater Pfad | Wachenslot-Belegung und spätere Gruppierung prüfen; kein unbeabsichtigter Ersatz-Endlosspawn |
| Aggressive/Defensive/Hold | Vorhandener Tribe-Vertrag | Erst nach Gruppen-/Spawnnachbereitung anwenden, spätere Befehle respektieren; Spezialtypen testen |
| Normale Tribe-Bewegung | Vorhanden | Neue Einheitenbereitschaft, gemischte Gruppen, Abbruch-/Fallbackpfade beachten |
| Laufmodus | Nicht über API-Enum allein belegt | Wrapper/Fast-Bit/UI-Vergleich und spätere Überschreibung offen |
| Map/Save | Vorhandener Zusatzdaten-Transport | Identitäts-/Feldroundtrip und Restzustand praktisch validieren |
| Multiplayer | Vorhandener ticksynchroner Transport | Zustandsvergleich, Maptransfer, Restore und Limits mit zwei Peers nachweisen |

Die enge technische Lücke für eine spätere Extender-Erweiterung ist ein outpostspezifischer Kontext vor der Auswahl/Erzeugung plus ein Ereignis nach vollständiger Outpost-Nachverarbeitung. Nur ein allgemeines Post-Create-Event schließt diese Lücke nicht. Ob man dafür eine offizielle API anfragt oder eine lokale native Erweiterung baut, wird hier noch nicht entschieden.

### Short report for the Script Extender author

> **Outpost spawn context and completed-spawn event**
>
> On native build `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, building types 2, 106 and 107 share update RVA `0xABB90`. `OnUnitCreate` does not expose the originating outpost and its post event runs before tribe assignment, siege crew creation and the outpost's subsequent move command. Changing only the event's unit type also leaves the caller's original type controlling crew creation. An outpost-specific selection/spawn context and a completed-spawn notification would allow per-building composition and human rally settings without guessing the source from position.
>
> Please also verify `TribeMoveType.Fast = -255` with `IssueMoveHereCommand`: the direct target `0x11B520` does not perform the player wrapper's low-byte bit-7 handling at `0x196100`. The wrapper sets tribe manager-relative field `+0x56C`; `0x19B260` reads that field for formation speed limiting. This is a static discrepancy report, not a proposed replacement constant.

## Diagnose- und Abnahmematrix für die nächste Phase

Keine dieser Prüfungen wurde hier als Spieltest ausgeführt. Ein erster Diagnosemod sollte beobachten, bevor er Verhalten verändert. Pro Ereignis: Tick, Modus, Building-ID/Global-ID/Typ/Owner, Profil/Maske/Zähler/Delay, Tribe-ID/Global-ID/Membercount/Stance, Spawnrolle, gewählter und tatsächlicher Unittyp, Rückgabe-ID/Global-ID/AliveState, Ziel, Bewegungszustand, Ergebnis und spätere Änderungen. Keine unbekannte Semantik als Filter verwenden, der ausgerechnet abweichende Fälle verwirft.

| Fall | Zu beobachtende Abnahme |
|---|---|
| Alle 3 Varianten × Mensch/KI | Baseline ohne Mod reproduzieren; reguläre Profile, Wachen, Gruppenabschluss separat zählen |
| Editor alle Bits aus/einzeln/alle an | Maske, 24 Profilzuordnungen, unabhängige Wachen, Größen-/Delaygrenzen |
| Update-Takt und Pause/Geschwindigkeit | Native Tickzahl gegen Zähleränderungen; keine voreilige Sekundenumrechnung |
| 100 % eines neuen Typs | XBow/Pike/Assassin/Healer/Ambusher je einzeln; Initialisierung, Bewegung, Kampf/Spezialfähigkeit, Anschlusszustand |
| Gemischte Anteile, lange Folge | Erfolgreiche Primärspawns, Wachen, Crew und Fehlversuche getrennt; erwartete Mengenstatistik des gewählten Modells |
| Maschinen und Crew | Jeder der sechs Crewfälle, teilweises Unitlimit, spätere Besatzung, Funktionsfähigkeit der Maschine |
| Voller Unit-/Tribe-Pool | 0-Rückgaben, keine Phantomzählung, korrekte Fortsetzung nach freiem Slot, keine Restgruppen-Endlosschleife |
| Zwei eigene Outposts gleicher Fraktion | Unterschiedliche Ziele desselben Typs bleiben unabhängig; normale Kasernenpunkte unverändert |
| Sammelpunkt unzugänglich | Kartenrand, Wasser/Mauer, getrennte PCL, blockierter Zielbereich; kein Teleport, kein laufend wiederholter Befehl |
| Wachen | Wegschicken, Überleben, Tod, Slot-Recycling; Sollzahl und Ersatzverhalten kontrollieren |
| Drei Haltungen | Sofort nach Erzeugung, nach erstem Unit-Update, nach Ankunft, bei Feindkontakt und nach Spielerbefehl prüfen |
| Laufmodus | Normaler UI-Befehl vs API, langsame/schnelle/mischte Gruppe, Steigung, Fallback, Folgebefehl; Feld und Timing messen |
| KI mit neuen Typen | Unveränderte KI-Zielwahl/Haltung, sinnvolle Rolle, kein versehentlich menschlicher Sammelpunkt |
| Besitzerwechsel | Mensch→KI, KI→Mensch, laufende Produktion; bestehende Tribe und Wachen nicht fremd steuern |
| Abriss aller 3 Varianten | Während Teilgruppe/Delay, nach Abschluss und mit Wachen; beduinische Ausnahme gezielt verfolgen |
| Slot-Wiederverwendung | Neue Global-ID darf keine alten Prozentwerte, HUD-Auswahl oder Befehle übernehmen |
| Map speichern/laden | Editor-Konfiguration, Besitzer und Identität; Start aus der neu geladenen Karte |
| Save laden | Während Produktion, Crewaufbau, ausstehender Bewegung; identische Restwerte und keine doppelten Befehle |
| Multiplayer | Host/Client-Konfiguration und Zieländerung, Pause, Limits, Save-Restore und Maptransfer; Tick-/Zustandschecks auf beiden Peers |
| Deaktivierte Mod/fehlende Daten | Vanilla-Verhalten, keine bleibenden UI-/Spawnpatches, explizit leere Daten überschreiben alte Einträge |

Offene statische Vertiefung vor produktiver Umsetzung: exakte Bedeutung der verbleibenden Modus-/Limitglobalen, vollständige Reader/Writer der KI-Rollen für neu zugelassene Typen, vollständiger normale-UI-Lauf-/Haltungscommandweg und feldweiser Native-Savevertrag. Diese Liste verhindert, dass die vorhandenen positiven Befunde mit einem abgeschlossenen Audit sämtlicher Spezialfälle verwechselt werden.
