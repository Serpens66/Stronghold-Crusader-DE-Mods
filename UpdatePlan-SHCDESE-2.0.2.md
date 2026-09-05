# Updateplan: SHCDE Script Extender 1.42.0 auf 2.0.2

## 1. Ziel und verbindlicher Rahmen

Dieser Plan beschreibt die direkte Migration aller betroffenen Workspace-Projekte von
SHCDE Script Extender 1.42.0 auf exakt 2.0.2. Es wird weder ein paralleles
1.42.0-Build noch eine Kompatibilitätsschicht für alte Extender-Verträge beibehalten.

Der Plan ersetzt `UpdatePlan-SHCDESE-1.43.2.md` als maßgebliche Arbeitsgrundlage.
`SHCDESE-1.43.2-ChoreTransport-API-Report.md` bleibt eine historische Analyse, seine
Fail-closed-Anforderungen werden hier auf den öffentlichen 2.0.2-Vertrag übertragen.

Nicht Bestandteil dieser Migration sind Änderungen am kanonischen
`shcde-script-extender`-Quellbaum, sachfremde Modfeatures oder README-Änderungen.
Vorhandene Benutzeränderungen, insbesondere in `MoveMoatTest`, `QueueTest`, der nach
`BugfixesAndQoL` übernommenen Moat-Fill-Funktion, `RandomEvents` und `_inspect`, müssen
erhalten bleiben. Die gelöschten Diagnosemods `ChoreTestMod`, `MoatFillTargetTest` und
`MPTest` werden nicht wiederhergestellt.

`MoveMoatTest` ist aufgrund der ausdrücklichen Benutzerentscheidung vom 2026-09-05
dauerhaft aus dem Zielumfang dieser Migration ausgeschlossen. Die P3M-Beschreibung
bleibt ausschließlich als historische Planung für einen möglichen separaten späteren
Auftrag erhalten. Kein Ausführungs-Chat dieses Plans darf Dateien unter
`MoveMoatTest/`, seine Tests, gebauten Artefakte oder seine Version ändern oder bauen.

### Geprüfte Versionen

| Version | Commit |
|---|---|
| 1.42.0 | `171d68e155a8f98c5f8c4ee154d9af154c9a2443` |
| 1.45.0 | `fd18bf19bd8316204e2e4ad69caadd72563fe7e1` |
| 2.0.0 | `62ee6012e3b0b6d6bb31412de973b5c47d3b54de` |
| 2.0.2 | `6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79` |

Die verbindliche native Prüfbasis ist die aktuell installierte `CrusaderDE.dll` mit
SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Vor jeder Adress-, RVA-, Pattern- oder Strukturannahme müssen installierte DLL,
`_inspect/CrusaderDE-Native-Baseline/CURRENT.json` und der verwendete Datensatz erneut
denselben Hash ausweisen.

### Ausgangslage und korrigiertes Inventar

- Das nach den Moat-/Queue-/Move-Änderungen erneut geprüfte Workspace-Inventar basiert
  auf Commit `6d71e174a8d74457f8e12887d725601e1c91a3c3` (`586`). Gegenüber dem vorherigen
  Prüfstand änderte dieser Commit insbesondere
  `MoveMoatTest/MoatUnitBehaviorReverseEngineering.md`,
  `MoveMoatTest/src/MoveMoatPathTest.cs`, `MoveMoatTest/src/MoatWorkTargetSelection.cs`
  und die gebauten MoveMoat-Artefakte. Zum Prüfzeitpunkt existierten keine
  uncommitteten MoveMoat-Quelldateien. Spätere Chats müssen den dann aktuellen Stand
  dennoch als ausgeschlossenen Bestand zählen, diese Dateien unangetastet lassen und
  dürfen die Zahlen nicht als unveränderliche Vorgabe behandeln.
- Im ursprünglich geprüften Workspace wurden 55 Projektdateien inventarisiert. 37 davon
  referenzierten SHCDESE direkt oder kompilierten dagegen; 18 weitere waren indirekte
  Regressionstests, Hilfsprojekte oder die zu entfernende Linux-Bridge. Inzwischen sind
  `ChoreTestMod`, `MoatFillTargetTest` und `MPTest` gelöscht; die Moat-Fill-Funktion und
  ihr umbenanntes Testprojekt liegen jetzt in `BugfixesAndQoL`. Nach P8 verbleiben von
  diesen 52 Projektdateien 50, weil das Linux-Runtimeprojekt und sein DetourProbe
  entfernt wurden. Davon sind 49 Teil dieser Migration (33 mit direktem SHCDESE-Bezug
  und 16 indirekte Projekte); `MoveMoatTest.csproj` ist die eine ausdrücklich
  ausgeschlossene aktuelle Projektdatei. Die Matrix behält die drei entfernten
  Runtime-Projektzeilen sowie die zwei entfernten Linux-Projektzeilen als
  Stilllegungsnachweis und verwendet für den übernommenen Test den aktuellen Pfad.
- Das Ausgangsinventar besaß 29 Runtime-Plugins mit altem `LibraryLoaded`-Handler. Die
  drei gelöschten Diagnosemods entfallen; 26 verbleibende Handler werden migriert.
- Ursprünglich referenzierten 25 Projekte Zhuqiaomon. Nach Wegfall von
  MoatFillTargetTest sind es 24: zwölf mit echter Zhuqiaomon-Nutzung in weiterhin 60
  C#-Quelldateien und zwölf mit reiner Referenzbereinigung.
- Unabhängig davon verwenden zehn Quelldateien in sechs Projektwurzeln direkt
  PolyHook2-`NativeDetour`. Dazu gehören die neu hinzugekommenen Moat-Work-Detours in
  `BugfixesAndQoL/ImprovedMoatFillingFix` und
  `MoveMoatTest/MoatWorkTargetSelection`; auch diese Aufrufer müssen auf RedBird
  migriert werden. Projektdateireferenzen allein bilden dieses Inventar nicht korrekt
  ab.
- Im aktuellen Workspace stehen 36 Aufrufe von `HookTransaction.Unload()`. Die frühere
  Zahl 28 ist durch neu hinzugekommene Workspace-Arbeit überholt.
- `GetSelectedChimps()` wird achtmal direkt in sieben Quelldateien aufgerufen.
- 14 Runtime-Projekte kompilieren die gemeinsame
  `PresetLobbyModSettingsViewModel.cs`; die frühere Zahl 13 war unvollständig.

## 2. Kurzfazit zur Zielversion

2.0.2 ist gegenüber 1.42.0 die sinnvollere Zielbasis. Die große Umstrukturierung von
Zhuqiaomon auf RedBird ist kein bloßer Namespacewechsel, sondern ändert Konstruktion,
Handles, Scanner, Hookbesitz und Teardown-Verträge. Eine saubere Quellmigration ist
deshalb zwingend.

Die bei der Prüfung gefundenen ID-Regressions aus 2.0.0 betreffen
`TribeGetNextPatrolWaypointEventArgs.TribeId` und
`VegetationGrowthEventArgs.VegetationId`. Beide sind in 2.0.2 korrigiert. Kein aktueller
Workspace-Code abonniert diese Events. Die RedBird-Testbasis bestand aus 234 Tests:
228 erfolgreich, sechs absichtlich übersprungen und keine fehlgeschlagen. Der
2.0.2-Extender ließ sich als Managed-Projekt kompilieren; ein beobachteter
Solution-Postbuild-Fehler beruhte nur auf einem dort nicht gesetzten `SolutionDir`.

Davon getrennt ist `GameTribeManagerAPI.UnassignUnit` in 2.0.2 weiterhin fehlerhaft:
Der öffentliche Wrapper reicht `tribeId, unitId` an eine interne/native Signatur weiter,
die `unitId, tribeId` erwartet. Diese Regression ist nicht durch die vorgenannten
Tribe-Eventkorrekturen behoben und benötigt für das exakte Ziel 2.0.2 einen modseitigen
Workaround.

Für Linux liefert 2.0.2 den neuen Updater und seit 2.0.1 auch
`libredbird_thread_patch.so` im vorgesehenen Pluginverzeichnis. Der alte private
Linux-Updater-Hook aus dem Workspace ist daher nicht mehr zulässig oder erforderlich.

1.45.0 senkte außerdem den Extender-Default für `MaxGameSpeed` von 5000 auf 1500.
Eine bereits vorhandene BepInEx-Konfiguration kann den alten gespeicherten Wert
behalten. Das ist kein Quellcodebruch, muss aber bei der 2.0.2-Installation geprüft
werden; `BugfixesAndQoL/MultiplayerGameSpeedRuntime` verwendet bereits den tatsächlich
konfigurierten Extenderwert.

## 3. Referenzbasis zuerst herstellen

1. Den einzigen kanonischen lokalen Fork `shcde-script-extender` ausschließlich über
   dessen `update.bat` aktualisieren. Den Quellbaum nicht per ZIP, Kopie oder Robocopy
   ersetzen.
2. Sicherstellen, dass `upstream` auf Rawras Originalprojekt zeigt und der ausgecheckte
   Stand Tag 2.0.2 beziehungsweise Commit
   `6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79` entspricht.
3. Den Extender über seine eigene `build.bat` bauen. Danach Produktversion und Hash von
   `SHCDESE.dll` sowie das Vorhandensein dieser zentral ausgelieferten Komponenten
   prüfen:
   - `RedBird.Abstractions.dll`
   - `RedBird.Core.dll`
   - `RedBird.X64.dll`
   - `RedBird.Backends.NativeX64.dll`
   - `Microsoft.Extensions.Logging.Abstractions.dll`
   - unter Linux zusätzlich `libredbird_thread_patch.so`
4. Erst diese Ausgabe als Referenz für alle Modprojekte verwenden. Ein zufällig noch
   vorhandener alter oder neuerer `bin`-Ordner ist kein Versionsnachweis.
5. RedBird-DLLs nicht in Modpakete kopieren. Runtime-Auflösung und Backendkonfiguration
   gehören dem Extender; direkte Nutzer referenzieren nur die erforderlichen
   Compile-time-Assemblies aus der bestätigten 2.0.2-Ausgabe.
6. Nach der Installation `BepInEx/config/000shcdese.cfg` prüfen. Ein aus 1.42.0
   übernommenes `MaxGameSpeed=5000` bewusst auf höchstens 1500 migrieren, sofern der
   Benutzer nicht ausdrücklich entgegen der 2.0.2-Empfehlung einen anderen Wert
   verlangt. Die Datei nicht allein anhand des neuen Code-Defaults als migriert ansehen.

## 4. Öffentliche Vertragsänderungen

### 4.1 Mindestversion und Projektverweise

Jedes BepInEx-Plugin, das SHCDESE benötigt, erhält den eindeutigen Vertrag:

    [BepInDependency("000shcdese", "2.0.2")]

Der String-Konstruktor ist in der installierten BepInEx-Version der offizielle
Mindestversionsvertrag und impliziert eine Hard Dependency. Alte Attribute, die nur
`DependencyFlags.HardDependency` angeben, sind zu ersetzen. Bibliotheks-, Core- und
Testprojekte ohne Plugin-Klasse erhalten kein künstliches Attribut, müssen aber gegen
die 2.0.2-Assemblies kompilieren.

Direkte RedBird-Nutzer referenzieren abhängig von den tatsächlich verwendeten Typen
`RedBird.Abstractions`, `RedBird.Core` und `RedBird.X64`. Das native Backend wird nicht
direkt referenziert. Projekte, die `HookTransaction` konstruieren, benötigen außerdem
`Microsoft.Extensions.Logging.Abstractions`, weil dieser Typ Teil der öffentlichen
Konstruktorsignatur ist. Als LoggerFactory ist nach `LibraryLoaded` die bereits
initialisierte `SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory` verwendbar;
`null` bleibt zulässig, verzichtet aber auf RedBird-Diagnosen.

In Runtime-Projekten erhalten diese zentral vom Extender ausgelieferten Referenzen
`Private=false`; isolierte Testausgaben dürfen ihre benötigten Testkopien dagegen lokal
führen. So wird weder ein zweites RedBird-Backend noch eine abweichende Logging-
Assembly in ein Modpaket eingeschleust.

