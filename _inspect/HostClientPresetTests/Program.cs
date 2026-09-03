using MessagePack;
using BugfixesAndQoL;
using ExtraFeatures;
using SerpsModsHost;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class Program
{
    private const int RemoteSentinel = 900001;
    private const int TrailSentinel = 900002;
    private const int AuthoritativeTrailSentinel = 900003;

    private static int Main()
    {
        try
        {
            TestLobbySettingsRouting();
            TestSharedPerPlayerLobbyConvergence();
            TestSharedLobbyLifecycle();
            TestFailedRegistrationStopsPerPlayerCoordinator();
            TestThrowingRegistrationStopsPerPlayerCoordinator();
            TestModSettingsHorizontalFocusScrollGuardRegistration();
            TestGameModeHelper();
            TestGameplayFeatureModePolicy();
            TestStartConditionsMapSessionState();
            TestGameplayGateSourceIntegration();
            TestLocalPerPlayerSetting();
            TestMarketGoodsOrderDefinition();
            TestResyncHostKickPolicy();
            TestAbruptHostMigrationPolicy();
            TestSelectedUnitHealthSummary();
            TestSurrenderAndStatisticsSettingAndPolicy();
            TestMultiplayerLobbyReturnPolicy();
            TestSteamLobbyInvitePolicy();
            TestSteamInviteBlacklistStore();
            TestMarketGoodPriceDefinition();
            TestAIMarketVanillaPricePolicy();
            TestEnemyProximityPolicy();
            TestAssassinClimbCancellationPolicy();
            TestAssassinClimbCostPolicy();
            TestAssassinCombatResumePolicy();
            TestAssassinCombatResumeNativeDefinition();
            TestAssassinPathReconstructionNativeDefinition();
            TestTroopActionButtonLayoutPolicy();
            TestLordHealthMultiplierPolicy();
            TestQuarryPileTargetSelectionPolicy();
            TestQuarryPileVanillaGroupPolicy();
            TestTemporaryGateBlockagePolicy();
            TestGatehouseAutomationSaveState();
            TestBoundedSaveStateDeserialization();
            TestPlagueFlagDiseaseRegistry();
            TestAIMarketNativeResolution();
            TestAiRecruitmentHorseDemandNativeResolution();
            TestAiStoneReserveNativeResolution();
            TestAiStoneReservePolicy();
            TestMultiplayerGameSpeedPolicyAndPacket();
            TestSiegeAmmoRestockPolicyAndPacket();
            TestMarketTradeIntegration();
            TestGameSpeedRepeatScheduler();
            TestArrayPerPlayerSetting();
            TestMarketOrderPresetRoundTrip();
            TestPresetLocalRoundTrip();
            TestDoNotPersistPresetExclusion();
            TestCastlePlannerBlueprintHudPolicies();
            TestCastleSpawnContentPolicy();
            TestSnapshotCompletionHook();
            TestFreeCastleProtocol();
            TestModSettingsSearchPolicy();
            TestUnitGoldCostSnapshot();
            TestSharedModSettingsSearchMatcher();

            string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestPlugin.dll");
            string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LobbyModSettings", "HostClientTest.msgpack");
            if (File.Exists(settingsPath))
                File.Delete(settingsPath);

            GameNetworkAPI.LocalHost = true;
            GameNetworkAPI.Networked = true;
            GameNetworkAPI.MultiplayerGame = true;
            var vm = new MixedViewModel();
            vm.PreparePresets(null, pluginPath, "HostClientTest");
            vm.ActivatePresets();
            vm.HostValue = 111;
            vm.ClientValue = 211;
            vm.LocalValue = 311;
            vm.SelectedPreset = 1;
            vm.HostValue = 122;
            vm.ClientValue = 222;
            vm.LocalValue = 322;
            vm.SelectedPreset = 0;

            AssertState(vm, host: true, mission: false, editable: false, true, true, true, "host normal");
            Check(vm.ActionsScopeNoticeVisibility == Noesis.Visibility.Visible, "mixed mod omitted the action-scope notice");
            Check(vm.ActionsScopeNoticeText.Contains("host settings"), "host received the client-only action-scope text");

            GameNetworkAPI.LocalHost = false;
            vm.System_RefreshSettingsAccess();
            AssertState(vm, host: false, mission: false, editable: false, false, true, true, "client normal");
            Check(vm.ActionsScopeNoticeText.Contains("only your local client settings"), "client received the host action-scope text");
            GameNetworkAPI.ThrowOnRoleQuery = true;
            vm.System_RefreshSettingsAccess();
            Check(!vm.IsLocalSettingsHost && !vm.CanEditHostSettings, "failed role query unlocked the client");
            GameNetworkAPI.ThrowOnRoleQuery = false;

            DateTime accessMarker = new DateTime(2020, 1, 2, 3, 4, 6, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(settingsPath, accessMarker);
            accessMarker = File.GetLastWriteTimeUtc(settingsPath);
            GameNetworkAPI.LocalHost = true;
            vm.System_RefreshSettingsAccess();
            Check(File.GetLastWriteTimeUtc(settingsPath) == accessMarker,
                "role/access notifications rewrote the msgpack file");
            GameNetworkAPI.LocalHost = false;
            vm.System_RefreshSettingsAccess();
            Check(File.GetLastWriteTimeUtc(settingsPath) == accessMarker,
                "restoring the client role rewrote the msgpack file");

            byte[] beforeReceive = File.ReadAllBytes(settingsPath);
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(vm, () => vm.HostValue = RemoteSentinel);
            byte[] afterReceive = File.ReadAllBytes(settingsPath);
            Check(beforeReceive.SequenceEqual(afterReceive), "incoming host sync wrote the msgpack file");
            Check(vm.HostValue == RemoteSentinel, "authorised incoming host sync was rejected");

            int revertNotifications = 0;
            int dependentRevertNotifications = 0;
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(vm.HostValue))
                    revertNotifications++;
                if (args.PropertyName == nameof(vm.HostValueText))
                    dependentRevertNotifications++;
            };
            byte[] beforeRejectedEdit = File.ReadAllBytes(settingsPath);
            vm.HostValue = RemoteSentinel + 1;
            Check(vm.HostValue == RemoteSentinel, "client mutated a host-only backing field");
            Check(revertNotifications == 1, "rejected client edit did not raise exactly one revert notification");
            Check(dependentRevertNotifications == 1, "rejected client edit did not revert its editable proxy property");
            Check(beforeRejectedEdit.SequenceEqual(File.ReadAllBytes(settingsPath)), "revert notification wrote the msgpack file");
            AssertNoSentinel(settingsPath, RemoteSentinel + 1);
            AssertStoredHostValues(settingsPath, 111, 111, 122);
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(vm, () => vm.HostValue = RemoteSentinel);

            vm.ClientValue = 233;
            vm.LocalValue = 333;
            Check(vm.ClientValue == 233, "client could not mutate its per-player setting");
            Check(vm.LocalValue == 333, "client could not mutate its preset-local setting");
            AssertNoSentinel(settingsPath, RemoteSentinel);
            AssertStoredHostValues(settingsPath, 111, 111, 122);

            vm.System_EnterMissionPreset(
                new Dictionary<string, byte[]> { [nameof(vm.HostValue)] = MessagePackSerializer.Serialize(TrailSentinel) },
                "Trail",
                editable: false);
            AssertState(vm, host: false, mission: true, editable: false, false, true, true, "client read-only Trail");
            AssertNoSentinel(settingsPath, TrailSentinel);
            byte[] beforeTrailReceive = File.ReadAllBytes(settingsPath);
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(
                vm,
                () => vm.HostValue = AuthoritativeTrailSentinel);
            Check(vm.HostValue == AuthoritativeTrailSentinel,
                "authorised host sync was rejected while the client had read-only Trail selected");
            Check(beforeTrailReceive.SequenceEqual(File.ReadAllBytes(settingsPath)),
                "authorised Trail host sync wrote the local msgpack file");
            AssertNoSentinel(settingsPath, AuthoritativeTrailSentinel);
            vm.HostValue = AuthoritativeTrailSentinel + 1;
            Check(vm.HostValue == AuthoritativeTrailSentinel,
                "client locally mutated a host-only value while read-only Trail was selected");
            vm.SelectedPreset = 1;
            Check(vm.HostValue == AuthoritativeTrailSentinel,
                "client preset changed the Trail-owned host property");
            Check(vm.ClientValue == 222 && vm.LocalValue == 322,
                "client preset did not apply personal settings inside the Trail context");
            vm.SelectedPreset = 0;
            Check(vm.HostValue == AuthoritativeTrailSentinel && vm.ClientValue == 233 && vm.LocalValue == 333,
                "client could not switch between personal presets inside the Trail context");
            vm.SelectedPreset = 2;
            Check(vm.HostValue == AuthoritativeTrailSentinel && vm.ClientValue == 233 && vm.LocalValue == 333,
                "restoring the Trail preset lost authoritative host state or changed personal client settings");
            vm.ClientValue = 244;
            vm.LocalValue = 344;
            AssertNoSentinel(settingsPath, TrailSentinel);
            AssertStoredHostValues(settingsPath, 111, 111, 122);

            vm.System_RefreshSettingsAccess();
            Check(vm.IsMissionPresetActive && !vm.CanEditHostSettings, "role refresh removed the Trail/client lock");
            vm.System_ExitMissionPreset();
            Check(!vm.IsMissionPresetActive && !vm.CanEditHostSettings, "Trail exit removed the multiplayer client lock");

            GameXAMLManagerAPI.Instance.ApplyNetworkSync(vm, () => vm.HostValue = RemoteSentinel + 2);
            vm.SelectedPreset = 1;
            Check(vm.HostValue == RemoteSentinel + 2, "client preset changed a host property");
            Check(vm.ClientValue == 222, "client preset did not apply its personal snapshot");
            Check(vm.LocalValue == 322, "client preset did not apply its preset-local snapshot");

            var compound = new CompoundViewModel();
            int originalMinimum = compound.Minimum;
            int originalMaximum = compound.Maximum;
            compound.Minimum = originalMaximum + 5;
            Check(compound.Minimum == originalMinimum && compound.Maximum == originalMaximum,
                "rejected compound setter mutated part of its min/max state");
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(compound, () => compound.Minimum = originalMaximum + 5);
            Check(compound.Minimum == originalMaximum + 5 && compound.Maximum == originalMaximum + 5,
                "authorised compound setter did not apply its complete min/max state");

            var table = new NestedTableViewModel();
            int originalCell = table.Row.Value;
            string originalSerialized = table.Serialized;
            table.Row.ValueText = (originalCell + 1).ToString();
            Check(table.Row.Value == originalCell && table.Serialized == originalSerialized,
                "rejected table edit mutated a row before owner authorisation");
            GameXAMLManagerAPI.Instance.ApplyNetworkSync(table, () => table.Serialized = "42");
            Check(table.Row.Value == 42 && table.Serialized == "42",
                "authorised table update did not apply row and owner state atomically");

            GameNetworkAPI.LocalHost = true;
            vm.System_RefreshSettingsAccess();
            vm.System_EnterMissionPreset(
                new Dictionary<string, byte[]> { [nameof(vm.HostValue)] = MessagePackSerializer.Serialize(TrailSentinel) },
                "Trail",
                editable: false);
            AssertState(vm, host: true, mission: true, editable: false, false, true, true, "host read-only Trail");
            vm.SelectedPreset = 0;
            Check(vm.IsMissionPresetActive && vm.CanEditHostSettings,
                "local preset remained locked inside the Trail context");
            Check(vm.SelectedPreset == 0 && vm.HostValue == 111 && vm.ClientValue == 244 && vm.LocalValue == 344,
                "local preset was not applied inside the read-only Trail context");
            vm.SelectedPreset = 2;
            Check(vm.SelectedPreset == 2 && vm.HostValue == TrailSentinel,
                "Trail preset could not be restored after selecting a local preset");
            vm.System_ExitMissionPreset();
            vm.System_EnterMissionPreset(
                new Dictionary<string, byte[]> { [nameof(vm.HostValue)] = MessagePackSerializer.Serialize(TrailSentinel) },
                "Trail",
                editable: true);
            AssertState(vm, host: true, mission: true, editable: true, true, true, true, "host editable Trail");

            GameNetworkAPI.LocalHost = false;
            vm.System_RefreshSettingsAccess();
            AssertState(vm, host: false, mission: true, editable: true, false, true, true, "client editable Trail");

            GameNetworkAPI.MultiplayerGame = false;
            GameNetworkAPI.Networked = true;
            GameNetworkAPI.LocalHost = true;
            var singleplayer = new MixedViewModel();
            string singleplayerPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "LobbyModSettings",
                "SingleplayerTrailTest.msgpack");
            if (File.Exists(singleplayerPath))
                File.Delete(singleplayerPath);
            singleplayer.PreparePresets(
                null,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SingleplayerTrailTest.dll"),
                "SingleplayerTrailTest");
            singleplayer.ActivatePresets();

            GameNetworkAPI.LocalHost = false;
            singleplayer.System_RefreshSettingsAccess();
            Check(!singleplayer.IsLocalSettingsHost && !singleplayer.CanEditHostSettings,
                "singleplayer game-mode detection overrode the Extender authority result");
            GameNetworkAPI.LocalHost = true;
            singleplayer.System_RefreshSettingsAccess();

            singleplayer.HostValue = 511;
            singleplayer.System_EnterMissionPreset(
                new Dictionary<string, byte[]> { [nameof(singleplayer.HostValue)] = MessagePackSerializer.Serialize(TrailSentinel) },
                "Trail",
                editable: false);
            AssertState(singleplayer, host: true, mission: true, editable: false, false, true, true, "singleplayer Trail selected");
            Check(singleplayer.HostReadOnlyNoticeVisibility == Noesis.Visibility.Collapsed,
                "singleplayer displayed the multiplayer host-read-only notice");
            Check(singleplayer.ActionsScopeNoticeVisibility == Noesis.Visibility.Collapsed,
                "singleplayer displayed the multiplayer preset-scope notice");
            singleplayer.HostValue = TrailSentinel + 1;
            Check(singleplayer.HostValue == TrailSentinel,
                "read-only Trail preset accepted a direct host-setting edit");

            singleplayer.SelectedPreset = 0;
            Check(singleplayer.IsMissionPresetActive && singleplayer.CanEditHostSettings,
                "local preset remained locked inside the singleplayer Trail context");
            singleplayer.HostValue = 522;
            Check(singleplayer.HostValue == 522,
                "singleplayer local preset rejected an editable host setting");
            singleplayer.SelectedPreset = 2;
            Check(!singleplayer.CanEditHostSettings && singleplayer.HostValue == TrailSentinel,
                "returning to Trail did not restore and lock the Trail snapshot");
            singleplayer.SelectedPreset = 0;
            Check(singleplayer.CanEditHostSettings && singleplayer.HostValue == 522,
                "edited singleplayer preset was not persisted across a Trail round-trip");

            GameNetworkAPI.MultiplayerGame = true;

            var activation = new ActivationViewModel();
            activation.PreparePresets(null, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Activation.dll"), "ActivationTest");
            GameNetworkAPI.LocalHost = true;
            activation.System_RefreshSettingsAccess();
            Check(activation.HasHostSettingsActivation && activation.HasClientSettingsActivation,
                "shared activation bindings did not detect both setting scopes");
            Check(activation.CanToggleHostSettings && activation.CanToggleClientSettings,
                "shared activation bindings were unexpectedly locked for the host");
            activation.HostSettingsEnabled = false;
            activation.ClientSettingsEnabled = false;
            Check(!activation.EnableMod && !activation.EnableClientFeatures,
                "shared activation bindings did not update the scope properties");
            GameNetworkAPI.LocalHost = false;
            activation.System_RefreshSettingsAccess();
            Check(!activation.CanToggleHostSettings && activation.CanToggleClientSettings,
                "shared activation bindings did not preserve host/client authority");

            var hostOnly = new HostOnlyViewModel();
            hostOnly.PreparePresets(null, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HostOnly.dll"), "HostOnlyTest");
            GameNetworkAPI.LocalHost = false;
            hostOnly.System_RefreshSettingsAccess();
            Check(!hostOnly.CanChangePreset, "pure host mod exposed a functional client preset");
            Check(hostOnly.HostReadOnlyNoticeVisibility == Noesis.Visibility.Visible, "pure host mod omitted its read-only notice");
            Check(hostOnly.ActionsScopeNoticeVisibility == Noesis.Visibility.Collapsed, "pure host mod displayed a client action-scope notice");
            Check(!hostOnly.HasHostSettingsActivation && !hostOnly.HostSettingsEnabled && !hostOnly.CanToggleHostSettings,
                "a host mod without an activation property exposed an enabled header switch");

            var conflicting = new ConflictingAttributesViewModel();
            conflicting.PreparePresets(null, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Conflicting.dll"), "ConflictingTest");
            Check(conflicting.HasHostSettings && !conflicting.HasClientSettings, "SyncHostOnly did not take precedence over a conflicting client attribute");

            Console.WriteLine("PASS: 1.42 routing, authority, game modes, Trail/client locks, presets, and MessagePack sentinels");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
    }

    private static void TestUnitGoldCostSnapshot()
    {
        Check(UnitCosts.UnitGoldCostSnapshotPolicy.SelectVanillaCost(true, 0, 75) == 75,
            "UnitCosts captured the current zero siege-tent cost instead of its Vanilla default");
        Check(UnitCosts.UnitGoldCostSnapshotPolicy.SelectVanillaCost(false, 40, 75) == 40,
            "UnitCosts replaced a regular unit cost with an unrelated siege-tent default");
        Check(UnitCosts.UnitGoldCostSnapshotPolicy.SelectVanillaCost(false, 0, 75) == 0,
            "UnitCosts did not preserve an explicit zero regular-unit cost");

        var snapshot = new UnitCosts.UnitGoldCostSnapshot<int>();
        Check(snapshot.CaptureIfMissing(1, () => 75, out Exception firstError) && firstError == null,
            "UnitCosts failed to capture an available Vanilla cost");
        Check(snapshot.CaptureIfMissing(1, () => 0, out Exception repeatError) && repeatError == null,
            "UnitCosts rejected an already captured Vanilla cost");
        Check(snapshot.TryGetValue(1, out int restoredCost) && restoredCost == 75,
            "UnitCosts overwrote the Vanilla snapshot while re-enabling the mod");

        Check(!snapshot.CaptureIfMissing(2, () => throw new InvalidOperationException("unavailable"), out Exception captureError) &&
              captureError is InvalidOperationException &&
              !snapshot.TryGetValue(2, out _),
            "UnitCosts stored a fabricated value after a failed Vanilla-cost read");
        Check(snapshot.CaptureIfMissing(2, () => 30, out Exception retryError) && retryError == null &&
              snapshot.TryGetValue(2, out int retriedCost) && retriedCost == 30,
            "UnitCosts did not retry a previously failed Vanilla-cost capture");
    }

    private static void TestModSettingsSearchPolicy()
    {
        Check(ModSettingsSearchPolicy.Rank("Gate closing distance", "Controls nearby enemies.", "gate closing", false) == 1,
            "mod-settings search did not rank a title prefix");
        Check(ModSettingsSearchPolicy.Rank("Gate closing distance", "Controls nearby enemies.", "GATE", false) < int.MaxValue,
            "mod-settings search became case-sensitive");
        Check(ModSettingsSearchPolicy.Rank("Apothecary range", "Search distance for plague clouds.", "plague distance", false) == int.MaxValue,
            "title-only search unexpectedly matched a tooltip");
        Check(ModSettingsSearchPolicy.Rank("Apothecary range", "Search distance for plague clouds.", "plague distance", true) == 3,
            "tooltip search did not require and match all query words");
        Check(ModSettingsSearchPolicy.Rank("Überbau-Regeln", "KI-Verhalten", "uberbau", false) < int.MaxValue,
            "localized search did not ignore diacritics");
        Check(ModSettingsSearchPolicy.Rank("Lord health", "Multiplier", "missing", true) == int.MaxValue,
            "mod-settings search returned an unrelated entry");
        Check(ModSettingsSearchPolicy.Rank("Buy prices", "Multiplier", "Market price multipliers", "market buy", false) < int.MaxValue,
            "mod-settings search did not combine section and setting titles");
        var matchedSections = new HashSet<string>(StringComparer.Ordinal) { "extra.section.market" };
        Check(ModSettingsSearchPolicy.ShouldIncludeCandidate(true, "extra.section.market", "extra.section.market", matchedSections),
            "global search omitted a directly matching section result");
        Check(!ModSettingsSearchPolicy.ShouldIncludeCandidate(false, "extra.market-buy", "extra.section.market", matchedSections),
            "global search did not suppress child settings for a pure section match");
        Check(ModSettingsSearchPolicy.ShouldIncludeCandidate(false, "extra.plague", "extra.section.plague", matchedSections),
            "global section deduplication suppressed an unrelated setting");
    }

    private static void TestSharedModSettingsSearchMatcher()
    {
        Check(ModSettingsSearchMatcher.IsMatch(" gate ", false, null, "gate", "Gate Closing", "Tooltip"),
            "shared search did not normalize whitespace or ignore title casing");
        Check(!ModSettingsSearchMatcher.IsMatch("plague", false, null, "range", "Apothecary Range", "Plague distance"),
            "shared search included tooltips while tooltip search was disabled");
        Check(ModSettingsSearchMatcher.IsMatch("plague", true, null, "range", "Apothecary Range", "Plague distance"),
            "shared search omitted tooltips while tooltip search was enabled");
        Check(ModSettingsSearchMatcher.IsMatch("uberbau regeln", false, null, "rules", "Überbau-Regeln", ""),
            "shared search did not normalize diacritics or multi-word text");
        Check(ModSettingsSearchMatcher.IsMatch("unrelated", false, "exact:key", "exact:key", "Duplicate title", ""),
            "shared exact-key search did not override normal text matching");
        Check(!ModSettingsSearchMatcher.IsMatch("Duplicate title", true, "missing:key", "exact:key", "Duplicate title", "Duplicate title"),
            "shared exact-key search fell back to duplicate title or tooltip text");
        Check(ModSettingsSearchMatcher.IsMatch(string.Empty, false, string.Empty, "key", "Title", "Tooltip"),
            "empty shared filter did not show all settings");
        Check(ModSettingsSearchMatcher.IsMatch(
                "market buy",
                false,
                string.Empty,
                "extra.market-buy",
                "Buy prices",
                "Multiplier",
                "extra.section.market",
                "Market price multipliers"),
            "shared search did not combine section and setting title terms");
        Check(ModSettingsSearchMatcher.IsMatch(
                "unrelated",
                false,
                "extra.section.market",
                "extra.market-buy",
                "Buy prices",
                "Multiplier",
                "extra.section.market",
                "Market price multipliers"),
            "shared exact-section search did not include a child setting");
        Check(!ModSettingsSearchMatcher.IsMatch(
                "market",
                false,
                "extra.section.other",
                "extra.market-buy",
                "Buy prices",
                "Multiplier",
                "extra.section.market",
                "Market price multipliers"),
            "shared exact-section search fell back to textual section matching");
        Check(ModSettingsSearchMatcher.IsSectionTitleMatch(" MARKET ", "Market price multipliers"),
            "shared section-title matching did not normalize case and whitespace");
    }

    private static void TestLobbySettingsRouting()
    {
        GameNetworkAPI.LocalHost = true;
        GameNetworkAPI.Networked = true;
        GameXAMLManagerAPI manager = GameXAMLManagerAPI.Instance;
        manager.ResetRoutingProbe();

        var viewModel = new RoutingProbeViewModel();
        manager.RegisterLobbyModSettings(
            new BepInEx.BaseUnityPlugin(),
            "RoutingProbe",
            viewModel,
            "unused.xaml");

        viewModel.UiOnlyValue = 2;
        viewModel.PresetOnlyValue = 3;
        Check(manager.BroadcastCount == 0 && manager.SaveCount == 0,
            "UI-only or PresetLocal notification entered the Extender protocol");

        viewModel.LocalValue = 4;
        Check(manager.BroadcastCount == 0 && manager.SaveCount == 1,
            "PersistLocal did not save exactly once without broadcasting");
        Check(manager.ReadStoredInt(nameof(viewModel.LocalValue)) == 4,
            "PersistLocal value was not stored");

        viewModel.TransientValue = 5;
        Check(manager.BroadcastCount == 1 && manager.SaveCount == 1,
            "sync plus DoNotPersist did not broadcast without storage");
        Check(!manager.HasStoredValue(nameof(viewModel.TransientValue)),
            "DoNotPersist value entered the storage snapshot");

        viewModel.PlayerValue = 6;
        Check(manager.BroadcastCount == 2 && manager.SaveCount == 2,
            "SyncPerPlayer did not broadcast and persist");

        manager.PrimeStoredInt(nameof(viewModel.HostValue), 17);
        manager.ResetRoutingCounts();
        GameNetworkAPI.LocalHost = false;
        manager.ApplyNetworkSync(viewModel, () => viewModel.HostValue = RemoteSentinel);
        Check(viewModel.HostValue == RemoteSentinel,
            "incoming host routing probe did not update runtime state");
        Check(manager.BroadcastCount == 0 && manager.SaveCount == 0,
            "incoming host routing probe was broadcast or persisted");

        viewModel.PlayerValue = 7;
        Check(manager.ReadStoredInt(nameof(viewModel.HostValue)) == 17,
            "client storage snapshot replaced the cached local host value");
        Check(manager.ReadStoredInt(nameof(viewModel.PlayerValue)) == 7,
            "client storage snapshot lost its per-player value");
    }

    private static void TestSharedPerPlayerLobbyConvergence()
    {
        GameNetworkAPI.LocalHost = false;
        GameNetworkAPI.Networked = true;
        GameNetworkAPI.MultiplayerGame = true;
        GameXAMLManagerAPI.Instance.ResetRoutingProbe();

        var viewModel = new SharedPerPlayerProbeViewModel();
        LobbyModSettingsPresetRegistration.Register(
            new BepInEx.BaseUnityPlugin(),
            null,
            "SharedPerPlayerProbe",
            viewModel,
            "unused.xaml");
        viewModel.Preference = new[] { 7, 8, 9 };
        viewModel.System_TestObservePerPlayerLobby(
            100,
            new Dictionary<int, ulong> { [1] = 11, [2] = 22 },
            false,
            2);

        Check(viewModel.LocalPlayerId == 2, "Shared did not bind the resolved local player ID");
        Check(viewModel.PreferenceData[1] == null, "Shared did not reset an unreported remote slot");
        Check(viewModel.PreferenceData[2].SequenceEqual(new[] { 7, 8, 9 }),
            "Shared did not publish the local personal value into its player slot");
        Check(!ReferenceEquals(viewModel.Preference, viewModel.PreferenceData[2]),
            "Shared aliased a mutable local value into the network companion array");
        Check(viewModel.RemoteDataChangeCount == 0,
            "Shared misreported its own stale-slot reset as a remote update");
        Check(!viewModel.IsPerPlayerLobbySettingsReady,
            "Shared reported readiness before the remote required value arrived");

        viewModel.PreferenceData[1] = new[] { 1, 2, 3 };
        viewModel.PreferenceData[2] = null;
        viewModel.System_TriggerUpdate(nameof(viewModel.PreferenceData));
        Check(!viewModel.IsPerPlayerLobbySettingsReady,
            "Shared reported readiness while the local required slot was empty");

        viewModel.Preference = new[] { 9, 8, 7 };
        Check(viewModel.PreferenceData[2].SequenceEqual(new[] { 9, 8, 7 }),
            "Shared did not mirror a local personal edit into the resolved companion slot");
        Check(!ReferenceEquals(viewModel.Preference, viewModel.PreferenceData[2]),
            "Shared aliased a mutable personal edit into the companion slot");
        Check(viewModel.RemoteDataChangeCount == 1,
            "Shared misreported its own local companion mirror as a remote update");
        Check(viewModel.IsPerPlayerLobbySettingsReady,
            "Shared did not refresh readiness after mirroring the final required local report");

        viewModel.System_RequestPerPlayerSettingsPublish();
        int lobbyChangesBeforeResolution = viewModel.LobbyChangeCount;
        viewModel.System_TestObservePerPlayerLobby(
            100,
            new Dictionary<int, ulong> { [1] = 11, [2] = 22 },
            true,
            2);
        Check(!viewModel.IsPerPlayerLobbySettingsReady,
            "Shared accepted a lobby with unresolved human identities");
        viewModel.System_TestObservePerPlayerLobby(
            100,
            new Dictionary<int, ulong> { [1] = 11, [2] = 22 },
            false,
            2);
        Check(viewModel.LobbyChangeCount == lobbyChangesBeforeResolution + 2 &&
              viewModel.LastLobbySnapshot != null &&
              !viewModel.LastLobbySnapshot.HasUnresolvedPlayers,
            "Shared did not publish resolution-only lobby state changes");

        int observationsBeforeMap = viewModel.ObservationCount;
        viewModel.System_RequestPerPlayerSettingsPublish();
        viewModel.System_TestObservePerPlayerLobby(
            null,
            null,
            false,
            2,
            preserveForMapTransition: true);
        Check(viewModel.ObservationCount == observationsBeforeMap,
            "Shared ran a domain settings observer during the map transition or active match");

        viewModel.PreferenceData[1] = new[] { 1, 2, 3 };
        viewModel.System_TriggerUpdate(nameof(viewModel.PreferenceData));
        Check(viewModel.RemoteDataChangeCount == 2,
            "Shared did not forward a real remote companion-array update");
        viewModel.System_TestObservePerPlayerLobby(
            100,
            new Dictionary<int, ulong> { [1] = 11, [2] = 22 },
            false,
            2);
        Check(viewModel.IsPerPlayerLobbySettingsReady,
            "Shared did not recognize a complete required per-player snapshot");

        int broadcastsBeforeSlotRemap = GameXAMLManagerAPI.Instance.BroadcastCount;
        Check(viewModel.System_TestRemapPerPlayerLobbyForMapTransition(
                new Dictionary<int, ulong> { [1] = 11, [3] = 22 },
                3,
                out string slotRemapError),
            "Shared could not remap lobby slots to final game slots: " + slotRemapError);
        Check(viewModel.LocalPlayerId == 3 &&
              viewModel.PreferenceData[2] == null &&
              viewModel.PreferenceData[3].SequenceEqual(new[] { 9, 8, 7 }),
            "Shared did not move personal settings with their Steam identity to the final game slot");
        Check(viewModel.System_ArePerPlayerSettingsReady(new[] { 1, 3 }, out _),
            "Shared did not accept the remapped final multiplayer roster");
        Check(GameXAMLManagerAPI.Instance.BroadcastCount == broadcastsBeforeSlotRemap,
            "Shared broadcast a local-only map-transition slot remap");
        Check(viewModel.System_TestRemapPerPlayerLobbyForMapTransition(
                new Dictionary<int, ulong> { [1] = 11, [2] = 22 },
                2,
                out slotRemapError),
            "Shared could not restore the synthetic lobby slots after the remap test: " + slotRemapError);

        viewModel.System_TestObservePerPlayerLobby(
            100,
            new Dictionary<int, ulong> { [1] = 33, [2] = 22 },
            false,
            2);
        Check(viewModel.PreferenceData[1] == null,
            "Shared retained a value after the same player slot was assigned to a different Steam user");
        Check(!viewModel.System_ArePerPlayerSettingsReady(new[] { 2 }, out _),
            "Shared accepted a requested player list that did not match the lobby roster");

        viewModel.System_TestObservePerPlayerLobby(null, null, false, 2);
        Check(viewModel.PreferenceData.Skip(1).All(value => value == null),
            "Shared retained per-player values after leaving the lobby");
        Check(viewModel.LastLobbySnapshot != null && !viewModel.LastLobbySnapshot.LobbyId.HasValue,
            "Shared did not publish an empty snapshot after leaving the lobby");

        ExpectInvalidPerPlayerRegistration(
            new MissingCompanionViewModel(),
            "missing companion array");
        ExpectInvalidPerPlayerRegistration(
            new ConflictingPerPlayerViewModel(),
            "conflicting classifications");
        ExpectInvalidPerPlayerRegistration(
            new MultidimensionalCompanionViewModel(),
            "multidimensional companion array");
        ExpectInvalidPerPlayerRegistration(
            new UnstableCompanionViewModel(),
            "unstable companion array instance");

        GameNetworkAPI.LocalHost = true;
    }

    private static void TestFailedRegistrationStopsPerPlayerCoordinator()
    {
        GameXAMLManagerAPI.Instance.ResetRoutingProbe();
        GameXAMLManagerAPI.Instance.FailNextRegistration = true;
        var viewModel = new SharedPerPlayerProbeViewModel();
        LobbyModSettingsPresetRegistration.Register(
            new BepInEx.BaseUnityPlugin(),
            null,
            "FailedSharedPerPlayerProbe",
            viewModel,
            "missing.xaml");

        viewModel.System_TestObservePerPlayerLobby(
            200,
            new Dictionary<int, ulong> { [1] = 11 },
            false,
            1);
        Check(viewModel.LocalPlayerId == 0 && viewModel.LastLobbySnapshot == null,
            "Shared left the per-player coordinator active after lobby-settings registration failed");
    }

    private static void TestThrowingRegistrationStopsPerPlayerCoordinator()
    {
        GameXAMLManagerAPI.Instance.ResetRoutingProbe();
        GameXAMLManagerAPI.Instance.ThrowNextRegistration = true;
        var viewModel = new SharedPerPlayerProbeViewModel();
        bool threw = false;
        try
        {
            LobbyModSettingsPresetRegistration.Register(
                new BepInEx.BaseUnityPlugin(),
                null,
                "ThrowingSharedPerPlayerProbe",
                viewModel,
                "missing.xaml");
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Check(threw, "Shared swallowed a lobby-settings registration exception");
        viewModel.System_TestObservePerPlayerLobby(
            201,
            new Dictionary<int, ulong> { [1] = 11 },
            false,
            1);
        Check(viewModel.LocalPlayerId == 0 && viewModel.LastLobbySnapshot == null,
            "Shared left the per-player coordinator active after lobby-settings registration threw");
    }

    private static void TestModSettingsHorizontalFocusScrollGuardRegistration()
    {
        GameXAMLManagerAPI.Instance.ResetRoutingProbe();
        ModSettingsHorizontalFocusScrollGuard.ResetForTests();

        var firstViewModel = new HostOnlyViewModel();
        LobbyModSettingsPresetRegistration.Register(
            new BepInEx.BaseUnityPlugin(),
            null,
            "HorizontalFocusGuardOne",
            firstViewModel,
            "one.xaml");
        Check(ModSettingsHorizontalFocusScrollGuard.AttachedViewCount == 1,
            "Shared did not attach the horizontal focus-scroll guard to a registered view");

        object firstView = GameXAMLManagerAPI.Instance.RegisteredModSettings[0].View;
        Check(!ModSettingsHorizontalFocusScrollGuard.Attach(
                firstView,
                null,
                "HorizontalFocusGuardOne"),
            "Shared attached the horizontal focus-scroll guard twice to the same view");

        LobbyModSettingsPresetRegistration.Register(
            new BepInEx.BaseUnityPlugin(),
            null,
            "HorizontalFocusGuardTwo",
            new HostOnlyViewModel(),
            "two.xaml");
        Check(ModSettingsHorizontalFocusScrollGuard.AttachedViewCount == 2,
            "Shared did not attach the horizontal focus-scroll guard to every registered view");
    }

    private static void ExpectInvalidPerPlayerRegistration(
        PresetLobbyModSettingsViewModel viewModel,
        string scenario)
    {
        bool rejected = false;
        try
        {
            LobbyModSettingsPresetRegistration.Register(
                new BepInEx.BaseUnityPlugin(),
                null,
                "Invalid" + viewModel.GetType().Name,
                viewModel,
                "unused.xaml");
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Check(rejected, "Shared did not reject " + scenario);
    }

    private static void TestPlagueFlagDiseaseRegistry()
    {
        var registry = new PlagueFlagDiseaseRegistry();
        registry.Track(7, 7001);
        registry.Track(2, 2001);
        Check(registry.Count == 2 && registry.ContainsGlobalId(7001) && registry.ContainsGlobalId(2001),
            "AI flag disease identities were not tracked");

        registry.Track(7, 7002);
        Check(!registry.ContainsGlobalId(7001) && registry.ContainsGlobalId(7002),
            "projectile slot reuse retained the stale global ID");
        PlagueFlagDiseaseIdentity[] snapshot = registry.Snapshot();
        Check(snapshot.Length == 2 && snapshot[0].SlotId == 2 && snapshot[1].SlotId == 7,
            "AI flag disease snapshot was not deterministic");

        var records = snapshot.Select(identity => new PlagueFlagDiseaseSaveRecord
        {
            SlotId = identity.SlotId,
            GlobalId = identity.GlobalId
        }).ToArray();
        byte[] bytes = MessagePackSerializer.Serialize(new PlagueFlagDiseaseSaveState { Projectiles = records });
        PlagueFlagDiseaseSaveState roundTrip = MessagePackSerializer.Deserialize<PlagueFlagDiseaseSaveState>(bytes);
        var restoredIdentities = roundTrip.Projectiles
            .Select(record => new PlagueFlagDiseaseIdentity(record.SlotId, record.GlobalId))
            .ToArray();
        var restored = new PlagueFlagDiseaseRegistry();
        restored.Restore(restoredIdentities);
        Check(restored.Count == 2 && restored.ContainsGlobalId(2001) && restored.ContainsGlobalId(7002),
            "AI flag disease save state did not round-trip");

        restored.RemoveSlot(2);
        Check(restored.Count == 1 && !restored.ContainsGlobalId(2001),
            "projectile deletion did not remove the tracked identity");

        bool duplicateRejected = false;
        try
        {
            restored.Restore(new[]
            {
                new PlagueFlagDiseaseIdentity(3, 3001),
                new PlagueFlagDiseaseIdentity(4, 3001)
            });
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        Check(duplicateRejected && restored.Count == 0,
            "duplicate AI flag disease identities were not rejected fail-closed");
    }

    private static void TestGatehouseAutomationSaveState()
    {
        var locator = new GatehouseMapLocator
        {
            OwnerPlayerId = 3,
            BuildingType = 77,
            TileXBegin = 120,
            TileYBegin = 240,
            TileXEnd = 124,
            TileYEnd = 248
        };
        var state = new GatehouseAutomationSaveState
        {
            Version = GatehouseAutomationSaveState.CurrentVersion,
            ManualOnlyGateGlobalIds = Array.Empty<int>(),
            ManualOnlyGateLocators = new[] { locator }
        };

        byte[] bytes = MessagePackSerializer.Serialize(state);
        GatehouseAutomationSaveState roundTrip = MessagePackSerializer.Deserialize<GatehouseAutomationSaveState>(bytes);
        Check(roundTrip.Version == 2, "gatehouse v2 version did not round-trip");
        Check(roundTrip.ManualOnlyGateGlobalIds != null && roundTrip.ManualOnlyGateGlobalIds.Length == 0,
            "gatehouse empty global-ID state was not serialized explicitly");
        Check(roundTrip.ManualOnlyGateLocators != null && roundTrip.ManualOnlyGateLocators.Length == 1,
            "gatehouse map locator did not round-trip");
        GatehouseMapLocator restored = roundTrip.ManualOnlyGateLocators[0];
        Check(restored.OwnerPlayerId == locator.OwnerPlayerId && restored.BuildingType == locator.BuildingType &&
            restored.TileXBegin == locator.TileXBegin && restored.TileYBegin == locator.TileYBegin &&
            restored.TileXEnd == locator.TileXEnd && restored.TileYEnd == locator.TileYEnd,
            "gatehouse map locator identity changed during serialization");
        Check(locator.HasValidShape, "valid gatehouse map locator was rejected");
        Check(!new GatehouseMapLocator { OwnerPlayerId = 0, BuildingType = 77 }.HasValidShape,
            "gatehouse locator accepted an invalid owner");
        Check(!new GatehouseMapLocator { OwnerPlayerId = 1, BuildingType = 0 }.HasValidShape,
            "gatehouse locator accepted an invalid building type");
        Check(locator.IdentityKey == restored.IdentityKey,
            "gatehouse locator uniqueness key changed during serialization");
        var differentTileLocator = new GatehouseMapLocator
        {
            OwnerPlayerId = locator.OwnerPlayerId,
            BuildingType = locator.BuildingType,
            TileXBegin = locator.TileXBegin + 1,
            TileYBegin = locator.TileYBegin,
            TileXEnd = locator.TileXEnd + 1,
            TileYEnd = locator.TileYEnd
        };
        Check(locator.IdentityKey != differentTileLocator.IdentityKey,
            "different gatehouse positions produced the same uniqueness key");

        var emptyState = new GatehouseAutomationSaveState
        {
            Version = GatehouseAutomationSaveState.CurrentVersion,
            ManualOnlyGateGlobalIds = Array.Empty<int>(),
            ManualOnlyGateLocators = Array.Empty<GatehouseMapLocator>()
        };
        GatehouseAutomationSaveState emptyRoundTrip = MessagePackSerializer.Deserialize<GatehouseAutomationSaveState>(
            MessagePackSerializer.Serialize(emptyState));
        Check(emptyRoundTrip.ManualOnlyGateGlobalIds.Length == 0 && emptyRoundTrip.ManualOnlyGateLocators.Length == 0,
            "gatehouse empty state was lost instead of overwriting stale archive data");

        byte[] legacyBytes = { 0x92, 0x01, 0x92, 0x0C, 0x22 };
        GatehouseAutomationSaveState legacy = MessagePackSerializer.Deserialize<GatehouseAutomationSaveState>(legacyBytes);
        Check(legacy.Version == 1 && legacy.ManualOnlyGateGlobalIds.SequenceEqual(new[] { 12, 34 }) &&
            legacy.ManualOnlyGateLocators == null,
            "gatehouse v1 save payload is no longer readable");

        var changedRuntimeIds = new GatehouseAutomationSaveState
        {
            Version = 2,
            ManualOnlyGateGlobalIds = new[] { 900001 },
            ManualOnlyGateLocators = new[] { locator }
        };
        GatehouseAutomationSaveState changedIdsRoundTrip = MessagePackSerializer.Deserialize<GatehouseAutomationSaveState>(
            MessagePackSerializer.Serialize(changedRuntimeIds));
        Check(changedIdsRoundTrip.ManualOnlyGateGlobalIds[0] == 900001 &&
            changedIdsRoundTrip.ManualOnlyGateLocators[0].TileXBegin == 120,
            "gatehouse map identity became dependent on the runtime global ID");
    }

    private static void TestBoundedSaveStateDeserialization()
    {
        ExpectMessagePackFailure(
            () => MessagePackSerializer.Deserialize<GatehouseAutomationSaveState>(
                new byte[] { 0x92, 0x01, 0xDD, 0x7F, 0xFF, 0xFF, 0xFF }),
            "gatehouse int.MaxValue array header was not rejected before allocation");
        ExpectMessagePackFailure(
            () => MessagePackSerializer.Deserialize<GatehouseAutomationSaveState>(
                new byte[] { 0x91, 0x02 }),
            "gatehouse root field count was not validated");
        ExpectMessagePackFailure(
            () => MessagePackSerializer.Deserialize<GatehouseAutomationSaveState>(
                new byte[] { 0x93, 0x02, 0x90, 0xDD, 0x7F, 0xFF, 0xFF, 0xFF }),
            "gatehouse int.MaxValue locator header was not rejected before allocation");

        int[] extendedGateIds = Enumerable.Range(1, 10001).ToArray();
        var extendedGateState = new GatehouseAutomationSaveState
        {
            Version = GatehouseAutomationSaveState.CurrentVersion,
            ManualOnlyGateGlobalIds = extendedGateIds,
            ManualOnlyGateLocators = Array.Empty<GatehouseMapLocator>()
        };
        ExpectMessagePackFailure(
            () => MessagePackSerializer.Serialize(extendedGateState),
            "gatehouse default save limit accepted Maximum+1");
        using (GatehouseAutomationSaveLimitPolicy.Register(
            "tests.extended-gatehouses",
            () => new GatehouseAutomationSaveLimits(extendedGateIds.Length)))
        {
            GatehouseAutomationSaveState restored =
                MessagePackSerializer.Deserialize<GatehouseAutomationSaveState>(
                    MessagePackSerializer.Serialize(extendedGateState));
            Check(restored.ManualOnlyGateGlobalIds.Length == extendedGateIds.Length,
                "registered dynamic gatehouse save limit was not honored");
        }
        Check(GatehouseAutomationSaveLimitPolicy.GetCurrent().MaximumSavedGatehouses == 10000,
            "disposed gatehouse save-limit provider remained active");

        ExpectMessagePackFailure(
            () => MessagePackSerializer.Deserialize<PlaguePopularitySaveState>(
                new byte[] { 0x93, 0x01, 0xDD, 0x7F, 0xFF, 0xFF, 0xFF }),
            "plague int.MaxValue managed-player header was not rejected before allocation");
        ExpectMessagePackFailure(
            () => MessagePackSerializer.Deserialize<PlaguePopularitySaveState>(
                new byte[] { 0x92, 0x01, 0x90 }),
            "plague root field count was not validated");
        ExpectMessagePackFailure(
            () => MessagePackSerializer.Deserialize<PlaguePopularitySaveState>(
                new byte[] { 0x93, 0x01, 0x90, 0xDD, 0x7F, 0xFF, 0xFF, 0xFF }),
            "plague int.MaxValue herd header was not rejected before allocation");
        ExpectMessagePackFailure(
            () => MessagePackSerializer.Deserialize<PlaguePopularitySaveState>(
                new byte[] { 0x93, 0x01, 0x90, 0x91, 0x93, 0x01, 0xDD, 0x7F, 0xFF, 0xFF, 0xFF }),
            "plague int.MaxValue projectile header was not rejected before allocation");
        ExpectMessagePackFailure(
            () => MessagePackSerializer.Deserialize<PlaguePopularitySaveState>(
                new byte[] { 0x93, 0x01, 0x90, 0x91, 0xC0 }),
            "plague null herd record was not rejected");
        ExpectMessagePackFailure(
            () => MessagePackSerializer.Deserialize<PlaguePopularitySaveState>(
                new byte[] { 0x93, 0x01, 0x90, 0x91, 0x93, 0x01, 0x91, 0x01, 0x92 }),
            "plague parallel projectile arrays with different lengths were not rejected at the second header");

        int[] slots = Enumerable.Range(1, 11).ToArray();
        uint[] globals = slots.Select(value => (uint)(1000 + value)).ToArray();
        var extendedPlagueState = new PlaguePopularitySaveState
        {
            ManagedPlayerIds = new[] { 1 },
            Herds = new[]
            {
                new PlagueHerdSaveRecord
                {
                    PlayerId = 1,
                    ProjectileSlotIds = slots,
                    ProjectileGlobalIds = globals
                }
            }
        };
        ExpectMessagePackFailure(
            () => MessagePackSerializer.Serialize(extendedPlagueState),
            "plague default projectiles-per-herd limit accepted Maximum+1");
        int dynamicProjectilesPerHerd = 11;
        using (PlaguePopularitySaveLimitPolicy.Register(
            "tests.extended-plague-herd",
            () => new PlaguePopularitySaveLimits(9, 4097, dynamicProjectilesPerHerd, 10001, 10001)))
        {
            PlaguePopularitySaveLimits activeLimits = PlaguePopularitySaveLimitPolicy.GetCurrent();
            Check(activeLimits.MaximumManagedPlayers == 9 && activeLimits.MaximumHerds == 4097 &&
                activeLimits.MaximumProjectilesPerHerd == 11 && activeLimits.MaximumTotalProjectiles == 10001 &&
                activeLimits.MaximumProjectileSlotId == 10001,
                "registered plague save-limit provider did not vary every upper bound");
            PlaguePopularitySaveState restored =
                MessagePackSerializer.Deserialize<PlaguePopularitySaveState>(
                    MessagePackSerializer.Serialize(extendedPlagueState));
            Check(restored.Herds[0].ProjectileSlotIds.Length == dynamicProjectilesPerHerd,
                "registered dynamic plague save limit was not honored");
        }
        Check(PlaguePopularitySaveLimitPolicy.GetCurrent().MaximumProjectilesPerHerd == 10,
            "disposed plague save-limit provider remained active");
    }

    private static void ExpectMessagePackFailure(Action action, string message)
    {
        try
        {
            action();
        }
        catch (MessagePackSerializationException)
        {
            return;
        }
        Check(false, message);
    }

    private static void TestResyncHostKickPolicy()
    {
        DateTime now = new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);
        var candidates = new[]
        {
            new ResyncHostKickCandidate(1, "Self", now.AddMinutes(-1), true, true, false),
            new ResyncHostKickCandidate(2, "Fresh", now.AddSeconds(-4), false, true, false),
            new ResyncHostKickCandidate(3, "AI", now.AddMinutes(-2), false, false, false),
            new ResyncHostKickCandidate(4, "Kicked", now.AddMinutes(-3), false, true, true)
        };
        Check(!ResyncHostKickPolicy.TrySelect(candidates, now, out _),
            "resync host kick selected an ineligible or fresh player");

        var stale = new[]
        {
            new ResyncHostKickCandidate(5, "Later", now.AddSeconds(-8), false, true, false),
            new ResyncHostKickCandidate(4, "OldestTieHigh", now.AddSeconds(-12), false, true, false),
            new ResyncHostKickCandidate(2, "OldestTieLow", now.AddSeconds(-12), false, true, false),
            new ResyncHostKickCandidate(6, "Uninitialized", DateTime.MaxValue, false, true, false)
        };
        Check(ResyncHostKickPolicy.TrySelect(stale, now, out ResyncHostKickCandidate selected),
            "resync host kick did not select an overdue human player");
        Check(selected.PlayerId == 2 && selected.PlayerName == "OldestTieLow",
            "resync host kick did not select the oldest heartbeat with deterministic player-ID tie-breaking");

        var boundary = new[]
        {
            new ResyncHostKickCandidate(7, "Boundary", now - ResyncHostKickPolicy.HeartbeatTimeout, false, true, false)
        };
        Check(!ResyncHostKickPolicy.TrySelect(boundary, now, out _),
            "resync host kick treated the exact timeout boundary as overdue");
    }

    private static void TestAbruptHostMigrationPolicy()
    {
        var soleLocalSurvivor = new[]
        {
            new AbruptHostMigrationCandidate(1, false, true, true, false, false),
            new AbruptHostMigrationCandidate(2, true, false, true, false, false),
            new AbruptHostMigrationCandidate(3, false, false, false, false, false)
        };
        Check(AbruptHostMigrationPolicy.TrySelectLocalSuccessor(
                  true, 1, false, true, true, false, false,
                  soleLocalSurvivor, out int successorPlayerId) && successorPlayerId == 2,
            "abrupt host migration did not select the sole local human survivor");

        Check(!AbruptHostMigrationPolicy.TrySelectLocalSuccessor(
                  false, 1, false, true, true, false, false,
                  soleLocalSurvivor, out _),
            "disabled abrupt host migration selected a successor");
        Check(!AbruptHostMigrationPolicy.TrySelectLocalSuccessor(
                  true, 1, false, false, true, false, false,
                  soleLocalSurvivor, out _),
            "abrupt host migration accepted a departing non-host");
        Check(!AbruptHostMigrationPolicy.TrySelectLocalSuccessor(
                  true, 1, false, true, false, false, false,
                  soleLocalSurvivor, out _),
            "abrupt host migration accepted a non-human departing host");
        Check(!AbruptHostMigrationPolicy.TrySelectLocalSuccessor(
                  true, 1, false, true, true, true, false,
                  soleLocalSurvivor, out _),
            "abrupt host migration repeated after the host was already kicked");
        Check(!AbruptHostMigrationPolicy.TrySelectLocalSuccessor(
                  true, 1, false, true, true, false, true,
                  soleLocalSurvivor, out _),
            "abrupt host migration repeated for a pending host kick");
        Check(!AbruptHostMigrationPolicy.TrySelectLocalSuccessor(
                  true, 0, false, true, true, false, false,
                  soleLocalSurvivor, out _),
            "abrupt host migration accepted an invalid departing player ID");

        var remoteSurvivor = new[]
        {
            new AbruptHostMigrationCandidate(1, false, true, true, false, false),
            new AbruptHostMigrationCandidate(2, false, false, true, false, false)
        };
        Check(!AbruptHostMigrationPolicy.TrySelectLocalSuccessor(
                  true, 1, false, true, true, false, false,
                  remoteSurvivor, out _),
            "abrupt host migration promoted a remote successor");

        var multipleSurvivors = new[]
        {
            new AbruptHostMigrationCandidate(1, false, true, true, false, false),
            new AbruptHostMigrationCandidate(2, true, false, true, false, false),
            new AbruptHostMigrationCandidate(3, false, false, true, false, false)
        };
        Check(!AbruptHostMigrationPolicy.TrySelectLocalSuccessor(
                  true, 1, false, true, true, false, false,
                  multipleSurvivors, out _),
            "abrupt host migration replaced Vanilla's multi-survivor selection");

        var ineligibleSurvivors = new[]
        {
            new AbruptHostMigrationCandidate(1, false, true, true, false, false),
            new AbruptHostMigrationCandidate(2, true, false, false, false, false),
            new AbruptHostMigrationCandidate(3, true, false, true, true, false),
            new AbruptHostMigrationCandidate(4, true, false, true, false, true),
            new AbruptHostMigrationCandidate(0, true, false, true, false, false)
        };
        Check(!AbruptHostMigrationPolicy.TrySelectLocalSuccessor(
                  true, 1, false, true, true, false, false,
                  ineligibleSurvivors, out _),
            "abrupt host migration selected an AI, kicked, pending, or invalid survivor");
    }

    private static void TestGameModeHelper()
    {
        CrusaderDE.MainViewModel.Reset();
        GamePlayerManagerAPI.Instance.MapEditor = false;
        Check(!GameModeHelper.IsMapEditor() && CrusaderDE.MainViewModel.InstanceReadCount == 0,
            "early map-editor detection constructed MainViewModel before viewModelLoaded");
        GamePlayerManagerAPI.Instance.MapEditor = true;
        Check(GameModeHelper.IsMapEditor(),
            "Script Extender map-editor state was not recognized");
        GamePlayerManagerAPI.Instance.MapEditor = false;
        CrusaderDE.MainViewModel.viewModelLoaded = true;
        CrusaderDE.MainViewModel.Instance.IsMapEditorMode = true;
        Check(GameModeHelper.IsMapEditor(),
            "loaded MainViewModel map-editor state was not recognized");
        CrusaderDE.MainViewModel.Reset();

        Platform_Multiplayer platform = Platform_Multiplayer.Instance;
        platform.activeLobby = null;
        platform.gameMembers = null;
        Director.instance = null;
        GameNetworkAPI.Networked = true;
        GameNetworkAPI.MultiplayerGame = false;
        GameData.Instance = new GameData
        {
            game_type = (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER,
            SkirmishGameType = (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM,
            coopTrailID = 0
        };

        GameModeSnapshot skirmish = GameModeHelper.Capture();
        Check(skirmish.LowLevelNetworked && !skirmish.IsRealMultiplayer &&
              skirmish.IsSingleplayerSkirmish &&
              skirmish.Kind == GameModeKind.CustomGame &&
              skirmish.AllowsCustomGameMods && skirmish.AllowsRegularGameplayMods,
            "local skirmish was misclassified as multiplayer");

        GameData.Instance.SkirmishGameType = (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_TRAIL;
        GameData.Instance.SkirmishTrailType = (int)GameTrailType.FirstEdition;
        GameModeSnapshot trail = GameModeHelper.Capture();
        Check(!trail.IsRealMultiplayer && trail.IsSingleplayerTrail &&
              trail.Kind == GameModeKind.VanillaTrail && !trail.AllowsCustomGameMods &&
              !trail.AllowsRegularGameplayMods,
            "singleplayer Trail was not recognized");

        foreach (GameModeKind blockedKind in new[]
        {
            GameModeKind.Unknown,
            GameModeKind.Campaign,
            GameModeKind.StandaloneMission
        })
        {
            Check(!GameModeHelper.AllowsRegularGameplayMods(
                      blockedKind, GameModeLaunchVariant.Standard) &&
                  !GameModeHelper.AllowsRegularGameplayMods(
                      blockedKind, GameModeLaunchVariant.Customized),
                $"regular gameplay policy accepted contradictory mode {blockedKind}");
        }
        foreach (GameModeKind customizableKind in new[]
        {
            GameModeKind.VanillaTrail,
            GameModeKind.CustomTrail,
            GameModeKind.CoopTrail,
            GameModeKind.SandsOfTime
        })
        {
            Check(!GameModeHelper.AllowsRegularGameplayMods(
                      customizableKind, GameModeLaunchVariant.Standard) &&
                  GameModeHelper.AllowsRegularGameplayMods(
                      customizableKind, GameModeLaunchVariant.Customized) &&
                  GameModeHelper.AllowsRegularGameplayMods(
                      customizableKind, GameModeLaunchVariant.RestoredCustomizedSave),
                $"regular gameplay policy mishandled Customize variants for {customizableKind}");
        }
        Check(GameModeHelper.AllowsRegularGameplayMods(
                  GameModeKind.CustomGame, GameModeLaunchVariant.Standard) &&
              GameModeHelper.AllowsRegularGameplayMods(
                  GameModeKind.MapEditor, GameModeLaunchVariant.Standard),
            "regular gameplay policy rejected Custom Game or Map Editor");

        string[] gameplayModGuids =
        {
            "BuildingCosts_Serp", "BuildingLimit_Serp", "CastlePlanner_Serp",
            "CheatMod_Serp", "ExtraFeatures_Serp", "ExtremePowers_Serp",
            "ImprovedHunters_Serp", "RandomEvents_Serp", "StartConditions_Serp",
            "UnitCosts_Serp", "UnitLimit_Serp"
        };
        foreach (string modGuid in gameplayModGuids)
        {
            GameplayModActivationProfile profile = GameplayModModePolicy.GetProfile(modGuid, modGuid);
            Check(profile.ModGuid == modGuid &&
                  GameplayModModePolicy.IsAllowed(profile, skirmish, out string customReason) &&
                  customReason == "custom-game" &&
                  !GameplayModModePolicy.IsAllowed(profile, trail, out _),
                $"typed gameplay profile is incorrect for {modGuid}");
        }
        bool unknownProfileRejected = false;
        try { GameplayModModePolicy.GetProfile("Unknown_Serp", "Unknown"); }
        catch (ArgumentOutOfRangeException) { unknownProfileRejected = true; }
        Check(unknownProfileRejected, "unknown gameplay-mod GUID received a permissive profile");

        Check(GameModeHelper.ResolveKind(false,
                  (int)Enums.eGameTypeModes.GAMETYPE_CAMPAIGN,
                  (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_NOT_SKIRMISH,
                  -1, 0) == GameModeKind.Campaign,
            "campaign was not classified from Vanilla's game-type enum");
        Check(GameModeHelper.ResolveKind(false,
                  (int)Enums.eGameTypeModes.GAMETYPE_MAP,
                  (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_NOT_SKIRMISH,
                  -1, 0) == GameModeKind.StandaloneMission,
            "standalone mission was not classified separately");
        Check(GameModeHelper.ResolveKind(false, -1, -1, -1, 0,
                  campaignMapId: 7) == GameModeKind.Campaign,
            "campaign event data was not classified");
        Check(GameModeHelper.ResolveKind(false,
                  (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER,
                  (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_TRAIL,
                  (int)GameTrailType.SandsEight, 0) == GameModeKind.SandsOfTime,
            "Sands of Time was not classified from its named Trail type");
        Check(GameModeHelper.ResolveKind(false,
                  (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER,
                  (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL,
                  -1, 0) == GameModeKind.CustomTrail,
            "Custom Trail was not classified");
        Check(GameModeHelper.ResolveKind(false,
                  (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER,
                  (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM,
                  -1, 2) == GameModeKind.CoopTrail,
            "Coop Trail was not classified before multiplayer state");
        Check(GameModeHelper.ResolveKind(true,
                  (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER,
                  (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM,
                  -1, 0) == GameModeKind.MapEditor,
            "Map Editor did not take precedence");
        GameData.Instance = new GameData { mapType = Enums.GameModes.MAP_EDITOR };
        Check(GameModeHelper.Capture().Kind == GameModeKind.MapEditor,
            "Vanilla's named map-type enum did not identify Map Editor");
        Check(GameModeHelper.ResolveKind(false, -1, -1, -1, -1) == GameModeKind.Unknown,
            "incomplete mode evidence did not fail closed");

        var restoredCustomTrail = new ExternalCustomizedOrigin(
            ExternalCustomizedOrigin.CustomTrail, -1, 90, 1, restoredFromSave: true);
        Check(GameModeHelper.ResolveLaunchVariant(
                  GameModeKind.CustomTrail, false, -1, -1, false, restoredCustomTrail) ==
              GameModeLaunchVariant.RestoredCustomizedSave,
            "restored customized Custom Trail was not recognized");
        Check(GameModeHelper.ResolveLaunchVariant(
                  GameModeKind.Campaign, false, -1, -1, false, restoredCustomTrail) ==
              GameModeLaunchVariant.Standard,
            "stale customized origin overrode an incompatible mode");
        Check(GameModeHelper.ResolveLaunchVariant(
                  GameModeKind.VanillaTrail, true, (int)GameTrailType.Warchest, 12, true, default) ==
              GameModeLaunchVariant.Customized,
            "Vanilla Trail Customize was not recognized");
        Check(GameModeHelper.ResolveLaunchVariant(
                  GameModeKind.SandsOfTime, true, (int)GameTrailType.SandsOne, 4, true, default) ==
              GameModeLaunchVariant.Customized,
            "Sands of Time Customize was not recognized");
        Check(GameModeHelper.ResolveLaunchVariant(
                  GameModeKind.SandsOfTime, false, (int)GameTrailType.SandsOne, 4, false, default) ==
              GameModeLaunchVariant.Standard,
            "direct Sands of Time was treated as customized");
        Check(GameModeHelper.ResolveLaunchVariant(
                  GameModeKind.CustomTrail, false, -1, -1, false,
                  new ExternalCustomizedOrigin(ExternalCustomizedOrigin.CoopTrail, -1, 0, 1, false)) ==
              GameModeLaunchVariant.Standard,
            "mismatched Coop origin enabled a Custom Trail");
        Check(GameModeHelper.ResolveLaunchVariant(
                  GameModeKind.VanillaTrail, true, (int)GameTrailType.FirstEdition, 1, false, default) ==
              GameModeLaunchVariant.Standard,
            "stale Vanilla Customize fields enabled a directly started Trail");
        Check(GameModeHelper.ResolveLaunchVariant(
                  GameModeKind.VanillaTrail,
                  true,
                  (int)GameTrailType.FirstEdition,
                  1,
                  true,
                  ExternalCustomizedOrigin.AvailableProvider(supportsBuiltInOrigins: true)) ==
              GameModeLaunchVariant.Standard,
            "stale Vanilla Customize fields bypassed an empty v2 origin provider");
        Check(GameModeHelper.ExternalOriginMatchesEvidence(
                  new ExternalCustomizedOrigin(
                      ExternalCustomizedOrigin.CoopTrail, -1, 0, 1, false),
                  GameModeKind.CoopTrail, -1, 1, -1, false, -1, -1) &&
              !GameModeHelper.ExternalOriginMatchesEvidence(
                  new ExternalCustomizedOrigin(
                      ExternalCustomizedOrigin.CoopTrail, -1, 1, 1, false),
                  GameModeKind.CoopTrail, -1, 1, -1, false, -1, -1),
            "Coop Customize origin was not matched against Vanilla's one-based Trail ID");
        Check(!GameModeHelper.ExternalOriginMatchesEvidence(
                  new ExternalCustomizedOrigin(
                      ExternalCustomizedOrigin.SandsOfTime,
                      (int)GameTrailType.SandsOne, (int)GameTrailType.SandsOne, 4, false),
                  GameModeKind.SandsOfTime,
                  (int)GameTrailType.SandsTwo,
                  0,
                  (int)GameTrailType.SandsTwo,
                  false,
                  -1,
                  -1),
            "a mismatched Sands Trail identifier passed origin validation");
        Check(!GameModeHelper.ExternalOriginMatchesEvidence(
                  new ExternalCustomizedOrigin(
                      ExternalCustomizedOrigin.VanillaTrail,
                      (int)GameTrailType.FirstEdition, (int)GameTrailType.FirstEdition, 3, false),
                  GameModeKind.VanillaTrail,
                  (int)GameTrailType.FirstEdition,
                  0,
                  (int)GameTrailType.FirstEdition,
                  true,
                  (int)GameTrailType.FirstEdition,
                  4),
            "a mismatched Vanilla mission identifier passed origin validation");

        Check(GameModeHelper.ResolveKind(false, -1, -1, -1, 0,
                  eventTrailType: (int)GameTrailType.Extreme) == GameModeKind.VanillaTrail,
            "OnLoadMap Trail event data was not classified");
        Check(GameModeHelper.ResolveKind(false, -1, -1, -1, 0,
                  eventTrailType: (int)GameTrailType.SandsTwo) == GameModeKind.SandsOfTime,
            "OnLoadMap Sands event data was not classified");

        GameData.Instance = new GameData();
        CrusaderDE.MainViewModel.Reset();
        GamePlayerManagerAPI.Instance.MapEditor = false;
        GameModeSnapshot editorLoad = GameModeHelper.Capture(
            new SHCDESE.EventAPI.MapLoader.LoadSaveGameEventArgs(true));
        Check(editorLoad.Kind == GameModeKind.MapEditor && !editorLoad.AllowsCustomGameMods &&
              editorLoad.AllowsRegularGameplayMods,
            "editor save load required an OnStartMap event");

        GamePlayerManagerAPI.Instance.MapEditor = true;
        GameModeSnapshot editorMapLoad = GameModeHelper.Capture(
            new SHCDESE.EventAPI.MapLoader.MapLoadEventArgs
            {
                CampaignMapID = uint.MaxValue,
                TrailType = -1
            });
        Check(editorMapLoad.Kind == GameModeKind.MapEditor && !editorMapLoad.AllowsCustomGameMods &&
              editorMapLoad.AllowsRegularGameplayMods,
            "OnLoadMap without OnStartMap did not detect Map Editor");
        GamePlayerManagerAPI.Instance.MapEditor = false;

        GameData.Instance = new GameData();
        GameModeSnapshot emptyLoad = GameModeHelper.Capture(
            new SHCDESE.EventAPI.MapLoader.MapLoadEventArgs
            {
                CampaignMapID = uint.MaxValue,
                TrailType = -1
            });
        Check(emptyLoad.Kind == GameModeKind.Unknown && !emptyLoad.AllowsCustomGameMods,
            "an empty OnLoadMap event was heuristically treated as Map Editor");

        int gateTransitions = 0;
        Action<bool> countGateTransition = _ => gateTransitions++;
        GameplayModActivationGate.StateChanged += countGateTransition;
        GameplayModActivationGate.ResetForTests();
        Check(!GameplayModActivationGate.IsAllowed,
            "gameplay gate did not start fail-closed");
        GameplayModActivationGate.SetSnapshotForTests(skirmish);
        Check(GameplayModActivationGate.IsAllowed,
            "gameplay gate rejected a Custom Game snapshot");
        GameplayModActivationGate.SetSnapshotForTests(trail);
        Check(!GameplayModActivationGate.IsAllowed,
            "gameplay gate retained permission for a direct Trail");
        GameplayModActivationGate.SetSnapshotForTests(editorMapLoad);
        Check(GameplayModActivationGate.IsAllowed,
            "gameplay gate rejected an editor OnLoadMap snapshot");
        GameplayModActivationGate.ResetForTests();
        Check(!GameplayModActivationGate.IsAllowed && gateTransitions == 4,
            "gameplay gate did not publish exactly the effective lifecycle transitions");
        GameplayModActivationGate.StateChanged -= countGateTransition;

        int resilientGateListeners = 0;
        Action<bool> throwingGateListener = _ => throw new InvalidOperationException("expected test failure");
        Action<bool> resilientGateListener = _ => resilientGateListeners++;
        GameplayModActivationGate.SetSnapshotForTests(editorMapLoad);
        GameplayModActivationGate.StateChanged += throwingGateListener;
        GameplayModActivationGate.StateChanged += resilientGateListener;
        GameplayModActivationGate.SetSnapshotForTests(trail);
        Check(resilientGateListeners == 1 && !GameplayModActivationGate.IsAllowed,
            "one failing gameplay gate listener blocked fail-closed sibling cleanup");
        GameplayModActivationGate.StateChanged -= throwingGateListener;
        GameplayModActivationGate.StateChanged -= resilientGateListener;

        GameData.Instance = new GameData
        {
            game_type = (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER,
            SkirmishGameType = (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_TRAIL,
            SkirmishTrailType = (int)GameTrailType.SandsOne,
            coopTrailID = 0
        };
        GameModeSnapshot explicitSandsLoad = GameModeHelper.Capture(
            new SHCDESE.EventAPI.MapLoader.MapLoadEventArgs
            {
                CampaignMapID = uint.MaxValue,
                TrailType = (int)GameTrailType.SandsOne
            });
        GameplayModActivationGate.ResetForTests();
        GameplayModActivationGate.SetLoadSnapshotForTests(explicitSandsLoad);
        GameplayModActivationGate.SetStartSnapshotForTests(skirmish);
        Check(GameplayModActivationGate.Snapshot.Kind == GameModeKind.SandsOfTime &&
              !GameplayModActivationGate.IsAllowed,
            "generic OnStartMap evidence overrode an explicit direct Sands load");
        GameplayModActivationGate.ResetForTests();

        GameModeSnapshot customizedSandsLoad = explicitSandsLoad.WithModeEvidenceForTests(
            GameModeKind.SandsOfTime,
            GameModeLaunchVariant.Customized,
            (int)GameTrailType.SandsOne);
        GameplayModActivationGate.SetLoadSnapshotForTests(customizedSandsLoad);
        GameplayModActivationGate.SetStartSnapshotForTests(skirmish);
        GameplayModActivationGate.SetLoadSnapshotForTests(explicitSandsLoad);
        Check(GameplayModActivationGate.Snapshot.Kind == GameModeKind.SandsOfTime &&
              GameplayModActivationGate.Snapshot.IsCustomized &&
              GameplayModActivationGate.IsAllowed,
            "OnLoadMap(Post) discarded a verified Sands Customize origin");
        GameplayModActivationGate.ResetForTests();

        GameModeSnapshot conflictingCustomGame = skirmish.WithModeEvidenceForTests(
            GameModeKind.CustomGame,
            GameModeLaunchVariant.Standard,
            eventTrailType: -1,
            hasConflictingCustomizedOrigin: true);
        GameplayModActivationGate.SetSnapshotForTests(conflictingCustomGame);
        Check(!GameplayModActivationGate.IsAllowed,
            "a conflicting stale Customize origin enabled an ordinary Custom Game");
        GameplayModActivationGate.ResetForTests();

        GameData.Instance = new GameData
        {
            game_type = (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER,
            SkirmishGameType = (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM,
            coopTrailID = 2
        };
        GameModeSnapshot directCoopLoad = GameModeHelper.Capture(
            new SHCDESE.EventAPI.MapLoader.MapLoadEventArgs
            {
                CampaignMapID = uint.MaxValue,
                TrailType = -1
            });
        GameplayModActivationGate.SetLoadSnapshotForTests(directCoopLoad);
        GameplayModActivationGate.SetStartSnapshotForTests(skirmish);
        Check(GameplayModActivationGate.Snapshot.Kind == GameModeKind.CoopTrail &&
              !GameplayModActivationGate.IsAllowed,
            "generic OnStartMap evidence enabled a directly started Coop Trail");
        GameplayModActivationGate.ResetForTests();

        GameModeSnapshot customizedCoopLoad = directCoopLoad.WithModeEvidenceForTests(
            GameModeKind.CoopTrail,
            GameModeLaunchVariant.Customized,
            eventTrailType: -1);
        GameplayModActivationGate.SetLoadSnapshotForTests(customizedCoopLoad);
        GameplayModActivationGate.SetStartSnapshotForTests(skirmish);
        Check(GameplayModActivationGate.Snapshot.Kind == GameModeKind.CoopTrail &&
              GameplayModActivationGate.IsAllowed,
            "generic OnStartMap evidence disabled a verified customized Coop Trail");
        GameplayModActivationGate.ResetForTests();

        GameData.Instance = new GameData
        {
            game_type = (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER,
            SkirmishGameType = (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL,
            coopTrailID = 0
        };
        GameModeSnapshot directCustomTrailLoad = GameModeHelper.Capture(
            new SHCDESE.EventAPI.MapLoader.MapLoadEventArgs
            {
                CampaignMapID = uint.MaxValue,
                TrailType = -1
            });
        GameplayModActivationGate.SetLoadSnapshotForTests(directCustomTrailLoad);
        GameplayModActivationGate.SetStartSnapshotForTests(skirmish);
        Check(GameplayModActivationGate.Snapshot.Kind == GameModeKind.CustomTrail &&
              !GameplayModActivationGate.IsAllowed,
            "generic OnStartMap evidence enabled a directly started Custom Trail");
        GameplayModActivationGate.ResetForTests();

        GameModeSnapshot customizedCustomTrailLoad = directCustomTrailLoad.WithModeEvidenceForTests(
            GameModeKind.CustomTrail,
            GameModeLaunchVariant.Customized,
            eventTrailType: -1);
        GameplayModActivationGate.SetLoadSnapshotForTests(customizedCustomTrailLoad);
        GameplayModActivationGate.SetStartSnapshotForTests(skirmish);
        Check(GameplayModActivationGate.Snapshot.Kind == GameModeKind.CustomTrail &&
              GameplayModActivationGate.IsAllowed,
            "generic OnStartMap evidence disabled a verified customized Custom Trail");
        GameplayModActivationGate.ResetForTests();

        GameData.Instance = new GameData { game_type = 3, SkirmishGameType = -1 };
        platform.activeLobby = new Platform_Multiplayer.MPLobby
        {
            members = new List<Platform_Multiplayer.MPLobbyMember>
            {
                new Platform_Multiplayer.MPLobbyMember { SkirmishMember = true },
                new Platform_Multiplayer.MPLobbyMember { SkirmishMember = true }
            }
        };
        platform.gameMembers = new List<Platform_Multiplayer.MPGameMember>
        {
            new Platform_Multiplayer.MPGameMember { skirmishAI = false, steamID = 0 },
            new Platform_Multiplayer.MPGameMember { skirmishAI = true, steamID = 0 }
        };
        GameModeSnapshot transitioningSkirmish = GameModeHelper.Capture();
        Check(transitioningSkirmish.IsSingleplayerSkirmishMode &&
              !transitioningSkirmish.IsSingleplayerSkirmish &&
              !transitioningSkirmish.IsRealMultiplayer &&
              transitioningSkirmish.SkirmishLobbyMembers == 2,
            "local skirmish transition required the temporarily unavailable subtype");

        GameData.Instance = new GameData();
        platform.activeLobby = new Platform_Multiplayer.MPLobby
        {
            members = new List<Platform_Multiplayer.MPLobbyMember>
            {
                null,
                new Platform_Multiplayer.MPLobbyMember { SkirmishMember = false }
            }
        };
        platform.gameMembers = new List<Platform_Multiplayer.MPGameMember>
        {
            null,
            new Platform_Multiplayer.MPGameMember { skirmishAI = false, steamID = 12345 }
        };
        GameModeSnapshot lobby = GameModeHelper.Capture();
        Check(lobby.IsRealMultiplayer && !lobby.PlatformMultiplayer &&
              lobby.RealLobbyMembers == 1 && lobby.RealNetworkGameMembers == 1,
            "pre-start lobby required the active-game signal or failed on null transition members");

        platform.activeLobby = null;
        GameNetworkAPI.MultiplayerGame = true;
        GameModeSnapshot activeGame = GameModeHelper.Capture();
        Check(activeGame.IsRealMultiplayer && activeGame.PlatformMultiplayer &&
              activeGame.ToDiagnosticString().Contains("platformMultiplayer=True"),
            "active multiplayer game did not use the Extender API signal");

        GameNetworkAPI.MultiplayerGame = false;
        GameNetworkAPI.Networked = false;
        GameModeSnapshot multiplayerSave = GameModeHelper.Capture(multiplayerSave: true);
        Check(multiplayerSave.IsRealMultiplayer && multiplayerSave.MultiplayerSave,
            "multiplayer-save signal was not authoritative");

        platform.activeLobby = null;
        platform.gameMembers = null;
        GameData.Instance = null;
        Director.instance = null;
        GameNetworkAPI.Networked = true;
    }

    private static void TestGameplayFeatureModePolicy()
    {
        CrusaderDE.MainViewModel.Reset();
        GamePlayerManagerAPI.Instance.MapEditor = false;
        Platform_Multiplayer.Instance.activeLobby = null;
        Platform_Multiplayer.Instance.gameMembers = null;
        Director.instance = null;
        GameNetworkAPI.Networked = true;
        GameNetworkAPI.MultiplayerGame = false;
        GameData.Instance = new GameData
        {
            game_type = (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER,
            SkirmishGameType = (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM,
            coopTrailID = 0
        };

        GameModeSnapshot customGame = GameModeHelper.Capture();
        GameModeSnapshot[] customizedModes =
        {
            customGame.WithModeEvidenceForTests(GameModeKind.VanillaTrail, GameModeLaunchVariant.Customized, (int)GameTrailType.FirstEdition),
            customGame.WithModeEvidenceForTests(GameModeKind.CustomTrail, GameModeLaunchVariant.Customized, -1),
            customGame.WithModeEvidenceForTests(GameModeKind.CoopTrail, GameModeLaunchVariant.Customized, -1),
            customGame.WithModeEvidenceForTests(GameModeKind.SandsOfTime, GameModeLaunchVariant.Customized, (int)GameTrailType.SandsOne)
        };
        GameModeSnapshot[] recognizedDirectModes =
        {
            customGame.WithModeEvidenceForTests(GameModeKind.Campaign, GameModeLaunchVariant.Standard, -1),
            customGame.WithModeEvidenceForTests(GameModeKind.StandaloneMission, GameModeLaunchVariant.Standard, -1),
            customGame.WithModeEvidenceForTests(GameModeKind.VanillaTrail, GameModeLaunchVariant.Standard, (int)GameTrailType.FirstEdition),
            customGame.WithModeEvidenceForTests(GameModeKind.CustomTrail, GameModeLaunchVariant.Standard, -1),
            customGame.WithModeEvidenceForTests(GameModeKind.CoopTrail, GameModeLaunchVariant.Standard, -1),
            customGame.WithModeEvidenceForTests(GameModeKind.SandsOfTime, GameModeLaunchVariant.Standard, (int)GameTrailType.SandsOne)
        };
        GameModeSnapshot conflicting =
            customGame.WithModeEvidenceForTests(GameModeKind.CustomGame, GameModeLaunchVariant.Standard, -1, true);

        GameNetworkAPI.MultiplayerGame = true;
        GameModeSnapshot realMultiplayerCustomGame = GameModeHelper.Capture();
        GameModeSnapshot[] realMultiplayerCustomizedModes =
        {
            realMultiplayerCustomGame.WithModeEvidenceForTests(GameModeKind.VanillaTrail, GameModeLaunchVariant.Customized, (int)GameTrailType.FirstEdition),
            realMultiplayerCustomGame.WithModeEvidenceForTests(GameModeKind.CustomTrail, GameModeLaunchVariant.Customized, -1),
            realMultiplayerCustomGame.WithModeEvidenceForTests(GameModeKind.CoopTrail, GameModeLaunchVariant.Customized, -1),
            realMultiplayerCustomGame.WithModeEvidenceForTests(GameModeKind.SandsOfTime, GameModeLaunchVariant.Customized, (int)GameTrailType.SandsOne)
        };
        GameModeSnapshot[] realMultiplayerDirectModes =
        {
            realMultiplayerCustomGame.WithModeEvidenceForTests(GameModeKind.Campaign, GameModeLaunchVariant.Standard, -1),
            realMultiplayerCustomGame.WithModeEvidenceForTests(GameModeKind.StandaloneMission, GameModeLaunchVariant.Standard, -1),
            realMultiplayerCustomGame.WithModeEvidenceForTests(GameModeKind.VanillaTrail, GameModeLaunchVariant.Standard, (int)GameTrailType.FirstEdition),
            realMultiplayerCustomGame.WithModeEvidenceForTests(GameModeKind.CustomTrail, GameModeLaunchVariant.Standard, -1),
            realMultiplayerCustomGame.WithModeEvidenceForTests(GameModeKind.CoopTrail, GameModeLaunchVariant.Standard, -1),
            realMultiplayerCustomGame.WithModeEvidenceForTests(GameModeKind.SandsOfTime, GameModeLaunchVariant.Standard, (int)GameTrailType.SandsOne)
        };
        GameNetworkAPI.MultiplayerGame = false;
        GamePlayerManagerAPI.Instance.MapEditor = true;
        GameModeSnapshot editor = GameModeHelper.Capture();
        GamePlayerManagerAPI.Instance.MapEditor = false;

        var owners = new Dictionary<GameplayFeatureId, string>
        {
            { GameplayFeatureId.BuildingCostTooltip, "BuildingCosts_Serp" },
            { GameplayFeatureId.BuildingLimitEnforcement, "BuildingLimit_Serp" },
            { GameplayFeatureId.UnitCostEnforcement, "UnitCosts_Serp" },
            { GameplayFeatureId.UnitLimitEnforcement, "UnitLimit_Serp" },
            { GameplayFeatureId.LordHealthMultipliers, "ExtraFeatures_Serp" },
            { GameplayFeatureId.EndlessExtremePowersRecharge, "CheatMod_Serp" },
            { GameplayFeatureId.RandomEventsRuntime, "RandomEvents_Serp" },
            { GameplayFeatureId.ImprovedHunterTargetSelection, "ImprovedHunters_Serp" },
            { GameplayFeatureId.ImprovedHunterPathfinding, "ImprovedHunters_Serp" },
            { GameplayFeatureId.CastleSpawning, "CastlePlanner_Serp" },
            { GameplayFeatureId.FreeCastlePreview, "CastlePlanner_Serp" },
            { GameplayFeatureId.CastleBlueprints, "CastlePlanner_Serp" }
        };
        var editorAllowed = new HashSet<GameplayFeatureId>
        {
            GameplayFeatureId.CastleBlueprints
        };
        var multiplayerBlocked = new HashSet<GameplayFeatureId>
        {
            GameplayFeatureId.ImprovedHunterTargetSelection,
            GameplayFeatureId.ImprovedHunterPathfinding
        };

        GameplayModActivationProfile castlePlannerProfile =
            GameplayModModePolicy.GetProfile("CastlePlanner_Serp", "Castle Planner");
        Check(recognizedDirectModes.All(mode =>
                  !GameplayModModePolicy.IsAllowed(castlePlannerProfile, mode, out _)),
            "CastlePlanner's general gameplay functions were enabled by the Blueprint-only direct-mode exception");

        foreach (KeyValuePair<GameplayFeatureId, string> entry in owners)
        {
            GameplayFeatureActivationProfile profile =
                GameplayFeatureModePolicy.GetProfile(entry.Value, entry.Key);
            Check(GameplayFeatureModePolicy.IsAllowed(profile, customGame, out _) &&
                  customizedModes.All(mode => GameplayFeatureModePolicy.IsAllowed(profile, mode, out _)),
                $"feature policy rejected a regular gameplay context for {entry.Key}");
            bool allRecognizedModesAllowed = entry.Key == GameplayFeatureId.CastleBlueprints;
            Check(recognizedDirectModes.All(mode =>
                      GameplayFeatureModePolicy.IsAllowed(profile, mode, out _) == allRecognizedModesAllowed) &&
                  !GameplayFeatureModePolicy.IsAllowed(profile, default, out _) &&
                  !GameplayFeatureModePolicy.IsAllowed(profile, conflicting, out _),
                $"feature direct-mode or fail-closed policy is incorrect for {entry.Key}");
            Check(GameplayFeatureModePolicy.IsAllowed(profile, editor, out _) == editorAllowed.Contains(entry.Key),
                $"feature editor policy is incorrect for {entry.Key}");
            Check(GameplayFeatureModePolicy.IsAllowed(profile, realMultiplayerCustomGame, out _) == !multiplayerBlocked.Contains(entry.Key),
                $"feature multiplayer policy is incorrect for {entry.Key}");
            Check(realMultiplayerCustomizedModes.All(mode =>
                      GameplayFeatureModePolicy.IsAllowed(profile, mode, out _) == !multiplayerBlocked.Contains(entry.Key)),
                $"feature customize multiplayer policy is incorrect for {entry.Key}");
            Check(realMultiplayerDirectModes.All(mode =>
                      GameplayFeatureModePolicy.IsAllowed(profile, mode, out _) == allRecognizedModesAllowed),
                $"feature direct-mode multiplayer policy is incorrect for {entry.Key}");
        }

        bool wrongOwnerRejected = false;
        try
        {
            GameplayFeatureModePolicy.GetProfile(
                "ExtraFeatures_Serp",
                GameplayFeatureId.BuildingCostTooltip);
        }
        catch (ArgumentOutOfRangeException)
        {
            wrongOwnerRejected = true;
        }
        Check(wrongOwnerRejected, "feature policy accepted a mismatched mod GUID");
        Check(!GameplayFeatureModePolicy.IsAllowed(
                  "ExtraFeatures_Serp",
                  GameplayFeatureId.BuildingCostTooltip,
                  customGame) &&
              !GameplayFeatureModePolicy.IsAllowed(
                  "BuildingCosts_Serp",
                  (GameplayFeatureId)int.MaxValue,
                  customGame),
            "feature policy evaluation did not fail closed for an unknown GUID/feature pair");

        GameplayFeatureModePolicy.ResetLoggedDecisionsForTests();
        Check(GameplayFeatureModePolicy.RecordDecisionForTests(GameplayFeatureId.RandomEventsRuntime, false) &&
              !GameplayFeatureModePolicy.RecordDecisionForTests(GameplayFeatureId.RandomEventsRuntime, false) &&
              GameplayFeatureModePolicy.RecordDecisionForTests(GameplayFeatureId.RandomEventsRuntime, true) &&
              !GameplayFeatureModePolicy.RecordDecisionForTests(GameplayFeatureId.RandomEventsRuntime, true),
            "feature decision logging was not deduplicated by effective mode decision");
    }

    private static void TestStartConditionsMapSessionState()
    {
        var state = new StartConditions.StartConditionsMapSessionState();
        Check(state.TryBeginNewMap() && state.IsHandled && !state.TryBeginNewMap(),
            "StartConditions allowed duplicate handling within one map");
        state.Reset();
        Check(state.TryBeginNewMap(),
            "StartConditions did not handle the next allowed map after an unload or gate reset");
        state.Reset();
        state.MarkSaveLoaded();
        Check(state.IsHandled && !state.TryBeginNewMap(),
            "StartConditions treated a loaded save as a fresh map");
        state.Reset();
        Check(!state.IsHandled && state.TryBeginNewMap(),
            "StartConditions did not recover after an allowed-blocked-allowed sequence");
    }

    private static void TestGameplayGateSourceIntegration()
    {
        string workspaceRoot = Path.GetFullPath(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        string[,] gameplayMods =
        {
            { "BuildingCosts", "BuildingCostsRuntime.cs", "BuildingCosts_Serp" },
            { "BuildingLimit", "BuildingLimitRuntime.cs", "BuildingLimit_Serp" },
            { "CastlePlanner", "CastlePlannerPlugin.cs", "CastlePlanner_Serp" },
            { "CheatMod", "CheatModRuntime.cs", "CheatMod_Serp" },
            { "ExtraFeatures", "ExtraFeaturesRuntime.cs", "ExtraFeatures_Serp" },
            { "ExtremePowers", "ExtremePowersPlugin.cs", "ExtremePowers_Serp" },
            { "ImprovedHunters", "ImprovedHuntersRuntime.cs", "ImprovedHunters_Serp" },
            { "RandomEvents", "RandomEventsRuntime.cs", "RandomEvents_Serp" },
            { "StartConditions", "StartConditionsRuntime.cs", "StartConditions_Serp" },
            { "UnitCosts", "UnitCostsRuntime.cs", "UnitCosts_Serp" },
            { "UnitLimit", "UnitLimitRuntime.cs", "UnitLimit_Serp" }
        };
        string policySource = File.ReadAllText(
            Path.Combine(workspaceRoot, "Shared", "GameplayModModePolicy.cs"));

        for (int index = 0; index < gameplayMods.GetLength(0); index++)
        {
            string mod = gameplayMods[index, 0];
            string project = File.ReadAllText(Path.Combine(workspaceRoot, mod, mod + ".csproj"));
            string runtime = File.ReadAllText(
                Path.Combine(workspaceRoot, mod, "src", gameplayMods[index, 1]));
            Check(project.Contains("GameplayModActivationGate.cs") &&
                  project.Contains("GameplayModModePolicy.cs") &&
                  project.Contains("GameplayFeatureModePolicy.cs") &&
                  runtime.Contains("GameplayModActivationGate.Initialize") &&
                  runtime.Contains("PluginGuid") &&
                  policySource.Contains(gameplayMods[index, 2]),
                $"{mod} is not bound to its GUID-based gameplay-mode profile");

            string[] competingCaptures = Directory.GetFiles(
                    Path.Combine(workspaceRoot, mod, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains("GameModeHelper.Capture("))
                .Select(Path.GetFileName)
                .ToArray();
            Check(competingCaptures.Length == 0,
                $"{mod} still performs a competing live game-mode capture: {string.Join(", ", competingCaptures)}");
        }

        foreach (string exemptProject in new[]
        {
            Path.Combine("BugfixesAndQoL", "BugfixesAndQoL.csproj"),
            Path.Combine("CustomCustomTrail", "CustomCustomTrail.csproj"),
            Path.Combine("SerpsModsHost", "SerpsModsHost.csproj")
        })
        {
            string project = File.ReadAllText(Path.Combine(workspaceRoot, exemptProject));
            Check(!project.Contains("GameplayModActivationGate.cs") &&
                  !project.Contains("GameplayModModePolicy.cs"),
                $"exempt project {exemptProject} unexpectedly received a restrictive gameplay profile");
        }

        string gateSource = File.ReadAllText(
            Path.Combine(workspaceRoot, "Shared", "GameplayModActivationGate.cs"));
        Check(gateSource.Contains("configuredEnabled=") &&
              gateSource.Contains("effectiveEnabled=") &&
              gateSource.Contains("disabled-by-mode") &&
              gateSource.Contains("GameplayFeatureModePolicy.LogDecisions") &&
              !gateSource.Contains("EnableMod ="),
            "gameplay gate logging or non-mutating settings contract regressed");

        string featurePolicySource = File.ReadAllText(
            Path.Combine(workspaceRoot, "Shared", "GameplayFeatureModePolicy.cs"));
        foreach (string expectedFeature in Enum.GetNames(typeof(GameplayFeatureId)))
        {
            Check(featurePolicySource.Contains(expectedFeature),
                $"central gameplay-feature policy is missing {expectedFeature}");
        }

        string[,] featureBindings =
        {
            { "BuildingCosts", "BuildingCostsRuntime.cs", "BuildingCostTooltip" },
            { "BuildingLimit", "BuildingLimitRuntime.Helpers.cs", "BuildingLimitEnforcement" },
            { "UnitCosts", "UnitCostsRuntime.cs", "UnitCostEnforcement" },
            { "UnitLimit", "UnitLimitRuntime.Helpers.cs", "UnitLimitEnforcement" },
            { "ExtraFeatures", "LordHealthRuntime.cs", "LordHealthMultipliers" },
            { "CheatMod", "CheatModRuntime.cs", "EndlessExtremePowersRecharge" },
            { "RandomEvents", "RandomEventsRuntime.cs", "RandomEventsRuntime" },
            { "ImprovedHunters", "ImprovedHuntersRuntime.cs", "ImprovedHunterTargetSelection" },
            { "ImprovedHunters", "ImprovedHuntersRuntime.cs", "ImprovedHunterPathfinding" },
            { "CastlePlanner", "CastlePlannerRuntime.cs", "CastleSpawning" },
            { "CastlePlanner", "FreeCastlePreviewRuntime.cs", "FreeCastlePreview" },
            { "CastlePlanner", "BlueprintRuntimeController.cs", "CastleBlueprints" }
        };
        for (int index = 0; index < featureBindings.GetLength(0); index++)
        {
            string runtimeSource = File.ReadAllText(Path.Combine(
                workspaceRoot,
                featureBindings[index, 0],
                "src",
                featureBindings[index, 1]));
            Check(runtimeSource.Contains("GameplayFeatureModePolicy.IsAllowed") &&
                  runtimeSource.Contains("GameplayFeatureId." + featureBindings[index, 2]) &&
                  runtimeSource.Contains("GameplayModActivationGate.Snapshot"),
                $"{featureBindings[index, 0]} feature {featureBindings[index, 2]} is not bound to the cached feature-mode policy");
        }

        string startConditionsRuntime = File.ReadAllText(
            Path.Combine(workspaceRoot, "StartConditions", "src", "StartConditionsRuntime.cs"));
        Check(startConditionsRuntime.Contains("GameplayModActivationGate.IsAllowed &&") &&
              startConditionsRuntime.Contains("ResetMapSession();") &&
              !startConditionsRuntime.Contains("handledCurrentMap"),
            "StartConditions mode precedence or map-session reset regressed");

        string hudCoordinatorSource = File.ReadAllText(
            Path.Combine(workspaceRoot, "Shared", "TroopActionButtonLayout.cs"));
        string bugfixRuntimeSource = File.ReadAllText(
            Path.Combine(workspaceRoot, "BugfixesAndQoL", "src", "BugfixesAndQoLRuntime.cs"));
        string assassinClimbSource = File.ReadAllText(
            Path.Combine(workspaceRoot, "BugfixesAndQoL", "src", "AssassinClimbRuntime.cs"));
        Check(hudCoordinatorSource.Contains("A direct editor launch can build the HUD") &&
              bugfixRuntimeSource.Contains("BeginEditorMapIfApplicable") &&
              bugfixRuntimeSource.Contains("GameModeHelper.IsMapEditor()") &&
              assassinClimbSource.Contains("initialized = true;") &&
              assassinClimbSource.Contains("RefreshButtonVisibility();") &&
              assassinClimbSource.Contains("Application.onBeforeRender += OnBeforeRender") &&
              assassinClimbSource.Contains("lastRenderFrame == Time.frameCount") &&
              assassinClimbSource.Contains("expectedSelectedCount > 0") &&
              assassinClimbSource.Contains("RefreshButtonVisibilityCore(troopPanel, force: false)"),
            "direct-editor Assassin HUD bootstrap regressed");
    }

    private static void TestSurrenderAndStatisticsSettingAndPolicy()
    {
        const int testLordUnitId = 120;
        const int testLordGlobalId = 8120;
        var validLord = new SurrenderLordSnapshot(2, testLordUnitId, testLordGlobalId, 2, true);
        var missingLord = new SurrenderLordSnapshot(2, -1, -1, -1, false);
        var deadLord = new SurrenderLordSnapshot(2, testLordUnitId, testLordGlobalId, 2, false);
        var foreignLord = new SurrenderLordSnapshot(2, testLordUnitId, testLordGlobalId, 3, true);

        Check(LordUnitControlsPolicy.CanActivate(
                true, true, true, false, false, 1, testLordUnitId, 2, validLord),
            "compact Lord HUD rejected the sole selected local Lord");
        Check(!LordUnitControlsPolicy.CanActivate(
                true, true, true, false, false, 2, testLordUnitId, 2, validLord),
            "compact Lord HUD accepted a mixed selection");
        Check(!LordUnitControlsPolicy.CanActivate(
                true, true, true, false, false, 1, testLordUnitId + 1, 2, validLord),
            "compact Lord HUD accepted a non-Lord selected unit");
        Check(!LordUnitControlsPolicy.CanActivate(
                true, true, true, false, false, 1, testLordUnitId, 3, validLord),
            "compact Lord HUD accepted another player's Lord");
        Check(!LordUnitControlsPolicy.CanActivate(
                true, true, true, false, true, 1, testLordUnitId, 2, validLord),
            "compact Lord HUD appeared for a spectator");
        Check(LordUnitControlsPolicy.CanActivate(
                true, true, false, true, false, 1, testLordUnitId, 2, validLord),
            "compact Lord HUD rejected the controlled player's Lord in the map editor");
        Check(!LordUnitControlsPolicy.CanActivate(
                true, true, false, true, false, 1, testLordUnitId, 3, validLord),
            "compact Lord HUD accepted another player's Lord in the map editor");
        Check(!LordUnitControlsPolicy.CanActivate(
                true, true, true, false, false, 1, testLordUnitId, 2, deadLord),
            "compact Lord HUD accepted a dead Lord");
        Check(LordUnitControlsPolicy.CanShowDisband(true, true, false) &&
              !LordUnitControlsPolicy.CanShowDisband(true, false, false) &&
              !LordUnitControlsPolicy.CanShowDisband(true, true, true),
            "Lord disband visibility ignored surrender availability");
        Check(LordUnitControlsPolicy.ShouldReturnToDefaultHud(true, true, 0) &&
              !LordUnitControlsPolicy.ShouldReturnToDefaultHud(false, true, 0) &&
              !LordUnitControlsPolicy.ShouldReturnToDefaultHud(true, false, 0) &&
              !LordUnitControlsPolicy.ShouldReturnToDefaultHud(true, true, 1),
            "compact Lord HUD default-HUD transition policy was incorrect");
        Check(LordUnitControlsPolicy.GetStanceTooltipAction(true, "GuardStanceButton") ==
              LordStanceTooltipAction.ShowVanillaBehavior,
            "Lord guard stance did not select the custom Vanilla-behavior rollover");
        Check(LordUnitControlsPolicy.GetStanceTooltipAction(true, "DefensiveStanceButton") ==
              LordStanceTooltipAction.UseVanillaStandGround &&
              LordUnitControlsPolicy.GetStanceTooltipAction(true, "AggressiveStanceButton") ==
              LordStanceTooltipAction.UseVanillaStandGround,
            "non-zero Lord stances did not select Vanilla's stand-ground rollover");
        Check(LordUnitControlsPolicy.GetStanceTooltipAction(false, "GuardStanceButton") ==
              LordStanceTooltipAction.UseVanilla &&
              LordUnitControlsPolicy.GetStanceTooltipAction(true, "UnitAttackHere") ==
              LordStanceTooltipAction.UseVanilla &&
              LordUnitControlsPolicy.GetStanceTooltipAction(true, null) ==
              LordStanceTooltipAction.UseVanilla,
            "normal troop, unrelated, or invalid rollovers did not remain Vanilla");

        Check(SurrenderPolicy.CanShowButton(true, true, false, false, validLord),
            "surrender button rejected an active player with a living lord");
        Check(!SurrenderPolicy.CanShowButton(false, true, false, false, validLord),
            "disabled surrender setting exposed the button");
        Check(!SurrenderPolicy.CanShowButton(true, true, false, false, missingLord),
            "surrender button accepted a missing lord");
        Check(!SurrenderPolicy.CanShowButton(true, true, false, false, deadLord),
            "surrender button accepted a dead lord");
        Check(!SurrenderPolicy.CanShowButton(true, true, false, false, foreignLord),
            "surrender button accepted a foreign lord");
        Check(!SurrenderPolicy.CanShowButton(true, true, true, false, validLord),
            "surrender button appeared in the map editor");
        Check(!SurrenderPolicy.CanShowButton(true, true, false, true, validLord),
            "surrender button appeared for a spectator");
        Check(!SurrenderPolicy.CanEnableButton(true, true, false),
            "multiplayer surrender remained enabled without Chore transport");
        Check(SurrenderPolicy.CanEnableButton(true, false, false),
            "singleplayer surrender incorrectly required Chore transport");

        Check(SurrenderPolicy.IsStatisticsViewer(true, 0, missingLord),
            "start spectator was not accepted as a statistics viewer");
        Check(SurrenderPolicy.IsStatisticsViewer(false, 2, deadLord),
            "eliminated player with a dead lord was not accepted as a statistics viewer");
        Check(SurrenderPolicy.IsStatisticsViewer(false, 2, missingLord),
            "eliminated player without a lord was not accepted as a statistics viewer");
        Check(!SurrenderPolicy.IsStatisticsViewer(false, 2, validLord),
            "active player with a living lord was accepted as a statistics viewer");
        Check(!SurrenderPolicy.IsStatisticsViewer(false, 0, missingLord),
            "non-spectator without a player slot was accepted as a statistics viewer");

        Check(SurrenderPolicy.CanShowStatisticsButton(true, true, false, true, true, true),
            "spectator statistics rejected a valid spectator");
        Check(!SurrenderPolicy.CanShowStatisticsButton(false, true, false, true, true, true),
            "disabled shared setting exposed spectator statistics");
        Check(!SurrenderPolicy.CanShowStatisticsButton(true, false, false, true, true, true),
            "spectator statistics appeared outside an active match");
        Check(!SurrenderPolicy.CanShowStatisticsButton(true, true, true, true, true, true),
            "spectator statistics appeared in the map editor");
        Check(!SurrenderPolicy.CanShowStatisticsButton(true, true, false, false, true, true),
            "spectator statistics appeared for an active player");
        Check(!SurrenderPolicy.CanShowStatisticsButton(true, true, false, true, false, true),
            "spectator statistics accepted an unsupported game mode");
        Check(!SurrenderPolicy.CanShowStatisticsButton(true, true, false, true, true, false),
            "spectator statistics appeared without a validated runtime");

        Check(SurrenderPolicy.CanPromoteEliminatedPlayerToSpectator(
                true, true, false, false, true, true, true, 2, missingLord),
            "eligible eliminated player was not promoted to spectator");
        Check(!SurrenderPolicy.CanPromoteEliminatedPlayerToSpectator(
                false, true, false, false, true, true, true, 2, missingLord),
            "disabled eliminated-player spectator setting still promoted a player");
        Check(!SurrenderPolicy.CanPromoteEliminatedPlayerToSpectator(
                true, true, true, false, true, true, true, 2, missingLord),
            "map-editor player was promoted to spectator");
        Check(!SurrenderPolicy.CanPromoteEliminatedPlayerToSpectator(
                true, true, false, true, true, true, true, 2, missingLord),
            "existing spectator was promoted again");
        Check(!SurrenderPolicy.CanPromoteEliminatedPlayerToSpectator(
                true, true, false, false, true, true, false, 2, missingLord),
            "player without a previously validated living lord was promoted during initialization");
        Check(!SurrenderPolicy.CanPromoteEliminatedPlayerToSpectator(
                true, true, false, false, true, true, true, 2, validLord),
            "active player with a living lord was promoted to spectator");
        Check(!SurrenderPolicy.CanPromoteEliminatedPlayerToSpectator(
                true, true, false, false, true, true, true, 0, missingLord),
            "invalid local player slot was promoted to spectator");
        Check(!SurrenderPolicy.CanPromoteEliminatedPlayerToSpectator(
                true, true, false, false, false, true, true, 2, missingLord),
            "unsupported campaign/tutorial mode promoted an eliminated player");
        Check(!SurrenderPolicy.CanPromoteEliminatedPlayerToSpectator(
                true, true, false, false, true, false, true, 2, missingLord),
            "unverified local multiplayer participant was promoted to spectator");

        Check(SurrenderPolicy.CanAcceptRequest(true, true, true, true, true, validLord),
            "host rejected an authenticated human surrender request");
        Check(!SurrenderPolicy.CanAcceptRequest(true, true, true, false, true, validLord),
            "host accepted an unknown surrender sender");
        Check(!SurrenderPolicy.CanAcceptRequest(true, true, true, true, false, validLord),
            "host accepted a non-human surrender sender");
        Check(!SurrenderPolicy.CanAcceptRequest(true, true, false, true, true, validLord),
            "non-host accepted a surrender request");

        Check(SurrenderPolicy.IsChoreDelivery(false),
            "surrender execution rejected Chore delivery without a Steam sender");
        Check(!SurrenderPolicy.IsChoreDelivery(true),
            "surrender execution accepted direct non-Chore delivery with a Steam sender");
        Check(SurrenderPolicy.CanExecute(2, validLord, testLordUnitId),
            "valid surrender execution was rejected");
        Check(!SurrenderPolicy.CanExecute(3, validLord, testLordUnitId),
            "forged player ID was accepted");
        Check(!SurrenderPolicy.CanExecute(0, validLord, testLordUnitId),
            "invalid surrender player slot was accepted");
        Check(!SurrenderPolicy.CanExecute(2, deadLord, testLordUnitId),
            "dead surrender lord was accepted");
        Check(!SurrenderPolicy.CanExecute(2, foreignLord, testLordUnitId),
            "foreign surrender lord was accepted");
        Check(!SurrenderPolicy.CanExecute(2, validLord, testLordUnitId - 1),
            "mismatched local global-ID resolution was accepted");

        var request = new SurrenderRequestPacket { ProtocolVersion = 1, RequestId = 17 };
        SurrenderRequestPacket requestRoundTrip = MessagePackSerializer.Deserialize<SurrenderRequestPacket>(
            MessagePackSerializer.Serialize(request));
        Check(requestRoundTrip.ProtocolVersion == 1 && requestRoundTrip.RequestId == 17,
            "surrender request packet did not round-trip");
        Check(typeof(SurrenderRequestPacket).GetFields().All(field => field.Name != "PlayerId"),
            "client surrender request contains a target player ID");

        var execution = new SurrenderExecutionPacket
        {
            PlayerId = 2
        };
        SurrenderExecutionPacket executionRoundTrip = MessagePackSerializer.Deserialize<SurrenderExecutionPacket>(
            MessagePackSerializer.Serialize(execution));
        Check(executionRoundTrip.PlayerId == 2,
            "surrender execution packet did not round-trip");
        byte[] observedSurrenderBody = MessagePackSerializer.Serialize(new SurrenderExecutionPacket
        {
            PlayerId = 1
        });
        Check(observedSurrenderBody.SequenceEqual(new byte[] { 0x01 }),
            "surrender execution body is no longer the expected one-byte minimal payload");

        string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SurrenderAndStatisticsSetting.dll");
        string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LobbyModSettings", "SurrenderAndStatisticsSettingTest.msgpack");
        if (File.Exists(settingsPath))
            File.Delete(settingsPath);

        GameNetworkAPI.Networked = true;
        GameNetworkAPI.MultiplayerGame = true;
        GameNetworkAPI.LocalHost = true;
        var setting = new SurrenderAndStatisticsSettingViewModel();
        setting.PreparePresets(null, pluginPath, "SurrenderAndStatisticsSettingTest");
        setting.ActivatePresets();
        Check(setting.EnableAiFixes, "EnableAiFixes did not default to true");
        Check(setting.EnableSurrenderAndStatistics, "EnableSurrenderAndStatistics did not default to true");
        Check(setting.EnableLordUnitControls, "EnableLordUnitControls did not default to true");
        Check(setting.EnableEliminatedPlayersBecomeSpectators,
            "EnableEliminatedPlayersBecomeSpectators did not default to true");
        Check(setting.EnableAbruptHostMigrationFix,
            "EnableAbruptHostMigrationFix did not default to true");
        Check(setting.EnableReturnToMultiplayerLobby,
            "EnableReturnToMultiplayerLobby did not default to true");
        Check(setting.AllowFullAiMultiplayerLobby,
            "AllowFullAiMultiplayerLobby did not default to true");
        Check(typeof(SurrenderAndStatisticsSettingViewModel).GetProperty("EnableSurrender") == null,
            "obsolete EnableSurrender property remains present");
        setting.EnableAiFixes = false;
        setting.EnableSurrenderAndStatistics = false;
        setting.EnableLordUnitControls = false;
        setting.EnableEliminatedPlayersBecomeSpectators = false;
        setting.EnableAbruptHostMigrationFix = false;
        setting.EnableReturnToMultiplayerLobby = false;
        setting.AllowFullAiMultiplayerLobby = false;
        setting.SelectedPreset = 1;
        Check(setting.EnableAiFixes, "new shared preset did not retain the EnableAiFixes default true value");
        Check(setting.EnableSurrenderAndStatistics, "new shared preset did not retain the default true value");
        Check(setting.EnableLordUnitControls, "new shared preset did not retain the Lord-controls default true value");
        Check(setting.EnableEliminatedPlayersBecomeSpectators,
            "new shared preset did not retain the spectator-promotion default true value");
        Check(setting.EnableAbruptHostMigrationFix,
            "new shared preset did not retain the abrupt host-migration default true value");
        Check(setting.EnableReturnToMultiplayerLobby,
            "new shared preset did not retain the lobby-return default true value");
        Check(setting.AllowFullAiMultiplayerLobby,
            "new shared preset did not retain the full-AI-lobby default true value");
        setting.SelectedPreset = 0;
        Check(!setting.EnableAiFixes, "EnableAiFixes did not round-trip through presets");
        Check(!setting.EnableSurrenderAndStatistics, "shared host value did not round-trip through presets");
        Check(!setting.EnableLordUnitControls, "Lord-controls host value did not round-trip through presets");
        Check(!setting.EnableEliminatedPlayersBecomeSpectators,
            "spectator-promotion host value did not round-trip through presets");
        Check(!setting.EnableAbruptHostMigrationFix,
            "abrupt host-migration value did not round-trip through presets");
        Check(!setting.EnableReturnToMultiplayerLobby,
            "lobby-return host value did not round-trip through presets");
        Check(!setting.AllowFullAiMultiplayerLobby,
            "full-AI-lobby host value did not round-trip through presets");

        Dictionary<string, byte[]> stalePayload =
            MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(settingsPath));
        Dictionary<string, byte[]> stalePreset =
            MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(stalePayload["__SerpPreset1"]);
        stalePreset["EnableCustomLordExtendedPackages"] = MessagePackSerializer.Serialize(true);
        stalePayload["__SerpPreset1"] = MessagePackSerializer.Serialize(stalePreset);
        File.WriteAllBytes(settingsPath, MessagePackSerializer.Serialize(stalePayload));

        var migratedSetting = new SurrenderAndStatisticsSettingViewModel();
        migratedSetting.PreparePresets(null, pluginPath, "SurrenderAndStatisticsSettingTest");
        migratedSetting.ActivatePresets();
        Check(!migratedSetting.EnableAiFixes && !migratedSetting.EnableSurrenderAndStatistics,
            "an obsolete Custom Lord preset key prevented current settings from loading");
        string workspaceRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        string bugfixesViewModelSource = File.ReadAllText(
            Path.Combine(workspaceRoot, "BugfixesAndQoL", "src", "BugfixesAndQoLViewModel.cs"));
        Check(!bugfixesViewModelSource.Contains("EnableCustomLordExtendedPackages"),
            "the obsolete Custom Lord package setting remains in the BugfixesAndQoL view model");
        string normalizedBugfixesViewModelSource = bugfixesViewModelSource.Replace(Environment.NewLine, "\n");
        Check(normalizedBugfixesViewModelSource.Contains("private bool enableAssassinCombatResumeFix = true;") &&
              normalizedBugfixesViewModelSource.Contains("[SyncHostOnly]\n        public bool EnableAssassinCombatResumeFix") &&
              bugfixesViewModelSource.Contains("EnableAssassinCombatResumeFix = true;"),
            "Assassin combat resume is not a default-enabled, resettable host setting");
        string combatResumeRuntimeSource = File.ReadAllText(
            Path.Combine(workspaceRoot, "BugfixesAndQoL", "src", "AssassinCombatResumeRuntime.cs"));
        Check(combatResumeRuntimeSource.Contains("settings.EnableAssassinCombatResumeFix") &&
              !combatResumeRuntimeSource.Contains("EnableImprovedAssassinPathfinding"),
            "Assassin combat resume is not initialized independently from improved pathfinding");

        string[] movedBooleanSettings =
        {
            "EnableSingleBuildingPause",
            "EnableFastRecruitRallyMovement",
            "RequireReachableEnemyForAutomaticGateClosing",
            "EnableQuarryPileRelocation",
            "EnableAIQuarryPileTowardsKeep",
            "PreventAIPause",
            "PreventEmergencyDemolition",
            "PreventHovelDeletion"
        };
        foreach (string propertyName in movedBooleanSettings)
        {
            Check(normalizedBugfixesViewModelSource.Contains(
                    "[SyncHostOnly]\n        public bool " + propertyName),
                propertyName + " is not classified as a synchronized host setting");
            Check(bugfixesViewModelSource.Contains(propertyName + " = true;"),
                propertyName + " does not reset to its migrated default true value");
        }
        Check(normalizedBugfixesViewModelSource.Contains(
                "[SyncHostOnly]\n        public int InaccessibleAIBuildingDemolitionProtection"),
            "inaccessible AI building protection is not classified as a synchronized host setting");
        Check(bugfixesViewModelSource.Contains(
                "TemporaryGateBlockagePolicy.ImprovedReachabilityMode;"),
            "inaccessible AI building protection does not default to improved reachability");

        string extraFeaturesViewModelSource = File.ReadAllText(
            Path.Combine(workspaceRoot, "ExtraFeatures", "src", "ExtraFeaturesViewModel.cs"));
        foreach (string propertyName in movedBooleanSettings)
        {
            Check(!extraFeaturesViewModelSource.Contains("public bool " + propertyName),
                propertyName + " still has an active ExtraFeatures setting");
        }
        Check(!extraFeaturesViewModelSource.Contains(
                "public int InaccessibleAIBuildingDemolitionProtection"),
            "inaccessible AI building protection still has an active ExtraFeatures setting");

        string[] unrestrictedMovedRuntimeFiles =
        {
            "AIEconomyProtectionHook.cs",
            "BugfixesAndQoLRuntime.MovedFeatures.cs",
            "QuarryPileRelocationRuntime.cs",
            "ReachableEnemyGatehouseRuntime.cs",
            "SingleBuildingPauseHook.cs"
        };
        foreach (string fileName in unrestrictedMovedRuntimeFiles)
        {
            string source = File.ReadAllText(
                Path.Combine(workspaceRoot, "BugfixesAndQoL", "src", fileName));
            Check(!source.Contains("GameplayModActivationGate") &&
                  !source.Contains("GameplayFeatureModePolicy"),
                fileName + " still restricts a transferred BugfixesAndQoL feature by game mode");
        }
        string gameplayFeaturePolicySource = File.ReadAllText(
            Path.Combine(workspaceRoot, "Shared", "GameplayFeatureModePolicy.cs"));
        Check(!gameplayFeaturePolicySource.Contains("AIQuarryPileTowardsKeep"),
            "AI quarry-pile placement still has a restrictive per-feature game-mode policy");

        GameNetworkAPI.LocalHost = false;
        setting.System_RefreshSettingsAccess();
        byte[] beforeClientMutation = File.ReadAllBytes(settingsPath);
        setting.EnableAiFixes = true;
        setting.EnableSurrenderAndStatistics = true;
        setting.EnableLordUnitControls = true;
        setting.EnableEliminatedPlayersBecomeSpectators = true;
        setting.EnableAbruptHostMigrationFix = true;
        setting.EnableReturnToMultiplayerLobby = true;
        setting.AllowFullAiMultiplayerLobby = true;
        Check(!setting.EnableAiFixes, "client mutated the host-only EnableAiFixes setting");
        Check(beforeClientMutation.SequenceEqual(File.ReadAllBytes(settingsPath)),
            "client EnableAiFixes mutation changed the local preset file");
        Check(!setting.EnableSurrenderAndStatistics, "client mutated the host-only EnableSurrenderAndStatistics setting");
        Check(!setting.EnableLordUnitControls, "client mutated the host-only EnableLordUnitControls setting");
        Check(!setting.EnableEliminatedPlayersBecomeSpectators,
            "client mutated the host-only EnableEliminatedPlayersBecomeSpectators setting");
        Check(!setting.EnableAbruptHostMigrationFix,
            "client mutated the host-only EnableAbruptHostMigrationFix setting");
        Check(!setting.EnableReturnToMultiplayerLobby,
            "client mutated the host-only EnableReturnToMultiplayerLobby setting");
        Check(!setting.AllowFullAiMultiplayerLobby,
            "client mutated the host-only AllowFullAiMultiplayerLobby setting");
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(setting, () =>
        {
            setting.EnableAiFixes = true;
            setting.EnableSurrenderAndStatistics = true;
            setting.EnableLordUnitControls = true;
            setting.EnableEliminatedPlayersBecomeSpectators = true;
            setting.EnableAbruptHostMigrationFix = true;
            setting.EnableReturnToMultiplayerLobby = true;
            setting.AllowFullAiMultiplayerLobby = true;
        });
        Check(setting.EnableAiFixes, "authoritative host sync did not update EnableAiFixes");
        Check(setting.EnableSurrenderAndStatistics, "authoritative host sync did not update EnableSurrenderAndStatistics");
        Check(setting.EnableLordUnitControls, "authoritative host sync did not update EnableLordUnitControls");
        Check(setting.EnableEliminatedPlayersBecomeSpectators,
            "authoritative host sync did not update EnableEliminatedPlayersBecomeSpectators");
        Check(setting.EnableAbruptHostMigrationFix,
            "authoritative host sync did not update EnableAbruptHostMigrationFix");
        Check(setting.EnableReturnToMultiplayerLobby,
            "authoritative host sync did not update EnableReturnToMultiplayerLobby");
        Check(setting.AllowFullAiMultiplayerLobby,
            "authoritative host sync did not update AllowFullAiMultiplayerLobby");

        setting.System_EnterMissionPreset(
            new Dictionary<string, byte[]>
            {
                [nameof(setting.EnableAiFixes)] = MessagePackSerializer.Serialize(false),
                [nameof(setting.EnableSurrenderAndStatistics)] = MessagePackSerializer.Serialize(false),
                [nameof(setting.EnableLordUnitControls)] = MessagePackSerializer.Serialize(false),
                [nameof(setting.EnableEliminatedPlayersBecomeSpectators)] = MessagePackSerializer.Serialize(false),
                [nameof(setting.EnableAbruptHostMigrationFix)] = MessagePackSerializer.Serialize(false),
                [nameof(setting.EnableReturnToMultiplayerLobby)] = MessagePackSerializer.Serialize(false),
                [nameof(setting.AllowFullAiMultiplayerLobby)] = MessagePackSerializer.Serialize(false)
            },
            "Trail",
            editable: false);
        Check(!setting.EnableAiFixes, "read-only Trail did not apply EnableAiFixes");
        Check(!setting.EnableSurrenderAndStatistics && !setting.CanEditHostSettings,
            "read-only Trail did not apply and lock EnableSurrenderAndStatistics");
        Check(!setting.EnableLordUnitControls, "read-only Trail did not apply EnableLordUnitControls");
        Check(!setting.EnableEliminatedPlayersBecomeSpectators,
            "read-only Trail did not apply EnableEliminatedPlayersBecomeSpectators");
        Check(!setting.EnableAbruptHostMigrationFix,
            "read-only Trail did not apply EnableAbruptHostMigrationFix");
        Check(!setting.EnableReturnToMultiplayerLobby,
            "read-only Trail did not apply EnableReturnToMultiplayerLobby");
        Check(!setting.AllowFullAiMultiplayerLobby,
            "read-only Trail did not apply AllowFullAiMultiplayerLobby");
        setting.EnableAiFixes = true;
        setting.EnableSurrenderAndStatistics = true;
        setting.EnableLordUnitControls = true;
        setting.EnableEliminatedPlayersBecomeSpectators = true;
        setting.EnableAbruptHostMigrationFix = true;
        setting.EnableReturnToMultiplayerLobby = true;
        setting.AllowFullAiMultiplayerLobby = true;
        Check(!setting.EnableAiFixes, "client changed EnableAiFixes inside a read-only Trail");
        Check(!setting.EnableSurrenderAndStatistics, "client changed EnableSurrenderAndStatistics inside a read-only Trail");
        Check(!setting.EnableLordUnitControls, "client changed EnableLordUnitControls inside a read-only Trail");
        Check(!setting.EnableEliminatedPlayersBecomeSpectators,
            "client changed EnableEliminatedPlayersBecomeSpectators inside a read-only Trail");
        Check(!setting.EnableAbruptHostMigrationFix,
            "client changed EnableAbruptHostMigrationFix inside a read-only Trail");
        Check(!setting.EnableReturnToMultiplayerLobby,
            "client changed EnableReturnToMultiplayerLobby inside a read-only Trail");
        Check(!setting.AllowFullAiMultiplayerLobby,
            "client changed AllowFullAiMultiplayerLobby inside a read-only Trail");
        setting.System_ExitMissionPreset();

        GameNetworkAPI.LocalHost = true;
        setting.System_RefreshSettingsAccess();
        setting.ResetSurrenderAndStatistics();
        Check(setting.EnableAiFixes, "EnableAiFixes reset value was not true");
        Check(setting.EnableSurrenderAndStatistics, "EnableSurrenderAndStatistics reset value was not true");
        Check(setting.EnableLordUnitControls, "EnableLordUnitControls reset value was not true");
        Check(setting.EnableEliminatedPlayersBecomeSpectators,
            "EnableEliminatedPlayersBecomeSpectators reset value was not true");
        Check(setting.EnableAbruptHostMigrationFix,
            "EnableAbruptHostMigrationFix reset value was not true");
        Check(setting.EnableReturnToMultiplayerLobby,
            "EnableReturnToMultiplayerLobby reset value was not true");
        Check(setting.AllowFullAiMultiplayerLobby,
            "AllowFullAiMultiplayerLobby reset value was not true");
    }

    private static void TestMultiplayerLobbyReturnPolicy()
    {
        Check(MultiplayerLobbyReturnPolicy.IsSupportedSession(true, true, true, 0),
            "normal multiplayer was rejected for post-game lobby return");
        Check(!MultiplayerLobbyReturnPolicy.IsSupportedSession(false, true, true, 0) &&
              !MultiplayerLobbyReturnPolicy.IsSupportedSession(true, false, true, 0) &&
              !MultiplayerLobbyReturnPolicy.IsSupportedSession(true, true, false, 0),
            "disabled or non-multiplayer sessions enabled post-game lobby return");
        Check(!MultiplayerLobbyReturnPolicy.IsSupportedSession(true, true, true, 1),
            "Coop Trail incorrectly enabled the normal post-game lobby return");

        Check(MultiplayerLobbyReturnPolicy.ShouldCreateLobby(true, true, true, false),
            "current host did not create the first replacement lobby");
        Check(!MultiplayerLobbyReturnPolicy.ShouldCreateLobby(true, false, true, false) &&
              !MultiplayerLobbyReturnPolicy.ShouldCreateLobby(true, true, false, false) &&
              !MultiplayerLobbyReturnPolicy.ShouldCreateLobby(true, true, true, true),
            "invalid, client, or repeated game-over state created a replacement lobby");

        long frequency = 1000;
        long start = 5000;
        Check(!MultiplayerLobbyReturnPolicy.HasTimedOut(
                  start,
                  start + (MultiplayerLobbyReturnPolicy.ExitWaitTimeoutSeconds * frequency) - 1,
                  frequency) &&
              MultiplayerLobbyReturnPolicy.HasTimedOut(
                  start,
                  start + MultiplayerLobbyReturnPolicy.ExitWaitTimeoutSeconds * frequency,
                  frequency),
            "post-game lobby wait timeout boundary was incorrect");
    }

    private static void TestLocalPerPlayerSetting()
    {
        var setting = new LocalPerPlayerSetting<bool>(true);
        Check(setting.SetValue(false), "local per-player default could not be changed before an ID was available");
        Check(setting.Data.Skip(1).All(value => value),
            "an unavailable local player ID was silently treated as synchronized slot 1");
        Check(!setting.TrySetLocalPlayerId(0) && !setting.TrySetLocalPlayerId(9),
            "an invalid local player ID was accepted");

        Check(setting.TrySetLocalPlayerId(2) && !setting.Data[2],
            "the local value was not assigned to validated player slot 2");
        Check(setting.SetValue(true) && setting.Data[2],
            "a local change did not update validated player slot 2");
        setting.Data[1] = false;
        Check(setting.Value,
            "a remote player slot replaced the local persisted preference");

        Check(setting.TrySetLocalPlayerId(7) && setting.Data[7],
            "the local value was not assigned to validated player slot 7");
        Check(setting.SetValue(false) && !setting.Data[7] && setting.Data[2],
            "a local change updated the wrong validated player slot");
    }

    private static void TestSelectedUnitHealthSummary()
    {
        var empty = new SelectedUnitHealthSummary();
        Check(!empty.HasUnits, "empty selected-unit health summary reported units");

        var single = new SelectedUnitHealthSummary();
        single.Add(83, 120);
        Check(single.HasUnits && single.UnitCount == 1 &&
              single.FormatCurrent() == "8" && single.FormatMaximum() == "12",
            "single-unit health summary was incorrect");
        Check(single.Band == SelectedUnitHealthBand.Yellow,
            "health at 69 percent was not classified as yellow");

        var lordDisplay = new SelectedUnitHealthSummary();
        lordDisplay.Add(750000, 750000);
        Check(lordDisplay.FormatCurrent() == "75000" && lordDisplay.FormatMaximum() == "75000",
            "Lord-sized health values were not scaled through the shared display path");

        var thresholds = new SelectedUnitHealthSummary();
        thresholds.Add(75, 100);
        Check(thresholds.Band == SelectedUnitHealthBand.Green,
            "health at the 75-percent threshold was not classified as green");

        thresholds = new SelectedUnitHealthSummary();
        thresholds.Add(40, 100);
        Check(thresholds.Band == SelectedUnitHealthBand.Yellow,
            "health at the 40-percent threshold was not classified as yellow");

        thresholds = new SelectedUnitHealthSummary();
        thresholds.Add(39, 100);
        Check(thresholds.Band == SelectedUnitHealthBand.Red,
            "health below the 40-percent threshold was not classified as red");

        var multiple = new SelectedUnitHealthSummary();
        multiple.Add(83, 120);
        multiple.Add(1657, 1980);
        Check(multiple.UnitCount == 2 &&
              multiple.FormatCurrent() == "174" && multiple.FormatMaximum() == "210",
            "multi-unit health summary did not sum current and maximum health");

        multiple.Add(-1, 100);
        multiple.Add(10, 0);
        Check(multiple.UnitCount == 2 &&
              multiple.FormatCurrent() == "174" && multiple.FormatMaximum() == "210",
            "invalid or dead health entries changed the summary");

        Check(SelectedUnitHealthSummary.ScaleForDisplay(4) == 0 &&
              SelectedUnitHealthSummary.ScaleForDisplay(5) == 1 &&
              SelectedUnitHealthSummary.ScaleForDisplay(14) == 1 &&
              SelectedUnitHealthSummary.ScaleForDisplay(15) == 2,
            "selected-unit health display scaling did not round halves away from zero");

        var large = new SelectedUnitHealthSummary();
        large.Add(int.MaxValue, int.MaxValue);
        large.Add(int.MaxValue, int.MaxValue);
        Check(large.CurrentHealth == 4294967294L && large.MaximumHealth == 4294967294L,
            "selected-unit health totals overflowed 32-bit values");
        Check(large.FormatCurrent() == "429496729" && large.FormatMaximum() == "429496729",
            "large selected-unit health display scaling was incorrect");
        Check(large.Band == SelectedUnitHealthBand.Green,
            "large selected-unit health totals overflowed during color classification");

        var byType = new SelectedUnitHealthSummary[89];
        byType[22].Add(500, 1000);
        byType[22].Add(750, 1000);
        byType[24].Add(2000, 2000);
        Check(byType[22].FormatCurrent() == "125" && byType[22].FormatMaximum() == "200" &&
              byType[22].Band == SelectedUnitHealthBand.Yellow &&
              byType[24].FormatCurrent() == "200" && byType[24].FormatMaximum() == "200" &&
              byType[24].Band == SelectedUnitHealthBand.Green,
            "health totals or colors were not kept separate by troop type");

        var selectedTypes = new int[89];
        for (int type = 1; type <= 35; type++)
            selectedTypes[type] = 1;
        selectedTypes[55] = 1;

        int[] page1 = SelectedUnitHealthPageLayout.GetVisibleTypes(selectedTypes, 0);
        int[] page2 = SelectedUnitHealthPageLayout.GetVisibleTypes(selectedTypes, 1);
        int[] page3 = SelectedUnitHealthPageLayout.GetVisibleTypes(selectedTypes, 2);
        int[] page4 = SelectedUnitHealthPageLayout.GetVisibleTypes(selectedTypes, 3);
        Check(page1.SequenceEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }) &&
              page2.SequenceEqual(new[] { 9, 10, 11, 12, 13, 14, 15, 16 }) &&
              page3.SequenceEqual(new[] { 18, 19, 20, 21, 22, 23, 24, 25 }) &&
              page4.SequenceEqual(new[] { 27, 28, 29, 30, 31, 32, 33, 34 }),
            "selected-unit health page slots did not mirror the vanilla HUD ordering");

        int[] emptyPage = SelectedUnitHealthPageLayout.GetVisibleTypes(null, 0);
        Check(emptyPage.Length == SelectedUnitHealthPageLayout.SlotCount && emptyPage.All(type => type == -1),
            "an empty selected-unit health page exposed occupied slots");
    }

    private static void TestMarketGoodsOrderDefinition()
    {
        int[] hd = MarketGoodsOrderDefinition.CreateHdOrder();
        Check(MarketGoodsOrderDefinition.IsValid(hd), "HD market order was rejected");

        int[] duplicate = (int[])hd.Clone();
        duplicate[1] = duplicate[0];
        Check(!MarketGoodsOrderDefinition.IsValid(duplicate), "duplicate market good was accepted");
        Check(!MarketGoodsOrderDefinition.IsValid(hd.Take(hd.Length - 1).ToArray()),
            "short market order was accepted");
        int[] unknown = (int[])hd.Clone();
        unknown[0] = 999;
        Check(!MarketGoodsOrderDefinition.IsValid(unknown), "unknown market good was accepted");
        Check(MarketGoodsOrderDefinition.AreEqual(
                MarketGoodsOrderDefinition.CloneOrDefault(duplicate),
                hd),
            "invalid market order did not fall back to HD");

        int first = hd[0];
        int last = hd[hd.Length - 1];
        int[] wrapped = MarketGoodsOrderDefinition.SwapGoodWithNeighbor(hd, first, -1);
        Check(wrapped[0] == last && wrapped[wrapped.Length - 1] == first,
            "position 1 did not swap cyclically with position 20");
        Check(hd[0] == first && hd[hd.Length - 1] == last,
            "cyclic swap mutated its input array");

        int current = hd[3];
        int expected = hd[7];
        Check(MarketGoodsOrderDefinition.TryGetTradeableNeighbor(
                hd,
                current,
                1,
                good => good == expected,
                out int neighbor) && neighbor == expected,
            "market navigation did not skip unavailable goods");
        Check(!MarketGoodsOrderDefinition.TryGetTradeableNeighbor(
                hd,
                current,
                1,
                good => false,
                out _),
            "market navigation found a neighbor when every good was unavailable");
    }

    private static void TestArrayPerPlayerSetting()
    {
        int[] hd = MarketGoodsOrderDefinition.CreateHdOrder();
        var setting = new LocalPerPlayerSetting<int[]>(
            hd,
            MarketGoodsOrderDefinition.CloneOrDefault);
        Check(setting.TrySetLocalPlayerId(2), "array setting rejected valid player slot 2");
        setting.Data[1][0] = 999;
        Check(setting.Value[0] == hd[0] && setting.Data[2][0] == hd[0],
            "per-player array slots shared a mutable instance");

        int[] moved = MarketGoodsOrderDefinition.SwapGoodWithNeighbor(hd, hd[4], 1);
        Check(setting.SetValue(moved), "local array value was not updated");
        Check(MarketGoodsOrderDefinition.AreEqual(setting.Value, moved) &&
              MarketGoodsOrderDefinition.AreEqual(setting.Data[2], moved),
            "local array change did not reach its validated player slot");
        Check(!ReferenceEquals(setting.Value, setting.Data[2]),
            "local array and synchronized slot reused the same instance");

        setting.Data[7] = MarketGoodsOrderDefinition.CreateHdOrder();
        setting.Data[7][0] = 777;
        Check(setting.Value[0] == moved[0],
            "remote array slot replaced or mutated the local preference");
    }

    private static void TestMarketGoodPriceDefinition()
    {
        Check(MarketGoodPriceDefinition.Count == 20, "market price editor does not expose all 20 tradeable goods");
        int[] goods = Enumerable.Range(0, MarketGoodPriceDefinition.Count)
            .Select(MarketGoodPriceDefinition.GetGood)
            .ToArray();
        Check(goods.Distinct().Count() == goods.Length, "market price editor contains duplicate goods");

        double[] defaults = MarketGoodPriceDefinition.CreateDefaultMultipliers();
        Check(defaults.Length == 20 && defaults.All(value => value == 1.0),
            "per-good market multipliers do not default to 1.0");
        defaults[0] = 4.0;
        Check(MarketGoodPriceDefinition.CreateDefaultMultipliers()[0] == 1.0,
            "per-good market multiplier defaults share mutable state");

        double[] invalid = MarketGoodPriceDefinition.NormalizeMultipliers(new[] { 2.0 });
        Check(invalid.All(value => value == 1.0), "invalid market multiplier arrays were not reset safely");
        double[] values = MarketGoodPriceDefinition.CreateDefaultMultipliers();
        values[0] = -1.0;
        values[1] = 7.0;
        values[2] = 1.26;
        values[3] = double.NaN;
        double[] normalized = MarketGoodPriceDefinition.NormalizeMultipliers(values);
        Check(normalized[0] == 0.0 && normalized[1] == 5.0 && normalized[2] == 1.3 && normalized[3] == 1.0,
            "per-good market multipliers were not clamped and rounded correctly");
        Check(Math.Abs(MarketGoodPriceDefinition.CombineMultipliers(1.5, 2.0) - 3.0) < 0.0001,
            "general and per-good market multipliers were not combined multiplicatively");
    }

    private static void TestAIMarketVanillaPricePolicy()
    {
        int[] basePrices = { 0, 1, 4, 5, 6, 9, 10, 17, int.MaxValue, int.MinValue };
        int[] amounts = { 0, 1, 5, 25, 37, int.MaxValue, int.MinValue };
        foreach (int basePrice in basePrices)
        {
            foreach (int amount in amounts)
            {
                int expected = unchecked((basePrice / 5) * amount);
                Check(
                    AIMarketVanillaPricePolicy.CalculateTradeTotal(basePrice, amount) == expected,
                    $"AI Vanilla market arithmetic diverged for basePrice={basePrice}, amount={amount}");
            }
        }

        for (int mask = 0; mask < 32; mask++)
        {
            bool modEnabled = (mask & 1) != 0;
            bool alsoForAI = (mask & 2) != 0;
            bool validPlayer = (mask & 4) != 0;
            bool validGood = (mask & 8) != 0;
            bool isAI = (mask & 16) != 0;
            bool expected = modEnabled && !alsoForAI && validPlayer && validGood && isAI;
            Check(
                AIMarketVanillaPricePolicy.ShouldUseVanillaPrice(
                    modEnabled, alsoForAI, validPlayer, validGood, isAI) == expected,
                $"AI Vanilla market routing diverged for mask={mask}");
        }
    }

    private static void TestEnemyProximityPolicy()
    {
        Check(EnemyProximityPolicy.SelectConfiguredRadius(false, 31, 16) == 31 &&
              EnemyProximityPolicy.SelectConfiguredRadius(true, 31, 16) == 16,
            "enemy proximity did not select independent Singleplayer and real-Multiplayer values");
        Check(EnemyProximityPolicy.ResolveHumanImmediateRadius(-1, 30) == 30 &&
              EnemyProximityPolicy.ResolveHumanImmediateRadius(-1, 15) == 15,
            "Vanilla human immediate proximity values were not restored for -1/disable");
        Check(EnemyProximityPolicy.ApplyHumanPlacementRadius(30, -1) == 30 &&
              EnemyProximityPolicy.ApplyHumanPlacementRadius(15, -1) == 15,
            "human -1 mode changed an original proximity parameter");
        Check(EnemyProximityPolicy.ApplyHumanPlacementRadius(30, 44) == 44 &&
              EnemyProximityPolicy.ApplyHumanPlacementRadius(15, 7) == 7,
            "normal human 30/15 placement paths did not use the configured radius");
        Check(EnemyProximityPolicy.ApplyHumanPlacementRadius(3, 44) == 3 &&
              EnemyProximityPolicy.ApplyHumanPlacementRadius(5, 44) == 5,
            "special human placement paths were incorrectly overridden");
        Check(EnemyProximityPolicy.ApplyAIRadius(15, 42, false) == 15,
            "an unclassified AI initial placement was overridden");
        Check(EnemyProximityPolicy.ApplyAIRadius(5, -1, true) == 5,
            "classified AI -1 mode changed its context-specific Vanilla radius");
        Check(EnemyProximityPolicy.ApplyAIRadius(5, 42, true) == 42,
            "a classified AI repair/rebuild did not use the active configured radius");
    }

    private static void TestAssassinClimbCostPolicy()
    {
        Check(AssassinClimbTransitionPolicy.CanUseStartTile(false, 0, 0),
            "Assassin climb transition policy rejected Vanilla's free start tile");
        Check(!AssassinClimbTransitionPolicy.CanUseStartTile(false, 42, byte.MaxValue),
            "disabled improved Assassin pathfinding relaxed a reserved start tile");
        Check(AssassinClimbTransitionPolicy.CanUseStartTile(true, 42, 1) &&
              AssassinClimbTransitionPolicy.CanUseStartTile(true, 42, byte.MaxValue),
            "improved Assassin pathfinding rejected a walkable reserved start tile");
        Check(!AssassinClimbTransitionPolicy.CanUseStartTile(true, 42, 0),
            "improved Assassin pathfinding accepted an impassable building start tile");
        Check(AssassinClimbTransitionPolicy.CanUseTargetTile(false, 0, 0),
            "Assassin climb transition policy rejected Vanilla's free target tile");
        Check(!AssassinClimbTransitionPolicy.CanUseTargetTile(false, 42, byte.MaxValue),
            "disabled improved Assassin pathfinding relaxed a reserved target tile");
        Check(AssassinClimbTransitionPolicy.CanUseTargetTile(true, 42, 1) &&
              AssassinClimbTransitionPolicy.CanUseTargetTile(true, 42, byte.MaxValue),
            "improved Assassin pathfinding rejected a walkable reserved target tile");
        Check(!AssassinClimbTransitionPolicy.CanUseTargetTile(true, 42, 0),
            "improved Assassin pathfinding accepted an impassable building target tile");
        Check(AssassinClimbTransitionPolicy.ShouldRelaxPathReconstruction(true, true, true),
            "enabled improved Assassin pathfinding did not relax Vanilla's matching reconstruction guard");
        Check(!AssassinClimbTransitionPolicy.ShouldRelaxPathReconstruction(false, true, true) &&
              !AssassinClimbTransitionPolicy.ShouldRelaxPathReconstruction(true, false, true) &&
              !AssassinClimbTransitionPolicy.ShouldRelaxPathReconstruction(true, true, false),
            "Assassin reconstruction guard was relaxed outside the fully installed enabled feature");

        Check(AssassinClimbCostPolicy.GetCardinalMovementTicks(1) == 16,
            "Assassin cardinal movement did not include eight Vanilla substeps and the delay threshold");
        Check(AssassinClimbCostPolicy.GetDiagonalMovementTicks(1) == 23,
            "Assassin diagonal movement did not use deterministic sqrt(2) scaling");
        Check(AssassinClimbCostPolicy.MinimumClimbTicks / AssassinClimbCostPolicy.GetCardinalMovementTicks(1) == 5 &&
              AssassinClimbCostPolicy.LowWallClimbTicks / AssassinClimbCostPolicy.GetCardinalMovementTicks(1) == 15 &&
              AssassinClimbCostPolicy.NormalWallClimbTicks / AssassinClimbCostPolicy.GetCardinalMovementTicks(1) == 25,
            "Assassin climb break-even field counts changed unexpectedly");
        Check(AssassinClimbCostPolicy.GetAdditionalTicks(false, 90, false, true, false) == 0,
            "ordinary Assassin movement received a climbing surcharge");
        Check(AssassinClimbCostPolicy.GetAdditionalTicks(true, 0, false, true, false) == 0,
            "level Assassin wall movement received a climbing surcharge");
        Check(AssassinClimbCostPolicy.GetAdditionalTicks(true, -90, false, true, false) == 80,
            "Assassin descent did not cost 80 ticks");
        Check(AssassinClimbCostPolicy.GetAdditionalTicks(true, 25, true, true, false) == 240,
            "Assassin low-wall ascent did not cost 240 ticks");
        Check(AssassinClimbCostPolicy.GetAdditionalTicks(true, 90, false, true, false) == 400,
            "Assassin normal-wall ascent did not cost 400 ticks");
        Check(AssassinClimbCostPolicy.GetAdditionalTicks(true, 1, false, true, true) == 80,
            "Assassin stair ascent did not enforce the 80-tick startup minimum");
        Check(AssassinClimbCostPolicy.GetAdditionalTicks(true, 45, false, true, true) == 200,
            "Assassin stair ascent was not interpolated by height");
        Check(AssassinClimbCostPolicy.GetAdditionalTicks(true, 135, false, true, true) == 600,
            "Assassin high stair ascent did not continue the interpolation slope");
    }

    private static void TestAssassinPathReconstructionNativeDefinition()
    {
        const string dllPath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        byte[] file = File.ReadAllBytes(dllPath);
        string hash;
        using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create())
            hash = BitConverter.ToString(sha256.ComputeHash(file)).Replace("-", string.Empty);
        Check(
            string.Equals(
                hash,
                AssassinPathReconstructionNativeDefinition.ReferenceSha256,
                StringComparison.OrdinalIgnoreCase),
            "canonical CrusaderDE.dll hash changed for the Assassin reconstruction contract");

        byte[] image = LoadPeImage(file);
        NativeResolution reference = NativePatternResolver.ResolveUnique(
            image,
            AssassinPathReconstructionNativeDefinition.EndpointBuildingGuardsPattern,
            AssassinPathReconstructionNativeDefinition.EndpointBuildingGuardsPatternRva,
            referenceHashMatches: true,
            "test Assassin reconstruction endpoint guards");
        Check(
            reference.Rva == AssassinPathReconstructionNativeDefinition.EndpointBuildingGuardsPatternRva &&
            reference.Method == "reference-rva",
            "Assassin reconstruction source signature did not match the canonical DLL at its reference RVA");

        NativeResolution fallback = NativePatternResolver.ResolveUnique(
            image,
            AssassinPathReconstructionNativeDefinition.EndpointBuildingGuardsPattern,
            AssassinPathReconstructionNativeDefinition.EndpointBuildingGuardsPatternRva,
            referenceHashMatches: false,
            "test Assassin reconstruction endpoint guards");
        Check(
            fallback.Rva == reference.Rva && fallback.Method == "signature-fallback",
            "Assassin reconstruction source signature was not unique in the canonical DLL");

        int currentJumpRva = reference.Rva +
            AssassinPathReconstructionNativeDefinition.CurrentTileRejectJumpOffset;
        int neighborJumpRva = reference.Rva +
            AssassinPathReconstructionNativeDefinition.NeighborTileRejectJumpOffset;
        Check(
            image.Skip(currentJumpRva)
                .Take(AssassinPathReconstructionNativeDefinition.OriginalCurrentTileRejectJump.Length)
                .SequenceEqual(AssassinPathReconstructionNativeDefinition.OriginalCurrentTileRejectJump) &&
            image.Skip(neighborJumpRva)
                .Take(AssassinPathReconstructionNativeDefinition.OriginalNeighborTileRejectJump.Length)
                .SequenceEqual(AssassinPathReconstructionNativeDefinition.OriginalNeighborTileRejectJump),
            "Assassin reconstruction jump offsets no longer select both audited Vanilla guards");
    }

    private static void TestAssassinCombatResumePolicy()
    {
        Check(AssassinCombatResumePolicy.TryConvertUnitIdToSpanIndex(1, 10000, out int firstSpanIndex) &&
              firstSpanIndex == 0 &&
              AssassinCombatResumePolicy.TryConvertUnitIdToSpanIndex(10000, 10000, out int lastSpanIndex) &&
              lastSpanIndex == 9999 &&
              AssassinCombatResumePolicy.TryConvertUnitIdToSpanIndex(7, 10000, out int observedSpanIndex) &&
              observedSpanIndex == 6,
            "Assassin combat resume did not convert one-based unit IDs exactly once");
        Check(!AssassinCombatResumePolicy.TryConvertUnitIdToSpanIndex(0, 10000, out _) &&
              !AssassinCombatResumePolicy.TryConvertUnitIdToSpanIndex(-1, 10000, out _) &&
              !AssassinCombatResumePolicy.TryConvertUnitIdToSpanIndex(10001, 10000, out _) &&
              !AssassinCombatResumePolicy.TryConvertUnitIdToSpanIndex(1, 0, out _),
            "Assassin combat resume accepted an invalid one-based unit ID");

        Check(AssassinCombatResumePolicy.ShouldProcessPostCombatPathRequest(true, true, true, true),
            "eligible Assassin post-combat caller did not enter unit inspection");
        Check(!AssassinCombatResumePolicy.ShouldProcessPostCombatPathRequest(false, true, true, true) &&
              !AssassinCombatResumePolicy.ShouldProcessPostCombatPathRequest(true, false, true, true) &&
              !AssassinCombatResumePolicy.ShouldProcessPostCombatPathRequest(true, true, false, true) &&
              !AssassinCombatResumePolicy.ShouldProcessPostCombatPathRequest(true, true, true, false),
            "Assassin post-combat caller gate did not fail closed outside its enabled audited context");
        foreach (bool combatResumeEnabled in new[] { false, true })
        {
            foreach (bool improvedPathfindingEnabled in new[] { false, true })
            {
                bool resumes = AssassinCombatResumePolicy.ShouldProcessPostCombatPathRequest(
                    true, combatResumeEnabled, true, true);
                Check(resumes == combatResumeEnabled,
                    $"combat-resume gate incorrectly depended on improved pathfinding={improvedPathfindingEnabled}");
            }
        }

        Check(AssassinCombatResumePolicy.IsEligibleAssassin(
                true, AliveState.IsAlive, eChimps.CHIMP_TYPE_ARAB_ASSASIN, 106),
            "eligible living Assassin in state 106 was rejected");
        Check(!AssassinCombatResumePolicy.IsEligibleAssassin(
                false, AliveState.IsAlive, eChimps.CHIMP_TYPE_ARAB_ASSASIN, 106) &&
              !AssassinCombatResumePolicy.IsEligibleAssassin(
                true, AliveState.MarkedForDeletion, eChimps.CHIMP_TYPE_ARAB_ASSASIN, 106) &&
              !AssassinCombatResumePolicy.IsEligibleAssassin(
                true, AliveState.IsAlive, eChimps.CHIMP_TYPE_KNIGHT, 106) &&
              !AssassinCombatResumePolicy.IsEligibleAssassin(
                true, AliveState.IsAlive, eChimps.CHIMP_TYPE_ARAB_ASSASIN, 101),
            "Assassin post-combat unit gate accepted an unresolved, dead, foreign, or wrong-state unit");

    }

    private static void TestAssassinCombatResumeNativeDefinition()
    {
        const string dllPath = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
        byte[] file = File.ReadAllBytes(dllPath);
        string hash;
        using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create())
            hash = BitConverter.ToString(sha256.ComputeHash(file)).Replace("-", string.Empty);
        Check(
            string.Equals(hash, AssassinCombatResumeNativeDefinition.ReferenceSha256, StringComparison.OrdinalIgnoreCase),
            "canonical CrusaderDE.dll hash changed for the Assassin combat-resume contract");

        byte[] image = LoadPeImage(file);
        NativeResolution state106Callsite = NativePatternResolver.ResolveUnique(
            image,
            AssassinCombatResumeNativeDefinition.State106CombatFinishCallSequence,
            AssassinCombatResumeNativeDefinition.State106CombatFinishCallSequenceRva,
            referenceHashMatches: false,
            "test Assassin state-106 combat-finish callsite");
        int state106CallRva = state106Callsite.Rva +
            AssassinCombatResumeNativeDefinition.State106CombatFinishCallOffset;
        int state106CallTarget = NativePatternResolver.ResolveRelativeTarget(
            image, state106CallRva + 1, state106CallRva + 5);
        Check(
            state106Callsite.Method == "signature-fallback" &&
            state106CallRva == AssassinCombatResumeNativeDefinition.State106CombatFinishCallRva &&
            state106CallRva + 5 == AssassinCombatResumeNativeDefinition.State106CombatFinishReturnRva &&
            state106CallTarget == AssassinCombatResumeNativeDefinition.CombatFinishHelperRva,
            "Assassin state 106 no longer enters the audited combat-finish helper");

        NativeResolution combatFinish = NativePatternResolver.ResolveUnique(
            image,
            AssassinCombatResumeNativeDefinition.CombatFinishHelperSequence,
            AssassinCombatResumeNativeDefinition.CombatFinishHelperSequenceRva,
            referenceHashMatches: false,
            "test combat-finish resume helper callsite");
        int resumeCallRva = combatFinish.Rva +
            AssassinCombatResumeNativeDefinition.CombatFinishResumeCallOffset;
        int resumeCallTarget = NativePatternResolver.ResolveRelativeTarget(
            image, resumeCallRva + 1, resumeCallRva + 5);
        Check(
            combatFinish.Method == "signature-fallback" &&
            resumeCallRva == AssassinCombatResumeNativeDefinition.CombatFinishResumeCallRva &&
            resumeCallRva + 5 == AssassinCombatResumeNativeDefinition.CombatFinishResumeReturnRva &&
            resumeCallTarget == AssassinCombatResumeNativeDefinition.PostCombatRepathRva,
            "combat-finish helper no longer enters the audited post-combat repath helper");

        NativeResolution repathPrologue = NativePatternResolver.ResolveUnique(
            image,
            AssassinCombatResumeNativeDefinition.PostCombatRepathPrologueSequence,
            AssassinCombatResumeNativeDefinition.PostCombatRepathPrologueRva,
            referenceHashMatches: false,
            "test post-combat repath helper prologue");
        Check(
            repathPrologue.Method == "signature-fallback" &&
            repathPrologue.Rva == AssassinCombatResumeNativeDefinition.PostCombatRepathRva &&
            AssassinCombatResumeNativeDefinition.PostCombatCallerReturnAddressStackOffset ==
                sizeof(ulong) + 0x30,
            "post-combat repath prologue no longer preserves its caller return address at RSP+0x38");

        NativeResolution pathRequest = NativePatternResolver.ResolveUnique(
            image,
            AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequence,
            AssassinCombatResumeNativeDefinition.PostCombatPathRequestSequenceRva,
            referenceHashMatches: false,
            "test post-combat saved-state path request");
        int pathRequestCallRva = pathRequest.Rva +
            AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallOffset;
        int pathRequestTarget = NativePatternResolver.ResolveRelativeTarget(
            image, pathRequestCallRva + 1, pathRequestCallRva + 5);
        int finalizeCallRva = pathRequest.Rva +
            AssassinCombatResumeNativeDefinition.PostCombatFinalizeCallOffset;
        int finalizeTarget = NativePatternResolver.ResolveRelativeTarget(
            image, finalizeCallRva + 1, finalizeCallRva + 5);
        Check(
            pathRequest.Method == "signature-fallback" &&
            pathRequestCallRva == AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallRva &&
            pathRequestCallRva == pathRequest.Rva +
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallOffset &&
            pathRequestTarget == AssassinCombatResumeNativeDefinition.CommonPathRequestRva &&
            finalizeCallRva == AssassinCombatResumeNativeDefinition.PostCombatFinalizeCallRva &&
            finalizeTarget == AssassinCombatResumeNativeDefinition.PostPathRequestRva,
            "post-combat helper no longer restores the saved state and target through the audited path calls");
        Check(
            AssassinCombatResumeNativeDefinition.PostCombatPathContextHookLength ==
                AssassinCombatResumeNativeDefinition.PostCombatPathContextHookBytes.Length &&
            AssassinCombatResumeNativeDefinition.PostCombatPathContextHookLength >=
                AssassinCombatResumeNativeDefinition.InlineHookMinimumOverwriteLength &&
            image.Skip(AssassinCombatResumeNativeDefinition.PostCombatPathContextHookRva)
                .Take(AssassinCombatResumeNativeDefinition.PostCombatPathContextHookLength)
                .SequenceEqual(AssassinCombatResumeNativeDefinition.PostCombatPathContextHookBytes) &&
            AssassinCombatResumeNativeDefinition.PostCombatPathContextHookRva +
                AssassinCombatResumeNativeDefinition.PostCombatPathContextHookLength ==
                AssassinCombatResumeNativeDefinition.PostCombatRestoredStateWriteRva &&
            image.Skip(AssassinCombatResumeNativeDefinition.PostCombatRestoredStateWriteRva)
                .Take(AssassinCombatResumeNativeDefinition.PostCombatRestoredStateWriteBytes.Length)
                .SequenceEqual(AssassinCombatResumeNativeDefinition.PostCombatRestoredStateWriteBytes) &&
            AssassinCombatResumeNativeDefinition.PostCombatRestoredStateWriteRva +
                AssassinCombatResumeNativeDefinition.PostCombatRestoredStateWriteBytes.Length ==
                AssassinCombatResumeNativeDefinition.PostCombatPathRequestCallRva,
            "post-combat Assassin context hook no longer preserves complete instruction boundaries before the path call");

        NativeResolution contextRead = NativePatternResolver.ResolveUnique(
            image,
            AssassinCombatResumeNativeDefinition.CommonPathContextReadSequence,
            AssassinCombatResumeNativeDefinition.CommonPathContextReadRva,
            referenceHashMatches: false,
            "test common path Assassin-context read");
        int readFlagTarget = NativePatternResolver.ResolveRelativeTarget(
            image,
            contextRead.Rva + 3,
            contextRead.Rva + 7);
        Check(
            readFlagTarget == AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva,
            "the common path request no longer reads the audited Assassin context flag");

        NativeResolution successClear = NativePatternResolver.ResolveUnique(
            image,
            AssassinCombatResumeNativeDefinition.CommonPathSuccessClearSequence,
            AssassinCombatResumeNativeDefinition.CommonPathSuccessClearSequenceRva,
            referenceHashMatches: false,
            "test common path success context clear");
        int successClearInstruction = successClear.Rva +
            AssassinCombatResumeNativeDefinition.CommonPathSuccessFlagClearOffset;
        int successClearTarget = NativePatternResolver.ResolveRelativeTarget(
            image,
            successClearInstruction + 3,
            successClearInstruction + 7);
        NativeResolution failureClear = NativePatternResolver.ResolveUnique(
            image,
            AssassinCombatResumeNativeDefinition.CommonPathFailureClearSequence,
            AssassinCombatResumeNativeDefinition.CommonPathFailureClearRva,
            referenceHashMatches: false,
            "test common path failure context clear");
        int failureClearTarget = NativePatternResolver.ResolveRelativeTarget(
            image,
            failureClear.Rva + 3,
            failureClear.Rva + 7);
        Check(
            successClearTarget == AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva &&
            failureClearTarget == AssassinCombatResumeNativeDefinition.AssassinPathContextFlagRva,
            "the common path request no longer clears the Assassin context on both audited exits");

        NativeResolution dispatcher = NativePatternResolver.ResolveUnique(
            image,
            AssassinCombatResumeNativeDefinition.DispatcherAssassinBranchPattern,
            AssassinCombatResumeNativeDefinition.DispatcherAssassinBranchRva,
            referenceHashMatches: false,
            "test Assassin path-builder dispatcher branch");
        int assassinBuilderTarget = NativePatternResolver.ResolveRelativeTarget(
            image,
            dispatcher.Rva + AssassinCombatResumeNativeDefinition.DispatcherAssassinBuilderCallOffset + 1,
            dispatcher.Rva + AssassinCombatResumeNativeDefinition.DispatcherAssassinBuilderCallOffset + 5);
        Check(
            assassinBuilderTarget == AssassinCombatResumeNativeDefinition.AssassinPathBuilderRva,
            "path dispatcher no longer routes the special context to the Assassin path builder");
    }

    private static void TestAssassinClimbCancellationPolicy()
    {
        Check(AssassinClimbCancellationPolicy.ShouldHandleCommand(
                true, true, true, true, EventHookPhase.Pre,
                (uint)TribeAICommand.UnitStop),
            "Assassin climb cancellation rejected the Script Extender Pre event for Vanilla's synchronized UnitStop command");
        Check(!AssassinClimbCancellationPolicy.ShouldHandleCommand(
                false, true, true, true, EventHookPhase.Pre,
                (uint)TribeAICommand.UnitStop) &&
              !AssassinClimbCancellationPolicy.ShouldHandleCommand(
                true, false, true, true, EventHookPhase.Pre,
                (uint)TribeAICommand.UnitStop) &&
              !AssassinClimbCancellationPolicy.ShouldHandleCommand(
                true, true, false, true, EventHookPhase.Pre,
                (uint)TribeAICommand.UnitStop) &&
              !AssassinClimbCancellationPolicy.ShouldHandleCommand(
                true, true, true, false, EventHookPhase.Pre,
                (uint)TribeAICommand.UnitStop) &&
              !AssassinClimbCancellationPolicy.ShouldHandleCommand(
                true, true, true, true, EventHookPhase.Post,
                (uint)TribeAICommand.UnitStop) &&
              !AssassinClimbCancellationPolicy.ShouldHandleCommand(
                true, true, true, true, EventHookPhase.Pre,
                (uint)TribeAICommand.UnitStop + 1),
            "Assassin climb cancellation did not fail closed outside the enabled, layout-validated UnitStop Pre event");
        Check(AssassinClimbCancellationPolicy.IsClimbingState(126) &&
              AssassinClimbCancellationPolicy.IsClimbingState(127) &&
              AssassinClimbCancellationPolicy.IsClimbingState(128) &&
              AssassinClimbCancellationPolicy.IsClimbingState(129) &&
              !AssassinClimbCancellationPolicy.IsClimbingState(125) &&
              !AssassinClimbCancellationPolicy.IsClimbingState(130),
            "Assassin climb-stop state filter does not cover exactly states 126 through 129");
    }

    private static void TestTroopActionButtonLayoutPolicy()
    {
        Check(!TroopActionButtonLayoutPolicy.IsEffectivelyOccupied(false, true) &&
              !TroopActionButtonLayoutPolicy.IsEffectivelyOccupied(true, false) &&
              TroopActionButtonLayoutPolicy.IsEffectivelyOccupied(true, true),
            "shared troop action collision policy did not require effective visibility and interactivity");

        Check(TroopActionButtonLayoutPolicy.TryResolveIdentity(
                "SerpTroopAction_Host", 1, "Example.Mod.Action", 150,
                out int explicitPriority, out string explicitActionId) &&
              explicitPriority == 150 && explicitActionId == "Example.Mod.Action",
            "shared troop action metadata contract rejected valid explicit metadata");
        Check(TroopActionButtonLayoutPolicy.TryResolveIdentity(
                "SerpTroopAction_0125_LegacyAction", null, null, null,
                out int legacyPriority, out string legacyActionId) &&
              legacyPriority == 125 && legacyActionId == "LegacyAction",
            "shared troop action metadata contract rejected the legacy host format");
        Check(!TroopActionButtonLayoutPolicy.TryResolveIdentity(
                "SerpTroopAction_Host", 2, "Example.Mod.Action", 150, out _, out _) &&
              !TroopActionButtonLayoutPolicy.TryResolveIdentity(
                "SerpTroopAction_Host", 1, "Example.Mod.Action", null, out _, out _),
            "shared troop action metadata contract accepted an unknown version or incomplete metadata");

        var knight = new TroopActionRequest("ExtraFeatures_Serp.KnightTransform", 100, true);
        var assassin = new TroopActionRequest("BugfixesAndQoL_Serp.AssassinClimb", 200, true);
        TroopActionLayoutDecision bothFree = TroopActionButtonLayoutPolicy.CreateDecision(
            new[] { assassin, knight }, false, false);
        Check(bothFree.Assignments.Select(value => $"{value.ActionId}:{value.Slot}").SequenceEqual(new[]
        {
            "ExtraFeatures_Serp.KnightTransform:BottomRight",
            "BugfixesAndQoL_Serp.AssassinClimb:BottomMiddle"
        }), "shared troop actions did not use priority order for the two free slots");

        TroopActionLayoutDecision reverseOrder = TroopActionButtonLayoutPolicy.CreateDecision(
            new[] { knight, assassin }, false, false);
        Check(reverseOrder.Assignments.Select(value => $"{value.ActionId}:{value.Slot}")
                .SequenceEqual(bothFree.Assignments.Select(value => $"{value.ActionId}:{value.Slot}")),
            "shared troop action assignment depended on registration or mod load order");

        TroopActionLayoutDecision rightOccupied = TroopActionButtonLayoutPolicy.CreateDecision(
            new[] { assassin, knight }, true, false);
        Check(rightOccupied.Assignments.Count == 1 &&
              rightOccupied.Assignments[0].ActionId == knight.ActionId &&
              rightOccupied.Assignments[0].Slot == TroopActionSlot.BottomMiddle &&
              rightOccupied.OverflowActionIds.SequenceEqual(new[] { assassin.ActionId }),
            "shared troop actions did not give the remaining middle slot to the highest priority action");

        TroopActionLayoutDecision middleOccupied = TroopActionButtonLayoutPolicy.CreateDecision(
            new[] { assassin, knight }, false, true);
        Check(middleOccupied.Assignments.Count == 1 &&
              middleOccupied.Assignments[0].ActionId == knight.ActionId &&
              middleOccupied.Assignments[0].Slot == TroopActionSlot.BottomRight,
            "shared troop actions did not preserve the working bottom-right Knight slot");

        TroopActionLayoutDecision bothOccupied = TroopActionButtonLayoutPolicy.CreateDecision(
            new[] { assassin, knight }, true, true);
        Check(bothOccupied.Assignments.Count == 0 && bothOccupied.OverflowActionIds.Count == 2,
            "shared troop actions overlaid occupied Vanilla/foreign slots");

        TroopActionLayoutDecision assassinOnly = TroopActionButtonLayoutPolicy.CreateDecision(
            new[] { assassin }, false, false);
        Check(assassinOnly.Assignments.Count == 1 &&
              assassinOnly.Assignments[0].Slot == TroopActionSlot.BottomRight,
            "a lone Assassin action did not receive the preferred bottom-right slot");

        TroopActionLayoutDecision knightOnly = TroopActionButtonLayoutPolicy.CreateDecision(
            new[] { knight }, false, false);
        Check(knightOnly.Assignments.Count == 1 &&
              knightOnly.Assignments[0].ActionId == knight.ActionId &&
              knightOnly.Assignments[0].Slot == TroopActionSlot.BottomRight,
            "a lone Knight action did not receive the preferred bottom-right slot");

        TroopActionLayoutDecision threeActions = TroopActionButtonLayoutPolicy.CreateDecision(
            new[]
            {
                new TroopActionRequest("Example.Mod.Third", 300, true),
                assassin,
                knight
            },
            false,
            false);
        Check(threeActions.Assignments.Count == 2 &&
              threeActions.OverflowActionIds.SequenceEqual(new[] { "Example.Mod.Third" }),
            "shared troop action overflow did not hide the lowest priority third action");

        TroopActionLayoutDecision tied = TroopActionButtonLayoutPolicy.CreateDecision(
            new[]
            {
                new TroopActionRequest("Example.Mod.B", 100, true),
                new TroopActionRequest("Example.Mod.A", 100, true)
            },
            false,
            false);
        Check(tied.Assignments[0].ActionId == "Example.Mod.A" &&
              tied.Assignments[1].ActionId == "Example.Mod.B",
            "equal troop action priorities were not resolved by ordinal action id");

        TroopActionLayoutDecision duplicate = TroopActionButtonLayoutPolicy.CreateDecision(
            new[]
            {
                new TroopActionRequest("Example.Mod.Duplicate", 100, true),
                new TroopActionRequest("Example.Mod.Duplicate", 200, true),
                knight
            },
            false,
            false);
        Check(duplicate.DuplicateActionIds.SequenceEqual(new[] { "Example.Mod.Duplicate" }) &&
              duplicate.Assignments.Count == 1 && duplicate.Assignments[0].ActionId == knight.ActionId,
            "duplicate troop action ids did not fail closed without blocking unrelated actions");
        TroopActionLayoutDecision hiddenDuplicate = TroopActionButtonLayoutPolicy.CreateDecision(
            new[]
            {
                new TroopActionRequest("Example.Mod.HiddenDuplicate", 100, true),
                new TroopActionRequest("Example.Mod.HiddenDuplicate", 200, false)
            },
            false,
            false);
        Check(hiddenDuplicate.DuplicateActionIds.SequenceEqual(new[] { "Example.Mod.HiddenDuplicate" }) &&
              hiddenDuplicate.Assignments.Count == 0,
            "a hidden duplicate troop action id did not make the shared contract fail closed");
    }

    private static void TestTemporaryGateBlockagePolicy()
    {
        Check(!TemporaryGateBlockagePolicy.ShouldSuppressDemolition(
                TemporaryGateBlockagePolicy.VanillaMode, true, true, true),
            "Vanilla mode suppressed inaccessible-building demolition");
        Check(TemporaryGateBlockagePolicy.ShouldSuppressDemolition(
                TemporaryGateBlockagePolicy.ImprovedReachabilityMode, true, true, true),
            "improved-check mode did not suppress a reachable AI building demolition");
        Check(!TemporaryGateBlockagePolicy.ShouldSuppressDemolition(
                TemporaryGateBlockagePolicy.ImprovedReachabilityMode, true, false, true),
            "improved-check mode did not fail open without a classification");
        Check(!TemporaryGateBlockagePolicy.ShouldSuppressDemolition(
                TemporaryGateBlockagePolicy.ImprovedReachabilityMode, true, true, false),
            "improved-check mode suppressed a building unreachable even with friendly gates");
        Check(TemporaryGateBlockagePolicy.ShouldSuppressDemolition(
                TemporaryGateBlockagePolicy.AlwaysPreventMode, true, false, false),
            "always-prevent mode did not suppress the dedicated AI demolition path");
        Check(!TemporaryGateBlockagePolicy.ShouldSuppressDemolition(
                TemporaryGateBlockagePolicy.AlwaysPreventMode, false, true, true),
            "always-prevent mode affected a non-AI or non-living building");

        Func<int, int, bool> nativeUnreachable = (_, __) => false;
        Func<int, int, bool> nativeReachable = (_, __) => true;

        GateBlockageEvaluation direct = TemporaryGateBlockagePolicy.Evaluate(
            new[] { 10 }, new[] { 10 }, Array.Empty<PclGateConnection>(), nativeUnreachable);
        Check(direct.Kind == GateBlockageEvaluationKind.ReachableWithoutFriendlyGate &&
              direct.HasDirectPclPath && direct.HasPathWithFriendlyGates &&
              direct.IsReachableUnderImprovedCheck,
            "a shared terrain PCL was not classified as reachable");

        var ownGate = new PclGateConnection(10, 20, ownerId: 5, buildingId: 40, globalId: 4000);
        GateBlockageEvaluation closedOwnGate = TemporaryGateBlockagePolicy.Evaluate(
            new[] { 10 }, new[] { 20 }, new[] { ownGate }, nativeUnreachable);
        GateBlockageEvaluation openOwnGate = TemporaryGateBlockagePolicy.Evaluate(
            new[] { 10 }, new[] { 20 }, new[] { ownGate }, nativeReachable);
        Check(closedOwnGate.Kind == GateBlockageEvaluationKind.ReachableViaFriendlyGate &&
              openOwnGate.Kind == GateBlockageEvaluationKind.ReachableViaFriendlyGate &&
              closedOwnGate.NativePlayerAwareReachable == false &&
              openOwnGate.NativePlayerAwareReachable == true &&
              closedOwnGate.UsedGateIndices.SequenceEqual(new[] { 0 }) &&
              openOwnGate.UsedGateIndices.SequenceEqual(new[] { 0 }),
            "current gate state changed the always-passable friendly-gate graph result");

        // A raised or lowered drawbridge is represented by the same gatehouse entry/exit link.
        GateBlockageEvaluation drawbridge = TemporaryGateBlockagePolicy.Evaluate(
            new[] { 20 }, new[] { 10 }, new[] { ownGate }, nativeUnreachable);
        Check(drawbridge.Kind == GateBlockageEvaluationKind.ReachableViaFriendlyGate,
            "an associated drawbridge was not treated as an always-passable gatehouse link");

        var ownAndAlliedGates = new[]
        {
            new PclGateConnection(10, 20, ownerId: 5, buildingId: 40, globalId: 4000),
            new PclGateConnection(20, 30, ownerId: 6, buildingId: 41, globalId: 4100)
        };
        GateBlockageEvaluation multipleFriendlyGates = TemporaryGateBlockagePolicy.Evaluate(
            new[] { 10 }, new[] { 30 }, ownAndAlliedGates, nativeUnreachable);
        Check(multipleFriendlyGates.Kind == GateBlockageEvaluationKind.ReachableViaFriendlyGate &&
              multipleFriendlyGates.UsedGateIndices.SequenceEqual(new[] { 0, 1 }),
            "a route through consecutive own and allied gates was not traversed");

        GateBlockageEvaluation nativeOnly = TemporaryGateBlockagePolicy.Evaluate(
            new[] { 10 }, new[] { 20 }, Array.Empty<PclGateConnection>(), nativeReachable);
        Check(nativeOnly.Kind == GateBlockageEvaluationKind.ReachableByNativeCurrentStateOnly &&
              nativeOnly.IsReachableUnderImprovedCheck,
            "positive native current-state reachability was not accepted as additional evidence");

        GateBlockageEvaluation sealedWall = TemporaryGateBlockagePolicy.Evaluate(
            new[] { 10 }, new[] { 20 }, Array.Empty<PclGateConnection>(), nativeUnreachable);
        Check(sealedWall.Kind == GateBlockageEvaluationKind.UnreachableEvenWithFriendlyGates &&
              !sealedWall.IsReachableUnderImprovedCheck,
            "a sealed wall without a friendly gate was classified as reachable");

        // Enemy gates are omitted by the runtime collector and therefore add no virtual link.
        Check(TemporaryGateBlockagePolicy.Evaluate(
                new[] { 10 }, new[] { 20 }, Array.Empty<PclGateConnection>(), nativeUnreachable).Kind ==
              GateBlockageEvaluationKind.UnreachableEvenWithFriendlyGates,
            "an omitted enemy gate affected classification");

        GateBlockageEvaluation invalidGates = TemporaryGateBlockagePolicy.Evaluate(
            new[] { 10 }, new[] { 20 },
            new[] { new PclGateConnection(0, 20), new PclGateConnection(30, 30) },
            nativeUnreachable);
        Check(invalidGates.Kind == GateBlockageEvaluationKind.UnreachableEvenWithFriendlyGates,
            "an invalid or no-op gate affected virtual-link classification");

        Check(TemporaryGateBlockagePolicy.ShouldSuppressDemolition(
                TemporaryGateBlockagePolicy.ImprovedReachabilityMode,
                isLivingAiBuilding: true,
                classificationAvailable: true,
                isReachableUnderImprovedCheck: closedOwnGate.IsReachableUnderImprovedCheck),
            "negative native reachability vetoed a graph-confirmed friendly-gate route");

    }

    private static void TestLordHealthMultiplierPolicy()
    {
        Check(LordHealthMultiplierPolicy.NormalizePercent(-1) == 10,
            "Lord health percentage did not clamp to 10%");
        Check(LordHealthMultiplierPolicy.NormalizePercent(100) == 100,
            "Lord health percentage changed the 100% default");
        Check(LordHealthMultiplierPolicy.NormalizePercent(900) == 500,
            "Lord health percentage did not clamp to 500%");

        uint humanVanilla = LordHealthMultiplierPolicy.CalculateVanillaMaximum(2000, 100);
        uint weakAI = LordHealthMultiplierPolicy.CalculateVanillaMaximum(2000, 50);
        uint strongAI = LordHealthMultiplierPolicy.CalculateVanillaMaximum(2000, 180);
        Check(humanVanilla == 2000 && weakAI == 1000 && strongAI == 3600,
            "Vanilla AI Lord health differences were not preserved");
        Check(LordHealthMultiplierPolicy.CalculateVanillaMaximum(2000, 180, 125) == 4500,
            "Vanilla enemy-health option was not retained in the AI Lord baseline");
        Check(LordHealthMultiplierPolicy.CalculateMaximum(weakAI, 200) == 2000 &&
              LordHealthMultiplierPolicy.CalculateMaximum(strongAI, 200) == 7200,
            "AI Lord multiplier flattened individual Vanilla health values");
        Check(LordHealthMultiplierPolicy.CalculateMaximum(humanVanilla, 10) == 200 &&
              LordHealthMultiplierPolicy.CalculateMaximum(humanVanilla, 500) == 10000,
            "Lord health multiplier produced an incorrect boundary value");

        uint wounded = LordHealthMultiplierPolicy.CalculateCurrent(750, 1500, 3000);
        Check(wounded == 1500 && LordHealthMultiplierPolicy.CalculateHealthPercent(wounded, 3000) == 50,
            "Lord health scaling did not preserve the wounded health ratio");
        uint repeated = LordHealthMultiplierPolicy.CalculateMaximum(humanVanilla, 200);
        Check(repeated == LordHealthMultiplierPolicy.CalculateMaximum(humanVanilla, 200),
            "Lord health target calculation was not idempotent");
        Check(LordHealthMultiplierPolicy.CalculateMaximum(uint.MaxValue, 500) == uint.MaxValue,
            "Lord health overflow was not clamped");
        Check(LordHealthMultiplierPolicy.CalculateCurrent(0, 1000, 2000) == 1,
            "an active Lord was allowed to reach zero health during scaling");
    }

    private static void TestQuarryPileTargetSelectionPolicy()
    {
        var candidates = new List<QuarryPileTargetCandidate>
        {
            new QuarryPileTargetCandidate(20, 20, 1, 0, true),
            new QuarryPileTargetCandidate(12, 10, 1, 4, false),
            new QuarryPileTargetCandidate(10, 10, 1, 5, false)
        };
        Check(QuarryPileTargetSelectionPolicy.TrySelectNearestAtPlacementTry(candidates, 1, 20, 20, out QuarryPileTargetCandidate nearest) &&
              nearest.X == 10 && nearest.Y == 10 && !nearest.IsCurrentPosition,
            "AI quarry-pile selection did not choose the valid candidate nearest to the Keep center");

        // A blocked closer position is absent because the runtime supplies only validated candidates.
        candidates = new List<QuarryPileTargetCandidate>
        {
            new QuarryPileTargetCandidate(20, 20, 1, 0, true),
            new QuarryPileTargetCandidate(12, 10, 1, 4, false)
        };
        Check(QuarryPileTargetSelectionPolicy.TrySelectNearestAtPlacementTry(candidates, 1, 20, 20, out nearest) &&
              nearest.X == 12 && nearest.Y == 10,
            "AI quarry-pile selection did not ignore a blocked nearer candidate");

        candidates = new List<QuarryPileTargetCandidate>
        {
            new QuarryPileTargetCandidate(9, 10, 2, 0, false),
            new QuarryPileTargetCandidate(11, 10, 1, 23, false),
            new QuarryPileTargetCandidate(10, 9, 1, 2, false)
        };
        Check(QuarryPileTargetSelectionPolicy.TrySelectNearestAtPlacementTry(candidates, 1, 20, 20, out nearest) &&
              nearest.X == 10 && nearest.Y == 9,
            "AI quarry-pile equal-distance tie did not use Vanilla try and candidate order");

        candidates = new List<QuarryPileTargetCandidate>
        {
            new QuarryPileTargetCandidate(10, 10, 1, 3, true),
            new QuarryPileTargetCandidate(15, 15, 1, 4, false)
        };
        Check(QuarryPileTargetSelectionPolicy.TrySelectNearestAtPlacementTry(candidates, 1, 20, 20, out nearest) &&
              nearest.IsCurrentPosition,
            "AI quarry-pile selection replaced an already optimal current position");

        candidates = new List<QuarryPileTargetCandidate>
        {
            new QuarryPileTargetCandidate(12, 10, 1, 4, false),
            new QuarryPileTargetCandidate(10, 10, 9, 5, false)
        };
        Check(QuarryPileTargetSelectionPolicy.TrySelectNearestAtPlacementTry(candidates, 1, 20, 20, out nearest) &&
              nearest.X == 12 && nearest.Y == 10 && nearest.PlacementTry == 1,
            "AI quarry-pile selection allowed a Keep-nearer outer Vanilla try to override try 1");

        candidates = new List<QuarryPileTargetCandidate>
        {
            new QuarryPileTargetCandidate(10, 10, 9, 5, true)
        };
        Check(!QuarryPileTargetSelectionPolicy.TrySelectNearestAtPlacementTry(candidates, 1, 20, 20, out _),
            "AI quarry-pile selection treated an outer current position as an allowed try-1 position");
        Check(!QuarryPileTargetSelectionPolicy.TrySelectNearestAtPlacementTry(
                Array.Empty<QuarryPileTargetCandidate>(),
                1,
                20,
                20,
                out _),
            "AI quarry-pile selection accepted an empty valid-candidate set");
    }

    private static void TestQuarryPileVanillaGroupPolicy()
    {
        QuarryPileVanillaGroupResolution valid = QuarryPileVanillaGroupPolicy.Resolve(700, 700);
        Check(valid.Status == QuarryPileVanillaGroupStatus.Valid && valid.CanUse &&
              !valid.RepairsPileGroup && valid.GroupId == 700,
            "matching Vanilla quarry-pile groups were not accepted");

        QuarryPileVanillaGroupResolution missingPile = QuarryPileVanillaGroupPolicy.Resolve(701, 0);
        Check(missingPile.Status == QuarryPileVanillaGroupStatus.RepairMissingPileGroup &&
              missingPile.CanUse && missingPile.RepairsPileGroup && missingPile.GroupId == 701,
            "a missing legacy pile group was not resolved from its quarry");

        QuarryPileVanillaGroupResolution missingQuarry = QuarryPileVanillaGroupPolicy.Resolve(0, 701);
        QuarryPileVanillaGroupResolution conflict = QuarryPileVanillaGroupPolicy.Resolve(701, 702);
        Check(!missingQuarry.CanUse && missingQuarry.Status == QuarryPileVanillaGroupStatus.MissingQuarryGroup &&
              !conflict.CanUse && conflict.Status == QuarryPileVanillaGroupStatus.ConflictingGroups,
            "missing or conflicting Vanilla structure groups were not rejected fail-closed");

        var repairs = new List<QuarryPileVanillaGroupRepair>();
        var ambiguities = new List<int>();
        QuarryPileVanillaGroupRepairSummary summary = QuarryPileVanillaGroupPolicy.PlanRepairs(
            new[]
            {
                new QuarryPileVanillaGroupCandidate(10, 20, 500, 500, 0, true),
                new QuarryPileVanillaGroupCandidate(11, 21, 501, 0, 11, true),
                new QuarryPileVanillaGroupCandidate(12, 22, 502, 502, 12, true),
                new QuarryPileVanillaGroupCandidate(13, 23, 0, 0, 0, true),
                new QuarryPileVanillaGroupCandidate(14, 0, 504, 0, 0, false)
            },
            repairs,
            ambiguities);
        Check(summary.ValidPairs == 4 && summary.AlreadyValid == 1 &&
              summary.PlannedRepairs == 2 && summary.InvalidCandidates == 1 &&
              summary.AmbiguousPiles == 0 && summary.RejectedGroups == 1,
            "Vanilla quarry-pile group repair summary was incorrect");
        Check(repairs.Count == 2 &&
              repairs[0].QuarryId == 11 && repairs[0].PileId == 21 && repairs[0].GroupId == 501 &&
              repairs[0].AssignPileGroup && repairs[0].ClearLegacyReverseLink &&
              repairs[1].QuarryId == 12 && repairs[1].PileId == 22 && !repairs[1].AssignPileGroup &&
              repairs[1].ClearLegacyReverseLink,
            "missing Vanilla groups and legacy reverse links were not repaired deterministically");
        Check(QuarryPileVanillaGroupPolicy.IsLegacyReverseLink(11, 11) &&
              !QuarryPileVanillaGroupPolicy.IsLegacyReverseLink(11, 12) &&
              !QuarryPileVanillaGroupPolicy.IsLegacyReverseLink(0, 0),
            "legacy 1.0.84 reverse-link detection was not sufficiently narrow");

        repairs.Clear();
        ambiguities.Clear();
        summary = QuarryPileVanillaGroupPolicy.PlanRepairs(
            new[]
            {
                new QuarryPileVanillaGroupCandidate(30, 40, 600, 0, 30, true),
                new QuarryPileVanillaGroupCandidate(31, 40, 601, 0, 31, true),
                new QuarryPileVanillaGroupCandidate(32, 41, 602, 0, 32, false)
            },
            repairs,
            ambiguities);
        Check(repairs.Count == 0 && ambiguities.Count == 1 && ambiguities[0] == 40 &&
              summary.AmbiguousPiles == 1 && summary.InvalidCandidates == 1,
            "ambiguous or invalid Vanilla quarry-pile groups were not rejected fail-closed");
    }

    private static void TestGameSpeedRepeatScheduler()
    {
        const long frequency = 1000;
        var repeat = new GameSpeedRepeatScheduler();
        Check(!repeat.Update(true, true, false, false, 1000, frequency),
            "initial game-speed press incorrectly emitted a repeat");
        Check(!repeat.Update(true, false, false, false, 1249, frequency),
            "game-speed repeat fired before 250 ms");
        Check(repeat.Update(true, false, false, false, 1250, frequency),
            "game-speed repeat did not fire at 250 ms");
        Check(repeat.Update(true, false, false, false, 2000, frequency),
            "delayed game-speed repeat did not fire once");
        Check(!repeat.Update(true, false, false, false, 2001, frequency) &&
              !repeat.Update(true, false, false, false, 2249, frequency) &&
              repeat.Update(true, false, false, false, 2250, frequency),
            "game-speed repeat caught up in a burst instead of rescheduling from execution");

        repeat.Reset();
        repeat.Update(true, true, false, false, 3000, frequency);
        Check(!repeat.Update(true, false, true, false, 3250, frequency) &&
              !repeat.Update(true, false, false, false, 4000, frequency),
            "simultaneous opposite keys did not cancel until release");
        repeat.Update(false, false, false, false, 4001, frequency);
        Check(!repeat.Update(true, true, false, false, 5000, frequency) &&
              repeat.Update(true, false, false, false, 5250, frequency),
            "release did not re-arm game-speed repeat");

        repeat.Reset();
        Check(!repeat.Update(true, true, false, true, 6000, frequency) &&
              !repeat.Update(true, false, false, false, 7000, frequency),
            "boundary saturation resumed before key release");
        repeat.Reset();
        Check(!repeat.Update(true, true, false, false, 8000, frequency) &&
              !repeat.Update(true, false, false, true, 8250, frequency) &&
              !repeat.Update(true, false, false, false, 9000, frequency),
            "repeat reaching a boundary resumed before key release");
        repeat.Reset();
        Check(!repeat.Update(true, true, false, false, long.MaxValue - 10, frequency),
            "overflow-safe repeat arming emitted an action");

        Check(MultiplayerGameSpeedPolicy.TryResolve(
                40,
                MultiplayerGameSpeedPolicy.IncreaseAction,
                0,
                out int normalRepeatSpeed) && normalRepeatSpeed == 45 &&
              MultiplayerGameSpeedPolicy.TryResolve(
                normalRepeatSpeed,
                MultiplayerGameSpeedPolicy.FastIncreaseAction,
                0,
                out int shiftedRepeatSpeed) && shiftedRepeatSpeed == 70 &&
              MultiplayerGameSpeedPolicy.TryResolve(
                shiftedRepeatSpeed,
                MultiplayerGameSpeedPolicy.IncreaseAction,
                0,
                out int unshiftedRepeatSpeed) && unshiftedRepeatSpeed == 75,
            "switching Shift during held repeats did not select 5/25/5 steps");
    }

    private static void TestSiegeAmmoRestockPolicyAndPacket()
    {
        AssertRestock(
            new[] { new SiegeAmmoRestockTarget(20, 20), new SiegeAmmoRestockTarget(10, 30) },
            10, 20, SiegeAmmoRestockModifier.Normal, 100, 10, 20,
            new Dictionary<int, ushort> { [10] = 35, [20] = 35 },
            "20+30 normal pool");
        AssertRestock(
            new[] { new SiegeAmmoRestockTarget(1, 0), new SiegeAmmoRestockTarget(2, 100) },
            10, 20, SiegeAmmoRestockModifier.Normal, 100, 10, 20,
            new Dictionary<int, ushort> { [1] = 20, [2] = 100 },
            "strongly unequal stocks");
        AssertRestock(
            new[] { new SiegeAmmoRestockTarget(3, 5), new SiegeAmmoRestockTarget(1, 5), new SiegeAmmoRestockTarget(2, 5) },
            10, 5, SiegeAmmoRestockModifier.Normal, 100, 10, 5,
            new Dictionary<int, ushort> { [1] = 7, [2] = 7, [3] = 6 },
            "stable-ID remainder");
        AssertRestock(
            new[] { new SiegeAmmoRestockTarget(1, 20) },
            10, 20, SiegeAmmoRestockModifier.Shift, 100, 50, 100,
            new Dictionary<int, ushort> { [1] = 120 },
            "Shift modifier");
        AssertRestock(
            new[] { new SiegeAmmoRestockTarget(1, 20) },
            10, 20, SiegeAmmoRestockModifier.Control, 100, 2, 4,
            new Dictionary<int, ushort> { [1] = 24 },
            "Ctrl modifier");
        AssertRestock(
            new[] { new SiegeAmmoRestockTarget(1, 20) },
            10, 20, SiegeAmmoRestockModifier.ShiftAndControl, 100, 10, 20,
            new Dictionary<int, ushort> { [1] = 40 },
            "combined modifier");
        AssertRestock(
            new[] { new SiegeAmmoRestockTarget(1, 0), new SiegeAmmoRestockTarget(2, 0) },
            10, 20, SiegeAmmoRestockModifier.Normal, 3, 3, 6,
            new Dictionary<int, ushort> { [1] = 3, [2] = 3 },
            "scarce stone");
        AssertRestock(
            new[] { new SiegeAmmoRestockTarget(1, 0) },
            7, 13, SiegeAmmoRestockModifier.Control, 100, 2, 2,
            new Dictionary<int, ushort> { [1] = 2 },
            "Tweaker ratio and rounded Ctrl pool");
        AssertRestock(
            new[] { new SiegeAmmoRestockTarget(2, ushort.MaxValue), new SiegeAmmoRestockTarget(1, ushort.MaxValue - 1) },
            10, 20, SiegeAmmoRestockModifier.Normal, 100, 1, 1,
            new Dictionary<int, ushort> { [1] = ushort.MaxValue, [2] = ushort.MaxValue },
            "16-bit saturation");

        Check(!SiegeAmmoRestockPolicy.TryCreatePlan(10, 20, SiegeAmmoRestockModifier.Normal, 0,
                new[] { new SiegeAmmoRestockTarget(1, 0) }, out _),
            "zero stone created a plan");
        Check(!SiegeAmmoRestockPolicy.TryCreatePlan(0, 20, SiegeAmmoRestockModifier.Normal, 10,
                new[] { new SiegeAmmoRestockTarget(1, 0) }, out _),
            "zero-cost Tweaker package created free ammunition");
        Check(!SiegeAmmoRestockPolicy.TryCreatePlan(10, 20, SiegeAmmoRestockModifier.Normal, 10,
                new[] { new SiegeAmmoRestockTarget(1, 0), new SiegeAmmoRestockTarget(1, 0) }, out _),
            "duplicate global IDs were accepted");
        Check(!SiegeAmmoRestockPolicy.TryCreatePlan(int.MaxValue, int.MaxValue, SiegeAmmoRestockModifier.Shift, int.MaxValue,
                new[] { new SiegeAmmoRestockTarget(1, 0) }, out SiegeAmmoRestockPlan overflowPlan) ||
              overflowPlan.AmmunitionAdded <= ushort.MaxValue,
            "overflow bypassed target capacity");

        var packet = new SiegeAmmoRestockPacket
        {
            ProtocolVersion = 1,
            PlayerId = 3,
            OperationId = 77,
            Modifier = (int)SiegeAmmoRestockModifier.Control,
            BaseStoneCost = 7,
            BaseAmmunitionAmount = 13,
            GlobalUnitIds = new[] { 11, 22, 33 }
        };
        SiegeAmmoRestockPacket decoded = MessagePackSerializer.Deserialize<SiegeAmmoRestockPacket>(
            MessagePackSerializer.Serialize(packet));
        Check(decoded.ProtocolVersion == packet.ProtocolVersion && decoded.PlayerId == packet.PlayerId &&
              decoded.OperationId == packet.OperationId && decoded.Modifier == packet.Modifier &&
              decoded.BaseStoneCost == packet.BaseStoneCost &&
              decoded.BaseAmmunitionAmount == packet.BaseAmmunitionAmount &&
              decoded.GlobalUnitIds.SequenceEqual(packet.GlobalUnitIds),
            "siege-ammunition packet formatter lost fields");

        packet.GlobalUnitIds = Enumerable.Range(1, SiegeAmmoRestockPolicy.MaximumTargetCount + 1).ToArray();
        bool oversizedRejected = false;
        try
        {
            MessagePackSerializer.Deserialize<SiegeAmmoRestockPacket>(MessagePackSerializer.Serialize(packet));
        }
        catch (MessagePackSerializationException)
        {
            oversizedRejected = true;
        }
        Check(oversizedRejected, "oversized siege-ammunition unit list was accepted");
        Check(SiegeAmmoRestockPolicy.ReplaceFirstTwoNumbers(
                "Reload (20 rocks for 10 stone)", 100, 50) ==
              "Reload (100 rocks for 50 stone)",
            "siege-ammunition tooltip numbers were not replaced");
        Check(SiegeAmmoRestockPolicy.ReplaceFirstTwoNumbers(
                "Nachladen (20 Felsen für 10 Steine)", 4, 2) ==
              "Nachladen (4 Felsen für 2 Steine)",
            "localized siege-ammunition tooltip numbers were not replaced");
        Check(SiegeAmmoRestockPolicy.ReplaceFirstTwoNumbers("Reload", 100, 50) == "Reload",
            "numberless siege-ammunition tooltip was corrupted");
    }

    private static void AssertRestock(
        SiegeAmmoRestockTarget[] targets,
        int baseCost,
        int baseAmount,
        SiegeAmmoRestockModifier modifier,
        int availableStone,
        int expectedCost,
        int expectedAdded,
        Dictionary<int, ushort> expected,
        string label)
    {
        Check(SiegeAmmoRestockPolicy.TryCreatePlan(
                baseCost, baseAmount, modifier, availableStone, targets, out SiegeAmmoRestockPlan plan),
            $"{label}: no plan");
        Check(plan.StoneCost == expectedCost && plan.AmmunitionAdded == expectedAdded,
            $"{label}: totals differ ({plan.StoneCost}/{plan.AmmunitionAdded})");
        Check(plan.Targets.Length == expected.Count &&
              plan.Targets.All(target => expected.TryGetValue(target.GlobalUnitId, out ushort ammunition) &&
                                         ammunition == target.Ammunition),
            $"{label}: distribution differs");
        long before = targets.Sum(target => (long)target.Ammunition);
        long after = plan.Targets.Sum(target => (long)target.Ammunition);
        Check(after - before == plan.AmmunitionAdded, $"{label}: ammunition conservation failed");
    }

    private static void TestMultiplayerGameSpeedPolicyAndPacket()
    {
        Check(MultiplayerGameSpeedPolicy.ProtocolVersion == 2,
            "multiplayer time-control protocol is not version 2");

        byte[] legacyDisabled = MessagePackSerializer.Serialize(false);
        byte[] legacyEveryone = MessagePackSerializer.Serialize(true);
        Check(MessagePackSerializer.Deserialize<MultiplayerTimeControlPermission>(legacyDisabled) ==
                MultiplayerTimeControlPermission.Disabled &&
              MessagePackSerializer.Deserialize<MultiplayerTimeControlPermission>(legacyEveryone) ==
                MultiplayerTimeControlPermission.Everyone,
            "legacy Boolean multiplayer time-control values were not migrated");
        foreach (MultiplayerTimeControlPermission permission in new[]
        {
            MultiplayerTimeControlPermission.Disabled,
            MultiplayerTimeControlPermission.OnlyHost,
            MultiplayerTimeControlPermission.Everyone
        })
        {
            byte[] permissionBytes = MessagePackSerializer.Serialize(permission);
            Check(MessagePackSerializer.Deserialize<MultiplayerTimeControlPermission>(permissionBytes) == permission,
                $"multiplayer time-control permission [{permission}] did not round-trip");
        }

        bool invalidPermissionTypeRejected = false;
        try
        {
            MessagePackSerializer.Deserialize<MultiplayerTimeControlPermission>(
                MessagePackSerializer.Serialize("invalid"));
        }
        catch (MessagePackSerializationException)
        {
            invalidPermissionTypeRejected = true;
        }
        Check(invalidPermissionTypeRejected,
            "invalid multiplayer time-control MessagePack type was accepted");

        bool invalidPermissionValueRejected = false;
        try
        {
            MessagePackSerializer.Deserialize<MultiplayerTimeControlPermission>(
                MessagePackSerializer.Serialize(3));
        }
        catch (MessagePackSerializationException)
        {
            invalidPermissionValueRejected = true;
        }
        Check(invalidPermissionValueRejected,
            "invalid multiplayer time-control enum value was accepted");

        bool invalidPermissionSerializationRejected = false;
        try
        {
            MessagePackSerializer.Serialize((MultiplayerTimeControlPermission)3);
        }
        catch (MessagePackSerializationException)
        {
            invalidPermissionSerializationRejected = true;
        }
        Check(invalidPermissionSerializationRejected,
            "invalid multiplayer time-control enum value was serialized");

        Check(!MultiplayerTimeControlPolicy.CanRequest(MultiplayerTimeControlPermission.Disabled, false) &&
              !MultiplayerTimeControlPolicy.CanRequest(MultiplayerTimeControlPermission.Disabled, true) &&
              !MultiplayerTimeControlPolicy.CanRequest(MultiplayerTimeControlPermission.OnlyHost, false) &&
              MultiplayerTimeControlPolicy.CanRequest(MultiplayerTimeControlPermission.OnlyHost, true) &&
              MultiplayerTimeControlPolicy.CanRequest(MultiplayerTimeControlPermission.Everyone, false) &&
              MultiplayerTimeControlPolicy.CanRequest(MultiplayerTimeControlPermission.Everyone, true),
            "multiplayer time-control host/client permission matrix is incorrect");

        Check(MultiplayerGameSpeedPolicy.TryResolvePausePacket(
                MultiplayerGameSpeedPolicy.ProtocolVersion,
                MultiplayerGameSpeedPolicy.PauseAction,
                0,
                0,
                out bool running) && !running &&
              MultiplayerGameSpeedPolicy.TryResolvePausePacket(
                MultiplayerGameSpeedPolicy.ProtocolVersion,
                MultiplayerGameSpeedPolicy.PauseAction,
                0,
                1,
                out bool paused) && paused,
            "valid multiplayer pause targets were rejected");
        Check(!MultiplayerGameSpeedPolicy.TryResolvePausePacket(
                MultiplayerGameSpeedPolicy.ProtocolVersion,
                MultiplayerGameSpeedPolicy.PauseAction,
                0,
                2,
                out _) &&
              !MultiplayerGameSpeedPolicy.TryResolvePausePacket(
                MultiplayerGameSpeedPolicy.ProtocolVersion,
                MultiplayerGameSpeedPolicy.PauseAction,
                10,
                1,
                out _),
            "invalid multiplayer pause payload was accepted");
        Check(!MultiplayerGameSpeedPolicy.TryResolvePacket(
                40,
                MultiplayerGameSpeedPolicy.ProtocolVersion,
                MultiplayerGameSpeedPolicy.IncreaseAction,
                0,
                1,
                MultiplayerGameSpeedPolicy.MaximumSpeed,
                out _),
            "game-speed action accepted a pause target");

        Check(MultiplayerGameSpeedPolicy.TryResolveDelivery(
                MultiplayerGameSpeedPolicy.PauseAction,
                0,
                1,
                out MultiplayerTimeControlDelivery pauseDelivery) &&
              pauseDelivery == MultiplayerTimeControlDelivery.Chore,
            "multiplayer pause did not select Chore delivery");
        Check(MultiplayerGameSpeedPolicy.TryResolveDelivery(
                MultiplayerGameSpeedPolicy.PauseAction,
                0,
                0,
                out MultiplayerTimeControlDelivery unpauseDelivery) &&
              unpauseDelivery == MultiplayerTimeControlDelivery.Direct,
            "multiplayer unpause did not select direct delivery");
        foreach (int speedAction in new[]
        {
            MultiplayerGameSpeedPolicy.IncreaseAction,
            MultiplayerGameSpeedPolicy.DecreaseAction,
            MultiplayerGameSpeedPolicy.SetAction,
            MultiplayerGameSpeedPolicy.FastIncreaseAction,
            MultiplayerGameSpeedPolicy.FastDecreaseAction
        })
        {
            int target = speedAction == MultiplayerGameSpeedPolicy.SetAction ? 70 : 0;
            Check(MultiplayerGameSpeedPolicy.TryResolveDelivery(
                    speedAction,
                    target,
                    0,
                    out MultiplayerTimeControlDelivery speedDelivery) &&
                  speedDelivery == MultiplayerTimeControlDelivery.Chore,
                $"multiplayer game-speed action [{speedAction}] did not retain Chore delivery");
        }
        Check(!MultiplayerGameSpeedPolicy.TryResolveDelivery(
                MultiplayerGameSpeedPolicy.PauseAction,
                10,
                0,
                out _) &&
              !MultiplayerGameSpeedPolicy.TryResolveDelivery(
                MultiplayerGameSpeedPolicy.PauseAction,
                0,
                2,
                out _) &&
              !MultiplayerGameSpeedPolicy.TryResolveDelivery(
                MultiplayerGameSpeedPolicy.IncreaseAction,
                0,
                1,
                out _) &&
              !MultiplayerGameSpeedPolicy.TryResolveDelivery(999, 0, 0, out _),
            "invalid or mixed multiplayer time-control payload selected a delivery");
        Check(MultiplayerGameSpeedPolicy.ShouldApplyPauseState(true, false) &&
              !MultiplayerGameSpeedPolicy.ShouldApplyPauseState(false, false) &&
              MultiplayerGameSpeedPolicy.ShouldApplyPauseState(false, true) &&
              !MultiplayerGameSpeedPolicy.ShouldApplyPauseState(true, true),
            "multiplayer pause idempotency policy is incorrect");

        Check(MultiplayerGameSpeedPolicy.TryResolvePacket(
                40,
                MultiplayerGameSpeedPolicy.ProtocolVersion,
                MultiplayerGameSpeedPolicy.IncreaseAction,
                0,
                out int increased) && increased == 45,
            "multiplayer game-speed increase did not advance one step");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                40,
                MultiplayerGameSpeedPolicy.DecreaseAction,
                0,
                out int decreased) && decreased == 35,
            "multiplayer game-speed decrease did not retreat one step");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                10,
                MultiplayerGameSpeedPolicy.FastIncreaseAction,
                0,
                out int fastFromMinimum) && fastFromMinimum == 35,
            "fast game-speed increase from minimum did not advance 25");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                15,
                MultiplayerGameSpeedPolicy.FastIncreaseAction,
                0,
                out int fastFromFifteen) && fastFromFifteen == 40,
            "fast game-speed increase from 15 did not advance 25");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                20,
                MultiplayerGameSpeedPolicy.FastDecreaseAction,
                0,
                out int fastBelowMinimum) && fastBelowMinimum == MultiplayerGameSpeedPolicy.MinimumSpeed,
            "fast game-speed decrease did not clamp to the lower bound");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                20,
                MultiplayerGameSpeedPolicy.FastIncreaseAction,
                0,
                out int fastFromTwenty) && fastFromTwenty == 45,
            "fast game-speed increase from 20 did not advance 25");
        Check(MultiplayerGameSpeedPolicy.TryResolvePacket(
                75,
                MultiplayerGameSpeedPolicy.ProtocolVersion,
                MultiplayerGameSpeedPolicy.FastIncreaseAction,
                0,
                out int fastAboveNinety) && fastAboveNinety == 100,
            "fast synchronized game-speed increase was incorrectly clamped at 90");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                75,
                MultiplayerGameSpeedPolicy.FastDecreaseAction,
                0,
                out int fastDecreaseFromSeventyFive) && fastDecreaseFromSeventyFive == 50,
            "fast game-speed decrease from 75 did not retreat 25");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                80,
                MultiplayerGameSpeedPolicy.FastDecreaseAction,
                0,
                out int fastFromEighty) && fastFromEighty == 55,
            "fast game-speed decrease from 80 did not retreat 25");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                85,
                MultiplayerGameSpeedPolicy.FastIncreaseAction,
                0,
                out int fastFromEightyFive) && fastFromEightyFive == 110,
            "fast game-speed increase from 85 did not advance beyond 90");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                90,
                MultiplayerGameSpeedPolicy.FastDecreaseAction,
                0,
                out int fastFromMaximum) && fastFromMaximum == 65,
            "fast game-speed decrease from maximum did not retreat 25");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                90,
                MultiplayerGameSpeedPolicy.FastIncreaseAction,
                0,
                out int fastAboveVanillaMaximum) && fastAboveVanillaMaximum == 115,
            "fast game-speed increase was incorrectly clamped at Vanilla's upper bound");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                4990,
                MultiplayerGameSpeedPolicy.FastIncreaseAction,
                0,
                out int fastAtConfiguredMaximum) &&
              fastAtConfiguredMaximum == MultiplayerGameSpeedPolicy.MaximumSpeed,
            "fast game-speed increase did not clamp to Script Extender's default maximum");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                75,
                MultiplayerGameSpeedPolicy.FastIncreaseAction,
                0,
                90,
                out int fastAtCustomMaximum) && fastAtCustomMaximum == 90,
            "fast game-speed increase ignored a custom Script Extender maximum");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                85,
                MultiplayerGameSpeedPolicy.FastIncreaseAction,
                0,
                92,
                out int fastAtNonStepMaximum) && fastAtNonStepMaximum == 92,
            "fast game-speed increase did not retain a non-step Script Extender maximum");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                10,
                MultiplayerGameSpeedPolicy.FastDecreaseAction,
                0,
                out int fastAtMinimum) && fastAtMinimum == MultiplayerGameSpeedPolicy.MinimumSpeed,
            "fast game-speed decrease exceeded the lower bound at 10");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                MultiplayerGameSpeedPolicy.MaximumSpeed,
                MultiplayerGameSpeedPolicy.IncreaseAction,
                0,
                out int maximum) && maximum == MultiplayerGameSpeedPolicy.MaximumSpeed,
            "multiplayer game-speed increase exceeded the upper bound");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                MultiplayerGameSpeedPolicy.MinimumSpeed,
                MultiplayerGameSpeedPolicy.DecreaseAction,
                0,
                out int minimum) && minimum == MultiplayerGameSpeedPolicy.MinimumSpeed,
            "multiplayer game-speed decrease exceeded the lower bound");
        Check(MultiplayerGameSpeedPolicy.TryResolve(
                40,
                MultiplayerGameSpeedPolicy.SetAction,
                65,
                out int absolute) && absolute == 65,
            "multiplayer game-speed absolute target was not retained");
        Check(!MultiplayerGameSpeedPolicy.TryResolve(40, 999, 0, out _),
            "unknown multiplayer game-speed action was accepted");
        Check(!MultiplayerGameSpeedPolicy.TryResolve(
                40,
                MultiplayerGameSpeedPolicy.IncreaseAction,
                MultiplayerGameSpeedPolicy.MinimumSpeed,
                out _),
            "multiplayer game-speed increase accepted a target reserved for Set");
        Check(!MultiplayerGameSpeedPolicy.TryResolve(
                40,
                MultiplayerGameSpeedPolicy.DecreaseAction,
                MultiplayerGameSpeedPolicy.MaximumSpeed,
                out _),
            "multiplayer game-speed decrease accepted a target reserved for Set");
        Check(!MultiplayerGameSpeedPolicy.TryResolve(
                40,
                MultiplayerGameSpeedPolicy.FastIncreaseAction,
                MultiplayerGameSpeedPolicy.MaximumSpeed,
                out _),
            "fast multiplayer game-speed increase accepted a target reserved for Set");
        Check(!MultiplayerGameSpeedPolicy.TryResolve(
                40,
                MultiplayerGameSpeedPolicy.FastDecreaseAction,
                MultiplayerGameSpeedPolicy.MinimumSpeed,
                out _),
            "fast multiplayer game-speed decrease accepted a target reserved for Set");
        Check(!MultiplayerGameSpeedPolicy.TryResolve(40, MultiplayerGameSpeedPolicy.SetAction, 64, out _),
            "non-step multiplayer game-speed target was accepted");
        Check(!MultiplayerGameSpeedPolicy.TryResolve(
                40,
                MultiplayerGameSpeedPolicy.SetAction,
                MultiplayerGameSpeedPolicy.MaximumSpeed + MultiplayerGameSpeedPolicy.SpeedStep,
                out _),
            "out-of-range multiplayer game-speed target was accepted");
        Check(!MultiplayerGameSpeedPolicy.TryResolvePacket(
                40,
                MultiplayerGameSpeedPolicy.ProtocolVersion + 1,
                MultiplayerGameSpeedPolicy.IncreaseAction,
                0,
                out _),
            "unknown multiplayer game-speed protocol was accepted");

        var packet = new MultiplayerGameSpeedChangePacket
        {
            ProtocolVersion = MultiplayerGameSpeedPolicy.ProtocolVersion,
            Action = MultiplayerGameSpeedPolicy.SetAction,
            TargetSpeed = 70,
            PauseState = 0
        };
        byte[] serialized = MessagePackSerializer.Serialize(packet);
        MultiplayerGameSpeedChangePacket roundTrip =
            MessagePackSerializer.Deserialize<MultiplayerGameSpeedChangePacket>(serialized);
        Check(roundTrip.ProtocolVersion == packet.ProtocolVersion &&
            roundTrip.Action == packet.Action &&
            roundTrip.TargetSpeed == packet.TargetSpeed &&
            roundTrip.PauseState == packet.PauseState,
            "multiplayer game-speed packet did not round-trip");

        byte[] forwardBuffer = MessagePackSerializer.Serialize(new FutureMultiplayerGameSpeedPacket
        {
            ProtocolVersion = MultiplayerGameSpeedPolicy.ProtocolVersion,
            Action = MultiplayerGameSpeedPolicy.SetAction,
            TargetSpeed = 75,
            PauseState = 0,
            FutureField = "future-field"
        });
        MultiplayerGameSpeedChangePacket forwardPacket =
            MessagePackSerializer.Deserialize<MultiplayerGameSpeedChangePacket>(forwardBuffer);
        Check(forwardPacket.ProtocolVersion == MultiplayerGameSpeedPolicy.ProtocolVersion &&
            forwardPacket.Action == MultiplayerGameSpeedPolicy.SetAction &&
            forwardPacket.TargetSpeed == 75 &&
            forwardPacket.PauseState == 0,
            "multiplayer game-speed formatter rejected an unknown trailing field");
    }

    private static void TestMarketTradeIntegration()
    {
        int beginPlayer = -1;
        int beginGood = -1;
        int endPlayer = -1;
        int endGood = -1;
        Action<int, int> begin = (playerId, good) =>
        {
            beginPlayer = playerId;
            beginGood = good;
        };
        Action<int, int> end = (playerId, good) =>
        {
            endPlayer = playerId;
            endGood = good;
        };

        MarketTradeIntegration.RegisterSingleBuyGuards(begin, end);
        Check(MarketTradeIntegration.HasSingleBuyGuards,
            "market-trade integration did not retain registered guards");
        MarketTradeIntegration.BeginSingleBuy(3, 7);
        MarketTradeIntegration.EndSingleBuy(3, 7);
        Check(beginPlayer == 3 && beginGood == 7 && endPlayer == 3 && endGood == 7,
            "market-trade integration did not forward the guarded purchase identity");

        MarketTradeIntegration.UnregisterSingleBuyGuards((_, __) => { });
        Check(MarketTradeIntegration.HasSingleBuyGuards,
            "a non-owner removed the market-trade integration callbacks");
        MarketTradeIntegration.UnregisterSingleBuyGuards(begin);
        Check(!MarketTradeIntegration.HasSingleBuyGuards,
            "the market-trade integration owner could not unregister its callbacks");
    }

    private static void TestAIMarketNativeResolution()
    {
        byte[] buy = ParseHex(AIMarketVanillaPricePolicy.BuyPriceFunctionPattern);
        byte[] sell = ParseHex(AIMarketVanillaPricePolicy.SellPriceFunctionPattern);
        byte[] referenceImage = CreateExecutableTestImage(0xD1000);
        CopyAt(referenceImage, AIMarketVanillaPricePolicy.BuyPriceFunctionRva, buy);
        CopyAt(referenceImage, AIMarketVanillaPricePolicy.SellPriceFunctionRva, sell);
        CopyAt(referenceImage, 0x2000, buy);

        NativeResolution reference = NativePatternResolver.ResolveUnique(
            referenceImage,
            AIMarketVanillaPricePolicy.BuyPriceFunctionPattern,
            AIMarketVanillaPricePolicy.BuyPriceFunctionRva,
            referenceHashMatches: true,
            "test buy helper");
        Check(
            reference.Rva == AIMarketVanillaPricePolicy.BuyPriceFunctionRva &&
            reference.Method == "reference-rva",
            "matching hash did not use the validated reference RVA exclusively");

        byte[] fallbackImage = CreateExecutableTestImage(0x10000);
        CopyAt(fallbackImage, 0x3000, buy);
        CopyAt(fallbackImage, 0x5000, sell);
        NativeResolution fallbackBuy = NativePatternResolver.ResolveUnique(
            fallbackImage,
            AIMarketVanillaPricePolicy.BuyPriceFunctionPattern,
            AIMarketVanillaPricePolicy.BuyPriceFunctionRva,
            referenceHashMatches: false,
            "test buy helper");
        NativeResolution fallbackSell = NativePatternResolver.ResolveUnique(
            fallbackImage,
            AIMarketVanillaPricePolicy.SellPriceFunctionPattern,
            AIMarketVanillaPricePolicy.SellPriceFunctionRva,
            referenceHashMatches: false,
            "test sell helper");
        Check(
            fallbackBuy.Rva == 0x3000 && fallbackSell.Rva == 0x5000 &&
            fallbackBuy.Method == "signature-fallback" && fallbackSell.Method == "signature-fallback",
            "unknown hash did not resolve both unique executable signatures");

        byte[] missingImage = CreateExecutableTestImage(0x10000);
        CopyAt(missingImage, 0x3000, buy);
        ExpectInvalidOperation(
            () => NativePatternResolver.ResolveUnique(
                missingImage,
                AIMarketVanillaPricePolicy.SellPriceFunctionPattern,
                AIMarketVanillaPricePolicy.SellPriceFunctionRva,
                referenceHashMatches: false,
                "missing sell helper"),
            "missing AI market signature was accepted");

        byte[] ambiguousImage = CreateExecutableTestImage(0x10000);
        CopyAt(ambiguousImage, 0x3000, buy);
        CopyAt(ambiguousImage, 0x5000, buy);
        ExpectInvalidOperation(
            () => NativePatternResolver.ResolveUnique(
                ambiguousImage,
                AIMarketVanillaPricePolicy.BuyPriceFunctionPattern,
                AIMarketVanillaPricePolicy.BuyPriceFunctionRva,
                referenceHashMatches: false,
                "ambiguous buy helper"),
            "ambiguous AI market signature was accepted");
    }

    private static void TestAiRecruitmentHorseDemandNativeResolution()
    {
        Check(
            AiRecruitmentHorseDemandNativeDefinition.IsKnightHorseOnlyFailure(28, 2, 0),
            "AI recruitment diagnostics did not recognize the audited horse-only result");
        Check(
            !AiRecruitmentHorseDemandNativeDefinition.IsKnightHorseOnlyFailure(28, 2, 23),
            "AI recruitment diagnostics misclassified a sword shortage as a horse-only result");
        Check(
            AiRecruitmentHorseDemandNativeDefinition.IsKnightEquipmentFailure(28, 2, 23) &&
            AiRecruitmentHorseDemandNativeDefinition.IsKnightEquipmentFailure(28, 2, 25),
            "AI recruitment diagnostics did not recognize the audited knight equipment results");
        Check(
            !AiRecruitmentHorseDemandNativeDefinition.IsKnightEquipmentFailure(27, 2, 23),
            "AI recruitment diagnostics accepted a non-knight equipment result");

        string pattern = AiRecruitmentHorseDemandNativeDefinition.RecruitEuropeanUnitPattern;
        int referenceRva = AiRecruitmentHorseDemandNativeDefinition.RecruitEuropeanUnitRva;
        byte[] bytes = MaterializePattern(pattern, 0x5A);
        byte[] referenceImage = CreateExecutableTestImage(referenceRva + 0x1000);
        CopyAt(referenceImage, referenceRva, bytes);
        CopyAt(referenceImage, 0x3000, bytes);

        NativeResolution reference = NativePatternResolver.ResolveUnique(
            referenceImage,
            pattern,
            referenceRva,
            referenceHashMatches: true,
            "test European recruitment");
        Check(reference.Rva == referenceRva && reference.Method == "reference-rva",
            "AI recruitment matching hash did not use only the validated reference RVA");

        byte[] fallbackImage = CreateExecutableTestImage(0x10000);
        CopyAt(fallbackImage, 0x3000, bytes);
        NativeResolution fallback = NativePatternResolver.ResolveUnique(
            fallbackImage,
            pattern,
            referenceRva,
            referenceHashMatches: false,
            "test European recruitment");
        Check(fallback.Rva == 0x3000 && fallback.Method == "signature-fallback",
            "AI recruitment unknown hash did not use its unique executable signature");

        ExpectInvalidOperation(
            () => NativePatternResolver.ResolveUnique(
                CreateExecutableTestImage(0x10000),
                pattern,
                referenceRva,
                referenceHashMatches: false,
                "missing European recruitment"),
            "missing AI recruitment signature was accepted");

        byte[] ambiguousImage = CreateExecutableTestImage(0x10000);
        CopyAt(ambiguousImage, 0x3000, bytes);
        CopyAt(ambiguousImage, 0x5000, bytes);
        ExpectInvalidOperation(
            () => NativePatternResolver.ResolveUnique(
                ambiguousImage,
                pattern,
                referenceRva,
                referenceHashMatches: false,
                "ambiguous European recruitment"),
            "ambiguous AI recruitment signature was accepted");
    }

    private static void TestAiStoneReserveNativeResolution()
    {
        string pattern = AiStoneReserveNativeDefinition.SellerReservePattern;
        int referenceRva = AiStoneReserveNativeDefinition.SellerReservePatternRva;
        byte[] bytes = MaterializePattern(pattern, 0x5A);
        Check(
            AiStoneReserveNativeDefinition.SellerReserveHookRva == 0x3F156 &&
            AiStoneReserveNativeDefinition.SellerReserveOverwriteLength == 20 &&
            bytes[AiStoneReserveNativeDefinition.SellerReserveHookOffset] == 0x42 &&
            bytes[AiStoneReserveNativeDefinition.SellerReserveHookOffset + 1] == 0x8D &&
            bytes[AiStoneReserveNativeDefinition.SellerReserveHookOffset + 2] == 0x14 &&
            bytes[AiStoneReserveNativeDefinition.SellerReserveHookOffset + 3] == 0x18,
            "AI stone-reserve hook offset does not point at the verified common threshold block");

        byte[] referenceImage = CreateExecutableTestImage(referenceRva + 0x1000);
        CopyAt(referenceImage, referenceRva, bytes);
        CopyAt(referenceImage, 0x3000, bytes);
        NativeResolution reference = NativePatternResolver.ResolveUnique(
            referenceImage,
            pattern,
            referenceRva,
            referenceHashMatches: true,
            "test AI seller stone reserve");
        Check(reference.Rva == referenceRva && reference.Method == "reference-rva",
            "AI stone-reserve matching hash did not use only the validated reference RVA");

        byte[] fallbackImage = CreateExecutableTestImage(0x10000);
        CopyAt(fallbackImage, 0x3000, bytes);
        NativeResolution fallback = NativePatternResolver.ResolveUnique(
            fallbackImage,
            pattern,
            referenceRva,
            referenceHashMatches: false,
            "test AI seller stone reserve");
        Check(fallback.Rva == 0x3000 && fallback.Method == "signature-fallback",
            "AI stone-reserve unknown hash did not use its unique executable signature");

        ExpectInvalidOperation(
            () => NativePatternResolver.ResolveUnique(
                CreateExecutableTestImage(0x10000),
                pattern,
                referenceRva,
                referenceHashMatches: false,
                "missing AI seller stone reserve"),
            "missing AI stone-reserve signature was accepted");

        byte[] ambiguousImage = CreateExecutableTestImage(0x10000);
        CopyAt(ambiguousImage, 0x3000, bytes);
        CopyAt(ambiguousImage, 0x5000, bytes);
        ExpectInvalidOperation(
            () => NativePatternResolver.ResolveUnique(
                ambiguousImage,
                pattern,
                referenceRva,
                referenceHashMatches: false,
                "ambiguous AI seller stone reserve"),
            "ambiguous AI stone-reserve signature was accepted");

        TestAiStoneLayoutSignature(
            AiStoneReserveNativeDefinition.AivSlotLayoutPattern,
            AiStoneReserveNativeDefinition.AivSlotLayoutPatternRva,
            "AIV slot layout");
        TestAiStoneLayoutSignature(
            AiStoneReserveNativeDefinition.AivStepLayoutPattern,
            AiStoneReserveNativeDefinition.AivStepLayoutPatternRva,
            "AIV step layout");
        TestAiStoneLayoutSignature(
            AiStoneReserveNativeDefinition.AivHighestFramePattern,
            AiStoneReserveNativeDefinition.AivHighestFramePatternRva,
            "AIV highest-frame layout");
        TestAiStoneLayoutSignature(
            AiStoneReserveNativeDefinition.AivInitialFirstBuildStatePattern,
            AiStoneReserveNativeDefinition.AivInitialFirstBuildStatePatternRva,
            "AIV initial first-build state");
        TestAiStoneLayoutSignature(
            AiStoneReserveNativeDefinition.AivResourceShortageReturnPattern,
            AiStoneReserveNativeDefinition.AivResourceShortageReturnPatternRva,
            "AIV resource-shortage state preservation");
        TestAiStoneLayoutSignature(
            AiStoneReserveNativeDefinition.AivFirstBuildSuccessPattern,
            AiStoneReserveNativeDefinition.AivFirstBuildSuccessPatternRva,
            "AIV first-build success state");
        TestAiStoneLayoutSignature(
            AiStoneReserveNativeDefinition.AivPlacementRetryPattern,
            AiStoneReserveNativeDefinition.AivPlacementRetryPatternRva,
            "AIV placement-retry state");
    }

    private static void TestAiStoneLayoutSignature(string pattern, int referenceRva, string name)
    {
        byte[] bytes = MaterializePattern(pattern, 0x5A);
        byte[] referenceImage = CreateExecutableTestImage(referenceRva + bytes.Length + 0x1000);
        CopyAt(referenceImage, referenceRva, bytes);
        NativeResolution reference = NativePatternResolver.ResolveUnique(
            referenceImage,
            pattern,
            referenceRva,
            referenceHashMatches: true,
            name);
        Check(reference.Rva == referenceRva && reference.Method == "reference-rva",
            $"AI stone-reserve {name} did not resolve at its reference RVA");

        byte[] fallbackImage = CreateExecutableTestImage(0x10000);
        CopyAt(fallbackImage, 0x3000, bytes);
        NativeResolution fallback = NativePatternResolver.ResolveUnique(
            fallbackImage,
            pattern,
            referenceRva,
            referenceHashMatches: false,
            name);
        Check(fallback.Rva == 0x3000 && fallback.Method == "signature-fallback",
            $"AI stone-reserve {name} did not resolve by unique fallback signature");

        ExpectInvalidOperation(
            () => NativePatternResolver.ResolveUnique(
                CreateExecutableTestImage(0x10000),
                pattern,
                referenceRva,
                referenceHashMatches: false,
                name),
            $"missing AI stone-reserve {name} signature was accepted");

        byte[] ambiguousImage = CreateExecutableTestImage(0x10000);
        CopyAt(ambiguousImage, 0x3000, bytes);
        CopyAt(ambiguousImage, 0x5000, bytes);
        ExpectInvalidOperation(
            () => NativePatternResolver.ResolveUnique(
                ambiguousImage,
                pattern,
                referenceRva,
                referenceHashMatches: false,
                name),
            $"ambiguous AI stone-reserve {name} signature was accepted");
    }

    private static void TestAiStoneReservePolicy()
    {
        Check(
            AiStoneReservePolicy.TryGetPlayerId(
                3UL * AiStoneReservePolicy.PlayerResourceStrideElements,
                out int playerId) && playerId == 3,
            "AI stone-reserve policy did not decode a valid seller player offset");
        Check(
            !AiStoneReservePolicy.TryGetPlayerId(
                3UL * AiStoneReservePolicy.PlayerResourceStrideElements + 1,
                out _) &&
            !AiStoneReservePolicy.TryGetPlayerId(0, out _) &&
            !AiStoneReservePolicy.TryGetPlayerId(
                9UL * AiStoneReservePolicy.PlayerResourceStrideElements,
                out _),
            "AI stone-reserve policy accepted an invalid seller player offset");

        byte[] table = new byte[AiStoneReservePolicy.AivSlotCount * AiStoneReservePolicy.AivSlotSize];
        WriteInt32(table, AiStoneReservePolicy.PlayerIdOffset, 3);
        Check(
            !AiStoneReservePolicy.TryFindPlayerSlot(table, 3, out _),
            "AI stone-reserve policy treated reserved AIV slot zero as a player slot");
        int expectedSlotOffset = 4 * AiStoneReservePolicy.AivSlotSize;
        WriteInt32(table, expectedSlotOffset + AiStoneReservePolicy.PlayerIdOffset, 3);
        Check(
            AiStoneReservePolicy.TryFindPlayerSlot(table, 3, out int slotOffset) &&
            slotOffset == expectedSlotOffset,
            "AI stone-reserve policy did not find the unique player AIV slot");
        Check(
            !AiStoneReservePolicy.TryFindPlayerSlot(table, 9, out _) &&
            !AiStoneReservePolicy.TryFindPlayerSlot(new byte[32], 3, out _),
            "AI stone-reserve policy accepted invalid player or table bounds");
        WriteInt32(
            table,
            5 * AiStoneReservePolicy.AivSlotSize + AiStoneReservePolicy.PlayerIdOffset,
            3);
        Check(
            !AiStoneReservePolicy.TryFindPlayerSlot(table, 3, out _),
            "AI stone-reserve policy accepted duplicate player AIV slots");

        byte[] slot = new byte[AiStoneReservePolicy.AivSlotSize];
        WriteInt32(slot, AiStoneReservePolicy.HighestFrameOffset, 4);
        WriteAivStep(slot, 0, 1, 100);
        WriteAivStep(slot, 1, 5, 101);
        WriteAivStep(slot, 2, 3, 102);
        WriteAivStep(slot, 3, 0, 103);
        WriteAivStep(slot, 4, 4, 104);
        var costs = new Dictionary<short, int?>
        {
            { 100, 20 },
            { 101, 40 },
            { 102, 99 },
            { 103, 99 },
            { 104, 99 }
        };
        Check(
            AiStoneReservePolicy.TryCalculateReserve(slot, type => costs[type], out int reserve) &&
            reserve == 20,
            "AI stone-reserve policy included a state other than Vanilla's initial first-build state");

        WriteAivStep(slot, 4, 1, 104);
        costs[104] = 70;
        Check(
            AiStoneReservePolicy.TryCalculateReserve(slot, type => costs[type], out reserve) &&
            reserve == 70,
            "AI stone-reserve policy did not include the highest-frame entry");
        WriteAivStep(slot, 4, 4, 104);

        costs[100] = 65;
        Check(
            AiStoneReservePolicy.TryCalculateReserve(slot, type => costs[type], out reserve) &&
            reserve == 65,
            "AI stone-reserve policy cached a stale building cost");
        costs[100] = null;
        Check(
            AiStoneReservePolicy.TryCalculateReserve(slot, type => costs[type], out reserve) &&
            reserve == 0,
            "AI stone-reserve policy did not ignore non-building AIV commands");

        costs[100] = 0;
        Check(
            AiStoneReservePolicy.TryCalculateReserve(slot, type => costs[type], out reserve) &&
            reserve == 0,
            "AI stone-reserve policy retained a reserve for a command without stone cost");

        costs[100] = 20;
        WriteAivStep(slot, 0, 1, 100);
        WriteAivStep(slot, 1, 1, 101);
        costs[101] = 40;
        Check(
            AiStoneReservePolicy.TryCalculateReserve(slot, type => costs[type], out reserve) &&
            reserve == 40,
            "AI stone-reserve policy did not select the maximum initial first-build cost");

        WriteAivStep(slot, 1, 5, 101);
        Check(
            AiStoneReservePolicy.TryCalculateReserve(slot, type => costs[type], out reserve) &&
            reserve == 20,
            "AI stone-reserve policy retained a reserve after a placement retry state");

        WriteAivStep(slot, 0, 3, 100);
        WriteAivStep(slot, 1, 3, 101);
        Check(
            AiStoneReservePolicy.TryCalculateReserve(slot, type => costs[type], out reserve) &&
            reserve == 0,
            "AI stone-reserve policy retained a reserve after all buildings completed");

        WriteAivStep(slot, 0, 2, 100);
        Check(
            !AiStoneReservePolicy.TryCalculateReserve(slot, type => 20, out _),
            "AI stone-reserve policy accepted an unknown AIV step status");
        WriteAivStep(slot, 0, 1, 100);
        Check(
            !AiStoneReservePolicy.TryCalculateReserve(slot, type => -1, out _),
            "AI stone-reserve policy accepted a negative building cost");

        Array.Clear(slot, 0, slot.Length);
        WriteInt32(slot, AiStoneReservePolicy.HighestFrameOffset, AiStoneReservePolicy.MaximumSteps - 1);
        WriteAivStep(slot, AiStoneReservePolicy.MaximumSteps - 1, 1, 105);
        Check(
            AiStoneReservePolicy.TryCalculateReserve(
                slot,
                type => type == 105 ? (int?)90 : null,
                out reserve) &&
            reserve == 90,
            "AI stone-reserve policy did not accept and scan the maximum valid frame");

        WriteInt32(slot, AiStoneReservePolicy.HighestFrameOffset, AiStoneReservePolicy.MaximumSteps);
        Check(
            !AiStoneReservePolicy.TryCalculateReserve(slot, type => 20, out _),
            "AI stone-reserve policy accepted an invalid highest frame");
        WriteInt32(slot, AiStoneReservePolicy.HighestFrameOffset, -1);
        Check(
            !AiStoneReservePolicy.TryCalculateReserve(slot, type => 20, out _),
            "AI stone-reserve policy accepted a negative highest frame");

        Check(
            AiStoneReservePolicy.TryValidateThreshold(200, 10, 40) &&
            !AiStoneReservePolicy.TryValidateThreshold(int.MaxValue, 0, 1) &&
            !AiStoneReservePolicy.TryValidateThreshold(200, 10, -1),
            "AI stone-reserve threshold overflow validation is incorrect");
    }

    private static byte[] CreateExecutableTestImage(int length)
    {
        byte[] image = new byte[length];
        image[0] = 0x4D;
        image[1] = 0x5A;
        WriteInt32(image, 0x3C, 0x80);
        image[0x80] = 0x50;
        image[0x81] = 0x45;
        image[0x86] = 1;
        image[0x94] = 0xF0;
        int section = 0x80 + 24 + 0xF0;
        WriteInt32(image, section + 8, length - 0x1000);
        WriteInt32(image, section + 12, 0x1000);
        WriteInt32(image, section + 16, length - 0x1000);
        WriteInt32(image, section + 36, unchecked((int)0x20000000));
        return image;
    }

    private static byte[] LoadPeImage(byte[] file)
    {
        int peOffset = BitConverter.ToInt32(file, 0x3C);
        int sectionCount = BitConverter.ToUInt16(file, peOffset + 6);
        int optionalHeaderSize = BitConverter.ToUInt16(file, peOffset + 20);
        int sizeOfImage = BitConverter.ToInt32(file, peOffset + 24 + 56);
        int sizeOfHeaders = BitConverter.ToInt32(file, peOffset + 24 + 60);
        byte[] image = new byte[sizeOfImage];
        Buffer.BlockCopy(file, 0, image, 0, Math.Min(sizeOfHeaders, file.Length));
        int sectionTable = peOffset + 24 + optionalHeaderSize;
        for (int index = 0; index < sectionCount; index++)
        {
            int header = sectionTable + index * 40;
            int virtualAddress = BitConverter.ToInt32(file, header + 12);
            int rawSize = BitConverter.ToInt32(file, header + 16);
            int rawOffset = BitConverter.ToInt32(file, header + 20);
            if (rawSize <= 0)
                continue;
            Buffer.BlockCopy(file, rawOffset, image, virtualAddress, rawSize);
        }
        return image;
    }

    private static byte[] ParseHex(string pattern) =>
        pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Convert.ToByte(value, 16))
            .ToArray();

    private static byte[] MaterializePattern(string pattern, byte wildcardValue) =>
        pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value == "?" || value == "??" ? wildcardValue : Convert.ToByte(value, 16))
            .ToArray();

    private static void CopyAt(byte[] destination, int offset, byte[] source) =>
        Array.Copy(source, 0, destination, offset, source.Length);

    private static void WriteInt32(byte[] destination, int offset, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, destination, offset, bytes.Length);
    }

    private static void WriteAivStep(
        byte[] destination,
        int stepIndex,
        byte status,
        short commandBuildingType)
    {
        int offset = AiStoneReservePolicy.StepsOffset + stepIndex * AiStoneReservePolicy.StepSize;
        destination[offset] = status;
        byte[] typeBytes = BitConverter.GetBytes(commandBuildingType);
        destination[offset + 2] = typeBytes[0];
        destination[offset + 3] = typeBytes[1];
    }

    private static void ExpectInvalidOperation(Action action, string failureMessage)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(failureMessage);
    }

    private static void TestMarketOrderPresetRoundTrip()
    {
        string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MarketOrderPresetTest.dll");
        string settingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "LobbyModSettings",
            "MarketOrderPresetTest.msgpack");
        if (File.Exists(settingsPath))
            File.Delete(settingsPath);

        int[] hd = MarketGoodsOrderDefinition.CreateHdOrder();
        int[] preset1 = MarketGoodsOrderDefinition.SwapGoodWithNeighbor(hd, hd[2], 1);
        int[] preset2 = MarketGoodsOrderDefinition.SwapGoodWithNeighbor(hd, hd[10], -1);

        var first = new MarketOrderPresetViewModel();
        first.PreparePresets(null, pluginPath, "MarketOrderPresetTest");
        first.ActivatePresets();
        first.Order = preset1;
        first.SelectedPreset = 1;
        first.Order = preset2;
        first.SelectedPreset = 0;
        Check(MarketGoodsOrderDefinition.AreEqual(first.Order, preset1),
            "preset 1 did not restore its market order");

        var restored = new MarketOrderPresetViewModel();
        restored.PreparePresets(null, pluginPath, "MarketOrderPresetTest");
        restored.ActivatePresets();
        Check(MarketGoodsOrderDefinition.AreEqual(restored.Order, preset1),
            "persisted preset 1 market order did not survive restart");
        restored.SelectedPreset = 1;
        Check(MarketGoodsOrderDefinition.AreEqual(restored.Order, preset2),
            "persisted preset 2 market order did not survive restart");

        int[] getterCopy = restored.Order;
        getterCopy[0] = 123456;
        Check(restored.Order[0] != 123456,
            "market-order getter exposed mutable persisted state");
    }

    private static void TestPresetLocalRoundTrip()
    {
        string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PresetLocalRoundTrip.dll");
        string settingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "LobbyModSettings",
            "PresetLocalRoundTrip.msgpack");
        if (File.Exists(settingsPath))
            File.Delete(settingsPath);

        GameNetworkAPI.LocalHost = true;
        var first = new MixedViewModel();
        first.PreparePresets(null, pluginPath, "PresetLocalRoundTrip");
        first.ActivatePresets();
        first.LocalValue = 411;
        first.SelectedPreset = 1;
        first.LocalValue = 422;
        first.SelectedPreset = 0;

        var restored = new MixedViewModel();
        restored.PreparePresets(null, pluginPath, "PresetLocalRoundTrip");
        restored.ActivatePresets();
        Check(restored.LocalValue == 411,
            "PresetLocal value from preset 1 did not survive restart");
        restored.SelectedPreset = 1;
        Check(restored.LocalValue == 422,
            "PresetLocal value from preset 2 did not survive restart");

        string castlePlannerPluginPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "CastlePlannerPresetProbe.dll");
        string castlePlannerSettingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "LobbyModSettings",
            "CastlePlannerPresetProbe.msgpack");
        if (File.Exists(castlePlannerSettingsPath))
            File.Delete(castlePlannerSettingsPath);

        GameNetworkAPI.LocalHost = true;
        GameNetworkAPI.Networked = true;
        GameNetworkAPI.MultiplayerGame = true;
        var castlePlanner = new CastlePlannerPresetProbeViewModel();
        castlePlanner.PreparePresets(
            null,
            castlePlannerPluginPath,
            "CastlePlannerPresetProbe");
        castlePlanner.ActivatePresets();
        Check(castlePlanner.Blueprints,
            "CastlePlanner Blueprint code default was not enabled");
        Check(castlePlanner.AllBlueprintCategoriesEnabled,
            "CastlePlanner Blueprint category defaults were not enabled");
        Check(castlePlanner.SpawnFortifications,
            "CastlePlanner SpawnFortifications default was not enabled");
        castlePlanner.SpawnFortifications = false;
        castlePlanner.BlueprintShowFortifications = false;
        castlePlanner.BlueprintShowDefensiveGroundFeatures = false;
        castlePlanner.SelectedPreset = 1;
        castlePlanner.BlueprintShowBuildings = false;
        castlePlanner.BlueprintShowFearFactorBuildings = false;
        castlePlanner.SelectedPreset = 0;

        GameNetworkAPI.LocalHost = false;
        castlePlanner.System_RefreshSettingsAccess();
        GameNetworkAPI.LocalHost = true;
        castlePlanner.System_RefreshSettingsAccess();
        Check(castlePlanner.Blueprints &&
              !castlePlanner.SpawnFortifications &&
              !castlePlanner.BlueprintShowFortifications &&
              castlePlanner.BlueprintShowBuildings &&
              !castlePlanner.BlueprintShowDefensiveGroundFeatures &&
              castlePlanner.BlueprintShowFearFactorBuildings,
            "CastlePlanner local Blueprint filters changed during multiplayer role refresh");

        var restoredCastlePlanner = new CastlePlannerPresetProbeViewModel();
        restoredCastlePlanner.PreparePresets(
            null,
            castlePlannerPluginPath,
            "CastlePlannerPresetProbe");
        restoredCastlePlanner.ActivatePresets();
        Check(restoredCastlePlanner.Blueprints &&
              !restoredCastlePlanner.SpawnFortifications &&
              !restoredCastlePlanner.BlueprintShowFortifications &&
              restoredCastlePlanner.BlueprintShowBuildings &&
              !restoredCastlePlanner.BlueprintShowDefensiveGroundFeatures &&
              restoredCastlePlanner.BlueprintShowFearFactorBuildings,
            "CastlePlanner Blueprint filters from preset 1 did not survive restart");
        restoredCastlePlanner.SelectedPreset = 1;
        Check(restoredCastlePlanner.BlueprintShowFortifications &&
              !restoredCastlePlanner.BlueprintShowBuildings &&
              restoredCastlePlanner.BlueprintShowDefensiveGroundFeatures &&
              !restoredCastlePlanner.BlueprintShowFearFactorBuildings,
            "CastlePlanner Blueprint filters from preset 2 did not survive restart");
    }

    private static void TestDoNotPersistPresetExclusion()
    {
        string pluginPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DoNotPersistPreset.dll");
        string settingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "LobbyModSettings",
            "DoNotPersistPreset.msgpack");
        if (File.Exists(settingsPath))
            File.Delete(settingsPath);

        GameNetworkAPI.LocalHost = true;
        var viewModel = new MixedViewModel();
        viewModel.PreparePresets(null, pluginPath, "DoNotPersistPreset");
        viewModel.ActivatePresets();
        viewModel.SelectedPreset = 1;
        viewModel.TransientHostValue = 733;
        viewModel.SelectedPreset = 0;

        Dictionary<string, byte[]> disabledSnapshot = viewModel.System_CreateDisabledMissionPresetSnapshot();
        Check(!disabledSnapshot.ContainsKey(nameof(viewModel.TransientHostValue)),
            "DoNotPersist host value entered the mission snapshot");

        Dictionary<string, byte[]> payload = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(
            File.ReadAllBytes(settingsPath));
        Check(!payload.ContainsKey(nameof(viewModel.TransientHostValue)),
            "DoNotPersist host value entered the top-level preset payload");
        foreach (string key in new[] { "__SerpPreset1", "__SerpPreset2" })
        {
            if (!payload.TryGetValue(key, out byte[] bytes))
                continue;
            Dictionary<string, byte[]> preset = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(bytes);
            Check(!preset.ContainsKey(nameof(viewModel.TransientHostValue)),
                "DoNotPersist host value entered " + key);
        }
    }

    private static void TestCastlePlannerBlueprintHudPolicies()
    {
        Check(
            CastlePlanner.BlueprintHudStatePolicy.Resolve(false, false, 0, 0) ==
                CastlePlanner.BlueprintHudDisplayState.Unavailable,
            "Blueprint HUD did not reserve unavailable for a missing Keep");
        Check(
            CastlePlanner.BlueprintHudStatePolicy.Resolve(true, false, 0, 0) ==
                CastlePlanner.BlueprintHudDisplayState.Off,
            "Blueprint HUD treated a hidden or suppressed layout as unavailable");
        Check(
            CastlePlanner.BlueprintHudStatePolicy.Resolve(true, true, 1, 2) ==
                CastlePlanner.BlueprintHudDisplayState.Loading,
            "Blueprint HUD omitted the loading state");
        Check(
            CastlePlanner.BlueprintHudStatePolicy.Resolve(true, true, 2, 2) ==
                CastlePlanner.BlueprintHudDisplayState.On,
            "Blueprint HUD omitted the on state");

    }

    private static void TestCastleSpawnContentPolicy()
    {
        Check(
            CastlePlanner.CastleSpawnContentPolicy.DefaultFortifications &&
            CastlePlanner.CastleSpawnContentPolicy.DefaultBuildings &&
            CastlePlanner.CastleSpawnContentPolicy.DefaultDefensiveGroundFeatures &&
            !CastlePlanner.CastleSpawnContentPolicy.DefaultFearFactorBuildings &&
            !CastlePlanner.CastleSpawnContentPolicy.DefaultSiegeEngines,
            "CastlePlanner castle-content defaults changed unexpectedly");
        Check(
            CastlePlanner.CastleSpawnContentPolicy.ShouldResetBeforeEnabling(
                false,
                true,
                true),
            "CastlePlanner did not reset content before an authoritative spawn enable");
        Check(
            !CastlePlanner.CastleSpawnContentPolicy.ShouldResetBeforeEnabling(
                false,
                true,
                false),
            "CastlePlanner allowed a client to reset host content");
        Check(
            CastlePlanner.CastleSpawnContentPolicy.ShouldDisableBeforeContentChange(
                true,
                true,
                false,
                false,
                false,
                false,
                false,
                false),
            "CastlePlanner retained an enabled spawn without host content");
        Check(
            !CastlePlanner.CastleSpawnContentPolicy.ShouldDisableBeforeContentChange(
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                false),
            "CastlePlanner allowed a client to derive a host spawn change");
        Check(
            !CastlePlanner.CastleSpawnContentPolicy.ShouldDisableBeforeContentChange(
                true,
                true,
                false,
                true,
                false,
                false,
                false,
                false),
            "CastlePlanner disabled spawning while a host content category remained enabled");
        Check(
            !CastlePlanner.CastleSpawnContentPolicy.ShouldDisableBeforeContentChange(
                true,
                true,
                false,
                false,
                false,
                false,
                false,
                true),
            "CastlePlanner disabled spawning while host-controlled braziers and flags remained enabled");
    }

    private static void TestSnapshotCompletionHook()
    {
        GameNetworkAPI.LocalHost = true;
        var viewModel = new SnapshotCompletionProbeViewModel();
        viewModel.PreparePresets(
            null,
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SnapshotCompletionProbe.dll"),
            "SnapshotCompletionProbe");
        viewModel.ActivatePresets();
        Check(viewModel.CompletionCount == 1,
            "Shared snapshot completion hook did not run exactly once during activation");
        Check(!viewModel.ObservedApplyingState,
            "Shared snapshot completion hook ran before snapshot application ended");
    }

    private static void AssertState(MixedViewModel vm, bool host, bool mission, bool editable, bool canEditHost, bool canReset, bool canChangePreset, string context)
    {
        Check(vm.IsLocalSettingsHost == host, context + ": host role");
        Check(vm.IsMissionPresetActive == mission, context + ": mission state");
        Check(vm.MissionPresetEditable == editable, context + ": editable state");
        Check(vm.CanEditHostSettings == canEditHost, context + ": host editability");
        Check(vm.CanEditClientSettings, context + ": client editability");
        Check(vm.CanResetSettings == canReset, context + ": reset availability");
        Check(vm.CanChangePreset == canChangePreset, context + ": preset availability");
    }

    private static void AssertNoSentinel(string path, int sentinel)
    {
        Dictionary<string, byte[]> payload = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(path));
        Check(!ContainsInt(payload, sentinel), "sentinel appeared in top-level msgpack");
        foreach (string key in new[] { "__SerpPreset1", "__SerpPreset2" })
        {
            if (payload.TryGetValue(key, out byte[] bytes))
                Check(!ContainsInt(MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(bytes), sentinel), "sentinel appeared in " + key);
        }
    }

    private static void AssertStoredHostValues(string path, int top, int preset1, int preset2)
    {
        Dictionary<string, byte[]> payload = MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllBytes(path));
        Check(ReadInt(payload, nameof(MixedViewModel.HostValue)) == top, "top-level local host value was not preserved");
        Check(ReadInt(MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(payload["__SerpPreset1"]), nameof(MixedViewModel.HostValue)) == preset1, "preset 1 host value changed");
        Check(ReadInt(MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(payload["__SerpPreset2"]), nameof(MixedViewModel.HostValue)) == preset2, "preset 2 host value changed");
    }

    private static bool ContainsInt(Dictionary<string, byte[]> values, int sentinel) =>
        values.Values.Any(bytes => bytes != null && TryReadInt(bytes, out int value) && value == sentinel);

    private static int ReadInt(Dictionary<string, byte[]> values, string key) => MessagePackSerializer.Deserialize<int>(values[key]);

    private static bool TryReadInt(byte[] bytes, out int value)
    {
        try { value = MessagePackSerializer.Deserialize<int>(bytes); return true; }
        catch { value = 0; return false; }
    }

    private static void TestFreeCastleProtocol()
    {
        PlayerIdentityResolution identity = PlayerIdentityHelper.ResolveLocalPlayerId(
                realMultiplayer: true,
                hasInGameHumanRoster: true,
                nativePlayerId: 3,
                gameMemberPlayerId: 3,
                lobbyPlayerId: 3,
                networkLobbyPlayerId: 2);
        Check(identity.IsResolved && identity.PlayerId == 3 && !string.IsNullOrEmpty(identity.Diagnostic),
            "Shared identity must prefer matching native/game sources and diagnose transitional lobby order");
        identity = PlayerIdentityHelper.ResolveLocalPlayerId(
                realMultiplayer: true,
                hasInGameHumanRoster: true,
                nativePlayerId: 4,
                gameMemberPlayerId: 3,
                lobbyPlayerId: 3,
                networkLobbyPlayerId: 2);
        Check(!identity.IsResolved && identity.Error.Contains("Authoritative"),
            "Shared identity must reject conflicting native and Steam-roster sources");
        identity = PlayerIdentityHelper.ResolveLocalPlayerId(
                realMultiplayer: true,
                hasInGameHumanRoster: false,
                nativePlayerId: 4,
                gameMemberPlayerId: 0,
                lobbyPlayerId: 3,
                networkLobbyPlayerId: 2);
        Check(identity.IsResolved && identity.PlayerId == 3,
            "Shared identity must ignore stale native state and use Vanilla's final mapping in the lobby phase");
        identity = PlayerIdentityHelper.ResolveLocalPlayerId(
                realMultiplayer: true,
                hasInGameHumanRoster: true,
                nativePlayerId: 3,
                gameMemberPlayerId: 3,
                lobbyPlayerId: 2,
                networkLobbyPlayerId: 2);
        Check(!identity.IsResolved && identity.Error.Contains("Final lobby mapping"),
            "Shared identity must reject a final lobby mapping that conflicts with the in-game slot");
        identity = PlayerIdentityHelper.ResolveLocalPlayerId(
                realMultiplayer: true,
                hasInGameHumanRoster: false,
                nativePlayerId: 0,
                gameMemberPlayerId: 0,
                lobbyPlayerId: 0,
                networkLobbyPlayerId: 2);
        Check(identity.IsResolved && identity.PlayerId == 2 && !string.IsNullOrEmpty(identity.Diagnostic),
            "Shared identity must retain the lobby-order ID only as a diagnosed provisional fallback");

        var finalPlayers = new Dictionary<int, ulong>
        {
            [1] = 1001UL,
            [3] = 1003UL
        };
        identity = PlayerIdentityHelper.ResolvePlayerIdForSteamId(1003UL, finalPlayers);
        Check(identity.IsResolved && identity.PlayerId == 3,
            "Shared Steam identity resolution did not return the final client slot after an interleaved AI");
        identity = PlayerIdentityHelper.ResolveAuthenticatedPerPlayerTarget(
            1003UL,
            payloadPlayerId: 2,
            finalPlayers);
        Check(identity.IsResolved && identity.PlayerId == 3 &&
              identity.Diagnostic.Contains("claimed slot 2"),
            "authenticated per-player resolution trusted the provisional payload slot instead of final slot 3");
        identity = PlayerIdentityHelper.ResolvePlayerIdForSteamId(1002UL, finalPlayers);
        Check(!identity.IsResolved && identity.Error.Contains("not part"),
            "Shared Steam identity resolution fell back to a plausible but foreign lobby slot");
        identity = PlayerIdentityHelper.ResolvePlayerIdForSteamId(
            1003UL,
            new Dictionary<int, ulong> { [2] = 1003UL, [3] = 1003UL });
        Check(!identity.IsResolved && identity.Error.Contains("multiple"),
            "Shared Steam identity resolution accepted a duplicated Steam identity");
        identity = PlayerIdentityHelper.ResolvePlayerIdForSteamId(
            1003UL,
            new Dictionary<int, ulong> { [9] = 1003UL });
        Check(!identity.IsResolved && identity.Error.Contains("invalid"),
            "Shared Steam identity resolution accepted an invalid player slot");
        Check(CastlePlanner.FreeCastlePacketRouting.IsOperationBootstrap(
                CastlePlanner.FreeCastlePacketKind.AbortRequest,
                receiverIsHost: true,
                senderIsHost: false),
            "Client abort requests must reach the host before operation IDs converge");
        Check(CastlePlanner.FreeCastlePacketRouting.IsOperationBootstrap(
                CastlePlanner.FreeCastlePacketKind.Reject,
                receiverIsHost: false,
                senderIsHost: true),
            "Host abort broadcasts must reach clients before operation IDs converge");
        Check(!CastlePlanner.FreeCastlePacketRouting.IsOperationBootstrap(
                CastlePlanner.FreeCastlePacketKind.Commit,
                receiverIsHost: false,
                senderIsHost: true),
            "Commit packets must remain bound to the converged operation ID");
        Check(CastlePlanner.FreeCastlePacketRouting.CanHostAcceptPreviewReady(
                awaitingGameplay: true,
                loading: false,
                selecting: false),
            "The host must retain authenticated readiness from a client that reaches gameplay first");
        Check(!CastlePlanner.FreeCastlePacketRouting.CanHostAcceptPreviewReady(
                awaitingGameplay: false,
                loading: false,
                selecting: false),
            "The host must reject PreviewReady outside the preview startup/selection phases");

        var selections = new[]
        {
            new CastlePlanner.FreeCastleSelection
            {
                PlayerId = 2,
                Rotation = 6,
                SpawnBraziersAndFlags = true,
                FlagProjectileType = 22,
                DisplayName = "Second Castle",
                RawData = new short[] { 4, -2, 9 }
            },
            new CastlePlanner.FreeCastleSelection
            {
                PlayerId = 1,
                Rotation = 0,
                SpawnBraziersAndFlags = false,
                FlagProjectileType = ushort.MaxValue,
                DisplayName = "First Castle",
                RawData = new short[] { 1, 2, 3 }
            }
        };
        byte[] encoded = CastlePlanner.FreeCastleProtocol.EncodeSelections(selections);
        byte[] compressed = CastlePlanner.FreeCastleProtocol.Compress(encoded);
        byte[] restored = CastlePlanner.FreeCastleProtocol.Decompress(compressed, encoded.Length);
        List<CastlePlanner.FreeCastleSelection> decoded =
            CastlePlanner.FreeCastleProtocol.DecodeSelections(restored);
        Check(decoded.Count == 2 && decoded[0].PlayerId == 1 && decoded[1].Rotation == 6 &&
              !decoded[0].SpawnBraziersAndFlags && decoded[1].SpawnBraziersAndFlags &&
              decoded[0].FlagProjectileType == ushort.MaxValue &&
              decoded[1].FlagProjectileType == 22,
            "free-castle canonical transfer did not preserve player order and fixed rotation");
        Check(CastlePlanner.FreeCastleProtocol.ProtocolVersion == 4,
            "free-castle selection protocol was not advanced to v4");
        Check(!string.Equals(
                CastlePlanner.FreeCastleProtocol.HashSelectionContent(new short[] { 1, 2, 3 }, 9),
                CastlePlanner.FreeCastleProtocol.HashSelectionContent(new short[] { 1, 2, 3 }, 22),
                StringComparison.OrdinalIgnoreCase),
            "free-castle content hash does not distinguish Lord flag types");

        CastlePlanner.FreeCastleSelection tampered = decoded[1].Clone();
        tampered.FlagProjectileType = 9;
        bool rejectedFlagTampering = false;
        try
        {
            CastlePlanner.FreeCastleProtocol.ValidateSelection(tampered);
        }
        catch (InvalidDataException)
        {
            rejectedFlagTampering = true;
        }
        Check(rejectedFlagTampering,
            "free-castle protocol accepted a flag type changed after hashing");
        Check(CastlePlanner.FreeCastleSelectionLookup.TryGetRotation(decoded, 2, out int spawnedRotation) &&
              spawnedRotation == 6,
            "spawned-castle blueprint did not recover the controlled player's committed rotation");
        Check(!CastlePlanner.FreeCastleSelectionLookup.TryGetRotation(decoded, 8, out _),
            "spawned-castle blueprint recovered a rotation for an absent player");
        Check(CastlePlanner.FreeCastleProtocol.Split(new byte[
                CastlePlanner.FreeCastleProtocol.MaximumChunkBytes + 1]).Count == 2,
            "free-castle transfer did not fragment at the configured boundary");
        var packet = new CastlePlanner.FreeCastlePacket
        {
            ProtocolVersion = CastlePlanner.FreeCastleProtocol.ProtocolVersion,
            Kind = (int)CastlePlanner.FreeCastlePacketKind.SelectionChunk,
            OperationId = 42,
            PlayerId = 2,
            Rotation = 6,
            ContentHash = new string('A', 64),
            ChunkIndex = 3,
            ChunkCount = 7,
            DataBase64 = "AQID"
        };
        CastlePlanner.FreeCastlePacket packetRoundTrip =
            MessagePackSerializer.Deserialize<CastlePlanner.FreeCastlePacket>(
                MessagePackSerializer.Serialize(packet));
        Check(packetRoundTrip.OperationId == 42 && packetRoundTrip.PlayerId == 2 &&
              packetRoundTrip.Rotation == 6 && packetRoundTrip.ChunkIndex == 3,
            "free-castle explicit numeric-key packet formatter did not round-trip");
        bool rejected = false;
        try
        {
            CastlePlanner.FreeCastleProtocol.ValidateSelection(new CastlePlanner.FreeCastleSelection
            {
                PlayerId = 1,
                Rotation = 1,
                DisplayName = "Invalid",
                RawData = new short[] { 1 }
            });
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Check(rejected, "free-castle protocol accepted a non-native fixed rotation");

        const ulong host = 76561190000000001UL;
        const ulong client = 76561190000000002UL;
        var ready = new HashSet<ulong> { host };
        Check(CastlePlanner.FreeCastleParticipantReadiness.AreAllReady(
                new[] { host }, ready),
            "one-human multiplayer did not complete after host readiness");
        Check(!CastlePlanner.FreeCastleParticipantReadiness.AreAllReady(
                new[] { host, client }, ready),
            "multiplayer completed before a remote human was ready");
        ready.Add(client);
        Check(CastlePlanner.FreeCastleParticipantReadiness.AreAllReady(
                new[] { host, client }, ready),
            "multiplayer did not complete after every human was ready");
    }

    private static void TestCastleSpawnCompatibility()
    {
        const string sharedHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        const string otherHash = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
        Check(CastlePlanner.CastleSpawnCompatibility.NormalizeSelection(
                "[Workshop] Later.aivjson",
                Array.Empty<string>(),
                catalogComplete: false) == "[Workshop] Later.aivjson",
            "CastlePlanner discarded a persisted Workshop selection before Steam was ready");
        Check(CastlePlanner.CastleSpawnCompatibility.NormalizeSelection(
                "[Workshop] Later.aivjson",
                new[] { "[Workshop] Later.aivjson" },
                catalogComplete: true) == "[Workshop] Later.aivjson",
            "CastlePlanner rejected a persisted Workshop selection after catalog discovery");
        Check(CastlePlanner.CastleSpawnCompatibility.NormalizeSelection(
                "[Workshop] Removed.aivjson",
                Array.Empty<string>(),
                catalogComplete: true) == string.Empty,
            "CastlePlanner retained a removed selection after the complete catalog refresh");
        var first = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["[Mod] Castle One.aivjson"] = sharedHash,
            ["[Mod] Local Only.aivjson"] = otherHash
        };
        var second = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["[Mod] Castle One.aivjson"] = sharedHash
        };
        string[] manifests = new string[9];
        manifests[1] = CastlePlanner.CastleSpawnCompatibility.EncodeManifest(first);
        manifests[2] = CastlePlanner.CastleSpawnCompatibility.EncodeManifest(second);
        string[] selections = new string[9];
        selections[1] = "[Mod] Castle One.aivjson";
        selections[2] = "[Mod] Castle One.aivjson";

        Check(CastlePlanner.CastleSpawnCompatibility.TryResolvePlayerReports(
                false,
                new[] { 2 },
                "[Mod] Local Only.aivjson",
                manifests[1],
                new string[9],
                new string[9],
                out Dictionary<int, CastlePlanner.CastleSpawnPlayerReport> localReports,
                out _) &&
              localReports.Count == 1 &&
              localReports[2].Selection == "[Mod] Local Only.aivjson" &&
              localReports[2].Manifest == manifests[1],
            "CastlePlanner singleplayer routing required empty synchronized companion slots");
        Check(!CastlePlanner.CastleSpawnCompatibility.TryResolvePlayerReports(
                false,
                Array.Empty<int>(),
                string.Empty,
                manifests[1],
                selections,
                manifests,
                out _,
                out _),
            "CastlePlanner accepted singleplayer spawning without an active human");
        Check(!CastlePlanner.CastleSpawnCompatibility.TryResolvePlayerReports(
                false,
                new[] { 1, 2 },
                string.Empty,
                manifests[1],
                selections,
                manifests,
                out _,
                out _),
            "CastlePlanner inferred singleplayer from local values with multiple humans");
        Check(CastlePlanner.CastleSpawnCompatibility.TryResolvePlayerReports(
                false,
                new[] { 1 },
                string.Empty,
                manifests[1],
                selections,
                manifests,
                out Dictionary<int, CastlePlanner.CastleSpawnPlayerReport> noCastleReports,
                out _) &&
              noCastleReports[1].Selection == string.Empty,
            "CastlePlanner rejected the local No castle selection in singleplayer");
        Check(CastlePlanner.CastleSpawnCompatibility.TryResolvePlayerReports(
                true,
                new[] { 1 },
                "[Mod] Local Only.aivjson",
                "v1",
                selections,
                manifests,
                out Dictionary<int, CastlePlanner.CastleSpawnPlayerReport> multiplayerReports,
                out _) &&
              multiplayerReports[1].Selection == selections[1] &&
              multiplayerReports[1].Manifest == manifests[1],
            "CastlePlanner used local values in a one-human multiplayer game");
        string[] missingMultiplayerManifests = (string[])manifests.Clone();
        missingMultiplayerManifests[1] = null;
        Check(!CastlePlanner.CastleSpawnCompatibility.TryResolvePlayerReports(
                true,
                new[] { 1 },
                "[Mod] Local Only.aivjson",
                manifests[1],
                selections,
                missingMultiplayerManifests,
                out _,
                out _),
            "CastlePlanner accepted a missing report in one-human multiplayer");

        Dictionary<string, string> decoded =
            CastlePlanner.CastleSpawnCompatibility.DecodeManifest(manifests[1]);
        Check(decoded.Count == 2 && decoded["[Mod] Castle One.aivjson"] == sharedHash,
            "CastlePlanner inventory manifest did not round-trip names and hashes");
        Check(CastlePlanner.CastleSpawnCompatibility.IsAvailableToAll(
                "[Mod] Castle One.aivjson", new[] { 1, 2 }, manifests, out string resolvedHash) &&
              resolvedHash == sharedHash,
            "CastlePlanner rejected an identical multiplayer AIVJSON");
        first["[Mod] Unselected Host Only.aivjson"] = otherHash;
        manifests[1] = CastlePlanner.CastleSpawnCompatibility.EncodeManifest(first);
        Check(CastlePlanner.CastleSpawnCompatibility.IsAvailableToAll(
                "[Mod] Castle One.aivjson", new[] { 1, 2 }, manifests, out resolvedHash) &&
              resolvedHash == sharedHash,
            "CastlePlanner rejected a selected AIVJSON because an unrelated inventory entry differed");
        Check(!CastlePlanner.CastleSpawnCompatibility.IsAvailableToAll(
                "[Mod] Local Only.aivjson", new[] { 1, 2 }, manifests, out _),
            "CastlePlanner accepted an AIVJSON missing on one peer");
        second["[Mod] Castle One.aivjson"] = otherHash;
        manifests[2] = CastlePlanner.CastleSpawnCompatibility.EncodeManifest(second);
        Check(!CastlePlanner.CastleSpawnCompatibility.IsAvailableToAll(
                "[Mod] Castle One.aivjson", new[] { 1, 2 }, manifests, out _),
            "CastlePlanner accepted equal names with different SHA-256 values");
        Check(CastlePlanner.CastleSpawnCompatibility.IsAvailableToAll(
                string.Empty, new[] { 1, 2 }, manifests, out _),
            "CastlePlanner rejected the explicit No castle selection");
        Check(CastlePlanner.CastleSpawnCompatibility.DecodeManifest(
                "v2\n" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Castle")) +
                "|" + sharedHash).Count == 0,
            "CastlePlanner accepted an unsupported inventory-manifest version");
        Check(CastlePlanner.CastleSpawnCompatibility.DecodeManifest(
                "v1\n" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Castle")) +
                "|" + new string('Z', 64)).Count == 0,
            "CastlePlanner accepted a non-hexadecimal SHA-256 value");
        Check(CastlePlanner.CastleSpawnCompatibility.TryDecodeManifest(
                "v1", out Dictionary<string, string> emptyInventory) &&
              emptyInventory.Count == 0,
            "CastlePlanner rejected a valid empty inventory manifest");
        Check(!CastlePlanner.CastleSpawnCompatibility.TryDecodeManifest(
                "v2\n" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Castle")) +
                "|" + sharedHash, out _),
            "CastlePlanner strictly decoded an unsupported inventory-manifest version");
        string encodedDuplicateName =
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Castle"));
        string encodedCaseDuplicateName =
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("CASTLE"));
        Check(!CastlePlanner.CastleSpawnCompatibility.TryDecodeManifest(
                "v1\n" + encodedDuplicateName + "|" + sharedHash + "\n" +
                encodedCaseDuplicateName + "|" + sharedHash, out _),
            "CastlePlanner accepted case-insensitively duplicate manifest names");
        Check(!CastlePlanner.CastleSpawnCompatibility.TryDecodeManifest(
                "v1\nmalformed-entry", out _),
            "CastlePlanner silently skipped a malformed manifest entry");
        Check(!CastlePlanner.CastleSpawnCompatibility.TryDecodeManifest(
                "v1\n" + encodedDuplicateName + "| " + sharedHash, out _),
            "CastlePlanner normalized whitespace around a malformed manifest hash");

        var largeInventory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < 2500; index++)
            largeInventory[$"[Workshop] Castle {index:0000}.aivjson"] = sharedHash;
        string largeManifest = CastlePlanner.CastleSpawnCompatibility.EncodeManifest(largeInventory);
        var decodedInventories = new Dictionary<int, IReadOnlyDictionary<string, string>>
        {
            [1] = CastlePlanner.CastleSpawnCompatibility.DecodeManifest(largeManifest),
            [2] = CastlePlanner.CastleSpawnCompatibility.DecodeManifest(largeManifest)
        };
        Check(CastlePlanner.CastleSpawnCompatibility.IsAvailableToAll(
                "[Workshop] Castle 2499.aivjson",
                new[] { 1, 2 },
                decodedInventories,
                out resolvedHash) && resolvedHash == sharedHash,
            "CastlePlanner rejected a matching entry in a large predecoded inventory");
    }

    private static void TestSteamLobbyInvitePolicy()
    {
        SteamInviteValidationInput input = ValidSteamInviteInput();
        Check(SteamLobbyInvitePolicy.Validate(input) == SteamInviteRejectionReason.None,
            "valid Steam-friend invite was rejected");
        Check(SteamLobbyInvitePolicy.IsLobbyMetadataUpdate(109775240900000001UL, 109775240900000001UL),
            "matching lobby metadata callback was ignored");
        Check(!SteamLobbyInvitePolicy.IsLobbyMetadataUpdate(109775240900000001UL, 76561198000000001UL),
            "member-data callback was incorrectly treated as a lobby validation result");
        Check(!SteamLobbyInvitePolicy.IsLobbyMetadataUpdate(0UL, 0UL),
            "invalid zero lobby callback was incorrectly treated as a lobby validation result");

        AssertSteamInviteReason(input, SteamInviteRejectionReason.ClientFeaturesDisabled,
            value => { value.ClientFeaturesEnabled = false; return value; });
        AssertSteamInviteReason(input, SteamInviteRejectionReason.PromptDisabled,
            value => { value.PromptEnabled = false; return value; });
        AssertSteamInviteReason(input, SteamInviteRejectionReason.InvalidInviterId,
            value => { value.InviterIdValid = false; return value; });
        AssertSteamInviteReason(input, SteamInviteRejectionReason.InvalidLobbyId,
            value => { value.LobbyIdValid = false; return value; });
        AssertSteamInviteReason(input, SteamInviteRejectionReason.InvalidGameId,
            value => { value.GameIdValid = false; return value; });
        AssertSteamInviteReason(input, SteamInviteRejectionReason.WrongApp,
            value => { value.InviteAppId = value.CurrentAppId + 1; return value; });
        AssertSteamInviteReason(input, SteamInviteRejectionReason.SelfInvite,
            value => { value.IsSelfInvite = true; return value; });
        AssertSteamInviteReason(input, SteamInviteRejectionReason.SteamBlocked,
            value => { value.Relationship = SteamInviteRelationshipKind.BlockedOrIgnored; return value; });
        AssertSteamInviteReason(input, SteamInviteRejectionReason.NotFriend,
            value => { value.Relationship = SteamInviteRelationshipKind.Other; return value; });
        AssertSteamInviteReason(input, SteamInviteRejectionReason.BlacklistUnavailable,
            value => { value.BlacklistUsable = false; return value; });
        AssertSteamInviteReason(input, SteamInviteRejectionReason.LocallyBlacklisted,
            value => { value.LocallyBlacklisted = true; return value; });

        foreach (SteamInviteRejectionReason reason in Enum.GetValues(typeof(SteamInviteRejectionReason)))
        {
            if (reason != SteamInviteRejectionReason.None)
                Check(!string.IsNullOrWhiteSpace(SteamLobbyInvitePolicy.Describe(reason)),
                    "Steam invite rejection reason has no log description: " + reason);
        }

        string warning = SteamLobbyInvitePolicy.FormatWarning(
            SteamInviteRejectionReason.NotFriend,
            "InitialValidation",
            76561198000000001UL,
            109775240900000001UL,
            3024040UL,
            3024040U,
            3024040U,
            "relationship=None" + Environment.NewLine + "untrusted detail");
        Check(warning.Contains("reason=NotFriend") &&
              warning.Contains("phase=InitialValidation") &&
              warning.Contains("inviterId=76561198000000001") &&
              warning.Contains("lobbyId=109775240900000001") &&
              warning.Contains("gameId=3024040") &&
              warning.Contains("inviteAppId=3024040") &&
               warning.Contains("currentAppId=3024040") &&
               warning.StartsWith("Suppressed in-game Steam lobby-invite popup:", StringComparison.Ordinal) &&
               warning.Contains("description='") &&
              !warning.Contains("\r") && !warning.Contains("\n"),
            "Steam invite rejection warning omitted fields or retained line breaks");
    }

    private static SteamInviteValidationInput ValidSteamInviteInput() =>
        new SteamInviteValidationInput
        {
            ClientFeaturesEnabled = true,
            PromptEnabled = true,
            InviterIdValid = true,
            LobbyIdValid = true,
            GameIdValid = true,
            InviteAppId = 3024040,
            CurrentAppId = 3024040,
            Relationship = SteamInviteRelationshipKind.Friend,
            BlacklistUsable = true,
        };

    private static void AssertSteamInviteReason(
        SteamInviteValidationInput valid,
        SteamInviteRejectionReason expected,
        Func<SteamInviteValidationInput, SteamInviteValidationInput> mutate)
    {
        SteamInviteValidationInput changed = mutate(valid);
        Check(SteamLobbyInvitePolicy.Validate(changed) == expected,
            "Steam invite policy did not return " + expected);
    }

    private static void TestSteamInviteBlacklistStore()
    {
        string directory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "SteamInviteBlacklistTests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "SteamInviteBlacklist.json");
        try
        {
            var store = new SteamInviteBlacklistStore(path);
            Check(store.IsUsable && store.Count == 0, "missing Steam invite blacklist was not treated as empty");
            Check(store.TryAdd(76561198000000001UL, out string error) && error.Length == 0,
                "could not add a Steam invite blacklist entry");
            Check(store.TryAdd(76561198000000001UL, out error) && store.Count == 1,
                "duplicate Steam invite blacklist entry changed the set");
            Check(store.TryAdd(76561198000000002UL, out error) && store.Count == 2,
                "could not add the second Steam invite blacklist entry");
            Check(File.ReadAllText(path).Contains("\"76561198000000001\""),
                "Steam ID was not serialized as a JSON string");

            var reloaded = new SteamInviteBlacklistStore(path);
            Check(reloaded.IsUsable && reloaded.Count == 2 && reloaded.Contains(76561198000000002UL),
                "Steam invite blacklist did not round-trip");
            Check(reloaded.TryClear(out error) && reloaded.IsUsable && reloaded.Count == 0,
                "Steam invite blacklist could not be cleared");

            File.WriteAllText(path, "{}" + Environment.NewLine);
            var invalid = new SteamInviteBlacklistStore(path);
            Check(!invalid.IsUsable && invalid.LoadError.Length > 0,
                "invalid Steam invite blacklist did not fail closed");
            Check(!invalid.TryAdd(76561198000000003UL, out error) && error.Length > 0,
                "invalid Steam invite blacklist accepted a new entry");
            Check(invalid.TryClear(out error) && invalid.IsUsable && invalid.Count == 0,
                "explicit clear did not recover an invalid Steam invite blacklist");

            File.WriteAllText(
                path,
                "{\"version\":1,\"blockedSteamIds\":[\"76561198000000004\",\"76561198000000004\"]}" +
                Environment.NewLine);
            var duplicateFile = new SteamInviteBlacklistStore(path);
            Check(duplicateFile.IsUsable && duplicateFile.Count == 1,
                "Steam invite blacklist did not deduplicate persisted Steam IDs");

            var maximumIds = new List<object>();
            for (ulong index = 1; index <= SteamInviteBlacklistStore.MaximumEntries; index++)
                maximumIds.Add((76561198000100000UL + index).ToString());
            var maximumRoot = new Dictionary<string, object>
            {
                ["version"] = 1,
                ["blockedSteamIds"] = maximumIds,
            };
            File.WriteAllText(path, DependencyFreeJson.Serialize(maximumRoot));
            var full = new SteamInviteBlacklistStore(path);
            Check(full.IsUsable && full.Count == SteamInviteBlacklistStore.MaximumEntries,
                "maximum-size Steam invite blacklist did not load");
            Check(!full.TryAdd(76561198000999999UL, out error) && error.Contains("limit"),
                "Steam invite blacklist accepted an entry beyond its limit");

            File.WriteAllText(path, new string(' ', (int)SteamInviteBlacklistStore.MaximumStoreBytes + 1));
            var oversized = new SteamInviteBlacklistStore(path);
            Check(!oversized.IsUsable && oversized.LoadError.Contains("large"),
                "oversized Steam invite blacklist did not fail closed");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void TestSharedLobbyLifecycle()
    {
        LobbyLifecycle.System_TestReset();
        var order = new List<string>();
        var requested = new Platform_Multiplayer.MPLobby
        {
            id = new Platform_Multiplayer.TestSteamId { m_SteamID = 1234 }
        };
        Platform_Multiplayer.Instance.activeLobby = requested;

        IDisposable first = LobbyLifecycle.SubscribeJoined(null, _ => order.Add("first"));
        IDisposable throwing = LobbyLifecycle.SubscribeJoined(null, _ => throw new InvalidOperationException("expected"));
        IDisposable second = LobbyLifecycle.SubscribeJoined(null, _ => order.Add("second"));
        Check(LobbyLifecycle.System_TestInstallCount == 1,
            "Shared lobby lifecycle installed more than one process-wide hook");

        LobbyLifecycle.System_TestCompleteJoin(requested, () => order.Add("vanilla"));
        Check(order.SequenceEqual(new[] { "vanilla", "first", "second" }),
            "Shared lobby lifecycle changed callback order or stopped after a subscriber exception");

        first.Dispose();
        order.Clear();
        LobbyLifecycle.System_TestCompleteJoin(requested, () => order.Add("vanilla"));
        Check(order.SequenceEqual(new[] { "vanilla", "second" }),
            "Shared lobby lifecycle removed the wrong subscriber");

        order.Clear();
        Platform_Multiplayer.Instance.activeLobby = null;
        LobbyLifecycle.System_TestCompleteJoin(requested, () => order.Add("vanilla"));
        Check(order.SequenceEqual(new[] { "vanilla" }),
            "Shared lobby lifecycle notified subscribers after a failed join");

        order.Clear();
        Platform_Multiplayer.Instance.activeLobby = new Platform_Multiplayer.MPLobby
        {
            id = new Platform_Multiplayer.TestSteamId { m_SteamID = 5678 }
        };
        LobbyLifecycle.System_TestCompleteJoin(requested, () => order.Add("vanilla"));
        Check(order.SequenceEqual(new[] { "vanilla" }),
            "Shared lobby lifecycle accepted a different active lobby");

        throwing.Dispose();
        second.Dispose();
        LobbyLifecycle.System_TestReset();
    }
}

[MessagePackObject]
public sealed class FutureMultiplayerGameSpeedPacket
{
    [Key(0)] public int ProtocolVersion;
    [Key(1)] public int Action;
    [Key(2)] public int TargetSpeed;
    [Key(3)] public int PauseState;
    [Key(4)] public string FutureField;
}

internal sealed class RoutingProbeViewModel : SHCDESE.ViewModels.LobbyModSettingsBaseViewModel
{
    private int hostValue = 1;
    private int playerValue = 1;
    private int localValue = 1;
    private int transientValue = 1;
    private int presetOnlyValue = 1;
    private int uiOnlyValue = 1;

    [SyncHostOnly]
    public int HostValue { get => hostValue; set => Set(ref hostValue, value, nameof(HostValue)); }

    [SyncPerPlayer]
    public int PlayerValue { get => playerValue; set => Set(ref playerValue, value, nameof(PlayerValue)); }

    [PersistLocal]
    public int LocalValue { get => localValue; set => Set(ref localValue, value, nameof(LocalValue)); }

    [SyncPerPlayer, DoNotPersist]
    public int TransientValue { get => transientValue; set => Set(ref transientValue, value, nameof(TransientValue)); }

    [PresetLocal]
    public int PresetOnlyValue { get => presetOnlyValue; set => Set(ref presetOnlyValue, value, nameof(PresetOnlyValue)); }

    public int UiOnlyValue { get => uiOnlyValue; set => Set(ref uiOnlyValue, value, nameof(UiOnlyValue)); }

    private void Set(ref int field, int value, string propertyName)
    {
        if (field == value)
            return;
        field = value;
        OnPropertyChanged(propertyName);
    }
}

internal sealed class MixedViewModel : PresetLobbyModSettingsViewModel
{
    private int hostValue = 101;
    private int clientValue = 201;
    private int localValue = 301;
    private int transientHostValue = 401;

    [SyncHostOnly]
    public int HostValue
    {
        get => hostValue;
        set
        {
            if (!CanMutateSettingWithDependents(nameof(HostValue), nameof(HostValueText)))
                return;
            if (hostValue == value)
                return;
            hostValue = value;
            OnPropertyChanged(nameof(HostValue));
            OnPropertyChanged(nameof(HostValueText));
        }
    }

    public string HostValueText => HostValue.ToString();

    [SyncPerPlayer]
    public int ClientValue { get => clientValue; set { if (clientValue == value) return; clientValue = value; OnPropertyChanged(nameof(ClientValue)); } }

    [PresetLocal]
    public int LocalValue { get => localValue; set { if (localValue == value) return; localValue = value; OnPropertyChanged(nameof(LocalValue)); } }

    [SyncHostOnly, DoNotPersist]
    public int TransientHostValue
    {
        get => transientHostValue;
        set
        {
            if (!CanMutateSetting(nameof(TransientHostValue)) || transientHostValue == value)
                return;
            transientHostValue = value;
            OnPropertyChanged(nameof(TransientHostValue));
        }
    }
}

internal sealed class CastlePlannerPresetProbeViewModel : PresetLobbyModSettingsViewModel
{
    private bool blueprints = true;
    private bool spawnFortifications = true;
    private bool blueprintShowFortifications = true;
    private bool blueprintShowBuildings = true;
    private bool blueprintShowDefensiveGroundFeatures = true;
    private bool blueprintShowFearFactorBuildings = true;

    public bool AllBlueprintCategoriesEnabled =>
        BlueprintShowFortifications &&
        BlueprintShowBuildings &&
        BlueprintShowDefensiveGroundFeatures &&
        BlueprintShowFearFactorBuildings;

    [SyncHostOnly]
    public bool SpawnFortifications
    {
        get => spawnFortifications;
        set
        {
            if (!CanMutateSetting(nameof(SpawnFortifications)) ||
                spawnFortifications == value)
            {
                return;
            }

            spawnFortifications = value;
            OnPropertyChanged(nameof(SpawnFortifications));
        }
    }

    [PresetLocal]
    public bool Blueprints
    {
        get => blueprints;
        set
        {
            if (blueprints == value)
                return;
            blueprints = value;
            OnPropertyChanged(nameof(Blueprints));
        }
    }

    [PresetLocal]
    public bool BlueprintShowFortifications
    {
        get => blueprintShowFortifications;
        set => SetBlueprintOption(
            ref blueprintShowFortifications,
            value,
            nameof(BlueprintShowFortifications));
    }

    [PresetLocal]
    public bool BlueprintShowBuildings
    {
        get => blueprintShowBuildings;
        set => SetBlueprintOption(
            ref blueprintShowBuildings,
            value,
            nameof(BlueprintShowBuildings));
    }

    [PresetLocal]
    public bool BlueprintShowDefensiveGroundFeatures
    {
        get => blueprintShowDefensiveGroundFeatures;
        set => SetBlueprintOption(
            ref blueprintShowDefensiveGroundFeatures,
            value,
            nameof(BlueprintShowDefensiveGroundFeatures));
    }

    [PresetLocal]
    public bool BlueprintShowFearFactorBuildings
    {
        get => blueprintShowFearFactorBuildings;
        set => SetBlueprintOption(
            ref blueprintShowFearFactorBuildings,
            value,
            nameof(BlueprintShowFearFactorBuildings));
    }

    private void SetBlueprintOption(
        ref bool field,
        bool value,
        string propertyName)
    {
        if (field == value)
            return;
        field = value;
        OnPropertyChanged(propertyName);
    }
}

internal sealed class SnapshotCompletionProbeViewModel : PresetLobbyModSettingsViewModel
{
    private bool value = true;

    internal int CompletionCount { get; private set; }
    internal bool ObservedApplyingState { get; private set; }

    [SyncHostOnly]
    public bool Value
    {
        get => value;
        set
        {
            if (!CanMutateSetting(nameof(Value)) || this.value == value)
                return;
            this.value = value;
            OnPropertyChanged(nameof(Value));
        }
    }

    protected override void OnSettingsSnapshotApplied()
    {
        CompletionCount++;
        ObservedApplyingState = IsApplyingSettingsSnapshot;
    }
}

internal sealed class SurrenderAndStatisticsSettingViewModel : PresetLobbyModSettingsViewModel
{
    private bool enableAiFixes = true;
    private bool enableSurrenderAndStatistics = true;
    private bool enableLordUnitControls = true;
    private bool enableEliminatedPlayersBecomeSpectators = true;
    private bool enableAbruptHostMigrationFix = true;
    private bool enableReturnToMultiplayerLobby = true;
    private bool allowFullAiMultiplayerLobby = true;

    [SyncHostOnly]
    public bool EnableAiFixes
    {
        get => enableAiFixes;
        set
        {
            if (!CanMutateSetting(nameof(EnableAiFixes)) || enableAiFixes == value)
                return;
            enableAiFixes = value;
            OnPropertyChanged(nameof(EnableAiFixes));
        }
    }

    [SyncHostOnly]
    public bool EnableSurrenderAndStatistics
    {
        get => enableSurrenderAndStatistics;
        set
        {
            if (!CanMutateSetting(nameof(EnableSurrenderAndStatistics)) || enableSurrenderAndStatistics == value)
                return;
            enableSurrenderAndStatistics = value;
            OnPropertyChanged(nameof(EnableSurrenderAndStatistics));
        }
    }

    [SyncHostOnly]
    public bool EnableLordUnitControls
    {
        get => enableLordUnitControls;
        set
        {
            if (!CanMutateSetting(nameof(EnableLordUnitControls)) || enableLordUnitControls == value)
                return;
            enableLordUnitControls = value;
            OnPropertyChanged(nameof(EnableLordUnitControls));
        }
    }

    [SyncHostOnly]
    public bool EnableEliminatedPlayersBecomeSpectators
    {
        get => enableEliminatedPlayersBecomeSpectators;
        set
        {
            if (!CanMutateSetting(nameof(EnableEliminatedPlayersBecomeSpectators)) ||
                enableEliminatedPlayersBecomeSpectators == value)
            {
                return;
            }
            enableEliminatedPlayersBecomeSpectators = value;
            OnPropertyChanged(nameof(EnableEliminatedPlayersBecomeSpectators));
        }
    }

    [SyncHostOnly]
    public bool EnableAbruptHostMigrationFix
    {
        get => enableAbruptHostMigrationFix;
        set
        {
            if (!CanMutateSetting(nameof(EnableAbruptHostMigrationFix)) ||
                enableAbruptHostMigrationFix == value)
            {
                return;
            }
            enableAbruptHostMigrationFix = value;
            OnPropertyChanged(nameof(EnableAbruptHostMigrationFix));
        }
    }

    [SyncHostOnly]
    public bool EnableReturnToMultiplayerLobby
    {
        get => enableReturnToMultiplayerLobby;
        set
        {
            if (!CanMutateSetting(nameof(EnableReturnToMultiplayerLobby)) ||
                enableReturnToMultiplayerLobby == value)
            {
                return;
            }
            enableReturnToMultiplayerLobby = value;
            OnPropertyChanged(nameof(EnableReturnToMultiplayerLobby));
        }
    }

    [SyncHostOnly]
    public bool AllowFullAiMultiplayerLobby
    {
        get => allowFullAiMultiplayerLobby;
        set
        {
            if (!CanMutateSetting(nameof(AllowFullAiMultiplayerLobby)) ||
                allowFullAiMultiplayerLobby == value)
            {
                return;
            }
            allowFullAiMultiplayerLobby = value;
            OnPropertyChanged(nameof(AllowFullAiMultiplayerLobby));
        }
    }

    internal void ResetSurrenderAndStatistics()
    {
        EnableAiFixes = true;
        EnableSurrenderAndStatistics = true;
        EnableLordUnitControls = true;
        EnableEliminatedPlayersBecomeSpectators = true;
        EnableAbruptHostMigrationFix = true;
        EnableReturnToMultiplayerLobby = true;
        AllowFullAiMultiplayerLobby = true;
    }
}

internal sealed class MarketOrderPresetViewModel : PresetLobbyModSettingsViewModel
{
    private readonly LocalPerPlayerSetting<int[]> order =
        new LocalPerPlayerSetting<int[]>(
            MarketGoodsOrderDefinition.CreateHdOrder(),
            MarketGoodsOrderDefinition.CloneOrDefault);

    public int[][] OrderData => order.Data;

    [SyncPerPlayer]
    public int[] Order
    {
        get => MarketGoodsOrderDefinition.CloneOrDefault(order.Value);
        set
        {
            if (!CanMutateSetting(nameof(Order)))
                return;
            int[] normalized = MarketGoodsOrderDefinition.CloneOrDefault(value);
            if (MarketGoodsOrderDefinition.AreEqual(order.Value, normalized))
                return;
            order.SetValue(normalized);
            OnPropertyChanged(nameof(Order));
        }
    }
}

internal sealed class SharedPerPlayerProbeViewModel : PresetLobbyModSettingsViewModel
{
    private int[] preference = { 4, 5, 6 };

    public int LocalPlayerId { get; private set; }
    public int ObservationCount { get; private set; }
    public int LobbyChangeCount { get; private set; }
    public int RemoteDataChangeCount { get; private set; }
    public PerPlayerLobbySnapshot LastLobbySnapshot { get; private set; }
    public int[][] PreferenceData { get; } = new int[9][];

    [SyncPerPlayer]
    public int[] Preference
    {
        get => (int[])preference.Clone();
        set
        {
            preference = value == null ? null : (int[])value.Clone();
            OnPropertyChanged(nameof(Preference));
        }
    }

    protected override void ConfigurePerPlayerLobbySettings(PerPlayerLobbySettingsBuilder settings)
    {
        settings
            .ResetSlotsWith(nameof(Preference), () => null)
            .RequireReport(nameof(Preference))
            .WhenLocalPlayerResolved(id => LocalPlayerId = id)
            .WhenLobbyChanged(snapshot =>
            {
                LobbyChangeCount++;
                LastLobbySnapshot = snapshot;
            })
            .WhenRemoteDataChanged(_ => RemoteDataChangeCount++)
            .OnObservation(() => ObservationCount++);
    }
}

internal sealed class MissingCompanionViewModel : PresetLobbyModSettingsViewModel
{
    [SyncPerPlayer]
    public int Value { get; set; }
}

internal sealed class ConflictingPerPlayerViewModel : PresetLobbyModSettingsViewModel
{
    public int[] ValueData { get; } = new int[9];

    [SyncHostOnly, SyncPerPlayer]
    public int Value { get; set; }
}

internal sealed class MultidimensionalCompanionViewModel : PresetLobbyModSettingsViewModel
{
    public int[,] ValueData { get; } = new int[9, 1];

    [SyncPerPlayer]
    public int Value { get; set; }
}

internal sealed class UnstableCompanionViewModel : PresetLobbyModSettingsViewModel
{
    public int[] ValueData => new int[9];

    [SyncPerPlayer]
    public int Value { get; set; }
}

internal sealed class HostOnlyViewModel : PresetLobbyModSettingsViewModel
{
    private int hostValue;

    [SyncHostOnly]
    public int HostValue
    {
        get => hostValue;
        set
        {
            if (!CanMutateSetting(nameof(HostValue)) || hostValue == value)
                return;
            hostValue = value;
            OnPropertyChanged(nameof(HostValue));
        }
    }
}

internal sealed class ActivationViewModel : PresetLobbyModSettingsViewModel
{
    private bool enableMod = true;
    private bool enableClientFeatures = true;

    [SyncHostOnly]
    public bool EnableMod
    {
        get => enableMod;
        set
        {
            if (!CanMutateSetting(nameof(EnableMod)) || enableMod == value)
                return;
            enableMod = value;
            OnPropertyChanged(nameof(EnableMod));
        }
    }

    [PresetLocal]
    public bool EnableClientFeatures
    {
        get => enableClientFeatures;
        set
        {
            if (!CanMutateSetting(nameof(EnableClientFeatures)) || enableClientFeatures == value)
                return;
            enableClientFeatures = value;
            OnPropertyChanged(nameof(EnableClientFeatures));
        }
    }
}

internal sealed class ConflictingAttributesViewModel : PresetLobbyModSettingsViewModel
{
    private int hostValue;

    [SyncHostOnly, SyncPerPlayer]
    public int HostValue
    {
        get => hostValue;
        set
        {
            if (!CanMutateSetting(nameof(HostValue)) || hostValue == value)
                return;
            hostValue = value;
            OnPropertyChanged(nameof(HostValue));
        }
    }
}

internal sealed class CompoundViewModel : PresetLobbyModSettingsViewModel
{
    private int minimum = 1;
    private int maximum = 10;

    [SyncHostOnly]
    public int Minimum
    {
        get => minimum;
        set
        {
            if (!CanMutateSetting(nameof(Minimum)))
                return;

            int normalized = Math.Max(1, value);
            bool minimumChanged = minimum != normalized;
            bool maximumChanged = maximum < normalized;
            if (!minimumChanged && !maximumChanged)
                return;

            minimum = normalized;
            if (maximumChanged)
                maximum = normalized;
            if (minimumChanged)
                OnPropertyChanged(nameof(Minimum));
            if (maximumChanged)
                OnPropertyChanged(nameof(Maximum));
        }
    }

    [SyncHostOnly]
    public int Maximum
    {
        get => maximum;
        set
        {
            if (!CanMutateSetting(nameof(Maximum)) || maximum == value)
                return;
            maximum = value;
            OnPropertyChanged(nameof(Maximum));
        }
    }
}

internal sealed class NestedTableViewModel : PresetLobbyModSettingsViewModel
{
    private string serialized = "7";

    public NestedTableViewModel()
    {
        Row = new NestedRowViewModel(
            7,
            () => CanMutateSetting(nameof(Serialized)),
            value =>
            {
                serialized = value.ToString();
                OnPropertyChanged(nameof(Serialized));
            });
    }

    public NestedRowViewModel Row { get; }

    [SyncHostOnly]
    public string Serialized
    {
        get => serialized;
        set
        {
            if (!CanMutateSetting(nameof(Serialized)))
                return;
            if (serialized == value || !int.TryParse(value, out int parsed))
                return;
            serialized = value;
            Row.SetValueFromOwner(parsed);
            OnPropertyChanged(nameof(Serialized));
        }
    }
}

internal sealed class NestedRowViewModel : INotifyPropertyChanged
{
    private readonly Func<bool> canEdit;
    private readonly Action<int> changed;
    private int value;

    public NestedRowViewModel(int value, Func<bool> canEdit, Action<int> changed)
    {
        this.value = value;
        this.canEdit = canEdit;
        this.changed = changed;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public int Value => value;

    public string ValueText
    {
        get => value.ToString();
        set
        {
            if (!int.TryParse(value, out int parsed))
            {
                OnPropertyChanged(nameof(ValueText));
                return;
            }
            SetValue(parsed, true);
        }
    }

    public void SetValueFromOwner(int newValue) => SetValue(newValue, false);

    private void SetValue(int newValue, bool notifyOwner)
    {
        if (!canEdit())
        {
            OnPropertyChanged(nameof(ValueText));
            return;
        }
        if (value == newValue)
            return;
        value = newValue;
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(ValueText));
        if (notifyOwner)
            changed(value);
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

namespace BepInEx
{
    public class BaseUnityPlugin { public PluginInfo Info { get; } = new PluginInfo(); }
    public sealed class PluginInfo { public string Location { get; set; } = typeof(Program).Assembly.Location; }
}

namespace BepInEx.Logging { public sealed class ManualLogSource { } }

namespace Noesis
{
    public enum Visibility { Visible, Hidden, Collapsed }
    public sealed class ComboBoxItem { public object Content { get; set; } public Visibility Visibility { get; set; } }
}

namespace SHCDESE.Interop
{
    public enum eGoods : short
    {
        STORED_WOOD_PLANKS = 2,
        STORED_RAW_HOPS = 3,
        STORED_STONE_BLOCKS = 4,
        STORED_IRON_INGOTS = 6,
        STORED_PITCH_REFINED = 8,
        STORED_RAW_WHEAT = 9,
        STORED_FOOD_BREAD = 10,
        STORED_FOOD_CHEESE = 11,
        STORED_FOOD_MEAT = 12,
        STORED_FOOD_FRUIT = 13,
        STORED_FOOD_ALE = 14,
        STORED_FLOUR = 16,
        STORED_BOWS = 17,
        STORED_CROSSBOWS = 18,
        STORED_SPEARS = 19,
        STORED_PIKES = 20,
        STORED_MACES = 21,
        STORED_SWORDS = 22,
        STORED_LEATHER_ARMOUR = 23,
        STORED_METAL_ARMOUR = 24
    }

    public enum eChimps : ushort
    {
        CHIMP_TYPE_KNIGHT = 28,
        CHIMP_TYPE_ARAB_ASSASIN = 73,
        CHIMP_TYPE_LORD = 55
    }
}

namespace SHCDESE.Interop.Enums
{
    public enum AliveState : short
    {
        None = 0,
        NeedsInit = 1,
        IsAlive = 2,
        MarkedForDeletion = 3
    }

    public enum TribeAICommand : uint
    {
        UnitStop = 31
    }
}

namespace SHCDESE.EventAPI
{
    public enum EventHookPhase
    {
        Pre,
        Post
    }
}

namespace SHCDESE.ViewModels
{
    public class LobbyModSettingsBaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        internal bool IsSuppressingSync { get; private set; }
        internal bool IsApplyingAuthorisedUpdate { get; private set; }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public void System_TriggerUpdate(string name) => OnPropertyChanged(name);
        internal void BeginAuthorisedUpdate() => IsApplyingAuthorisedUpdate = true;
        internal void EndAuthorisedUpdate() => IsApplyingAuthorisedUpdate = false;

        protected bool CanEdit(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || IsApplyingAuthorisedUpdate)
                return true;

            PropertyInfo property = GetType().GetProperty(propertyName);
            if (property?.GetCustomAttribute<SyncHostOnlyAttribute>() == null ||
                !GameNetworkAPI.IsNetworkedEnvironment() ||
                GameNetworkAPI.IsLocalHost())
            {
                return true;
            }

            NotifyRevert(propertyName);
            return false;
        }

        private void NotifyRevert(string propertyName)
        {
            IsSuppressingSync = true;
            try
            {
                OnPropertyChanged(propertyName);
            }
            finally
            {
                IsSuppressingSync = false;
            }
        }
    }
}

namespace SHCDESE.API.Components.Network
{
    [AttributeUsage(AttributeTargets.Property)] public sealed class SyncHostOnlyAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property)] public sealed class SyncPerPlayerAttribute : Attribute { }
}

