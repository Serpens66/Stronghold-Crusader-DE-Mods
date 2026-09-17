# APIShared

APIShared ist ein eigenständiger BepInEx-Mod, der gemeinsam genutzte SHCDE-Funktionen als typisierte, prozessweite API bereitstellt. Mehrere Mods können dadurch dieselben Hooks, Beobachter und nativen Änderungen verwenden, ohne konkurrierende Implementierungen zu installieren.

Aktueller Stand:

- Version: `0.3.6`
- BepInEx-GUID: `APIShared_Serp`
- Assembly und Namespace: `APIShared`
- Ziel-Framework: .NET Framework 4.8.1
- Laufzeitabhängigkeit: Script Extender `000shcdese` ab Version `2.3.0`

## Installation

APIShared wird genau einmal als eigener Mod installiert:

```text
BepInEx/
└── plugins/
    ├── 000shcdese/
    └── APIShared_Serp/
        ├── APIShared.dll
        ├── APIShared.xml
        └── ...
```

Für einzeln installierte Mods wird APIShared separat über den GitHub-Release [`APIShared/v0.3.6`](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/APIShared%2Fv0.3.6) installiert. Ein Verbrauchermod liefert **keine eigene Kopie von `APIShared.dll`** mit. Alle Verbrauchermods referenzieren dieselbe zentral installierte Assembly.

Das SerpsMods-Workshop-Modpack ist davon ausgenommen: Es enthält bereits genau eine interne APIShared-Kopie. Neben dem Modpack dürfen weder APIShared noch einzelne darin enthaltene Serps-Mods separat installiert werden.

Mehrere Kopien sind auch dann zu vermeiden, wenn BepInEx oder der Script Extender doppelte GUIDs erkennt. Die Auswahlmechanismen garantieren bei unterschiedlich verschachtelten Paketen nicht gemeinsam, dass Plugin-DLL und zugehörige Assets aus derselben Version stammen. Insbesondere gilt nicht, dass der zuletzt entpackte Stand automatisch verwendet wird.

## In ein eigenes Projekt einbinden

### BepInEx-Abhängigkeiten

Der Verbrauchermod deklariert Script Extender und APIShared als harte Abhängigkeiten. Die Versionsnummer bei APIShared sollte der mindestens benötigten API-Version entsprechen:

```csharp
using BepInEx;

[BepInDependency("000shcdese", "2.3.0")]
[BepInDependency("APIShared_Serp", "0.3.6")]
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class MyPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "MyMod.Author";
    public const string PluginName = "My Mod";
    public const string PluginVersion = "1.0.0";
}
```

### Projektverweis

Die Projektdatei verweist auf die zentral installierte Assembly. `<Private>false</Private>` ist entscheidend: MSBuild darf `APIShared.dll` nicht in den Ausgabeordner des Verbrauchermods kopieren.

```xml
<PropertyGroup Condition="'$(GameDir)' == ''">
  <GameDir>E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition</GameDir>
</PropertyGroup>

<PropertyGroup Condition="'$(ApiSharedDir)' == ''">
  <ApiSharedDir>$(GameDir)\BepInEx\plugins\APIShared_Serp</ApiSharedDir>
</PropertyGroup>

<ItemGroup>
  <Reference Include="APIShared">
    <HintPath>$(ApiSharedDir)\APIShared.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>

<Target Name="ValidateApiSharedReference" BeforeTargets="BeforeBuild">
  <Error Condition="!Exists('$(ApiSharedDir)\APIShared.dll')"
         Text="APIShared.dll wurde nicht gefunden: $(ApiSharedDir)" />
</Target>
```

Für lokale Entwicklung darf `ApiSharedDir` auf den Ausgabeordner des APIShared-Projekts zeigen. Auch dabei bleibt `Private` auf `false`. Das Releasepaket des Verbrauchermods darf weder `APIShared.dll` noch den Ordner `APIShared_Serp` enthalten; APIShared wird separat installiert und aktualisiert. In den Release-Hinweisen sollte die mindestens benötigte APIShared-Version mit einem direkten Link auf den dazugehörigen APIShared-Release genannt werden.

## API verwenden

Alle Capabilities werden mit der stabilen BepInEx-GUID des Verbrauchermods angefordert. Diese GUID identifiziert den Besitzer von Registrierungen und exklusiven nativen Änderungen. Anzeigenamen, Assemblynamen oder zufällige Werte sind dafür ungeeignet.