Zhuqiaomon-Referenzen und -Existenzprüfungen sowie PolyHook2-Referenzen ohne verbleibende
direkte Nutzung werden entfernt. Die zwölf als `bereinigen` klassifizierten Projekte
verwenden keine Zhuqiaomon-Typen im eigenen Quelltext; trotzdem ist vor dem Entfernen zu
prüfen, ob eine verwendete öffentliche SHCDESE-Signatur jetzt einen RedBird-Typ
exponiert und dadurch eine Compile-time-Referenz erfordert. Iced bleibt nur in
Projekten, deren eigener Code Iced weiterhin verwendet.

### 4.2 `CrusaderLibrary.LibraryLoaded`

Der alte Callback

    OnCrusaderLibraryLoaded(IntPtr moduleHandle, ReadOnlySpan<byte> memory)

wird in den 26 verbleibenden Runtime-Plugins durch einen Callback mit
`CrusaderLibraryLoadContext` ersetzt. Die drei übrigen Handler gehörten zu
ChoreTestMod, MoatFillTargetTest und MPTest; sie sind mit den obsoleten Diagnosemods
entfallen und werden nicht portiert.

- `context.ModuleHandle` ersetzt den alten Modulhandle.
- `context.Memory` ist die schreibgeschützte Speicheransicht für vorhandene reine
  Auswertungen.
- `context.CreateScanner()` ist der bevorzugte Einstieg für neue Scans.
- `context.Region` ist die zwingende Eingabe für RedBird-Hooks und -Transaktionen. Sie
  enthält den beim Laden erstellten stabilen Snapshot mit der echten Modulbasis; sie
  ist keine nachträglich live aktualisierte Kopie des Codes.
- Context und Region bleiben für die Lebensdauer der nativen Bibliothek gültig,
  gehören aber dem Extender und dürfen von Mods niemals disposed werden.
- Der obsolete RedBird-Konstruktor aus `memory` und Imagebase darf nicht als
  Kompatibilitätsabkürzung verwendet werden; er erzeugt eine Kopie und besitzt nicht
  den gewünschten Regionsvertrag.
- Späte Subscriber werden vom Extender unmittelbar aufgerufen. Ein anschließendes
  explizites `-=` ist zwar unschädlich, aber kein Ersatz für einen korrekten
  prozessweiten Runtime-Lebenszyklus.

Das 2.0.2-ExampleMod enthält noch die alte Signatur und ist an dieser Stelle veraltet.
Maßgeblich sind die kompilierten öffentlichen Typen und der aktuelle Extender-Code.

### 4.3 Zhuqiaomon auf RedBird

Die Migration erfolgt semantisch und je Hook, nicht über globale Textsubstitution.
Grundabbildungen:

| Zhuqiaomon | RedBird 2.0.2 |
|---|---|
| `HookRef<X64ManagedFunctionDetourAOB<T>>` | `DetourHandle<T>` |
| `HookRef<X64InlineHook>` | `HookHandle<X64InlineHook>` |
| `handle.Value.Hook.Trampoline` | `handle.Original` |
| `SimpleNativeArray<T>` | `RedBird.Core.Memory.SimpleNativeArray<T>` |
| `ManagedValue<T>` | `RedBird.Core.Memory.Managed.ManagedValue<T>` |
| `ManagedAssemblyImmediate<T>` | `RedBird.X64.Assembly.Stateful.ManagedAssemblyImmediate<T>` |
| `X64SmartCPUContextRegs` | `RedBird.X64.Assembly.X64SmartCPUContextRegs` |
| Zhuqiaomon-Scanner | `RedBird.Core.Memory.Scanners` und `RedBird.X64.Memory.Scanners` |
| Zhuqiaomon-Hooktransaktion | RedBird-Hooks und `HookTransaction` mit expliziten Optionen |

Alte `AddDetour(ref handle, address, callback)`- und Context-Hook-Aufrufe werden auf
vorab angelegte Handles und einen expliziten `HookTarget` umgestellt, normalerweise
`HookTarget.FromAddress(...)`, wenn `Shared.NativePatternResolver` die Adresse bereits
eindeutig aufgelöst hat. Nach `Commit()` sind sowohl das `CommitResult` als auch jedes
benötigte Handle (`Success`, `Failure`, `ResolvedAddress`) zu prüfen. Ein Detour darf
`Original` erst nach erfolgreichem Commit aufrufen; in fail-open Callbackpfaden ist
gegebenenfalls `TryGetOriginal` zu verwenden.

Direkte PolyHook2-`NativeDetour`-Aufrufer sind derselbe Migrationsfall und dürfen nicht
stehenbleiben, nur weil sie kein `using Zhuqiaomon` besitzen. Jeder manuell mit
`ManualApply`, `Apply`, `Undo` und `Dispose` verwaltete Detour wird in einen
`DetourHandle<T>` innerhalb einer RedBird-Transaktion überführt. Die bisherige
Alles-oder-nichts-Installation, Aufrufreihenfolge der Originalfunktion und der
Rollbackpfad bei einem teilweise fehlgeschlagenen Hooksatz bleiben erhalten.
Insbesondere gilt das für die zwei Standalone-Moat-Fill-Detours in BugfixesAndQoL und
die drei gemeinsamen Moat-Work- sowie die weiteren zentralen Detours in MoveMoatTest.

`X64ManagedFunctionAOB<T>` existiert nicht mehr. Solche Aufrufer verwenden einen
`DataScanner` mit kompiliertem Pattern und anschließend `TryGetFunction`, oder nach
eindeutiger Patternvalidierung `Marshal.GetDelegateForFunctionPointer`. Mehrdeutige
oder fehlende Treffer deaktivieren das Feature fail-closed.

Context-Hooks erhalten explizite `ContextHookOptions`, insbesondere dieselbe
Registermaske und Platzierung überschriebener Instruktionen wie vorher. Defaults dürfen
nicht stillschweigend den alten Maschinenvertrag ersetzen. Die in ImprovedHunters
verwendeten `ManagedAssemblyImmediate<short>`-Stellen besitzen keinen von null
abweichenden Offset; die geänderte RedBird-Vorzeichenkonvention erfordert dort keine
fachliche Wertinvertierung, aber Get/Set und Rückschreiben sind durch Tests zu belegen.

RedBirds `ManagedAssemblyImmediate<T>` validiert jetzt die codierte Operandenbreite,
behandelt `SetValue` als Legacy-Override und stellt bei `Dispose()` den Originalwert
wieder her. Die vom Extender gelieferte
`GameGlobalsManager.Instance.RabbitDespawnTickTime` darf die Mod deshalb nicht
disposen. Von ImprovedHunters selbst erzeugte Camel-/Chicken-Sites müssen dagegen bei
einem nachweislich echten finalen Feature-Teardown disposed oder explizit auf den
Originalwert zurückgesetzt werden, bevor ihre Referenz verworfen wird.

Die Namespace-Umstellung von `Zhuqiaomon.Extensions` auf
`RedBird.X64.Extensions` ist ebenfalls semantisch zu prüfen. RedBird entfernte unter
anderem `X64Fastcall`, `X64FastcallEx` und `X64FastcallSafeEx*` und änderte Stack-/XMM-
Details einiger verbliebener Hilfen. Die drei aktuellen Workspace-Importer verwenden
nur `AddUnrestrictedJmp`; dessen Maschinenbytes sind unverändert. Dieser Befund muss
vor der Umsetzung erneut gelten, statt alle Extension-Aufrufe blind umzubenennen.

### 4.4 Hookbesitz, `Unload()` und `Dispose()`

Zhuqiaomons `Unload()` deaktivierte installierte Hooks; Zhuqiaomons `Dispose()` baute
sie nicht automatisch ab. RedBird trennt Besitz, Aktivierung und Transaktionsobjekt
anders. Deshalb werden die 36 aktuellen `Unload()`-Aufrufe einzeln klassifiziert:

- Prozessweite Hooks: `OwnsHooks=false`, Runtime und erforderliche Handles bleiben
  gemäß ihrem tatsächlichen Zugriff verwurzelt. RedBird lässt die Hooks bei Dispose der
  Transaktion absichtlich weiterleben; ein Handle muss nur gehalten werden, wenn die Mod
  später `Original`, Status oder Einzelsteuerung benötigt. Kein Abbau in
  `BaseUnityPlugin.OnDestroy()`, weil dieses in SHCDE bereits während des normalen
  Starts ausgeführt wird.
- Endgültig feature-eigene Hooks: `OwnsHooks=true`; ein idempotentes `Dispose()` baut
  sie vollständig ab und ersetzt den bisherigen endgültigen `Unload(); Dispose()`-
  Pfad.
- Vorübergehend deaktivierbare Hooks: `DisableAll()` und später `EnableAll()` auf
  derselben lebenden Transaktion. Ein endgültiger Dispose erfolgt erst am tatsächlichen
  Lebensende des Features.
- Fehlgeschlagene Initialisierung: bisherige atomare Semantik bewahren. Wenn ein
  Feature alle Hooks benötigt, `FailureMode=RollbackAndThrow` beziehungsweise ein
  gleichwertiger expliziter Rollbackpfad; partielle Installation nur dort erlauben, wo
  der bestehende Featurevertrag sie nachweislich unterstützt.

Die 36 Stellen verteilen sich aktuell auf BugfixesAndQoL, ExtraFeatures,
ImprovedHunters und MoatCommandTest. Initialisierungs-Catchpfade und normale
Feature-Disposes dürfen nicht doppelt abbauen. Bei `OwnsHooks=false` darf insbesondere
nicht angenommen werden, das Verwerfen von Transaktion oder Handle deinstalliere den
Hook; dafür ist ein besessener Hook oder ein expliziter Disable-/Dispose-Pfad nötig.

### 4.5 Direkte Speicherschreibpfade

Acht Quellpfade verwenden Zhuqiaomon/Kernel32 für Seitenschutz und rohe Writes:

- BugfixesAndQoL: `AiStoneReserveFix`, `AssassinPathReconstructionPatch`,
  `AssemblyPointPlacementPatch`, `HealerAttackCommandPatch`,
  `LordControlGroupNativePatch`.
- ExtraFeatures: `PlagueDurationPatch`.
- ImprovedHunters: `AutomaticChickenTargetPatch`, `HunterHutVisibilityPatch`.

Wo der Patch als RedBird-Byte-/Assembly-Hook ausdrückbar ist, soll die Transaktion auch
den Patch besitzen und zurückrollen. Andernfalls sind RedBirds öffentlicher
`CodePatch.Read`/`CodePatch.Write`-Pfad zu verwenden; er stellt Seitenschutz auch bei
Exceptions wieder her und führt den erforderlichen Instruction-Cache-Flush aus. Ein
manueller `NativeMemoryManager.Protect`-Pfad ist nur zulässig, wenn `CodePatch` den
fachlichen Vorgang nachweislich nicht abbilden kann; dann müssen alter Schutzwert,
Wiederherstellung im `finally` und `InstructionCache.Flush` explizit geprüft werden.
`CodePatch.Write` suspendiert andere Threads nicht. Ausführbaren Code deshalb
bevorzugt innerhalb der RedBird-Transaktion installieren; ein roher Schreibpfad ist
nur an einem nachweislich sicheren Lifecycle-Punkt oder unter belegter eigener
Synchronisierung zulässig.
Für jeden Pfad bleiben Originalbytes, exakte Blocklänge, Endadresse, Schutzwiederher-
stellung und Rollback dokumentiert. Bei Maschinenblöcken sind Reads vor clobbernden
Writes, Register-Liveness, Stack, Flags, ABI-erhaltene Register und Rücksprungwert
statisch zu prüfen.

### 4.6 Weitere API-Brüche

#### Ausgewählte Einheiten

`GamePlayerManagerAPI.GetSelectedChimps()` liefert ab 1.45.0
`SelectedUnitInfo[]` statt `int[]`. Acht direkte Aufrufe in sieben Dateien sind
anzupassen:

- BugfixesAndQoL: `AssassinClimbRuntime`, `MountedStockpileMovementPatch`,
  `SiegeAmmoRestockFeature`.
- EnemyGatePathfindingTest: `SamePclBridgeDiagnostics`.
- ExtraFeatures: `KnightDismountRuntime` einschließlich seines sicheren Wrappers.
- MoveMoatTest: `MoveMoatPathTest`.
- QueueTest: `QueueRuntime`.

