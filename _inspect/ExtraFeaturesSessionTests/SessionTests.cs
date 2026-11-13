using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using APIShared.Health;
using APIShared.Modifiers;
using ExtraFeatures;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

// The production consumer, policy, legacy decoder and public value/context types are source-linked.
// Only the external shared-service registration and native access boundary are replaced here.
namespace BepInEx.Logging { public sealed class ManualLogSource { } }
namespace Shared
{
    public enum GameplayFeatureId { LordHealthMultipliers }
    public static class DebugLogHelper
    {
        public static int Errors;
        public static void LogErrorLimited(object log, string key, string text) { Errors++; }
    }
    public static class GameplayModActivationGate
    {
        public static bool Allowed = true;
        public static bool IsEnabled(bool enabled) => Allowed && enabled;
        public static bool IsFeatureAllowed(string owner, GameplayFeatureId feature) => Allowed;
    }
}
namespace ExtraFeatures
{
    public static class ExtraFeaturesPlugin { public const string PluginGuid = "ExtraFeatures.Tests"; }
    public sealed class ExtraFeaturesViewModel
    {
        public int HumanLordHealthPercent = 110, AILordHealthPercent = 120;
        public int HumanSoldierHealthPercent = 130, AISoldierHealthPercent = 140;
        public int HumanCivilianHealthPercent = 150, AICivilianHealthPercent = 160;
        public int HumanBuildingHealthPercent = 170, AIBuildingHealthPercent = 180;
        public bool EnableMod = true, MarketPricesAlsoForAI;
    }
}
namespace SHCDESE.Interop
{
    public struct GameUnit
    {
        public uint r_GlobalId, r_CurrentHealth, r_MaxHealth;
        public ushort r_IsKilledByProjectile;
        public int r_ControllableForPlayerId;
        public eChimps r_UnitChimp;
        public AliveState r_AliveState;
    }
}
namespace SHCDESE.API.Components.SaveData
{
    public sealed class SaveContext { }
    public sealed class LoadContext { }
}
namespace SHCDESE.API
{
    public sealed class ModSaveDataAPI
    {
        public static readonly ModSaveDataAPI Instance = new ModSaveDataAPI();
        public Func<SaveContext, byte[]> Save;
        public Action<byte[], LoadContext> Load;
        public int Registrations;
        public bool RegisterModDataHandler(string id, Func<SaveContext, byte[]> save,
            Action<byte[], LoadContext> load, Action unload)
        {
            Registrations++; Save = save; Load = load; return true;
        }
    }
    public sealed class GameMapArchiveManagerAPI
    {
        public static readonly GameMapArchiveManagerAPI Instance = new GameMapArchiveManagerAPI();
        public object Archive = new object();
        public object GetMapArchive() => Archive;
    }
    public sealed class GamePlayerManagerAPI
    {
        public const int MAX_PLAYERS = 8;
        public static readonly GamePlayerManagerAPI Instance = new GamePlayerManagerAPI();
        public int[] Ids = new int[9], Globals = new int[9];
        public bool[] AI = new bool[9];
        public int AIReads;
        public int GetLordUnitId(int player) => Ids[player];
        public int GetLordUnitGlobalId(int player) => Globals[player];
        public bool IsAIPlayer(int player) { AIReads++; return AI[player]; }
    }
    public unsafe sealed class GameUnitManagerAPI
    {
        public static readonly GameUnitManagerAPI Instance = new GameUnitManagerAPI();
        public GameUnit* Units;
        public bool IsValidId(int id) => id > 0 && id < 16;
        public bool TryGetUnitById(int id, out GameUnit* unit) { unit = IsValidId(id) ? Units + id : null; return unit != null; }
        public delegate bool UnitPredicate(in GameUnit unit);
        public UnitQuery QueryUnits() => new UnitQuery(this);
        public sealed class UnitQuery
        {
            private readonly GameUnitManagerAPI owner;
            private UnitPredicate filter;
            public UnitQuery(GameUnitManagerAPI value) { owner = value; }
            public UnitQuery Where(UnitPredicate value) { filter = value; return this; }
            public void ToIdList(List<int> target) { for (int id = 1; id < 16; id++) if (filter(in owner.Units[id])) target.Add(id); }
        }
    }
}
namespace APIShared.Health
{
    public sealed class HealthModifierHandle
    {
        public bool IsEnabled;
        public void SetEnabled(bool enabled) { IsEnabled = enabled; }
    }
    public static class HealthModifiers
    {
        public static Func<HealthModifierContext, ValueModifierContribution> Provider;
        public static Func<HealthLegacyBasis[]> Legacy;
        public static HealthModifierHandle Handle;
        public static int Registrations, LegacyRegistrations;
        public static bool TryRegister(string owner, string id, Func<HealthModifierContext, ValueModifierContribution> provider,
            out HealthModifierHandle handle, out string reason, int order = 0)
        {
            Registrations++; Provider = provider; Handle = handle = new HealthModifierHandle(); reason = ""; return true;
        }
        public static bool TryRegisterLegacyBasisProvider(string owner, string id, Func<HealthLegacyBasis[]> provider,
            out string reason, int order = 0)
        {
            LegacyRegistrations++; Legacy = provider; reason = ""; return true;
        }
    }
}

