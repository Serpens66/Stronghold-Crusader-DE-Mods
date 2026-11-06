using APIShared;
using APIShared.GameModes;
using APIShared.ModSettings;
using ExtraFeatures;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// External native-service boundary only. The production adapter and the real
// settings/preset/lifecycle sources run unchanged in this offline fixture.
namespace APIShared.Economy
{
    public sealed class WorkerProductionBonusOverrideRegistration
    {
        public int? Percent { get; private set; }
        public void SetPercent(int? percent) { Percent = percent; }
    }

    public static class WorkerProductionBonusOverrides
    {
        public static bool Available = true;
        public static int Registrations;
        public static WorkerProductionBonusOverrideRegistration Last;
        public static string CompatibilityKey => Available ? "worker-production-bonus/v1/fixture" : string.Empty;
        public static bool TryRegister(string ownerGuid, string registrationId,
            out WorkerProductionBonusOverrideRegistration registration, out string diagnostic, int order = 0)
        {
            Registrations++;
            registration = Available ? new WorkerProductionBonusOverrideRegistration() : null;
            Last = registration;
            diagnostic = Available ? string.Empty : "fixture unavailable";
            return Available;
        }
    }
}

namespace APIShared.Lobby
{
    public sealed class LobbyLaunchGuardContext { public bool RequiresPeerAgreement { get; set; } = true; }
    public static class LobbyLaunchStatus { public static void RequestRefresh() { } }
    public static class LobbyLaunchGuards
    {
        public static bool Available = true;
        public static Func<LobbyLaunchGuardContext, string> Last;
        public static bool TryRegister(string ownerGuid, string registrationId,
            Func<LobbyLaunchGuardContext, string> guard, out string diagnostic, int order = 0)
        {
            if (Available) Last = guard;
            diagnostic = Available ? string.Empty : "fixture launch admission unavailable";
            return Available;
        }
    }
}

internal static class WorkerProductionBonusTests
{
    private static string Report(string key, bool publish = true) => ExtraFeaturesViewModel.CreateWorkerProductionBonusReport(key, publish);
    private static void SetPeerReport(ExtraFeaturesViewModel settings, int slot, string report)
    {
        settings.WorkerProductionBonusCompatibilityKeyData[slot] = report;
        settings.System_TriggerUpdate(nameof(settings.WorkerProductionBonusCompatibilityKeyData));
    }
    private static GameModeSnapshot Mode(GameModeKind kind, bool multiplayer = false, int humans = 2) => new GameModeSnapshot(
        multiplayer, !multiplayer && kind == GameModeKind.CustomGame, !multiplayer && kind == GameModeKind.CustomGame, false,
        kind == GameModeKind.MapEditor, false, true, multiplayer, false, multiplayer, multiplayer,
        multiplayer ? humans : 0, multiplayer ? humans : 0, 0, multiplayer ? humans : 0, multiplayer ? humans : 0,
        0, 0, -1, 0, kind, GameModeLaunchVariant.Standard, 0, -1, -1, -1, 0, false);

    private static void Publish(MissionContext context, MissionLifecycleKind kind, bool replay = false,
        MissionInitializationPhase phase = MissionInitializationPhase.BeforeLoad) =>
        Shared.MissionEvents.Publish(new MissionLifecycleNotification(context, kind,
            kind == MissionLifecycleKind.Initialization ? phase : MissionInitializationPhase.ManagedReady,
            kind == MissionLifecycleKind.End ? MissionEndReason.Unloaded : MissionEndReason.None,
            kind != MissionLifecycleKind.Initialization, replay));

