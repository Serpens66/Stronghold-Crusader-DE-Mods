using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using APIShared.Automation;
using APIShared.Economy;
using APIShared.Simulation;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;

namespace AIMarketAcceptance
{
    internal static partial class Acceptance
    {
        private static AiMarketRegistration giftRecruitObserver;
        private static int giftDonor, giftArmed, giftReceived, giftRejected, recruitStockDrop, interveningSale, recruitTick, recruitedUnit;
        private static IDisposable giftTransitionSubscription;
        private static long giftSequence;
        private static State recruitedState;

        private static void RegisterGiftRecruit()
        {
            Require(AllyTransferEvents.TryRegister(AcceptancePlugin.Guid, "single-gift",
                null, OnSingleGift, out string reason), reason);
            Require(AiMarketEvents.TryRegister(AcceptancePlugin.Guid, "natural-recruit",
                ObserveRecruitStart, ObserveRecruitEnd, out giftRecruitObserver, out reason, 100,
                onFailure: error => observationFailure = error), reason);
            giftTransitionSubscription = UnitR3EventHooks.OnUnitTransition.Observable.Subscribe(ObserveTransition);
            Require(SimulationTickEvents.TryRegister(AcceptancePlugin.Guid, "gift-recruit-tick",
                CheckRecruitmentStock, out reason), reason);
        }

        private static IEnumerable<AutomationWait> RunGiftRecruit(AutomationRun run)
        {
            var mission = run.CurrentMission;
            Require(mission != null && !mission.Mode.IsRealMultiplayer && !mission.IsEditor, "requires the isolated local fixture");
            yield return run.WaitForPlayState(mission);
            foreach(var wait in run.SetPause(mission,true)) yield return wait;
            run.InSimulation(mission,PrepareWarmup);
            Director.instance.SetEngineFrameRate(250);
            foreach(var wait in run.SetPause(mission,false)) yield return wait;
            var clock=Stopwatch.StartNew();
            while(!run.InSimulation(mission,TryPrepareReady))
            {
                Require(clock.Elapsed.TotalSeconds<90,"no native package of five maces reached an active request in the fixture");
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(250));
            }