Nur `SelectedUnitInfo.UnitId` wird als 1-basierte Game-ID weitergegeben. `UnitType` darf
einen späteren Pointer-Lookup nur ersetzen, wenn Filterung und Verhalten identisch
bleiben. Arraypositionen sind niemals Unit-IDs.

Die neuen MoveMoat-/Queue-Pfade benötigen dabei eine gezielte Umsetzung statt einer
Typumbenennung: QueueTest iteriert `SelectedUnitInfo[]` und liest je Element `UnitId`.
MoveMoatTest besitzt zwei Aufrufe; `TryCaptureSelectedGroup` sortiert heute bewusst ein
`int[]` für eine deterministische Gruppensignatur. Dort zuerst die `UnitId`-Werte in ein
eigenes `int[]` projizieren und anschließend wie bisher numerisch sortieren; weder
`SelectedUnitInfo[]` direkt sortieren noch die bestehende deterministische Reihenfolge
unbeabsichtigt ändern.

Direkte Zugriffe auf Unity-/Spielzustände namens `state.selectedChimps`, etwa in
`LordUnitControlsFeature`, `TroopHudMiddleClickCameraFeature` und
`SelectedUnitHealthFeature`, gehören nicht zu dieser API-Änderung: Dort liegt weiterhin
das jeweils konkret geprüfte State-Array vor. Diese Stellen dürfen nicht durch eine
globale Suche mechanisch auf `SelectedUnitInfo` umgeschrieben werden.

#### Gold

`SetPlayerGold` und `GetPlayerGold` verwenden jetzt signierte `int`-Werte.
`StartConditions` entfernt den `(uint)`-Cast und validiert den fachlich zulässigen
Bereich vor dem Setzen. `BugfixesAndQoL/CtrlMarketTradeHook` stellt gespeicherte
Goldwerte, Kostenvergleiche und Zwischenrechnungen auf `int` um. Negative oder
überlaufende Ergebnisse dürfen keinen Kauf/Verkaufspfad freigeben.

#### Spielzeit

`CurrentDayReactive`, `CurrentMonthReactive`, `CurrentYearReactive` sowie die zugehörigen
Getter und Setter verwenden jetzt `uint`. Die zwei Getter-Aufrufe in RandomEvents
werden entweder im `uint`-Bereich weiterverarbeitet oder vor einer nötigen
`int`-Konvertierung explizit gegen `int.MaxValue` geprüft. Kein unchecked Wraparound.

#### ID-/Indexkorrekturen

`GatehouseQueryEventArgs.UnitId` ist in 2.0.2 bereits 1-basiert. BugfixesAndQoL und
ExtraFeatures dürfen `Shared/GatehouseQueryUnitIdPolicy.TryConvertSpanIndexToGameId`
nicht mehr darauf anwenden. Der Eventwert wird direkt als `unitId` verwendet; die
bisherige `ResolveCandidateDecision`-Logik zur Ermittlung der Vanilla-Entscheidung
bleibt erhalten oder wird ohne Indexkonvertierung passend umbenannt.

Die bis 1.44.0 nötigen Sonderkorrekturen für
`BuildingR3EventHooks.OnTogglePause` und
`BuildingExtensions.UpdateLocalGoodsResourceVisuals` sind ab 1.45.0 upstream behoben.
Im Workspace bestehen derzeit keine Aufrufer dieser Sonderpfade. Ebenso bestehen keine
Subscriber für die in 2.0.0 kurzzeitig fehlerhaften Tribe- und Vegetation-Events.

Der allgemeine Vertrag bleibt unverändert: Game-IDs sind 1-basiert, Span- und
Arrayindizes 0-basiert. Jede Grenze rechnet genau einmal `+1` beziehungsweise `-1`.

#### `GameTribeManagerAPI.UnassignUnit`

Der Fehler besteht im offiziellen Tag v2.0.2 fort. Geprüft wurden
`GameTribeManagerAPI.UnassignUnit(tribeId, unitId)` und
`BulkTribeDetours.c_game_tribe_remove_unit_hook_impl(manager, unitId, tribeId)` am
Commit `6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79`: Der Wrapper ruft die zweite Methode mit
`tribeId, unitId` auf und vertauscht damit die beiden Integerargumente.

- QueueTest verwendet bereits einen direkt und eindeutig aufgelösten nativen Delegate
  mit Vertrag `(manager, unitId, tribeId)`. Dieser Pfad bleibt für das Ziel 2.0.2
  erhalten, wird aber auf `CrusaderLibraryLoadContext.ModuleHandle`/`Memory` umgestellt
  und weiterhin gegen Pattern, RVA, Funktionsgrenze und Native-Hash abgesichert.
- AIDefense besitzt fünf direkte `UnassignUnit`-Aufrufe. Sie dürfen unter 2.0.2 nicht
  unverändert bleiben. AIDefense erhält einen eigenen, standalone-fähigen und
  fail-closed validierten nativen Adapter mit korrekter Argumentreihenfolge; QueueTest
  darf keine harte oder versteckte Laufzeitabhängigkeit werden.
- `UnassignUnitEx` ist eine andere, verwaltete Ersatzimplementierung und darf nicht ohne
  Nachweis identischer nativer Nebenwirkungen als mechanischer Ersatz eingesetzt
  werden.
- Der englische Upstream-Bugreport ist bereits erstellt und ist kein offener
  Migrationsschritt mehr. Es wird erwartet, dass 2.0.3 den Fehler behebt; das gilt aber
  erst nach Prüfung des tatsächlich veröffentlichten Tags als bestätigt. Dieser Plan
  bleibt bis zu einer ausdrücklichen Zieländerung bei exakt 2.0.2 und behält deshalb die
  nativen Adapter. Wird später 2.0.3 beauftragt und der Fix im Quellcode bestätigt,
  müssen Referenzbasis, Matrix und Tests gemeinsam neu bewertet und die Adapter auf den
  korrigierten öffentlichen Wrapper zurückgeführt werden.

### 4.7 Verbindliche Aktualisierungen an `AGENTS.md`

Die Migration muss die dauerhaften Arbeitsregeln an die nun bestätigten Verträge
anpassen; temporäre Inventarzahlen gehören dagegen nur in diesen Plan.

- Den bestehenden `UnassignUnit`-TODO ergänzen: Der Fehler ist nicht nur für 1.42.0,
  sondern nachweislich auch für exakt 2.0.2 vorhanden. QueueTest verwendet deshalb den
  direkten nativen Vertrag; AIDefense benötigt denselben semantischen Workaround.
- Den gemeinsamen Moat-Hookbesitz dokumentieren: Wenn MoveMoatTest installiert und
  seine Bridge bereit ist, besitzt MoveMoatTest die gemeinsamen Moat-Work-Hooks und
  BugfixesAndQoL liefert nur den Aktivierungsprovider. Ohne MoveMoatTest darf
  BugfixesAndQoL die zwei Standalone-Hooks installieren. Fehlende, inkompatible oder
  unbekannte Bridgezustände brechen fail-closed ab; niemals beide Hooksätze parallel.
- Festhalten, dass MoatFillTargetTest in BugfixesAndQoL aufgegangen ist und nicht mehr
  als eigenständige Mod, Abhängigkeit oder Lifecycle-Vorlage verwendet wird.
- Die Regeln erst nach dem jeweiligen Code-/Testpaket finalisieren, damit AGENTS.md den
  tatsächlich abgenommenen Endvertrag beschreibt. README-Dateien bleiben unverändert.

## 5. Settings, Chore-Transport und Modverwaltung

### 5.1 Zentralen Settings-Workaround entfernen

`Shared/PresetLobbyModSettingsViewModel.cs` enthält noch
`ScriptExtenderMultiplayerSyncWorkaround` und dessen `EnsureInstalled`-Aufruf. Die
zugrunde liegenden Sender-, Roster- und Syncfehler sind upstream behoben. Die komplette
Workaroundklasse und ihr Installationsaufruf werden entfernt; Presets, Rollenmodell,
Trail-Snapshots, `[SyncHostOnly]`, `[SyncPerPlayer]` und `[PresetLocal]` bleiben
unverändert.

Danach sind alle 14 einbindenden Runtime-Projekte neu zu prüfen und zu bauen:

`BugfixesAndQoL`, `BuildingCosts`, `BuildingLimit`, `CastlePlanner`, `CheatMod`,
`CustomCustomTrail`, `ExtraFeatures`, `ExtremePowers`, `ImprovedHunters`,
`RandomEvents`, `SerpsModsHost`, `StartConditions`, `UnitCosts` und `UnitLimit`.

Die bestehenden Preset-/Trail-Tests müssen weiterhin beweisen, dass empfangene Host-
und Trailwerte nicht in lokale Presets gelangen und dass persönliche Clientwerte nicht
von unvollständigen Snapshots zurückgesetzt werden.

### 5.2 Fail-closed Chore-Transport

Die öffentliche Klasse `ChoreNetworkTransport` wurde nach 1.42.0 entfernt.
`SendPacketToAllEx2(..., viaChore: true)` existiert in 2.0.2, fällt intern aber auf Steam
zurück, wenn kein ChoreManager vorhanden ist oder die Nutzlast zu groß ist. Für
simulationskritische Modaktionen wäre ein solcher stiller Transportwechsel falsch.

Jeder betroffene Sendehelper muss deshalb vor lokaler Mutation alle Bedingungen prüfen:

1. Der zugehörige Packet-Hook ist erfolgreich registriert.
2. Es wird genau ein danach unverändertes Packetobjekt erzeugt. Dieses wird einmal mit
   `GameNetworkAPI.Serialize(packet)` vorserialisiert, um den exakten Body und seine
   Größe zu prüfen.
3. `GameGlobalsManager.Instance.ChoreManagerVA != 0`.
4. Die exakte Gesamtgröße einschließlich zweibyte Packet-ID erfüllt
   `2 + body.Length <= 1200`.
5. Erst danach wird das ursprüngliche Packetobjekt mit
   `SendPacketToAllEx2(packet, packetId, viaChore: true)` aufgerufen. Niemals das
   vorserialisierte `byte[]` als `T` übergeben, weil die API es sonst nochmals als
   MessagePack-Bytearray codiert.
6. Bei fehlender Vorbedingung oder Exception erfolgen weder lokale Mutation noch ein
   expliziter Steam-Ersatzpfad.

`SendPacketToAllEx2<T>` serialisiert intern ein zweites Mal. Die Vorprüfung garantiert
für 2.0.2 nur dann denselben Grenzwert, wenn das Packetobjekt zwischen beiden
Serialisierungen unverändert bleibt und sein Formatter deterministisch ist. Der Helper
darf daher keine lazy, zeitabhängigen oder mutierbaren Formatterdaten verwenden. Diese
doppelte Serialisierung ist durch die derzeitige öffentliche API unvermeidbar; eine
wirklich rohe öffentliche Chore-Sendemethode existiert nicht.

Aktuell betroffen:

- BugfixesAndQoL: Assassin-Climb, Multiplayer-Spieltempo,
  Einzelgebäude-Pause, Siege-Ammo-Restock, Steinbruchhaufen-Verschiebung und
  Kapitulation.
- ExtraFeatures: Rittertransformation und Torautomatik.
- RandomEvents: Initialisierung, Eventbatches und Wegweiseraktionen; die vorhandene
  Ein-Chore-pro-Tick-Regel bleibt erhalten.
- ExtremePowers API: `ExtremePowerNetworkRuntime`.

ChoreTestMod gehört ausdrücklich nicht mehr zu den Transport-Abnahmetests: Der damit
untersuchte Upstream-Bug tritt nach bereits erfolgter Bestätigung nicht mehr auf. Der
Diagnosemod ist obsolet und wird aus aktivem Build, Installation und Lobbyvergleich
entfernt, ohne ihn auf 2.0.2 zu portieren. Die Chore-Grenztests werden in den Tests der
tatsächlich verbleibenden Aufrufer abgedeckt.

Keine Reflection auf interne Choremethoden, kein Aufruf nativer BulkChore-Wrapper,
keine Modkopie der entfernten Klasse und kein eigener Steam-Zieltick-Scheduler.