internal static unsafe class SessionTests
{
    private static int checks;
    private static void Check(bool result, string name) { checks++; if (!result) throw new Exception(name); }
    private static HealthModifierContext Context(HealthObjectKind kind, eChimps unit, eStructs building, bool ai = false) =>
        new HealthModifierContext(kind, 1, 101, 1, ai, unit, building, 30000, HealthInitializationReason.Created);
    private static void Factor(HealthModifierContext context, decimal expected)
    {
        ValueModifierContribution result = HealthModifiers.Provider(context);
        Check(result.Kind == ValueModifierKind.Factor && result.Stage == ValueModifierStage.BeforeFixedValue && result.Value == expected,
            "Mission factor reaches the shared before-fixed stage");
    }
    private static void CreateLord(int player, int unitId, uint global)
    {
        GameUnitManagerAPI.Instance.Units[unitId] = new GameUnit { r_GlobalId = global, r_MaxHealth = 30000,
            r_CurrentHealth = 10000, r_UnitChimp = eChimps.CHIMP_TYPE_LORD, r_ControllableForPlayerId = player,
            r_AliveState = AliveState.IsAlive };
        GamePlayerManagerAPI.Instance.Ids[player] = unitId;
        GamePlayerManagerAPI.Instance.Globals[player] = (int)global;
    }
    private static byte[] Legacy(params LordHealthBasis[] records)
    {
        // Independent, historical flat MessagePack format, with unsigned 32-bit values.
        var bytes = new List<byte> { 0xdc, 0, (byte)(1 + records.Length * 3), 1 };
        Action<uint> number = value => { bytes.Add(0xce); bytes.Add((byte)(value >> 24)); bytes.Add((byte)(value >> 16)); bytes.Add((byte)(value >> 8)); bytes.Add((byte)value); };
        foreach (LordHealthBasis basis in records) { number((uint)basis.PlayerId); number(basis.GlobalId); number(basis.VanillaMaximum); }
        return bytes.ToArray();
    }
    private static void Reject(byte[] bytes, string name)
    {
        bool failed = false; try { LordHealthSaveState.Decode(bytes); } catch { failed = true; } Check(failed, name);
    }

