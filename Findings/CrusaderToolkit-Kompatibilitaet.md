# Crusader Toolkit: Kompatibilität mit Serps Mods und APIShared

Stand: 13.11.2026. Entwicklerbericht für die gemeinsame Abstimmung mit dem Toolkit-Autor. Die vorgeschlagenen Änderungen wurden nicht implementiert.

## Ergebnis und Prüfgrenzen

Es gibt bestätigte, konfigurationsabhängige Konflikte: AutoTax berücksichtigt unsere veränderten Popularitätsbeiträge nicht; StockDelta und GranaryOverview vermischen beim Spectator-Spielerwechsel Historien; SmartDodge interpretiert interne Pathfinding-Bewegungen als Spielerabsicht. BuildingProduction kollidiert außerdem mit dem optionalen Waffenproduktions-HUD von APIShared. Diese Ergebnisse sind statisch bestätigt; das sichtbare Zusammenspiel wurde nicht im Spiel reproduziert.

Zusätzlich bestehen offene Multiplayer-Risiken bei SmartDodge und TargetPriority sowie eine noch nicht abschließend auditierte Baupfad-Abweichung bei ResourceLayout. Fehlende Preset-/Suchintegration ist eine Integrationslücke, kein Nachweis eines Absturzes oder einer beschädigten Speicherung.

Alle 13 Toolkit-Mods wurden hinsichtlich ihrer relevanten Überschneidungen inventarisiert. Das ist keine pauschale Freigabe sämtlicher Kombinationen, Einstellungen oder Spielversionen. Insbesondere wurden weder reale Multiplayer-Partien noch Installation, Builds oder automatisierte Spieleingaben ausgeführt. Unabhängige Toolkit-Bugs ohne konkreten Bezug zum Workspace sind nicht Gegenstand dieses Berichts.

### Geprüfte Quellen