            run.InSimulation(mission,()=>
            {
                var players=GamePlayerManagerAPI.Instance;
                giftDonor=mission.LocalPlayerId??players.GetLocalPlayerId();
                Require(giftDonor>0&&giftDonor!=player,"the fixture has no distinct local donor");
                int team=players.GetPlayerTeam(giftDonor);
                if(team<=0) { team=1; players.SetPlayerTeam(giftDonor,team); }
                players.SetPlayerTeam(player,team);
                Require(players.GetPlayerTeam(giftDonor)==team&&players.GetPlayerTeam(player)==team,"local fixture team admission failed");
                if(players.GetGoodAmount(giftDonor,(eGoods)good)==0)
                    Require(players.TryAddGood(giftDonor,(eGoods)good,1),"donor has no weapon storage; provide a local fixture with an armoury");
                State request=Read(player,good);
                Require(request.Active&&request.Pending==5&&request.Expected==5&&request.Package==5&&request.Credit==0&&request.Stock==0,
                    "five-item request changed before the gift");
                giftSequence=request.Sequence;
                int recruitGold=GameUnitManagerAPI.Instance.GetUnitGoldCost(eChimps.CHIMP_TYPE_MACEMAN);
                Require(recruitGold>0&&players.SetPlayerGold(player,recruitGold),"could not stage the small local recruitment budget");
                Info("GIFT_RECRUIT_SOURCE player="+player+" good="+good+" source="+request.Source.ToString("X")+" sequence="+giftSequence+" pending=5 stock=0");
                Info("GIFT_RECRUIT_BUDGET gold="+recruitGold+" baseMarketBuy="+players.GetTradeBasePrice((eGoods)good).BuyPrice);
                return true;
            });
            Director.instance.SetEngineFrameRate(40);
            giftRecruitObserver.SetEnabled(true);
            Volatile.Write(ref giftArmed,1);
            try
            {
                EngineInterface.GameAction(Enums.GameActionCommand.Ally_SendGoods,player,good,1);
                foreach(var wait in run.SetPause(mission,false)) yield return wait;
                yield return AutomationWait.Until("native one-weapon gift receipt",()=>
                {
                    Require(Volatile.Read(ref giftRejected)==0,"ordinary ally gift did not debit and credit exactly one");
                    if(observationFailure!=null)throw observationFailure;
                    return Volatile.Read(ref giftReceived)!=0;
                });
                Info("GIFT_RECRUIT_DELIVERED player="+player+" good="+good+" debit=1 credit=1 sequence="+giftSequence);
                double next=0;
                var recruitClock=Stopwatch.StartNew();
                while(Volatile.Read(ref recruitStockDrop)==0)
                {
                    Require(Volatile.Read(ref interveningSale)==0,"a native market sale removed or replaced the gift window");
                    if(observationFailure!=null)throw observationFailure;
                    if(recruitClock.Elapsed.TotalSeconds>=next)
                    {
                        State progress=run.InSimulation(mission,()=>Read(player,good));
                        Info("GIFT_RECRUIT_WAIT unit="+recruitedUnit+" stock="+progress.Stock+" pending="+progress.Pending+
                            " credit="+progress.Credit+" sequence="+progress.Sequence);
                        Require(progress.Stock<=1&&progress.Sequence==giftSequence,
                            "the gift window was replaced by a market purchase or a different native order");
                        next+=5;
                    }
                    Require(recruitClock.Elapsed.TotalSeconds<45,"no native recruitment consumed the gift within 45 seconds");
                    yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(100));
                }
                RequireState(recruitedState,"after-natural-recruitment");
                Require(recruitedState.Sequence==giftSequence,"native recruitment changed the tracked purchase identity");
                Info("GIFT_RECRUIT_PASS player="+player+" good="+good+" source="+recruitedState.Source.ToString("X")+
                    " sequence="+giftSequence+" tick="+recruitTick+" unit="+recruitedUnit+" pending=4 package=5 credited=1 stock=0");
            }
            finally
            {
                Volatile.Write(ref giftArmed,0);
                giftRecruitObserver.SetEnabled(false);
                if(run.CurrentMission?.SessionId==mission.SessionId)
                    run.InSimulation(mission,()=>{Call(aic,"RestoreAll");return true;});
            }
        }

        private static void OnSingleGift(AllyTransferPostEventArgs args)
        {
            if(Volatile.Read(ref giftArmed)==0)return;
            var result=args.Result;
            if(result.SenderPlayerId!=giftDonor||result.RecipientPlayerId!=player||args.GoodId!=good)return;
            if(result.RequestedAmount==1&&result.TransferredAmount==1&&result.SenderDebit==1&&result.RecipientCredit==1)
                Volatile.Write(ref giftReceived,1);
            else Volatile.Write(ref giftRejected,1);
        }

        private static long ObserveRecruitStart(AiMarketContext context)
        {
            if(Volatile.Read(ref giftArmed)==0||Volatile.Read(ref giftReceived)==0||context.PlayerId!=player)return 0;
            if(context.Operation==AiMarketOperation.Sell&&context.GoodIndex==good)return 2;
            if(context.Operation!=AiMarketOperation.Recruitment)return 0;
            return Read(player,good).Stock==1?1:0;
        }

        private static void ObserveRecruitEnd(AiMarketContext context,long token,bool completed,int result)
        {
            if(!completed||token==0||Volatile.Read(ref recruitStockDrop)!=0)return;
            State after=Read(player,good);
            if(after.Stock!=0)return;
            if(token==2){Volatile.Write(ref interveningSale,1);return;}
            if(Volatile.Read(ref recruitedUnit)<=0)return;
            CheckRecruitmentStock(SimulationTickEvents.TryGetCurrentTick(out int tick)?tick:0);
        }

        private static void ObserveTransition(UnitTransitionEventArgs args)
        {
            if(Volatile.Read(ref giftArmed)==0||Volatile.Read(ref giftReceived)==0||args.Phase!=EventHookPhase.Pre||
                args.Source!=UnitTransitionSource.EuropeanBarracks||args.PlayerOwnerId!=player||
                args.NextUnitType!=eChimps.CHIMP_TYPE_MACEMAN||args.UnitId<=0)return;
            try { if(Read(player,good).Stock==1)Volatile.Write(ref recruitedUnit,args.UnitId); }
            catch(Exception error){observationFailure=error;}
        }

        private static void CheckRecruitmentStock(int tick)
        {
            if(Volatile.Read(ref giftArmed)==0||Volatile.Read(ref recruitedUnit)<=0||Volatile.Read(ref recruitStockDrop)!=0)return;
            State after=Read(player,good);
            if(after.Stock!=0)return;
            recruitedState=after;
            recruitTick=tick;
            Volatile.Write(ref recruitStockDrop,1);
        }
    }
}