### 5.3 GUID-idempotente Assetregistrierung

`SerpsModsHost` registriert Child-Assetmods bereits per GUID, wertet danach aber noch
die gesamte Directoryliste aus. Nach jedem `RegisterAssetMod(directory)` wird
`TryGetRegisteredDirectory(mod.Guid, out registeredDirectory)` als autoritative
2.0.2-Abfrage verwendet.

- Fehlt die GUID, wird H004 ausgelöst.
- Zeigt die registrierte GUID auf einen anderen kanonischen, case-insensitiv
  verglichenen Pfad, wird H004 gemeldet und `registeredCount` nicht erhöht.
- Eine frühere Registrierung wird nicht durch einen späteren Child überschrieben.
- Der bestehende Dateisystem-Duplikatdetektor bleibt als ergänzende Diagnose erhalten.

### 5.4 Expliziter `NetworkMode`

Ohne Feld verwendet 2.0.2 `ModNetworkMode.Clientside` (`0`). Deshalb muss jede
maßgebliche, getrackte Quell- und Paketkopie von `info.json` explizit klassifiziert
werden. Generierte oder bereits installierte Kopien werden durch den vorgesehenen
Build neu erzeugt und nur verifiziert; ihre manuelle Bearbeitung ersetzt niemals die
Änderung der autoritativen Quelle.

`NetworkMode: 0` erhalten ausschließlich lokale, den Simulationszustand nicht
verändernde Werkzeuge:

- ActiveAIVDetector
- CustomLordUpload
- HunterQueryTargetDiagnostic
- VanillaAICExporter

`NetworkMode: 1` erhalten alle übrigen aktiven Runtime-, Gameplay-, Patch-, Netzwerk-
und Testmods, insbesondere APITest, SerpNativeAPI und TestMod LUA. Bereits
korrekt auf 1 gesetzte Testmods bleiben auf 1. Der stillgelegte ChoreTestMod erhält
keine neue Metadatenmigration; dasselbe gilt für MoatFillTargetTest und MPTest.
SerpsModsHost und seine gameplayrelevanten Childmods müssen gemeinsam konsistent sein.

Custom-Lord-Vorlagen wie `your-unique-lord-guid` sind keine veröffentlichte Workspace-
Runtime-Mod und werden nicht pauschal umgeschrieben. Ein daraus erstelltes Paket ist
bei Veröffentlichung nach seinem tatsächlichen Verhalten zu klassifizieren.

`VersionCheckUrl` und `WorkshopUrl` sind optional. Sie werden nur eingetragen, wenn ein
kanonischer öffentlicher GitHub-/GitLab-Release-Endpunkt beziehungsweise die echte
Steam-Workshop-Detailseite bekannt ist. Die Migration erfindet keine URLs und hängt
nicht von ihnen ab.

Die neue `GameAIManagerAPI.TryGetLordDetails`-Methode ist eine optionale Ergänzung und
kein Migrationszwang. Der aktuelle Workspace besitzt keinen direkten Aufrufer. Der im
alten 1.43.2-Plan erwähnte eigene `getComputerName`-/Subtype-Titelfix ist im aktuellen
Workspace-Quelltext nicht mehr vorhanden; daraus wird daher keine künstliche
Entfernungs- oder Ersatzaufgabe abgeleitet. CustomLordUpload und die vorhandenen
Custom-Lord-Listenfunktionen werden lediglich regressionsgeprüft.

### 5.5 Noesis-Binding

`INoesisElementBindingAware` ist eine sinnvolle optionale 2.0.2-API für ViewModels, die
Routed Events eines konkreten Noesis-Elements manuell verdrahten müssen. Sie wird nicht
pauschal in alle Preset-ViewModels eingebaut. Als separater, nicht migrationskritischer
Nachgang kann `SerpsModsHost` seine manuelle `PreviewKeyDown`-Verdrahtung darüber
recreation-sicher machen: altes Element abmelden, neues Element genau einmal anmelden.

## 6. LinuxModding

Der 2.0.2-Extender erkennt Wine, startet `data/mod-updater.sh`, übersetzt Pfade, wendet
Staging-/Delete-Manifeste an, wartet auf das Spielende und startet anschließend erneut.
Der bisherige Workspace-Hook auf die private Methode
`MapModManager.LaunchUpdaterAndExit` dupliziert damit offiziellen Code und bindet sich
an eine intern stark geänderte Implementierung.

Für die Migration gilt daher:

- `LinuxModding.dll`, sein C#-Projekt, `LinuxModdingPlugin`,
  `LinuxWorkshopUpdaterBridge`, der MonoMod-Detour und zugehörige Probe-/Requestdatei-
  Logik werden aus aktivem Paket und Build entfernt.
- Der eigene Staging-, Delete-, Warte- und Neustart-Loop entfällt vollständig.
- LinuxModding wird nicht länger als BepInEx-/Script-Extender-Mod gebaut, registriert
  oder im Lobbyvergleich geführt.
- Erhalten werden darf ein klar getrenntes, optionales Setup-/Launcher-Hilfsmittel,
  dessen einzige Laufzeitaufgabe das Setzen von
  `WINEDLLOVERRIDES=winhttp=n,b` vor dem normalen Spielstart ist.
- Das Installationsprüfskript darf BepInEx, SHCDESE 2.0.2, den offiziellen Shell-Updater
  und `libredbird_thread_patch.so` prüfen. Es darf keine private Extenderfunktion hooken
  und den offiziellen Updateablauf nicht ersetzen.

Obsolete Dateien sind erst bei der tatsächlichen Umsetzung nach einer eng begrenzten
Referenzsuche zu entfernen. Historische Analyseartefakte unter `_inspect` werden nicht
gelöscht.

## 7. Betroffenheitsmatrix

Legende: `direkt` bedeutet echte Zhuqiaomon- oder PolyHook-Quellmigration;
`bereinigen` bedeutet nur eine veraltete Projekt-/Existenzreferenz. `LL` steht für den neuen
`CrusaderLibraryLoadContext`. `Preset` bezeichnet den zentralen Settings-Workaround.

### 7.1 Projekte mit direktem SHCDESE-Bezug

| Projektdatei | Zielversion | LibraryLoaded | direkte RedBird-Migration | nur Referenzbereinigung | SelectedUnitInfo | Gold/Zeit/ID | Chore | Settings-Workaround | NetworkMode | Test-/Buildbedarf |
|---|---:|---:|---:|---:|---:|---|---|---:|---:|---|
| ActiveAIVDetector/ActiveAIVDetector.csproj | 2.0.2 | ja | ja | nein | nein | nein | nein | nein | 0 | Runtime, Pattern, Hook |
| AIDefense/AIDefense.csproj | 2.0.2 | ja | nein | nein | nein | UnassignUnit-Workaround | nein | nein | 1 | Runtime, Native, Gameplay |
| AIVParser/AIVParser.Tests/AIVParser.Tests.csproj | 2.0.2 | nein | nein | nein | nein | nein | nein | nein | n/a | Tests |
| APITest/APITest.csproj | 2.0.2 | nein | nein | nein | nein | nein | nein | nein | 1 | API-Runtime |
| BugfixesAndQoL/BugfixesAndQoL.csproj | 2.0.2 | ja | ja | nein | ja | Gold + Gatehouse-/Moat-IDs | ja | ja | 1 | vollständige Regression, Moat-Ownership |
| BuildingCosts/BuildingCosts.csproj | 2.0.2 | ja | nein | ja | nein | nein | nein | ja | 1 | Runtime, Presets |
| BuildingLimit/BuildingLimit.csproj | 2.0.2 | ja | nein | ja | nein | nein | nein | ja | 1 | Runtime, Presets |
| CastlePlanner/AIVPlacement.Tests/CastlePlanner.AIVPlacement.Tests.csproj | 2.0.2 | nein | nein | nein | nein | nein | nein | nein | n/a | Tests |
| CastlePlanner/CastlePlanner.csproj | 2.0.2 | ja | ja | nein | nein | nein | nein | ja | 1 | Runtime, Native, Presets |
| CheatMod/CheatMod.csproj | 2.0.2 | ja | nein | ja | nein | nein | nein | ja | 1 | Runtime, Presets |
| ChoreTestMod/ChoreTestMod.csproj | entfällt | nein (entfernen) | nein | nein | nein | nein | nein | nein | entfällt | obsoleten Diagnosemod stilllegen |
| CustomCustomTrail/CustomCustomTrail.csproj | 2.0.2 | ja | nein | nein | nein | Trail-Snapshot-IDs | nein | ja | 1 | Trail/Host/Client |
| CustomCustomTrail/CustomCustomTrail.Tests/CustomCustomTrail.Tests.csproj | 2.0.2 | nein | nein | nein | nein | Trail-Snapshot-IDs | nein | nein | n/a | Tests |
| CustomLordUpload/CustomLordUpload.csproj | 2.0.2 | nein | nein | nein | nein | Lord-ID-Regression | nein | nein | 0 | lokale Upload-UI |
| EnemyGatePathfindingTest/EnemyGatePathfindingTest.csproj | 2.0.2 | ja | ja | nein | ja | Gatehouse-Regression | nein | nein | 1 | Native/ID-Test |
| EngineerSiegeFix/EngineerSiegeFix.csproj | 2.0.2 | ja | nein | ja | nein | nein | nein | nein | 1 | Gameplay-Test |
| ExtraFeatures/ExtraFeatures.csproj | 2.0.2 | ja | ja | nein | ja | Gatehouse-ID | ja | ja | 1 | vollständige Regression |
| ExtremePowers/ExtremePowers.API.csproj | 2.0.2 | nein | nein | ja | nein | nein | ja | nein | n/a | API-/Chore-Tests |
| ExtremePowers/ExtremePowers.csproj | 2.0.2 | ja | nein | ja | nein | nein | indirekt | ja | 1 | Runtime, Presets |
| ExtremePowers/tests/ExtremePowers.ApiTests.csproj | 2.0.2 | nein | nein | nein | nein | nein | Chore-Tests | nein | n/a | Tests |
| HunterQueryTargetDiagnostic/HunterQueryTargetDiagnostic.csproj | 2.0.2 | ja | ja | nein | nein | nein | nein | nein | 0 | passive Runtime |
| ImprovedHunters/ImprovedHunters.csproj | 2.0.2 | ja | ja | nein | nein | nein | nein | ja | 1 | Native/Gameplay |
| MoatCommandTest/MoatCommandTest.csproj | 2.0.2 | ja | ja | nein | nein | nein | nein | nein | 1 | Native/Gameplay |
| MoatFillTargetTest/MoatFillTargetTest.csproj | entfällt | nein (entfernt) | nein | nein | nein | nach BugfixesAndQoL übernommen | nein | nein | entfällt | Stilllegung verifizieren |
| MoveMoatTest/MoveMoatTest.csproj | außerhalb dieses Plans | nicht prüfen | nicht migrieren | nein | nicht prüfen | nicht prüfen | nein | nein | unverändert | ausdrücklich ausgeschlossen; weder bauen noch testen |
| MPTest/MPTest.csproj | entfällt | nein (entfernt) | nein | nein | nein | nein | nein | nein | entfällt | Stilllegung verifizieren |
| OxTetherIdleFixTest/OxTetherIdleFixTest.csproj | 2.0.2 | ja | ja | nein | nein | nein | nein | nein | 1 | Native/Gameplay |
| QueueTest/QueueTest.csproj | 2.0.2 | ja | ja | nein | ja | Unit-/Tribe-ID + Unassign | nein | nein | 1 | Queue-/Native-Vertragstest |
| RandomEvents/RandomEvents.csproj | 2.0.2 | ja | nein | ja | nein | UInt32-Zeit | ja | ja | 1 | Events/Chore/Presets |
| SerpNativeAPI/SerpNativeAPI.csproj | 2.0.2 | ja | nein | nein | nein | nein | nein | nein | 1 | API-/Konflikttests |
| SerpsModsHost/SerpsModsHost.csproj | 2.0.2 | ja | nein | ja | nein | Asset-GUID/Pfad | nein | ja | 1 | Pack-/Duplikattests |
| StartConditions/StartConditions.csproj | 2.0.2 | ja | nein | ja | nein | signiertes Gold | nein | ja | 1 | Werte-/Presettests |
| StockpileAccessFixTest/StockpileAccessFixTest.csproj | 2.0.2 | ja | ja | nein | nein | nein | nein | nein | 1 | Native/Gameplay |
| TrailEditor/TrailEditor.Core/TrailEditor.Core.csproj | 2.0.2 | nein | nein | nein | nein | Trail-/ID-Regression | nein | nein | n/a | Core-Tests |
| UnitCosts/UnitCosts.csproj | 2.0.2 | ja | nein | ja | nein | nein | nein | ja | 1 | Runtime, Presets |
| UnitLimit/UnitLimit.csproj | 2.0.2 | ja | nein | ja | nein | nein | nein | ja | 1 | Runtime, Presets |
| VanillaAICExporter/VanillaAICExporter.csproj | 2.0.2 | ja | nein | ja | nein | AIC-ID-Regression | nein | nein | 0 | lokaler Export |

