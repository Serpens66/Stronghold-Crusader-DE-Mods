using System;
using BepInEx.Logging;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.API.Components.SaveData;
namespace BepInEx.Logging { public class ManualLogSource {} }
namespace CrusaderDE { public class Placeholder {} }
namespace R3 { public class Subject<T> {
    public Action<T> Changed; public Subject<T> Observable=>this;
    public IDisposable Subscribe(Action<T> callback) { Changed+=callback; return new Token(()=>Changed-=callback); }
    public void Emit(T value)=>Changed?.Invoke(value);
    class Token:IDisposable { Action remove; public Token(Action a){remove=a;} public void Dispose(){remove?.Invoke();remove=null;} }
} }
namespace SHCDESE.EventAPI {
    public enum EventHookPhase { Pre,Post }
    public static class UnitR3EventHooks { public static R3.Subject<UnitCreateEventArgs> OnUnitCreate=new R3.Subject<UnitCreateEventArgs>(); }
}
namespace SHCDESE.EventAPI.Units { public class UnitCreateEventArgs { public EventHookPhase Phase; public long ReturnValue; public eChimps UnitType; public int PlayerOwnerId; } }
namespace SHCDESE.Interop.Enums { public enum EnemyHPModifier { Normal,Weak,Strong,VeryStrong } public enum AliveState : short { Dead=0,NeedsInit=1,IsAlive=2 } }
namespace SHCDESE.Interop {
    public enum eChimps { CHIMP_TYPE_LORD=55, Soldier=1 }
    public enum eGoods { Count=26 }
    public struct GameUnit { public uint r_GlobalId,r_CurrentHealth,r_MaxHealth,r_HealthBarBlocks; public ushort r_CurrentHealthPercentage; public int r_ControllableForPlayerId; public eChimps r_UnitChimp; public AliveState r_AliveState; }
}
namespace Shared {
    public static class DebugLogHelper { public static void LogDebug(object l,string s){} public static void LogError(object l,string s){} public static void LogWarning(object l,string s){} }
    public enum GameplayFeatureId { LordHealthMultipliers }
    public static class GameplayModActivationGate { public static bool Allowed=true; public static object Snapshot=>null; public static bool IsEnabled(bool enabled)=>Allowed&&enabled; }
    public static class GameplayFeatureModePolicy { public static int Calls; public static bool IsAllowed(string owner,GameplayFeatureId f,object s){Calls++;return GameplayModActivationGate.Allowed;} }
}
namespace SHCDESE.API {
    public class ModSaveDataAPI {
        public static ModSaveDataAPI Instance=new ModSaveDataAPI();
        public Func<SaveContext,byte[]> Save; public Action<byte[],LoadContext> Load;
        public bool RegisterModDataHandler(string id,Func<SaveContext,byte[]> save,Action<byte[],LoadContext> load){if(Save!=null)return false;Save=save;Load=load;return true;}
        public bool UnregisterModDataHandler(string id){Save=null;Load=null;return true;}
    }
    public class GameMapArchiveManagerAPI {
        public static GameMapArchiveManagerAPI Instance=new GameMapArchiveManagerAPI(); public byte[] Bytes;
        public byte[] TryReadBinaryFile(string file,bool ignoreCase)=>Bytes;
    }
    public class GameTimeManagerAPI { public static GameTimeManagerAPI Instance=new GameTimeManagerAPI(); public event Action<int> OnTick; public void Tick(int t)=>OnTick?.Invoke(t); public int Subscribers=>OnTick?.GetInvocationList().Length??0; }
    public class GamePlayerManagerAPI {
        public static GamePlayerManagerAPI Instance=new GamePlayerManagerAPI(); public int[] Ids=new int[9],Globals=new int[9]; public bool[] AI=new bool[9]; public int Reads,AIReads; public bool ThrowOnce;
        public int GetLordUnitId(int p){Reads++;if(ThrowOnce){ThrowOnce=false;throw new Exception("read failure");} return Ids[p];}
        public int GetLordUnitGlobalId(int p){Reads++;return Globals[p];}
        public bool IsAIPlayer(int p){AIReads++;return AI[p];}
        public int GetAILord(int p)=>AI[p]?1:0;
        public SHCDESE.Interop.Enums.EnemyHPModifier GetEnemyHealthModifier()=>SHCDESE.Interop.Enums.EnemyHPModifier.Normal;
    }
    public unsafe class GameUnitManagerAPI {
        public static GameUnitManagerAPI Instance=new GameUnitManagerAPI(); public GameUnit* Units; public uint BaseHealth=100;
        public bool TryGetUnitById(int id,out GameUnit* unit){unit=id>0&&id<16?Units+id:null; return unit!=null;}
        public uint GetDefaultHealth(eChimps type)=>BaseHealth;
    }
    public class GameAIManagerAPI {
        public static GameAIManagerAPI Instance=new GameAIManagerAPI(); public int Percent=150;
        public class AIC { public int lord_hps_percent; }
        public class AICArray { public int Length=>2; public AIC GetValue(int i)=>new AIC{lord_hps_percent=Instance.Percent}; }
        public AICArray GetAICArray()=>new AICArray();
    }
}
namespace SHCDESE.API.Components.SaveData {
    public class SaveContext { public bool IsSaveFile=true,IsMapEditorSave; }
    public class LoadContext { public bool IsSaveFile=true; }
}
namespace ExtraFeatures {
    public static class ExtraFeaturesPlugin { public const string PluginGuid="ExtraFeatures"; }
    public class ExtraFeaturesViewModel { public int HumanLordHealthPercent=200,AILordHealthPercent=200; public bool EnableMod=true,MarketPricesAlsoForAI; }internal sealed unsafe class LordHealthRuntime : IDisposable
    {
        internal const string SaveDataIdentifier = "ExtraFeatures.LordHealth.v1";
        private const string ArchiveFileName = "_SE_ModData_" + SaveDataIdentifier + ".msgpack";
        private const int FirstPlayerId = 1;
        private const int LastPlayerId = 8;
        private const int ScanTickInterval = 10;
        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly LordHealthBasis[] bases = new LordHealthBasis[9];
        private readonly uint[] appliedGlobalIds = new uint[9];
        private readonly uint[] createdGlobalIds = new uint[9];
        private IDisposable unitCreatedSubscription;
        private int pendingPlayersMask;
        private int warnedPlayersMask;
        private long sessionId;
        private bool initialized;
        private bool mapActive;
        private bool loadedSave;
        private int humanPercent = LordHealthMultiplierPolicy.DefaultPercent;
        private int aiPercent = LordHealthMultiplierPolicy.DefaultPercent;