namespace SHCDESE.API.Components.ModManager
{
    [AttributeUsage(AttributeTargets.Property)] public sealed class PersistLocalAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property)] public sealed class DoNotPersistAttribute : Attribute { }

    public static class LobbyModSettingsStorage
    {
        public const string STORAGE_FOLDER_NAME = "LobbyModSettings";
        public const string FILE_EXTENSION = ".msgpack";
    }
}

namespace SHCDESE.API
{
    public static class GameNetworkAPI
    {
        public static bool LocalHost = true;
        public static bool Networked = true;
        public static bool ThrowOnRoleQuery;
        public static bool MultiplayerGame
        {
            get => Platform_Multiplayer.MPGameActive;
            set => Platform_Multiplayer.MPGameActive = value;
        }
        public static bool IsNetworkedEnvironment() => Networked;
        public static bool IsMultiplayerGame() => Platform_Multiplayer.MPGameActive;
        public static bool IsLocalHost()
        {
            if (ThrowOnRoleQuery)
                throw new InvalidOperationException("simulated unavailable network singleton");
            return LocalHost;
        }
    }
    public sealed class GameXAMLManagerAPI
    {
        private bool _isProcessingNetworkSync;
        private Dictionary<string, byte[]> routingCache =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public static GameXAMLManagerAPI Instance { get; } = new GameXAMLManagerAPI();
        public List<Registration> RegisteredModSettings { get; } = new List<Registration>();
        public int BroadcastCount { get; private set; }
        public int SaveCount { get; private set; }
        public bool FailNextRegistration { get; set; }
        public bool ThrowNextRegistration { get; set; }

