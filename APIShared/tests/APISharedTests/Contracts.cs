using APIShared.ModSettings;
using APIShared;
using CrusaderDE;
using Iced.Intel;
using APIShared.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace APISharedTests
{
    public partial class RuntimeTests
    {
        [TestMethod]
        [TestCategory("Contracts")]
        public void VerifyPublicSurface() => TestPublicSurface();

        private static void TestPublicSurface()
        {
            var imageSlots = (UnitHudImageSlot[])Enum.GetValues(typeof(UnitHudImageSlot));
            Assert(imageSlots.Length >= 7 &&
                imageSlots[0] == UnitHudImageSlot.UIBuildingsO011 &&
                imageSlots[1] == UnitHudImageSlot.UIBuildingsO012 &&
                imageSlots[2] == UnitHudImageSlot.UIButtonsK007 &&
                imageSlots[3] == UnitHudImageSlot.UIButtonsK008 &&
                imageSlots[4] == UnitHudImageSlot.UIButtonsO016 &&
                imageSlots[5] == UnitHudImageSlot.UIButtonsO017 &&
                imageSlots[6] == UnitHudImageSlot.UIButtonsO018,
                "existing unit-HUD image slots retain their ordinal prefix; additional slots are allowed");
            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "APIShared.UnitLookupFailure",
                "APIShared.UnitAccess",
                "APIShared.AssassinPathBuilder",
                "APIShared.AssassinPathAPI",
                "APIShared.AssassinTransitionKind",
                "APIShared.AssassinGateTransitionPolicy",
                "APIShared.IEnemyGateClimbRoutePolicySnapshot",
                "APIShared.MissionStartKind",
                "APIShared.MissionMapType",
                "APIShared.MissionLifecycleKind",
                "APIShared.MissionInitializationPhase",
                "APIShared.MissionEndReason",
                "APIShared.MissionContext",
                "APIShared.MissionLifecycleNotification",
                "APIShared.IMissionLifecycleCapability",
                "APIShared.GameModes.GameModeKind",
                "APIShared.GameModes.GameModeLaunchVariant",
                "APIShared.GameModes.GameTrailType",
                "APIShared.GameModes.GameModeSnapshot",
                "APIShared.GameModes.GameModeHelper",
                "APIShared.GameModes.GameplayModAllowedContext",
                "APIShared.GameModes.GameplayModActivationProfile",
                "APIShared.GameModes.GameplayModModePolicy",
                "APIShared.SerpsMods.GameplayFeatureId",
                "APIShared.SerpsMods.GameplayFeatureActivationProfile",
                "APIShared.SerpsMods.GameplayFeatureModePolicy",
                "APIShared.ModSettings.ModSettingsSearchMatcher",
                "APIShared.ModSettings.ModSettingsSearch",
                "APIShared.ModSettings.ModSettingsSearchVisibilityConverter",
                "APIShared.ModSettings.ModSettingsSearchEntry",
                "APIShared.ModSettings.PerPlayerLobbySettingsBuilder",
                "APIShared.ModSettings.PerPlayerLobbySnapshot",
                "APIShared.ModSettings.PresetLocalAttribute",
                "APIShared.ModSettings.PresetLobbyModSettingsViewModel",
                "APIShared.ModSettings.LobbyModSettingsPresetRegistration",
                "APIShared.ModSettings.LobbyModSettingsPresetRegistration+PreparedExternalSettings",
                "APIShared.ModSettings.IModSettingsPresetEndpoint",
                "APIShared.ModSettings.IModSettingsMissionSourceEndpoint",
                "APIShared.ModSettings.IModSettingsWorkingCopyEndpoint",
                "APIShared.ModSettings.ModSettingsWorkingSourceKind",
                "APIShared.ModSettings.ModSettingsWorkingSource",
                "APIShared.ModSettings.IModSettingsWorkingSourceProvider",
                "APIShared.ModSettings.ModSettingsWorkingSourceRegistry",
                "APIShared.ModSettings.PublishedPresetValueMode",
                "APIShared.ModSettings.PresetSaveBulkMode",
                "APIShared.ModSettings.PresetSettingScope",
                "APIShared.ModSettings.PresetSettingDescriptor",
                "APIShared.ModSettings.DynamicPresetSetting",
                "APIShared.ModSettings.RequiresRestartAttribute",
                "APIShared.ModSettings.IModSettingsApplicationBackend", "APIShared.ModSettings.INetworkModSettingsApplicationBackend",
                "APIShared.ModSettings.ModSettingsApplication",
                "APIShared.ModSettings.IDynamicPresetSettingsProvider",
                "APIShared.ModSettings.PresetSaveSelection",
                "APIShared.ModSettings.PresetSaveSettingViewModel",
                "APIShared.ModSettings.ModSettingsPresetSourceKind",
                "APIShared.ModSettings.ModSettingsPresetListEntry",
                "APIShared.ModSettings.ModSettingsPresetSaveTarget",
                "APIShared.ModSettings.PublishedPresetSetting",
                "APIShared.ModSettings.PublishedModSettingsPreset",
                "APIShared.ModSettings.ModSettingsPresetJson",
                "APIShared.GatehouseFootprintCandidate",
                "APIShared.GatehouseDrawbridgeCoupling",
                "APIShared.GatehouseDistanceOrigin",
                "APIShared.GatehouseTimingSettings",
                "APIShared.GatehouseTimingValues",
                "APIShared.IGatehouseDistanceOriginCapability",
                "APIShared.IGatehouseTimingCapability",
                "APIShared.IGatehouseAutomationCapability",
                "APIShared.IUnitHudPresentationCapability",
                "APIShared.IUnitHudActivationCapability",
                "APIShared.IAivBuildStepCapability",
                "APIShared.ILobbyStateCapability",
                "APIShared.LobbyStateSnapshot",
                "APIShared.LocalSelectionAPI",
                "APIShared.LocalSelectionSnapshot",
                "APIShared.IPlayerDefeatCapability",
                "APIShared.IBriefingGoldPresentationCapability",
                "APIShared.IBuildingRepairCapability",
                "APIShared.BuildingRepairQuote",
                "APIShared.RepairTooltipEntry",
                "APIShared.RepairTooltipViewModel",
                "APIShared.BriefingGoldAdjustmentStage",
                "APIShared.BriefingGoldContext",
                "APIShared.BriefingGoldAdjuster",
                "APIShared.PlayerLordDeathNotification",
                "APIShared.PlayerDefeatNotification",
                "APIShared.IAivBuildStepObserver",
                "APIShared.IAivBuildStepInvocation",
                "APIShared.AivBuildStepContext",
                "APIShared.AivBuildStepCompletion",
                "APIShared.AiBuildDiagnosticRecord",
                "APIShared.AiRouteConnection",
                "APIShared.AiRouteEvidence",
                "APIShared.AiPathTileSample",
                "APIShared.AiCoarseCellSample",
                "APIShared.AiNearbyPathEvidence",
                "APIShared.AiEconomyGridEvidence",
                "APIShared.AiBuildDiagnostic",
                "APIShared.SavegameModSettingsRecord",
                "APIShared.TrailCreatorRule",
                "APIShared.SavegameLoadChoiceState",
                "APIShared.SavegameModSettingsRecordFormatter",
                "APIShared.SavegameModSettings",
                "APIShared.IApiShared",
                "APIShared.NativeApiState",
                "APIShared.LobbyPreparationOverride",
                "APIShared.ElevatedMoatAiState",
                "APIShared.ElevatedMoatAiCapability",
                "APIShared.NativeCapabilityDiagnostic",
                "APIShared.NativeCapabilityIds",
                "APIShared.NativeCapabilityState",
                "APIShared.UnitHudSurface",
                "APIShared.UnitHudMouseButton",
                "APIShared.UnitHudImageSlot",
                "APIShared.UnitHudTint",
                "APIShared.UnitHudUnitSnapshot",
                "APIShared.UnitHudCategoryMatcher",
                "APIShared.UnitHudCategoryImageResolver",
                "APIShared.UnitHudTextKind",
                "APIShared.UnitHudTextResolver",
                "APIShared.UnitHudTextProfile",
                "APIShared.UnitHudCategoryDefinition",
                "APIShared.UnitHudRecruitmentTicket",
                "APIShared.UnitHudRecruitmentHandler",
                "APIShared.UnitHudCategorySnapshot",
                "APIShared.UnitHudSlotSnapshot",
                "APIShared.UnitHudControlGroupSnapshot",
                "APIShared.UnitHudInteractionContext",
                "APIShared.UnitHudInteractionHandler",
                "APIShared.UnitHudImageOverrideContext",
                "APIShared.UnitHudImageOverrideResolver",
                "APIShared.UnitHudImageOverrideDefinition",
                "APIShared.ApiShared",
                "APIShared.MarkedUnitSelectionSnapshot",
                "APIShared.MarkedUnitSelectionAPI",
                "APIShared.PlayerPerspectiveAPI",
                "APIShared.EnemyGateSearchKind",
                "APIShared.IEnemyGatePathPolicy",
                "APIShared.IEnemyGateRegionPairObserver",
                "APIShared.IEnemyGateAssassinObserver",
                "APIShared.IEnemyGateRoutePolicyProvider",
                "APIShared.IEnemyGateRoutePolicySnapshot",
                "APIShared.EnemyGatePathPolicyBridge",
                "APIShared.IEnemyBridgePathObserver",
                "APIShared.EnemyBridgeDiagnosticBridge", "APIShared.TemporaryGateRouteAcceptanceBridge", "APIShared.ITemporaryGateRouteAcceptanceObserver",
                // BepInEx discovers the plugin type; it is public but is not a consumer service.
                "APIShared.APISharedPlugin"
            };

            MethodInfo referenceLifeHelper = typeof(UnitAccess).GetMethod("IsReallyAlive",
                new[] { typeof(SHCDESE.Interop.GameUnit).MakeByRefType() });
            MethodInfo pointerLifeHelper = typeof(UnitAccess).GetMethod("IsReallyAlive",
                new[] { typeof(SHCDESE.Interop.GameUnit).MakePointerType() });
            Assert(referenceLifeHelper != null && referenceLifeHelper.IsStatic && referenceLifeHelper.ReturnType == typeof(bool) &&
                pointerLifeHelper != null && pointerLifeHelper.IsStatic && pointerLifeHelper.ReturnType == typeof(bool),
                "UnitAccess retains the required reference/pointer bool life helpers; additional overloads are allowed");
            Type[] exported = typeof(IApiShared).Assembly.GetExportedTypes();
            foreach (Type type in exported)
            {
                // Required existing contracts must survive; additive public API extensions are allowed.
                expected.Remove(type.FullName);
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    AssertSafePublicType(method.ReturnType, $"{type.FullName}.{method.Name} return type");
                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        // Explicit native bridges expose only their audited immediate views.
                        // All other public types and parameters retain the native-type prohibition.
                        if (type == typeof(UnitAccess) && method.Name == "TryGetById" &&
                            parameter.Name == "unit" && parameter.ParameterType.IsByRef &&
                            parameter.ParameterType.GetElementType().IsPointer &&
                            parameter.ParameterType.GetElementType().GetElementType() == typeof(SHCDESE.Interop.GameUnit))
                            continue;
                        if (type == typeof(UnitAccess) && method.Name == "IsReallyAlive" &&
                            method.ReturnType == typeof(bool) && method.GetParameters().Length == 1 &&
                            parameter.Name == "unit" &&
                            parameter.ParameterType == typeof(SHCDESE.Interop.GameUnit).MakePointerType())
                            continue;
                        if ((type == typeof(AssassinPathBuilder) &&
                                (method.Name == "Invoke" || method.Name == "BeginInvoke") ||
                             type == typeof(AssassinPathAPI) && (method.Name == "RunVanillaBuilder" ||
                                method.Name == "TryStageWeightedRoute" || method.Name == "TryGetCurrentWeightedRequest")) &&
                            parameter.Name == "context" && parameter.ParameterType == typeof(IntPtr))
                            continue;
                        AssertSafePublicType(parameter.ParameterType, $"{type.FullName}.{method.Name} parameter {parameter.Name}");
                    }
                }
                if (!typeof(Delegate).IsAssignableFrom(type))
                    foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        foreach (ParameterInfo parameter in constructor.GetParameters())
                            AssertSafePublicType(parameter.ParameterType, $"{type.FullName} constructor parameter {parameter.Name}");
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    AssertSafePublicType(property.PropertyType, $"{type.FullName}.{property.Name} property type");
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    AssertSafePublicType(field.FieldType, $"{type.FullName}.{field.Name} field type");
            }

            foreach (string missing in expected)
                Assert(false, $"expected exported API type is missing: {missing}");

            var expectedAcquisitionMethods = new HashSet<string>(StringComparer.Ordinal)
            {
                "TryGetGatehouseDistanceOrigin",
                "TryGetGatehouseTiming",
                "TryGetUnitHudPresentation",
                "TryGetAivBuildStep",
                "TryGetLobbyState",
                "TryGetMissionLifecycle",
                "TryGetPlayerDefeat",
                "TryGetBriefingGoldPresentation",
                "TryGetBuildingRepair"
            };
            foreach (MethodInfo method in typeof(IApiShared).GetMethods())
                expectedAcquisitionMethods.Remove(method.Name);
            foreach (string missing in expectedAcquisitionMethods)
                Assert(false, $"expected capability acquisition method is missing: IApiShared.{missing}");

            Assert(NativeCapabilityIds.GatehouseDistanceOrigin == "gatehouse-distance-origin",
                "distance-origin capability ID must remain stable");
            Assert(NativeCapabilityIds.GatehouseTiming == "gatehouse-timing",
                "gatehouse-timing capability ID must remain stable");
            Assert(NativeCapabilityIds.UnitHudPresentation == "unit-hud-presentation",
                "unit-HUD capability ID must remain stable");
            Assert(NativeCapabilityIds.AivBuildStep == "aiv-build-step",
                "AIV build-step capability ID must remain stable");
            Assert(NativeCapabilityIds.LobbyState == "lobby-state",
                "lobby-state capability ID must remain stable");
            Assert(NativeCapabilityIds.PlayerDefeat == "player-defeat",
                "player-defeat capability ID must remain stable");
            Assert(NativeCapabilityIds.BriefingGoldPresentation == "briefing-gold-presentation",
                "briefing-gold capability ID must remain stable");
            Assert(NativeCapabilityIds.BuildingRepair == "building-repair",
                "building-repair capability ID must remain stable");
        }
        [TestMethod]
        [TestCategory("Contracts")]
        public void VerifyOwnerBoundClient() => TestOwnerBoundClient();

        private static void TestOwnerBoundClient()
        {
            foreach (string invalid in new[] { null, "", "  " })
            {
                bool rejected = false;
                try { ApiShared.ForMod(invalid); } catch (ArgumentException) { rejected = true; }
                Assert(rejected, "owner-bound entry must reject empty GUIDs immediately");
            }
            var runtime = new ApiSharedRuntime();
            var client = new ModApiClient("Foreign.Author.MyMod", runtime);
            Assert(client.OwnerGuid == "Foreign.Author.MyMod" && client.State == NativeApiState.Pending,
                "arbitrary foreign GUID does not require Serps profiles");
            Assert(!client.TryGetGatehouseTiming(out _, out var pending) && pending.State == NativeCapabilityState.Pending,
                "owner-bound client preserves pending capability diagnostics");
            // Publish a managed fixture without installing game hooks or completing native initialization.
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ApiSharedRuntime).GetField("lobbyState", privateInstance).SetValue(runtime, new LobbyStateService(null));
            typeof(ApiSharedRuntime).GetField("lobbyStateDiagnostic", privateInstance).SetValue(runtime,
                new NativeCapabilityDiagnostic(NativeCapabilityIds.LobbyState, NativeCapabilityState.Available, "", "managed fixture"));
            Assert(client.TryGetLobbyState(out var managed, out var managedDiagnostic) && managed != null &&
                managedDiagnostic.State == NativeCapabilityState.Available && client.State == NativeApiState.Pending,
                "available managed capability must be acquired before global readiness");
            int calls = 0;
            client.WhenReady(_ => throw new InvalidOperationException("test-early"));
            client.WhenReady(c => { Assert(ReferenceEquals(c, client), "callback retains owner-bound client"); calls++; });
            runtime.Initialize(ModuleBase, CreatePeImage(0x4000, true), "UNKNOWN", new FakeMemory(), null, null, false);
            Assert(calls == 1, "early callback failure must not block later consumers");
            client.WhenReady(_ => throw new InvalidOperationException("test-late"));
            client.WhenReady(_ => calls++);
            Assert(calls == 2, "late callbacks are synchronous and equally isolated");
            var failingLogger = new BepInEx.Logging.ManualLogSource("ReadinessLoggerFixture");
            failingLogger.LogEvent += (_, __) => throw new InvalidOperationException("test log listener");
            typeof(ApiSharedRuntime).GetField("log", privateInstance).SetValue(runtime, failingLogger);
            client.WhenReady(_ => throw new InvalidOperationException("test-late-with-failing-log"));
            client.WhenReady(_ => calls++);
            Assert(calls == 3, "failing log listeners must not break callback isolation");
            Assert(!client.TryGetGatehouseTiming(out _, out var unsupported) && unsupported.State == NativeCapabilityState.UnsupportedBuild,
                "global Ready does not imply native service support");
            Assert(typeof(IApiShared).Assembly.GetType("APIShared.UnitCommands.UnitCommandPathAPI").IsNotPublic,
                "specialized command runtime is not a public third-party contract");
            Assert(!typeof(IApiShared).Assembly.GetExportedTypes().Any(t => t.Namespace == "Shared"),
                "APIShared no longer exports historical Shared contracts");
        }
        [TestMethod]
        [TestCategory("Contracts")]
        public void VerifyReadinessAndIndependentCapabilities() => TestReadinessAndIndependentCapabilities();

        private static void TestReadinessAndIndependentCapabilities()
        {
            byte[] image = CreatePeImage(0x4000, true);
            var runtime = new ApiSharedRuntime();
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out NativeCapabilityDiagnostic originPending) &&
                originPending.State == NativeCapabilityState.Pending, "pre-initialization origin query should be Pending");
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic pending) &&
                pending.State == NativeCapabilityState.Pending, "pre-initialization query should be Pending");
            int readyBefore = 0;
            runtime.WhenReady(_ => readyBefore++);
            var memory = new FakeMemory();
            runtime.Initialize(ModuleBase, image, "UNKNOWN", memory, null, null, false);
            Assert(runtime.State == NativeApiState.Ready && readyBefore == 1, "unknown build should still publish Ready");
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic gate) &&
                gate.State == NativeCapabilityState.UnsupportedBuild, "unknown build should disable only gatehouse");
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out NativeCapabilityDiagnostic origin) &&
                origin.State == NativeCapabilityState.UnsupportedBuild, "unknown build should disable distance origin without mutation");
            Assert(memory.OperationCount == 0, "unknown build performs no native operation");
            int readyAfter = 0;
            runtime.WhenReady(_ => readyAfter++);
            Assert(readyAfter == 1, "post-initialization readiness callback should be synchronous");

            runtime = new ApiSharedRuntime();
            runtime.Initialize(0, ReadOnlySpan<byte>.Empty, string.Empty, new FakeMemory(), null, null, false);
            Assert(runtime.State == NativeApiState.Ready, "missing native module is a gate capability error, not a global failure");
            Assert(!runtime.TryGetGatehouseTiming("owner", out _, out NativeCapabilityDiagnostic missingHash) &&
                missingHash.State == NativeCapabilityState.UnsupportedBuild, "missing hash is unsupported for gatehouse");
            Assert(!runtime.TryGetGatehouseDistanceOrigin("owner", out _, out NativeCapabilityDiagnostic missingOriginHash) &&
                missingOriginHash.State == NativeCapabilityState.UnsupportedBuild, "missing hash is unsupported for distance origin");
        }
        [TestMethod]
        [TestCategory("Contracts")]
        public void VerifyOwnership() => TestOwnership();

        private static void TestOwnership()
        {
            var registry = new NativeOwnershipRegistry();
            var first = new[] { new NativeInterval(100, 110) };
            Assert(registry.TryReserve("A", "cap", NativeReservationMode.Exclusive, first, out _), "first reservation");
            Assert(registry.TryReserve("A", "cap", NativeReservationMode.Exclusive, first, out _), "same reservation is idempotent");
            Assert(registry.TryReserve("B", "other", NativeReservationMode.Exclusive,
                new[] { new NativeInterval(110, 120) }, out _), "adjacent half-open intervals do not overlap");
            Assert(!registry.TryReserve("C", "third", NativeReservationMode.Exclusive,
                new[] { new NativeInterval(109, 111) }, out string conflict) && conflict == "A",
                "exclusive overlap identifies the first owner");
        }

    }
}
