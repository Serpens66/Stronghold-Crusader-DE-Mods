using APIShared;
using APIShared.GameModes;
using APIShared.ModSettings;
using ExtraFeatures;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Engine access/event delivery are doubles. Production runtime, settings, mode
// policy and preset persistence execute unchanged; this is not a game/MP run.
internal static class PeriodicGoldTests
{
    private sealed class Player
    {
        internal int Id, Gold, Pending;
        internal bool AI, Alive = true;
    }
    private sealed class World : IPeriodicGoldWorld
    {
        internal readonly List<Player> Players = new List<Player>();
        internal int Subscriptions, Visits, Credits, Debits;
        public int CurrentTick { get; set; }
        private Action<int> tick;
        public event Action<int> Tick
        {
            add { tick += value; Subscriptions++; }
            remove { tick -= value; Subscriptions--; }
        }
        internal void Emit(int value, bool paused = false)
        {
            CurrentTick = value;
            if (!paused) tick?.Invoke(value);
        }
        public void VisitAlivePlayers(Action<int, bool, int, int> visitor)
        {
            Visits++;
            foreach (Player p in Players.Where(p => p.Alive)) visitor(p.Id, p.AI, p.Gold, p.Pending);
        }
        public void AddIncomingGold(int id, int amount) { Players.Single(p => p.Id == id).Pending += amount; Credits++; }
        public void SetGold(int id, int gold) { Players.Single(p => p.Id == id).Gold = gold; Debits++; }
    }
    private static GameModeSnapshot Mode(GameModeKind kind) => new GameModeSnapshot(
        false, kind == GameModeKind.CustomGame, kind == GameModeKind.CustomGame, false,
        kind == GameModeKind.MapEditor, false, true, false, false, false, false,
        0, 0, 0, 0, 0, 0, 0, -1, 0, kind, GameModeLaunchVariant.Standard, 0, -1, -1, -1, 0, false);
    private static void Publish(MissionContext context, MissionLifecycleKind kind, bool replay = false) =>
        Shared.MissionEvents.Publish(new MissionLifecycleNotification(context, kind,
            kind == MissionLifecycleKind.Initialization ? MissionInitializationPhase.BeforeLoad : MissionInitializationPhase.ManagedReady,
            kind == MissionLifecycleKind.End ? MissionEndReason.Unloaded : MissionEndReason.None,
            kind != MissionLifecycleKind.Initialization, replay));

