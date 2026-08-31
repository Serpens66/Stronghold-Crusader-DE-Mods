# Extreme Powers: Modding-Erkenntnisse

## Untersuchter Spielstand

- Steam-App: Stronghold Crusader Definitive Edition, öffentlicher Build `24816905`.
- Kanonische Datei: `Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll`.
- Dateigröße: `3.451.392` Bytes.
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Installierte Datei, zuletzt geändert (UTC): `2026-08-24 15:06:48`.
- PE-Compile-Zeit laut Rizin: `2026-08-19 13:01:20` (im Header als UTC+1 bezeichnet).
- Alle nachfolgenden RVAs gelten ausschließlich für diesen Hash. Ein anderer Hash muss vollständig auf Vanilla zurückfallen.

## Bereits vorhandene Script-Extender-Oberfläche

`GamePlayerManagerAPI` bietet derzeit nur das Aktivierungsflag und direkten Mana-Zugriff:

- `IsLocalPlayerExtremePowersEnabled()` / `SetLocalPlayerExtremePowersEnabled(bool)`.
- `GetLocalPlayerExtremePowersMana(int)` / `SetLocalPlayerExtremePowersMana(int, int)`.
- `GamePlayerResources.r_ExtremePowersMana` liegt bei `+0x39D4`.

Es gibt bislang keine Script-Extender-API für Kosten, Regeneration, Effektparameter, Zielwahl oder das Ersetzen einer Power. Die Unit-API stellt dagegen `CreateUnitWorld` und `CreateUnitLocal` bereit und ist damit für eine deterministisch auf allen Peers aufgerufene Replacement-Funktion geeignet.

## Managed UI und IDs

- `Enums.GameActionCommand.ExtremePower = 1068`.
- `MainViewModel.ButtonExtremeClick` ruft `EngineInterface.GameAction(ExtremePower, powerId, 0)` auf.
- Die Vanilla-XAML ordnet die acht Buttons den IDs `0..7` zu.
- Reihenfolge: Pfeilsalve, Heilung, Speerträger, Ingenieure, Streitkolbenkämpfer, Gold, Ritter, Steinsalve.
- `GameData` schaltet die Buttons bei `636`, `1272`, `1908`, `2544`, `3180`, `3816`, `4452`, `5088` Mana frei.

## Bestätigte native Funktionen

### Managed GameAction-Gateway

- Export `DLL_GameAction`: VA `0x180081870`, RVA `0x81870`.
- Der Switch deckt die Commands 1002–1073 ab; Index 66 beziehungsweise Command 1068 springt über den Tabellenwert `0x0008257E` nach VA `0x18008257E`.
- Der Fall prüft Spiel-/Editorzustand und ruft für die Power-ID in `ECX` VA `0x180105510` auf.
- Referenzbytes am Eintritt des Falls: `83 3D ?? ?? ?? ?? 00 75 ?? 8D 86 18 FC FF FF 83 F8 01`.

### Power-Auswahl, Kostenprüfung und Targeting-Vorbereitung

- Funktion VA `0x180105510`, RVA `0x105510`, Funktionsende `0x180105655`.
- Spielerressourcen haben den Stride `0x583C`; die Funktion liest das Mana aus dem bestätigten Ressourcenfeld `+0x39D4` (als bildbasierte Adresse `0x379E7A4`).
- Für reguläre IDs berechnet `imul ecx, eax, 0x27C` mit `eax = powerId + 1` exakt `(powerId + 1) * 636`.
- Die Prüfung ist `cmp edi, ecx` / `jl reject`.
- Power 5 (Gold) wechselt direkt in einen Chore-/Befehlsweg. Die übrigen Powers setzen einen Zielmodus; die Sprungtabelle beginnt bei VA `0x180105658`.
- Bestätigte Eintrittssignatur: `40 53 48 83 EC 20 8B 05 ?? ?? ?? ?? 8B D9 85 C0`.
- Die API detourt diese Auswahlfunktion erst nach Hash- und Signaturprüfung. Für abweichende Kosten wird das Mana nur während des Vanilla-Aufrufs kompensiert; danach wird der unveränderte Wert wiederhergestellt. Damit bleiben Vanilla-Targeting und Abbruchverhalten erhalten.

