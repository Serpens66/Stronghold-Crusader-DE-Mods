# Vergleich der „TruelyMostWanted DLL v2.8.1“ mit der installierten Vanilla-DLL

## Kurzfazit

Die DLL ist eine direkt veränderte, erneut kompilierte `Assembly-CSharp.dll`. Beide Assemblies enthalten dieselben 164 dekompilierten C#-Typdateien; es wurden keine neuen Klassen, externen Mod-Abhängigkeiten oder erkennbaren Ressourcen hinzugefügt. Nach identischer Auflösung aller Unity-/Noesis-Abhängigkeiten weichen nur vier Spielklassen inhaltlich ab:

1. `Director`
2. `KeyManager`
3. `CrusaderDE.HUD_AlliesPanel`
4. `CrusaderDE.FRONT_Multiplayer`

Die wesentlichen Features sind eine stark erweiterte Spielgeschwindigkeitssteuerung, direkte Geschwindigkeits-Hotkeys, flexiblere Mengenänderungen beim Gütertransfer mit Verbündeten, eine alphabetische Sortierung der Custom-Lord-Liste sowie ein großer Refactor des Multiplayer-Buttonhandlers. Im Multiplayer-Refactor steckt sehr wahrscheinlich ein echter Fehler: Ein neues Dictionary für Gebäudeschalter bleibt `null` und wird anschließend verwendet.

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

## 5. Prüfung der beworbenen Custom-Lord-Funktionen

Die vom Autor beworbenen Punkte wurden nachträglich einzeln gegen Vanilla geprüft. Ergebnis: Die automatische A–Z-Sortierung ist in dieser DLL klar vorhanden. Die übrigen fünf Aussagen lassen sich in der angegebenen DLL nicht bestätigen; mehrere werden durch identischen Vanilla-Code direkt widerlegt.

| Werbeaussage | Befund in dieser DLL |
|---|---|
| Custom-Lord-Menü sortiert automatisch A–Z | **Bestätigt** |
| Umschalt beim Custom-Lord-Button sortiert Z–A | **Nicht vorhanden** |
| Fehlende Suchleiste beziehungsweise Sortierbuttons im Custom-Lord-Panel | **Nicht in dieser DLL erkennbar** |
| Lordtyp-Filter im „Add Random Lords“-Panel | **Nicht vorhanden** |
| Lordtyp-Filter im „Random Skirmish“-Panel | **Nicht vorhanden** |
| Custom Lords im Zufalls-Lord-Button | **Nicht vorhanden** |

### 5.1 Automatische Sortierung A–Z: bestätigt

Die Custom-Lord-Liste wird in `FRONT_Multiplayer.SkirmishAIAddClick(string param)` beim numerischen Aktionswert `98` aufgebaut.

Vanilla verwendet die vom `CustomisationFileManager` zurückgegebene Reihenfolge unverändert:

    List<CustomisationFileManager.CustomLord> customLords =
        CustomisationFileManager.Instance.GetCustomLords();