Die Matrix ist vor der Umsetzung erneut per `rg` gegen neue oder verschobene
Projektdateien zu prüfen. Neue Funde werden nach denselben Verträgen eingeordnet und
nicht stillschweigend ausgelassen.

### 7.2 Weitere Workspace-Projekte

Diese ursprünglich 18 Projektdateien besitzen keinen direkten SHCDESE-Verweis im Projekttext, sind
aber entweder Teil einer betroffenen Testkette, ein inzwischen entfernter Linux-Bestand oder
ein abschließend mitzuprüfendes abhängiges Werkzeug. Zusammen mit den 37 Zeilen aus
7.1 bilden die Tabellen 55 Verantwortungszeilen: 52 aktuelle Projektdateien und drei
stillgelegte Runtime-Projekte. Nach Entfernung der zwei Linux-Projekte verbleiben 16
aktuelle indirekte Projekte. Der umbenannte Moat-Test ersetzt dabei den historischen
Testpfad; er ist keine zusätzliche stillgelegte Projektzeile.

| Projektdatei | Zielversion | LibraryLoaded | direkte RedBird-Migration | nur Referenzbereinigung | SelectedUnitInfo | Gold/Zeit/ID | Chore | Settings-Workaround | NetworkMode | Test-/Buildbedarf |
|---|---:|---:|---:|---:|---:|---|---|---:|---:|---|
| AIVParser/AIVParser.Cli/AIVParser.Cli.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Parser-ID-Regression | nein | nein | n/a | CastlePlanner-Kette bauen/testen |
| AIVParser/AIVParser.Core/AIVParser.Core.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Parser-ID-Regression | nein | nein | n/a | CastlePlanner-Kette bauen/testen |
| AIVPlacement/AIVPlacement.Core/AIVPlacement.Core.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Placement-ID-Regression | nein | nein | n/a | Regression bauen/testen |
| AIVPlacement/AIVPlacement.OracleComparison/AIVPlacement.OracleComparison.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Placement-ID-Regression | nein | nein | n/a | Oracle-Regression testen |
| AIVPlacement/AIVPlacement.Tests/AIVPlacement.Tests.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Placement-ID-Regression | nein | nein | n/a | Tests ausführen |
| CastlePlanner/AIVPlacement.Core/CastlePlanner.AIVPlacement.Core.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Placement-ID-Regression | nein | nein | n/a | mit CastlePlanner prüfen |
| CustomCustomTrail/CustomCustomTrail.Core/CustomCustomTrail.Core.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Trail-Snapshot-IDs | nein | nein | n/a | mit Trailtests bauen |
| EnemyGatePathfindingTest/EnemyGatePathfindingTest.PolicyTests.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Gatehouse-ID | nein | nein | n/a | nach ID-Migration testen |
| LinuxModding/LinuxModding.csproj | entfällt | nein | nein | nein | nein | nein | nein | nein | entfällt | aktiven Plugin-Build entfernen |
| LinuxModding/tests/DetourProbe/DetourProbe.csproj | entfällt | nein | nein | nein | nein | nein | nein | nein | entfällt | mit privater Bridge stilllegen |
| MapParser/MapParser.Cli/MapParser.Cli.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Parser-ID-Regression | nein | nein | n/a | Regression bauen/testen |
| MapParser/MapParser.Core/MapParser.Core.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Parser-ID-Regression | nein | nein | n/a | Regression bauen/testen |
| MapParser/MapParser.Tests/MapParser.Tests.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Parser-ID-Regression | nein | nein | n/a | Tests ausführen |
| BugfixesAndQoL/tests/ImprovedMoatFilling.Tests.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Unit-/Moat-ID-Regression | nein | nein | n/a | Standalone-/MoveMoat-Ownership testen |
| OxTetherIdleFixTest/tests/OxTetherIdleFixTest.Tests.csproj | indirekt 2.0.2 | nein | nein | nein | nein | nein | nein | nein | n/a | RedBird-Modtests ausführen |
| QueueTest/tests/QueueTest.StaticTests.csproj | indirekt 2.0.2 | nein | nein | nein | ja | Unit-ID-Regression | nein | nein | n/a | statische Tests ausführen |
| TrailEditor/TrailEditor.Cli/TrailEditor.Cli.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Trail-/ID-Regression | nein | nein | n/a | Regression bauen/testen |
| TrailEditor/TrailEditor.Tests/TrailEditor.Tests.csproj | indirekt 2.0.2 | nein | nein | nein | nein | Trail-/ID-Regression | nein | nein | n/a | Decoder-Tests gegen 2.0.2 |

## 8. Aufteilung in eigenständige Ausführungs-Chats

Die Migration wird nicht als ein einziger Chat ausgeführt. Die folgenden Pakete sind
absichtlich nach gemeinsamer API, Überschneidungen und Buildbarkeit getrennt. Sie
werden in der angegebenen Reihenfolge bearbeitet; parallele Bearbeitung ist wegen der
gemeinsamen Dateien und der bereits vorhandenen Benutzeränderungen nicht zulässig.

### 8.1 Verbindliches Übergabeprotokoll

Jeder neue Chat muss vor seiner ersten Änderung:

1. `AGENTS.md` und dieses Dokument vollständig lesen.
2. `git status --short` prüfen und bestehende Änderungen als Benutzerarbeit behandeln.
3. Den Status und die Evidenz aller vorausgesetzten Pakete in der folgenden Tabelle
   prüfen. Eine bloß vorhandene Datei gilt nicht als Abschlussnachweis.
4. Die für sein Paket genannten `rg`-Inventare erneut erzeugen, weil sich der Workspace
   zwischen Chats ändern kann. Abweichende Zahlen im Übergabevermerk festhalten.
5. Die bestätigte Extender-Version und bei nativer Arbeit den aktuellen Native-Hash
   prüfen. Bei Hashabweichung keine alte RVA oder Bytefolge verwenden.

Jeder Chat bearbeitet grundsätzlich genau ein Paket. Am Ende trägt er in der
Statustabelle Status, Datum, tatsächlich geänderte Pfade, ausgeführte Prüfungen und
offene Restpunkte ein. `abgeschlossen` ist nur erlaubt, wenn alle Abnahmepunkte des
Pakets erfüllt sind. Bei einer fachlichen Blockade wird `blockiert` mit der exakten
Entscheidungsfrage eingetragen; der nächste Chat darf nicht darüber hinweggehen.

Mit ausdrücklicher Benutzerfreigabe vom 2026-09-05 sind tatsächliche manuelle
Spiel-, Host/Client- und Proton-Sitzungen optionale Nachabnahmen. Ihr Fehlen, ein
nicht erreichbarer zweiter PC oder ein fehlender später Runtime-Marker blockiert weder
ein Paket noch P9. Statische Prüfungen, automatisierte Testprogramme, Builds,
Installations- und Artefaktprüfungen bleiben verpflichtend. Wo der Plan im Folgenden
von Host/Client-, Runtime-, Spiel- oder Proton-Abnahme spricht, ist sie deshalb als
optionale manuelle Nachprüfung zu lesen, sofern nicht ausdrücklich eine automatisierte
Prüfung oder statische Evidenz genannt ist.

Erlaubte Statuswerte: `offen`, `in Arbeit`, `blockiert`, `zurückgestellt`, `entfällt`,
`abgeschlossen`. `entfällt` kennzeichnet einen ausdrücklich aus dem Zielumfang
genommenen Bestand und ist kein impliziter Arbeitsauftrag.

| Paket | Inhalt | Voraussetzung | Status | Evidenz/Übergabe |
|---|---|---|---|---|
| P0 | Extender- und Native-Basis | keine | abgeschlossen | 2026-09-05: Repo/Tag, lokaler Build, offizielles Releaseartefakt, Installation, RedBird/Linux-Dateien, Native-Hash und `MaxGameSpeed=1500` verifiziert; Details in `UpdateStatus-SHCDESE-2.0.2.md` |
| P1 | Shared Settings sowie Trail/Chore/Host-Gruppe | P0 | abgeschlossen | 2026-09-05: P1a–P1c, automatisierte Tests, Builds, Installation und Hashprüfungen abgeschlossen; echte Sitzungen sind gemäß Benutzerfreigabe optionale Nachabnahme; Details in `UpdateStatus-SHCDESE-2.0.2.md` |
| P2 | übrige Projekte ohne direkte Zhuq-Quellnutzung | P0, P1 | abgeschlossen | 2026-09-05: P2a–P2c, Fachtests, Builds, Installation, TrailEditor-Kette, LUA-Manifest und Stilllegungsnachweise vollständig abgeschlossen; Details in `UpdateStatus-SHCDESE-2.0.2.md` |
| P3 | kleinere direkte RedBird-Mods und Tests, ohne MoveMoatTest | P0 | abgeschlossen | 2026-09-05: P3a und P3b vollständig migriert, getestet, gebaut, installiert und hashgeprüft; Details in `UpdateStatus-SHCDESE-2.0.2.md` |
| P3M | MoveMoatTest (ausdrücklich ausgeschlossen) | entfällt | entfällt | Benutzerentscheidung 2026-09-05; Mod, Tests, Artefakte und Version nicht bearbeiten oder bauen |
| P4 | CastlePlanner und Parser-/Placement-Kette | P0, P1 | abgeschlossen | 2026-09-05: RedBird/LoadContext/Dependency/NetworkMode, vier Testketten, Build, Installation und Hashprüfung abgeschlossen; Details in `UpdateStatus-SHCDESE-2.0.2.md` |
| P5 | ImprovedHunters | P0, P1 | abgeschlossen | 2026-09-05: P5a-P5c, 19 Zhuq-Dateien, 16 Context-Hooks, sieben Teardowns, drei Stateful-Sites, zwei Memorypfade, Tests, Build, Installation und Hashprüfung abgeschlossen; Details in `UpdateStatus-SHCDESE-2.0.2.md` |
| P6 | BugfixesAndQoL, Moat-Fill und Gatehouse-Shared-Policy | P0, P1 | abgeschlossen | 2026-09-05: P6a-P6c, API-/ID-/Gold-/Speed-Verträge, 29 RedBird-Hooks, fünf Memorypfade, Moat-Ownership, sechs Chore-Sender, vollständige Regression, Build/Installation/Hashprüfung abgeschlossen; echte Sitzungen optional; Details in `UpdateStatus-SHCDESE-2.0.2.md` |
| P7 | ExtraFeatures | P0, P1, P6 | abgeschlossen | 2026-09-05: P7a-P7b, acht RedBird-Hooks, Plague-Memorypatch, SelectedUnitInfo, Gatehouse-ID, zwei Chore-Sender, vollständige Regression, Build/Installation/Hashprüfung abgeschlossen; echte Sitzungen optional; Details in `UpdateStatus-SHCDESE-2.0.2.md` |
| P8 | LinuxModding-Entkopplung | P0 | abgeschlossen | 2026-09-05: Runtime/Bridge/Probe/Paket/Build/Release entfernt; launcher-only Helfer, vier Tests und offizieller 2.0.2-Updaterpfad geprüft; echter Proton-Test optional; Details in `UpdateStatus-SHCDESE-2.0.2.md` |
| P9 | workspaceweite Endabnahme | P1–P8; P3M ist ausgenommen | abgeschlossen | 2026-09-05: 49/49 Projekte mit Buildnachweis, vollständige automatisierte Regression, 46 Manifeste, 27 installierte Pakete/1.402 Dateien, Abhängigkeiten, CRLF und kanonische Hashes geprüft; zwei P9-Funde behoben; Details in `UpdateStatus-SHCDESE-2.0.2.md` |

