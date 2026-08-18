# AI-Vanillapreise bei global veränderten Marktpreisen

## Zweck und aktueller Stand

Diese Datei dokumentiert die Analyse und die in ExtraFeatures `1.0.17`
umgesetzte KI-Vanillapreis-Funktion:

- Die allgemeinen Kauf- und Verkaufspreisfaktoren verändern weiterhin die
  globalen Marktpreise.
- Die Faktoren pro handelbarem Gut werden zusätzlich mit dem jeweiligen
  allgemeinen Faktor kombiniert.
- Der Marktpreisbereich befindet sich bereits ganz unten in den Modsettings.
- Die Checkbox „Auch für KI“ und die atomare KI-Preishelper-Sonderbehandlung
  sind eingebaut.
- Keine globale Preisumschaltung während eines Handels verwenden.
- Keine Geld- oder Ressourcenbestände durch Vorher-/Nachher-Vergleiche
  auswerten.

## Gewünschtes Endverhalten

Im Marktpreisbereich soll neben den allgemeinen Marktpreis-Slidern eine
synchronisierte Host-Checkbox mit der Beschriftung **„Auch für KI“** ergänzt
werden.

- Standardwert: deaktiviert.
- Aktiviert: Die eingestellten globalen und gutspezifischen Kauf- und
  Verkaufspreise gelten für Menschen und KI.
- Deaktiviert: Menschen verwenden die eingestellten Preise, KI-Spieler sollen
  die Vanilla-Kauf- und -Verkaufspreise verwenden.
- Die global gesetzten Marktpreise bleiben auch bei deaktivierter Checkbox
  bestehen; es darf keine kurzzeitige globale Umschaltung pro Handel geben.
- Der Tooltip der Checkbox muss ausdrücklich erklären, dass die eingestellten
  Handelspreise unabhängig von dieser Checkbox den Wert der Modsetting
  **„Wirtschaftsboni“** beeinflussen.
- Die Checkbox ist eine Host-Spielregel und daher als `[SyncHostOnly]` zu
  klassifizieren.
- Neue Locale-Keys müssen in allen von `Shared/SerpLocalization.cs`
  unterstützten Sprachen vorhanden sein. Wenn keine Übersetzung bekannt ist,
  den englischen Text verwenden.
- Die Checkbox braucht einen nichtleeren Tooltip und
  `ToolTipService.ShowDuration="60000"`.

## Maßgebliche Dateien des bestehenden Mods

- `ExtraFeatures/src/ExtraFeaturesViewModel.cs`
  - Allgemeine Faktoren: `MarketBuyPriceMultiplier` und
    `MarketSellPriceMultiplier`.
  - Gutspezifische Arrays: `MarketGoodBuyPriceMultipliers` und
    `MarketGoodSellPriceMultipliers`.
  - Standardwerte der bisherigen Faktoren sind `1.0`.
- `ExtraFeatures/src/ExtraFeaturesRuntime.MarketPriceMultipliers.cs`
  - Liest für jedes Good `GetDefaultTradeBasePrice(...)`.
  - Kombiniert allgemeinen und gutspezifischen Faktor.
  - Schreibt das Ergebnis über `SetTradeBasePrice(...)` in die globalen
    aktiven Basispreise.
- `ExtraFeatures/src/ExtraFeaturesRuntime.CtrlMarketTrade.cs`
  - Verwendet das vorhandene manuelle Markt-Event für 1/5/25-Güter-Handel.
  - Dieser Pfad ist **kein** KI-Handelspfad.
- `ExtraFeatures/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml`
  - Marktpreisbereich ab etwa Zeile 126; bereits letzter Themenbereich.
- `Shared/SerpLocalization.cs`
  - Gemeinsame Lokalisierung und neue Textschlüssel.
- `ExtraFeatures/src/AIEconomyProtectionHook.cs`
  - Vorhandene Vorlage für hashgebundene RVAs, eindeutige Pattern-Fallbacks,
    transaktionale native Hooks und fail-closed Verhalten.
