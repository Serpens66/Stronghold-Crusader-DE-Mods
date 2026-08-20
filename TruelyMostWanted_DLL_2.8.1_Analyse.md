# Vergleich der „TruelyMostWanted DLL v2.8.1“ mit der installierten Vanilla-DLL

## Kurzfazit

Die DLL ist eine direkt veränderte, erneut kompilierte `Assembly-CSharp.dll`. Beide Assemblies enthalten dieselben 164 dekompilierten C#-Typdateien; es wurden keine neuen Klassen, externen Mod-Abhängigkeiten oder erkennbaren Ressourcen hinzugefügt. Nach identischer Auflösung aller Unity-/Noesis-Abhängigkeiten weichen nur vier Spielklassen inhaltlich ab:

1. `Director`
2. `KeyManager`
3. `CrusaderDE.HUD_AlliesPanel`
4. `CrusaderDE.FRONT_Multiplayer`

Die wesentlichen Features sind eine stark erweiterte Spielgeschwindigkeitssteuerung, direkte Geschwindigkeits-Hotkeys, flexiblere Mengenänderungen beim Gütertransfer mit Verbündeten sowie ein großer Refactor des Multiplayer-Buttonhandlers. Im Multiplayer-Refactor steckt sehr wahrscheinlich ein echter Fehler: Ein neues Dictionary für Gebäudeschalter bleibt `null` und wird anschließend verwendet.

## Vergleichsbasis

### Modifizierte DLL

- Pfad: `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\TruelyMostWanted DLL v 2.8.1\Assembly-CSharp.dll`
- Größe: 3.680.768 Bytes
- SHA-256: `F64CE976BC160111B56B5CFF6B29824595AFA41C80B491A17B01062A71DC0F1C`
- Dateizeit: 20.08.2026 19:12:20

### Installierte Vanilla-DLL

