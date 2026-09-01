# SerpNativeAPI

SerpNativeAPI stellt Mods typisierte, gemeinsam verwaltete Fähigkeiten für native SHCDE-Funktionen bereit. Verbraucher erhalten keine beliebigen Speicheradressen, Scanner, Zielpointer oder Detour-Objekte. Native Versionsprüfung, Besitz, Konflikterkennung, Mutation und Diagnosen bleiben in der API.

Aktuelle Moddaten:

- BepInEx-GUID: `SerpNativeAPI_Serp`
- Version: `0.1.0`
- Ziel-Framework: .NET Framework 4.8.1
- harte Laufzeitabhängigkeit: Script Extender `000shcdese`

## Installation und Projektreferenz

SerpNativeAPI muss als eigener BepInEx-Mod installiert sein. Ein Verbrauchermod deklariert beide harten Abhängigkeiten:

    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("SerpNativeAPI_Serp", BepInDependency.DependencyFlags.HardDependency)]

Die Assemblyreferenz darf nicht privat in den Verbraucherordner kopiert werden:

    <PropertyGroup>
      <SerpNativeApiPath>$(GameDir)\BepInEx\plugins\SerpNativeAPI_Serp\SerpNativeAPI.dll</SerpNativeApiPath>
    </PropertyGroup>
    <ItemGroup>
      <Reference Include="SerpNativeAPI">
        <HintPath>$(SerpNativeApiPath)</HintPath>
        <Private>false</Private>
      </Reference>
    </ItemGroup>
    <Target Name="ValidateSerpNativeApi" BeforeTargets="BeforeBuild">
      <Error Condition="!Exists('$(SerpNativeApiPath)')" Text="SerpNativeAPI.dll wurde nicht gefunden: $(SerpNativeApiPath)" />
    </Target>

Bei einer Workspace-Referenz kann der `HintPath` stattdessen auf `SerpNativeAPI/BepInEx/plugins/SerpNativeAPI_Serp/SerpNativeAPI.dll` zeigen. Auch dann bleibt `<Private>false>` zwingend. Im installierten Verbraucherordner darf keine zweite API-DLL liegen.

## Readiness und Diagnosen

Native Initialisierung endet erst mit dem `CrusaderLibrary.LibraryLoaded`-Ereignis. Ein Verbraucher soll daher `WhenReady` verwenden:

    private void Awake()
    {
        SerpNativeApi.WhenReady(OnNativeApiReady);
    }

    private void OnNativeApiReady(ISerpNativeApi api)
    {
        if (api.State != NativeApiState.Ready)
        {
            Logger.LogError("SerpNativeAPI ist global nicht verfügbar.");
            return;
        }

        // Capabilities werden anschließend unabhängig angefordert.
    }

`SerpNativeApi.Current` ist für Statusabfragen verfügbar, ersetzt während `Pending` aber nicht die Readiness-Registrierung. `WhenReady` ruft spät registrierte Callbacks unmittelbar auf.

Jede `TryGet...`- und Mutationsmethode liefert ein `NativeCapabilityDiagnostic` mit:

- `CapabilityId`
- `State`
- vollständigem `BinaryHash`
- verständlicher `Reason`
- optionalem `ConflictOwnerGuid`

`UnsupportedBuild` bedeutet, dass nur die hashgebundene Capability nicht unterstützt wird. Andere, beispielsweise durch ein Script-Extender-Event bereitgestellte Capabilities können trotzdem `Available` sein. `Unavailable` ist globalen Initialisierungsfehlern vorbehalten. Diagnosen sollten mit Zeitstempel, Besitzer-GUID und vollständigem Hash geloggt werden.

Als `ownerGuid` wird immer die stabile BepInEx-GUID des Verbrauchermods übergeben. Sie steuert Idempotenz, deterministische Callbackreihenfolge und Konfliktdiagnosen; Anzeigenamen oder zufällige Werte sind ungeeignet.