### Finaler Effekt-Dispatcher, Kostenprüfung und Manaabzug

- Funktion VA `0x1800CD630`, RVA `0xCD630`, Funktionsende `0x1800CD82F`.
- Signatur: `48 89 5C 24 10 48 89 6C 24 18 48 89 74 24 20 57 48 83 EC 40`.
- Aufrufparameter: Spieler-ID in `EDX`, Power-ID in `R8D`, Ziel-Tile-ID in `R9D`.
- Die Funktion berechnet erneut `(powerId + 1) * 636`, prüft das Mana und zieht es erst nach dem Effekt bei VA `0x1800CD814` ab.
- Sprungtabelle bei VA `0x1800CD830`: `CD7AF, CD73E, CD688, CD688, CD688, CD774, CD688, CD7D6`.
- Die gemeinsame Spawn-Routine ruft VA `0x1801264D0` auf. Bestätigte Vanilla-Paare Einheit/Anzahl: Power 2 `24/20`, Power 3 `30/14`, Power 4 `26/20`, Power 6 `28/10`.
- Heilung (Power 1) ruft VA `0x1800E1E70` mit Radiusparameter `6` und Wert `0x1F40 = 8000` auf.
- Gold (Power 5) addiert `1000 + (Zeitwert mod 1500)`, also inklusive Grenzen `1000..2499`, zum Goldfeld direkt hinter dem Manafeld.
- Pfeilsalve (Power 0) ruft VA `0x1800DD6C0` mit Radius `6`, Wert `0x1770 = 6000` und Projektilmodus `1` auf.
- Steinsalve (Power 7) ruft dieselbe Routine mit Radius `9`, Wert `0x4650 = 18000` und Projektilmodus `0` auf.
- Die Routine reicht den Wert an die Trefferpunkte-Subtraktion gefundener Einheiten weiter; er ist damit als Stärke/Schaden verwendbar. Die Routine ist `0x9C2 = 2498` Bytes groß und durchsucht Einheiten innerhalb der Zielregion.
- Eine separat steuerbare Projektilzahl oder Streuung ist weder im Aufruf noch in der untersuchten Routine belegt. Die API bietet dafür bewusst keine wirkungslose Eigenschaft an.
- Zusätzlich abgesicherte Hilfseingänge: Heilung RVA `0xE1E70` mit `48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48`, Salve RVA `0xDD6C0` mit `44 89 4C 24 20 44 89 44 24 18 48 89 4C 24 08 53` und Gold-Zyklus RVA `0x7530` mit `4C 63 81 48 9C 00 00 33 C0 42 0F B7 54 41 08 41`.

### Auswahlzustand

- Globaler ausgewählter Power-Index: VA `0x18366A0C4` für diesen Build.
- Schreibstellen befinden sich im `DLL_GameAction`-Fall bei VA `0x1800825E3` bis `0x180082667`.
- Dieser Wert ist UI-/Auswahlzustand, nicht der finale Effekt-Dispatcher. Mehrere weitere Leser gehören Save-/Diagnose- oder Darstellungspfaden und dürfen nicht vorschnell als Effektausführung interpretiert werden.

## Vanilla-Werte und Validierungsstatus

| Bereich | Wert | Status |
|---|---:|---|
| Kosten 0–7 | `636 × (ID + 1)` | im Managed HUD und in nativer Auswahlprüfung bestätigt |
| Mana | `GamePlayerResources + 0x39D4` | Script Extender und native Auswahlfunktion bestätigt |
| Regeneration | Vanilla erhöht höchstens um `1`, Grenze `7000`; API-Skalierung `0..1000 %` | Ressourcenroutine und interner Writer bestätigt und signaturgesichert |
| Pfeil-/Steinsalve | Schaden `6000`/`18000`, Radius `6`/`9`, Projektilmodus `1`/`0` | Aufruf und Schadenspfad bestätigt; keine separate Projektilzahl/Streuung belegt |
| Heilung | Menge `8000`, Radiusparameter `6` | finaler Effektzweig bestätigt |
| Speerträger | Typ `24`, Anzahl `20` | finaler Effektzweig bestätigt |
| Ingenieure | Typ `30`, Anzahl `14` | finaler Effektzweig bestätigt |
| Streitkolbenkämpfer | Typ `26`, Anzahl `20` | finaler Effektzweig bestätigt |
| Ritter | Typ `28`, Anzahl `10` | finaler Effektzweig bestätigt |
| Gold | `1000 + (Zeitwert mod 1500)`, somit `1000..2499` | finaler Effektzweig bestätigt |