Die verwalteten Capabilities für Missions-Lebenszyklus, Lobby-Zustand und Spieler-Niederlagen werden bereits beim Laden von APIShared initialisiert. Capabilities, die von der nativen Spielbibliothek abhängen, stehen erst nach deren Initialisierung bereit. `ApiShared.WhenReady` ist deshalb der einfachste gemeinsame Einstieg:

```csharp
using APIShared;

private void Awake()
{
    ApiShared.WhenReady(OnApiReady);
}

private void OnApiReady(IApiShared api)
{
    if (api.State != NativeApiState.Ready)
    {
        Logger.LogError("APIShared konnte nicht vollständig initialisiert werden.");
        return;
    }

    if (!api.TryGetGatehouseTiming(
            PluginGuid,
            out IGatehouseTimingCapability capability,
            out NativeCapabilityDiagnostic diagnostic))
    {
        LogDiagnostic(diagnostic);
        return;
    }

    // capability verwenden
}

private void LogDiagnostic(NativeCapabilityDiagnostic diagnostic)
{
    Logger.LogError(
        $"APIShared: capability={diagnostic?.CapabilityId}, " +
        $"state={diagnostic?.State}, hash={diagnostic?.BinaryHash}, " +
        $"conflictOwner={diagnostic?.ConflictOwnerGuid}, reason={diagnostic?.Reason}");
}
```

`ApiShared.Current` liefert die prozessweite API-Instanz direkt. Bei einer späten Registrierung ruft `WhenReady` den Callback unmittelbar auf.

Jeder fehlgeschlagene Erwerb und jede fehlgeschlagene Mutation liefert ein `NativeCapabilityDiagnostic`. Die wichtigsten Zustände sind:

- `Available`: Die Capability kann verwendet werden.
- `Pending`: Die erforderliche Initialisierung ist noch nicht abgeschlossen.
- `UnsupportedBuild`: Diese Capability unterstützt die installierte native Spielversion nicht.
- `PatternMissing`, `Ambiguous` oder `ValidationFailed`: Das benötigte Ziel konnte nicht sicher bestimmt oder validiert werden.
- `Conflict`: Eine Registrierung oder ein exklusives natives Ziel wird bereits von einem anderen Besitzer verwendet.
- `Faulted`: Bei der Initialisierung der Capability ist ein unerwarteter Fehler aufgetreten.

Capabilities werden unabhängig voneinander initialisiert. Eine nicht verfügbare native Capability bedeutet daher nicht automatisch, dass auch alle anderen Funktionen ausgefallen sind.

## Enthaltene Capabilities

### Missions-Lebenszyklus (`mission-lifecycle`)

`IMissionLifecycleCapability` veröffentlicht den Lebenszyklus interaktiver Missionen:

- Initialisierungsphasen vor und nach nativen beziehungsweise verwalteten Ladeschritten;
- einen erfolgreichen Missionsstart;
- das Ende oder den Abbruch einer Mission mit Endgrund;
- `Current` als Kontext der aktuell vollständig gestarteten Mission.

`MissionContext` enthält unter anderem eine prozesslokale Session-ID, Startart, Spielmodus, Kartenpfad und -name, Kartenparameter, lokalen Spieler, Hoststatus und Kartentyp. Fehlende Informationen werden als `null` beziehungsweise `Unknown` geliefert und nicht aus einer vorherigen Mission übernommen.

Registrierungen müssen auf dem Unity-Thread mit einer stabilen, pro Besitzer eindeutigen `registrationId` erfolgen. Ein bereits laufender erfolgreicher Start wird einem späten Beobachter einmal mit `IsReplay=true` zugestellt; Initialisierungsereignisse werden nicht nachträglich wiederholt.

```csharp
if (!ApiShared.Current.TryGetMissionLifecycle(
        PluginGuid,
        out IMissionLifecycleCapability lifecycle,
        out NativeCapabilityDiagnostic diagnostic) ||
    !lifecycle.TryRegisterObserver(
        "main",
        onStart: OnMissionStart,
        onEnd: OnMissionEnd,
        onInitialization: OnMissionInitialization,
        diagnostic: out diagnostic))
{
    LogDiagnostic(diagnostic);
}
```

### Lobby-Zustand (`lobby-state`)

`ILobbyStateCapability` stellt unveränderliche Snapshots der aktuellen Multiplayer-Lobby bereit. Ein `LobbyStateSnapshot` enthält:

- die Steam-Lobby-ID;
- die Zuordnung einbasierter Spielerslots zu Steam-IDs;
- den einbasierten lokalen Spielerslot;
- noch nicht aufgelöste Spieleridentitäten;
- Fehler- und Diagnoseinformationen;
- die Information, ob der letzte Lobbyzustand während eines Kartenwechsels erhalten bleibt.