        public LordHealthRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Initialize()
        {
            if (initialized) return;
            unitCreatedSubscription = UnitR3EventHooks.OnUnitCreate.Observable.Subscribe(OnUnitCreated);
            // Archive callbacks run in Pre and Post, before managed initialization is
            // complete. BeginMap reads the final archive instead of applying early data.
            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(SaveDataIdentifier, SaveState, IgnoreEarlyLoad))
            {
                unitCreatedSubscription.Dispose();
                unitCreatedSubscription = null;
                throw new InvalidOperationException("Lord health save-data registration failed.");
            }
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
            initialized = true;
        }

        private static void IgnoreEarlyLoad(byte[] bytes, LoadContext context) { }

        public void BeginMap(long newSessionId, bool isLoadedSave)
        {
            if (sessionId == newSessionId) return;
            ResetMapState();
            sessionId = newSessionId;
            if (!IsFeatureModeAllowed()) return;
            loadedSave = isLoadedSave;
            if (loadedSave)
            {
                try
                {
                    byte[] bytes = GameMapArchiveManagerAPI.Instance.TryReadBinaryFile(ArchiveFileName, ignoreCase: true);
                    if (bytes == null || bytes.Length == 0)
                    {
                        Shared.DebugLogHelper.LogWarning(log,
                            "Extra Features Lord health inactive: this save has no recorded Vanilla HP basis. Legacy saves are not supported.");
                        return;
                    }
                    LordHealthSaveState state = LordHealthSaveState.Decode(bytes);
                    foreach (LordHealthBasis basis in state.Records) bases[basis.PlayerId] = basis;
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(log, $"Extra Features Lord health inactive: invalid saved HP basis: {ex}");
                    return;
                }
            }
            humanPercent = LordHealthMultiplierPolicy.NormalizePercent(settings.HumanLordHealthPercent);
            aiPercent = LordHealthMultiplierPolicy.NormalizePercent(settings.AILordHealthPercent);
            pendingPlayersMask = 0x1FE;
            mapActive = true;
            Shared.DebugLogHelper.LogDebug(log,
                $"Extra Features Lord health initialized: session={sessionId}, savedBasis={loadedSave}, humans={humanPercent}%, AI={aiPercent}%.");
            ApplyAvailableLords();
        }

