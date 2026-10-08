# API catalog

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

`APIShared.ModSettings.ToolTipPresentation` exposes common tooltip sizes for external XAML without source links. Resolution-sensitive properties must be read on the Unity thread.

## Failure and registration rules

`NativeApiState.Ready` means the global API is published. Capability diagnostics may still report `Pending`, `UnsupportedBuild`, `PatternMissing`, `Ambiguous`, `ValidationFailed`, `Conflict` or `Faulted`. Each failed operation returns a reason; `ConflictOwnerGuid` identifies an owner where applicable. Native hash fields may be empty for managed services or before native initialization.

Registration IDs are unique within an owner and service; stable IDs allow deterministic ordering. Registrations do not imply replacement or unsubscription. Callback exceptions are isolated where the service explicitly documents that contract; native Vanilla exceptions retain the original propagation rules. No API-wide promise of Unity-thread dispatch exists. Use logical activation to suspend supported presentation features; never dispose a published process-wide hook.

## Public type index

The following source-generated index includes data contracts and advanced APIs. XML documentation in `APIShared.xml` provides member-level details.

### Core

`APISharedPlugin`, `ApiShared`, `IApiShared`, `ModApiClient`, `NativeApiState`, `NativeCapabilityDiagnostic`, `NativeCapabilityIds`, `NativeCapabilityState`.

### Missions

`IMissionLifecycleCapability`, `MissionContext`, `MissionEndReason`, `MissionInitializationPhase`, `MissionLifecycleKind`, `MissionLifecycleNotification`, `MissionMapType`, `MissionStartKind`.

### Lobby

`ILobbyStateCapability`, `LobbyPreparationOverride`, `LobbyStateSnapshot`.

### Players

`IPlayerDefeatCapability`, `PlayerDefeatNotification`, `PlayerLordDeathNotification`, `PlayerPerspectiveAPI`.

### Units

`LocalSelectionAPI`, `LocalSelectionSnapshot`, `MarkedUnitSelectionAPI`, `MarkedUnitSelectionSnapshot`, `UnitAccess`, `UnitLookupFailure`.

### Presentation

`BriefingGoldAdjustmentStage`, `BriefingGoldContext`, `IBriefingGoldPresentationCapability`, `IUnitHudActivationCapability`, `IUnitHudPresentationCapability`, `UnitHudCategoryDefinition`, `UnitHudCategorySnapshot`, `UnitHudControlGroupSnapshot`, `UnitHudImageOverrideContext`, `UnitHudImageOverrideDefinition`, `UnitHudImageSlot`, `UnitHudInteractionContext`, `UnitHudMouseButton`, `UnitHudRecruitmentTicket`, `UnitHudSlotSnapshot`, `UnitHudSurface`, `UnitHudTextKind`, `UnitHudTextProfile`, `UnitHudTint`, `UnitHudUnitSnapshot`.

### Buildings

`BuildingRepairQuote`, `GatehouseDistanceOrigin`, `GatehouseDrawbridgeCoupling`, `GatehouseFootprintCandidate`, `GatehouseTimingSettings`, `GatehouseTimingValues`, `IBuildingRepairCapability`, `IGatehouseAutomationCapability`, `IGatehouseDistanceOriginCapability`, `IGatehouseTimingCapability`, `RepairTooltipEntry`, `RepairTooltipViewModel`.

### GameModes

`GameModeHelper`, `GameModeKind`, `GameModeLaunchVariant`, `GameModeSnapshot`, `GameTrailType`, `GameplayModActivationProfile`, `GameplayModAllowedContext`, `GameplayModModePolicy`.

### ModSettings

`DynamicPresetSetting`, `IDynamicPresetSettingsProvider`, `IModSettingsApplicationBackend`, `IModSettingsMissionSourceEndpoint`, `IModSettingsPresetEndpoint`, `IModSettingsWorkingCopyEndpoint`, `IModSettingsWorkingSourceProvider`, `INetworkModSettingsApplicationBackend`, `LobbyModSettingsPresetRegistration`, `ModSettingsApplication`, `ModSettingsPresetJson`, `ModSettingsPresetListEntry`, `ModSettingsPresetSaveTarget`, `ModSettingsPresetSourceKind`, `ModSettingsSearch`, `ModSettingsSearchEntry`, `ModSettingsSearchMatcher`, `ModSettingsSearchVisibilityConverter`, `ModSettingsWorkingSource`, `ModSettingsWorkingSourceKind`, `ModSettingsWorkingSourceRegistry`, `PerPlayerLobbySettingsBuilder`, `PerPlayerLobbySnapshot`, `PresetLobbyModSettingsViewModel`, `PresetLocalAttribute`, `PresetSaveBulkMode`, `PresetSaveSelection`, `PresetSaveSettingViewModel`, `PresetSettingDescriptor`, `PresetSettingScope`, `PublishedModSettingsPreset`, `PublishedPresetSetting`, `PublishedPresetValueMode`, `RequiresRestartAttribute`, `ToolTipPresentation`.

### Pathfinding

`AssassinAttackControlAPI`, `AssassinGateTransitionPolicy`, `AssassinPathAPI`, `AssassinTransitionKind`, `ElevatedMoatAiCapability`, `ElevatedMoatAiState`, `EnemyBridgeDiagnosticBridge`, `EnemyGatePathPolicyBridge`, `EnemyGateSearchKind`, `IAssassinTraversalView`, `IEnemyBridgePathObserver`, `IEnemyBridgeTopologyObserver`, `IEnemyGateAssassinObserver`, `IEnemyGateClimbRoutePolicySnapshot`, `IEnemyGatePathPolicy`, `IEnemyGateRegionPairObserver`, `IEnemyGateRoutePolicyProvider`, `IEnemyGateRoutePolicySnapshot`, `ITemporaryAssassinGateObserver`, `ITemporaryGateRouteAcceptanceObserver`, `TemporaryGateRouteAcceptanceBridge`.

### Diagnostics

`AiBuildDiagnostic`, `AiBuildDiagnosticRecord`, `AiCoarseCellSample`, `AiEconomyGridEvidence`, `AiNearbyPathEvidence`, `AiPathTileSample`, `AiRouteConnection`, `AiRouteEvidence`, `AivBuildStepCompletion`, `AivBuildStepContext`, `IAivBuildStepCapability`, `IAivBuildStepInvocation`, `IAivBuildStepObserver`.

### Savegames

`SavegameLoadChoiceState`, `SavegameModSettings`, `SavegameModSettingsRecord`, `SavegameModSettingsRecordFormatter`, `TrailCreatorRule`.

### SerpsMods

`GameplayFeatureActivationProfile`, `GameplayFeatureId`, `GameplayFeatureModePolicy`, `SerpsModProfiles`.