    private static void TestConsumer()
    {
        var settings = new ExtraFeaturesViewModel();
        var runtime = new HealthModifierRuntime(new BepInEx.Logging.ManualLogSource(), settings);
        runtime.Initialize(); runtime.Initialize();
        Check(HealthModifiers.Registrations == 1 && HealthModifiers.LegacyRegistrations == 1 && ModSaveDataAPI.Instance.Registrations == 1,
            "Shared health and old reader registrations remain process-owned and idempotent");
        Check(!HealthModifiers.Handle.IsEnabled, "HP consumer is dormant before a mission");
        CreateLord(1, 1, 101);
        runtime.BeginMap(1, false);
        Check(HealthModifiers.Handle.IsEnabled, "Custom mission enables shared basis capture");
        Factor(Context(HealthObjectKind.Unit, eChimps.CHIMP_TYPE_LORD, default), 1.1m);
        Factor(Context(HealthObjectKind.Unit, eChimps.CHIMP_TYPE_LORD, default, true), 1.2m);
        Factor(Context(HealthObjectKind.Unit, eChimps.CHIMP_TYPE_ENGINEER, default), 1.3m);
        Factor(Context(HealthObjectKind.Unit, eChimps.CHIMP_TYPE_SIEGE_TOWER, default, true), 1.4m);
        Factor(Context(HealthObjectKind.Unit, eChimps.CHIMP_TYPE_WOODCUTTER, default), 1.5m);
        Factor(Context(HealthObjectKind.Unit, eChimps.CHIMP_TYPE_PEASANT, default, true), 1.6m);
        Factor(Context(HealthObjectKind.Building, default, eStructs.STRUCT_GATE_MAIN), 1.7m);
        Factor(Context(HealthObjectKind.Building, default, eStructs.STRUCT_TOWER1, true), 1.8m);
        Check(HealthModifiers.Provider(Context(HealthObjectKind.Unit, eChimps.CHIMP_TYPE_MOTHER, default)).Kind == ValueModifierKind.None &&
            HealthModifiers.Provider(Context(HealthObjectKind.Building, default, eStructs.STRUCT_SIEGE_TOWER)).Kind == ValueModifierKind.None,
            "Decoration and siege proxy receive no EF category multiplier");
        foreach (eStructs wall in new[] { eStructs.STRUCT_WAS_WALL, eStructs.STRUCT_WOOD_WALL,
            eStructs.STRUCT_STONE_WALL, eStructs.STRUCT_CRENAL_WALL })
            foreach (bool ai in new[] { false, true })
                Check(HealthModifiers.Provider(Context(HealthObjectKind.Building, default, wall, ai)).Kind == ValueModifierKind.None,
                    "Wall contexts receive no human or AI building factor: " + wall);
        Check(HealthModifiers.Provider(new HealthModifierContext(HealthObjectKind.Unit, 1, 101, 0, false,
                eChimps.CHIMP_TYPE_ARCHER, default, 30000, HealthInitializationReason.Created)).Kind == ValueModifierKind.None,
            "Neutral ownership is not a human player HP setting");
        settings.HumanLordHealthPercent = 500;
        runtime.BeginMap(1, false);
        Factor(Context(HealthObjectKind.Unit, eChimps.CHIMP_TYPE_LORD, default), 1.1m);
        Check(GameUnitManagerAPI.Instance.Units[1].r_MaxHealth == 30000 && GameUnitManagerAPI.Instance.Units[1].r_CurrentHealth == 10000,
            "Consumer publishes policy without writing native health");
        Check(ModSaveDataAPI.Instance.Save(new SaveContext()) == null, "Obsolete Lord save entry is never emitted");
        runtime.ResetMapState();
        Check(!HealthModifiers.Handle.IsEnabled && HealthModifiers.Provider(Context(HealthObjectKind.Unit, eChimps.CHIMP_TYPE_LORD, default)).Kind == ValueModifierKind.None,
            "Ending the mission disables future contributions without changing completed HP");
        Shared.GameplayModActivationGate.Allowed = false;
        runtime.BeginMap(2, false);
        Check(!HealthModifiers.Handle.IsEnabled, "Editor/disallowed modes keep every HP category inactive");
        Shared.GameplayModActivationGate.Allowed = true;
        settings.EnableMod = false; runtime.BeginMap(3, false);
        Check(!HealthModifiers.Handle.IsEnabled, "Disabled EF publishes no health modifier");
        settings.EnableMod = true;
        settings.HumanLordHealthPercent = settings.AILordHealthPercent = settings.HumanSoldierHealthPercent = settings.AISoldierHealthPercent =
            settings.HumanCivilianHealthPercent = settings.AICivilianHealthPercent = settings.HumanBuildingHealthPercent = settings.AIBuildingHealthPercent = 100;
        runtime.BeginMap(4, false);
        Check(HealthModifiers.Handle.IsEnabled && HealthModifiers.Provider(Context(HealthObjectKind.Unit, eChimps.CHIMP_TYPE_LORD, default)).Kind == ValueModifierKind.Factor &&
            HealthModifiers.Provider(Context(HealthObjectKind.Unit, eChimps.CHIMP_TYPE_LORD, default)).Value == 1m,
            "Neutral percentages request restoration of their coordinated original basis");

        byte[] old = Legacy(new LordHealthBasis(1, 101, 25000));
        ModSaveDataAPI.Instance.Load(old, new LoadContext()); runtime.BeginMap(5, true);
        HealthLegacyBasis[] imported = HealthModifiers.Legacy();
        Check(imported.Length == 1 && imported[0].UnitId == 1 && imported[0].GlobalId == 101 && imported[0].Maximum == 25000,
            "Matching old Lord basis is imported after completed mission start");
        Check(HealthModifiers.Legacy() == null, "Legacy basis is offered exactly once");
        Check(GameUnitManagerAPI.Instance.Units[1].r_MaxHealth == 30000, "Legacy import does not itself mutate HP");
        ModSaveDataAPI.Instance.Load(old, new LoadContext());
        GameMapArchiveManagerAPI.Instance.Archive = new object(); runtime.BeginMap(6, true);
        Check(HealthModifiers.Legacy() == null, "Stale archive callback cannot supply another world's basis");
        ModSaveDataAPI.Instance.Load(Legacy(new LordHealthBasis(1, 999, 25000)), new LoadContext()); runtime.BeginMap(7, true);
        Check(HealthModifiers.Legacy().Length == 0, "Reused Lord slot cannot inherit a different global identity");
        ModSaveDataAPI.Instance.Load(old, new LoadContext());
        GameUnitManagerAPI.Instance.Units[1].r_UnitChimp = eChimps.CHIMP_TYPE_ARCHER; runtime.BeginMap(8, true);
        Check(HealthModifiers.Legacy().Length == 0, "Legacy Lord basis cannot target another unit type");
        ModSaveDataAPI.Instance.Load(new byte[] { 0x91, 2 }, new LoadContext()); runtime.BeginMap(9, true);
        Check(HealthModifiers.Legacy() == null && Shared.DebugLogHelper.Errors == 1,
            "Malformed legacy data is rejected without disabling independent categories");
        runtime.BeginMap(10, true);
        Check(HealthModifiers.Legacy() == null && HealthModifiers.Handle.IsEnabled,
            "Missing legacy data leaves shared save-compatibility decisions to the service");
        ModSaveDataAPI.Instance.Load(Legacy(), new LoadContext()); runtime.BeginMap(11, true);
        Check(HealthModifiers.Legacy() != null, "A valid empty legacy archive differs from absent or rejected data");
        CreateLord(3, 6, 106);
        ModSaveDataAPI.Instance.Load(Legacy(new LordHealthBasis(3, 106, 31000)), new LoadContext()); runtime.BeginMap(12, true);
        imported = HealthModifiers.Legacy();
        Check(imported.Length == 1 && imported[0].UnitId == 6 && imported[0].Maximum == 31000,
            "Legacy player IDs resolve to the actual one-based Lord unit ID");
        GameUnitManagerAPI.Instance.Units[6].r_ControllableForPlayerId = 4;
        ModSaveDataAPI.Instance.Load(Legacy(new LordHealthBasis(3, 106, 31000)), new LoadContext()); runtime.BeginMap(13, true);
        Check(HealthModifiers.Legacy().Length == 0, "Legacy basis cannot be imported onto another owner's unit");
        Reject(new byte[] { 0xdd, 0x7f, 0xff, 0xff, 0xff }, "Oversized legacy array is rejected before allocating");
        Reject(new byte[] { 0x91, 1, 0 }, "Legacy trailing bytes rejected");
        Reject(Legacy(new LordHealthBasis(1, 1, 0)), "Legacy zero maximum rejected");
        Reject(Legacy(new LordHealthBasis(9, 1, 100)), "Legacy player outside one-based range rejected");
        Reject(Legacy(new LordHealthBasis(1, 0, 100)), "Legacy zero identity rejected");
        Reject(Legacy(new LordHealthBasis(1, 1, 100), new LordHealthBasis(1, 2, 100)), "Duplicate legacy player rejected");
    }