### 8.2 Paket P0: Referenzbasis

- Abschnitt 3 vollständig ausführen: Extender per `update.bat`, Tag/Commit bestätigen,
  Extender per `build.bat` bauen und Ausgaben einschließlich RedBird verifizieren.
- Installierte `CrusaderDE.dll` gegen `CURRENT.json` hashen.
- Installierte `000shcdese.cfg` einschließlich `MaxGameSpeed` prüfen.
- Keine Modquelle ändern.

Übergabe: Extender-Commit, SHCDESE-Assemblyversion/-hash, vorhandene RedBird-Dateien,
Native-Hash und effektiver MaxGameSpeed-Wert.

### 8.3 Paket P1: Shared Settings, Trail, Chore und Host

Zulässiger fachlicher Umfang:

- `Shared/PresetLobbyModSettingsViewModel.cs`: obsoleten Sync-Workaround entfernen.
- CustomCustomTrail samt Core/Tests, ExtremePowers samt API/Tests, RandomEvents,
  SerpsModsHost und StartConditions vollständig auf 2.0.2 migrieren.
- Die bereits als Benutzeränderung vorhandene Löschung von ChoreTestMod respektieren
  und nur bestätigen, dass aktiver Build, Installation und Lobbyvergleich keinen Rest
  mehr enthalten; den Mod weder wiederherstellen noch auf 2.0.2 portieren.
- Dabei LibraryLoaded, stale Zhuq-Referenzen, Chore-Helper, UInt32-Zeit, signiertes Gold,
  GUID-Verzeichnisprüfung, Dependencyattribute und NetworkMode dieses Pakets gemeinsam
  abschließen.
- Gemeinsame Preset-, Trail-, Chore-, Host- und Protokolltests vor den Builds ausführen.

Übergabe: entfernte Workaround-Symbole, Chore-Aufruferliste, Testresultate und je Mod
Build-/Installationsresultat. Noch nicht migrierte Presetmods werden ausdrücklich als
P5/P6/P7 beziehungsweise P4 zugeordnet und in P1 nicht gebaut.

### 8.4 Paket P2: Projekte ohne direkte Zhuq-Quellnutzung

Zulässiger Umfang:

- AIDefense, APITest, BuildingCosts, BuildingLimit, CheatMod, CustomLordUpload,
  EngineerSiegeFix, SerpNativeAPI, UnitCosts, UnitLimit und VanillaAICExporter.
- Pro Projekt LibraryLoaded, SHCDESE-Mindestversion, stale Zhuq-/PolyHook-Referenzen,
  öffentliche RedBird-Typabhängigkeiten, NetworkMode und Fachtests abschließen.
- In AIDefense die fünf fehlerhaft gewordenen `UnassignUnit`-Aufrufe über den
  validierten lokalen 2.0.2-Adapter führen und Erfolgs-, Fehler- sowie Rollbackpfade
  testen.
- Nach bestandener AIDefense-Abnahme den `UnassignUnit`-Vertrag in `AGENTS.md` wie in
  Abschnitt 4.7 beschrieben aktualisieren. Der bereits vorhandene Upstream-Bugreport
  wird nicht dupliziert; den Extender selbst nicht ändern.
- Die bereits gelöschten MoatFillTargetTest- und MPTest-Bestände nur als Stilllegung
  verifizieren; nichts daraus wiederherstellen oder gegen 2.0.2 bauen.
- TrailEditor.Core/-CLI/-Tests gehören eindeutig zu P2 und werden als indirekte
  Regressionskette gebaut beziehungsweise getestet. Ihr Quellcode wird ohne konkreten
  2.0.2-Befund nicht verändert. AIVParser gehört ausschließlich zu P4.
- Die maßgebliche `TestMod LUA`-Manifestkopie erhält hier `NetworkMode: 1`; sie besitzt
  keine Projektdatei und darf deshalb nicht aus der 55-Projekte-Matrix abgeleitet
  werden.

Übergabe: pro Projekt Referenzentscheidung, statische Negativsuche, Testresultat und
Build-/Installationsresultat.

### 8.5 Paket P3: kleinere direkte RedBird-Migrationen

Zulässiger Umfang:

- ActiveAIVDetector, EnemyGatePathfindingTest samt PolicyTests,
  HunterQueryTargetDiagnostic, MoatCommandTest, OxTetherIdleFixTest samt Tests,
  QueueTest samt StaticTests und
  StockpileAccessFixTest.
- Je Mod die vollständige Zhuq-/RedBird-, LoadContext-, Handle-, HookTarget-,
  SelectedUnitInfo-, Dependency- und NetworkMode-Migration durchführen.
- In QueueTest den direkten nativen Remove-Vertrag `(manager, unitId, tribeId)` samt
  Hash-, Pattern-, RVA-, Grenz- und Rollbacktests beibehalten; nicht auf den fehlerhaften
  2.0.2-Wrapper zurückwechseln. `QueueTest/NATIVE_CONTRACT.md` muss den bisher nur für
  1.42.0 genannten Wrapperbefund auf den bestätigten 2.0.2-Stand erweitern.
- Vorhandene Benutzerarbeit in QueueTest sowie die nach BugfixesAndQoL übernommene
  Moat-Fill-Implementierung nicht zurücksetzen oder durch ältere Analysefassungen
  ersetzen. MoveMoatTest gehört ausschließlich zu P3M und ist in P3 nicht anzufassen.

Übergabe: pro Hook Commit-/Handle-Prüfung, Besitzklassifikation, Patternnachweis,
Testresultat und Build-/Installationsresultat.

### 8.5a Paket P3M: MoveMoatTest (aus Zielumfang entfernt)

Dieses Paket ist nur historische Dokumentation für einen separaten späteren Auftrag.
Es ist kein Bestandteil oder Abschlusskriterium dieser Migration. Kein Chat darf es durch
das Bearbeiten eines anderen Pakets beiläufig beginnen. Insbesondere dürfen Quellcode,
Projektdatei, Dokumentation, gebaute DLL/PDB, installierte Ausgabe und Versionsangaben
von MoveMoatTest innerhalb dieses Plans nicht verändert, getestet oder gebaut werden.
Bei einem separaten späteren Auftrag ist zuerst der dann aktuelle Arbeitsbaum neu zu lesen;
später hinzugekommene Benutzeränderungen sind dann Teil der neuen Ausgangslage und
dürfen nicht durch den heute geprüften Commitstand ersetzt werden.

Nach einer solchen Freigabe gilt für die Migration:

- Sämtliche direkten PolyHook-Detours einschließlich der drei gemeinsamen Moat-Work-
  Hooks in einer atomaren RedBird-Migration erfassen. Die öffentliche
  `RegisterImprovedMoatFillingProvider`-Bridge und ihr Ergebnisvertrag `1/0` bleiben
  erhalten. Der statische Featurezustand darf MoveMoatTest erst nach vollständig
  erfolgreichem Commit als Hookbesitzer ausweisen; bei Teilfehlern bleiben weder Hooks
  noch ein scheinbar bereiter Bridgezustand zurück.
- Beide `GetSelectedChimps()`-Aufrufe auf `SelectedUnitInfo[]` umstellen. Für
  `TryCaptureSelectedGroup` zuerst ausschließlich die 1-basierten `UnitId`-Werte in
  ein eigenes `int[]` projizieren und dieses wie bisher numerisch sortieren. Dadurch
  bleiben Gruppensignatur und Auswahlreihenfolge deterministisch.
- Die neu hinzugekommenen Policies `GroundOnly`, `FriendlyOnly` und
  `AllowEnemyForDiagnostic` samt Besitzerprüfung unverändert trennen. Die RedBird-
  Migration darf weder feindliche Moat-Tiles in produktive Routen aufnehmen noch
  Diagnose- und Ground-only-Ergebnisse als Friendly-Routen wiederverwenden.
- Den synchronen, auswahlgebundenen Reachability-Cache um
  `EnsureMoatWorkReachability`/`TryGetMoatWorkRoute` erhalten: Map-Epoch, Tick,
  Tile-Manager, Spieler, Einheit, Startposition, Grid-Generation und Enemy-Route-Modus
  bleiben Bestandteil der Gültigkeit. Terrainänderungen sowie verschachtelte oder neue
  Selektionen invalidieren die Ergebnisse; Endpoint-Caches dürfen nicht zwischen
  Selektionen leaken.
- Den transaktionalen Vanilla-Fallback vollständig erhalten. Vor dem Retry werden der
  native Unit-Pfadpuffer einschließlich Originallänge sowie `routeVariant` und
  `moatPathMode` gesichert. Vertragsablehnung oder Exception stellt Puffer, Länge und
  beide Moduswerte wieder her und fällt ohne partiellen nativen Zustand auf Vanilla
  zurück. Beim Wechsel vom PolyHook-Trampolin zu RedBird `Original` muss dessen exakte
  Aufrufreihenfolge relativ zu Sicherung, temporären Writes, Validierung und Rollback
  unverändert bleiben.
- Die aktuellen Performance-/Diagnosezähler sind Beobachtungscode und dürfen den
  produktiven Kontrollfluss nicht übernehmen. Die jüngsten Änderungen fügen keine
  weiteren nativen Hookziele hinzu; Hook- und Projektinventare deshalb nicht allein
  wegen neuer Helper oder Caches erhöhen.

Abnahme nach Freigabe: RedBird-Commit und vollständiger Teilfehler-Rollback; erste und
letzte gültige IDs; leere, mehrfache und gemischte Selektion; Ground-only, freundliche,
feindliche und ungültige Besitzer; Cache-Wiederverwendung nur innerhalb derselben
gültigen Selektion; Invalidierung nach Tick/Terrain/Mapwechsel; Vertragsablehnung und
Exception mit bytegleichem Pfadpuffer, Originallänge, `routeVariant` und `moatPathMode`;
anschließend optionaler gemeinsamer Runtime-Test mit dem bereits migrierten
BugfixesAndQoL, bei dem pro Moat-Work-Zieladresse genau ein Besitzer aktiv ist.

Historische Übergabeanforderung für einen separaten Auftrag: neu erhobener
Arbeitsbaumstand, vollständige Detour-/Original-Aufrufmatrix,
Rollback- und Cachetests, kombinierter automatisierter Ownership-Test, eigener
Build/Installation und optionaler später Runtime-Marker. Ohne die verpflichtende
statische/automatisierte Übergabe wäre ein separater MoveMoat-Auftrag nicht abgeschlossen;
dies berührt den Abschluss dieses Plans nicht.

### 8.6 Paket P4: CastlePlanner

- CastlePlanner als eigenes Paket migrieren, einschließlich RedBird-Hook,
  LoadContext, Presetbasis, Dependency und NetworkMode.
- CastlePlanner.AIVPlacement.Core/-Tests sowie die AIVParser-, AIVPlacement- und
  MapParser-Abhängigkeitskette regressionsprüfen.
- Custom-Lord-Pfade und lokalisierte Anzeigen nur regressionsprüfen; der historische
  `getComputerName`-/Subtype-Arbeitspunkt besitzt keinen aktuellen Workspace-Aufrufer.

Übergabe: Pattern-/Hooknachweis, vollständige Placement-/Parser-Testresultate,
CastlePlanner-Build und optionaler später Runtime-Marker.

### 8.7 Paket P5: ImprovedHunters

- Alle 19 Zhuqiaomon-Dateien und sieben aktuellen Unload-Stellen geschlossen migrieren.
- Stateful-Immediates nach Besitzer trennen: Extender-eigenes Rabbit-Site nicht
  disposen, mod-eigene Camel-/Chicken-Sites beim echten Teardown restaurieren.
- Sämtliche Context-Hooks, direkten Memory-Patches und Feature-Disposes statisch sowie
  im Spiel prüfen.

Übergabe: Liste aller migrierten Hooks/Sites, Besitz- und Teardownentscheidung je
Feature, Patternnachweise, Tests, Build und optionaler Runtime-Marker.