| Quelle | Identität der untersuchten Momentaufnahme |
| --- | --- |
| Toolkit | `af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf`, Arbeitsbaum zu Prüfungsbeginn sauber |
| Workspace | `02f329b0fc6824c512fc12f82ba52d203e1a6847`, Arbeitsbaum zu Prüfungsbeginn sauber |
| APIShared | `e8be2a112923320536f40719192352835784983d`, Arbeitsbaum zu Prüfungsbeginn sauber |
| Native Spiel-DLL | `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; installierte kanonische DLL, gegen `CURRENT.json` und verwendete Datensätze geprüft |

Toolkit-Pfade beziehen sich auf `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Fremde Mods\crusader-toolkit`. Andere Quellpfade sind relativ zum Workspace. Die Quellenzeilen sind Beobachtungen dieses Stands, keine dauerhaft garantierten Adressen.

Native Grundlage: [aktuelle Baseline](../_inspect/CrusaderDE-Native-Baseline/CURRENT.md), insbesondere `knowledge/POPULARITY_PARAMETERS.md` und [Spectator-Berichtspfad](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SPECTATOR_REPORTS.md). Generische native Funktionsnamen bleiben teilweise `candidate`; die hier verwendeten Datenflüsse sind statisch nachverfolgt. Erkenntnisse des anderen Chats wurden zur Abgrenzung herangezogen; dessen [Lernanalyse](CrusaderToolkit-Lernanalyse.md) behandelt Übernahmeideen statt dieses Kompatibilitätsaudits.

## Bestätigte Konflikte

### K1 — AutoTax trifft bei geänderten Popularitätsbeiträgen falsche Entscheidungen (P2)

**Betroffen:** AutoTax zusammen mit ExtraFeatures-Popularitätsoptionen oder entsprechenden fremden APIShared-Policies. Ohne abweichende Beiträge ist dieser konkrete Konflikt nicht belegt.

**Fehlerfall:** In einer freigegebenen Custom-Einzelspielerpartie mit Keep, Bevölkerung 20, voller Ration einer Nahrungsart, sonst neutralen Beiträgen und Popularität 100 wird der menschliche Beitrag für „Low taxes“ in ExtraFeatures auf `+16` Berichtspunkte gesetzt. AutoTax mit Ziel 99–99,5 wählt Steuerstufe 4 und prognostiziert `−50` interne Einheiten. Die konfigurierte Stufe bewirkt tatsächlich `+400`, begrenzt auf 100 Popularität. AutoTax kann so auf einer ungeeigneten Steuerstufe bleiben, statt den Überschuss mit einer tatsächlich negativen Stufe abzubauen.

**Belegkette:**

- Toolkit `AutoTax/src/TaxPolicy.cs:10,21,50,63,98`: feste `TaxEffects` und feste Nahrungsformel; alle Steuerkandidaten werden mit diesen Werten bewertet.
- Toolkit `AutoTax/src/AutoTaxPlugin.cs:262–266`: rohe `PlayState`-Beiträge und eine selbst berechnete Nahrungskorrektur bilden die Eingabe.
- `ExtraFeatures/src/ExtraFeaturesViewModel.Popularity.cs:763–768` erlaubt den Beispielwert; `ExtraFeatures/src/PopularityRuntime.cs:75–87` veröffentlicht die wirksamen Sessionbeiträge.
- `APIShared/src/Economy/PopularityOverrides.cs:173,220–222` und `APIShared/src/Economy/PopularityPlayerIl.cs:44–57`: Umrechnung und spielerabhängige Anwendung der effektiven Policy.
- `APIShared/src/ScriptExtenderFixes/Popularity/PopularityPresentation.cs:41–67`: Die vorhandene Darstellungskorrektur arbeitet mit einem getrennten Bericht und ViewModel-Werten. Sie ändert den von AutoTax gelesenen Rohsnapshot nicht.

**Native Prüfung:** Der Takt `0xD1AD0` führt die Komponentenberechnung `0xCB090` aus: Spieler-/Keep-Prüfung, Sondermodi und kleine Bevölkerung als Bypässe, Ration/Vielfalt und Steuer sowie weitere Beiträge, Ereignisabzüge und abschließende Begrenzung auf `0..10000`. Der getrennte Bericht `0x19D960` erzeugt Vanilla-Beiträge und gegebenenfalls UI-Vorschauen. APIShared setzt die Rollenpolicy vor der Komponentenrechnung ein. Berichtspunkte entsprechen hier 25 internen Einheiten, angezeigte Gesamtpopularität dagegen 100 internen Einheiten pro Punkt.

**Widerlegung geprüft:** AutoTax sperrt MP, Editor und Spectator; diese Guards greifen im beschriebenen Custom-Einzelspielerfall nicht. Die Bevölkerungsgrenze greift bei 20 ebenfalls nicht. ExtraFeatures ist im gewählten Kontext freigegeben. Die standardmäßig abgeschaltete Steuergruppe verhindert lediglich den Fehler ohne diese unterstützte Konfiguration.

**Ausgeführte Verifikation:** Die tatsächliche `TaxPolicy.cs` wurde unverändert mit PowerShell `Add-Type` kompiliert. `Evaluate(800,10000,25,25,3,99,99.5)` und anschließend `Evaluate(801,10000,-50,-50,4,99,99.5)` ergaben jeweils Stufe `4`, Prognose `−50`. Das prüft die reale Reglerentscheidung, nicht die Ausführung im Spiel.

**Fixvorschlag:** APIShared um eine öffentliche, rein lesende Berechnung effektiver Popularitätsbeiträge und Steuer-/Rationskandidaten aus einem konsistenten Zustand erweitern. Sie muss bestehende Human-/AI-Policies, globale Werte, Modifier-Komposition, Rundung und Sonderfälle teilen. AutoTax behält Strategie und Befehle; es ersetzt nur Rohprognose und Vanilla-Konstanten. Ein passender öffentlicher effektiver Rechner fehlt derzeit. Reflection auf interne Rechner oder bloßes Lesen globaler Extender-Werte wäre kein ausreichender Fix, weil die Rollenpolicy verloren geht. Der API-Entwurf braucht Implementierung und Integrationsprüfung.

**Bis dahin:** AutoTax bei veränderten Popularitätsbeiträgen abschalten. Andere Beitragsquellen können dieselbe Grundursache auslösen; sie werden nicht als zusätzliche unabhängige Befunde gezählt.

### K2 — StockDelta vermischt Bestände verschiedener Spectator-Spieler (P2)

**Fehlerfall:** A hat zum Monatsanfang 100 Holz. Nach dem Wechsel auf B mit 1.000 Holz im selben Monat zeigt StockDelta `+900`, obwohl keine Bestandsänderung stattgefunden hat. Beim nächsten Monatswechsel kann diese Differenz auch als letzter Monatswert stehen bleiben.

**Belegkette:** Toolkit `StockDelta/src/StockDeltaPlugin.cs:153–180` führt nur einen Satz `current`, `monthStart` und `lastMonth`; `:216` berechnet die Anzeige als aktuellen Bestand minus diesen Monatsanfang. Resets werden bei Kartenstart/Save-Laden angefordert (`:74–83`), nicht beim Spielerwechsel. `Spectator/src/SpectatorPerspectiveRuntime.cs:800–838` ändert die Ansicht innerhalb derselben Mission.

**Native Prüfung:** `DLL_SetEditorPlayer` (`0x872E0`, confirmed) setzt für Spieler 1–8 den nativen Ansichtsindex. `DLL_RunTick` (`0x86680`, confirmed) stellt diesen nach der gesonderten Spectator-Eingabeverarbeitung wieder her; `0x19D960` kopiert danach die Ressourcen des betrachteten Spielers in den Bericht. Der Managed-Pfad übernimmt diese Ressourcen in `PlayState`. Eine Ansichtsänderung ist deshalb keine Ressourcenänderung desselben Spielers.

**Widerlegung geprüft:** StockDelta besitzt weder einen Spieler-/Ansichtsschlüssel für die Historie noch eine Subscription auf APIShared-Perspektivwechsel. Ein unverändertes Monatsdatum verhindert gerade den Reset. Das Ausblenden einzelner Labels trennt die Statistik nicht.

**Fixvorschlag:** Historie nach Mission und betrachteter Spieler-ID trennen oder bei Wechsel verwerfen; unvollständige Monate ausdrücklich als unbekannt behandeln. `PlayerPerspectiveAPI.SubscribeChanges` existiert bereits. Das Ereignis allein genügt noch nicht: Der erste Snapshot nach dem Ereignis kann zur alten Ansicht gehören. Erst bei einem nachweislich frischen Bericht der neuen Ansicht darf eine neue Basis entstehen.

**Sinnvolle APIShared-Ergänzung:** Ein öffentliches, unveränderliches Berichtskontext-Objekt mit Mission, tatsächlicher Ansichts-ID und Berichtsgeneration/Freshness. Es würde auch GranaryOverview und weitere Statistikmods absichern. Keine zweite Perspektivverwaltung; die vorhandene Identitätsverwaltung soll diesen Kontext liefern. Bis zur Umsetzung ist Abschalten der Deltaanzeige beim Beobachten wechselnder Spieler ein manueller Workaround.

### K3 — GranaryOverview vermischt Monats- und Produktionshistorie beim Spectator-Wechsel (P2)

**Fehlerfall:** Nach dem Wechsel A → B innerhalb eines Monats werden Bs Nahrungsbestände mit As Monatsanfangsbeständen verrechnet. Bereits erfasste Produktionsraten von A bleiben zusätzlich im Ledger, während neue Ereignisse für B aufgenommen werden. Die daraus abgeleitete Versorgungseinschätzung gehört dann zu keinem einzelnen Spieler.

**Belegkette:** Toolkit `GranaryOverview/src/GranaryOverviewPlugin.cs:208–245` verwaltet einen gemeinsamen Monatszustand; `:274` zieht dessen Basis vom aktuell betrachteten Bestand ab. Die Karten-/Save-Resets (`:76–77`) decken den Ansichtswechsel nicht ab. `GranaryOverview/src/FoodLedger.cs:168–200` summiert Records nach Gebäudetyp ohne Spielerpartition; `:232–240` nimmt Ereignisse anhand des gerade von APIShared korrigierten `GetLocalPlayerId()` an. Die Ressourcenquelle folgt dem in K2 geprüften Berichtspfad.

**Widerlegung geprüft:** Die Zulassungsprüfung neuer Ereignisse entfernt keine schon gespeicherten Records des vorher betrachteten Spielers. Gleicher Monat löst auch keinen Ledger-Reset aus. Ein späteres Auslaufen des Messfensters repariert die Zwischenzeit nicht.

**Fixvorschlag:** Den Frische-/Ansichtskontext aus K2 nutzen und sowohl Monatsbasis als auch Produktionsledger pro Mission/Ansicht führen oder beim bestätigten Wechsel zurücksetzen. Nach einem Reset muss das Panel fehlende Messhistorie anzeigen. Nur die sichtbare Bestandszeile zu reparieren reicht nicht. Ein gemeinsamer APIShared-Berichtskontext ist sinnvoll; das fachliche Nahrungsmodell bleibt im Toolkit.

### K4 — SmartDodge übernimmt Pathfindings internen Stopp als Spielerziel (P2)

**Fehlerfall:** Ein für SmartDodge aktivierter berittener Bogenschütze bewegt sich zu einem Ziel. Pathfinding hat `AnchorSingleMoves` sowie eine aktive Gefahren-/Neuplanungsregel und hält ihn vorübergehend an einer Wartekachel an. SmartDodge ersetzt darauf sein gemerktes Rückkehrziel und seinen Posten durch diese Kachel und löscht den eigenen Wegpunkt. Weitere Ausweichentscheidungen arbeiten damit nicht mehr gegen die ursprüngliche Spielerabsicht.

**Belegkette:**

- Toolkit `SmartDodge/src/SmartDodgePlugin.cs:103–106`: Jeder fremde `OnUnitMoveHere`-Pre-Aufruf gilt als Spielerauftrag; ausgeschlossen sind nur eigene Dodge-Aufträge.
- Toolkit `SmartDodge/src/DodgeController.cs:236–243`: `Resume`/`Post` werden überschrieben, `Waypoint` gelöscht. Spätere Rückkehrpfade verwenden diese Werte (`:306–352`).
- `Pathfinding/src/Engine/RouteReplanWatch.cs:285–298,532–535`: Der absichtliche Wartestopp ruft `MoveToTile` auf. Die vorgelagerten Freigaben stehen bei `:158–198`; berittene Bogenschützen sind durch die Soldatenauswahl nicht grundsätzlich ausgeschlossen (`Pathfinding/src/Engine/FireEscape.cs:96–103`).
- `shcde-script-extender/src/SHCDESE.BepInEx/API/GameUnitManagerAPI.cs:1181–1183` führt direkt in den Wrapper; `shcde-script-extender/src/SHCDESE.BepInEx/Detours/BulkUnitDetours.cs:1471–1473` veröffentlicht das Pre-Ereignis vor dem nativen Aufruf.

**Widerlegung geprüft:** Die interne Eigenauftragsmarkierung von SmartDodge erfasst Pathfinding nicht. Umgekehrt ist der Wartestopp nicht mit öffentlich auswertbarer Herkunft versehen. Pathfinding kann später das ursprüngliche Ziel erneut ausgeben; ein bedingungsloser dauerhafter Zielverlust wird deshalb ausdrücklich nicht behauptet. Bestätigt ist die falsche Veränderung des privaten Intent-Zustands im verwalteten Aufrufpfad, nicht eine reproduzierte Ingame-Endlosschleife.

**Fixvorschlag:** Gemeinsame, additive Befehlsprovenienz in APIShared: Eigentümer-GUID, Zweck wie Nutzerabsicht/temporäres Ausweichen/temporärer Stopp/Wiederaufnahme und Bezug auf die ursprüngliche Absicht. Zusätzlich eine explizite Koordination, welche Bewegungen während einer fremden Wartephase zulässig sind. Pathfinding und SmartDodge müssen den Vertrag verwenden. Bloß alle Fremdaufträge zu ignorieren ist unzureichend, weil auch echte Befehle oder dauerhafte Zieländerungen über andere Mods kommen können.

Diese Erweiterung ist ein Entwurf. Die bestehenden `TroopCommandInputEvents` beobachten lokale Eingabe, ersetzen aber keine vollständige Herkunfts- und Besitzkoordination interner Unit-/Tribe-Aufträge. Als kurzfristiger Workaround SmartDodge für Einheiten mit aktiver Pathfinding-Automatik abschalten.

### K5 — BuildingProduction verliert Produkticons beim zusätzlichen Waffenproduktions-HUD (P2, bedingt)

**Bedingung:** Ein Verbraucher registriert ein zusätzliches, im aktuellen Workshop erlaubtes APIShared-Waffenrezept und aktiviert dadurch dessen eigenes HUD. Für die geprüfte Workspace-Momentaufnahme wurde kein regulär aktiver Verbraucher solcher Zusatzrezepte festgestellt; dies ist ein Konflikt mit einer vorhandenen APIShared-Funktion, kein allgemeiner Fehler beim bloßen Laden von APIShared.

**Fehlerfall und Beleg:** Toolkit `BuildingProduction/src/ProductionPanel.cs:132–154` erzeugt für Produktzeilen mit `goods.Buttons != null` keine eigenen Icons. Stattdessen positioniert `:262–309` die bestehenden Vanilla-Radiobuttons um. `APIShared/src/Economy/WeaponProduction/WeaponProductionHud.PanelView.cs:152–175` blendet genau deren gemeinsame `StackPanel`-Zeile aus, um die Rezeptauswahl selbst darzustellen. Somit fehlen die von BuildingProduction vorausgesetzten Icons in seinen Zeilen. `BuildingProduction/src/ProductionGoods.cs:47–49` verbindet diese Darstellung mit den Vanilla-Produkten der drei betroffenen Workshoptypen.

**Widerlegung geprüft:** `MoveWorkshopButtons` ändert Maße, Ausrichtung und Position, stellt aber die Sichtbarkeit der ausgeblendeten Elternzeile nicht her. Die APIShared-Auswahl bleibt absichtlich Eigentümer dieser Sichtbarkeit. APIShareds `HasHudDemand`- und Gebäudeguards begrenzen den Fall auf die tatsächlich aktive Rezeptoberfläche.

**Fixvorschlag:** BuildingProduction bei aktiver Fremdauswahl rein dekorative eigene Produkticons verwenden und die Auswahlbuttons unverändert lassen. Für zusätzliche Produkte die effektiven Rezept-/Produktdaten statt ausschließlich der festen Vanilla-Liste verwenden. APIShared könnte einen rein lesenden aktuellen Workshop-Präsentationskontext veröffentlichen: ausgewähltes Gebäude, wirksame Optionen, aktuelles/nächstes Produkt und Eigentümerschaft der Auswahloberfläche. Kein zweiter Satz konkurrierender Auswahlbuttons. Entwurf und Layout müssen noch integriert und visuell geprüft werden.

## Offene Risiken und weitere Integrationslücken

### R1 — SmartDodge und TargetPriority: Multiplayer-Freigabe nicht belastbar

**Bestätigte Quelleigenschaften:** Beide Manifeste deklarieren `NetworkMode=0` (`SmartDodge/info.json:11`, `TargetPriority/info.json:11`). SmartDodge aktiviert Einheiten aus lokaler Auswahl (`SmartDodge/src/SmartDodgePlugin.cs:214–237`) und gibt aus lokalen Entscheidungen unmittelbar `MoveToTile` aus (`SmartDodge/src/DodgeController.cs:590–594`). TargetPriority entscheidet standardmäßig für eigene Einheiten, nicht für Verbündete/Gegner (`TargetPriority/src/Settings.cs:52–57`); `TargetPriority/src/Settings.cs:150` und `TargetPriority/src/TargetHook.cs:788–796` koppeln das an den jeweiligen lokalen Spieler. Sein Auswahlpfad verändert Tabellen/Code während des Originalaufrufs (`:739–753`) und im Sonderpfad Schützenfelder (`:768–782`). Es wurde kein gemeinsamer Simulationsentscheid, synchronisierter Aktivierungspfad oder ausdrücklicher MP-Ausschluss in diesen Pfaden gefunden.

**Risiko:** Unterschiedliche lokale Auswahl oder Perspektive kann unterschiedliche Simulationsentscheidungen erzeugen, auch bei gleichen installierten Mods. Das gefährdet das deterministische Zusammenspiel aller Gameplaymods. Eine tatsächlich desynchronisierte Partie und die komplette native Zielwahl-/Schusskette wurden hier nicht nachgewiesen; deshalb ist dies kein als reproduziert gemeldeter Desync-Befund.

**Vorgehen:** Bis zur Klärung beide Funktionen im echten Multiplayer fail-closed sperren. Für echte MP-Unterstützung TargetPriority mit gemeinsam veröffentlichten Regeln pro tatsächlichem Spieler betreiben; SmartDodge benötigt tick-synchrone Aktivierung und deterministische Ausführung oder synchronisierte, autorisierte Befehle. APIShareds bestehende Chore-/Settings-/Simulationsdienste zuerst verwenden. Eine Ergänzung für Befehlsherkunft und Koordination aus K4 ist sinnvoll. `NetworkMode=1` allein synchronisiert weder Einstellungen noch Entscheidungen und wäre als alleiniger Fix unzureichend. Einen allgemeinen Zielwahlbroker erst nach vollständigem nativen Audit entwerfen.

### R2 — ResourceLayout umgeht Extender-Wrapper; tatsächlicher Baukonflikt noch offen

Toolkit `ResourceLayout/src/GameGrid.cs:25–37` reflektiert den privaten Placement-Validator und ruft dessen `Original` auf. `ResourceLayout/src/ResourceLayoutPlugin.cs:621–645` macht dasselbe mit dem Bauhook. Die zugehörigen Wrapper-Ereignisse werden an diesen direkten Einstiegen ausgelassen. `BuildingLimit/src/BuildingLimitRuntime.cs:97–99` hängt an `OnPlacementValidation`; die Planungsprüfung kann diese modseitige Ablehnung deshalb nicht über diesen Wrapper sehen.

Die native Baufunktion kann jedoch später weitere live gepatchte Prüfungen aufrufen. Die vollständige Validator-/Bauunterkette ist hier nicht abgeschlossen. Daher **kein bestätigter BuildingLimit-Bypass, Kosten-Exploit oder kostenloses Bauen**. BuildingCosts verändert außerdem Werte unabhängig vom ausgelassenen `OnBuildStructure`; bloßes Fehlen dieses Ereignisses beweist keinen Kostenfehler.

Sinnvolle gemeinsame Untersuchung: ein öffentlicher, seiteneffektarm definierter Platzierungscheck und ein eigener bezahlter Spieler-Bauauftrag unter APIShared, einschließlich aktiver Modregeln, Ressourcen, Rotation, Varianten, Sondergebäuden, Missions-/Spielerautorität und Synchronisierung. Nicht einfach `Original` durch einen anderen direkten Call ersetzen und Gleichwertigkeit behaupten. Vorschauprüfung und tatsächliche Ausführung müssen getrennte Verträge besitzen. Der externe Quellbaum wurde nicht geändert.

### I1 — Toolkit-Einstellungen fehlen in unserer globalen Suche und Missionskonfiguration

Toolkit-Settings sind eigene `INotifyPropertyChanged`-ViewModels mit BepInEx-Konfiguration und direkter Extender-Registrierung, beispielsweise `SmartDodge/src/SmartDodgeSettings.cs:12–47` und `SmartDodge/src/SmartDodgePlugin.cs:70`. Die Toolkit-Quellen registrieren weder einen APIShared-Preset-Endpunkt noch einen Suchkatalog.

`SerpsModsHost/src/ModSettingsSearchViewModel.cs:189–201` berücksichtigt nur registrierte Suchquellen. Toolkit-Tabs fehlen daher in globalen Treffern und Modfiltern, bleiben aber regulär bedienbar. `APIShared/src/ModSettings/ModSettingsParticipantDiscovery.cs:97–112` verlangt einen typisierten Preset-Endpunkt und passend klassifizierte Werte; Toolkit-Werte werden nicht automatisch Teil der APIShared-Map-/Trail-/Savegame-Settings. AutoTax besitzt eigene Match-Speicherung; deren Existenz wird dadurch nicht bestritten.

Für Zusammenarbeit bietet sich die bestehende öffentliche Presetregistrierung an, mit ausdrücklich gewählter Klassifizierung je Einstellung. Reine Darstellungspräferenzen bleiben lokal; simulationswirksame gemeinsame Werte benötigen echte Autoritäts-/Syncverträge. Nicht sämtliche lokalen Automationspräferenzen pauschal zu Hostwerten machen. Für reine Suchintegration reicht auch eine explizite Registrierung im vorhandenen Suchkatalog. Hierfür ist keine neue APIShared-Infrastruktur erforderlich. Eine harte APIShared-Abhängigkeit ist für die tatsächlich migrierten Funktionen sinnvoll, keine automatische Voraussetzung für jeden unabhängigen UI-Mod.

## Entkräftete Verdachtsfälle und Prüfumfang je Toolkit-Mod

| Toolkit-Mod | Geprüfte Überschneidung und Ergebnis |
| --- | --- |
| AutoTax | Popularitätsmodifikatoren, Rohbericht, Guards, Speicherung: K1; eigene Matchdatei kollidiert nicht allein wegen zusätzlicher APIShared-Savedaten. |
| BuildGrid | Gemeinsame Eingabe-/Renderoberfläche inventarisiert; kein konkreter gegenseitiger Hookverlust bestätigt. |
| BuildingProduction | Eigene Paneländerungen und APIShared-Rezeptauswahl: K5. Weitere statistische Näherungen sind keine hier belegten Cross-Mod-Bugs. |
| ContinuousRecruit | Regulärer `EngineInterface.GameAction(MakeTroop,1,type)` bei `ContinuousRecruit/src/ContinuousRecruitPlugin.cs:179–186`. Er erreicht die APIShared-Interzeptoren von UnitCosts (`UnitCosts/src/MakeTroopGameActionHook.cs:45–47`) und UnitLimit (`UnitLimit/src/MakeTroopGameActionHook.cs:70–79`); kein belegter Kosten-/Limitbypass. |
| GranaryOverview | Spectator-Ressourcen und Produktionsledger: K3. Nicht jede feste Formel wurde als modübergreifender Fehler gewertet. |
| HealthBarColors | Sprite-/Healthbar-Eingriffe und unsere beschädigten Healthbars betrachtet; kein nachgewiesener harter Konflikt. Gemeinsame Spielobjekte allein beweisen kein gegenseitiges Überschreiben. |
| RangeDisplay | Auswahl-/Eingabepfade, Renderer und HealthBarColors-Zusammenarbeit betrachtet; kein bestätigter Konflikt mit unseren Mods. |
| ResourceLayout | Wrapper-/Validierungsabweichung: R2; kein freigegebener Ersatz-Baupfad. |
| SmartDodge | Befehlsbeobachtung und Pathfinding: K4; MP-Vertrag: R1. |
| StockDelta | Spectator-Ansicht und monatliche Historie: K2. |
| TargetPriority | MP-Vertrag: R1. Zielwahleinstieg `0x18E9A0` ist nicht APIShareds AttackTargetRules-Stelle `0x1867A0`; daraus kein direkter Überschreibspannen-Konflikt bestätigt. Vollständige Zielwahlsemantik bleibt außerhalb der abgeschlossenen Audits. |
| TradePresets | Neue Partie gegenüber Save-Laden, rohe Handelsschwellen und Einzelspieler-Guard betrachtet; kein konkret belegter Konflikt mit unseren Marktpreisregeln. |
| UnitStats | Datenquellen und Default-Stats gegenüber modifizierter Gesundheit betrachtet; feste Typstatistik ist nicht automatisch ein fehlerhafter Live-HP-Wert. Kein bestätigter Befund ohne passenden Anzeigevertrag. |

Gemeinsame Harmony-Postfixe auf `KeyManager.Update` sind grundsätzlich kombinierbar; ihr gemeinsames Ziel wurde nicht als Konflikt gezählt. Mod-GUIDs und Toolkit-Ressourcenpfade wurden auf erkennbare Identitätsüberschneidungen geprüft. Eine APIShared-Abhängigkeit oder spätere Ladereihenfolge allein löst weder veraltete Modelle noch uneindeutige Befehlsherkunft.

## Empfohlene Zusammenarbeit und spätere Abnahme

1. **Zuerst K1 und K2/K3:** effektive Popularitätsberechnung und frischer Berichtskontext sind wiederverwendbare APIShared-Ergänzungen mit klaren unabhängigen Verbrauchern.
2. **K4 gemeinsam umstellen:** Herkunft, Absicht und temporäre Steuerung eines Auftrags definieren; beide Verbraucher migrieren. Keine privaten Fremdmod-Feldzugriffe als Dauerlösung.
3. **R1 absichern:** ausdrücklicher MP-Ausschluss, bis ein deterministischer Entwurf und echte Host-/Clienttests vorliegen. Kein bloßes Manifest-Umetikettieren.
4. **K5 mit einer Zusatzrezept-Fixture prüfen**, anschließend Präsentationskontext und dekorative Icons integrieren.
5. **R2 separat vollständig auditieren**, bevor eine gemeinsame bezahlte Bau-API zugesagt wird. I1 kann überwiegend vorhandene öffentliche Dienste verwenden.

Jede API-Ergänzung additiv gestalten, mit stabilen öffentlichen Identitäten, Besitzer-GUID, Verfügbarkeit, Thread, Lebensdauer, Fehlerisolation und klarer Abgrenzung zwischen Beobachtung und Mutation. Keine Pflichtmember zu bestehenden Interfaces hinzufügen. Verbraucher referenzieren die öffentliche Assembly mit `Private=false`, deklarieren die tatsächlich benötigte Mindestversion in Plugin und Manifest und liefern keine zweite APIShared-Kopie aus. Fachliche Strategien, Aktivierung und Texte bleiben im Toolkit.

Für eine Umsetzung sind insbesondere diese bestehenden Prüfungen relevant:

- K1: Toolkit `tests/AutoTax/Program.cs`; APIShared `tests/PopularityDisplay.Tests/ReportTests.cs`, `PopularityModifierTests.cs`, `FearPopularityTests.cs`; ExtraFeatures `tests/PopularityLifecycleTests.cs`. Den oben ausgeführten realen Reglerfall mit effektiver Kandidatentabelle absichern.
- K2/K3: vorhandene Perspektiv-/Identitätstests erweitern. A → B → A im selben Monat, Wechsel am Monatsrand, Pause, Save-Laden und absichtlich verzögerte Berichte prüfen; kein falscher Zwischenwert aus altem Snapshot. Produktionsledger getrennt nachprüfen.
- K4: temporärer Pathfinding-Stopp, Dodge und spätere Wiederaufnahme; neuer echter Nutzerbefehl während der Wartephase; keine doppelte oder verlorene Absicht. Vorhandene Replan-/Befehlsprüfungen um die gemeinsame Herkunft erweitern.
- K5: zusätzliche Workshopoption, Wechsel auf Vanilla-Rezept, HUD-Neuerstellung und Moddeaktivierung. Ingame prüfen, dass Zahlen, Icons und Auswahl nebeneinander korrekt bleiben.
- R1: echte Host-/Clientpartie mit unterschiedlichen lokalen Spielern, Ansichten und Aktivierungen. Logs und identische Simulationsergebnisse statt bloßer Installation als Nachweis verwenden.

Dieser Auftrag änderte nur diesen Bericht. Ausgeführte technische Prüfung: Quell- und Aufrufpfadprüfung, hashgebundene Baseline-Abgleiche, isolierter Lauf der tatsächlichen AutoTax-Reglerklasse sowie Dokumentprüfung. Die vorgeschlagenen Korrekturen sind überprüfbare Entwürfe, keine bereits getesteten Implementierungen.