Unbekannte Effektwerte werden in der API nicht als angebliche Vanilla-Werte ausgegeben. Der native Mutationspfad wird ausschließlich für den exakten DLL-Hash und zusätzlich passende Eintrittssignaturen aktiviert.

## Regeneration

- Die Ressourcenroutine beginnt bei VA `0x1800CDB20`, RVA `0xCDB20`. Der einzige bestätigte direkte Aufrufer liegt bei VA `0x1800CE261`; unmittelbar davor steht `mov rcx, rbx` bei RVA `0xCE25E`.
- Funktionsprolog: `48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 4C 8B D1`.
- Der Regenerationsblock reicht von VA `0x1800CDD87` bis `0x1800CDDCB` (RVA `0xCDD87..0xCDDCB`). Er liest das durch die lokale Basisverschiebung als `+0x3950` adressierte Ressourcenfeld, entsprechend `GamePlayerResources.r_ExtremePowersMana + 0x39D4`, vergleicht mit `0x1B58 = 7000` und schreibt höchstens `+1` zurück.
- Blockanfang: `45 8B 88 50 39 00 00 41 81 F9 58 1B 00 00 7D 35`. Der vollständige 69-Byte-Block, die Callsite und der Funktionstail bei RVA `0xCDE3C` sind Teil des Build-Guards.
- Die Vanilla-Kadenz verwendet eine Division-durch-drei-Sequenz (`0x55555556`) und erhöht nur bei dem bestätigten Restwert `2`.
- Die API detourt ausschließlich diese Ressourcenroutine, nimmt vor dem Originalaufruf einen Mana-Snapshot und skaliert nachher nur das exakt bestätigte Delta `+1`. Andere Deltas bleiben unverändert und werden einmalig protokolliert. Dadurch werden Mana-Gutschriften von Cheat Mod oder anderen Schreibern außerhalb dieser Routine nicht mehr als Regeneration interpretiert.
- Ganzzahlige Reste werden pro Spieler akkumuliert, sodass zum Beispiel zwei Vanilla-Schritte bei `50 %` zusammen genau einen Punkt ergeben.
- `100 %` lässt Vanilla unverändert; `0 %` entfernt den bestätigten Schritt; `1000 %` erzeugt zehn Punkte pro bestätigtem Vanilla-Schritt, stets begrenzt auf `7000`. Karten-Unload löscht alle Akkumulatoren.

## Netzwerk und Determinismus