        public void RegisterLobbyModSettings(global::BepInEx.BaseUnityPlugin plugin, string name, object vm, string xaml)
        {
            if (ThrowNextRegistration)
            {
                ThrowNextRegistration = false;
                throw new InvalidOperationException("Synthetic registration failure.");
            }
            if (FailNextRegistration)
            {
                FailNextRegistration = false;
                return;
            }
            RegisteredModSettings.Add(new Registration
            {
                ViewModel = vm,
                View = new object()
            });
            if (!(vm is INotifyPropertyChanged notify))
                return;

            notify.PropertyChanged += (sender, args) =>
            {
                if (_isProcessingNetworkSync ||
                    sender is SHCDESE.ViewModels.LobbyModSettingsBaseViewModel revertingVm && revertingVm.IsSuppressingSync ||
                    sender == null ||
                    string.IsNullOrEmpty(args.PropertyName))
                {
                    return;
                }

                PropertyInfo property = sender.GetType().GetProperty(args.PropertyName);
                if (property == null)
                    return;

                bool hostOnly = property.GetCustomAttribute<SHCDESE.API.Components.Network.SyncHostOnlyAttribute>() != null;
                bool perPlayer = property.GetCustomAttribute<SHCDESE.API.Components.Network.SyncPerPlayerAttribute>() != null;
                bool synced = hostOnly || perPlayer;
                bool persisted =
                    (synced || property.GetCustomAttribute<SHCDESE.API.Components.ModManager.PersistLocalAttribute>() != null) &&
                    property.GetCustomAttribute<SHCDESE.API.Components.ModManager.DoNotPersistAttribute>() == null;
                if (!synced && !persisted)
                    return;
                if (hostOnly && GameNetworkAPI.IsNetworkedEnvironment() && !GameNetworkAPI.IsLocalHost())
                    return;

                if (synced && GameNetworkAPI.IsNetworkedEnvironment())
                    BroadcastCount++;
                if (persisted)
                    SaveSnapshot(sender);
            };
        }