### 8.8 Paket P6: BugfixesAndQoL

- Alle 20 Zhuqiaomon-Dateien, die zusätzlichen PolyHook-Detours der übernommenen
  `ImprovedMoatFillingFix` und aktuell 18 Unload-Stellen migrieren.
- SelectedUnitInfo, Gold, sechs Chore-Funktionen, fünf Memory-Schreibpfade,
  Multiplayer-Spieltempo, Gatehouse-ID und Moat-Fill gemeinsam abschließen.
- In P6 nur den aktuell zulässigen Stand abschließen: BugfixesAndQoL allein installiert
  genau seinen Standalone-Satz; eine fehlende oder inkompatible MoveMoat-Bridge bleibt
  ohne Doppelhook fail-closed. Bridge und Providervertrag bleiben für die spätere
  Kombination erhalten, ohne MoveMoatTest hierfür zu bearbeiten oder zu bauen.
- Der kombinierte Hookbesitz mit MoveMoatTest wird erst nach ausdrücklicher Freigabe in
  P3M getestet. Erst danach die kombinierte Moat-Ownership-Regel in `AGENTS.md` als
  abgenommen markieren. Die bereits belegte MoatFillTargetTest-Ablösung und der
  Standalone-Vertrag können in P6 dokumentiert werden.
- `Shared/GatehouseQueryUnitIdPolicy.cs` auf den 2.0.2-ID-Vertrag bringen; dies ist die
  Voraussetzung für P7.
- Den effektiven Extender-MaxGameSpeed verwenden und mit 1500 sowie einem bewusst
  abweichenden Konfigurationswert testen.

Übergabe: Hook-/Memory-/Chore-Matrix, ID- und Speedtests, vollständige Modtests, Build
sowie optionale Host-/Client- und Runtime-Nachabnahme.

### 8.9 Paket P7: ExtraFeatures

- Alle acht Zhuqiaomon-Dateien und aktuell zehn Unload-Stellen migrieren.
- SelectedUnitInfo, beide Chore-Funktionen, Gatehouse-ID und Plague-Memorypatch gegen
  die in P6 fertiggestellte Shared-Policy abschließen.
- Die Nutzer von `AddUnrestrictedJmp` gegen die unveränderten RedBird-Bytes prüfen;
  entfernte Fastcall-Hilfen nicht nachbauen, solange kein echter Aufrufer existiert.

Übergabe: Hook-/Memory-/Chore-Matrix, Gatehouse-Test, Presettests, Build sowie optionale
Host-/Client- und Runtime-Nachabnahme.

### 8.10 Paket P8: LinuxModding

- Abschnitt 6 vollständig umsetzen. Vor jeder Entfernung die exakten aktiven Paket-,
  Build- und Releaseziele auflisten und gegen den Workspace-Root auflösen.
- Private Bridge und DetourProbe gemeinsam stilllegen; optionale Launcher-/Checker-
  Funktion auf winhttp-Override und offizielle 2.0.2-Dateien begrenzen.
- Keine README ändern, solange der Benutzer dies nicht ausdrücklich erlaubt.

Übergabe: entfernte aktive Komponenten, verbliebene Hilfsskripte und statische Prüfung
des offiziellen Update-/Restartpfads; ein echter Proton-Test ist optionale Nachabnahme.

### 8.11 Paket P9: Workspaceweite Endabnahme

- Die Inventare aus Abschnitt 1 neu zählen; neue Funde einem abgeschlossenen Paket
  nachziehen, statt nur die alten Sollzahlen zu bestätigen.
- Alle 49 aktuellen Projektdateien im Zielumfang entweder erfolgreich bauen/testen oder
  als nachweislich nicht ausführbares Hilfsprojekt begründen. Die drei historischen
  Runtime-Zeilen ChoreTestMod, MoatFillTargetTest und MPTest, die zwei in P8 entfernten
  Linux-Projekte und das ausdrücklich ausgeschlossene `MoveMoatTest` werden nicht gegen
  2.0.2 gebaut oder getestet.
- Sämtliche Negativsuchen, CRLF-Prüfungen, Paket-/Manifestprüfungen und automatisierten
  Host-/Client-Vertragstests durchführen. Die echte Host-/Client-Sitzung bleibt eine
  optionale Nachabnahme und blockiert P9 nicht.
- Die ausdrückliche Benutzerentscheidung vom 2026-09-05 entfernt MoveMoatTest dauerhaft
  aus diesem Migrationsziel. Umfang, Inventare, Matrix, Paketstatus und Definition of
  Done sind gemeinsam angepasst; P3M blockiert P9 daher nicht.
- Bereits erfolgreich über `build.bat` installierte Mods nicht grundlos erneut bauen.
  Nur nach weiteren Codeänderungen ist ein erneuter Build erforderlich.
- Versionsnummern bleiben unverändert. Eine spätere finale Versionsanhebung ist ein
  eigener Release-Schritt nach ausdrücklicher Feststellung, dass die Migration final
  ist, und erfordert danach konsistente Neubuilds.

Übergabe: vollständige Definition-of-Done-Checkliste, verbleibende Risiken, Testlogs,
installierte Modversionen und klare Aussage, ob die 2.0.2-Migration abgeschlossen ist.

### 8.12 Empfohlene Grenzen bei Fortsetzung in mehreren Chats

Ein Hauptpaket darf mehrere Chats benötigen. Dann bearbeitet ein Chat genau einen der
folgenden Slices, setzt den Hauptpaketstatus auf `in Arbeit` und nennt den zuletzt
vollständig abgeschlossenen Slice in der Evidenzspalte. Ein angebrochener Slice wird im
nächsten Chat anhand dieser Evidenz fortgesetzt; Slices desselben Hauptpakets werden
nicht parallel bearbeitet. Der letzte Slice setzt das Hauptpaket erst nach dessen
vollständiger Abnahme auf `abgeschlossen`.

| Slice | Empfohlener Inhalt | Voraussetzung innerhalb des Pakets |
|---|---|---|
| P1a | Shared Settings-Workaround, CustomCustomTrail samt Core/Tests | P0 |
| P1b | ChoreTestMod-Stilllegung verifizieren; ExtremePowers und RandomEvents samt Chore-/Zeittests | P1a |
| P1c | SerpsModsHost, StartConditions und paketweite Preset-/Host-Abnahme | P1b |
| P2a | BuildingCosts, BuildingLimit, CheatMod, UnitCosts und UnitLimit | P1; abgeschlossen 2026-09-05 |
| P2b | AIDefense samt UnassignUnit-Adapter, APITest, CustomLordUpload, EngineerSiegeFix und VanillaAICExporter | P2a; abgeschlossen 2026-09-05 |
| P2c | SerpNativeAPI, TrailEditor-Kette, TestMod-LUA-Manifest und Stilllegungsnachweis für MoatFillTargetTest/MPTest | P2b; abgeschlossen 2026-09-05 |
| P3a | ActiveAIVDetector, EnemyGatePathfindingTest und HunterQueryTargetDiagnostic | P0; abgeschlossen 2026-09-05 |
| P3b | MoatCommandTest, OxTetherIdleFixTest, QueueTest samt nativem Remove-Vertrag und StockpileAccessFixTest | P3a; abgeschlossen 2026-09-05 |
| P3M-a | entfällt in diesem Plan; nur bei separatem Auftrag aktuellen MoveMoat-Arbeitsbaum neu inventarisieren | ausdrücklich außerhalb des Zielumfangs |
| P3M-b | entfällt in diesem Plan; historische Migrationsskizze | separater Auftrag |
| P3M-c | entfällt in diesem Plan; historische Migrationsskizze | separater Auftrag |
| P5a | gemeinsame RedBird-/Scanner-/Handle-Infrastruktur und Hookinventar | P1 |
| P5b | Stateful-Immediates, Memorypfade und Besitz-/Teardownmigration | P5a |
| P5c | statische Tests, Build, Installation und optionaler später Runtime-Marker | P5b |
| P6a | SelectedUnitInfo, Gold, Gatehouse-Shared-Policy, Speedvertrag und Standalone-Moat-Ownership | P1 |
| P6b | RedBird-Hooks, 18 Unload-Stellen, fünf Memory-Schreibpfade und Standalone-Moat-Detours | P6a |
| P6c | sechs Chore-Funktionen, vollständige Tests, Build und automatisierte Host/Client-Verträge; echte Sitzung optional | P6b |
| P7a | RedBird-Hooks, zehn Unload-Stellen und Plague-Memorypatch | P6; abgeschlossen 2026-09-05 |
| P7b | SelectedUnitInfo, Gatehouse-ID, beide Chore-Funktionen, Tests und Build | P7a; abgeschlossen 2026-09-05 |

P0, P4, P8 und P9 sind bereits eng genug geschnitten. Wenn auch dort ein Chatwechsel
nötig wird, wird der konkrete letzte abgeschlossene Abnahmepunkt statt eines künstlich
neu erfundenen Slices in der Evidenzspalte dokumentiert.

## 9. Technische Reihenfolge innerhalb der Pakete

### Schritt 1: Referenzen und Mindestversion

- Bestätigte 2.0.2-Ausgabe bereitstellen.
- Alle 33 aktuellen direkten SHCDESE-Projektdateien im Zielumfang prüfen; Zhuqiaomon entfernen,
  RedBird nur bei direkter Nutzung ergänzen und sonstige Low-Level-Abhängigkeiten auf
  echte Nutzung reduzieren. Die drei stillgelegten historischen Runtime-Projekte nur
  auf vollständige Entfernung prüfen; die ausgeschlossene 34. Projektdatei
  `MoveMoatTest.csproj` nicht bearbeiten, bauen oder testen.
- Alle Runtime-Plugins auf die Mindestversion 2.0.2 festlegen.
- Noch nicht bauen; zunächst sämtliche Quellmigrationen und statischen Prüfungen
  abschließen.

Abnahme: Keine Projektdatei verweist auf Zhuqiaomon oder eine andere Extender-Version;
alle Pluginabhängigkeiten sind eindeutig.

### Schritt 2: LoadContext und RedBird-Grundmigration

- Alle 25 im Zielumfang verbleibenden LibraryLoaded-Handler und weitergereichten
  Initialisierungssignaturen umstellen.
- Die elf im Zielumfang direkt betroffenen Projekte typweise auf RedBird migrieren;
  das zwölfte, `MoveMoatTest`, bleibt ausdrücklich ausgeschlossen.
- Scannerergebnisse, Hookoptionen, Handleverwurzelung und Fehlerpfade je Feature
  prüfen.
- Die acht direkten Memory-Patchpfade separat und mit Maschinenvertrag migrieren.

Abnahme: Kein `using Zhuqiaomon`, kein Zhuqiaomon-Typ und kein Hook aus einem
Memory-Snapshot; eindeutige Patternfehler deaktivieren das jeweilige Feature.

### Schritt 3: Hook-Lebenszyklen

- Alle `Unload()`-Stellen im Zielumfang nach prozessweit, endgültig feature-owned oder
  temporär schaltbar klassifizieren; die historische Gesamtzahl 36 schließt den
  unangetasteten MoveMoat-Bestand ein.
- RedBird-Besitzoption und Teardown entsprechend implementieren.
- Plugin-`OnDestroy()` statisch auf prozessweite `Dispose`, `-=`, Uninstall- oder
  Unload-Aufrufe prüfen.
- Für jede Runtime dokumentieren, wodurch sie nach Zerstörung der Plugin-Komponente
  verwurzelt bleibt.

Abnahme: Kein mechanischer Unload-Ersatz, kein Startup-Teardown und keine doppelten
Disposes; permanente und schaltbare Hooks verhalten sich in Tests unterschiedlich und
korrekt.

### Schritt 4: API- und ID-Migration

- SelectedUnitInfo, signiertes Gold, UInt32-Spielzeit und Gatehouse-IDs wie in Abschnitt
  4.6 umstellen.
- QueueTest behält seinen validierten direkten Remove-Delegate; AIDefense ersetzt alle
  fünf Aufrufe des fehlerhaften 2.0.2-`UnassignUnit`-Wrappers durch seinen eigenen
  validierten Adapter. Beide bleiben voneinander unabhängig.
