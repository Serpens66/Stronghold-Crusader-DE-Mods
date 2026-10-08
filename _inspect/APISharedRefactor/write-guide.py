from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[2]
def write(rel,s):
    p=ROOT/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
write('APIShared/README.md','''# APIShared

APIShared is a standalone BepInEx plugin for Stronghold Crusader Definitive Edition. It provides shared mission/lobby observation, presentation hooks, building services and optional settings integration. Independent mods share one implementation instead of installing competing hooks. Third-party mods do not need SerpsModsHost, ExtendedData, friend-assembly access or source files from this repository.

Plugin GUID: `APIShared_Serp`. Assembly: `APIShared.dll`. Target framework: .NET Framework 4.8.1. Current development version: **0.4.12**, requiring Script Extender **2.14.0**. This namespace migration requires rebuilding consumers; do not mix old consumer binaries with the migrated APIShared binary.

## Install once

Install Script Extender and APIShared into `BepInEx/plugins/000shcdese` and `BepInEx/plugins/APIShared_Serp`. Download APIShared from the [releases](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases?q=APIShared). A consumer must not ship its own APIShared DLL. The SerpsMods Workshop pack already contains one infrastructure copy; do not install duplicate pack/standalone copies.

## Start here

Declare hard dependencies on Script Extender and the APIShared version you compiled against. Reference the centrally installed DLL using `<Private>false</Private>`; see the [compilable example project](examples/ThirdPartyMod/ThirdPartyMod.csproj).

```csharp
using APIShared;

// Keep this in a static or publisher-rooted runtime, not a plugin Update method.
private static readonly ModApiClient Api = ApiShared.ForMod("Example.Author.MyMod");

private void Awake()
{
    // Managed mission/lobby capabilities are available after APIShared.Awake.
    if (Api.TryGetMissionLifecycle(out var missions, out var diagnostic))
        missions.TryRegisterObserver("main", OnStarted, OnEnded, null, out diagnostic);

    // Native services have independent availability after initialization.
    Api.WhenReady(client =>
    {
        if (!client.TryGetBuildingRepair(out var repair, out var failure))
            LogDiagnostic(failure);
    });
}
```

`ForMod` binds your stable BepInEx GUID, validates it, and reserves nothing. `ApiShared.Current` remains available for direct acquisitions with an explicit GUID. `WhenReady` means global initialization has reached a terminal state, including `Unavailable`; it does **not** guarantee every capability is available. Always check each `TryGet` and mutation result. `Pending` means initialization is incomplete; `UnsupportedBuild`, validation errors and conflicts concern the requested service.

Early readiness callbacks run on the initialization publisher thread. Late callbacks run synchronously on the registering thread. Exceptions are logged and isolated in both paths. There is no implicit main-thread dispatch. Mission/lobby registration and Noesis work require the Unity thread; simulation notifications must not directly change UI.

## Find the right API

- [API catalog and contracts](docs/API_CATALOG.md): services, threading, ownership, lifetime and advanced APIs.
- [Third-party guide](docs/THIRD_PARTY_GUIDE.md): optional mode policies, settings, packaging and migration.
- [Compilable examples](examples/ThirdPartyMod): mission events, custom mode permissions, preset settings and HUD extension.
- [Architecture](ARCHITECTURE.md): where contracts and implementation live.

All source-linked `Shared` helpers remain internal implementation details. Public settings now live in `APIShared.ModSettings`, mode snapshots/profiles in `APIShared.GameModes`, and established Serps policies in `APIShared.SerpsMods`. APIShared applies no Serps mode policy automatically to a third-party mod.

Shared hooks and registrations live until process exit. Do not dispose them from plugin teardown: SHCDE destroys BepInEx startup components during normal startup. Use the service's logical activation controls where available. Registration IDs are stable and owner-local; reusing one does not replace its callback.
''')
write('APIShared/ARCHITECTURE.md','''# APIShared architecture

APIShared owns typed, process-wide services and the common ModSettings integration. Its assembly identity and BepInEx GUID remain `APIShared` and `APIShared_Serp`.

## Source map

| Directory | Responsibility |
|---|---|
| `src/Core` | Public entry point, owner-bound client, initialization and internal native infrastructure |
| `src/Missions`, `src/Lobby`, `src/Players` | Shared observers and immutable lifecycle/state contracts |
| `src/Presentation`, `src/Buildings` | HUD/briefing presentation, repair and gatehouse services |
| `src/Units`, `src/Pathfinding`, `src/Diagnostics`, `src/Savegames` | Validated helpers, advanced integration and shared observations |
| `src/GameModes` | General mode snapshots and optional caller-defined permissions |
| `src/SerpsMods` | Existing Serps GUID profiles and feature exceptions |
| `src/ModSettings` | Public settings integration and its implementation |
| `src/UnitCommands` | Internal BugfixesAndQoL/MoatMove command and formation implementation |

Capability contracts retain the `APIShared` namespace. Directories organize implementation without forcing namespace churn in these contracts. Public former `Shared` types are placed in the three explicit namespaces above. Internal source-linked `Shared` utilities still provide dependency-free JSON, dispatch, logging and per-consumer adapters; they are not a third-party dependency.

## Publication and lifetime

`APISharedPlugin.Awake` establishes main-thread dispatch and managed services. Native initialization is published by `CrusaderLibrary.Instance.LibraryLoaded`. `ApiSharedRuntime.ProcessInstance`, static registries and long-lived publishers retain runtime objects after startup cleanup. There is no plugin Update/coroutine/teardown host.

Global readiness is separate from individual service diagnostics. Capabilities isolate failures; unsupported native services must not disable independent managed observation. Owner-bound clients delegate to the same service acquisition and ownership checks as direct consumers. Creating a client installs nothing.

Readiness callbacks share one exception boundary for early and late delivery, always outside the initialization lock. No dispatch or callback thread transformation is added. Native hooks, executable ranges and installation order are unchanged by this refactor.

## Settings implementation

`PresetLobbyModSettingsViewModel` is a partial class: the main file handles integration, permissions and notifications; `.Sources.cs` handles source selection and UI/search commands; `.Persistence.cs` contains the existing preset controller and stable storage schema. `PerPlayerLobbySettings.cs` owns lobby convergence and its builder contracts. `LobbyModSettingsPresetRegistration.cs` owns preparation, registration and horizontal focus-scroll handling.

The extraction preserves executable member bodies and persistence keys. JSON still uses the source-linked `Shared.DependencyFreeJson`. Personal, host, per-player and trail settings retain their existing sync and save boundaries.

## Policy and compatibility

Mode capture describes the current context. `GameplayModModePolicy` evaluates an explicit caller profile; it is optional. `SerpsModProfiles` and `GameplayFeatureModePolicy` preserve our existing permissions and exceptions. Unknown/conflicting contexts remain denied by the optional evaluator.

The installed Script Extender is the compile/runtime source of truth. APIShared currently declares 2.14.0. Native support is still controlled by existing hash-bound validators and the current native baseline, not by historical documentation version numbers. The canonical Fixes source remains the compatibility reference; this refactor installs no additional hooks and changes no native targets.

The pre-refactor migration record is retained in `MIGRATION_PLAN.md` as historical context. Current public contracts are documented in `docs/API_CATALOG.md`.
''')
write('APIShared/docs/THIRD_PARTY_GUIDE.md','''# Third-party integration guide

## Initialization and ownership

Use your plugin's stable GUID for `ApiShared.ForMod`. Keep the resulting client, logger, settings and callbacks in a static runtime or a long-lived publisher. Register managed lifecycle services from `Awake`, after the hard APIShared dependency has initialized. Acquire native services in `WhenReady`. Never treat a native failure as proof that all managed services failed.

The example library compiles against ordinary public assemblies and has no `InternalsVisibleTo` access. Build it with the game path supplied through `GameDir`; its APIShared reference defaults to this repository's output for verification and can be overridden with `ApiSharedDir` for normal installed use. APIShared's build driver compiles it as a consumer check, but does not install it into the game.

## Optional game-mode permissions

Read `MissionLifecycleNotification.Context.Mode` for the event being handled. Use `GameModeHelper.Capture()` for a current snapshot where appropriate. Capture itself applies no permissions. Construct `GameplayModActivationProfile` with your own GUID, allowed contexts and `allowRealMultiplayer`, then call `GameplayModModePolicy.IsAllowed`. You can instead implement your own policy directly from the snapshot.

The built-in optional evaluator rejects unknown or conflicting origins. Its existing contexts do not offer a tutorial permission. These defaults do not prevent a third-party mod from making its own informed policy decision without the evaluator. Do not call `SerpsModProfiles.GetProfile` for a foreign GUID; those profiles deliberately describe our established mods only.

## Presets without SerpsModsHost

Derive your settings from `APIShared.ModSettings.PresetLobbyModSettingsViewModel` and register through `LobbyModSettingsPresetRegistration.Register`. The Script Extender displays the mod tab; APIShared owns discovery, personal preset files, persistence and lobby convergence. SerpsModsHost and ExtendedData are optional consumers, not prerequisites.

Classify saved properties with Script Extender `[SyncHostOnly]` or `[SyncPerPlayer]`, or APIShared `[PresetLocal]`. Each per-player property needs a public compatible `<Name>Data` array for slots 0..8. Keep setters behind `CanMutateSetting()` and call `OnPropertyChanged` after actual changes. Configure additional per-player rules through `ConfigurePerPlayerLobbySettings`; do not implement another roster poller.

Bind host/client UI sections to `CanEditHostSettings` and `CanEditClientSettings`. Bind common preset/source actions to the public `System_*` properties and commands; do not replace them with a second persistence engine. Use nonempty tooltips with `ToolTipService.ShowDuration="60000"`. Settings-search metadata is optional when the control title/tooltip layout is unambiguous.

The example includes a minimal host/local settings tab. Use localized labels and override `ResolveSettingsUiText` for translated common actions in a production mod. APIShared supplies English fallbacks. Shared file formats and legacy `__Serp*` persistence keys are retained for existing saves; they do not restrict mod GUIDs or require the Serps pack.

## Migration and packaging

This development refactor moves `Shared` public ModSettings types to `APIShared.ModSettings`, mode types/evaluator to `APIShared.GameModes`, and Serps feature tables to `APIShared.SerpsMods`. `GameplayModModePolicy.GetProfile` becomes `SerpsModProfiles.GetProfile`; foreign mods construct profiles directly. Update XAML imports from `clr-namespace:Shared;assembly=APIShared` to `clr-namespace:APIShared.ModSettings;assembly=APIShared`.

Rebuild every dependent assembly, including companion APIs, after this source-breaking migration. Change only APIShared-owned symbols: local source-linked `Shared` helpers keep their namespace. The internal command/formation runtime is unavailable to third-party consumers.

Release consumers with hard minimum APIShared/Script Extender versions and `<Private>false</Private>` for runtime references. Never include APIShared.dll, Script Extender DLLs or game DLLs in the consumer package. Put your XAML under the usual mod Override directory. Keep one centrally installed APIShared instance.
''')
catalog='''# API catalog

Capabilities are acquired through `ModApiClient` or `IApiShared`. The table describes the existing contracts, not a guarantee of support for every game build. Always inspect returned diagnostics. All unit/building/player game IDs are one-based where documented; array indices are not game IDs.

| Area / entry | Purpose and availability | Thread, ownership and lifetime |
|---|---|---|
| `TryGetMissionLifecycle` | Managed initialization checkpoints, completed mission start/end and current context | Register on Unity thread; publisher-thread notifications; owner/registration ordering; late start replay only; process lifetime |
| `TryGetLobbyState` | Managed immutable multiplayer-lobby snapshots | Register on Unity thread; immediate known-state replay; deterministic owner/ID ordering; process lifetime |
| `TryGetPlayerDefeat` | Managed one-shot lord death and official loss transitions | Tick/simulation publisher; no initial-state replay; do not change UI directly; process lifetime |
| `TryGetUnitHudPresentation` | Categories, image overrides, interactions, recruitment tickets and control groups | Unity/Noesis presentation; owner-local IDs; ambiguity preserves Vanilla; logical activation extension; process lifetime |
| `TryGetBriefingGoldPresentation` | Ordered adjustments after Vanilla briefing calculation | Presentation publisher; stage/owner/ID ordering; invalid results preserve last safe value; process lifetime |
| `TryGetGatehouseTiming` | Typed timing/distance settings | Native validation required; exclusive owner; permanent hook and logical state; automation via `IGatehouseAutomationCapability` |
| `TryGetGatehouseDistanceOrigin` | Vanilla begin coordinate or complete-bounds center | Native validation required; exclusive owner; permanent runtime state |
| `TryGetBuildingRepair` | Repair quote and repair-tooltip presentation | Acquired on demand after native initialization; respect each operation's contract; UI on Unity thread |
| `TryGetAivBuildStep` | Before/after observation around one unchanged Vanilla call | Native caller thread; deterministic begin order and reverse completion; owner-local IDs; process lifetime |
| `APIShared.GameModes` | Mode snapshots, caller-defined contexts and optional permissions | No automatic permission enforcement; use the relevant mission snapshot; explicit multiplayer policy |
| `APIShared.ModSettings` | Settings base class, registration, presets, sources and search | Unity-thread UI/registration; existing host/per-player sync; personal persistence remains isolated |

## Direct helpers and advanced integration

`LocalSelectionAPI`, `MarkedUnitSelectionAPI` and `PlayerPerspectiveAPI` provide selection/perspective access independent of a custom command engine. `UnitAccess` validates unit lookup/liveness; its unsafe pointer views are immediate-use advanced contracts and must not outlive their valid game state. `GatehouseDrawbridgeCoupling` is a pure spatial helper: callers supply validated bounds and live identity predicates; it grants no ownership or access permission.

`AssassinPathAPI`, `AssassinAttackControlAPI`, `EnemyGatePathPolicyBridge`, `EnemyBridgeDiagnosticBridge` and `TemporaryGateRouteAcceptanceBridge` expose specialized path integration/diagnostics. They are not a general-purpose movement-command API. Use their documented immediate context and ownership contracts; do not retain transient native contexts. `AiBuildDiagnostic` remains dormant without a registration. `SavegameModSettings` supplies typed save/trail settings integration. `LobbyPreparationOverride` is advanced shared lobby-preparation integration, not an alternative general lifecycle service.

The internal command dispatcher, formation implementation, native addresses, scanners, memory writers and concrete services are not supported public extension points. Prefer Script Extender APIs directly for ordinary unit commands, pathing data, messages and events already supplied there.

## Failure and registration rules

`NativeApiState.Ready` means the global API is published. Capability diagnostics may still report `Pending`, `UnsupportedBuild`, `PatternMissing`, `Ambiguous`, `ValidationFailed`, `Conflict` or `Faulted`. Each failed operation returns a reason; `ConflictOwnerGuid` identifies an owner where applicable. Native hash fields may be empty for managed services or before native initialization.

Registration IDs are unique within an owner and service; stable IDs allow deterministic ordering. Registrations do not imply replacement or unsubscription. Callback exceptions are isolated where the service explicitly documents that contract; native Vanilla exceptions retain the original propagation rules. No API-wide promise of Unity-thread dispatch exists. Use logical activation to suspend supported presentation features; never dispose a published process-wide hook.

## Public type index

The following source-generated index includes data contracts and advanced APIs. XML documentation in `APIShared.xml` provides member-level details.

'''
for domain in ['Core','Missions','Lobby','Players','Units','Presentation','Buildings','GameModes','ModSettings','Pathfinding','Diagnostics','Savegames','SerpsMods']:
    entries=[]
    for p in sorted((ROOT/'APIShared/src'/domain).glob('*.cs')):
        entries+=re.findall(r'^    public\s+(?:(?:static|sealed|abstract|readonly|unsafe|partial)\s+)*(?:class|struct|interface|enum)\s+(\w+)',p.read_text(encoding='utf-8-sig'),re.M)
    catalog+='### '+domain+'\n\n'+', '.join('`'+x+'`' for x in sorted(set(entries)))+'.\n\n'