    private static void TestExistingMarketBoundary()
    {
        var players = GamePlayerManagerAPI.Instance; players.AI[2] = true;
        var session = new SessionHarness(); var market = session.aiMarketVanillaPriceHook;
        for (int active = 0; active < 2; active++) for (int enabled = 0; enabled < 2; enabled++) for (int applyAI = 0; applyAI < 2; applyAI++)
        {
            session.mapActive = active != 0; session.settings.EnableMod = enabled != 0; session.settings.MarketPricesAlsoForAI = applyAI != 0;
            session.CaptureAIMarketSessionSettings();
            for (int player = 0; player <= 9; player++) for (int good = -1; good <= (int)eGoods.Count; good++) for (int pointer = 0; pointer < 2; pointer++)
            {
                bool expected = active != 0 && enabled != 0 && applyAI == 0 && pointer != 0 && player >= 1 && player <= 8 &&
                    good >= 0 && good < (int)eGoods.Count && players.AI[player];
                Check(market.ShouldUseVanillaPrice(new IntPtr(pointer), player, good) == expected, "Existing market guard boundary");
            }
        }
        session.mapActive = true; session.settings.EnableMod = true; session.settings.MarketPricesAlsoForAI = false;
        session.CaptureAIMarketSessionSettings(); session.settings.MarketPricesAlsoForAI = true;
        Check(market.ShouldUseVanillaPrice(new IntPtr(1), 2, 0), "Existing market setting remains a mission snapshot");
        session.CaptureAIMarketSessionSettings();
        Check(!market.ShouldUseVanillaPrice(new IntPtr(1), 2, 0), "Existing market refresh applies next snapshot");
    }

    public static int Main()
    {
        GameUnitManagerAPI.Instance.Units = (GameUnit*)Marshal.AllocHGlobal(sizeof(GameUnit) * 16);
        for (int id = 0; id < 16; id++) GameUnitManagerAPI.Instance.Units[id] = default;
        try { TestConsumer(); TestExistingMarketBoundary(); Console.WriteLine("PASS: " + checks + " HP consumer/legacy and retained market-boundary checks."); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { Marshal.FreeHGlobal((IntPtr)GameUnitManagerAPI.Instance.Units); GameUnitManagerAPI.Instance.Units = null; }
    }
}