## Gatehouse Timing verwenden

    if (!api.TryGetGatehouseTiming(
            PluginGuid,
            out IGatehouseTimingCapability gatehouse,
            out NativeCapabilityDiagnostic diagnostic))
    {
        LogDiagnostic(diagnostic);
        return;
    }

    var settings = new GatehouseTimingSettings(
        enabled: true,
        humanReopenDelaySeconds: 0.0,
        aiReopenDelaySeconds: 0.0,
        humanCloseDistanceTiles: 5.0,
        aiCloseDistanceTiles: 5.0);

    if (!gatehouse.TryApply(settings, out diagnostic))
        LogDiagnostic(diagnostic);

Die API rundet mit `MidpointRounding.AwayFromZero`, verwendet 40 Ticks pro Sekunde und acht native Einheiten pro Feld und prüft zusätzlich den nativen `UInt16`-Wertebereich. Die vier Immediates werden gemeinsam validiert, geschrieben und verifiziert. Bei Teilfehlern erfolgt ein Rollback.

`Enabled=false` ignoriert die übrigen Werte des Settings-Objekts und stellt die katalogisierten Vanilla-Werte wieder her. Der einmal erworbene Prozessbesitz wird dabei absichtlich nicht freigegeben. Ein anderer Besitzer erhält bei überlappender exklusiver Mutation `Conflict`.

Wichtig: Die derzeitige Capability verändert nur Distanzen und Wiederöffnungszeiten. Vanilla misst die Distanz noch von der Begin-Koordinate des Gebäudes. Der geplante Mittelpunkt-Fix und seine offene Analyse stehen in [TODOGatehouse.md](TODOGatehouse.md).

## Selected Unit Command verwenden

Das Handle muss für die gewünschte Lebensdauer verwurzelt bleiben, üblicherweise in einem statischen Feld. Es darf nicht in `OnDisable` oder `OnDestroy` der kurzlebigen BepInEx-Komponente entsorgt werden.

    private static ISelectedUnitCommandRegistration selectedCommandRegistration;

    private void RegisterSelectedCommand(ISerpNativeApi api)
    {
        if (!api.TryGetSelectedUnitCommand(
                PluginGuid,
                out ISelectedUnitCommandCapability capability,
                out NativeCapabilityDiagnostic diagnostic))
        {
            LogDiagnostic(diagnostic);
            return;
        }

        if (!capability.TryRegisterBefore(
                OnSelectedUnitCommand,
                out selectedCommandRegistration,
                out diagnostic))
        {
            LogDiagnostic(diagnostic);
        }
    }

    private static void OnSelectedUnitCommand(SelectedUnitCommandContext context)
    {
        if (context.Command != TribeAICommand.UnitStop)
            return;

        // TribeId, TargetValue1, TargetValue2 und Argument6 auswerten.
    }

Pro Besitzer-GUID gibt es höchstens eine idempotente Registrierung. Eine spätere Registrierung desselben Besitzers liefert dasselbe Handle; sie ersetzt den ursprünglichen Callback nicht. `Disable()` pausiert, `Enable()` aktiviert wieder und `Dispose()` entfernt die Registrierung dauerhaft. Nach `Dispose()` kann derselbe Besitzer neu registrieren.

Die API vermittelt ausschließlich `EventHookPhase.Pre` aus `TribeR3EventHooks.OnTribeIssueOrderWithTarget`. Verbraucher erhalten einen unveränderlichen Snapshot und nie die veränderlichen EventArgs. Callbackfehler werden je Besitzer isoliert; weitere Callbacks laufen in ordinaler Reihenfolge der Besitzer-GUID weiter. SerpNativeAPI setzt weder `SkipOriginalFunction` noch Argumente oder Rückgabewerte. Direkte fremde Abonnenten des zugrunde liegenden Extender-Events liegen außerhalb dieser Garantie.

## Eine neue Capability hinzufügen

Jede fachliche API gehört in eine eigene Datei unter `src`. Zusammengehörige Operationen wie Get/Set oder Registrierung/Handle bleiben gemeinsam. Eine Capability-Datei enthält möglichst vollständig:

- ihre öffentlichen Settings-, Kontext-, Handle- und Capability-Verträge;
- den spezifischen Extender-Adapter oder den kompilierten Native-Zielkatalog;
- Auflösung und Validierung;
- Besitzer-/Broker- oder Mutationslogik;
- capability-spezifische Diagnosen.

`Contracts.cs` bleibt auf gemeinsame Zustände, Diagnosen, IDs und `ISerpNativeApi` beschränkt. `SerpNativeApiRuntime.cs` koordiniert nur Initialisierung, Readiness und Veröffentlichung. Allgemeine PE-, Speicher-, Seitenschutz-, Besitz- und Logging-Helfer gehören in `NativeInfrastructure.cs`. Eine Capability darf keine Implementierungsdetails einer anderen Capability voraussetzen.

Vorgehen für eine Erweiterung:

1. Zuerst `_inspect/CrusaderDE-Native-Baseline/CURRENT.md` und `CURRENT.json` lesen und den Hash der installierten kanonischen DLL vergleichen.
2. Prüfen, ob der Script Extender bereits eine passende typisierte API oder ein Event besitzt. Diese Oberfläche ist einem zusätzlichen nativen Hook vorzuziehen.
3. Bei nativen Zielen Funktion, RVA, Grenzen, Section, Bytes, semantische Invarianten und betroffene halboffene Intervalle für genau den bestätigten vollständigen DLL-Hash katalogisieren. Unbekannte Builds mutieren nichts. Keine unbeschränkten AOB-Fallbacks einführen.
4. Nur fachlich typisierte Verträge veröffentlichen. Keine Speicheradressen, Pointer, Scanner, rohen Delegates, Trampolines, Seitenschutz- oder Detour-Objekte an Verbraucher geben.
5. Capabilities unabhängig initialisieren. Ein lokaler Fehler darf andere Capabilities nicht deaktivieren; globale Veröffentlichung allein darf `NativeApiState.Unavailable` erzeugen.
6. Native Intervalle vor Mutation reservieren. Wiederholungen desselben Besitzers sind idempotent, fremde Überschneidungen scheitern geschlossen mit Besitzerdiagnose.
7. Zusammengehörige Writes transaktional ausführen: erwarteten Zustand prüfen, alte Werte und jeden Seitenschutz sichern, gemeinsam schreiben und verifizieren, vollständig zurückrollen, Schutzwerte einzeln restaurieren und den Instruction Cache leeren. Primär- und Cleanupfehler gemeinsam melden.
8. Dauerhafte Events, Delegates, Trampolines und Subscriptions statisch oder anderweitig für den Prozess verwurzeln. Nicht auf `OnDisable`, `OnDestroy`, `Update` oder Coroutines der BepInEx-Plugininstanz vertrauen.
9. Fake-Adapter und Tests unter `_inspect/SerpNativeAPITests` ergänzen. Mindestens unbekannte Builds, unabhängige Fehler, Konflikte, Idempotenz, externe Mutation, Rollback, Cleanupfehler, Reentranz und Callbackfehler abdecken.
10. `SerpNativeAPI/_inspect/native-surface-audit.csv`, `ARCHITECTURE.md` und bei offenen Analysen eine eigene TODO-Datei aktualisieren. Erst nach statischen Prüfungen und Tests den vorgesehenen `build.bat`-Treiber einmal ausführen.

## Projektunterlagen

- [ARCHITECTURE.md](ARCHITECTURE.md): kompakte Architektur und Sicherheitsgrenzen
- [TODOGatehouse.md](TODOGatehouse.md): offene Mittelpunktanalyse
- [_inspect/HANDOFF.md](_inspect/HANDOFF.md): implementierter Übergabestand
- [_inspect/native-surface-audit.csv](_inspect/native-surface-audit.csv): weitere Migrationskandidaten

Vor Version 1.0 sind die Verträge primär für die Workspace-Mods bestimmt; Änderungen sollen dennoch bewusst, typisiert und in allen Verbrauchern atomar erfolgen.