    internal static void Run(Action<bool, string> check)
    {
        Settings(check);
        var settings = new ExtraFeaturesViewModel(false);
        settings.PreparePresets(null, Path.Combine(TestWorkDirectory.New("ProductionBonusRuntime"), "ExtraFeatures.dll"), "ProductionBonusRuntime");
        settings.ActivatePresets();
        settings.PreparePerPlayerLobbySettings(null, "ProductionBonusRuntime", ExtraFeaturesPlugin.PluginGuid, false);
        settings.ActivatePerPlayerLobbySettings();
        settings.FixedProductionBonusPercent = -50;
        var runtime = new WorkerProductionBonusRuntime(null, settings);
        int registrations = APIShared.Economy.WorkerProductionBonusOverrides.Registrations;
        runtime.Register(); runtime.Register();
        var native = APIShared.Economy.WorkerProductionBonusOverrides.Last;
        check(APIShared.Economy.WorkerProductionBonusOverrides.Registrations == registrations + 1 &&
            native.Percent == null, "Production bonus registration is process-lived and initially inactive");
        check(settings.WorkerProductionBonusCompatibilityKey == Report(APIShared.Economy.WorkerProductionBonusOverrides.CompatibilityKey),
            "Local report publishes native history support separately from consumer publication readiness");
        long id = 19000;
        Func<MissionStartKind, GameModeKind, MissionContext> begin = (kind, mode) =>
        {
            var context = new MissionContext(++id, kind, Mode(mode), null, "production bonus fixture", false);
            Publish(context, MissionLifecycleKind.Initialization);
            check(native.Percent == (mode == GameModeKind.CustomGame || mode == GameModeKind.MapEditor ? (int?)settings.FixedProductionBonusPercent : null),
                "Production bonus is captured before native initialization and respects the mode gate");
            check(!settings.CanConfigureFixedProductionBonus, "Production input locks before native initialization");
            Publish(context, MissionLifecycleKind.Start);
            return context;
        };
        var map = begin(MissionStartKind.NewGame, GameModeKind.CustomGame);
        check(native.Percent == -50 && !settings.CanConfigureFixedProductionBonus,
            "Custom mission captures the negative percent and locks the input");
        settings.FixedProductionBonusPercent = 25;
        settings.FixedProductionBonusValueText = "100";
        check(settings.FixedProductionBonusPercent == -50 && native.Percent == -50,
            "Both numeric and text edits are rejected during a mission");
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(settings, () => settings.FixedProductionBonusPercent = -1);
        check(settings.FixedProductionBonusPercent == -50 && native.Percent == -50,
            "A live network write cannot change a production setting during the mission");
        Publish(map, MissionLifecycleKind.Start, replay: true);
        check(native.Percent == -50, "Lifecycle replay does not recapture a running bonus");
        settings.EnableMod = false;
        check(native.Percent == null, "Disabling ExtraFeatures removes only its production contribution");
        settings.EnableMod = true;
        check(native.Percent == -50, "Reactivation keeps the frozen mission percent");
        Publish(map, MissionLifecycleKind.End);
        check(native.Percent == null && settings.CanConfigureFixedProductionBonus,
            "Mission end removes the contribution and unlocks settings");
        foreach (var mode in new[] { GameModeKind.Campaign, GameModeKind.StandaloneMission, GameModeKind.Unknown })
        {
            map = begin(MissionStartKind.NewGame, mode);
            check(native.Percent == null, "Disallowed mode retains Vanilla production: " + mode);
            Publish(map, MissionLifecycleKind.End);
        }
        settings.FixedProductionBonusPercent = -100;
        map = begin(MissionStartKind.LoadedSave, GameModeKind.MapEditor);
        check(native.Percent == -100, "Editor/save lifecycle applies the selected policy through the existing mode gate");
        Publish(map, MissionLifecycleKind.End);

        settings.FixedProductionBonusPercent = -75;
        map = new MissionContext(++id, MissionStartKind.LoadedSave, Mode(GameModeKind.Unknown), null, "production save load fixture", false);
        Publish(map, MissionLifecycleKind.Initialization);
        check(native.Percent == null && !settings.CanConfigureFixedProductionBonus,
            "A save captures its prepared setting while unknown early mode evidence remains closed");
        var knownSave = new MissionContext(id, MissionStartKind.LoadedSave, Mode(GameModeKind.CustomGame), null, "production save load fixture", false);
        Publish(knownSave, MissionLifecycleKind.Initialization, phase: MissionInitializationPhase.NativeLoaded);
        check(native.Percent == -75,
            "NativeLoaded publishes the captured save policy before postLoading can start the independent simulation timer");
        var lateSnapshot = settings.System_CreateModDefaultSnapshot();
        lateSnapshot[nameof(settings.FixedProductionBonusPercent)] = MessagePack.MessagePackSerializer.Serialize(25);
        settings.System_ApplyWorkingSnapshot(lateSnapshot);
        Publish(knownSave, MissionLifecycleKind.Start);
        check(native.Percent == -75 && settings.FixedProductionBonusPercent == -75,
            "A local working snapshot cannot bypass the mission lock or make a later save record another percentage");
        lateSnapshot[nameof(settings.EnableMod)] = MessagePack.MessagePackSerializer.Serialize(false);
        settings.System_ApplyWorkingSnapshot(lateSnapshot);
        check(native.Percent == null, "A trusted late EnableMod=false snapshot immediately withdraws the captured bonus");
        lateSnapshot[nameof(settings.EnableMod)] = MessagePack.MessagePackSerializer.Serialize(true);
        settings.System_ApplyWorkingSnapshot(lateSnapshot);
        check(native.Percent == -75, "A trusted late EnableMod=true snapshot reapplies the captured percent with current admission");
        Publish(knownSave, MissionLifecycleKind.End);
        settings.FixedProductionBonusPercent = -100;

        settings.WorkerProductionBonusCompatibilityKey = Report("same");
        SetPeerReport(settings, 1, Report("same"));
        check(!settings.TryGetWorkerProductionBonusPeerReadiness(out _), "A local report alone cannot admit real multiplayer");
        var guard = APIShared.Lobby.LobbyLaunchGuards.Last;
        check(guard(new APIShared.Lobby.LobbyLaunchGuardContext { RequiresPeerAgreement = false }) == null,
            "Solo-skirmish Play is not blocked by multiplayer peer readiness");
        // Canonical Vanilla leaves MPGameActive/Director.MultiplayerGame false
        // until after the lobby has already dispatched Ready and Play.
        GameNetworkAPI.MultiplayerGame = false;
        check(!string.IsNullOrEmpty(guard(new APIShared.Lobby.LobbyLaunchGuardContext())),
            "A fresh real lobby requires peer reports while in-game multiplayer flags are still false");
        var soloRoster = new Dictionary<int, ulong> { [1] = 1001 };
        settings.System_TestObservePerPlayerLobby(701, soloRoster, false, 1);
        check(settings.TryGetWorkerProductionBonusPeerReadiness(out _),
            "A centrally validated one-human roster requires exactly its one authenticated report");
        settings.System_TestObservePerPlayerLobby(701, soloRoster, true, 1);
        check(!settings.TryGetWorkerProductionBonusPeerReadiness(out _),
            "An unresolved coordinator snapshot cannot admit a seemingly complete solo report");
        settings.System_TestObservePerPlayerLobby(701, soloRoster, false, 1);
        settings.System_TestObservePerPlayerLobby(null, null, false, 0, preserveForMapTransition: true);
        check(settings.TryGetWorkerProductionBonusPeerReadiness(out _),
            "The coordinator's map-transition preservation keeps the known solo roster and report");
        map = new MissionContext(++id, MissionStartKind.LoadedSave, Mode(GameModeKind.CustomGame, true, 1), null,
            "production solo MP-save fixture", false);
        Publish(map, MissionLifecycleKind.Initialization); Publish(map, MissionLifecycleKind.Start);
        check(native.Percent == -100,
            "An admitted solo multiplayer save applies the requested percent despite authoritative real-MP mode");
        Publish(map, MissionLifecycleKind.End);
        settings.System_TestObservePerPlayerLobby(null, null, false, 0);
        check(!settings.TryGetWorkerProductionBonusPeerReadiness(out _),
            "An empty coordinator snapshot retires the expected roster rather than retaining solo admission");
        var twoPlayerRoster = new Dictionary<int, ulong> { [1] = 1001, [2] = 1002 };
        settings.System_TestObservePerPlayerLobby(702, twoPlayerRoster, false, 1);
        check(!settings.TryGetWorkerProductionBonusPeerReadiness(out _),
            "A centrally validated two-human roster still waits for the missing second report");
        foreach (string report in new[] { null, "other", "same" })
        {
            SetPeerReport(settings, 2, report == null ? null : Report(report));
            var unknownSave = new MissionContext(++id, MissionStartKind.LoadedSave, Mode(GameModeKind.Unknown), null,
                "production early MP admission fixture", false);
            Publish(unknownSave, MissionLifecycleKind.Initialization);
            check(native.Percent == null, "Unknown early save evidence cannot grant single-player admission");
            var realMpSave = new MissionContext(id, MissionStartKind.LoadedSave, Mode(GameModeKind.CustomGame, true), null,
                "production early MP admission fixture", false);
            Publish(realMpSave, MissionLifecycleKind.Initialization, phase: MissionInitializationPhase.NativeLoaded);
            check(native.Percent == (report == "same" ? (int?)-100 : null),
                "NativeLoaded rechecks real-MP admission with missing, mismatched or matching authenticated reports");
            SetPeerReport(settings, 2, Report("other"));
            Publish(realMpSave, MissionLifecycleKind.Start);
            check(native.Percent == null, "Managed Start rechecks peer admission without recapturing the percent");
            Publish(realMpSave, MissionLifecycleKind.End);
        }
        SetPeerReport(settings, 2, Report("same"));
        check(settings.TryGetWorkerProductionBonusPeerReadiness(out _), "Matching authenticated report slots pass the domain comparison");
        check(guard(new APIShared.Lobby.LobbyLaunchGuardContext()) == null,
            "Launch guard admits matching peer support before the mission mode gate opens");
        map = new MissionContext(++id, MissionStartKind.NewGame, Mode(GameModeKind.CustomGame, true), null, "production MP fixture", false);
        Publish(map, MissionLifecycleKind.Initialization); Publish(map, MissionLifecycleKind.Start);
        check(native.Percent == -100, "Real-MP mode snapshot activates the frozen bonus only with matching peer reports");
        Publish(map, MissionLifecycleKind.End);
        SetPeerReport(settings, 2, Report("other"));
        check(!settings.TryGetWorkerProductionBonusPeerReadiness(out _), "Different native support cannot admit the feature");
        check(!string.IsNullOrEmpty(guard(new APIShared.Lobby.LobbyLaunchGuardContext())),
            "Launch guard rejects mismatched native support with a player-facing reason");
        map = new MissionContext(++id, MissionStartKind.LoadedSave, Mode(GameModeKind.CustomGame, true), null, "production MP mismatch fixture", false);
        Publish(map, MissionLifecycleKind.Initialization); Publish(map, MissionLifecycleKind.Start);
        check(native.Percent == null, "Mismatched MP startup never publishes a peer-local non-Vanilla bonus");
        Publish(map, MissionLifecycleKind.End);
        SetPeerReport(settings, 2, Report(string.Empty, false));
        check(!settings.TryGetWorkerProductionBonusPeerReadiness(out _), "Explicit unavailable report cannot admit the feature");
        settings.FixedProductionBonusPercent = 50;
        check(!string.IsNullOrEmpty(guard(new APIShared.Lobby.LobbyLaunchGuardContext())),
            "Vanilla production rejects different historical native protection on peers");
        SetPeerReport(settings, 2, Report("same", false));
        check(guard(new APIShared.Lobby.LobbyLaunchGuardContext()) == null,
            "Vanilla production admits matching native history guards without requiring every consumer to publish");
        SetPeerReport(settings, 2, null);
        check(!string.IsNullOrEmpty(guard(new APIShared.Lobby.LobbyLaunchGuardContext())),
            "Vanilla production still waits for every peer's history support report");
        SetPeerReport(settings, 2, Report("same", false));
        settings.FixedProductionBonusPercent = -100;
        check(!string.IsNullOrEmpty(guard(new APIShared.Lobby.LobbyLaunchGuardContext())),
            "Non-Vanilla output additionally requires every matching peer to publish an override");
        settings.FixedProductionBonusPercent = -100; settings.EnableMod = false;
        check(guard(new APIShared.Lobby.LobbyLaunchGuardContext()) == null,
            "Disabled ExtraFeatures admits matching native history support without consumer publication");
        SetPeerReport(settings, 2, Report(string.Empty, false));
        check(!string.IsNullOrEmpty(guard(new APIShared.Lobby.LobbyLaunchGuardContext())),
            "Disabled ExtraFeatures still rejects mixed historical native protection");
        settings.WorkerProductionBonusCompatibilityKey = Report(string.Empty, false);
        SetPeerReport(settings, 1, Report(string.Empty, false));
        check(guard(new APIShared.Lobby.LobbyLaunchGuardContext()) == null,
            "Identically unavailable history protection is admitted for disabled output overrides");
        settings.EnableMod = true; settings.FixedProductionBonusPercent = 50;
        check(guard(new APIShared.Lobby.LobbyLaunchGuardContext()) == null,
            "Identically unavailable history protection is also admitted for the neutral fifty-percent rule");
        SetPeerReport(settings, 2, string.Empty);
        check(!string.IsNullOrEmpty(guard(new APIShared.Lobby.LobbyLaunchGuardContext())),
            "An unversioned or malformed report cannot masquerade as unavailable native history support");

        APIShared.Economy.WorkerProductionBonusOverrides.Available = false;
        var missing = new ExtraFeaturesViewModel(false);
        new WorkerProductionBonusRuntime(null, missing).Register();
        check(missing.WorkerProductionBonusCompatibilityKey == Report(string.Empty, false),
            "A failed service publishes unavailable, not an unresolved or successful report");
        APIShared.Economy.WorkerProductionBonusOverrides.Available = true;
        APIShared.Lobby.LobbyLaunchGuards.Available = false;
        registrations = APIShared.Economy.WorkerProductionBonusOverrides.Registrations;
        var missingAdmission = new ExtraFeaturesViewModel(false);
        new WorkerProductionBonusRuntime(null, missingAdmission).Register();
        check(missingAdmission.WorkerProductionBonusCompatibilityKey == Report(APIShared.Economy.WorkerProductionBonusOverrides.CompatibilityKey, false) &&
            APIShared.Economy.WorkerProductionBonusOverrides.Registrations == registrations,
            "Missing launch admission prevents consumer publication while reporting the eager native history guards");
        APIShared.Lobby.LobbyLaunchGuards.Available = true;
        GameNetworkAPI.MultiplayerGame = false;
    }