    internal static void Run(Action<bool, string> check)
    {
        Settings(check);
        var settings = new ExtraFeaturesViewModel(false);
        var world = new World();
        var human = new Player { Id = 2, Gold = 1000, Pending = 7 };
        var ai = new Player { Id = 7, AI = true, Gold = 10, Pending = 23 };
        var dead = new Player { Id = 8, Gold = 999, Alive = false };
        world.Players.AddRange(new[] { human, ai, dead });
        var runtime = new PeriodicGoldRuntime(null, settings, world);
        runtime.Register(); runtime.Register();
        check(world.Subscriptions == 0, "Gold runtime has no tick handler in menus");
        long session = 9000;
        Func<int, MissionStartKind, GameModeKind, MissionContext> begin = (tick, kind, mode) =>
        {
            world.CurrentTick = tick;
            var context = new MissionContext(++session, kind, Mode(mode), null, "gold fixture", false);
            Publish(context, MissionLifecycleKind.Initialization);
            check(world.Subscriptions == 0, "Initialization retires old ticker before starting another map");
            Publish(context, MissionLifecycleKind.Start);
            return context;
        };
        var map = begin(0, MissionStartKind.NewGame, GameModeKind.CustomGame);
        world.Emit(400);
        check(world.Subscriptions == 0 && world.Visits == 0, "Zero/zero never subscribes or visits players");
        Publish(map, MissionLifecycleKind.End);
        settings.PeriodicGoldHuman = 50; settings.PeriodicGoldAI = -25;
        map = begin(0, MissionStartKind.NewGame, GameModeKind.CustomGame);
        check(world.Subscriptions == 1 && !settings.CanConfigurePeriodicGold, "Active gold session owns one subscription and locks editors");
        settings.PeriodicGoldHuman = 1; settings.PeriodicGoldAIValueText = "99";
        check(settings.PeriodicGoldHuman == 50 && settings.PeriodicGoldAI == -25, "Mission locks both numeric and text setters");
        world.Emit(0); world.Emit(399);
        check(human.Pending == 7 && ai.Gold == 10, "No immediate payout or payout before tick 400");
        world.Emit(400);
        check(human.Pending == 57 && human.Gold == 1000, "Positive Human amount queues incoming gold without direct credit");
        check(ai.Gold == 0 && ai.Pending == 23 && dead.Gold == 999, "Negative AI amount clamps to zero and preserves incoming/dead players");
        world.Emit(400); world.Emit(399);
        check(world.Visits == 1, "Repeated and older ticks cannot pay twice");
        Publish(map, MissionLifecycleKind.Start, true);
        check(world.Subscriptions == 1, "Replayed start does not duplicate subscription");
        world.Emit(800, paused: true);
        check(world.Visits == 1, "Paused engine does not deliver ticks");
        world.Emit(800);
        check(human.Pending == 107 && world.Visits == 2, "Next simulation boundary pays exactly once");
        settings.EnableMod = false;
        check(world.Subscriptions == 0, "Disabling ExtraFeatures completely detaches the gold handler");
        world.Emit(1200);
        settings.EnableMod = true; world.Emit(1200);
        check(world.Visits == 2 && world.Subscriptions == 1, "Reactivation neither catches up nor repays current tick");
        world.Emit(1600);
        check(world.Visits == 3, "Reactivation resumes on the next boundary");
        Publish(map, MissionLifecycleKind.End);
        check(world.Subscriptions == 0 && settings.CanConfigurePeriodicGold, "End detaches ticker and unlocks settings");
        map = begin(1600, MissionStartKind.LoadedSave, GameModeKind.CustomGame);
        world.Emit(1600); world.Emit(1999);
        check(world.Visits == 3, "Save loaded at a paid boundary never repays it");
        world.Emit(2000);
        check(world.Visits == 4, "Save retains global interval phase");
        Publish(map, MissionLifecycleKind.End);
        map = begin(2399, MissionStartKind.LoadedSave, GameModeKind.CustomGame);
        world.Emit(2400);
        check(world.Visits == 5, "Save immediately before boundary waits one tick rather than restarting interval");
        Publish(map, MissionLifecycleKind.End);
        settings.PeriodicGoldHuman = 0; settings.PeriodicGoldAI = 5;
        map = begin(0, MissionStartKind.EditorCreated, GameModeKind.MapEditor);
        int previousHuman = human.Pending; ai.Alive = false;
        world.Emit(400);
        check(human.Pending == previousHuman && ai.Pending == 23, "Disabled Human role and defeated AI receive no payout in editor");
        ai.Alive = true; world.Emit(800);
        check(ai.Pending == 28 && ai.Gold == 0, "AI-only positive payout also works in editor");
        Publish(map, MissionLifecycleKind.End);
        foreach (GameModeKind mode in new[] { GameModeKind.Campaign, GameModeKind.StandaloneMission, GameModeKind.VanillaTrail, GameModeKind.Unknown })
        {
            map = begin(0, MissionStartKind.NewGame, mode);
            check(world.Subscriptions == 0, "Disallowed mission mode has no gold ticker: " + mode);
            Publish(map, MissionLifecycleKind.End);
        }
        settings.PeriodicGoldHuman = int.MaxValue; settings.PeriodicGoldAI = int.MinValue;
        human.Gold = int.MaxValue - 12; human.Pending = 5; ai.Gold = int.MaxValue;
        map = begin(0, MissionStartKind.NewGame, GameModeKind.CustomGame);
        world.Emit(400);
        check(human.Pending == 12 && ai.Gold == 0 && ai.Pending == 28, "Int32 extrema are safe for pending credit and direct debit");
        int credits = world.Credits;
        world.Emit(800);
        check(world.Credits == credits, "No credit when balance plus pending already fills Int32 capacity");
        Publish(map, MissionLifecycleKind.End);
        settings.PeriodicGoldHuman = -1; settings.PeriodicGoldAI = 0;
        human.Gold = 1; human.Pending = 123;
        map = begin(0, MissionStartKind.NewGame, GameModeKind.CustomGame);
        world.Emit(400); world.Emit(800);
        check(human.Gold == 0 && human.Pending == 123, "Repeated Human debit at zero preserves pending income");
        // Replacement/failed attempts must retire the previous session even without Start.
        var failed = new MissionContext(++session, MissionStartKind.LoadedSave, Mode(GameModeKind.CustomGame), null, "failed", false);
        Publish(failed, MissionLifecycleKind.Initialization);
        Publish(map, MissionLifecycleKind.End);
        check(world.Subscriptions == 0, "Replacing an active map detaches ticker before failed load");
        Publish(failed, MissionLifecycleKind.End);
        check(settings.CanConfigurePeriodicGold, "Failed load leaves settings unlocked");
        settings.PeriodicGoldHuman = 77; settings.PeriodicGoldAI = -5;
        human.Gold = 20; human.Pending = 3; ai.Gold = 20; ai.Pending = 9;
        var clientSettings = new ExtraFeaturesViewModel(false);
        var clientWorld = new World();
        clientWorld.Players.AddRange(new[] { new Player { Id = 2, Gold = 20, Pending = 3 },
            new Player { Id = 7, AI = true, Gold = 20, Pending = 9 } });
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(clientSettings, () =>
        {
            clientSettings.PeriodicGoldHuman = settings.PeriodicGoldHuman;
            clientSettings.PeriodicGoldAI = settings.PeriodicGoldAI;
        });
        new PeriodicGoldRuntime(null, clientSettings, clientWorld).Register();
        map = begin(0, MissionStartKind.NewGame, GameModeKind.CustomGame);
        foreach (int tick in new[] { 399, 400, 400, 799, 800, 1200 }) { world.Emit(tick); clientWorld.Emit(tick); }
        check(clientWorld.Players[0].Pending == human.Pending && clientWorld.Players[0].Gold == human.Gold &&
            clientWorld.Players[1].Gold == ai.Gold && clientWorld.Players[1].Pending == ai.Pending,
            "Two offline peers with synchronized settings execute identical tick/accounting results");
        int beforeGateClosure = human.Pending;
        // Model a captured event delegate before the activation notification arrives.
        var blocked = new MissionContext(++session, MissionStartKind.NewGame, Mode(GameModeKind.Campaign), null, "gate transition", false);
        Shared.GameplayModActivationGate.Gate.Update(new MissionLifecycleNotification(blocked,
            MissionLifecycleKind.Initialization, MissionInitializationPhase.BeforeLoad, MissionEndReason.None, false));
        world.Emit(1600); clientWorld.Emit(1600);
        check(human.Pending == beforeGateClosure && clientWorld.Players[0].Pending == beforeGateClosure,
            "Captured tick callbacks cannot pay after the authoritative mode gate closes");
        Publish(map, MissionLifecycleKind.End);
        check(clientWorld.Subscriptions == 0 && world.Subscriptions == 0, "Both offline peers detach at mission end");
    }

