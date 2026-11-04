using APIShared;
using APIShared.GameModes;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Player;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Reflection;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static readonly ConcurrentQueue<TestAction> testActions = new ConcurrentQueue<TestAction>();
        private static readonly TestActionLedger actionLedger = new TestActionLedger();
        private static long nextTestAction;
        private static TestToolsViewModel toolsModel;
        private static IDisposable forceAttackSubscription, forceDefendSubscription;
        [ThreadStatic] private static ForcedOrderContext forcedOrder;
        private static readonly ConcurrentQueue<Action> presentationCallbacks = new ConcurrentQueue<Action>();
        private static object engineLock;

        private sealed class TestAction
        {
            internal long Session, Operation, PowerGeneration;
            internal TestActionKind Kind;
            internal string StatusPage;
            internal int Actor, Target, Requester, Amount;
            internal eGoods Good;
            internal bool Force;
            internal ToolPower Power;
            internal int Tile;
            internal GateSelection Gate;
            internal CreditTrace Credit;
            internal string CreditError;
        }

        private static void RegisterTestActions()
        {
            // Vanilla's monitor is private in the real assembly. Resolve once; never access a publicized field directly.
            FieldInfo monitor = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
            if (monitor == null || !monitor.IsPrivate || monitor.FieldType != typeof(object) ||
                (engineLock = monitor.GetValue(null)) == null)
                throw new MissingFieldException("EngineInterface", "private static object threadLock");
            // Retain subscribers for process lifetime. No global AI acceptance override exists.
            forceAttackSubscription = PlayerR3EventHooks.OnPlayerAIEvaluateAttackOrder.Observable.Subscribe(args => {
                if (args.Phase != EventHookPhase.Pre || forcedOrder == null ||
                    !forcedOrder.Matches(TestActionKind.Attack, args.TargetPlayerId,
                        args.SourcePlayerId, args.ContextPlayerId)) return;
                args.ReturnValue = true;
                args.SkipOriginalFunction = true;
            });
            forceDefendSubscription = PlayerR3EventHooks.OnPlayerAIEvaluateDefendOrder.Observable.Subscribe(args => {
                if (args.Phase != EventHookPhase.Pre || forcedOrder == null ||
                    !forcedOrder.Matches(TestActionKind.Defend, args.TargetPlayerId,
                        args.SourcePlayerId, args.SourcePlayerId)) return;
                args.ReturnValue = true;
                args.SkipOriginalFunction = true;
            });
        }

        private static bool IsToolsSession()
        {
            // Avoid touching mode services before the managed game view exists.
            if (settings == null || !IsCurrentSession(CurrentSessionId) ||
                !MainViewModel.viewModelLoaded || GameData.Instance?.lastGameState == null) return false;
            bool enabled = ToolSessionPolicy.Enabled(settings.Activation.TestTools, settings.Activation.SingleplayerTest,
                GameModeHelper.IsMapEditor(), SpectatorPerspectiveRuntime.IsSpectatorObserver(),
                PlayerPerspectiveAPI.GetControlledPlayerId() > 0);
            return ToolSessionPolicy.CanMutate(enabled,
                IsCurrentSession(CurrentSessionId), MainViewModel.viewModelLoaded,
                GameData.Instance?.lastGameState != null, GameModeHelper.IsRealMultiplayer(),
                Director.instance != null, Director.instance != null && Director.instance.MultiplayerGame);
        }

        private static bool IsEditorToolsSession() => IsToolsSession() && GameModeHelper.IsMapEditor();

        private static bool IsActivePlayerTestSession() => IsToolsSession() && !GameModeHelper.IsMapEditor() &&
            !SpectatorPerspectiveRuntime.IsSpectatorObserver() && PlayerPerspectiveAPI.GetControlledPlayerId() > 0;

        // The local view uses a normal build menu: an active test player or an input-controlled spectator.
        private static bool HasOwnBuildMenu() => IsActivePlayerTestSession() || IsControlledBuildSession();

        private static bool IsPlacementPlayer(int owner) => IsSelectableAi(owner) ||
            IsActivePlayerTestSession() && owner == PlayerPerspectiveAPI.GetControlledPlayerId() && IsToolsPlayer(owner, false);

        private static bool IsOrderTarget(int owner) => OrderTargetPolicy.IsAvailable(
            IsToolsPlayer(owner, false), SpectatorDefeat.IsPlayerEliminated(owner));

        private static unsafe bool IsToolsPlayer(int owner, bool requireAi)
        {
            if (!IsToolsSession() || owner < 1 || owner > 8 ||
                !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner, out GamePlayerResources* resources) ||
                resources == null) return false;
            // Editor slots can be configured before a Lord/AI has been initialized.
            if (IsEditorToolsSession()) return true;
            var state = GameData.Instance.lastGameState;
            if (!state.is_human_or_skirmish_player(owner) ||
                (requireAi && !GamePlayerManagerAPI.Instance.IsAIPlayer(owner))) return false;
            return resources->r_WinLossState != WinLossState.Loss && resources->r_LordUnitId > 0;
        }

        private static string PlayerLabel(int owner, bool includeNumber = true)
        {
            string name = null;
            try
            {
                var state = GameData.Instance?.lastGameState;
                // This static Vanilla entry also passes through the Extender's Custom-Lord name hook.
                if (state != null && state.is_skirmish_player(owner) &&
                    state.computer_names != null && owner < state.computer_names.Length)
                    name = OnScreenText.getComputerName(state.computer_register[owner], state.computer_names[owner]);
                else name = Platform_Multiplayer.Instance?.getSkirmishName(owner);
            }
            catch (Exception error) { LogOnce("TEST_PLAYER_NAME_ERROR: " + error); }
            if (string.IsNullOrWhiteSpace(name)) name = SerpLocalization.Get("Tools.Player");
            return includeNumber ? owner + " · " + name : name;
        }

        private static void QueueCredit(eGoods good, string text, bool debit)
        {
            if (!CreditMath.TryAmount(text, debit, out int amount)) { toolsModel.SetActionStatus(TestActionKind.Credit, SerpLocalization.Get("Tools.InvalidAmount")); return; }
            QueueTestAction(new TestAction { Kind = TestActionKind.Credit, Actor = humanSelectedOwner,
                Good = good, Amount = amount });
        }

        private static void QueueOrder(TestActionKind kind, int target, bool force)
        {
            int requester = kind == TestActionKind.Defend ? target : PlayerPerspectiveAPI.GetControlledPlayerId();
            if (!IsToolsPlayer(requester, false)) requester = humanSelectedOwner;
            QueueTestAction(new TestAction { Kind = kind, Actor = humanSelectedOwner,
                Target = target, Requester = requester, Force = force });
        }

        private static void QueueTestAction(TestAction action)
        {
            action.StatusPage = toolsModel?.ActiveViewKey;
            if (!IsToolsSession() || (action.Kind != TestActionKind.PowerToggle && !IsToolsPlayer(action.Actor, action.Kind != TestActionKind.Credit)))
            { ReportTestAction(action, SerpLocalization.Get("Tools.InvalidTarget")); return; }
            action.Session = CurrentSessionId;
            action.PowerGeneration = powerGeneration;
            action.Operation = Interlocked.Increment(ref nextTestAction);
            testActions.Enqueue(action);
            RequestRender();
            ReportTestAction(action, SerpLocalization.Get("Tools.Queued"));
        }

        private static void ReportTestAction(TestAction action, string message)
        {
            toolsModel?.SetActionStatus(action.Kind, message, action.StatusPage);
        }

        private static void DrainTestActions()
        {
            // Runs once per Unity render frame, including pause/editor; identical lock to Vanilla native calls.
            while (testActions.TryDequeue(out TestAction action))
            {
                string result;
                try
                {
                    lock (engineLock)
                    {
                        if (!IsToolsSession() || !IsCurrentSession(action.Session) ||
                            !actionLedger.TryBegin(action.Session, action.Operation) ||
                            (action.Kind != TestActionKind.PowerToggle && !IsToolsPlayer(action.Actor, action.Kind != TestActionKind.Credit)))
                            continue;
                        switch (action.Kind)
                        {
                            case TestActionKind.Credit: result = ExecuteCredit(action); break;
                            case TestActionKind.Power: result = ExecutePower(action); break;
                            case TestActionKind.Gate: result = ExecuteGate(action); break;
                            case TestActionKind.GateAutomation:
                                if (!GateIsCurrent(action.Gate) || action.Actor != humanSelectedOwner || gateAutomation == null)
                                    result = SerpLocalization.Get("Tools.InvalidTarget");
                                else { gateAutomation.Apply(action.Actor, action.Gate.Id, action.Gate.Global, action.Force);
                                    result = SerpLocalization.Get(action.Force ? "Tools.GateAutomatic" : "Tools.GateFixed"); }
                                break;
                            case TestActionKind.PowerToggle:
                                CancelPowerSelection();
                                GamePlayerManagerAPI.Instance.SetLocalPlayerExtremePowersEnabled(action.Force);
                                toolsModel?.RefreshPowerFlag(GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled());
                                result = ""; break;
                            default: result = ExecuteOrder(action); break;
                        }
                    }
                    ReportTestAction(action, result);
                    log.LogInfo("TEST_ACTION session=" + action.Session + " operation=" + action.Operation +
                        " kind=" + action.Kind + " actor=" + action.Actor + " target=" + action.Target +
                        " force=" + action.Force + " amount=" + action.Amount + " good=" + action.Good +
                        " power=" + action.Power?.Id + " tile=" + action.Tile + " building=" + action.Gate?.Id +
                        action.Credit?.LogFields + " result=" + result);
                    if (action.CreditError != null)
                        log.LogError("TEST_CREDIT_ERROR session=" + action.Session + " operation=" + action.Operation +
                            " actor=" + action.Actor + " amount=" + action.Amount + " good=" + action.Good +
                            action.Credit?.LogFields + ": " + action.CreditError);
                }
                catch (Exception error) { LogOnce("TEST_ACTION_ERROR: " + error); ReportTestAction(action, SerpLocalization.Get("Tools.Failed")); }
            }
        }

        private static unsafe string ExecuteCredit(TestAction action)
        {
            if (!TestToolsViewModel.IsGrantable(action.Good) || action.Amount == 0 ||
                !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(action.Actor, out GamePlayerResources* resources))
                return SerpLocalization.Get("Tools.InvalidTarget");
            var players = GamePlayerManagerAPI.Instance;
            bool gold = action.Good == eGoods.STORED_GOLD;
            bool reserveOnly = !gold && action.Amount < 0 &&
                players.GetCurrentGameTypeMode() == eGameTypeModes.GAMETYPE_SIEGE_THAT_BUILDER;
            // Immediate gold credits affect stored gold only; debits still include the delivery reserve.
            action.Credit = new CreditTrace(ReadDebitBalance(action.Actor, action.Good), action.Amount,
                gold && action.Amount > 0, reserveOnly);
            try
            {
                if (action.Amount < 0)
                {
                    long actual = CreditDebit.Execute(action.Amount, reserveOnly,
                        () => action.Credit.Observe(ReadDebitBalance(action.Actor, action.Good)),
                        value => {
                            if (gold) { if (!players.AddPlayerGold(action.Actor, -value)) throw new InvalidOperationException("Gold debit failed."); }
                            else players.RemoveGood(action.Actor, action.Good, value);
                        }, value => players.SubtractIncomingGood(action.Actor, action.Good, value));
                    return SerpLocalization.Get(gold ? "Tools.GoldRemoved" : "Tools.GoodsRemoved",
                        "Amount", actual, "Player", PlayerLabel(action.Actor));
                }
                action.Credit.Failure = CreditMath.Check(action.Credit.Before, action.Amount, gold);
                if (action.Credit.Failure != CreditFailure.None)
                {
                    action.Credit.Observe(action.Credit.Before);
                    return CreditFailureText(action);
                }
                if (gold)
                {
                    if (!players.AddPlayerGold(action.Actor, action.Amount))
                        throw new InvalidOperationException("Gold credit failed.");
                }
                else players.AddIncomingGood(action.Actor, action.Good, action.Amount);
                GoodsBalance after = action.Credit.Observe(ReadDebitBalance(action.Actor, action.Good));
                if (action.Credit.Actual != action.Credit.Expected ||
                    (!gold && (long)after.Incoming != (long)action.Credit.Before.Incoming + action.Amount))
                    throw new CreditAccountingException(CreditFailure.Postcondition, "Credit postcondition failed.");
                return SerpLocalization.Get(gold ? "Tools.GoldCredited" : "Tools.GoodsCredited",
                    "Amount", action.Amount, "Player", PlayerLabel(action.Actor));
            }
            catch (Exception error)
            {
                action.Credit.Failure = error is CreditAccountingException accounting ? accounting.Failure : CreditFailure.Provider;
                action.CreditError = error.ToString();
                // A provider may have changed an account before failing. Observe under the same engine lock.
                action.Credit.ActualKnown = false;
                try { action.Credit.Observe(ReadDebitBalance(action.Actor, action.Good)); }
                catch (Exception readError) { action.CreditError += "\nBalance read failed: " + readError; }
                return CreditFailureText(action);
            }
        }

        private static string CreditFailureText(TestAction action)
        {
            CreditTrace trace = action.Credit;
            string reason = trace.Failure == CreditFailure.NegativeBalance ? "Tools.NegativeBalance" :
                trace.Failure == CreditFailure.Overflow ? "Tools.Overflow" :
                trace.Failure == CreditFailure.Postcondition ? "Tools.BalancePostcondition" : "Tools.BalanceOperationFailed";
            string details = SerpLocalization.Get("Tools.BalanceDetails", "Player", PlayerLabel(action.Actor),
                "Good", Translate.Instance.lookUpText(Enums.eTextSections.TEXT_GOODS, (int)action.Good),
                "Current", trace.Current, "Stored", trace.Before.Stored, "Incoming", trace.Before.Incoming,
                "Amount", trace.Amount, "Expected", trace.Expected,
                "Actual", trace.ActualKnown ? (object)trace.Actual : SerpLocalization.Get("Tools.BalanceUnknown"),
                "Limit", int.MaxValue);
            return SerpLocalization.Get(reason) + " " + details;
        }

        private static unsafe GoodsBalance ReadDebitBalance(int actor, eGoods good)
        {
            var players = GamePlayerManagerAPI.Instance;
            if (!players.TryGetPlayerResourcesById(actor, out GamePlayerResources* resources) || resources == null)
                throw new InvalidOperationException("Resource owner disappeared.");
            int* incoming = (int*)(&resources->r_incomingNull);
            return new GoodsBalance(good == eGoods.STORED_GOLD ? players.GetPlayerGold(actor) : players.GetGoodAmount(actor, good), incoming[(int)good]);
        }

        private static unsafe string ExecuteOrder(TestAction action)
        {
            if (nativeOrder == null) return SerpLocalization.Get("Tools.NativeUnavailable");
            if (!OrderTargetPolicy.CanExecute(action.Kind, IsOrderTarget(action.Actor),
                action.Kind == TestActionKind.Cancel || IsOrderTarget(action.Target)))
                return SerpLocalization.Get("Tools.InvalidTarget");
            var players = GamePlayerManagerAPI.Instance;
            if (!players.IsAIPlayer(action.Actor) || !players.TryGetPlayerResourcesById(action.Actor, out GamePlayerResources* resources) ||
                resources->r_LordUnitId == 0 || !IsToolsPlayer(action.Requester, false))
                return SerpLocalization.Get("Tools.AiNotRunning");
            if (action.Kind == TestActionKind.Attack)
            {
                var state = GameData.Instance.lastGameState;
                if (action.Target == action.Actor || state.teams[action.Target] == state.teams[action.Actor])
                    return SerpLocalization.Get("Tools.EnemyRequired");
            }
            int nativeMode = action.Kind == TestActionKind.Attack ? 1 :
                (int)(action.Kind == TestActionKind.Defend ? AIRequestOrderType.DefendMe : AIRequestOrderType.ClearOrders);
            ForcedOrderContext previous = forcedOrder;
            try
            {
                forcedOrder = action.Force ? new ForcedOrderContext(action.Kind, action.Actor, action.Requester, action.Target) : null;
                nativeOrder(action.Kind == TestActionKind.Attack ? action.Target : 0, action.Actor, action.Requester, nativeMode);
            }
            finally { forcedOrder = previous; }
            uint expected = action.Kind == TestActionKind.Attack ? 1u : action.Kind == TestActionKind.Defend ? 2u : 0u;
            bool accepted = resources->r_AIAllyOrderMode == expected &&
                (expected == 0 || resources->r_AIAllyOrderRequesterPlayerId == action.Requester) &&
                (action.Kind != TestActionKind.Attack || resources->r_AIRequestedAttackTargetPlayerId == action.Target);
            return SerpLocalization.Get(accepted ? (action.Kind == TestActionKind.Cancel ? "Tools.OrderCancelled" : "Tools.OrderAccepted") : "Tools.OrderRejected");
        }

        private static void ResetTestActions(long session)
        {
            while (testActions.TryDequeue(out _)) { }
            nextTestAction = 0;
            actionLedger.Reset(session);
            toolsModel = null;
        }
    }
}