- Versionsgebundene 1.42-Sonderkorrekturen entfernen, aber den allgemeinen
  ID-/Indexvertrag erhalten.
- Custom-Lord-/Kulturpfade als Regression prüfen. Da der im alten Plan genannte
  `getComputerName`-/Subtype-Fix im aktuellen Workspace nicht existiert, ist hier kein
  Code zu entfernen oder zu ersetzen.

Abnahme: Statische Suche und Fachtests zeigen keine falsche ID-Basis, keine unsigned
Goldsignatur und keinen unchecked Zeitüberlauf.

### Schritt 5: Settings, Chore und Host

- Zentralen Settings-Workaround entfernen und alle 14 Konsumenten testen.
- Jeden Chore-Aufrufer auf den vorgeprüften öffentlichen 2.0.2-Pfad migrieren.
- SerpsModsHost auf `TryGetRegisteredDirectory` umstellen und Duplikattests erweitern.
- `NetworkMode` in allen Quell-, Paket- und mitgeführten Manifestkopien konsistent
  setzen.

Abnahme: Kein `ChoreNetworkTransport`, kein alter Sync-Workaround, korrekte
GUID-Pfad-Diagnosen und keine implizit clientseitige Gameplay-Mod.

### Schritt 6: LinuxModding entkoppeln

- Vor der Entfernung alle Paket-, Build- und Releaseverweise auf den alten Bridge-Code
  bestimmen.
- Aktiven Plugin-/Updater-Ersatz entfernen; optionalen winhttp-Launcher und Checker als
  eigenständiges Hilfsmittel begrenzen.
- Offiziellen 2.0.2-Updater statisch prüfen; eine echte Proton-Sitzung ist optionale
  Nachabnahme.

Abnahme: Kein installierter Code hookt `MapModManager.LaunchUpdaterAndExit`; Updates
laufen über den offiziellen Shell-Updater und der Launcher setzt nur den notwendigen
Wine-Override.

### Schritt 7: Gesamtaudit und Builds

- Alle statischen und automatisierten Prüfungen abschließen.
- CRLF aller geänderten Textdateien sowie das Fehlen wörtlicher `\\r\\n`-Sequenzen
  kontrollieren.
- Versionen während Migration und Debugging unverändert lassen.
- Danach jede tatsächlich geänderte Runtime-Mod genau einmal über ihre eigene
  `build.bat` direkt aus PowerShell und erhöht bauen/installieren.
- Eine finale Versionsanhebung erst nach fachlicher Abnahme modweise atomar über
  Plugin-, Assembly-, Manifest-, Host-, Paket- und `info.json`-Versionen durchführen.

Abnahme: Alle betroffenen Projekte kompilieren gegen 2.0.2; Build- und installierte
Ausgaben enthalten keine Zhuqiaomon-DLL oder veraltete Extenderreferenz.

## 10. Statische und automatisierte Prüfungen

Vor dem ersten Mod-Build müssen Negativsuchen mindestens Folgendes bestätigen:

- kein `Zhuqiaomon` in Quellcode, Projektdateien, Buildlogik oder aktiven Paketen;
- kein direkter PolyHook2-`NativeDetour` und keine aktive PolyHook2-Referenz; alle
  tatsächlich benötigten Detours laufen über RedBird;
- kein LibraryLoaded-Handler mit `(IntPtr, ReadOnlySpan<byte>)`;
- kein `ChoreNetworkTransport`;
- keine `ScriptExtenderMultiplayerSyncWorkaround`-Klasse und kein `EnsureInstalled`;
- keine zusätzliche `+1`-Konvertierung von `GatehouseQueryEventArgs.UnitId`;
- kein `int[]` als Ergebnis von `GetSelectedChimps()`;
- keine nur per Flags deklarierte SHCDESE-Hard-Dependency;
- kein Plugin-`OnDestroy()` mit prozessweitem Hook-/Subscriptionabbau;
- kein aktives Linux-Bridge-Assembly und kein Hook auf die private Updatermethode.

Erforderliche Testgruppen:

1. RedBird-Hooktests: erfolgreicher Commit, teilweiser Fehler, atomarer Rollback,
   dauerhafte Hooks, endgültiges Dispose sowie Disable/Enable für echte Togglefälle.
   Zusätzlich die drei bisherigen `Zhuqiaomon.Extensions`-Importer prüfen; aktuell darf
   nur das byteidentische `AddUnrestrictedJmp` übrig bleiben.
2. Native Patternprüfung: genau ein Treffer im vorgesehenen PE-Bereich, gültige
   Zieladressen, unveränderte Strides/Offsets und fail-closed Verhalten.
   Für QueueTest und AIDefense zusätzlich den nativen Remove-Vertrag
   `(manager, unitId, tribeId)`, erste/letzte gültige IDs, vertauschte IDs,
   Teilfehler und Rollback prüfen.
3. SelectedUnitInfo: leere Auswahl, mehrere Einheiten, gemischte Typen, ungültige IDs
   und unveränderte Auswahlreihenfolge.
4. Gold: null, positive Grenzwerte, unzureichendes Gold, negative/überlaufende
   Zwischenwerte und identisches Handelsverhalten.
5. Zeit: normale Monate/Jahre und Werte oberhalb `int.MaxValue` ohne Wraparound.
   Für Stateful-Immediates zusätzlich Operandenbreitengrenzen, Originalwiederherstellung
   und getrennten Besitz der Rabbit-/Camel-/Chicken-Sites prüfen.
6. Gatehouse: erste und letzte gültige Unit-ID, verbündete/feindliche Gatehouses und
   kein benachbarter Slot durch Off-by-one.
   Moat-Fill zusätzlich allein mit BugfixesAndQoL, gemeinsam mit MoveMoatTest, mit
   fehlender/inkompatibler Bridge sowie mit einem von MoveMoat gemeldeten
   Installationsfehler prüfen; pro Zieladresse darf genau ein Hookbesitzer aktiv sein.
   Der Standalone-Teil gehört zu P6; alle Prüfungen, die MoveMoatTest ausführen oder
   verändern, bleiben bis zur ausdrücklichen Freigabe P3M vorbehalten. Für MoveMoat
   zusätzlich die drei Traversal-Policies, Besitzergrenzen, Cache-Gültigkeit und den
   vollständigen Fallback-Rollback von Pfadpuffer/-länge, `routeVariant` und
   `moatPathMode` prüfen.
7. Chore: fehlender Manager, fehlender Packet-Hook, Serialisierungsfehler, Gesamtgröße
   1199, exakt 1200 und 1201 Bytes, pausierte Simulation und keine lokale Mutation bei
   Ablehnung. Im Test dasselbe unveränderte Packetobjekt zweimal mit
   `GameNetworkAPI.Serialize` serialisieren und beide Bodies byteweise vergleichen;
   die zweite, API-interne Serialisierung ist öffentlich nicht auslesbar. Bei nicht
   deterministischem Formatter den Sendepfad fail-closed deaktivieren.
8. Settings: Host/Client-Sync, Per-Player-Werte, Trail-Snapshotwechsel, Roster-
   Neuzuordnung, Join/Leave sowie keine lokale Persistenz empfangener Hostwerte.
9. Assetregistrierung: gleiche GUID/gleicher Pfad, gleiche GUID/anderer Pfad,
   case-variierter Pfad und späterer Child ohne Überschreiben der ersten Registrierung.
10. NetworkMode: identische Modlisten, nur Mode-0-Abweichung, Mode-1-Version fehlt oder
    weicht ab sowie SerpsModsHost mit seinen Childmods.
11. Linux/Proton: winhttp-Override, Thread-Patch-Datei und offizielles Staging/Delete
    statisch beziehungsweise automatisiert prüfen; Spielende und Neustart ohne
    Workspace-Bridge sind optionale manuelle Nachabnahme.

Wenn eine optionale Laufzeitnachprüfung durchgeführt wird, endet ihr Nachweis nicht bei
einem READY-Log: Nach dem normalen Startup-Cleanup soll je betroffener Runtime
mindestens ein späterer Karten-, Tick- oder Render-Marker erscheinen. Chore- und
Settings-Nachabnahme soll dann mit echtem Host und Client erfolgen. Fehlende manuelle
Sitzungsevidenz verhindert weder Paketabschluss noch die Definition of Done.

## 11. Definition of Done

Die Migration ist erst abgeschlossen, wenn:

- die ausdrückliche Benutzerentscheidung vom 2026-09-05 dokumentiert ist, durch die
  MoveMoatTest aus dem Zielumfang entfernt wurde und unangetastet bleibt;
- alle 33 aktuellen direkt SHCDESE-bezogenen und alle 16 weiteren Projektdateien im
  Zielumfang gegen den bestätigten 2.0.2-Vertrag geprüft und gebaut/getestet sind; die
  drei historischen Runtime-Projekte ChoreTestMod, MoatFillTargetTest und MPTest sowie
  die zwei Linux-Projekte sind nachvollziehbar stillgelegt;
- jedes verbleibende Runtime-Plugin eine Mindestabhängigkeit auf 2.0.2 besitzt;
- keine Zhuqiaomon-Abhängigkeit oder -API und kein direkter PolyHook2-Detour mehr
  vorhanden ist;
- alle RedBird-Hooks einen expliziten Besitz-, Fehler- und Lebenszyklusvertrag haben;
- Context und Region des Extenders nirgends von einer Mod disposed werden;
- SelectedUnitInfo, Gold, Zeit und alle ID-Grenzen fachlich korrekt migriert sind;
- QueueTest und AIDefense den bestätigt fehlerhaften 2.0.2-`UnassignUnit`-Wrapper nicht
  verwenden und `AGENTS.md` diesen versionsgebundenen Vertrag korrekt wiedergibt;
- BugfixesAndQoL im Zielumfang ausschließlich seinen geprüften Standalone-Moat-Hooksatz
  besitzt und bei fehlender, inkompatibler oder fehlerhaft meldender MoveMoat-Bridge
  ohne Doppelhook fail-closed bleibt; eine kombinierte MoveMoat-Laufzeitabnahme gehört
  nicht zu diesem Plan;
- Chore-Aktionen bei jeder fehlenden Vorbedingung ohne Mutation abbrechen und keinen
  unbemerkten Steam-Fallback auslösen;
- der obsolete Settings-Workaround entfernt ist und alle 14 Presetmods die gemeinsamen
  Tests bestehen;
- alle Manifeste einen korrekten expliziten NetworkMode besitzen;
- SerpsModsHost den tatsächlich registrierten GUID-Pfad autoritativ prüft;
- der alte Linux-Updater-Hook nicht mehr installiert oder gebaut wird;
- native Pattern und Maschinenverträge gegen die kanonische DLL belegt sind;
- geänderte Textdateien CRLF verwenden und alle vorgesehenen Builds sowie statischen
  und automatisierten Prüfungen erfolgreich sind; optionale manuelle Spiel-,
  Host/Client- und Proton-Sitzungen sind kein Abschlusskriterium;
- finale Versionsstände, sofern nach Abnahme erhöht, innerhalb jeder Mod atomar und
  ohne unbeabsichtigte alte aktive Versionsangabe konsistent sind.

## 12. Bewusst nicht geplante Änderungen

- Keine Änderung am kanonischen Script-Extender-Quellbaum und kein eigener Fix-Fork
  innerhalb der Mods.
- Keine Rückwärtskompatibilität zu 1.42.0, 1.45.0 oder 2.0.0.
- Keine Reflection- oder native Kopie entfernter Chore-Interna.
- Kein ungeprüfter Steam-Ersatz für simulationskritische Chores.
- Kein pauschaler Abbau prozessweiter Ressourcen in Plugin-`OnDestroy()`.
- Keine pauschale Einführung von `INoesisElementBindingAware`.
- Keine erfundenen Release- oder Workshop-URLs.
- Keine ungeprüfte Entfernung robuster GameMode-, Roster-, Trail- oder Custom-Lord-
  Logik nur deshalb, weil 2.0.2 neue Hilfs-APIs besitzt.
- Keine Persistenzmigration weg vom gemeinsamen Preset-/Trailformat.
- Kein Versionsbump während offener Tests und kein Build vor Abschluss der statischen
  Codekontrollen.
- Keine Änderung oder Löschung bestehender Analyseartefakte und Benutzerarbeit.