        public void ResetRoutingProbe()
        {
            RegisteredModSettings.Clear();
            FailNextRegistration = false;
            ThrowNextRegistration = false;
            routingCache = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            ResetRoutingCounts();
        }

        public void ResetRoutingCounts()
        {
            BroadcastCount = 0;
            SaveCount = 0;
        }

        public void PrimeStoredInt(string propertyName, int value) =>
            routingCache[propertyName] = MessagePackSerializer.Serialize(value);

        public bool HasStoredValue(string propertyName) => routingCache.ContainsKey(propertyName);

        public int ReadStoredInt(string propertyName) =>
            MessagePackSerializer.Deserialize<int>(routingCache[propertyName]);

        private void SaveSnapshot(object viewModel)
        {
            var payload = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (PropertyInfo property in viewModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                bool hostOnly = property.GetCustomAttribute<SHCDESE.API.Components.Network.SyncHostOnlyAttribute>() != null;
                bool synced = hostOnly ||
                    property.GetCustomAttribute<SHCDESE.API.Components.Network.SyncPerPlayerAttribute>() != null;
                bool persisted =
                    (synced || property.GetCustomAttribute<SHCDESE.API.Components.ModManager.PersistLocalAttribute>() != null) &&
                    property.GetCustomAttribute<SHCDESE.API.Components.ModManager.DoNotPersistAttribute>() == null;
                if (!property.CanRead || !persisted)
                    continue;

                if (hostOnly && !GameNetworkAPI.IsLocalHost())
                {
                    if (routingCache.TryGetValue(property.Name, out byte[] cached))
                        payload[property.Name] = (byte[])cached.Clone();
                    continue;
                }

                object value = property.GetValue(viewModel);
                if (value != null)
                    payload[property.Name] = MessagePackSerializer.Serialize(property.PropertyType, value);
            }

            routingCache = payload;
            SaveCount++;
        }