        public void ResetMapState()
        {
            mapActive = false;
            pendingPlayersMask = 0;
            warnedPlayersMask = 0;
            sessionId = 0;
            loadedSave = false;
            Array.Clear(bases, 0, bases.Length);
            Array.Clear(appliedGlobalIds, 0, appliedGlobalIds.Length);
            Array.Clear(createdGlobalIds, 0, createdGlobalIds.Length);
        }

        // Only an unpublished initialization candidate may be disposed.
        // Ordinary sessions retain these process-rooted registrations.
        public void Dispose()
        {
            ResetMapState();
            if (!initialized) return;
            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            ModSaveDataAPI.Instance.UnregisterModDataHandler(SaveDataIdentifier);
            unitCreatedSubscription?.Dispose();
            unitCreatedSubscription = null;
            initialized = false;
        }

        private void OnGameTick(int tick)
        {
            if (!mapActive || pendingPlayersMask == 0 || tick % ScanTickInterval != 0) return;
            if (!IsFeatureModeAllowed()) { ResetMapState(); return; }
            try { ApplyAvailableLords(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Extra Features Lord health scan failed: {ex}");
            }
        }

        private static bool IsFeatureModeAllowed() =>
            Shared.GameplayFeatureModePolicy.IsAllowed(ExtraFeaturesPlugin.PluginGuid,
                Shared.GameplayFeatureId.LordHealthMultipliers, Shared.GameplayModActivationGate.Snapshot);

        private void OnUnitCreated(UnitCreateEventArgs args)
        {
            if (!mapActive || args.Phase != EventHookPhase.Post || args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue) return;
            // Post arguments retain original inputs even when Pre subscribers change them.
            // Read the returned unit's actual type/owner; do not write HP here.
            if (!GameUnitManagerAPI.Instance.TryGetUnitById((int)args.ReturnValue, out GameUnit* unit) ||
                unit == null || unit->r_UnitChimp != eChimps.CHIMP_TYPE_LORD) return;
            int playerId = unit->r_ControllableForPlayerId;
            if (playerId < FirstPlayerId || playerId > LastPlayerId || unit->r_GlobalId == 0) return;
            createdGlobalIds[playerId] = unit->r_GlobalId;
            pendingPlayersMask |= 1 << playerId;
            // The caller publishes Player.LordUnitId and finishes AI HP scaling AFTER Post.
        }

        private void ApplyAvailableLords()
        {
            int pending = pendingPlayersMask;
            for (int playerId = FirstPlayerId; playerId <= LastPlayerId; playerId++)
            {
                int bit = 1 << playerId;
                if ((pending & bit) == 0) continue;
                pendingPlayersMask &= ~bit;
                try { if (!TryApplyPlayerLord(playerId)) pendingPlayersMask |= bit; }
                catch { pendingPlayersMask |= bit; throw; }
            }
        }

        private bool TryGetPlayerLord(int playerId, out GameUnit* lord, out bool retry, bool capturePending = false)
        {
            lord = null;
            retry = false;
            int unitId = GamePlayerManagerAPI.Instance.GetLordUnitId(playerId);
            if (unitId <= 0) return false;
            int expectedGlobalId = GamePlayerManagerAPI.Instance.GetLordUnitGlobalId(playerId);
            if (expectedGlobalId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out lord) ||
                lord == null || lord->r_GlobalId != (uint)expectedGlobalId ||
                lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD || lord->r_ControllableForPlayerId != playerId)
            {
                retry = true;
                return false;
            }
            // The spawn helper initializes health while the slot still has NeedsInit.
            // Wait before applying; saving may record its completed spawn maximum.
            // Never revive a dying Lord whose alive flag has not yet changed.
            if (lord->r_CurrentHealth == 0) return false;
            retry = lord->r_AliveState == AliveState.NeedsInit;
            return lord->r_AliveState == AliveState.IsAlive || (capturePending && retry);
        }