Die Mod ersetzt dies durch:

    List<CustomisationFileManager.CustomLord> list =
        CustomisationFileManager.Instance.GetCustomLords()
            .OrderBy(e => e.lordDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

Umsetzung und Auswirkung:

- Sortierschlüssel ist `lordDisplayName`, also der sichtbare Lordname.
- `StringComparer.OrdinalIgnoreCase` ignoriert Groß-/Kleinschreibung und verwendet einen kulturunabhängigen ordinalen Vergleich.
- Die sortierte Liste wird anschließend in `customLordRows` überführt und als `ItemsSource` von `RefCustomLordList` gesetzt.
- Bei mindestens einem Eintrag wird weiterhin Index 0 vorausgewählt.
- Workshop-Lords behalten weiterhin ihr vorhandenes Typbild; die Sortierung unterscheidet nicht zwischen lokalen und Workshop-Lords.

Dieser Punkt ist eine echte Modänderung und keine Decompiler-Umordnung.

### 5.2 Umschalt für Z–A: in dieser DLL nicht umgesetzt

Im Aufbau der Custom-Lord-Liste existiert weder:

- `OrderByDescending`,
- `Reverse()` beziehungsweise `Enumerable.Reverse`,
- eine Abfrage von linker oder rechter Umschalttaste,
- noch eine alternative absteigende Vergleichsfunktion.

Die einzige Abfrage von `KeyCode.LeftShift` in der gesamten modifizierten `FRONT_Multiplayer`-Klasse befindet sich in einer anderen Buttonaktion und ändert den Startgüter-Level von 1 auf 4. Sie steht in keinem Zusammenhang mit Custom Lords oder deren Sortierung. `KeyCode.RightShift` kommt dort gar nicht vor.

Damit sortiert diese konkrete DLL den Custom-Lord-Dialog immer A–Z. Das Halten von Umschalt beim Teleskop-/Kompass-Button kann den C#-Code dieser DLL nicht auf Z–A umstellen.

### 5.3 Suchleiste und Sortierbuttons im Custom-Lord-Panel: nicht erkennbar

Die Klasse besitzt zwar `RefMP_SearchFilter` mit Fokus-, Textänderungs-, Escape- und Eingabehandlern. Diese Elemente und Handler sind jedoch bereits unverändert in Vanilla vorhanden und filtern die Multiplayer-Karten- beziehungsweise Lobbyliste über `populateMapList()`/`populateLobbyList()`. Sie sind nicht mit `customLordRows` oder `RefCustomLordList` verbunden.

Für die Custom-Lord-Liste enthält die Mod-DLL:

- kein neues Such-Textfeld,
- keinen neuen TextChanged-Handler,
- keine Filterung von `customLordRows`,
- keine neuen Sortierbutton-Handler,
- und keine zusätzlichen Buttonaktionsnamen für eine Custom-Lord-Suche oder Sortierrichtung.

Beide untersuchten Assemblies enthalten außerdem keine eingebetteten Ressourcen. In der bereitgestellten `Assembly-CSharp.dll` steckt daher kein zusätzliches Noesis-XAML, das eine fehlende Suchleiste oder Sortierbuttons optisch ergänzen könnte.

Falls ein vollständiges Modpaket separat veränderte XAML-/Asset-Dateien enthält, könnten dort UI-Elemente ergänzt sein. Die hier untersuchte DLL liefert dafür aber weder die UI-Ressource noch die erkennbare C#-Filterlogik.

### 5.4 Lordtyp-Filter im „Add Random Lords“-Panel: nicht vorhanden

Der betreffende Pfad liegt ebenfalls in `SkirmishAIAddClick`. Die Mod enthält dort keine neue Auswahlmaske, keine Liste erlaubter `lordType`-Werte und keine Prüfung des Lordtyps vor dem zufälligen Hinzufügen.

Auch `UpdateRandomAIButtons()` ist zwischen Vanilla und Mod ordinal identisch. Die Methode aktiviert lediglich die Buttons für eine bestimmte Anzahl zufälliger KI-Spieler anhand freier Spielerplätze. Sie wertet keine Lordtypen aus.

Die Funktion zum Hinzufügen von 1 bis 8 zufälligen Lords verwendet weiterhin ausschließlich `random.Next(29)` und prüft danach nur, ob die zugehörigen DLCs vorhanden sind. Ein Lordtyp-Filter ist in diesem Pfad nicht vorhanden.

### 5.5 Lordtyp-Filter im „Random Skirmish“-Panel: nicht vorhanden

Die beiden maßgeblichen Methoden wurden als vollständige Methodenblöcke ordinal verglichen:

- `CreateRandomSkirmish()` ist exakt identisch zu Vanilla (11.629 Zeichen auf beiden Seiten).
- `updateRandomSkirmishPanel()` ist exakt identisch zu Vanilla (2.114 Zeichen auf beiden Seiten).

In diesen Methoden gibt es keine neue Lordtyp-Auswahl oder Custom-Lord-Filterung. Der Zufallsgenerator verändert weiterhin Kartengröße, Zahl der KIs, Spieltyp, Vorteil, Startbedingungen und erweiterte Spieloptionen wie in Vanilla. Diese Werbeaussage wird von der untersuchten DLL nicht getragen.

### 5.6 Custom Lords im Zufalls-Lord-Button: nicht vorhanden

Der einzelne Zufalls-Lord-Button verwendet weiterhin den Aktionswert `99`. Der Ablauf ist in Mod und Vanilla fachlich gleich:

1. `System.Random` wird erzeugt.
2. Mit `random.Next(29)` wird ein eingebauter Lordindex von 0 bis 28 gewählt.
3. Nicht besessene DLC-Lords werden verworfen und neu gezogen.
4. Der Lord wird über `Platform_Multiplayer.Instance.AddSkirmishPlayerLocal(int lordIndex, ...)` hinzugefügt.
5. `AIVs[...]` wird nur mit diesem numerischen Vanilla-/DLC-Lordindex initialisiert.

Die Routine ruft in diesem Pfad weder `GetCustomLords()` noch `AddCustomSkirmishPlayerLocal(CustomLord, ...)` auf. Dasselbe gilt für das gleichzeitige Erzeugen von 1 bis 8 zufälligen Lords über die negativen Aktionswerte `-1` bis `-8`.

Custom Lords werden ausschließlich über den separaten Custom-Lord-Dialog und dessen `AddCustomLord`-Aktion hinzugefügt. Sie sind in dieser DLL nicht Bestandteil der Zufallsauswahl.

### 5.7 Mögliche Erklärung für die Abweichung zur Werbung

Die Werbung könnte sich auf eine andere Version der DLL, auf nicht mitgelieferte Begleitdateien oder auf geplante, aber in Version 2.8.1 nicht fertig eingebaute Funktionen beziehen. Für diese konkrete Datei mit SHA-256 `F64CE976BC160111B56B5CFF6B29824595AFA41C80B491A17B01062A71DC0F1C` ist nur die A–Z-Sortierung nachweisbar.

## 6. Recompiler- und Darstellungsartefakte

Mehrere sichtbare Unterschiede sind keine absichtlichen Gameplay-Änderungen:

- Statische Feldinitialisierungen wurden bei neu kompilierten Klassen teilweise in explizite statische Konstruktoren verschoben, zum Beispiel `Director.instance = null`. Das ist semantisch gleichwertig.
- Große Switch-Anweisungen wurden neu sortiert und lokale Variablen umnummeriert.
- Logisch identische Bedingungen wurden invertiert oder in anderer Reihenfolge ausgegeben.
- Die generierte `Assembly-CSharp.csproj` unterscheidet sich in Referenzreihenfolge und HintPaths, nicht durch eine neue Laufzeitabhängigkeit.
- Ein erster Vergleich ohne gemeinsamen Referenzpfad erzeugte zahlreiche falsche Unterschiede. Diese wurden ausdrücklich nicht als Modänderungen gewertet.

## 7. Gesamtbewertung

Mit hoher Sicherheit beabsichtigte Änderungen:

- Spielgeschwindigkeit von 1 bis 1000 statt regulär 10 bis 90 beziehungsweise 300 als Zuschauer.
- Feinere, gestaffelte Geschwindigkeitsänderung und Persistenz hoher Singleplayer-Werte.
- Direkte Geschwindigkeitswahl über linke Strg + linke Umschalt + Ziffer.
- Mengenfaktoren und Alt-Reset im Güterdialog mit Verbündeten.
- Automatische, groß-/kleinschreibungsunabhängige A–Z-Sortierung der Custom-Lord-Liste.
- Generalisierung und Aufteilung des Multiplayer-Buttonhandlers.

Nicht in dieser DLL nachweisbar, obwohl beworben:

- Z–A-Sortierung durch Halten von Umschalt.
- Custom-Lord-Suchleiste oder zusätzliche Sortierbuttons.
- Lordtyp-Filter in den beiden Zufallspanels.
- Custom Lords im Zufalls-Lord-Button.

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
