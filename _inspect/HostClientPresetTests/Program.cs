using MessagePack;
using BugfixesAndQoL;
using ExtraFeatures;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
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
            TestLocalPerPlayerSetting();
            TestMarketGoodsOrderDefinition();
            TestResyncHostKickPolicy();
            TestSelectedUnitHealthSummary();
            TestSurrenderAndStatisticsSettingAndPolicy();
            TestMultiplayerLobbyReturnPolicy();
            TestMarketGoodPriceDefinition();
            TestAIMarketVanillaPricePolicy();
            TestAssassinClimbCancellationPolicy();
            TestAssassinClimbCostPolicy();
            TestTroopActionButtonLayoutPolicy();
            TestLordHealthMultiplierPolicy();
            TestQuarryPileTargetSelectionPolicy();
            TestTemporaryGateBlockagePolicy();
            TestGatehouseAutomationSaveState();
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
            TestFreeCastleProtocol();

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
        GameData.Instance = new GameData { game_type = 3, SkirmishGameType = 0 };

        GameModeSnapshot skirmish = GameModeHelper.Capture();
        Check(skirmish.LowLevelNetworked && !skirmish.IsRealMultiplayer &&
              skirmish.IsSingleplayerSkirmish,
            "local skirmish was misclassified as multiplayer");

        GameData.Instance.SkirmishGameType = 1;
        GameModeSnapshot trail = GameModeHelper.Capture();
        Check(!trail.IsRealMultiplayer && trail.IsSingleplayerTrail,
            "singleplayer Trail was not recognized");

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

    private static void TestSurrenderAndStatisticsSettingAndPolicy()
    {
        var validLord = new SurrenderLordSnapshot(2, 120, 8120, 2, true);
        var missingLord = new SurrenderLordSnapshot(2, -1, -1, -1, false);
        var deadLord = new SurrenderLordSnapshot(2, 120, 8120, 2, false);
        var foreignLord = new SurrenderLordSnapshot(2, 120, 8120, 3, true);

        Check(LordUnitControlsPolicy.CanActivate(
                true, true, true, false, false, 1, 120, 2, validLord),
            "compact Lord HUD rejected the sole selected local Lord");
        Check(!LordUnitControlsPolicy.CanActivate(
                true, true, true, false, false, 2, 120, 2, validLord),
            "compact Lord HUD accepted a mixed selection");
        Check(!LordUnitControlsPolicy.CanActivate(
                true, true, true, false, false, 1, 121, 2, validLord),
            "compact Lord HUD accepted a non-Lord selected unit");
        Check(!LordUnitControlsPolicy.CanActivate(
                true, true, true, false, false, 1, 120, 3, validLord),
            "compact Lord HUD accepted another player's Lord");
        Check(!LordUnitControlsPolicy.CanActivate(
                true, true, true, false, true, 1, 120, 2, validLord),
            "compact Lord HUD appeared for a spectator");
        Check(LordUnitControlsPolicy.CanActivate(
                true, true, false, true, false, 1, 120, 2, validLord),
            "compact Lord HUD rejected the controlled player's Lord in the map editor");
        Check(!LordUnitControlsPolicy.CanActivate(
                true, true, false, true, false, 1, 120, 3, validLord),
            "compact Lord HUD accepted another player's Lord in the map editor");
        Check(!LordUnitControlsPolicy.CanActivate(
                true, true, true, false, false, 1, 120, 2, deadLord),
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

        Check(SurrenderPolicy.CanExecute(1, 1, 2, 11, 8120, false, validLord),
            "valid surrender execution was rejected");
        Check(!SurrenderPolicy.CanExecute(1, 1, 3, 11, 8120, false, validLord),
            "forged player ID was accepted");
        Check(!SurrenderPolicy.CanExecute(1, 1, 2, 11, 9999, false, validLord),
            "foreign global lord ID was accepted");
        Check(!SurrenderPolicy.CanExecute(1, 1, 2, 11, 8120, true, validLord),
            "duplicate surrender operation was accepted");

        var request = new SurrenderRequestPacket { ProtocolVersion = 1, RequestId = 17 };
        SurrenderRequestPacket requestRoundTrip = MessagePackSerializer.Deserialize<SurrenderRequestPacket>(
            MessagePackSerializer.Serialize(request));
        Check(requestRoundTrip.ProtocolVersion == 1 && requestRoundTrip.RequestId == 17,
            "surrender request packet did not round-trip");
        Check(typeof(SurrenderRequestPacket).GetFields().All(field => field.Name != "PlayerId"),
            "client surrender request contains a target player ID");

        var execution = new SurrenderExecutionPacket
        {
            ProtocolVersion = 1,
            PlayerId = 2,
            OperationId = 11,
            LordGlobalId = 8120
        };
        SurrenderExecutionPacket executionRoundTrip = MessagePackSerializer.Deserialize<SurrenderExecutionPacket>(
            MessagePackSerializer.Serialize(execution));
        Check(executionRoundTrip.ProtocolVersion == 1 &&
              executionRoundTrip.PlayerId == 2 &&
              executionRoundTrip.OperationId == 11 &&
              executionRoundTrip.LordGlobalId == 8120,
            "surrender execution packet did not round-trip");
        byte[] observedSurrenderBody = MessagePackSerializer.Serialize(new SurrenderExecutionPacket
        {
            ProtocolVersion = 1,
            PlayerId = 1,
            OperationId = 1,
            LordGlobalId = 3935
        });
        Check(observedSurrenderBody.SequenceEqual(new byte[] { 0x94, 0x01, 0x01, 0x01, 0xCD, 0x0F, 0x5F }),
            "surrender execution body no longer matches the observed seven-byte canonical payload");

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
        Check(setting.EnableReturnToMultiplayerLobby,
            "EnableReturnToMultiplayerLobby did not default to true");
        Check(typeof(SurrenderAndStatisticsSettingViewModel).GetProperty("EnableSurrender") == null,
            "obsolete EnableSurrender property remains present");
        setting.EnableAiFixes = false;
        setting.EnableSurrenderAndStatistics = false;
        setting.EnableLordUnitControls = false;
        setting.EnableEliminatedPlayersBecomeSpectators = false;
        setting.EnableReturnToMultiplayerLobby = false;
        setting.SelectedPreset = 1;
        Check(setting.EnableAiFixes, "new shared preset did not retain the EnableAiFixes default true value");
        Check(setting.EnableSurrenderAndStatistics, "new shared preset did not retain the default true value");
        Check(setting.EnableLordUnitControls, "new shared preset did not retain the Lord-controls default true value");
        Check(setting.EnableEliminatedPlayersBecomeSpectators,
            "new shared preset did not retain the spectator-promotion default true value");
        Check(setting.EnableReturnToMultiplayerLobby,
            "new shared preset did not retain the lobby-return default true value");
        setting.SelectedPreset = 0;
        Check(!setting.EnableAiFixes, "EnableAiFixes did not round-trip through presets");
        Check(!setting.EnableSurrenderAndStatistics, "shared host value did not round-trip through presets");
        Check(!setting.EnableLordUnitControls, "Lord-controls host value did not round-trip through presets");
        Check(!setting.EnableEliminatedPlayersBecomeSpectators,
            "spectator-promotion host value did not round-trip through presets");
        Check(!setting.EnableReturnToMultiplayerLobby,
            "lobby-return host value did not round-trip through presets");

        GameNetworkAPI.LocalHost = false;
        setting.System_RefreshSettingsAccess();
        byte[] beforeClientMutation = File.ReadAllBytes(settingsPath);
        setting.EnableAiFixes = true;
        setting.EnableSurrenderAndStatistics = true;
        setting.EnableLordUnitControls = true;
        setting.EnableEliminatedPlayersBecomeSpectators = true;
        setting.EnableReturnToMultiplayerLobby = true;
        Check(!setting.EnableAiFixes, "client mutated the host-only EnableAiFixes setting");
        Check(beforeClientMutation.SequenceEqual(File.ReadAllBytes(settingsPath)),
            "client EnableAiFixes mutation changed the local preset file");
        Check(!setting.EnableSurrenderAndStatistics, "client mutated the host-only EnableSurrenderAndStatistics setting");
        Check(!setting.EnableLordUnitControls, "client mutated the host-only EnableLordUnitControls setting");
        Check(!setting.EnableEliminatedPlayersBecomeSpectators,
            "client mutated the host-only EnableEliminatedPlayersBecomeSpectators setting");
        Check(!setting.EnableReturnToMultiplayerLobby,
            "client mutated the host-only EnableReturnToMultiplayerLobby setting");
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(setting, () =>
        {
            setting.EnableAiFixes = true;
            setting.EnableSurrenderAndStatistics = true;
            setting.EnableLordUnitControls = true;
            setting.EnableEliminatedPlayersBecomeSpectators = true;
            setting.EnableReturnToMultiplayerLobby = true;
        });
        Check(setting.EnableAiFixes, "authoritative host sync did not update EnableAiFixes");
        Check(setting.EnableSurrenderAndStatistics, "authoritative host sync did not update EnableSurrenderAndStatistics");
        Check(setting.EnableLordUnitControls, "authoritative host sync did not update EnableLordUnitControls");
        Check(setting.EnableEliminatedPlayersBecomeSpectators,
            "authoritative host sync did not update EnableEliminatedPlayersBecomeSpectators");
        Check(setting.EnableReturnToMultiplayerLobby,
            "authoritative host sync did not update EnableReturnToMultiplayerLobby");

        setting.System_EnterMissionPreset(
            new Dictionary<string, byte[]>
            {
                [nameof(setting.EnableAiFixes)] = MessagePackSerializer.Serialize(false),
                [nameof(setting.EnableSurrenderAndStatistics)] = MessagePackSerializer.Serialize(false),
                [nameof(setting.EnableLordUnitControls)] = MessagePackSerializer.Serialize(false),
                [nameof(setting.EnableEliminatedPlayersBecomeSpectators)] = MessagePackSerializer.Serialize(false),
                [nameof(setting.EnableReturnToMultiplayerLobby)] = MessagePackSerializer.Serialize(false)
            },
            "Trail",
            editable: false);
        Check(!setting.EnableAiFixes, "read-only Trail did not apply EnableAiFixes");
        Check(!setting.EnableSurrenderAndStatistics && !setting.CanEditHostSettings,
            "read-only Trail did not apply and lock EnableSurrenderAndStatistics");
        Check(!setting.EnableLordUnitControls, "read-only Trail did not apply EnableLordUnitControls");
        Check(!setting.EnableEliminatedPlayersBecomeSpectators,
            "read-only Trail did not apply EnableEliminatedPlayersBecomeSpectators");
        Check(!setting.EnableReturnToMultiplayerLobby,
            "read-only Trail did not apply EnableReturnToMultiplayerLobby");
        setting.EnableAiFixes = true;
        setting.EnableSurrenderAndStatistics = true;
        setting.EnableLordUnitControls = true;
        setting.EnableEliminatedPlayersBecomeSpectators = true;
        setting.EnableReturnToMultiplayerLobby = true;
        Check(!setting.EnableAiFixes, "client changed EnableAiFixes inside a read-only Trail");
        Check(!setting.EnableSurrenderAndStatistics, "client changed EnableSurrenderAndStatistics inside a read-only Trail");
        Check(!setting.EnableLordUnitControls, "client changed EnableLordUnitControls inside a read-only Trail");
        Check(!setting.EnableEliminatedPlayersBecomeSpectators,
            "client changed EnableEliminatedPlayersBecomeSpectators inside a read-only Trail");
        Check(!setting.EnableReturnToMultiplayerLobby,
            "client changed EnableReturnToMultiplayerLobby inside a read-only Trail");
        setting.System_ExitMissionPreset();

        GameNetworkAPI.LocalHost = true;
        setting.System_RefreshSettingsAccess();
        setting.ResetSurrenderAndStatistics();
        Check(setting.EnableAiFixes, "EnableAiFixes reset value was not true");
        Check(setting.EnableSurrenderAndStatistics, "EnableSurrenderAndStatistics reset value was not true");
        Check(setting.EnableLordUnitControls, "EnableLordUnitControls reset value was not true");
        Check(setting.EnableEliminatedPlayersBecomeSpectators,
            "EnableEliminatedPlayersBecomeSpectators reset value was not true");
        Check(setting.EnableReturnToMultiplayerLobby,
            "EnableReturnToMultiplayerLobby reset value was not true");
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

    private static void TestAssassinClimbCostPolicy()
    {
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

    private static void TestAssassinClimbCancellationPolicy()
    {
        Check(AssassinClimbCancellationPolicy.ShouldHandleCommand(
                true, true, true, AssassinClimbCancellationPolicy.UnitStopCommand),
            "Assassin climb cancellation rejected Vanilla's synchronized UnitStop command");
        Check(!AssassinClimbCancellationPolicy.ShouldHandleCommand(
                false, true, true, AssassinClimbCancellationPolicy.UnitStopCommand) &&
              !AssassinClimbCancellationPolicy.ShouldHandleCommand(
                true, false, true, AssassinClimbCancellationPolicy.UnitStopCommand) &&
              !AssassinClimbCancellationPolicy.ShouldHandleCommand(
                true, true, false, AssassinClimbCancellationPolicy.UnitStopCommand) &&
              !AssassinClimbCancellationPolicy.ShouldHandleCommand(
                true, true, true, AssassinClimbCancellationPolicy.UnitStopCommand + 1),
            "Assassin climb cancellation did not fail closed outside the enabled synchronized UnitStop path");
        Check(AssassinClimbCancellationPolicy.IsClimbingState(126) &&
              AssassinClimbCancellationPolicy.IsClimbingState(127) &&
              AssassinClimbCancellationPolicy.IsClimbingState(128) &&
              AssassinClimbCancellationPolicy.IsClimbingState(129) &&
              !AssassinClimbCancellationPolicy.IsClimbingState(125) &&
              !AssassinClimbCancellationPolicy.IsClimbingState(130),
            "Assassin climb-stop state filter does not cover exactly states 126 through 129");
        Check(!AssassinClimbCancellationPolicy.UsesPreviousTileForRollback(126) &&
              !AssassinClimbCancellationPolicy.UsesPreviousTileForRollback(127) &&
              !AssassinClimbCancellationPolicy.UsesPreviousTileForRollback(128) &&
              AssassinClimbCancellationPolicy.UsesPreviousTileForRollback(129),
            "Assassin climb-stop rollback did not select Previous exclusively for active descent");
    }

    private static void TestTroopActionButtonLayoutPolicy()
    {
        Check(!TroopActionButtonLayoutPolicy.IsEffectivelyOccupied(false) &&
              TroopActionButtonLayoutPolicy.IsEffectivelyOccupied(true),
            "shared troop action collision policy did not give every displayed Vanilla/foreign button priority");

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
        var assassin = new TroopActionRequest("ExtraFeatures_Serp.AssassinClimb", 200, true);
        TroopActionLayoutDecision bothFree = TroopActionButtonLayoutPolicy.CreateDecision(
            new[] { assassin, knight }, false, false);
        Check(bothFree.Assignments.Select(value => $"{value.ActionId}:{value.Slot}").SequenceEqual(new[]
        {
            "ExtraFeatures_Serp.KnightTransform:BottomRight",
            "ExtraFeatures_Serp.AssassinClimb:BottomMiddle"
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
            TargetSpeed = 70
        };
        byte[] serialized = MessagePackSerializer.Serialize(packet);
        MultiplayerGameSpeedChangePacket roundTrip =
            MessagePackSerializer.Deserialize<MultiplayerGameSpeedChangePacket>(serialized);
        Check(roundTrip.ProtocolVersion == packet.ProtocolVersion &&
            roundTrip.Action == packet.Action &&
            roundTrip.TargetSpeed == packet.TargetSpeed,
            "multiplayer game-speed packet did not round-trip");

        byte[] forwardBuffer = MessagePackSerializer.Serialize(new FutureMultiplayerGameSpeedPacket
        {
            ProtocolVersion = MultiplayerGameSpeedPolicy.ProtocolVersion,
            Action = MultiplayerGameSpeedPolicy.SetAction,
            TargetSpeed = 75,
            FutureField = "future-field"
        });
        MultiplayerGameSpeedChangePacket forwardPacket =
            MessagePackSerializer.Deserialize<MultiplayerGameSpeedChangePacket>(forwardBuffer);
        Check(forwardPacket.ProtocolVersion == MultiplayerGameSpeedPolicy.ProtocolVersion &&
            forwardPacket.Action == MultiplayerGameSpeedPolicy.SetAction &&
            forwardPacket.TargetSpeed == 75,
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
        var selections = new[]
        {
            new CastlePlanner.FreeCastleSelection
            {
                PlayerId = 2,
                Rotation = 6,
                DisplayName = "Second Castle",
                RawData = new short[] { 4, -2, 9 }
            },
            new CastlePlanner.FreeCastleSelection
            {
                PlayerId = 1,
                Rotation = 0,
                DisplayName = "First Castle",
                RawData = new short[] { 1, 2, 3 }
            }
        };
        byte[] encoded = CastlePlanner.FreeCastleProtocol.EncodeSelections(selections);
        byte[] compressed = CastlePlanner.FreeCastleProtocol.Compress(encoded);
        byte[] restored = CastlePlanner.FreeCastleProtocol.Decompress(compressed, encoded.Length);
        List<CastlePlanner.FreeCastleSelection> decoded =
            CastlePlanner.FreeCastleProtocol.DecodeSelections(restored);
        Check(decoded.Count == 2 && decoded[0].PlayerId == 1 && decoded[1].Rotation == 6,
            "free-castle canonical transfer did not preserve player order and fixed rotation");
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
    [Key(3)] public string FutureField;
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

internal sealed class SurrenderAndStatisticsSettingViewModel : PresetLobbyModSettingsViewModel
{
    private bool enableAiFixes = true;
    private bool enableSurrenderAndStatistics = true;
    private bool enableLordUnitControls = true;
    private bool enableEliminatedPlayersBecomeSpectators = true;
    private bool enableReturnToMultiplayerLobby = true;

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

    internal void ResetSurrenderAndStatistics()
    {
        EnableAiFixes = true;
        EnableSurrenderAndStatistics = true;
        EnableLordUnitControls = true;
        EnableEliminatedPlayersBecomeSpectators = true;
        EnableReturnToMultiplayerLobby = true;
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
    public int coopTrailID = -1;
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