        private bool TryGetBasis(int playerId, GameUnit* lord, out LordHealthBasis basis)
        {
            basis = bases[playerId];
            if (basis.GlobalId == lord->r_GlobalId) return true;
            if (loadedSave && createdGlobalIds[playerId] != lord->r_GlobalId)
            {
                LogPlayerWarningOnce(playerId, "saved Lord identity has no matching Vanilla HP basis");
                return false;
            }
            if (lord->r_MaxHealth == 0) return false;
            basis = new LordHealthBasis(playerId, lord->r_GlobalId, lord->r_MaxHealth);
            bases[playerId] = basis;
            return true;
        }

        private bool TryApplyPlayerLord(int playerId)
        {
            if (!TryGetPlayerLord(playerId, out GameUnit* lord, out bool retry)) return !retry;
            if (appliedGlobalIds[playerId] == lord->r_GlobalId) return true;
            if (!TryGetBasis(playerId, lord, out LordHealthBasis basis))
                return lord->r_MaxHealth != 0; // Zero can become ready; missing saved identity is terminal.
            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            int selectedPercent = isAI ? aiPercent : humanPercent;
            uint oldMaximum = lord->r_MaxHealth;
            uint oldCurrent = lord->r_CurrentHealth;
            uint newMaximum = LordHealthMultiplierPolicy.CalculateMaximum(basis.VanillaMaximum, selectedPercent);
            uint newCurrent = LordHealthMultiplierPolicy.CalculateCurrent(oldCurrent, oldMaximum, newMaximum);
            ushort healthPercent = LordHealthMultiplierPolicy.CalculateHealthPercent(newCurrent, newMaximum);
            lord->r_MaxHealth = newMaximum;
            lord->r_CurrentHealth = newCurrent;
            lord->r_CurrentHealthPercentage = healthPercent;
            lord->r_HealthBarBlocks = (uint)(healthPercent / 10);
            appliedGlobalIds[playerId] = lord->r_GlobalId;
            Shared.DebugLogHelper.LogDebug(log,
                $"Extra Features applied Lord health: player={playerId}, globalId={lord->r_GlobalId}, " +
                $"multiplier={selectedPercent}%, health={oldCurrent}/{oldMaximum}->{newCurrent}/{newMaximum}, vanillaMax={basis.VanillaMaximum}.");
            return true;
        }

        private byte[] SaveState(SaveContext context)
        {
            if (!mapActive || !context.IsSaveFile || context.IsMapEditorSave || !IsFeatureModeAllowed()) return null;
            var records = new List<LordHealthBasis>(LastPlayerId);
            for (int playerId = FirstPlayerId; playerId <= LastPlayerId; playerId++)
            {
                if (TryGetPlayerLord(playerId, out GameUnit* lord, out _, capturePending: true) &&
                    TryGetBasis(playerId, lord, out LordHealthBasis basis)) records.Add(basis);
            }
            // Capture a new Lord saved before its next tick without mutating native save data.
            return LordHealthSaveState.Encode(records.ToArray());
        }

        private void LogPlayerWarningOnce(int playerId, string reason)
        {
            int bit = 1 << playerId;
            if ((warnedPlayersMask & bit) != 0) return;
            warnedPlayersMask |= bit;
            Shared.DebugLogHelper.LogWarning(log, $"Extra Features skipped Lord health for player {playerId}: {reason}.");
        }
    }