- SHCDE arbeitet mit deterministischen Chores/Commands. Ein Replacement darf nicht nur lokal eine Einheit erzeugen.
- Das API-Paketformat ist explizit und enthält Protokoll, Power-ID, Spieler-ID, Zielart, Tile beziehungsweise globale Unit-ID und Operation-ID. Ungültige Länge, Version, Spieler, Power oder Ziel werden verworfen.
- `ExtremePowers.API.dll` registriert einen eigenen `R3PacketEventHook<ExtremePowerChore>` mit explizitem `IMessagePackFormatter`; das Array besitzt exakt sieben Felder.
- `QueueReplacement` sendet über den Script-Extender-Chore-Transport. Empfangene Pakete werden auf Protokoll, Power, Spieler, Zielart, Tile/globaler Unit-ID, Registrierung, Kosten und Duplikate geprüft. Erst danach laufen Callback und Manaabbuchung im empfangenden Chore-Tick.
- Der Chore-Empfang des Script Extenders liefert absichtlich keine `SenderSteamId`; Pakete mit gesetzter Sender-ID werden verworfen. Eine kryptographische Absender-Spieler-Zuordnung ist in diesem Transport daher nicht verfügbar. Spieler-ID, aktiver Spielerslot und alle deterministischen Nutzdaten werden dennoch auf jedem Peer neu validiert.
- Die Loganalyse des lokalen Skirmish zeigte `players=[1], unresolved=True`. Die frühere Bereitschaftsschranke verlangte auch dort `System_ArePerPlayerSettingsReady(...)`, weshalb Auswahl und Dispatcher still an Vanilla weiterleiteten und die Gold-Power weiterhin Gold gab. Das war die bestätigte Ursache des Testfehlers.
- Singleplayer, Trail und lokaler Skirmish umgehen Teilnehmerberichte jetzt vollständig. Nur ein mit `Shared.GameModeHelper.Capture(args.bMultiplayerSave != 0)` als echter Multiplayer festgehaltener Kartenlauf verlangt vollständige Berichte aller menschlichen Teilnehmer.
- Der Bericht ist kein bloßes `1` mehr, sondern enthält API-Protokoll, kanonischen DLL-Hash, Native-/Vanilla-Backendstatus und die vom Script Extender dynamisch zugeteilte Paket-ID. Da `GetPacketEventFor<T>()` freie IDs in Registrierungsreihenfolge vergibt, wird eine abweichende Mod-Ladereihenfolge dadurch erkannt und die gesamte Modifikation fällt auf Vanilla zurück.
- Der direkte Vanilla-HUD-Weg nutzt den bereits synchronisierten Extreme-Power-Chore des Spiels. Für die Gold-Demo wird zur Kartenpunktwahl die Vanilla-Auswahl der Pfeilsalve vorbereitet und danach nur der ausgewählte Power-Index wieder auf Gold gestellt.
- Operationen des generischen API-Chores werden pro Spieler und Operation-ID dedupliziert; Karten-Unload verwirft die Historie.
- `QueueReplacement` liefert neben dem Ergebnis einen Ablehnungsgrund. Auswahl, Dispatcher und Chorepfad melden Zustandswechsel beziehungsweise begrenzte Ausführungsdiagnosen mit Power, Spieler, Ziel, Mana, Kosten, Operation und Callbackfehler.

## Spawn- und Kostenregel

- Im Vanilla-Dispatcher ruft der Spawnzweig RVA `0x1264D0` auf und prüft dessen Rückgabewert nur für eine nachgelagerte Einheiteninitialisierung. Der gemeinsame Manaabzug bei RVA `0xCD814` wird auch bei Rückgabewert `0` erreicht.
- Die API übernimmt diese Semantik: Nach erfolgreichem `CanExecute` und normal zurückgekehrtem Callback werden die vollständigen Kosten abgezogen, auch wenn wegen des Einheitenlimits nur ein Teil oder keine Einheit erzeugt wurde. Eine `CanExecute`-Ablehnung oder Callback-Exception kostet nichts.
- Spawn-Typen müssen definierte `eChimps`-Werte strikt zwischen `CHIMP_TYPE_NULL` und `CHIMP_NUM_TYPES` sein. Die tatsächliche Spawnanzahl wird protokolliert.
- Temporäre Kostenkompensation verwendet einen breiteren Zwischenwert und fällt bei nicht darstellbarem Ergebnis ohne Exception auf Vanilla zurück. Goldaddition sättigt am nativen `UInt32`-Maximum.

## Targeting-Status

- `None` und `MapPoint` sind implementiert. Kartenpunkte verwenden weiterhin Vanillas Zielmodus und Abbruchverhalten.
- Für `Unit` wurde im kanonischen Build noch kein eindeutig validierter gemeinsamer Click-/Unit-Pick-Einstieg nachgewiesen. `SupportsUnitTargeting` ist daher bewusst `false`; Unit-Replacements werden bereits bei der Registrierung abgelehnt und niemals als Kartenpunkt fehlinterpretiert.

## Implementierte Assemblies und Extraktionsgrenze

