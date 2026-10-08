# APIShared

APIShared is a standalone BepInEx plugin for Stronghold Crusader Definitive Edition. It provides shared mission/lobby observation, presentation hooks, building services and optional settings integration. Independent mods share one implementation instead of installing competing hooks. Third-party mods do not need SerpsModsHost, ExtendedData, friend-assembly access or source files from this repository.

Plugin GUID: `APIShared_Serp`. Assembly: `APIShared.dll`. Target framework: .NET Framework 4.8.1. Current development version: **0.4.12**, requiring Script Extender **2.14.0**. This namespace migration requires rebuilding consumers; do not mix old consumer binaries with the migrated APIShared binary.

## Install once

Install Script Extender and APIShared into `BepInEx/plugins/000shcdese` and `BepInEx/plugins/APIShared_Serp`. Download APIShared from the [releases](https://github.com/SHCDE-APIShared/APIShared/releases). A consumer must not ship its own APIShared DLL. The SerpsMods Workshop pack already contains one infrastructure copy; do not install duplicate pack/standalone copies.

## Start here

Declare hard dependencies on Script Extender and the minimum APIShared version your features require. BepInEx accepts newer versions; the versioned attribute is a minimum, not an exact pin. Mirror required plugin minima in `info.json` using the [generic dependency format](docs/DEPENDENCIES.md). Reference the centrally installed DLL using `<Private>false</Private>`; see the [compilable example project](examples/ThirdPartyMod/ThirdPartyMod.csproj).

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

APIShared owns all its runtime sources inside this project. It does not compile the mod workspace's `Shared` helpers; consumers use the public assembly rather than source-linking APIShared internals. Public settings live in `APIShared.ModSettings`, mode snapshots/profiles in `APIShared.GameModes`, and established Serps policies in `APIShared.SerpsMods`. APIShared applies no Serps mode policy automatically to a third-party mod.

Shared hooks and registrations live until process exit. Do not dispose them from plugin teardown: SHCDE destroys BepInEx startup components during normal startup. Use the service's logical activation controls where available. Registration IDs are stable and owner-local; reusing one does not replace its callback.

## Build and contribute

This repository includes its own tests and validation tools. See [CONTRIBUTING.md](CONTRIBUTING.md) for the standalone build and dependency paths. SerpsMods integration checks are additional workspace checks and are not required by an independent checkout.

The MIT-licensed source repository is [SHCDE-APIShared/APIShared](https://github.com/SHCDE-APIShared/APIShared). Earlier releases remain in the [historical mod repository](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases?q=APIShared). Until a new release is published, use those historical downloads. Release preparation runs from the independent checkout through `release.bat` and creates a draft by default.