    private static void Settings(Action<bool, string> check)
    {
        string fixture = TestWorkDirectory.New("PeriodicGoldPreset");
        string plugin = Path.Combine(fixture, "ExtraFeatures.dll");
        Func<ExtraFeaturesViewModel> prepare = () =>
        {
            var model = new ExtraFeaturesViewModel(false);
            model.PreparePresets(null, plugin, "PeriodicGold"); model.ActivatePresets();
            return model;
        };
        var settings = prepare();
        check(settings.PeriodicGoldHuman == 0 && settings.PeriodicGoldAI == 0, "Periodic gold defaults are zero");
        settings.PeriodicGoldHumanValueText = " +125 "; settings.PeriodicGoldAIValueText = "-30";
        check(settings.PeriodicGoldHuman == 125 && settings.PeriodicGoldAI == -30, "Signed whole gold amounts accepted independently");
        foreach (string invalid in new[] { "1.5", "1,5", "1 gold", "gold 1", "", "-", "+", "2147483648", "-2147483649", "1e2", "1 000" })
        {
            settings.PeriodicGoldHumanValueText = invalid; settings.PeriodicGoldAIValueText = invalid;
            check(settings.PeriodicGoldHuman == 125 && settings.PeriodicGoldAI == -30 && settings.PeriodicGoldHumanValueText == "125", "Invalid gold text preserves authoritative values: " + invalid);
        }
        settings.PeriodicGoldHumanValueText = int.MinValue.ToString(); settings.PeriodicGoldAIValueText = int.MaxValue.ToString();
        check(settings.PeriodicGoldHuman == int.MinValue && settings.PeriodicGoldAI == int.MaxValue, "Full signed Int32 range is accepted");
        settings.PeriodicGoldHuman = 125; settings.PeriodicGoldAI = -30;
        var restored = prepare();
        check(restored.PeriodicGoldHuman == 125 && restored.PeriodicGoldAI == -30, "Both gold amounts persist across restart");
        restored.System_SavePersonalPreset("gold-pair", "Gold pair", "", new[] {
            new PresetSaveSelection { PropertyName = nameof(restored.PeriodicGoldHuman), Mode = PublishedPresetValueMode.Fixed },
            new PresetSaveSelection { PropertyName = nameof(restored.PeriodicGoldAI), Mode = PublishedPresetValueMode.Fixed } }, false);
        var preset = restored.System_TestPublishedPresets.Single(p => p.Id == "gold-pair");
        restored.System_LoadModDefaults();
        check(restored.PeriodicGoldHuman == 0 && restored.PeriodicGoldAI == 0, "Shared defaults reset both gold fields");
        restored.System_TestLoadPreset(preset.StableId);
        check(restored.PeriodicGoldHuman == 125 && restored.PeriodicGoldAI == -30, "Named preset restores both gold fields");
        GameNetworkAPI.LocalHost = false; GameNetworkAPI.Networked = true; GameNetworkAPI.MultiplayerGame = true;
        restored.PeriodicGoldHuman = 999; restored.PeriodicGoldAIValueText = "999";
        check(!restored.CanConfigurePeriodicGold && restored.PeriodicGoldHuman == 125 && restored.PeriodicGoldAI == -30, "Client cannot edit either host gold setting");
        GameXAMLManagerAPI.Instance.ApplyNetworkSync(restored, () => { restored.PeriodicGoldHuman = 7; restored.PeriodicGoldAI = -9; });
        check(restored.PeriodicGoldHuman == 7 && restored.PeriodicGoldAI == -9, "Authoritative gold synchronization applies both roles");
        GameNetworkAPI.LocalHost = true; GameNetworkAPI.Networked = false; GameNetworkAPI.MultiplayerGame = false;
        var own = prepare();
        check(own.PeriodicGoldHuman == 125 && own.PeriodicGoldAI == -30, "Received gold values do not overwrite local preset");
        own.ResetToDefaultCommand.Execute(null);
        check(own.PeriodicGoldHuman == 0 && own.PeriodicGoldAI == 0, "ExtraFeatures reset includes both gold fields");
    }
}
