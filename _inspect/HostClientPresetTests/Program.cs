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
            TestGameModeHelper();
            TestLocalPerPlayerSetting();
            TestMarketGoodsOrderDefinition();
            TestMarketGoodPriceDefinition();
            TestAIMarketVanillaPricePolicy();
            TestAIMarketNativeResolution();
            TestArrayPerPlayerSetting();
            TestMarketOrderPresetRoundTrip();
            TestPresetLocalRoundTrip();
            TestDoNotPersistPresetExclusion();

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

            var hostOnly = new HostOnlyViewModel();
            hostOnly.PreparePresets(null, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HostOnly.dll"), "HostOnlyTest");
            GameNetworkAPI.LocalHost = false;
            hostOnly.System_RefreshSettingsAccess();
            Check(!hostOnly.CanChangePreset, "pure host mod exposed a functional client preset");
            Check(hostOnly.HostReadOnlyNoticeVisibility == Noesis.Visibility.Visible, "pure host mod omitted its read-only notice");
            Check(hostOnly.ActionsScopeNoticeVisibility == Noesis.Visibility.Collapsed, "pure host mod displayed a client action-scope notice");

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

    private static void TestGameModeHelper()
    {
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

        GameData.Instance = new GameData();
        platform.activeLobby = new Platform_Multiplayer.MPLobby
        {
            members = new List<Platform_Multiplayer.MPLobbyMember>
            {
                new Platform_Multiplayer.MPLobbyMember { SkirmishMember = false }
            }
        };
        GameModeSnapshot lobby = GameModeHelper.Capture();
        Check(lobby.IsRealMultiplayer && !lobby.PlatformMultiplayer,
            "pre-start lobby required the active-game signal");

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

    private static void CopyAt(byte[] destination, int offset, byte[] source) =>
        Array.Copy(source, 0, destination, offset, source.Length);

    private static void WriteInt32(byte[] destination, int offset, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, destination, offset, bytes.Length);
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

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
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

        public void RegisterLobbyModSettings(global::BepInEx.BaseUnityPlugin plugin, string name, object vm, string xaml)
        {
            RegisteredModSettings.Add(new Registration { ViewModel = vm });
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
        public sealed class Registration { public object ViewModel { get; set; } }
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

internal sealed class Platform_Multiplayer
{
    public static Platform_Multiplayer instance = new Platform_Multiplayer();
    public static Platform_Multiplayer Instance => instance;
    public static bool MPGameActive;

    public MPLobby activeLobby;
    public List<MPGameMember> gameMembers;
    public bool IsHost;

    internal sealed class MPLobby
    {
        public List<MPLobbyMember> members;
    }

    internal sealed class MPLobbyMember
    {
        public bool SkirmishMember;
    }

    internal sealed class MPGameMember
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
        public bool IsInMapEditor() => false;
    }
}

namespace SHCDESE.BepInEx.Bootstrap
{
    public static class Plugin { public static SHCDESE.ViewModels.LobbyModSettingsBaseViewModel ModSettingsHubViewModel { get; } = new SHCDESE.ViewModels.LobbyModSettingsBaseViewModel(); }
}

namespace CrusaderDE
{
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