internal static class LordHealthMultiplierPolicy
    {
        public const int MinimumPercent = 10;
        public const int MaximumPercent = 500;
        public const int DefaultPercent = 100;

        public static int NormalizePercent(int percent) =>
            Math.Max(MinimumPercent, Math.Min(MaximumPercent, percent));

        public static uint CalculateMaximum(uint vanillaMaximum, int settingPercent) =>
            Scale(vanillaMaximum, NormalizePercent(settingPercent));

        public static uint CalculateCurrent(uint currentHealth, uint currentMaximum, uint targetMaximum)
        {
            if (targetMaximum == 0)
                return 1;
            if (currentMaximum == 0)
                return targetMaximum;

            ulong boundedCurrent = Math.Min((ulong)currentHealth, currentMaximum);
            ulong scaled = (boundedCurrent * targetMaximum + currentMaximum / 2UL) / currentMaximum;
            return (uint)Math.Max(1UL, Math.Min((ulong)targetMaximum, scaled));
        }

        public static ushort CalculateHealthPercent(uint currentHealth, uint maximumHealth)
        {
            if (maximumHealth == 0)
                return 0;

            ulong boundedCurrent = Math.Min((ulong)currentHealth, maximumHealth);
            ulong percent = (boundedCurrent * 100UL + maximumHealth / 2UL) / maximumHealth;
            return (ushort)Math.Min(100UL, percent);
        }

        private static uint Scale(uint value, int percent)
        {
            if (value == 0)
                return 1;

            ulong scaled = ((ulong)value * (ulong)Math.Max(1, percent) + 50UL) / 100UL;
            return (uint)Math.Max(1UL, Math.Min(uint.MaxValue, scaled));
        }
    }
