using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Economy;
using APIShared.Multiplayer;
using APIShared.Savegames;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;

namespace AIMarketAcceptance
{
    [BepInPlugin(Guid, "AI Market Acceptance", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.4")]
    [BepInDependency("APIShared_Serp", "0.65.0")]
    [BepInDependency("AIMarketOverbuyDiagnostic_Serp", "0.1.0")]
    public sealed class AcceptancePlugin : BaseUnityPlugin
    {
        public const string Guid="AIMarketAcceptance_Serp";
        private void Awake()=>Acceptance.Initialize(Logger);
    }
    // Explicit .run opt-in only. APIShared owns the persistent Unity automation driver;
    // all callbacks/delegates here are process-rooted statics, without startup teardown.
    internal static partial class Acceptance
    {
        private const BindingFlags Static=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static ManualLogSource log;
        private static ModApiClient api;
        private static Type owner,aic;
        private static long module;
        private static string runId,fixture,save;
        private static bool giftRecruitRun;
        private static int initialized,player,good,expectRestore,restores,decisionChecks,rawVerified;
        private static long lastRawSession;
        private static Exception observationFailure;
        private static AiMarketRegistration marketObserver;
        private static readonly bool[] warmupActors=new bool[GamePlayerManagerAPI.MAX_PLAYERS+1];
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void RecruitCall(IntPtr root,int player);
        private static RecruitCall recruit;
        private struct State
        {
            internal bool Active;
            internal int Pending,Expected,Package,Credit,Stock,Source,LastStock;
            internal long Sequence;
        }
        internal static void Initialize(ManualLogSource logger)
        {
            if(Interlocked.Exchange(ref initialized,1)!=0)return;
            log=logger;
            string path=Path.Combine(Paths.ConfigPath,"SerpAutomation",AcceptancePlugin.Guid+".run");
            if(!File.Exists(path))return;
            string[] request=File.ReadAllLines(path);
            if(request.Length<2||(request[1]!="market-save-roundtrip"&&request[1]!="market-gift-recruit"))return;
            giftRecruitRun=request[1]=="market-gift-recruit";
            if(!System.Guid.TryParseExact(request[0],"N",out var id)||id==System.Guid.Empty)
                throw new InvalidDataException("Invalid owned acceptance run ID; request retained.");
            runId=id.ToString("N");
            // The external runner restores config. Keep normal Workshop listing while
            // preventing a forced Steam restart from escaping its hidden desktop.
            SHCDESE.BepInEx.Bootstrap.Plugin.Instance.DisableSteamUGCUpdates.Value=true;
            fixture=request.Length>2&&!string.IsNullOrWhiteSpace(request[2])?request[2]:
                Path.Combine(Paths.BepInExRootPath,"SerpAutomationRollback","fixture.sav");
            CrusaderLibrary.Instance.LibraryLoaded+=context=>{
                module=context.ModuleHandle.ToInt64();
                recruit=Marshal.GetDelegateForFunctionPointer<RecruitCall>(context.ModuleHandle+0x40740);
            };
            api=ApiShared.ForMod(AcceptancePlugin.Guid);
            api.WhenReady(_=>{
                if(!api.Ui.TryPost(()=>{
                    try
                    {
                        string[] current=File.ReadAllLines(path);
                        Require(current.Length>=2&&current[0]==request[0]&&current[1]==request[1],"request changed before startup");
                        Require(AiMarketEvents.CheckIntegrity(),"shared market hooks unavailable: "+AiMarketEvents.Diagnostic);
                        owner=FindType("AIMarketOverbuyDiagnostic.MarketPendingDemandFix");
                        aic=FindType("AIMarketOverbuyDiagnostic.MarketBacklogAic");
                        if(giftRecruitRun) RegisterGiftRecruit();
                        else
                        {
                            RegisterComponentContracts();
                            Require(NativeWorldRestoreEvents.TryRegister(AcceptancePlugin.Guid,"pre-fold-ledger",AfterRestore,out string reason,100),reason);
                            Require(AiMarketEvents.TryRegister(AcceptancePlugin.Guid,"loaded-ledger-decision",BeforeDecision,
                                (c,t,d,r)=>{},out marketObserver,out reason,-200,onFailure:e=>observationFailure=e),reason);
                            marketObserver.SetEnabled(true);
                        }
                        File.Delete(path);
                        api.Automation.Start(id,Steps(),log,quitWhenDone:true);
                    }
                    catch(Exception error){Info("INIT_FAIL "+error);throw;}
                }))throw new InvalidOperationException("Unity dispatch unavailable; owned request retained.");
            });
        }
        private static IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("load-owned-fixture",TimeSpan.FromSeconds(150),r=>r.LoadSave(fixture));
            if(giftRecruitRun)
            {
                yield return new AutomationStep("vanilla-single-gift-and-recruit",TimeSpan.FromSeconds(230),RunGiftRecruit,true);
                yield break;
            }
            yield return new AutomationStep("native-source-credit-save",TimeSpan.FromSeconds(180),PrepareAndSave,true);
            yield return new AutomationStep("load-roundtrip-one",TimeSpan.FromSeconds(150),r=>Reload(r,1));
            yield return new AutomationStep("check-roundtrip-one",TimeSpan.FromSeconds(45),r=>CheckLoaded(r,1),true);
            yield return new AutomationStep("load-roundtrip-two",TimeSpan.FromSeconds(150),r=>Reload(r,2));
            yield return new AutomationStep("check-roundtrip-two",TimeSpan.FromSeconds(45),r=>CheckLoaded(r,2),true);
        }
        private static IEnumerable<AutomationWait> PrepareAndSave(AutomationRun run)
        {
            var mission=run.CurrentMission;
            Require(mission!=null&&!mission.IsEditor&&!mission.Mode.IsRealMultiplayer,"acceptance requires local gameplay");
            yield return run.WaitForPlayState(mission);
            foreach(var wait in run.SetPause(mission,true))yield return wait;
            run.InSimulation(mission,PrepareWarmup);
            Director.instance.SetEngineFrameRate(250);
            foreach(var wait in run.SetPause(mission,false))yield return wait;
            var warmup=Stopwatch.StartNew();double nextStatus=10;
            while(!run.InSimulation(mission,TryPrepareReady))
            {
                Require(warmup.Elapsed.TotalSeconds<90,"no genuine recruit request after90s natural warmup; see WARMUP_GATE and CANDIDATE_RESULT");
                if(warmup.Elapsed.TotalSeconds>=nextStatus)
                {run.InSimulation(mission,LogWarmupGates);nextStatus+=10;}
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(250));
            }
            Require(GamePlayerManagerAPI.Instance.IsLocalPaused(),"native gate capture did not retain pause");
            run.InSimulation(mission,()=>{
                RequireFixOn("before-contract-regressions");
                VerifyPureCapture();
                VerifyOwnerRepairCallbacks();
                RequireState(Read(player,good),"after-contract-regressions");
                return true;
            });
            save=Path.Combine(ConfigSettings.GetSavesPath(),"SerpAcceptance-"+runId+".sav");
            Require(!File.Exists(save),"run-owned save already exists; refusing overwrite");
            yield return AutomationWait.Until("native safe save",()=>Director.instance.SafeToSave());
            Director.instance.FinishedSaving();
            var screen=CameraControls2D.instance.getScreenXYForSaveCentring(1,1);int x=-1,y=-1;
            GameMap.instance.getRotationCentre(ref x,ref y);
            Require(EngineInterface.SaveSaveGame(save,screen.x,screen.y,x,y,false,false,false),"native save writer rejected fixture");
            Require(File.Exists(save)&&new FileInfo(save).Length>0,"run-owned save missing or empty");
            Info("SAVE_WRITTEN path="+save+" player="+player+" good="+good+" pending=4 credited=1 source=native-recruitment fixturePendingWrites=0");
        }
        private static unsafe bool PrepareWarmup()
        {
            RequireFixOn("before-native-warmup");
            var players=GamePlayerManagerAPI.Instance;int eligible=0;
            for(int candidate=1;candidate<warmupActors.Length;candidate++)
            {
                if(!(bool)Call(aic,"CanTestEquipmentBuyer",candidate))continue;
                if(!players.TryGetPlayerResourcesById(candidate,out GamePlayerResources* r)||r==null)continue;
                Call(aic,"Configure",candidate);warmupActors[candidate]=true;eligible++;
                int gold=players.GetPlayerGold(candidate);
                if(gold<1000000)Require(players.AddPlayerGold(candidate,1000000-gold),"warmup funding failed");
                for(int g=(int)eGoods.STORED_BOWS;g<=(int)eGoods.STORED_METAL_ARMOUR;g++)
                {int stock=players.GetGoodAmount(candidate,(eGoods)g);if(stock>0)players.RemoveGood(candidate,(eGoods)g,stock);}
            }
            Require(eligible>0,"no native custom equipment buyer in fixture");
            Info("WARMUP_BEGIN fundedActors="+eligible+" speed=250 deadlineSeconds=90 physicalEquipmentDeficit=True fixturePendingWrites=0");
            return LogWarmupGates();
        }
        private static unsafe bool LogWarmupGates()
        {
            var players=GamePlayerManagerAPI.Instance;
            for(int candidate=1;candidate<warmupActors.Length;candidate++)
                if(warmupActors[candidate]&&players.TryGetPlayerResourcesById(candidate,out GamePlayerResources* r)&&r!=null)
                    Info("WARMUP_GATE player="+candidate+" native2B6C="+r->N00004FD9+" purchase3974="+r->N0000535B+
                        " readyPeasants="+r->r_ReadyPeasants+" existingPeasants="+r->r_ExistingPeasants+" totalPeasants="+r->r_TotalPeasants+
                        " housing="+r->r_CivilianHousingSpace+" mode="+r->r_AICombatRecruitmentMode+" cadence="+r->N0000513D+
                        " gold="+players.GetPlayerGold(candidate));
            return true;
        }
        private static unsafe bool TryPrepareReady()
        {
            var players=GamePlayerManagerAPI.Instance;bool ready=false;
            for(int candidate=1;candidate<warmupActors.Length;candidate++)
                if(warmupActors[candidate]&&players.TryGetPlayerResourcesById(candidate,out GamePlayerResources* r)&&r!=null&&
                    r->N00004FD9>0&&r->N0000535B!=0){ready=true;break;}
            if(!ready)return false;
            // Keep the gate observation, ordinary pause request and actual source execution
            // under the same engine monitor. A queued AI tick cannot consume this opportunity.
            EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused,1,1);
            Require(players.IsLocalPaused(),"ordinary pause was vetoed while capturing native recruitment readiness");
            if(PrepareFixture())return true;
            EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused,0,0);
            return false;
        }
        private static unsafe bool PrepareFixture()
        {
                Require(module!=0&&recruit!=null&&AiMarketEvents.CheckIntegrity(),"audited recruitment entry not ready");
                var players=GamePlayerManagerAPI.Instance;
                for(int candidate=1;candidate<=GamePlayerManagerAPI.MAX_PLAYERS&&player==0;candidate++)
                {
                    if(!warmupActors[candidate])continue;
                    bool eligible=(bool)Call(aic,"CanTestEquipmentBuyer",candidate);
                    if(!players.TryGetPlayerResourcesById(candidate,out GamePlayerResources* r)||r==null)
                    { Info("CANDIDATE player="+candidate+" eligible="+eligible+" resources=False");continue; }
                    Info("CANDIDATE player="+candidate+" ai="+players.IsAIPlayer(candidate)+" lord="+players.GetAILord(candidate)+
                        " equipmentBuyer="+eligible+" readyPeasants="+r->r_ReadyPeasants+" existingPeasants="+r->r_ExistingPeasants+" gold="+players.GetPlayerGold(candidate)+
                        " nativeRecruitGate2B6C="+r->N00004FD9+" purchaseGate3974="+r->N0000535B+
                        " recruitMode="+r->r_AICombatRecruitmentMode+" cadenceIndex="+r->N0000513B+" cadenceCounter="+r->N0000513D+
                        " lastStand="+r->r_AILastStandPhase);
                    if(!eligible||r->N00004FD9==0||r->N0000535B==0)continue;
                    // 190CA0 checks missing equipment before it scans idle peasants. Zero
                    // ready peasants therefore does not forbid a genuine missing-good wish.
                    Call(aic,"Configure",candidate);
                    int attempts=RecruitmentAttemptBound(r);
                    int gold=players.GetPlayerGold(candidate);
                    if(gold<1000000)Require(players.AddPlayerGold(candidate,1000000-gold),"funded recruitment fixture failed");
                    for(int g=(int)eGoods.STORED_BOWS;g<=(int)eGoods.STORED_METAL_ARMOUR;g++)
                    {
                        if(giftRecruitRun&&(g==(int)eGoods.STORED_LEATHER_ARMOUR||g==(int)eGoods.STORED_METAL_ARMOUR))
                        {
                            int armour=players.GetGoodAmount(candidate,(eGoods)g);
                            if(armour<4)Require(players.TryAddGood(candidate,(eGoods)g,4-armour),"fixture armour not accepted");
                            continue;
                        }
                        State prior=Read(candidate,g);
                        if(prior.Active&&prior.Expected>0)Require(players.TryAddGood(candidate,(eGoods)g,prior.Expected),"close prior request physically");
                        int stock=players.GetGoodAmount(candidate,(eGoods)g);
                        if(stock>0)players.RemoveGood(candidate,(eGoods)g,stock);
                    }
                    // Actual game producer through the shared broker. Never write pending or fabricate provenance.
                    for(int attempt=0;attempt<attempts&&player==0;attempt++)
                    {
                        recruit(new IntPtr(module+0x404C950),candidate);
                        for(int g=(int)eGoods.STORED_BOWS;g<=(int)eGoods.STORED_METAL_ARMOUR;g++)
                        {
                            State found=Read(candidate,g);
                            if(!found.Active||found.Pending!=5||found.Expected!=5||found.Package!=5||found.Credit!=0||found.Stock!=0||found.Sequence<=0)continue;
                            if(giftRecruitRun&&g!=(int)eGoods.STORED_MACES)continue;
                            if(found.Source!=0x403A7&&found.Source!=0x40597&&found.Source!=0x40D3C&&found.Source!=0x40D62)continue;
                            player=candidate;good=g;break;
                        }
                    }
                    var units=GameUnitManagerAPI.Instance.GetUnitManager();
                    Info("CANDIDATE_RESULT player="+candidate+" attempts="+attempts+" selected="+(player==candidate)+
                        " readyPeasants="+r->r_ReadyPeasants+" existingPeasants="+r->r_ExistingPeasants+" nativeRecruitGate2B6C="+r->N00004FD9+
                        " cadenceCounter="+r->N0000513D+" nativeFailure="+(units.Pointer==null?-1:(int)units.Pointer->r_RecruitmentResultFailureReason)+
                        " nativeMissingGood="+(units.Pointer==null?-1:(int)units.Pointer->r_RecruitmentResultMissingGoodId));
                }
                if(player==0)return false;
                if(giftRecruitRun)return true;
                Require(players.TryAddGood(player,(eGoods)good,1),"physical partial delivery failed");
                players.RemoveGood(player,(eGoods)good,1);
                RequireState(Read(player,good),"before-save");
                return true;
        }
        private static unsafe int RecruitmentAttemptBound(GamePlayerResources* resources)
        {
            var records=GameAIManagerAPI.Instance.GetAICArray();
            long address=checked(module+0x404C950+((long)resources->r_AILordMinusOne-1)*sizeof(InternalAIC));
            long delta=address-records.GetArrayAddress().ToInt64();
            Require(resources->r_AILordMinusOne>0&&delta>=0&&delta%sizeof(InternalAIC)==0&&delta/sizeof(InternalAIC)<records.Length,
                "native recruitment profile is outside the public AIC array");
            int index=checked((int)(delta/sizeof(InternalAIC)));
            InternalAIC profile=records.GetValue(index);
            uint mode=resources->N0000513B;
            Require(mode<=2,"native recruitment cadence index outside three audited rates: "+mode);
            int rate=mode==0?profile.troop_production_rate1:mode==1?profile.troop_production_rate2:profile.troop_production_rate3;
            Require(rate<=255,"native recruitment cadence exceeds the bounded acceptance setup: "+rate);
            int attempts=Math.Max(1,rate+1);
            Info("NATIVE_CADENCE profile="+index+" index="+mode+" rate="+rate+" attempts="+attempts+" missingGoodsCheckedBeforePeasants=True");
            return attempts;
        }
        private static IEnumerable<AutomationWait> Reload(AutomationRun run,int ordinal)
        {
            Volatile.Write(ref expectRestore,ordinal);Volatile.Write(ref rawVerified,0);
            MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
            foreach(var wait in run.LoadSave(save))yield return wait;
        }
        private static bool AfterRestore(NativeWorldRestoreContext context)
        {
            if(Volatile.Read(ref expectRestore)==0)return true;
            try
            {
                Require(context.Mission!=null&&SamePath(context.Mission.FilePath,save),"unexpected raw restore mission");
                RequireFixOn("raw-pre-first-fold");
                RequireState(Read(player,good),"raw-pre-first-fold");
                Interlocked.Exchange(ref lastRawSession,context.Mission.SessionId);
                Interlocked.Increment(ref restores);Volatile.Write(ref rawVerified,1);
                Info("RAW_RESTORE_PASS ordinal="+expectRestore+" session="+context.Mission.SessionId+" pending=4 credited=1 beforeFirstFold=True");
                return true;
            }
            catch(Exception error){observationFailure=error;Info("RAW_RESTORE_FAIL "+error);return false;}
        }
        private static long BeforeDecision(AiMarketContext context)
        {
            if(context.Operation!=AiMarketOperation.PurchaseDecision||context.PlayerId!=player||Volatile.Read(ref rawVerified)==0)return 0;
            // If Vanilla has already fulfilled the packet, zero pending is legitimate.
            // An open packet must retain its partial supply through managed MissionStarted.
            State state=Read(player,good);
            if(state.Pending!=0)RequireState(state,"loaded-purchase-decision");
            Interlocked.Increment(ref decisionChecks);return 0;
        }
        private static IEnumerable<AutomationWait> CheckLoaded(AutomationRun run,int ordinal)
        {
            var mission=run.CurrentMission;yield return run.WaitForPlayState(mission);
            Require(observationFailure==null,"restore/decision observation failed: "+observationFailure);
            Require(Volatile.Read(ref rawVerified)!=0&&restores==ordinal&&Interlocked.Read(ref lastRawSession)==mission.SessionId,
                "raw restore checkpoint did not precede this managed session");
            foreach(var wait in run.SetPause(mission,true))yield return wait;
            run.InSimulation(mission,()=>{
                RequireFixOn("after-managed-start");
                State state=Read(player,good);
                if(state.Pending!=0)RequireState(state,"after-managed-start");
                int before=Volatile.Read(ref decisionChecks);
                var decision=Marshal.GetDelegateForFunctionPointer<RecruitCall>(new IntPtr(module+0x3EC90));
                decision(new IntPtr(module+0x404C950),player);
                Require(Volatile.Read(ref decisionChecks)==before+1,"new mission did not reactivate shared decision observers");
                Require(observationFailure==null,"loaded decision lost source credit: "+observationFailure);
                Info("ROUNDTRIP_PASS ordinal="+ordinal+" session="+mission.SessionId+" sourceCreditPreserved=True rawBeforeFirstFold=True freshDecisionGate=True");
                return true;
            });
            if(ordinal==2)
            {
                run.InSimulation(mission,()=>{Call(aic,"RestoreAll");return true;});
                marketObserver.SetEnabled(false);Volatile.Write(ref expectRestore,0);
            }
        }
        private static unsafe State Read(int p,int g)
        {
            object gate=owner.GetField("gate",Static).GetValue(null);
            lock(gate)
            {
                var requests=(Array)owner.GetField("requests",Static).GetValue(null);
                object request=requests.GetValue(p*(int)eGoods.Count+g),target=Field(request,"Target");
                Require(GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(p,out GamePlayerResources* r)&&r!=null,"player record unavailable");
                return new State{Active=(bool)Field(target,"Active"),Expected=(int)Field(target,"ExpectedPending"),
                    Package=(int)Field(target,"OriginalPackage"),Credit=(int)Field(target,"Credited"),LastStock=(int)Field(target,"LastObservedStock"),
                    Pending=((int*)&r->r_AIPendingMarketPurchaseAmountNull)[g],Stock=GamePlayerManagerAPI.Instance.GetGoodAmount(p,(eGoods)g),
                    Source=(int)Field(request,"SourceRva"),Sequence=(long)Field(request,"Sequence")};
            }
        }
        private static void RequireState(State state,string stage)
        {
            Require(state.Active&&state.Pending==4&&state.Expected==4&&state.Package==5&&state.Credit==1&&state.Stock==0&&state.LastStock==0,
                stage+": active="+state.Active+" pending="+state.Pending+" expected="+state.Expected+" package="+state.Package+" credited="+state.Credit+" stock="+state.Stock+" lastStock="+state.LastStock);
        }
        private static Type FindType(string name)
        {foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){var type=assembly.GetType(name,false);if(type!=null)return type;}throw new TypeLoadException(name+" requires the diagnosis development build.");}
        private static object Field(object value,string name)=>value.GetType().GetField(name,Instance).GetValue(value);
        private static object Call(Type type,string name,params object[] arguments)=>type.GetMethod(name,Static).Invoke(null,arguments);
        private static bool SamePath(string a,string b)=>!string.IsNullOrEmpty(a)&&!string.IsNullOrEmpty(b)&&string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static void Info(string message)=>log.LogInfo(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")+" MARKET_ACCEPT "+runId+" "+message);
    }
}