write('APIShared/docs/API_CATALOG.md',catalog)
p=ROOT/'APIShared/MIGRATION_PLAN.md';s=p.read_text(encoding='utf-8-sig')
if not s.startswith('> Historical'):write('APIShared/MIGRATION_PLAN.md','> Historical implementation record. For the current structure and API, use ARCHITECTURE.md and docs/API_CATALOG.md. Version and source-path references below describe earlier stages.\n\n'+s)

write('APIShared/examples/ThirdPartyMod/ExamplePlugin.cs','''using System;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;

namespace ThirdPartyMod
{
    [BepInPlugin(Guid, "APIShared Example", "1.0.0")]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.4.12")]
    public sealed class ExamplePlugin : BaseUnityPlugin
    {
        public const string Guid = "Example.Author.APISharedDemo";
        private static readonly ModApiClient Api = ApiShared.ForMod(Guid);
        private static readonly ExampleSettings Settings = new ExampleSettings();
        private static ManualLogSource log;

        private void Awake()
        {
            log = Logger;
            MissionExample.Register(Api, Log);
            Api.WhenReady(OnReady);
        }

        private void OnReady(ModApiClient client)
        {
            // The publisher roots this callback across SHCDE startup cleanup.
            APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
                this, log, "APIShared Example", Settings, "ScriptExtenderUI/APISharedExample.xaml");
            if (client.TryGetUnitHudPresentation(out var hud, out var diagnostic))
                HudExample.Register(hud, Log);
            else Log(diagnostic.Reason);
        }

        internal static void Log(string message) =>
            log?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
''')
write('APIShared/examples/ThirdPartyMod/MissionExample.cs','''using System;
using APIShared;
using APIShared.GameModes;

namespace ThirdPartyMod
{
    internal static class MissionExample
    {
        // Arbitrary foreign GUID, optional contexts and multiplayer restriction.
        private static readonly GameplayModActivationProfile Profile = new GameplayModActivationProfile(
            ExamplePlugin.Guid, "APIShared Example", GameplayModAllowedContext.CustomGame |
            GameplayModAllowedContext.MapEditor, allowRealMultiplayer: false);
        private static Action<string> log;

        internal static void Register(ModApiClient api, Action<string> logger)
        {
            log = logger;
            if (!api.TryGetMissionLifecycle(out var missions, out var diagnostic) ||
                !missions.TryRegisterObserver("missions", OnStarted, OnEnded, null, out diagnostic))
                log(diagnostic.Reason);
        }

        private static void OnStarted(MissionLifecycleNotification notification)
        {
            bool allowed = GameplayModModePolicy.IsAllowed(Profile, notification.Context.Mode, out var reason);
            log($"Mission {notification.Context.SessionId}: allowed={allowed}, replay={notification.IsReplay}, reason={reason}");
            // A policy decision only: a real mod decides which of its own features to activate.
        }

        private static void OnEnded(MissionLifecycleNotification notification) =>
            log($"Mission {notification.Context.SessionId} ended; clean up mod-local session state here.");
    }
}
''')
write('APIShared/examples/ThirdPartyMod/ExampleSettings.cs','''using APIShared.ModSettings;
using SHCDESE.API.Components.Network;

namespace ThirdPartyMod
{
    public sealed class ExampleSettings : PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true;
        private bool showOverlay = true;

        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set { if (!CanMutateSetting() || value == enableMod) return; enableMod = value; OnPropertyChanged(nameof(EnableMod)); }
        }

        [PresetLocal]
        public bool ShowOverlay
        {
            get => showOverlay;
            set { if (!CanMutateSetting() || value == showOverlay) return; showOverlay = value; OnPropertyChanged(nameof(ShowOverlay)); }
        }
    }
}
''')
write('APIShared/examples/ThirdPartyMod/HudExample.cs','''using System;
using APIShared;
using Noesis;

namespace ThirdPartyMod
{
    internal static class HudExample
    {
        // Load a mod-owned image through your normal asset pipeline on the Unity thread.
        // Null deliberately preserves the current image until an asset is assigned.
        internal static ImageSource Icon;

        internal static void Register(IUnitHudPresentationCapability hud, Action<string> log)
        {
            var definition = new UnitHudImageOverrideDefinition("swordsman-icon", UnitHudImageSlot.UIButtonsK007);
            if (!hud.TryRegisterImageOverride(definition, ResolveIcon, out var diagnostic))
                log(diagnostic.Reason);
        }

        private static ImageSource ResolveIcon(UnitHudImageOverrideContext context) => Icon;
    }
}
''')
write('APIShared/examples/ThirdPartyMod/Override/ScriptExtenderUI/APISharedExample.xaml','''<ScrollViewer xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:api="clr-namespace:APIShared.ModSettings;assembly=APIShared" HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Auto">
  <StackPanel Margin="16">
    <TextBlock Text="HOST OPTIONS" />
    <StackPanel IsEnabled="{Binding CanEditHostSettings}">
      <CheckBox Content="Enable example" IsChecked="{Binding EnableMod}" ToolTip="Allows the host to activate this example." ToolTipService.ShowDuration="60000" />
    </StackPanel>
    <TextBlock Text="CLIENT OPTIONS" Margin="0,12,0,0" />
    <StackPanel IsEnabled="{Binding CanEditClientSettings}">
      <CheckBox Content="Show overlay" IsChecked="{Binding ShowOverlay}" ToolTip="Local presentation preference, stored in personal presets." ToolTipService.ShowDuration="60000" />
    </StackPanel>
  </StackPanel>
</ScrollViewer>
''')
template=(ROOT/'_inspect/APISharedPresetConsumerTests/APISharedPresetConsumerTests.csproj').read_text(encoding='utf-8-sig')
template=template.replace('APISharedPresetConsumerTests','ThirdPartyMod.Examples').replace('{B9720676-1DC7-4E9D-BD53-7745DFD57E87}','{5E1808C6-784A-4D95-9EF9-833488F0B31A}')
template=template.replace('$(MSBuildThisFileDirectory)..\\..\\APIShared\\BepInEx\\plugins\\APIShared_Serp','$(ApiSharedDir)')
template=template.replace('<ItemGroup>\n    <Reference Include="System"', '<PropertyGroup Condition="\'$(ApiSharedDir)\' == \'\'"><ApiSharedDir>$(MSBuildThisFileDirectory)..\\..\\BepInEx\\plugins\\APIShared_Serp</ApiSharedDir></PropertyGroup>\n  <ItemGroup>\n    <Reference Include="UnityEngine"><HintPath>$(GameDir)\\Stronghold Crusader Definitive Edition_Data\\Managed\\UnityEngine.dll</HintPath><Private>false</Private></Reference>\n    <Reference Include="UnityEngine.CoreModule"><HintPath>$(GameDir)\\Stronghold Crusader Definitive Edition_Data\\Managed\\UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>\n    <Reference Include="System"')
template=template.replace('<Compile Include="ConsumerSettings.cs" />','<Compile Include="ExamplePlugin.cs" /><Compile Include="MissionExample.cs" /><Compile Include="ExampleSettings.cs" /><Compile Include="HudExample.cs" />')
write('APIShared/examples/ThirdPartyMod/ThirdPartyMod.csproj',template)
print('English guides, type catalog and public-only examples written.')