- `ExtraFeatures/UpdateToNewDLL.md`
  - Muss bei neuen nativen Adressen und Signaturen aktualisiert werden.
- `.inspect/HostClientPresetTests`
  - Vorgeschriebene Preset-/Synchronisationsprüfungen.

## Analysierte Vanilla-Basis

Kanonische DLL:

`E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll`

- Steam-Build-ID laut bestehender ExtraFeatures-Dokumentation: `24651686`
- Dateigröße: `3450880` Bytes
- SHA-256:
  `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

Die Analyse wurde mit dem vorgeschriebenen Wrapper
`.native-analysis/Run-Rizin-With-Ghidra.cmd` durchgeführt.

## Sicher nachgewiesene Vanilla-Handelspfade

### 1. Vom bestehenden Market-Event gehookter manueller Handel

Der Script Extender hookt `c_game_player_market_buy` bei Referenz-RVA
`0xD5A00` über das Pattern:

`48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 56 48 83 EC ?? 45 85 C9`

Die native Signatur ist:

`void c_game_player_market_buy(int playerId, int bSell, eGoods good, int bShiftModifier)`

Das daraus erzeugte `OnPlayerMarketInteraction`-Event enthält lediglich:

- `PlayerId`
- `Selling`
- `Good`
- `ShiftModifier`
- `Phase`

Es enthält **keine gehandelte Menge und kein Erfolgsergebnis**. Das Post-Event
wird nach dem `void`-Trampoline auch dann ausgelöst, wenn Vanilla den Handel
wegen zu wenig Gold, zu wenig Ware oder fehlendem Lagerplatz nicht ausgeführt
hat.

Die Funktion setzt intern abhängig vom Shift-Wert einen Multiplikator auf 1
oder 5 und handelt damit fest 5 oder 25 Güter. ExtraFeatures reserviert den
Wert `2` für den eigenen Ein-Gut-Handel und überspringt dafür Vanilla.

Relevante Script-Extender-Quellen:

- `shcde-script-extender/src/SHCDESE.BepInEx/Detours/BulkPlayerDetours.cs`
- `shcde-script-extender/src/SHCDESE.BepInEx/EventAPI/Player/PlayerMarketInteractionEventArgs.cs`

### 2. KI-Kauf

Die KI berechnet selbst eine beliebige benötigte Menge. Der nachgewiesene
Aufrufpfad ist:

- KI-Kauflogik und Preis-/Goldprüfung: Call-Site RVA `0x3ED9E`
- KI-Kauftransaktion: RVA `0x29650`
- Gesamtpreishelfer für Kauf: RVA `0xCEB10`

Die KI-Kauftransaktion erhält `playerId`, `good` und eine beliebige `amount`.
Sie berechnet den Gesamtpreis, versucht exakt diese Ressourcenmenge
hinzuzufügen und zieht nur bei erfolgreichem Hinzufügen den Gesamtpreis direkt
von den relevanten Goldfeldern ab. Sie gibt Erfolg/Misserfolg zurück.

Der Kaufpreis wird bereits vor Aufruf der Transaktion geprüft. Eine reine
nachträgliche Goldkorrektur kann daher keinen Kauf retten, den die KI wegen
eines erhöhten Modpreises gar nicht erst versucht.

### 3. KI-Verkauf

Die KI berechnet selbst eine beliebige Überschussmenge. Der nachgewiesene
Aufrufpfad ist:

- KI-Verkaufslogik: Call-Site RVA `0x3F22F`
- KI-Verkaufstransaktion: RVA `0x29700`
- Gesamtpreishelfer für Verkauf: RVA `0xCEB90`

Die Verkaufstransaktion erhält ebenfalls `playerId`, `good` und eine beliebige
`amount`. Sie schreibt den berechneten Erlös in Gold- und Statistikfelder und
entfernt anschließend genau die übergebene Gütermenge.

### 4. Direkter Call-Abgleich

Ein auf die `.text`-Section begrenzter PE-Scanner ergab für direkte `CALL
rel32`-Aufrufer:

```text
Ziel RVA 0x29650 (KI-Kauf):     Call-Site RVA 0x3ED9E
Ziel RVA 0x29700 (KI-Verkauf):  Call-Site RVA 0x3F22F
Ziel RVA 0xD5A00 (Market-Event): keine direkten nativen CALL-Aufrufer
```

Damit ist insbesondere nachgewiesen, dass die normalen KI-Kauf- und
KI-Verkaufsroutinen die vom bestehenden `OnPlayerMarketInteraction`-Event
gehookte Funktion nicht aufrufen.

## Exakte Preisarithmetik

Die KI-Gesamtpreishelfer verwenden nicht
`Basispreis * Menge / 5`, sondern:

```text
Gesamtpreis = (Basispreis / 5 mit nativer Ganzzahldivision) * Menge
```

Das ist für Rundung und spätere Vergleichstests wichtig.

- Kaufhelfer RVA `0xCEB10` liest den aktiven Kauf-Basispreis bei Offset
  `0x1817B8`, teilt zuerst durch 5 und multipliziert dann mit `amount`.
- Verkaufhelfer RVA `0xCEB90` liest den aktiven Verkauf-Basispreis bei Offset
  `0x1817BC`, teilt zuerst durch 5 und multipliziert dann mit `amount`.
- Beide erhalten gemäß beobachteter Aufrufe einen Player-Identifier, ein Good
  und die beliebige Menge. Die genaue Delegate-Signatur und ABI müssen vor
  Installation eines Hooks nochmals vollständig gegen alle Aufrufer validiert
  werden.

## Warum vorhandene Events nicht ausreichen

`OnPlayerAddResource` und `OnPlayerSubtractResource` liefern zwar Player, Good
und Menge. Die KI-Transaktionen erreichen diese allgemeinen Funktionen:

- KI-Kauf verwendet die allgemeine Add-Resource-Funktion bei RVA `0xB58B0`.
- KI-Verkauf verwendet die allgemeine Subtract-Resource-Funktion bei RVA
  `0xB7D80`.

Diese Events werden aber ebenso von Produktion, Verbrauch, Baukosten,
Rückerstattungen und vielen anderen Spielvorgängen ausgelöst. Sie enthalten
keinen Grund „Markthandel“. Eine Behandlung aller KI-Ressourcenänderungen als
Handel wäre falsch. Heuristiken anhand von Goldständen, zeitlicher Nähe oder
Vorher-/Nachher-Differenzen sind ausdrücklich nicht zu verwenden.

## Verworfene beziehungsweise ungeeignete Ansätze

1. **Globale Preise während eines Handels kurz umschalten**
   - Unsicher bei gleichzeitig ablaufenden Trades, verschachtelten Aufrufen und
     Multiplayer.
   - Wurde vom Benutzer ausdrücklich abgelehnt.
2. **KI im bestehenden Market-Post-Event korrigieren**
   - Das Event sieht den Vanilla-KI-Handel nicht.
   - Keine beliebige Menge und kein Erfolgsergebnis vorhanden.
   - Post wird auch nach fehlgeschlagenen manuellen Trades ausgelöst.
3. **Gold oder Güter vorher und nachher vergleichen**
   - Nicht transaktionssicher, besonders bei mehreren KI-Aktionen.
   - Wurde vom Benutzer ausdrücklich abgelehnt.
4. **Allgemeine Add-/Subtract-Resource-Events als Marktindikator verwenden**
   - Die Ursache der Ressourcenänderung ist nicht unterscheidbar.
5. **Nur nach einer KI-Kauftransaktion Gold korrigieren**
   - Die KI kann den Kauf aufgrund des modifizierten Preises bereits vor der
     Transaktion ablehnen.
   - Verkaufsstatistiken und weitere vom Transaktionspfad geschriebene Werte
     könnten weiterhin den modifizierten statt den Vanilla-Erlös enthalten.

## Empfohlener Implementierungsweg

Der fachlich sauberste derzeit bekannte Weg sind zwei eng begrenzte native
Detours auf die KI-relevanten Gesamtpreishelfer bei `0xCEB10` und `0xCEB90`.
Dies ist in 1.0.17 mit der vollständigen Hook-Sicherheitsprüfung aus
`AGENTS.md` implementiert.

Gewünschte Callback-Logik:

1. Wenn ExtraFeatures deaktiviert ist: immer Vanilla-Trampoline verwenden.
2. Wenn „Auch für KI“ aktiviert ist: immer Vanilla-Trampoline verwenden. Das
   Trampoline liest dann die bereits global modifizierten aktiven Preise.
3. Wenn der betreffende Player kein KI-Spieler ist: immer Trampoline verwenden.
4. Nur wenn der Mod aktiv, „Auch für KI“ deaktiviert und
   `GamePlayerManagerAPI.Instance.IsAIPlayer(playerId)` wahr ist:
   - Vanilla-Basispreis über `GetDefaultTradeBasePrice(good)` lesen.
   - Kauf beziehungsweise Verkauf auswählen.
   - Exakt `(vanillaBasePrice / 5) * amount` mit der validierten nativen
     Integersemantik zurückgeben.

Vorteile dieses Weges:

- Keine globale Preisumschaltung.
- Keine Bestandsdifferenzen.
- Beliebige KI-Mengen werden direkt berücksichtigt.
- Kaufentscheidung und tatsächliche Zahlung sehen denselben Vanilla-Preis.
- KI-Verkaufserlös und die von Vanilla daraus fortgeschriebenen Statistiken
  bleiben konsistent.
- Menschliche Spieler und andere Preisabfragen bleiben durch die explizite
  KI-Prüfung auf den global modifizierten Preisen.

Vor der Umsetzung noch zwingend prüfen:

- Exakte Delegate-Signaturen und Calling Convention beider Helfer.
- Alle direkten und indirekten Aufrufer, insbesondere ob ein Aufruf einen
  ungültigen oder nicht spielerbezogenen `playerId` übergeben kann.
- Tatsächliche Detour-Überschreibspanne ab beiden Funktionsanfängen,
  Instruktionsgrenzen, RIP-relative Operanden, eingehende Branchziele und die
  erste Instruktion nach der Überschreibspanne.
- Eindeutige semantische AOB-Signaturen für abweichende DLL-Hashes.
- Hashpfad: beim bekannten SHA-256 direkte Referenz-RVA verwenden und Bytes
  validieren; nur bei anderem Hash begrenzt und eindeutig scannen.
- Fail-closed und transaktionale Installation beider Hooks: bei Abweichung
  darf keiner der beiden Hooks aktiv bleiben.
- Fehler im optionalen Callback dürfen Vanilla nicht verhindern; identische
  Fehler drosseln und mit Millisekunden-Zeitstempel loggen.
- Dauerhafte Hooks nicht in `OnDisable()` oder `OnDestroy()` wegen des
  BepInEx-Lifecycles entfernen.

Wenn diese Prüfung zeigt, dass ein Preishelfer-Detour nicht sicher oder nicht
ausreichend player-spezifisch ist, anhalten und den Benutzer informieren. Nicht
stillschweigend auf eine globale Umschaltung oder Bestandsheuristik ausweichen.

## UI-, Preset- und Lokalisierungsimplementierung

1. In `ExtraFeaturesViewModel.cs` eine boolesche Property, beispielsweise
   `MarketPricesAlsoForAI`, mit `[SyncHostOnly]` anlegen.
2. Standardwert und Resetwert auf `false` setzen.
3. Beschriftungs- und Tooltip-Bindings ergänzen.
4. Checkbox im allgemeinen Marktpreisblock direkt bei den allgemeinen
   Slidern platzieren; der gesamte Marktpreisbereich bleibt der letzte Bereich
   der Modsettings.
5. Tooltipinhalt sinngemäß in Deutsch:

   „Legt fest, ob die eingestellten Kauf- und Verkaufspreise auch für
   KI-Spieler gelten. Wenn deaktiviert, handelt die KI zu Vanilla-Preisen. Die
   eingestellten Handelspreise beeinflussen unabhängig von dieser Einstellung
   den Wert der Modsetting ‚Wirtschaftsboni‘.“

6. Alle Sprachen in `Shared/SerpLocalization.cs` ergänzen und Locale-Key-Parität
   prüfen.
7. Presetsynchronisation, Trail-Snapshot und Host-/Client-Sperren über das
   vorhandene gemeinsame Presetsystem laufen lassen; kein eigenes
   Persistenzsystem anlegen.

## Vorgesehene Codeorganisation

- Einen eigenen Hooktyp im Stil von `AIEconomyProtectionHook.cs` anlegen,
  beispielsweise `AIMarketVanillaPriceHook.cs`.
- Installation über den bestehenden Library-Loaded-Pfad in
  `ExtraFeaturesPlugin.cs` beziehungsweise `ExtraFeaturesRuntime.cs`.
- Neue Datei in `ExtraFeatures.csproj` aufnehmen.
- Keine alte oder alternative Implementierung parallel als Fallback behalten.
- `ExtraFeatures/UpdateToNewDLL.md` um Hash, beide RVAs, Patterns,
  Aufrufersemantik und Updateaudit ergänzen.
- Die Versionsnummer und den Changelog erst bei tatsächlich fertiggestellter
  und geprüfter Umsetzung aktualisieren.

## Prüf- und Abnahmeplan

Vor dem einzigen abschließenden Build:

1. Reine Arithmetiktests für beide Richtungen und Mengen wie `1`, `5`, `25`
   sowie nicht durch 5 teilbare Basispreise ergänzen.
2. Prüfen:
   - Mod deaktiviert: vollständig Vanilla.
   - Checkbox aktiviert: Menschen und KI verwenden globale Modpreise.
   - Checkbox deaktiviert: Menschen verwenden Modpreise, KI Vanilla-Preise.
   - Allgemeiner und gutspezifischer Faktor werden weiterhin gemeinsam auf die
     globalen Preise angewendet.
   - Faktoren `0`, `1` und `5` sowie verschiedene beliebige KI-Mengen.
   - Erfolgreicher und wegen Gold/Lagerplatz abgelehnter KI-Kauf.
   - KI-Verkauf und zugehörige Statistik-/Goldwerte.
3. Multiplayer/Skirmish/Trail-Kontexte über `Shared/GameModeHelper.cs` und das
   gemeinsame Presetsystem prüfen; keine eigene Multiplayererkennung bauen.
4. `.inspect/HostClientPresetTests` ausführen und gegebenenfalls um die neue
   `[SyncHostOnly]`-Property erweitern.
5. XAML-Audit auf Tooltip, `ShowDuration="60000"`, Locale-Key-Parität,
   Bindings und Scrollbarkeit ausführen.
6. Alle geänderten Textdateien explizit auf CRLF und nackte LF prüfen.
7. Erst danach einmal `ExtraFeatures/build.bat /nopause` direkt aus PowerShell
   mit erhöhten Rechten ausführen; weder `cmd /c` noch `Start-Process`
   dazwischenschalten.
8. Im Spiel mindestens einen KI-Kauf und einen KI-Verkauf mit auffälligen
   Faktoren testen und anhand begrenzter Diagnosemarker bestätigen, dass die
   Hooks tatsächlich ausgelöst wurden und die übergebenen Mengen plausibel
   sind.

## Implementierungsentscheidung

Die zwei empfohlenen Preishelper-Detours sind beauftragt und in 1.0.17
implementiert. Bei einem unbekannten DLL-Hash werden sie nur nach je einem
eindeutigen ausführbaren Signaturtreffer und vollständiger Spannvalidierung
gemeinsam installiert; andernfalls bleibt ausschließlich diese KI-Sonderregel
deaktiviert.