        public void ApplyNetworkSync(SHCDESE.ViewModels.LobbyModSettingsBaseViewModel viewModel, Action action)
        {
            _isProcessingNetworkSync = true;
            viewModel.BeginAuthorisedUpdate();
            try
            {
                if (_isProcessingNetworkSync)
                    action();
            }
            finally
            {
                viewModel.EndAuthorisedUpdate();
                _isProcessingNetworkSync = false;
            }
        }
        public sealed class Registration
        {
            public object ViewModel { get; set; }
            public object View { get; set; }
        }
    }
}

#pragma warning disable 0649
internal sealed class Director
{
    public static Director instance;
    public bool MultiplayerGame;
    public bool SkirmishModeGame;
}

internal sealed class GameData
{
    public static GameData Instance;
    public int game_type = -1;
    public int SkirmishGameType = -1;
    public int SkirmishTrailType = -1;
    public int coopTrailID = -1;
    public Enums.GameModes mapType = Enums.GameModes.BUILD;
    public bool IsSandsOfTime() =>
        game_type == (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER &&
        SkirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_TRAIL &&
        SkirmishTrailType >= (int)Shared.GameTrailType.SandsOne &&
        SkirmishTrailType <= (int)Shared.GameTrailType.SandsEight;
}

internal static class Enums
{
    internal enum eGameTypeModes
    {
        GAMETYPE_CAMPAIGN = 0,
        GAMETYPE_BUILDER = 1,
        GAMETYPE_MAP = 2,
        GAMETYPE_MULTIPLAYER = 3,
        GAMETYPE_TUTORIAL = 4,
    }

