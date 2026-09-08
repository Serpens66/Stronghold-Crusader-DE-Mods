# Funktionswahrender Audit häufiger Mod-Callbacks

Stand: 2026-09-08

## Umfang und Beweisbasis

Geprüft wurden die Runtime-Quellen und die jeweils eingebundenen `Shared`-Quellen von:

- `BugfixesAndQoL`
- `BuildingCosts`
- `BuildingLimit`
- `CustomCustomTrail`
- `ExtraFeatures`
- `RandomEvents`
- `CastlePlanner`
- `StartConditions`
- `UnitCosts`
- `UnitLimit`

Test-, Diagnose-, Backup-, CLI-, `bin`- und `obj`-Bestände wurden nicht als produktive Callbackquellen gewertet. Die Prüfung umfasst direkte Unity- und Script-Extender-Abonnements, detourte Unity-`Update`-Methoden, Noesis-Aktualisierungspfade, Eingabe-, Unit-, Building-, Ressourcen- und Netzwerkereignisse sowie native Detours und Context-Hooks.

Die installierte `CrusaderDE.dll` und `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` stimmen mit SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` überein. Die semantische Baseline gehört zu Script Extender 2.3.0 und Commit `a0cd52993b44a6909d4f7f6a92f82fa5888a8e63`.

Wichtiger Frequenzbefund: `Director.Update()` kann `FatControler.NoesisGUIUpdateChecksInGame()` beim Leeren der UI-Puffer bis zu dreimal innerhalb eines gerenderten Frames aufrufen; ohne Pufferarbeit folgt zusätzlich der zeitgesteuerte Aufruf. Daher sind die vier modseitigen Noesis-Detours nicht bloß „einmal pro Frame“, sondern im Belastungsfall bis zu dreimal pro Frame aktiv. Vanilla setzt unter anderem Recruitment-Buttons und Markt-UI bei jedem dieser Durchläufe erneut. Ein lokales „nur beim ersten Aufruf des Frames“-Guard wäre deshalb nicht funktionsgleich, weil ein späterer Vanilla-Durchlauf die Modwerte wieder überschreiben könnte.

## Gesamtergebnis

- Der größte sichere Gewinn liegt beim gemeinsamen Lobby-Poller: dieselbe Roster-/Identitätsaufnahme kann bei gemeinsamer Installation derzeit aus zehn source-linked Assemblykopien erfolgen. Sie soll pro Prozess nur einmal aufgenommen und anschließend an alle eigenständigen Modinstanzen verteilt werden.
- Die vier Noesis-Detours und zwei `HUD_Main.UpdateRollover`-Detours brauchen weiterhin einen Abschluss nach Vanillas letzter UI-Aktualisierung. Sie können voraussichtlich durch eine einmalige Post-`Director.Update`-Phase ersetzt werden; das darf erst nach den unten beschriebenen Reihenfolge- und UI-Regressionstests freigegeben werden.
- Eingabe, Auswahl, Darstellung und einige native Zustände besitzen in Script Extender 2.3.0 kein vollständiges Ereignis. Diese Poller beziehungsweise nativen Hotpath-Hooks dürfen nicht entfernt oder willkürlich gedrosselt werden.
- Mehrere Tickpfade sind fachlich notwendig, arbeiten aber auch im Leerlauf oder allozieren beim Abarbeiten. Dort sind Dirty-/Queue-Guards, bedarfsabhängige Abonnements und wiederverwendbare Puffer sicherer als eine Frequenzreduktion.
- Ein ausdrücklich einmaliger oder zeitlich begrenzter Callback ist bereits korrekt und wird nicht durch komplexere Infrastruktur ersetzt.

## Direkte Frame- und Render-Callbacks

| Mod/Feature | Quelle und Lebensdauer | Erkannte Zustände / Reaktionsbedarf | Urteil | Funktionswahrende Maßnahme |
|---|---|---|---|---|
| Alle zehn Mods: Preset-/Lobby-Einstellungen | `Shared/PresetLobbyModSettingsViewModel.cs`, dauerhaft außerhalb gestarteter Maps, Prüfung alle 15 Renderframes; bis zu zehn Assemblykopien | Lobby-Roster, lokale Identität, Host-/Clientrolle, Slotwechsel, Join/Leave; Reaktion innerhalb der bisherigen 15-Frame-Grenze | **leichter ausführbar** | Ein versionierter, prozessweiter Lobby-Observer nimmt exakt im bisherigen 15-Frame-Rhythmus einmal einen unveränderlichen Snapshot auf. Jede Modkopie behält ihren eigenen Coordinator und erhält nur Snapshots. Join-/Map-Ereignisse dienen als Dirty-Signale, Polling bleibt als vollständiger Fallback für nicht gemeldete Slot-/Rosterwechsel. |
| CustomCustomTrail: Kompatibilitätsrefresh nach Registrierungen | `CustomCustomTrailPlugin.cs`, genau ein `onBeforeRender`, danach Selbstabmeldung | Muss nach allen Startregistrierungen, aber vor Nutzung der Kompatibilitätsdaten laufen | **notwendig** | Unverändert behalten. Der Callback ist bereits einmalig, allokationsarm und korrekt abgemeldet. |
| CastlePlanner: Blueprint-Steuerung | `BlueprintRuntimeController.cs`, dauerhaft registriert, teure Arbeit nur bei aktivem Blueprintmodus oder Hotkey-Capture | Maus-/Tastatureingabe, Kamera, Overlay, Workerabschluss; muss auch bei pausierter Simulation im aktuellen Renderframe reagieren | **notwendig** | Frame-Nähe behalten. Die vorhandenen Aktivitätsguards bleiben maßgeblich; eine Tickumstellung wäre bei Pause und Eingabereihenfolge nicht gleichwertig. |
| CastlePlanner: Free-Castle-Preview | `FreeCastlePreviewRuntime.cs`, dauerhaft registriert, Arbeit nur bei ausstehendem/aktivem Preview | UI-Bereitschaft, asynchroner Katalogabschluss, Countdown, Netzwerk-Retry/Timeout | **nur bei Bedarf nötig** | Erst beim Eintritt in `pending`/`active` abonnieren und bei Erfolg, Abbruch, Lobbyverlassen, Mapstart, Fehler und Dispose abmelden. Zustandsübergänge stammen bereits aus eigenen Hooks/Packets, daher bleibt das Frameverhalten während der aktiven Phase unverändert. |
| ExtraFeatures: Gatehouse-Automation-Button | `GatehouseAutomationRuntime.cs`, dauerhaft | aktuell selektiertes eigenes Gate, Noesis-Elementverfügbarkeit, Icon-Ladevorgang; sichtbare Reaktion im selben Frame | **notwendig** | Kein vollständiges Selection-Changed-Ereignis vorhanden. Polling nur in erlaubter Map und bei aktiviertem Feature ausführen; Icon-Laden als separat abgeschlossene Einmaloperation behandeln. Eine spätere gemeinsame Post-UI-Phase ist geeignet, reine Ereignisumstellung nicht. |
| BugfixesAndQoL: Ally-Goods-Mengenanzeige | `AllyGoodsAmountModifierHook.cs`, dauerhaft | Shift/Ctrl gedrückt oder losgelassen, Fokusverlust; aktualisiert vier Bindings nur bei Moduswechsel | **ereignisbasiert ersetzbar** | `InputR3EventHooks.OnKeyDown`/`OnKeyUp` für linkes/rechtes Shift und Ctrl plus `Application.focusChanged` verwenden. Aus allen vier Tasten einen Modifierzustand bilden und nur bei Zustandsänderung Properties melden. Initialzustand bei Map/UI-Aktivierung einmal aufnehmen. |
| BugfixesAndQoL: Assassin-Climb-Button | `AssassinClimbRuntime.cs`, dauerhaft nach Initialisierung | Auswahl genau eines steuerbaren Assassinen, UI-Neuerzeugung, Sichtbarkeit | **notwendig** | Script Extender 2.3.0 meldet keine vollständige Auswahländerung. Framepolling mit bestehendem Signaturcache behalten, aber nur in laufender erlaubter Map und bei aktivem Feature arbeiten. Post-UI-Broker ist möglich; Drosselung ist nicht funktionsgleich. |
| BugfixesAndQoL: Auflösungswiederherstellung | `DisplayResolutionPersistenceHook.cs`, nur während Recovery, frame-dedupliziert, endet nach zwei gerenderten Frames oder Timeout | muss zwei tatsächlich gerenderte Frames und Fokus-/Auflösungszustand beobachten | **notwendig** | Unverändert behalten. Simulationsticks und Zeitdrosselung würden die Bedeutung „gerenderter Frame“ verändern. |
| BugfixesAndQoL: Friendly-Moat-Performancebericht | `FriendlyMoatMovementRuntime.cs`, dauerhaft, Bericht höchstens alle fünf Sekunden und nur nach Queries | verschiebt Diagnoseausgabe aus nativen Callbacks auf sicheren Managed-Pfad | **leichter ausführbar** | Vor Stopwatch/Logging zuerst atomaren `queryCount == 0`-Guard verwenden. Eine Verlagerung auf `OnTick` würde Berichte während Pause verzögern und ist nur mit akzeptierter Diagnoseänderung zulässig. |
| BugfixesAndQoL: Lord-Unit-Controls | `LordUnitControlsFeature.cs`, dauerhaft | Auswahl genau eines eigenen Lords, Vanilla öffnet dafür nicht in allen Fällen den üblichen Truppenpfad | **notwendig** | Framepolling und Cache behalten; nur in erlaubter laufender Map/aktivem Feature arbeiten. `SetupSelectedTroops` allein ist kein vollständiger Ersatz. |
| BugfixesAndQoL: Selected-Unit-Health | `SelectedUnitHealthFeature.cs`, dauerhaft | beliebige Schadens-, Heilungs-, Lösch- und Auswahländerungen; Anzeige soll ohne Tickverzögerung stimmen | **leichter ausführbar** | Polling behalten, weil es kein vollständiges Health-/Selection-Ereignismodell gibt. Das Array `SelectedUnitHealthSummary[CHIMP_NUM_TYPES]` einmal anlegen und pro Frame zurücksetzen/wiederverwenden; ViewModel nur bei geänderter Zusammenfassung publizieren. |
| BugfixesAndQoL: Surrender/Spectator-Promotion/Lobby-Return | `SurrenderFeature.cs`, dauerhaft; Lobby-Return-Arbeit nur bei Pending, Eliminationsprüfung während Match | frühestmögliche Lord-Elimination, Spectator-Promotion, Game-over-/UI-Reihenfolge, asynchroner Lobbyreturn | **Entscheidung des Benutzers erforderlich** | Lobby-Return-Teil nur während `pendingVanillaExit` abonnieren. Für die Eliminationsprüfung existiert kein nachweislich vollständiges Todes-/Entfernungsereignis; für volle Gleichwertigkeit während geeigneter Matches pro Frame behalten. Jede Drosselung verschiebt Promotion/UI und benötigt eine bewusste Entscheidung. |

## Noesis-, UI- und Unity-Update-Detours

| Mod/Feature | Frequenz und Kosten | Urteil | Maßnahme |
|---|---|---|---|
| UnitCosts: Recruitment-Verfügbarkeit | Nach jedem `NoesisGUIUpdateChecksInGame`; prüft bis zu 27 Buttons und bei konfigurierten Mehrkosten Ressourcen | **leichter ausführbar** | Muss nach Vanillas letzter Button-Aktivierung laufen. In eine einmalige Post-`Director.Update`-Phase verschieben. Zusätzlich nur konfigurierte Unittypen in einer vorberechneten kompakten Liste prüfen; keine Vollprüfung aller Buttonfelder. |
| UnitLimit: Recruitment-Verfügbarkeit | Nach jedem Noesis-Durchlauf; prüft bis zu 27 Buttons, Cachezugriffe sind O(1), entfernt aber auch Pending-Recruitments | **leichter ausführbar** | Einmalige Post-`Director.Update`-Phase. Nur konfigurierte Limits iterieren; Pending-Ablauf einmal pro Phase, nicht je Button. Unit-/Tent-Caches bleiben ereignisbasiert. |
| BugfixesAndQoL: HD-Markt-Navigation | Nach jedem Noesis-Durchlauf; gute frühe Mod-/Panel-/Ownership-Guards, danach Nachbarsuche und Iconzuweisung | **leichter ausführbar** | In dieselbe Post-UI-Phase verschieben und nur bei aktivem Tradepostpanel ausführen. Zuletzt publizierte Goods-ID/Icon-Identitäten cachen, damit unveränderte Noesis-Properties nicht erneut gesetzt werden. |
| BugfixesAndQoL: Ctrl-Einzelhandel-UI | Nach jedem Noesis-Durchlauf; bei Ctrl im Markt Ressourcen-/Preisabfragen, Stringersetzung und Buttonstatus | **leichter ausführbar** | In Post-UI-Phase verschieben. Dirty-Signale: Modifierwechsel, Handelsgutwechsel, Ressourcenereignis und Tradepostpanelwechsel; ein einmaliger Frame-Fallback bleibt, damit direkte/native Ressourcenänderungen nicht verpasst werden. |
| BuildingCosts: `HUD_Main.UpdateRollover` | durch Noesis potenziell mehrfach pro Frame; liest Reflectionfelder und bildet Ressourcensignatur, publiziert nur bei Cacheänderung | **leichter ausführbar** | Nach dem letzten Noesis-Durchlauf einmal aktualisieren. Hover-/Sichtbarkeitszustand und Ressourcensignatur weiter vollständig vergleichen; reine Ressourcenereignisse reichen wegen direkter nativer Änderungen nicht aus. |
| BuildingLimit: `HUD_Main.UpdateRollover` | potenziell mehrfach pro Frame; Cache verhindert wiederholtes Publizieren | **leichter ausführbar** | Ebenfalls einmal nach `Director.Update`; Building-Cache-Ereignisse als Dirty-Signal, finaler UI-Zustandsvergleich als Fallback. |
| CastlePlanner: `FRONT_Multiplayer.Update` für AIV-Platzierung | einmal pro Lobbyframe; aufwendige Aufnahme 10 Hz, Assetfingerprint 2 Hz, sonst Worker-/UI-/Ready-Prüfung | **notwendig** | Update beibehalten. Billigen `no pending work && panel inactive`-Guard vor Reflection/Workerprüfungen setzen; bestehende 10-Hz-/2-Hz-Gates erhalten. |
| BugfixesAndQoL: Custom-Lord-Liste `FRONT_Multiplayer.Update` | einmal pro Lobbyframe; Arbeit nur bei sichtbarer Liste und gehaltenen Pfeiltasten, Vanilla-Wiederholrate 100 ms | **notwendig** | Beibehalten. `OnKeyDown` allein bildet gehaltene Tasten und Vanillas Wiederholung nicht gleichwertig ab. |
| BugfixesAndQoL: Multiplayer-AIV-Sync `FRONT_Multiplayer.Update` | einmal pro Lobbyframe; sofortiger Return ohne Manifest/Transfer, ansonsten Workerabschluss/Timeout/Startblockade | **notwendig** | Beibehalten; UI-Thread- und Reihenfolgeanforderung. Dynamisches Detour-Installieren wäre riskanter als der vorhandene Fast-Path. |
| BugfixesAndQoL: `HUD_MPResync.Update` | pro aktivem Resync-UI-Frame | **notwendig** | Beibehalten; die Prüfung ist an die sichtbare Resync-Instanz gebunden und muss Host-/Mitgliederänderungen ohne zusätzliche Verzögerung erkennen. |
| BugfixesAndQoL: `KeyManager.Update` für Multiplayer-Spielgeschwindigkeit | pro Frame; Eingabe- und Repeat-Scheduler | **notwendig** | Beibehalten; unmittelbare Eingabe und gehaltene Tasten erfordern Framenähe. Fast-Path darf nicht allozieren. |
| CastlePlanner: `CameraControls2D.Update` | pro Kameraframe; Boolean-/HUD-Guard und danach Original | **notwendig** | Unverändert behalten; verhindert Zoom im exakt selben Eingabeframe. |

### Gemeinsame Post-UI-Phase

Für die sechs Noesis-/Rollover-Nachbearbeitungen ist folgende Architektur funktionswahrend, sofern die Regressionstests erfolgreich sind:

1. Eine source-linked `SharedPostUiUpdateBroker`-Implementierung detourt pro Prozess genau einmal `Director.Update` und ruft Subscriber nach dem Original auf. Damit liegt die Phase garantiert nach allen Noesis-Durchläufen dieses Frames.
2. Der zuerst geladene Mod veröffentlicht über `AppDomain` nur einen stabilen, BCL-basierten Vertrag mit expliziter Vertragsversion. Spätere Mods erkennen diese Version und registrieren ihren eigenen Delegate. Keine Assemblytypen eines Mods dürfen Vertragsbestandteil sein.
3. Subscriber werden in registrierter Reihenfolge einzeln mit `try/catch` aufgerufen; ein fehlerhafter Mod verhindert weder Vanilla noch andere Mods. Duplicate-IDs werden abgewehrt, Dispose meldet sauber ab.
4. Bei fehlendem oder inkompatiblem Brokervertrag installiert jeder Mod seinen bisherigen eigenständigen Detour. Ein inkompatibler Prozessbroker führt also nicht zum Funktionsverlust. Der Broker besitzt den Hook prozessweit bis zum Spielende; Mod-Subscriber bleiben abmeldbar.
5. `UnitCosts` und `UnitLimit` dürfen ihre gegenseitige Reihenfolge nicht voraussetzen. Beide wenden nur zusätzliche Einschränkungen auf Vanillas aktuellen Zustand an, sodass das kombinierte Resultat unabhängig von ihrer Reihenfolge die Schnittmenge beider Regeln bleibt.

Vor Freigabe muss bewiesen werden, dass `Director.Update` in allen Ingame-UI-Pfaden erreicht wird, der Callback nach dem letzten Noesis-Aufruf liegt und Pause, Fokusverlust, UI-Wechsel sowie Editor keine abweichende sichtbare Zwischen-/Endlage erzeugen. Bis dahin bleiben die existierenden Detours funktional maßgeblich.

## Simulationsticks

| Mod/Feature | Arbeit und notwendige Semantik | Urteil | Funktionswahrende Maßnahme |
|---|---|---|---|
| StartConditions: Keep-Readiness-Waiter | nur während ausstehender Keep-Bereitschaft; beendet sich bei Erfolg, Timeout oder Cancel | **notwendig** | Unverändert. Bereits bedarfsabhängig und garantiert den frühesten geeigneten Simulationstick. |
| RandomEvents: Scheduler/Handshake/Monatswechsel | Mapinitialisierung, Host-/Client-Acks und Retries, monatliche Ereignisse, Banditen-/Signpost-Warteschlangen | **notwendig** | Tick während pending/aktiver Map behalten. Kein vollständiges Month-Changed-Ereignis vorhanden; Frequenzreduktion könnte einen anderen Ausführungstick und Multiplayerreihenfolge erzeugen. Außerhalb pending/aktiver Map abmelden. |
| CastlePlanner: Deferred-Compound-Queue | bearbeitet ausstehende Compound-Platzierungen auf dem ersten geeigneten Tick; derzeit `Keys.ToArray()` bei Arbeit | **nur bei Bedarf nötig** | Nur bei nichtleerer Queue abonnieren und bei Leerstand abmelden; alternativ dauerhaften O(1)-Leerreturn behalten. Schlüssel in wiederverwendbaren Puffer kopieren statt pro Tick Array zu allozieren. |
| ExtraFeatures: Lord Health | scannt alle zehn Ticks Spieler 1–8, bis Lords erkannt/angewandt sind | **Entscheidung des Benutzers erforderlich** | `OnUnitCreate(Post)` kann als Dirty-Signal dienen, ist aber nicht als vollständiger Ersatz bewiesen, weil die Lord-ID möglicherweise erst im nativen Aufrufer nach dem Create-Callback gesetzt wird. Für volle Gleichwertigkeit 10-Tick-Fallback behalten und nur ungelöste Spieler prüfen; Event-only kann Lords verpassen. |
| ExtraFeatures: Fear-Factor-Diagnose | verarbeitet atomaren Render-Marker und schreibt einmaligen Alive-Log | **leichter ausführbar** | Gameplayhooks bleiben unabhängig. Tick nur bis zum Alive-Log und bei pending Overlaymarker nötig; danach abmelden und vom Renderhook bei erstem Marker einmalig wieder aktivieren. Alternativ wegen minimaler Kosten unverändert lassen. |
| BugfixesAndQoL: Extended Shift Command Queue | Commandtiming, Completion, Erwartungssignale, Cohort-Koaleszierung; erstellt bei Arbeit temporäre Dictionaries/Listen | **leichter ausführbar** | Ticksemantik behalten. Nach `currentTick` und notwendigem Large-Move-Update sofort zurückkehren, wenn Queues, Signale und Cohorts leer sind. Arbeitsdictionary/-listen wiederverwenden. Keine Drosselung, weil sich Ausführungstick und MP-Chore-Reihenfolge ändern würden. |
| BugfixesAndQoL: Friendly Moat Attack-State | räumt zuerst synchrone Scopes auf, für die wegen `SkipOriginalFunction` kein Postevent kommen kann, danach verfolgt Angriffsstatus | **notwendig** | Tick behalten. Die Cleanup-Phase ist selbst die Absicherung gegen fremde Subscriber und darf nicht dynamisch abgemeldet werden. Nach Cleanup bei leerem Tracking sofort zurückkehren. |
| BugfixesAndQoL: Plague Popularity | Projectile-Spawn/Delete-Ereignisse plus Reconciliation natürlicher Abläufe; natürliche Expiry kann ohne Deleteevent erfolgen | **nur bei Bedarf nötig** | Tick nur solange Herd-/Projectilezustand, erwartete Callbackdiagnose oder Mapinitialisierung offen ist. Native Popularitätskorrektur bleibt im exakten Callback. Event-only ist nicht vollständig. |
| BugfixesAndQoL: Quarry Pile Relocation | erster Tick nach Mapstart zur AI-Spawn-Beobachtung, danach nur ausstehende Relocations; derzeit Schlüsselkopie je Arbeitstick | **nur bei Bedarf nötig** | Für den ersten Maptick und bei nichtleerer Pending-Menge abonnieren; Building-Spawn/Packet kann erneut aktivieren. Wiederverwendbaren Key-Puffer nutzen. |

## Ereignisse mit potenziell sehr hoher Häufigkeit

Diese Pfade sind nicht periodisch, können aber pro Unit, Building, Tile, Ressourcenoperation oder Eingabeframe sehr häufig auftreten.

| Familie / Vorkommen | Frequenz | Urteil und Begründung |
|---|---|---|
| Unit-Caches und Übergänge: `UnitLimit/ActiveUnitCache`, `UnitCosts.OnUnitTransition`, Fast-Recruit-Rally | pro Create/Delete/Transition | **notwendig**. Ereignisse ersetzen Vollscans und halten die O(1)-Caches korrekt. Phasen-/Source-/Featureguards müssen vor allen weiteren Abfragen stehen; im nicht passenden Pfad keine Allokation und kein Logging. |
| Building-Caches und Platzierung: BuildingLimit, UnitLimit Siege-Tents, UnitCosts, RandomEvents Signposts | pro Spawn/Delete/Placement/Damage | **notwendig**. Die Regeln müssen vor der nativen Platzierung beziehungsweise im exakten Damage-Aufruf wirken. Mapstart-Vollscan bleibt nur Initialabgleich. |
| Ressourcen-/Marktereignisse: ExtraFeatures und BugfixesAndQoL | pro Goods-/Marktoperation | **notwendig**. Sie sind bereits ereignisbasiert und vermeiden Framepolling. UI darf diese Ereignisse nur als Dirty-Signal nutzen, weil direkte native Änderungen nicht garantiert gemeldet werden. |
| Gatehouse Query: ExtraFeatures Gatehouse Automation und Bugfixes Reachable Enemy Gatehouse | pro nativer Unit-/Gate-Prüfung, somit öfter als pro Frame möglich | **notwendig**. Das Ergebnis wird in der laufenden nativen Entscheidung benötigt; nachträgliche Events sind zu spät. Aktivierungs-, Player-, Building- und Unit-ID-Guards müssen ganz vorne bleiben. Beide Mods ändern getrennte fachliche Fälle und dürfen keine Installation des jeweils anderen voraussetzen. |
| Tribe-/Unit-Order-Events: Extended Shift, Friendly Moat, Assassin, TroopMovementFix3 | pro Befehl, bei Gruppen potenziell mehrfach | **notwendig**. Pre-/Post-Reihenfolge und native Commandsemantik sind Teil der Funktion. Keine Umstellung auf spätere Tickpoller. |
| InputR3 `OnKey`/KeyManager-Achsen | pro gehaltene Taste beziehungsweise Achsenabfrage | **notwendig**, wo kontinuierliche Bewegung/Repeat benötigt wird. Reine `OnKeyDown`-Umstellung nur für Zustandsmodifier wie Ally-Goods, nicht für Kamera oder Wiederholung. |

## Native Hotpaths auf FBCB9319

Die nativen Detours wurden nach Aufrufort und Fast-Path bewertet. Statische Bytepatches ohne Managed-Callback verursachen keine zusätzliche Managed-Aufrufarbeit und sind deshalb nicht als Poller eingestuft; ihre funktionale/nativvertragliche Prüfung bleibt davon unberührt.

| Native Familie / Dateien | Tatsächliche Größenordnung | Urteil | Auditmaßnahme |
|---|---|---|---|
| Sichtbare-Tile-Overlay: `LargeMoveTargetMarkerRenderer`, Extended-Shift Draw-/Overlaypfade | pro sichtbarem Tile beziehungsweise pro Tribe-/Draw-Aufruf, klar öfter als pro Frame | **notwendig** | Darstellung muss im exakten Vanilla-Drawlauf injiziert werden. Vor Featuredelegate und Dictionarylookup atomaren/volatilen `markerCount == 0`-Fast-Path verwenden; keine Logs/Allokationen im Callback. Immutable Snapshot nur außerhalb des Rendercallbacks publizieren. |
| Fear-Overlay: `FearFactorNeutralizationRuntime.HideFearOverlay` | pro gerendertem Unit-Overlay/Healthbar | **notwendig** | Deaktivierter Pfad bleibt Pointercheck plus Boolean. Aktiviert nur Registerwert und atomaren Marker ändern; Logging weiterhin aus Tick/Managedphase. Kein Event kann den bereits laufenden Drawentscheid ersetzen. |
| Friendly-Moat/Pathfinding: `FriendlyMoatMovementRuntime`, `MoatWorkTargetSelection`, `NativeMovementRecovery` | pro Path-/Flood-/Region-/Candidate-/Cursorprüfung, teils pro Unit und Tile | **notwendig** | Muss während derselben nativen Entscheidung wirken. Vor Pointer-/API-Arbeit Scope-, Feature-, Owner- und Unittypguards; temporäre Sets/Puffer außerhalb der Callbacks wiederverwenden. Die vielen Hooks dürfen nicht durch einen anderen Workspace-Mod bereitgestellt werden. |
| Assassin Pathfinding/Combat Resume: `AssassinPathfindingRuntime`, `AssassinCombatResumeRuntime` | pro betreffender Pathanforderung/Statewechsel | **notwendig** | Exakte native Kontext- und Reihenfolgesemantik; Ereignisse wären zu spät. In Nicht-Assassin-/nicht aktivem Kontext sofort Original/Fast-return, keine Diagnoseallokation. |
| Troop Movement Fix 3 und Patrol: `TroopMovementFix3*`, `AiDefensePatrolFix` | pro Unit-/Tribe-Bewegungsentscheidung | **notwendig** | Detours/Context-Hooks behalten. Settings/Unittyp/Tribe zuerst prüfen. Keine Frame- oder Ticknachbearbeitung kann dieselbe Route/Cadence sicher herstellen. |
| Plague-/Projectile-Pfade: `PlagueDurationPatch`, `PlagueApothecarySearchRangePatch`, `PlagueApothecaryStateTransitionFix`, `PlagueTargetReservationFix`, `PlagueTreatmentFadeFix`, `AiFlagDiseaseTracker` | pro Disease-Update, Targetsuche, Projectile-/Behandlungsschritt | **notwendig** | Korrektur muss innerhalb des nativen Vergleichs/Transitionsschritts erfolgen. Deaktivierter Pfad maximal Flags/Boolean. Registrylookup O(1), keine Callbacklogs; Fehler einmalig außerhalb des Hotpaths melden. |
| AI Bau-/Reparatur-/Ökonomiepfade: `AIDefenseRepairRuntime`, `AITowerRuinRepairFix`, `BetterAIOverbuildRulesFix`, `AIEconomyProtectionHook`, `AiStoneReserveFix`, `AIMarketVanillaPriceHook` | pro AI-Bau-, Reparatur-, Preis- oder Verkaufsentscheidung; nicht zwingend pro Frame, aber burstfähig | **notwendig** | Ereignisse sind entweder nicht vorhanden oder kämen nach der Entscheidung. Konfiguration und Spielerart cachen, bevor teure APIs aufgerufen werden. `AIMarketVanillaPriceHook` sollte den aktivierten Mod-/AI-Preisstatus außerhalb des Preiscallbacks als volatile Snapshotflags führen; Original genau einmal. |
| Cursor-/Placement-Klassifikation: `EnemyProximityBulldozeCursorHook`, `MountedStockpileMovementPatch`, `MinimapPlacementClickHook`, Assembly-/Tunnel-Placement | Cursorpfade pro Frame/Tile, Klick-/Placementpfade pro Aktion | **notwendig** | Cursor-Klassifikation muss synchron sein. Fast-Path auf Feature und relevanten Modus/Mapper vor nativen Lookups. Klick-/Placementhooks sind aktionsgebunden und benötigen keine zusätzliche Drosselung. |
| Extended-Shift Chore-/Waypoint-Handler | pro Auftrag/Waypoint/Multiplayer-Chore | **notwendig** | Muss Marker vor dem Original schreiben und danach sicher zurücksetzen; späterer Tick wäre netzwerksemantisch falsch. Nur lokale selektierte Tribe prüfen, Original in jedem Fehlerpfad genau einmal. |
| Ctrl-Marktvalidator und sonstige Actionhooks | pro Markt-/GameAction, nicht periodisch | **notwendig** | Validierung muss synchron vor Ausführung erfolgen. UI-Refresh kann optimiert werden, Validator nicht. |

Die übrigen gefundenen Managed-Hooks sind durch konkrete Nutzeraktionen oder seltene Lebenszyklusaufrufe begrenzt: Lobby-/Trail-Buttons, Missionwechsel, Maplisten, Editor Save, Disband, Repair, Pause, Siege-Restock, Game-over, Connection-Issue, Hover Enter/Leave, Recruitment GameAction und Paketempfang. Sie sind keine Poller. Ihre Detours bleiben nötig, weil sie die Aktion selbst abfangen; eine Umstellung auf Framepolling wäre schlechter.

## Periodische Timer und verzögerte Aktionen

| Mod/Feature | Frequenz und Arbeit | Urteil | Funktionswahrende Maßnahme |
|---|---|---|---|
| Shared Crash Breadcrumbs in BugfixesAndQoL, ExtraFeatures und UnitLimit | bei aktivierter Diagnose je Assembly jede Sekunde; kopiert Ring, aktive Scopes und Counter, formatiert bis zu 32 KiB und schreibt abwechselnd zwei Dateien | **leichter ausführbar** | Pro Recorder eine atomare Dirty-/Sequence-Prüfung vor Snapshotaufbau und Dateizugriff. Nach jedem `Enter`, `Record`, `Complete` und Clean-Shutdown dirty setzen; nach erfolgreichem Schreiben nur dann clean setzen, wenn sich die Sequenz während des Schreibens nicht erneut geändert hat. Damit bleibt die maximale Persistenzlatenz eine Sekunde, unveränderte Snapshots verursachen aber keine wiederholte Allokation/I/O. Recorder bleiben modlokal, weil ihre Daten und Dateien modbezogen sind. |
| BugfixesAndQoL Steam-Lobby-Invite-Validierung | `System.Threading.Timer` alle fünf Sekunden; derzeit werden selbst bei leeren Mengen Listen angelegt | **leichter ausführbar** | Unter dem Lock zuerst `pendingInvites.Count == 0 && recentInvites.Count == 0` prüfen und ohne Listenallokation zurückkehren. Bei Arbeit wiederverwendbare Listen verwenden oder nur dann anlegen. Den bestehenden Fünf-Sekunden-Zeitanker behalten, damit der bisherige 20–25-Sekunden-Timeoutbereich nicht unbemerkt verschoben wird. |
| StartConditions sowie Limit-/Kostenbenachrichtigungen | einzelne `TimerEngine.AddDelayedAction`-Aufrufe, bei Neuanforderung ersetzt/abgebrochen | **notwendig** | Keine periodischen Poller. Unverändert behalten und auf idempotenten Cancel bei Mapende/Dispose achten. |

## Konkreter Umbauplan

### Phase A – sichere lokale Verbesserungen

1. Ally-Goods auf KeyDown/KeyUp/Focus umstellen und Modifierzustand testen.
2. Selected-Unit-Health-Array und Extended-Shift/CastlePlanner/Quarry-Arbeitspuffer wiederverwenden.
3. Crash-Breadcrumb-Snapshots dirty-gesteuert schreiben und leere Steam-Invite-Timerdurchläufe allokationsfrei machen.
4. Free-Castle-Preview, CastlePlanner Deferred Queue, Quarry und Plague-Tick nur in ihren exakt definierten aktiven Zuständen abonnieren; alle Terminalpfade zentral über idempotente `SetPollingRequired(bool)`-Methoden führen.
5. Native sichtbare-Tile-, Gatehouse-, Path- und Disease-Callbacks mit billigstem Inaktivitätsguard beginnen; Callbackpfade auf null Allokationen prüfen.
6. Keine Version erhöhen und keine README ändern, bis die Änderungen einzeln getestet und als final bestätigt sind.

### Phase B – prozessweiter Lobby-Observer

1. Source-linked, BCL-only AppDomain-Vertrag mit `ContractVersion`, `Register`, `Unregister`, unveränderlichem DTO und Fehlerisolation erstellen.
2. Genau einen 15-Frame-Poller und sofortige Dirty-Anstöße bei Join, Mapstart/-ende und bekannten Lobbyaktionen installieren.
3. Snapshotaufnahme einmal ausführen; per Wertvergleich nur Änderungen veröffentlichen. Jede Modinstanz behält ihren eigenen fachlichen Coordinator und seine Host-/Clientlogik.
4. Bei Vertragsinkompatibilität oder Brokerfehler lokalen bisherigen Poller verwenden. Niemals schweigend ohne Beobachter weiterlaufen.
5. Last-subscriber-Abmeldung darf den Prozesshost nur entfernen, wenn dies nachweislich detoursicher ist; ansonsten bleibt der billige Host bis Prozessende und pollt ohne Subscriber nicht.

### Phase C – gemeinsame Post-UI-Phase

1. Broker zunächst in einem isolierten Testhost und danach einzeln mit UnitCosts, UnitLimit, BuildingCosts, BuildingLimit und BugfixesAndQoL validieren.
2. Vanilla-Callzahl, Zeitpunkt des letzten Noesis-Aufrufs und finale Propertywerte instrumentieren, ohne im Release-Hotpath zu loggen.
3. Erst Recruitment-Hooks migrieren; danach Markt- und Rolloverhooks. Jeder Schritt behält bis zur bewiesenen Gleichwertigkeit einen separat aktivierbaren alten Pfad für Tests, aber nicht dauerhaft als Releasefallback parallel.
4. Gemeinsame Installation prüft beliebige Mod-Ladereihenfolgen, Brokervertragsversion und Subscriberfehler. Nach finaler Freigabe bleibt pro Prozess nur ein `Director.Update`-Detour.

### Phase D – optionale Konsolidierung ohne Frequenzgewinn

Die drei `FRONT_Multiplayer.Update`-Detours könnten technisch denselben Broker nutzen. Da jeder fachliche Callback weiterhin einmal pro Frame laufen muss und die aktuellen Fast-Paths billig sind, ist dies kein vorrangiger Performanceumbau. Eine Konsolidierung ist nur sinnvoll, wenn Detour-Kompatibilität oder Wartbarkeit konkret problematisch werden.

## Regressionstestplan

### Installationsmatrix

- jeden der zehn Mods einzeln nur mit seinen deklarierten harten Abhängigkeiten;
- alle zehn gemeinsam;
- relevante Paare: UnitCosts + UnitLimit, BuildingCosts + BuildingLimit, CastlePlanner + BugfixesAndQoL, ExtraFeatures + BugfixesAndQoL;
- jede Broker-teilnehmende Mod als zuerst und zuletzt geladene Assembly;
- fehlende optionale Mods sowie absichtlich inkompatible Brokervertragsversion im Testhost.

### Lebenszyklus- und Spielzustände

- Mapstart, Mapende, direktes neues Spiel, Save laden und erneut laden;
- Pause, minimale/maximale Spielgeschwindigkeit und lange Simulationssprünge;
- Mapeditor und Rückkehr ins Menü;
- Lobby erstellen, beitreten, verlassen, Hostmigration, Slot-/Teamwechsel und Spielerjoin/-leave;
- Host und Client, erfolgreicher/abgebrochener Handshake, Timeout und Paket-Duplikat;
- Fokusverlust/-gewinn, Auflösungswechsel, UI-Panelwechsel und verzögerte Noesis-Elementerzeugung;
- Feature zur Laufzeit an/aus und Modfunktion bei Mapbeginn bereits deaktiviert.

### Funktionsgleichheit

- UnitCosts/UnitLimit: alle Recruitment-Buttons, Ctrl/Shift-Mengen, Ressourcenänderung, Pending-Recruitment, Siege-Tents und gleichzeitige Limits/Kosten;
- BuildingCosts/BuildingLimit: Hover-/Selected-Tooltip, kompakt/detailliert, Platzierungsmodus und direkte native Ressourcen-/Buildingänderung;
- Markt: normales/HD-Goods-Ordering, Ctrl, Ctrl+Shift, zu wenig Gold/Güter/Lager, AutoTrade und Editor;
- Auswahlfeatures: schnelle Auswahlwechsel innerhalb aufeinanderfolgender Frames, UI-Neuaufbau, Lord/Assassin/mehrere Unittypen, Tod und Heilung;
- CastlePlanner: Eingabe bei Pause, Kameraunterdrückung, Workerabschluss, Queueausführung im exakt gleichen Tick, Preview-Retry/Timeout;
- RandomEvents/Plague/Quarry/Friendly Moat: natürliche Expiry ohne Deleteevent, fehlendes Postevent durch fremdes `SkipOriginalFunction`, erster möglicher Tick und Multiplayerreihenfolge.

### Hotpath-Messungen

- Callzähler pro Renderframe für `Director.Update`, Noesis, jeden Subscriber, `onBeforeRender`, Tick und native Unit-/Tile-/Draw-Hooks;
- kein aktiver Subscriber beziehungsweise keine teure Zustandsaufnahme außerhalb des definierten Aktivitätsfensters;
- null Managed-Allokationen im kritischen Fast-Path soweit technisch möglich; insbesondere Visible-Tile, Fear-Overlay, Gatehouse, Pathfinding, Recruitment-Nachbearbeitung im unveränderten Zustand und Tick-Leerlauf;
- identische finale Noesis-/ViewModel-Werte nach dem letzten Update des Frames;
- identische GameAction-/Chore-/Packetreihenfolge und identischer Ausführungstick;
- vollständige Abmeldung nach Dispose, Mapende und Terminalzuständen, ohne doppelte Registrierung nach erneutem Mapstart.

## Benutzerentscheidungen vor späterem Codeumbau

Für drei Bereiche gibt es derzeit keine belegte eventbasierte Alternative mit voller Gleichwertigkeit:

1. **Assassin-Climb, Lord-Unit-Controls und Gatehouse-Button:** Empfehlung ist Polling einmal pro gerendertem Frame in erlaubten aktiven Maps. Eine Drosselung auf 30/15 Hz spart Abfragen, kann die Anzeige aber um etwa 33/67 ms verzögern und kurze Auswahlzustände verpassen.
2. **Surrender-Spectator-Promotion:** Empfehlung ist Prüfung pro gerendertem Frame während geeigneter Matches. Tick- oder Zeitdrosselung kann Promotion und Game-over-/UI-Reihenfolge verschieben.
3. **ExtraFeatures Lord Health:** Empfehlung ist Unit-Create als Dirty-Signal plus bestehender 10-Tick-Fallback für ungelöste Lords. Reines Eventverhalten ist billiger, kann aber einen Lord verpassen, falls die Player-Lord-ID erst nach dem Create-Callback gesetzt wird.

Ohne gegenteilige Entscheidung gilt in allen drei Bereichen die funktionswahrende Empfehlung; eine verlustbehaftete Drosselung beziehungsweise Event-only-Variante wird nicht umgesetzt.

## Abschließende Gegensuche

Die Gegensuche umfasste `Application.onBeforeRender`, `GameTimeManagerAPI.Instance.OnTick`, `Update`, `LateUpdate`, `FixedUpdate`, Rendermethoden, Coroutines, `System.Threading.Timer`, `TimerEngine.AddDelayedAction`, detourte `Update`- und `UpdateRollover`-Methoden, `NoesisGUIUpdateChecksInGame`, Script-Extender-Observables, `AddDetour`, `AddContextHook`, `X64InlineHook` und MonoMod-`Hook`-Konstruktionen. Alle produktiven wiederkehrenden Treffer des definierten Umfangs sind oben entweder einzeln oder in einer ausdrücklich benannten Ereignis-/Native-Familie eingeordnet. Es verbleibt kein bekannter häufiger Callback ohne fachliche Begründung und Urteil.