    private static void Settings(Action<bool, string> check)
    {
        string path = Path.Combine(TestWorkDirectory.New("ProductionBonusPreset"), "ExtraFeatures.dll");
        Func<ExtraFeaturesViewModel> prepare = () =>
        {
            var model = new ExtraFeaturesViewModel(false);
            model.PreparePresets(null, path, "ProductionBonus"); model.ActivatePresets();
            return model;
        };
        GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
        var settings = prepare();
        check(settings.FixedProductionBonusPercent == 50, "Production bonus defaults to Vanilla 50 percent");
        settings.FixedProductionBonusValueText = "-50";
        check(settings.FixedProductionBonusPercent == -50, "Signed whole production percent accepts a penalty");
        foreach (string invalid in new[] { "1.5", "1,5", "1%", "", "-", "2147483648" })
        {
            settings.FixedProductionBonusValueText = invalid;
            check(settings.FixedProductionBonusPercent == -50, "Invalid production percent preserves the value: " + invalid);
        }
        settings.FixedProductionBonusPercent = int.MinValue;
        check(settings.FixedProductionBonusPercent == -100, "Production percent clamps at minus 100");
        settings.FixedProductionBonusPercent = int.MaxValue;
        check(settings.FixedProductionBonusPercent == 1000, "Production percent clamps at plus 1000");
        settings.FixedProductionBonusPercent = -1;
        settings.WorkerProductionBonusCompatibilityKey = "ephemeral";
        var restored = prepare();
        check(restored.FixedProductionBonusPercent == -1 && restored.WorkerProductionBonusCompatibilityKey == null,
            "Production setting survives restart while runtime compatibility reports do not persist");
        restored.System_SavePersonalPreset("production-bonus", "Production bonus", "", new[] {
            new PresetSaveSelection { PropertyName = nameof(restored.FixedProductionBonusPercent), Mode = PublishedPresetValueMode.Fixed } }, false);
        var preset = restored.System_TestPublishedPresets.Single(p => p.Id == "production-bonus");
        restored.System_LoadModDefaults();
        check(restored.FixedProductionBonusPercent == 50, "Shared defaults restore the Vanilla coefficient");
        restored.System_TestLoadPreset(preset.StableId);
        check(restored.FixedProductionBonusPercent == -1, "Named preset restores a negative production percent");
        GameNetworkAPI.LocalHost = false; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
        restored.FixedProductionBonusPercent = 100; restored.FixedProductionBonusValueText = "200";
        check(restored.FixedProductionBonusPercent == -1 && !restored.CanConfigureFixedProductionBonus,
            "Clients cannot edit the host production setting");
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(restored, () => restored.FixedProductionBonusPercent = -100);
        check(restored.FixedProductionBonusPercent == -100, "Authoritative host production percent is accepted");
        GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
        var own = prepare();
        check(own.FixedProductionBonusPercent == -1, "Received host percent does not overwrite the local preset");
        own.ResetToDefaultCommand.Execute(null);
        check(own.FixedProductionBonusPercent == 50, "ExtraFeatures reset restores the production bonus default");
        own.FixedProductionBonusPercent = -25;
        own.SetFixedProductionBonusMissionLocked(true);
        var saved = own.System_CreateModDefaultSnapshot();
        saved[nameof(own.FixedProductionBonusPercent)] = MessagePack.MessagePackSerializer.Serialize(-75);
        own.System_ApplyWorkingSnapshot(saved);
        check(own.FixedProductionBonusPercent == -25 && !own.CanConfigureFixedProductionBonus,
            "A local working snapshot cannot change a locked production setting");
        own.System_TestLoadPreset(preset.StableId);
        check(own.FixedProductionBonusPercent == -25, "A published preset cannot bypass the production mission lock");
        own.System_LoadModDefaults();
        check(own.FixedProductionBonusPercent == -25, "Loading Mod defaults cannot bypass the production mission lock");
        own.System_EnterMissionPreset(saved, "Production bonus mission restoration", false);
        check(own.FixedProductionBonusPercent == -75 && !own.CanConfigureFixedProductionBonus,
            "A genuine mission-entry snapshot restores the locked setting without unlocking its editor");
        own.System_ExitMissionPreset();
        check(own.FixedProductionBonusPercent == -25 && !own.CanConfigureFixedProductionBonus,
            "A genuine mission-exit snapshot restores the owned setting while the editor remains locked");
        own.SetFixedProductionBonusMissionLocked(false);
    }
}