    internal enum eSkirmishGameMode
    {
        SKIRMISH_GAME_NOT_SKIRMISH = -1,
        SKIRMISH_GAME_CUSTOM = 0,
        SKIRMISH_GAME_TRAIL = 1,
        SKIRMISH_GAME_CUSTOM_TRAIL = 2,
        SKIRMISH_GAME_TEST_MISSION = 3,
    }

    internal enum GameModes
    {
        BUILD = 0,
        ECO = 1,
        SIEGE = 2,
        INVASION = 3,
        MAP_EDITOR = 10,
    }
}

public sealed class Platform_Multiplayer
{
    public static Platform_Multiplayer instance = new Platform_Multiplayer();
    public static Platform_Multiplayer Instance => instance;
    public static bool MPGameActive;

    public MPLobby activeLobby;
    public List<MPGameMember> gameMembers;
    public bool IsHost;

    public struct TestSteamId
    {
        public ulong m_SteamID;
    }

    public sealed class MPLobby
    {
        public TestSteamId id;
        public List<MPLobbyMember> members;
    }

    public sealed class MPLobbyMember
    {
        public bool SkirmishMember;
    }

    public sealed class MPGameMember
    {
        public bool skirmishAI;
        public ulong steamID;
    }
}
#pragma warning restore 0649

namespace SHCDESE.API
{
    public sealed class GamePlayerManagerAPI
    {
        public static GamePlayerManagerAPI Instance { get; } = new GamePlayerManagerAPI();
        public bool MapEditor { get; set; }
        public bool IsInMapEditor() => MapEditor;
    }
}

namespace SHCDESE.EventAPI.MapLoader
{
    internal sealed class MapStartEventArgs
    {
        public byte bMultiplayerSave { get; set; }
        public int CampaignMapId { get; set; }
    }