- Pfad: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll`
- Größe: 3.693.056 Bytes
- SHA-256: `7C60F0E8D5BC48DF8EEBCC5FE3AA777E5802BC92EFFDEE39B91E81F5868F539F`
- Dateizeit: 10.08.2026 16:28:51

### Methode

- Dekompilierung mit `ilspycmd 10.1.0.8386`.
- Beide DLLs wurden mit demselben Referenzpfad auf die installierten Managed-DLLs dekompiliert. Das ist wichtig: Ohne diesen identischen Referenzpfad erzeugt ILSpy tausende rein darstellungsbedingte Unterschiede, etwa Enum-Zahlen statt Enum-Namen, zusätzliche Casts und anders gewählte Overloads.
- Anschließend erfolgte ein dateiweiser C#-Vergleich sowie für den verdächtigen Dictionary-Pfad zusätzlich eine Kontrolle der IL-Ausgabe.
- Nach Ausschluss der generierten Projektdatei blieben genau vier abweichende C#-Dateien.

Die nachvollziehbaren Dekompilate liegen unter:

- `.inspect\TruelyMostWanted_2.8.1_AssemblyDiff\vanilla_resolved`
- `.inspect\TruelyMostWanted_2.8.1_AssemblyDiff\modded_resolved`

## 1. `Director`: erweiterte Spielgeschwindigkeit

Betroffene Methoden: `IncreaseFrameRate()`, `DecreaseFrameRate()` und `SetEngineFrameRate(double fps)`.

### Vanilla-Verhalten

- Normaler Höchstwert: 90.
- Im Zuschauermodus: höchstens 300.
- Unterhalb beziehungsweise bis etwa 95 wird in 5er-Schritten erhöht, darüber in 25er-Schritten.
- Verringern ist nur oberhalb 10 möglich; unter 125 erfolgt es in 5er-, darüber in 25er-Schritten.
- Werte außerhalb des erlaubten Bereichs werden in `SetEngineFrameRate` nicht begrenzt, sondern auf 40 zurückgesetzt.
- Im Singleplayer werden nur Werte bis 90 in den Einstellungen gespeichert.

### Mod-Verhalten

- Globaler Wertebereich: 1 bis 1000, unabhängig vom Zuschauermodus.
- Erhöhen verwendet gestaffelte Schritte:
  - unter 5: auf 5
  - unter 90: `+5`
  - 90 bis unter 300: `+10`
  - 300 bis unter 500: `+20`
  - 500 bis unter 1000: `+50`
- Verringern verwendet gestaffelte Schritte:
  - bis 5: auf 1
  - über 5 bis 90: `-5`
  - über 90 bis 200: `-10`
  - über 200 bis 500: `-20`
  - über 500 bis 1000: `-50`
- `SetEngineFrameRate` begrenzt jeden Wert mit `Math.Clamp(..., 1, 1000)` statt ungültige Werte auf 40 zurückzusetzen.
- Im Singleplayer wird nun jeder Wert bis 1000 in `ConfigSettings.Settings_GameSpeed` persistiert.
- `SetEngineFrameRate` erzeugt selbst einen On-Screen-Eintrag für die neue Geschwindigkeit. `IncreaseFrameRate` und `DecreaseFrameRate` erzeugen danach zusätzlich weiterhin ihren eigenen Eintrag; bei diesen beiden Pfaden kann die Anzeige deshalb doppelt erfolgen.
- Die vorhandenen Kompensationsstufen für sehr niedrige Geschwindigkeiten bleiben erhalten.

### Nebenwirkung

Sehr hohe Werte bis 1000 werden jetzt absichtlich zugelassen. Ob die native Simulation, Animationen und Netzwerksynchronisation bei allen diesen Werten stabil bleiben, lässt sich aus der Managed-DLL allein nicht beweisen.

## 2. `KeyManager`: direkte Geschwindigkeits-Hotkeys

Im laufenden Spiel wurde ein neuer Block eingefügt. Solange linke Strg- und linke Umschalttaste gehalten werden, setzen die Zifferntasten folgende Geschwindigkeiten:

| Tastenkombination | gesetzter Wert |
|---|---:|
| Linke Strg + linke Umschalt + 1 | 10 |
| Linke Strg + linke Umschalt + 2 | 20 |
| Linke Strg + linke Umschalt + 3 | 40 |
| Linke Strg + linke Umschalt + 4 | 60 |
| Linke Strg + linke Umschalt + 5 | 80 |
| Linke Strg + linke Umschalt + 6 | 120 |
| Linke Strg + linke Umschalt + 7 | 160 |
| Linke Strg + linke Umschalt + 8 | 200 |
| Linke Strg + linke Umschalt + 9 | 240 |
| Linke Strg + linke Umschalt + 0 | 240 |

Auffälligkeiten:

- `0` setzt ebenfalls 240. Das kann Absicht sein, wirkt aber eher wie ein Tippfehler oder ein nicht fertig belegter letzter Geschwindigkeitswert.
- Der Code verwendet `Input.GetKey`, nicht `Input.GetKeyDown`. Während die Kombination gehalten wird, wird `SetEngineFrameRate` daher in jedem Frame erneut aufgerufen. Wegen des neu dort eingebauten On-Screen-Texts kann dies unnötig viele Meldungen beziehungsweise Aktualisierungen erzeugen.
- Der große Switch in derselben Methode wurde vom verwendeten Compiler neu angeordnet. Die Fälle für Lesezeichen, Baugebäude, Clans und Extremkräfte sind inhaltlich weiterhin vorhanden. Auch die umgeordnete Escape-/Workshop-Uploader-Bedingung ist logisch äquivalent zu Vanilla. Diese Umordnungen sind keine zusätzlichen Modfeatures.

## 3. `HUD_AlliesPanel`: flexiblere Gütermengen

Die Methode `ButtonClicked(string param)` wurde um eine generische Verarbeitung der Mengenbuttons erweitert.

### Neue Modifikatoren

- Linke Umschalttaste: Faktor `5`.
- Linke Strg-Taste: Faktor `0,2`.
- Linke Alt-Taste bei einem `X...`-Mengenbutton: setzt die ausgewählte Menge direkt auf `0`.
- Sind Umschalt und Strg gleichzeitig gedrückt, gewinnt Strg, weil der Faktor nacheinander erst auf 5 und dann auf 0,2 gesetzt wird.

Der resultierende Betrag wird in `int` umgewandelt und dadurch abgeschnitten. Beispiele für Strg:

| normaler Buttonwert | Änderung mit linker Strg-Taste |
|---:|---:|
| 5 | 1 |
| 10 | 2 |
| 25 | 5 |
| 100 | 20 |
| 500 | 100 |

### Refactor

- Vanilla hatte einzelne Switch-Fälle für `X5`, `X10`, `X25`, `X100`, `X500` und die jeweiligen Minusvarianten.
- Die Mod parst stattdessen jeden Parameter im Format `X<Zahl>` oder `X<Zahl>-` generisch.
- Negative Ergebnisse werden weiterhin auf 0 begrenzt.
- Die Güterauswahl wurde ebenfalls von 24 einzelnen Fällen `G01` bis `G24` auf generisches Parsen von `G<Zahl>` umgestellt.
- Die übrigen Ally-Befehle wurden überwiegend nur neu angeordnet; Angreifen, Verteidigen, Waren senden/anfordern, Abbrechen und Zurückkehren bleiben funktional vorhanden.

### Kleine Verhaltensausweitung

Durch das generische Parsen akzeptiert die Methode nun auch vorher nicht explizit vorgesehene numerische `X...`- und `G...`-Parameter. Die normale XAML-Oberfläche dürfte weiterhin nur ihre bekannten Werte liefern. Ein ungültiger, aber numerischer `G`-Index wird an dieser Stelle nicht gegen den gültigen Warenbereich geprüft.

## 4. `FRONT_Multiplayer`: großer Buttonhandler-Refactor

Dies ist der mit Abstand größte Quellunterschied. Die Anzahl normaler UI-Aktionen wurde jedoch nicht funktional reduziert: 160 vorher explizite Switch-Fälle wurden durch Präfix-/Mustererkennung und Hilfsmethoden ersetzt.

### Neu vorgeschaltete Routen in `ButtonClicked`

Vor dem großen Switch werden jetzt unter anderem folgende Parametergruppen abgefangen:

- `Kick_*` → `KickPlayerFromLobby`
- exakt `Radar0` bis `Radar9`-ähnliche sechsstellige Parameter → `SelectRadarKeep`
- `Coop_Friend*` → Fortschritt beziehungsweise Verbergen/Anzeigen
- `TeamFaceUp*` → `ForwardTeamFaceUp`
- `STRUCT_*` → Gebäudeverfügbarkeit
- `GOODS_*` → Handelswarenverfügbarkeit
- `TROOPS_*` → Truppenverfügbarkeit
- `AISettings_*` → KI-Einstellungsfenster
- `CoopContinue*` → Koop-Freund als Skirmish-KI
- `RadarUp*` → Radar-/Keep-Gruppierung
- `TeamFace*` → Teamauswahl
- `Setup` und `LobbySettings` → gemeinsamer Settings-Dialog
- Parameter mit Suffix `_leave` → verzögertes Ausblenden des Tooltips

### Neu angelegte Hilfsmethoden

- `ToggleBuildingAvailability(int)`
- `ToggleTradeGoodAvailability(string)`
- `ToggleTroopAvailability(string)`
- `ShowCoopFriendHidePanel(string)`
- `AddCoopFriendAsSkirmishAI(string)`
- `MoveRadarKeepGroupUp(string)`
- `HideGameTypeTooltip()`
- `ShowAISettingsPanel(string)`
- `SelectTeamFace(string)`
- `OpenMultiplayerSettingsPanel(string)`
- `KickPlayerFromLobby(string)`
- `SelectRadarKeep(string)`
- `ShowCoopFriendProgress(string)`
- `ForwardTeamFaceUp(string)`

Der Code in diesen Hilfsmethoden entspricht weitgehend den aus dem früheren Switch herausgezogenen Vanilla-Blöcken. Der hauptsächliche Zweck scheint eine Verkürzung und Generalisierung des sehr großen Buttonhandlers zu sein.

### Wahrscheinlicher schwerer Fehler: uninitialisiertes Dictionary

Die Mod fügt folgendes Feld hinzu:

    private static readonly Dictionary<string, int> BuildingIndexByName;

Beim Empfang eines Parameters mit `STRUCT_` wird unmittelbar Folgendes ausgeführt:

    BuildingIndexByName.TryGetValue(param, out var value)

Weder das Dekompilat noch die kontrollierte IL-Ausgabe enthalten jedoch eine Zuweisung beziehungsweise ein `stsfld` für `BuildingIndexByName`. Es existiert nur das Feld und dessen `ldsfld` beim Lesen. Das Feld behält daher den CLR-Standardwert `null`.

Folge: Jeder normale Klick auf einen `STRUCT_*`-Schalter im erweiterten Multiplayer-Setup dürfte an `TryGetValue` mit einer `NullReferenceException` scheitern. Vanilla behandelte die 13 Gebäudenamen einzeln und hatte dieses Problem nicht.

Die erwartete Zuordnung müsste sinngemäß die 13 vorhandenen Gebäudeschalter auf die Indizes 0 bis 12 abbilden:

1. `STRUCT_BARRACKS_STONE`
2. `STRUCT_BARRACKS_WOOD`
3. `STRUCT_BEDOUIN_STOCKADE`
4. `STRUCT_CATTLEFARM`
5. `STRUCT_APPLEFARM`
6. `STRUCT_WHEATFARM`
7. `STRUCT_HOPSFARM`
8. `STRUCT_TRADEPOST`
9. `STRUCT_BALLISTA`
10. `STRUCT_MANGONEL`
11. `STRUCT_PITCH_DIGGER`
12. `STRUCT_CHURCH`
13. `STRUCT_MOAT`

### Robustheitsunterschiede des Refactors

- Mehrere neue Präfixpfade sind breiter als die alten exakten Switch-Fälle. Beispielsweise wird jeder String mit `Kick_`, `GOODS_`, `TROOPS_` oder `AISettings_` verarbeitet.
- Einige Helfer verwenden `int.Parse` oder lesen die letzte Zeichenposition ohne vollständige Format- und Bereichsprüfung. Normale UI-Parameter sind passend formatiert; unerwartete oder extern eingespeiste Parameter können jetzt aber eher `FormatException`, `IndexOutOfRangeException` oder einen ungültigen Arrayzugriff auslösen.
- `System.IO` wurde als Namespace ergänzt, weil der herausgezogene Kick-Pfad weiterhin `File.Exists` für benutzerdefinierte Lord-Sprachausgabe verwendet. Das ist keine neue externe Abhängigkeit.

## 5. Recompiler- und Darstellungsartefakte

Mehrere sichtbare Unterschiede sind keine absichtlichen Gameplay-Änderungen:

- Statische Feldinitialisierungen wurden bei neu kompilierten Klassen teilweise in explizite statische Konstruktoren verschoben, zum Beispiel `Director.instance = null`. Das ist semantisch gleichwertig.
- Große Switch-Anweisungen wurden neu sortiert und lokale Variablen umnummeriert.
- Logisch identische Bedingungen wurden invertiert oder in anderer Reihenfolge ausgegeben.
- Die generierte `Assembly-CSharp.csproj` unterscheidet sich in Referenzreihenfolge und HintPaths, nicht durch eine neue Laufzeitabhängigkeit.
- Ein erster Vergleich ohne gemeinsamen Referenzpfad erzeugte zahlreiche falsche Unterschiede. Diese wurden ausdrücklich nicht als Modänderungen gewertet.

## 6. Gesamtbewertung

Mit hoher Sicherheit beabsichtigte Änderungen:

- Spielgeschwindigkeit von 1 bis 1000 statt regulär 10 bis 90 beziehungsweise 300 als Zuschauer.
- Feinere, gestaffelte Geschwindigkeitsänderung und Persistenz hoher Singleplayer-Werte.
- Direkte Geschwindigkeitswahl über linke Strg + linke Umschalt + Ziffer.
- Mengenfaktoren und Alt-Reset im Güterdialog mit Verbündeten.
- Generalisierung und Aufteilung des Multiplayer-Buttonhandlers.

Mit hoher Sicherheit problematisch:

- `BuildingIndexByName` wird nicht initialisiert und kann beim ersten `STRUCT_*`-Klick eine `NullReferenceException` auslösen.
- Die Geschwindigkeits-Hotkeys laufen durch `GetKey` potenziell jeden Frame.
- Strg+Umschalt+0 dupliziert den Wert 240 von Strg+Umschalt+9.

Mit mittlerer Sicherheit nur Refactor ohne beabsichtigte Verhaltensänderung:

- Die meisten ausgelagerten Multiplayer-Hilfsmethoden.
- Umordnung des großen `KeyManager`-Switches und der Escape-/Workshop-Logik.

## Grenzen der Analyse

- Verglichen wurde ausschließlich die angegebene Managed-DLL mit der aktuell installierten Vanilla-`Assembly-CSharp.dll` anhand der oben dokumentierten Hashes.
- Begleitende XAML-, Asset-, native DLL- oder Konfigurationsänderungen außerhalb dieser Datei sind nicht Teil dieses Vergleichs.
- Dekompilierter C#-Code ist eine Rekonstruktion. Der kritische Dictionary-Befund wurde deshalb zusätzlich anhand des IL-Lese-/Schreibzugriffs geprüft.
- Die Analyse beschreibt statische Codeunterschiede. Ein Laufzeittest der modifizierten DLL wurde nicht durchgeführt und war für den angeforderten Vergleich nicht erforderlich.