public class MarketHarness { private volatile bool useVanillaAIPricesForSession;
internal void SetSessionOverride(bool enabled)
        {
            useVanillaAIPricesForSession = enabled;
        }
public bool ShouldUseVanillaPrice(IntPtr playerManager, int playerId, int good)
        {
            if (!useVanillaAIPricesForSession || playerManager == IntPtr.Zero ||
                playerId < 1 || playerId > 8 || good < 0 || good >= (int)eGoods.Count)
                return false;

            // Settings are fixed for the session; player classification is live game state.
            return GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
        }
}
public class SessionHarness { public MarketHarness aiMarketVanillaPriceHook=new MarketHarness(); public bool mapActive; public ExtraFeaturesViewModel settings=new ExtraFeaturesViewModel();
public void CaptureAIMarketSessionSettings()
        {
            aiMarketVanillaPriceHook?.SetSessionOverride(mapActive &&
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                !settings.MarketPricesAlsoForAI);
        }
}
public static unsafe class SessionTests {
    static int checks;
    static void Assert(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static void Create(int player,int id,uint global,uint max=100,uint current=50) {
        GameUnitManagerAPI.Instance.Units[id]=new GameUnit {r_GlobalId=global,r_ControllableForPlayerId=player,r_UnitChimp=eChimps.CHIMP_TYPE_LORD,r_AliveState=AliveState.IsAlive,r_MaxHealth=max,r_CurrentHealth=current};
        GamePlayerManagerAPI.Instance.Ids[player]=id; GamePlayerManagerAPI.Instance.Globals[player]=(int)global;
    }
    static void Spawn(int player,int id,eChimps type=eChimps.CHIMP_TYPE_LORD,EventHookPhase phase=EventHookPhase.Post)=>UnitR3EventHooks.OnUnitCreate.Emit(new UnitCreateEventArgs {PlayerOwnerId=player,ReturnValue=id,UnitType=type,Phase=phase});
    static void Reject(Action action,string message) { bool rejected=false;try{action();}catch{rejected=true;}Assert(rejected,message); }
    static void TestSavedBases(LordHealthRuntime lord,ExtraFeaturesViewModel settings) {
        var units=GameUnitManagerAPI.Instance;var players=GamePlayerManagerAPI.Instance;var clock=GameTimeManagerAPI.Instance;
        var archive=GameMapArchiveManagerAPI.Instance;var save=ModSaveDataAPI.Instance;
        for(int p=0;p<9;p++){players.Ids[p]=0;players.Globals[p]=0;players.AI[p]=false;}
        settings.HumanLordHealthPercent=200;settings.AILordHealthPercent=300;
        // Real native mission values, deliberately unlike the default table or fake AIC.
        Create(1,1,201,237,79); Create(2,2,202,330,330);players.AI[2]=true;
        lord.ResetMapState();lord.BeginMap(4,false);
        Assert(units.Units[1].r_MaxHealth==474&&units.Units[1].r_CurrentHealth==158,"custom mission basis lost");
        Assert(units.Units[2].r_MaxHealth==990,"actual AI basis lost");
        byte[] bytes=save.Save(new SaveContext());
        var decoded=LordHealthSaveState.Decode(bytes);
        Assert(decoded.Records.Length==2&&decoded.Records[0].VanillaMaximum==237&&decoded.Records[1].VanillaMaximum==330,"save contains scaled maximum");
        Assert(save.Save(new SaveContext{IsMapEditorSave=true})==null&&save.Save(new SaveContext{IsSaveFile=false})==null,"editor/map save received HP state");
        // Native load normalizes the healthy Lord, leaves the wounded Lord unchanged.
        units.Units[2].r_MaxHealth=100;units.Units[2].r_CurrentHealth=100;
        settings.HumanLordHealthPercent=300;settings.AILordHealthPercent=200;archive.Bytes=bytes;
        save.Load(new byte[]{0xc0},new LoadContext()); // An early/stale load callback cannot replace the final archive.
        lord.ResetMapState();lord.BeginMap(5,true);
        Assert(units.Units[1].r_MaxHealth==711&&units.Units[1].r_CurrentHealth==237,"wounded reload compounded or healed");
        Assert(units.Units[2].r_MaxHealth==660&&units.Units[2].r_CurrentHealth==660,"healthy native normalization lost saved basis");
        for(int i=0;i<20;i++) {
            archive.Bytes=save.Save(new SaveContext());
            lord.ResetMapState();lord.BeginMap(6+i,true);
            Assert(units.Units[1].r_MaxHealth==711&&units.Units[1].r_CurrentHealth==237,"repeat load drifts");
        }
        archive.Bytes=save.Save(new SaveContext());settings.HumanLordHealthPercent=100;settings.AILordHealthPercent=100;
        lord.ResetMapState();lord.BeginMap(30,true);
        Assert(units.Units[1].r_MaxHealth==237&&units.Units[1].r_CurrentHealth==79&&units.Units[2].r_MaxHealth==330,"100% did not restore actual baseline");
        // New units after loading may acquire a new basis; save before the next tick.
        Create(3,3,203,175,70);Spawn(3,3,eChimps.Soldier); // Original event arguments differ from actual unit.
        bytes=save.Save(new SaveContext());
        Assert(units.Units[3].r_MaxHealth==175&&units.Units[3].r_CurrentHealth==70,"save callback changed native unit");
        decoded=LordHealthSaveState.Decode(bytes);
        Assert(decoded.Records.Length==3&&decoded.Records[2].VanillaMaximum==175,"save-before-tick lost new basis");
        archive.Bytes=bytes;settings.HumanLordHealthPercent=200;lord.ResetMapState();lord.BeginMap(31,true);
        Assert(units.Units[3].r_MaxHealth==350&&units.Units[3].r_CurrentHealth==140,"saved pending Lord was not scaled once");
        // Reusing a unit slot/global identity different from the saved one must not inherit HP.
        Create(1,1,204,321,107);archive.Bytes=bytes;lord.ResetMapState();lord.BeginMap(32,true);
        Assert(units.Units[1].r_MaxHealth==321,"mismatched global ID inherited saved basis");
        int reads=players.Reads;clock.Tick(200);Assert(reads==players.Reads,"missing identity polled forever");
        Create(1,1,205,219,73);Spawn(1,1);clock.Tick(210);Assert(units.Units[1].r_MaxHealth==438,"new replacement did not capture new basis");
        // Zero health must remain terminal even before native changes the alive flag.
        Create(4,4,206,150,0);Spawn(4,4);clock.Tick(220);Assert(units.Units[4].r_CurrentHealth==0,"dying Lord revived");
        Create(5,5,207,270,90);units.Units[5].r_AliveState=AliveState.NeedsInit;Spawn(5,5);
        clock.Tick(220);Assert(units.Units[5].r_MaxHealth==270,"NeedsInit was written before readiness");
        var pendingSave=save.Save(new SaveContext());
        bool pendingRecorded=false;foreach(var b in LordHealthSaveState.Decode(pendingSave).Records)if(b.GlobalId==207)pendingRecorded=b.VanillaMaximum==270;
        Assert(pendingRecorded,"save lost NeedsInit Lord baseline");
        units.Units[5].r_AliveState=AliveState.IsAlive;clock.Tick(230);
        Assert(units.Units[5].r_MaxHealth==540&&units.Units[5].r_CurrentHealth==180,"NeedsInit did not retry when ready");
        archive.Bytes=null;lord.ResetMapState();lord.BeginMap(33,true);reads=players.Reads;clock.Tick(230);
        Assert(reads==players.Reads&&units.Units[1].r_MaxHealth==438&&save.Save(new SaveContext())==null,"legacy save was reconstructed or multiplied");
        archive.Bytes=new byte[]{0xc0};lord.ResetMapState();lord.BeginMap(34,true);clock.Tick(240);
        Assert(reads==players.Reads&&save.Save(new SaveContext())==null,"corrupt save was applied");
        var eight=new LordHealthBasis[8];for(int i=0;i<8;i++)eight[i]=new LordHealthBasis(i+1,(uint)(i+1),uint.MaxValue);
        Assert(LordHealthSaveState.Decode(LordHealthSaveState.Encode(eight)).Records.Length==8,"maximum payload failed");
        Reject(()=>LordHealthSaveState.Decode(new byte[]{0xdd,0x7f,0xff,0xff,0xff}),"oversized array accepted");
        Reject(()=>LordHealthSaveState.Decode(new byte[]{0x91,2}),"future version accepted");
        Reject(()=>LordHealthSaveState.Decode(new byte[]{0x91,1,0}),"trailing bytes accepted");
        Reject(()=>LordHealthSaveState.Decode(new byte[]{0x94,1,1,1}),"truncated record accepted");
        Reject(()=>LordHealthSaveState.Decode(new byte[129]),"oversized payload accepted");
        Reject(()=>LordHealthSaveState.Encode(new[]{new LordHealthBasis(1,1,0)}),"zero basis accepted");
        Reject(()=>LordHealthSaveState.Encode(new[]{new LordHealthBasis(9,1,100)}),"invalid player accepted");
        Reject(()=>LordHealthSaveState.Encode(new[]{new LordHealthBasis(1,0,100)}),"zero global ID accepted");
        Reject(()=>LordHealthSaveState.Encode(new[]{new LordHealthBasis(1,1,100),new LordHealthBasis(1,2,100)}),"duplicate player accepted");
        lord.ResetMapState();
    }
    public static string Run() {
        var units=GameUnitManagerAPI.Instance; var players=GamePlayerManagerAPI.Instance; var clock=GameTimeManagerAPI.Instance;
        units.Units=(GameUnit*)Marshal.AllocHGlobal(sizeof(GameUnit)*16);
        for(int i=0;i<16;i++)units.Units[i]=default;
        try {
            var settings=new ExtraFeaturesViewModel(); var lord=new LordHealthRuntime(new BepInEx.Logging.ManualLogSource(),settings);
            lord.Initialize(); lord.Initialize(); Assert(clock.Subscribers==1,"duplicate tick");
            Create(1,1,101); lord.BeginMap(1,false);
            Assert(units.Units[1].r_MaxHealth==200&&units.Units[1].r_CurrentHealth==100,"initial proportional health");
            int reads=players.Reads,modeReads=Shared.GameplayFeatureModePolicy.Calls;
            for(int i=0;i<10000;i++)clock.Tick(i);
            Assert(players.Reads==reads&&Shared.GameplayFeatureModePolicy.Calls==modeReads,"idle still scans");
            settings.HumanLordHealthPercent=300; lord.BeginMap(1,false); Assert(players.Reads==reads,"duplicate session recaptured settings");
            Create(2,2,102); players.Ids[2]=0; Spawn(2,2); Assert(players.Reads==reads,"event queried player before caller completion");
            players.AI[2]=true; Create(2,2,102,150,75); clock.Tick(9); Assert(units.Units[2].r_MaxHealth==150,"early application");
            clock.Tick(10); Assert(units.Units[2].r_MaxHealth==300&&units.Units[2].r_CurrentHealth==150,"late AI Lord");
            Assert(players.Reads==reads+2,"dirty scan did not target one player");
            Spawn(1,1); clock.Tick(20); Assert(units.Units[1].r_MaxHealth==200,"duplicate spawn multiplied health");
            Create(1,3,103); Spawn(1,3); clock.Tick(30); Assert(units.Units[3].r_MaxHealth==200,"replacement lost session settings");
            var archive=GameMapArchiveManagerAPI.Instance; var save=ModSaveDataAPI.Instance;
            archive.Bytes=save.Save(new SaveContext());
            settings.HumanLordHealthPercent=100; settings.AILordHealthPercent=100; lord.ResetMapState(); lord.BeginMap(2,true);
            Assert(units.Units[3].r_MaxHealth==100&&units.Units[3].r_CurrentHealth==50,"reload with default settings");
            Assert(units.Units[2].r_MaxHealth==150,"AI reload default");
            reads=players.Reads; Spawn(1,0); units.Units[7].r_UnitChimp=eChimps.Soldier; Spawn(1,7); Spawn(1,3,eChimps.CHIMP_TYPE_LORD,EventHookPhase.Pre); clock.Tick(40); Assert(players.Reads==reads,"unrelated event scheduled work");
            Create(3,4,104,0,50); Spawn(3,4); clock.Tick(50); Assert(units.Units[4].r_CurrentHealth==50,"unready wrote health");
            units.Units[4].r_MaxHealth=100; clock.Tick(60); reads=players.Reads; clock.Tick(70); Assert(players.Reads==reads,"ready player retried");
            Create(4,5,105); Spawn(4,5); players.ThrowOnce=true; clock.Tick(80); clock.Tick(90); Assert(units.Units[5].r_CurrentHealthPercentage==50,"exception lost pending player");
            Create(5,6,106); Spawn(5,6); units.Units[6].r_AliveState=AliveState.Dead; clock.Tick(100); reads=players.Reads; clock.Tick(110); Assert(players.Reads==reads,"dead Lord polls forever");
            lord.ResetMapState(); Spawn(1,3); clock.Tick(120); Assert(players.Reads==reads,"retired session processed spawn");
            Shared.GameplayModActivationGate.Allowed=false; lord.BeginMap(3,false); clock.Tick(130); Assert(players.Reads==reads,"editor or disallowed mode wrote health");
            Shared.GameplayModActivationGate.Allowed=true;
            TestSavedBases(lord,settings);
            var session=new SessionHarness(); var market=session.aiMarketVanillaPriceHook;
            for(int active=0;active<2;active++)for(int enabled=0;enabled<2;enabled++)for(int applyAI=0;applyAI<2;applyAI++) {
                session.mapActive=active!=0; session.settings.EnableMod=enabled!=0;session.settings.MarketPricesAlsoForAI=applyAI!=0;session.CaptureAIMarketSessionSettings();
                for(int p=0;p<=9;p++)for(int g=-1;g<=26;g++)for(int ptr=0;ptr<2;ptr++) {
                    bool expected=active!=0&&enabled!=0&&applyAI==0&&ptr!=0&&p>=1&&p<=8&&g>=0&&g<26&&players.AI[p];
                    Assert(market.ShouldUseVanillaPrice(new IntPtr(ptr),p,g)==expected,"market guard mismatch");
                }
            }
            session.mapActive=true;session.settings.EnableMod=true;session.settings.MarketPricesAlsoForAI=false;session.CaptureAIMarketSessionSettings();
            session.settings.MarketPricesAlsoForAI=true; Assert(market.ShouldUseVanillaPrice(new IntPtr(1),2,0),"live setting leaked into snapshot");
            session.CaptureAIMarketSessionSettings();Assert(!market.ShouldUseVanillaPrice(new IntPtr(1),2,0),"reload missed changed setting");
            session.settings.MarketPricesAlsoForAI=false;session.CaptureAIMarketSessionSettings();players.AI[2]=false;Assert(!market.ShouldUseVanillaPrice(new IntPtr(1),2,0),"AI classification incorrectly frozen");
            session.mapActive=false;session.CaptureAIMarketSessionSettings();players.AIReads=0;for(int i=0;i<10000;i++)market.ShouldUseVanillaPrice(new IntPtr(1),1,0);Assert(players.AIReads==0,"disabled market queries AI");
            return $"{checks} checks passed; 10000 settled Lord ticks: zero player/mode reads; market snapshots/reload/live AI validated. Callback arithmetic, original fallback and breadcrumbs unchanged.";
        } finally { Marshal.FreeHGlobal((IntPtr)units.Units); units.Units=null; }
    }
}
}
public static class Program { public static void Main() { System.Console.WriteLine(ExtraFeatures.SessionTests.Run()); } }