Beobachter werden mit einer besitzerlokalen `registrationId` für die Prozesslaufzeit registriert. Ist bereits ein Snapshot bekannt, erhält ein neuer Beobachter ihn unmittelbar.

### Spieler-Niederlagen (`player-defeat`)

`IPlayerDefeatCapability` veröffentlicht zwei voneinander getrennte Zustandsübergänge:

- `PlayerLordDeathNotification`, wenn ein zuvor bestätigt lebender Lord verschwindet oder stirbt;
- `PlayerDefeatNotification`, wenn Vanilla den Spieler offiziell in den Niederlagenstatus versetzt.

Die Meldungen enthalten Session-ID, einbasierte Spieler-ID und Simulationstick; die Lord-Meldung enthält zusätzlich die einbasierte Unit-ID und die globale Identität des zuletzt bestätigten Lords. Bereits bestehende Zustände werden bei der Registrierung nicht nachträglich veröffentlicht. Mindestens einer der beiden Callbacks muss angegeben werden.

### Gatehouse-Distanzursprung (`gatehouse-distance-origin`)

`IGatehouseDistanceOriginCapability` wählt den Ursprung für gegnerische Distanzprüfungen an Torhäusern:

- `VanillaBuildingBegin`: Vanillas Eckkoordinate;
- `BuildingBoundsCenter`: der Mittelpunkt der vollständigen Gebäudegrenzen.

Die Einstellung gilt prozessweit. APIShared validiert und übernimmt die zugehörige native Änderung exklusiv für die angegebene Besitzer-GUID.

### Gatehouse-Zeiten und -Distanzen (`gatehouse-timing`)

`IGatehouseTimingCapability` setzt vier Torhauswerte gemeinsam und transaktional:

- Wiederöffnungsverzögerung für menschliche Spieler: `0` bis `30` Sekunden;
- Wiederöffnungsverzögerung für KI-Spieler: `0` bis `120` Sekunden;
- Schließdistanz für Menschen und KI: jeweils `5` bis `50` Felder.

Die unterstützten Grenzen und Vanilla-Werte stehen in `GatehouseTimingValues`. `Enabled=false` stellt die vier Vanilla-Werte wieder her; der separat verwaltete Distanzursprung bleibt unverändert.

```csharp
var settings = new GatehouseTimingSettings(
    enabled: true,
    humanReopenDelaySeconds: 0.0,
    aiReopenDelaySeconds: 10.0,
    humanCloseDistanceTiles: 12.5,
    aiCloseDistanceTiles: 20.0);

if (!capability.TryApply(settings, out NativeCapabilityDiagnostic diagnostic))
    LogDiagnostic(diagnostic);
```

### Unit-HUD-Präsentation (`unit-hud-presentation`)

`IUnitHudPresentationCapability` bündelt gemeinsam nutzbare Darstellung und Interaktion für besondere Einheitenkategorien. Verbrauchermods können:

- Kategorien anhand validierter `UnitHudUnitSnapshot`-Objekte registrieren;
- Kategorien auf Truppenauswahl, Kontrollgruppen, Hovertext, Armeebericht, Rekrutierung und Einheitendetails anzeigen;
- lokalisierte Namen, Kurztexte und Beschreibungen mit sicheren Fallbacks liefern;
- eigene Bilder und Farbtönungen für Kategorien setzen;
- deterministische Bild-Overrides für die unterstützten `UnitHudImageSlot`-Werte registrieren;
- Mausinteraktionen mit Kategorien beobachten;
- Rekrutierungsanforderungen über Tickets annehmen und nach der Zuordnung erzeugter Einheiten abschließen;
- sichtbare Truppenslots, ausgewählte Kategorien und die zehn Vanilla-Kontrollgruppen abfragen;
- eine einbasierte Unit-ID sicher aus allen Kontrollgruppen entfernen;
- eine verzögerte, Unity-thread-sichere HUD-Aktualisierung anfordern.

Registrierungen gelten für die Prozesslaufzeit. Die Capability implementiert zusätzlich `IUnitHudActivationCapability`; damit lassen sich alle Registrierungen eines Besitzers oder einzelne Kategorien und Bild-Overrides vorübergehend aktivieren beziehungsweise deaktivieren.