    internal sealed class MapLoadEventArgs
    {
        public uint CampaignMapID { get; set; }
        public byte bMultiplayerSave { get; set; }
        public int TrailType { get; set; }
    }

    internal sealed class LoadSaveGameEventArgs
    {
        public LoadSaveGameEventArgs(bool loadingEditorMap) => LoadingEditorMap = loadingEditorMap;
        public bool LoadingEditorMap { get; }
    }
}

namespace SHCDESE.BepInEx.Bootstrap
{
    public static class Plugin { public static SHCDESE.ViewModels.LobbyModSettingsBaseViewModel ModSettingsHubViewModel { get; } = new SHCDESE.ViewModels.LobbyModSettingsBaseViewModel(); }
}

namespace CrusaderDE
{
    public sealed class MainViewModel
    {
        private static readonly MainViewModel Value = new MainViewModel();
        public static bool viewModelLoaded;
        public static int InstanceReadCount { get; private set; }
        public bool IsMapEditorMode { get; set; }

        public static MainViewModel Instance
        {
            get
            {
                InstanceReadCount++;
                if (!viewModelLoaded)
                    throw new InvalidOperationException("MainViewModel is not loaded.");
                return Value;
            }
        }

        public static void Reset()
        {
            viewModelLoaded = false;
            InstanceReadCount = 0;
            Value.IsMapEditorMode = false;
        }
    }

    public sealed class Translate
    {
        public static Translate Instance { get; } = new Translate();
        public Dictionary<string, string> GameTexts { get; } = new Dictionary<string, string>();
    }
}

namespace Shared
{
    internal static class DebugLogHelper
    {
        public static void LogInfo(BepInEx.Logging.ManualLogSource log, string text) { }
        public static void LogWarning(BepInEx.Logging.ManualLogSource log, string text) { }
        public static void LogError(BepInEx.Logging.ManualLogSource log, string text) { }
    }
}