- `ExtremePowers.API.dll` enthält ausschließlich `api/`: Verträge, Bootstrap, Assemblymetadaten, Hash-/Signaturwächter, native Detours, Targeting und Netzwerktransport.
- `ExtremePowers.dll` enthält allein den BepInEx-Einstieg, Preset-Modsettings, UI-Adapter und die Gold-zu-Spawn-Demo.
- Die Abhängigkeit verläuft nur vom Testmod zur API. Das API-Projekt enthält keine Quelle aus `src/`, `Locales/`, `Override/`, `Patches/` oder `Shared` und kennt weder Settings noch Lokalisierung oder XAML.
- `LocalExtremePowersApiClient` ist die einzige Brücke. Bei späterer Übernahme in den Script Extender kann dieser Adapter ersetzt werden, ohne Settings, Demo oder UI umzubauen.
- Beide Assemblies und das Paket verwenden während der Testphase Version `0.1.0`.

## Externe Quellen

- Die lokalen Script-Extender-Strukturen bestätigen das Manafeld und die oben genannten Zugriffsmethoden.
- Das lokale SHC-HD-Reverse-Engineering und Sourcehold/UCP liefern nützliche Lockstep- und Strukturkontexte, aber bislang keine belastbar benannten SHCDE-Extreme-Power-Effektfunktionen.
- Aus diesen Quellen wurden daher keine unbestätigten Offsets oder Vanilla-Parameter übernommen.

## Reproduzierbare Befehle

Rizin ausschließlich über den Workspace-Wrapper starten:

    & '.native-analysis\Run-Rizin-With-Ghidra.cmd' -c "iE~GameAction" -c "q" '<CrusaderDE.dll>'
    & '.native-analysis\Run-Rizin-With-Ghidra.cmd' -c "pxw 4 @ 0x180083308" -c "pd 100 @ 0x180082540" -c "q" '<CrusaderDE.dll>'
    & '.native-analysis\Run-Rizin-With-Ghidra.cmd' -c "pd 240 @ 0x180105510" -c "pdf @ 0x1800CD630" -c "pxw 32 @ 0x1800CD830" -c "q" '<CrusaderDE.dll>'
    & '.native-analysis\Run-Rizin-With-Ghidra.cmd' -q -c 's 0x1800ce240' -c 'pd 20' -c 's 0x1800cdb20' -c 'pd 30' '<CrusaderDE.dll>'
    & '.native-analysis\Run-Rizin-With-Ghidra.cmd' -q -c 's 0x1800cdb20' -c 'p8 32' -c 's 0x1800cdd87' -c 'p8 69' -c 's 0x1800ce25e' -c 'p8 8' '<CrusaderDE.dll>'

Hash und Metadaten:

    Get-FileHash -Algorithm SHA256 -LiteralPath '<CrusaderDE.dll>'
    Get-Item -LiteralPath '<CrusaderDE.dll>' | Select-Object Length,LastWriteTimeUtc

## Offene Fragen

1. Besitzt die Salvenroutine intern eine anderweitig steuerbare Projektilzahl oder Streuung, obwohl diese nicht als Aufrufparameter vorliegen?
2. Ist die dynamische Script-Extender-Paket-ID bei identischer Modinstallation im echten Host-/Client-Lauf gleich, und zeigt der neue Token eine absichtlich abweichende Registrierungsreihenfolge zuverlässig an?
3. Kann der Chore-Transport künftig eine verifizierbare Absender-Spieler-Zuordnung bereitstellen?
4. Wo liegt ein für alle Unit-Ziele eindeutiger und sicher detourbarer Click-/Unit-Pick-Einstieg?

## Verworfene Hypothesen

- Rohe Treffer der Bytefolge `D4 39` waren keine belastbaren Zugriffe auf `r_ExtremePowersMana`; ein Feldzugriff muss über Ressourcenbasis, Spielerstride und Funktionskontext bestätigt werden.
- Leser des globalen Auswahlwerts bei `0x18366A0C4` sind nicht automatisch Effekt-Dispatcher; mehrere Treffer kopieren Diagnose-, Save- oder Darstellungszustand.
- Der passende DLL-Hash allein genügt nicht zum Aktivieren nativer Änderungen. Jede Hookstelle benötigt zusätzlich eindeutige Signatur, erwartete Instruktionen und bestätigte Funktionsgrenzen.