```csharp
var category = new UnitHudCategoryDefinition(
    categoryId: "my-elite-archer",
    displayName: "Elite Archer",
    baseUnitType: archerType,
    surfaces: UnitHudSurface.All,
    imageResolver: ResolveIcon,
    tint: new UnitHudTint(255, 220, 120, 96),
    order: 0,
    textProfile: new UnitHudTextProfile(
        "Elite Archer",
        "EA",
        "A specialized archer.",
        ResolveLocalizedText));

if (!hud.TryRegisterCategory(category, MatchesEliteArcher, out diagnostic))
    LogDiagnostic(diagnostic);

((IUnitHudActivationCapability)hud).SetOwnerActive(true);
hud.RequestRefresh();
```

Die derzeitige Rekrutierungsintegration unterstützt Kategorien mit dem europäischen Bogenschützen als Vanilla-Basistyp. Ein `UnitHudRecruitmentHandler` entscheidet vor Vanillas Rekrutierungsaktion, ob er ein Ticket übernimmt; ein übernommenes Ticket muss anschließend mit `TryCompleteRecruitment` abgeschlossen werden.

### AIV-Bauschritt (`aiv-build-step`)

`IAivBuildStepCapability` vermittelt Beobachter für den prozessweiten AIV-Bauschritt. `IAivBuildStepObserver.TryBegin` wird vor dem unveränderten Vanilla-Aufruf ausgeführt und kann ein aufrufsbezogenes `IAivBuildStepInvocation` zurückgeben. Dessen `Complete`-Methode erhält nach Vanilla:

- denselben unveränderlichen Aufrufkontext;
- die Information, ob Vanilla normal beendet wurde;
- Vanillas unveränderten Rückgabewert oder `0`, falls der Aufruf nicht abgeschlossen wurde.

Mehrere Beobachter werden deterministisch gestartet und in umgekehrter Reihenfolge abgeschlossen. APIShared führt Vanilla dabei genau einmal aus. Eine identische Registrierung ist idempotent; dieselbe Besitzer-/Registrierungs-ID darf nicht für einen anderen Beobachter wiederverwendet werden.

## Gemeinsame Spielmodus-Erkennung

Neben den Capabilities enthält die Assembly im Namespace `Shared` eine zentrale Spielmodusauswertung:

```csharp
using Shared;

GameModeSnapshot mode = GameModeHelper.Capture();

if (mode.IsRealMultiplayer)
    Logger.LogInfo("Eine echte Multiplayer-Partie ist aktiv.");

Logger.LogInfo(mode.ToDiagnosticString());
```

`GameModeSnapshot` unterscheidet unter anderem Karteneditor, Kampagne, Einzelmission, freie Partie, Vanilla-, benutzerdefinierte und Koop-Kreuzzüge, Sands of Time, Tutorial sowie angepasste und aus Spielständen wiederhergestellte Starts. Er enthält außerdem die zugrunde liegenden Lobby-, Netzwerk-, Kampagnen- und Trail-Indizien.

`GameplayModModePolicy` und `GameplayFeatureModePolicy` enthalten Profile für fest katalogisierte Serps-Mod-GUIDs und deren Features. Sie sind keine allgemeine Registrierungs- oder Erweiterungsoberfläche für fremde Mods: Unbekannte Mod-GUIDs beziehungsweise nicht zugeordnete Kombinationen werden abgelehnt. Drittanbieter sollten `GameModeSnapshot` auswerten und ihre eigene Modusregel ausdrücklich definieren.

## Lebensdauer und Fehlerbehandlung

- Registrierungen und Hooks sind grundsätzlich für die gesamte Prozesslaufzeit ausgelegt.
- Verwendete Capability-Objekte, Observer und Callbacks sollten entsprechend langlebig gehalten werden.
- Besitzer- und Registrierungs-IDs müssen stabil und eindeutig sein.
- Callbackfehler eines Verbrauchers werden, soweit der jeweilige Broker dies unterstützt, von anderen Verbrauchern isoliert. Eigene Callbacks sollten trotzdem keine Ausnahmen nach außen geben.
- Native Capabilities arbeiten fail-closed: Bei unbekannter Spielversion, uneindeutigem Ziel, Validierungsfehler oder Konflikt erfolgt keine unsichere Mutation.
- Vollständige Diagnosen sollten mit Capability-ID, Zustand, Grund, Konfliktbesitzer und vollständigem Binary-Hash protokolliert werden.

Die mitgelieferte `APIShared.xml` enthält die XML-Dokumentation der öffentlichen Typen, Methoden, Eigenschaften und Enum-Werte und kann von IDEs direkt neben `APIShared.dll` verwendet werden.
