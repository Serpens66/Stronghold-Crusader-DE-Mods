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

namespace SpectatorEditorBuildTest
{
    public sealed partial class SpectatorEditorBuildTestPlugin
    {
        private static readonly ConcurrentQueue<TestAction> testActions = new ConcurrentQueue<TestAction>();
        private static readonly TestActionLedger actionLedger = new TestActionLedger();
        private static long nextTestAction;
        private static TestToolsViewModel toolsModel;
        private static IDisposable forceAttackSubscription, forceDefendSubscription;
        [ThreadStatic] private static ForcedOrderContext forcedOrder;
        private static int lastRenderFrame = -1;
        private static readonly ConcurrentQueue<Action> presentationCallbacks = new ConcurrentQueue<Action>();
        private static object engineLock;

        private sealed class TestAction
        {
            internal long Session, Operation, PowerGeneration;
            internal TestActionKind Kind;
            internal int Actor, Target, Requester, Amount;
            internal eGoods Good;
            internal bool Force;
            internal ToolPower Power;
            internal int Tile;
            internal GateSelection Gate;
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
            if (!IsCurrentSession(CurrentSessionId) || !MainViewModel.viewModelLoaded ||
                GameData.Instance?.lastGameState == null) return false;
            return !GameModeHelper.IsRealMultiplayer() && Director.instance != null &&
                !Director.instance.MultiplayerGame;
        }

        private static bool IsEditorToolsSession() => IsToolsSession() && GameModeHelper.IsMapEditor();

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

        private static string PlayerLabel(int owner)
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
            return owner + " · " + name;
        }

        private static void QueueCredit(eGoods good, string text)
        {
            if (!CreditMath.TryParse(text, out int amount)) { toolsModel.Status = SerpLocalization.Get("Tools.InvalidAmount"); return; }
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
            if (!IsToolsSession() || (action.Kind != TestActionKind.PowerToggle && !IsToolsPlayer(action.Actor, true)))
            { if (toolsModel != null) toolsModel.Status = SerpLocalization.Get("Tools.InvalidTarget"); return; }
            action.Session = CurrentSessionId;
            action.PowerGeneration = powerGeneration;
            action.Operation = Interlocked.Increment(ref nextTestAction);
            testActions.Enqueue(action);
            toolsModel.Status = SerpLocalization.Get("Tools.Queued");
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
                            (action.Kind != TestActionKind.PowerToggle && !IsToolsPlayer(action.Actor, true)))
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
                                result = SerpLocalization.Get(action.Force ? "Tools.PowersOn" : "Tools.PowersOff"); break;
                            default: result = ExecuteOrder(action); break;
                        }
                    }
                    if (toolsModel != null) toolsModel.Status = result;
                    log.LogInfo("TEST_ACTION session=" + action.Session + " operation=" + action.Operation +
                        " kind=" + action.Kind + " actor=" + action.Actor + " target=" + action.Target +
                        " force=" + action.Force + " amount=" + action.Amount + " good=" + action.Good +
                        " power=" + action.Power?.Id + " tile=" + action.Tile + " building=" + action.Gate?.Id + " result=" + result);
                }
                catch (Exception error) { LogOnce("TEST_ACTION_ERROR: " + error); if (toolsModel != null) toolsModel.Status = SerpLocalization.Get("Tools.Failed"); }
            }
        }

        private static unsafe string ExecuteCredit(TestAction action)
        {
            if (!TestToolsViewModel.IsGrantable(action.Good) || action.Amount <= 0 ||
                !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(action.Actor, out GamePlayerResources* resources))
                return SerpLocalization.Get("Tools.InvalidTarget");
            var players = GamePlayerManagerAPI.Instance;
            if (action.Good == eGoods.STORED_GOLD)
            {
                int before = players.GetPlayerGold(action.Actor);
                if (!CreditMath.Fits(before, action.Amount)) return SerpLocalization.Get("Tools.Overflow");
                if (!players.AddPlayerGold(action.Actor, action.Amount) || players.GetPlayerGold(action.Actor) != before + action.Amount)
                    throw new InvalidOperationException("Gold postcondition failed.");
                return SerpLocalization.Get("Tools.GoldCredited", "Amount", action.Amount, "Player", PlayerLabel(action.Actor));
            }
            int* incoming = (int*)(&resources->r_incomingNull);
            int value = incoming[(int)action.Good];
            int stored = players.GetGoodAmount(action.Actor, action.Good);
            if (value < 0 || stored < 0 || !CreditMath.Fits((long)value + stored, action.Amount))
                return SerpLocalization.Get("Tools.Overflow");
            players.AddIncomingGood(action.Actor, action.Good, action.Amount);
            if (incoming[(int)action.Good] != value + action.Amount) throw new InvalidOperationException("Incoming goods postcondition failed.");
            return SerpLocalization.Get("Tools.GoodsCredited", "Amount", action.Amount, "Player", PlayerLabel(action.Actor));
        }

        private static unsafe string ExecuteOrder(TestAction action)
        {
            if (nativeOrder == null) return SerpLocalization.Get("Tools.NativeUnavailable");
            var players = GamePlayerManagerAPI.Instance;
            if (!players.IsAIPlayer(action.Actor) || !players.TryGetPlayerResourcesById(action.Actor, out GamePlayerResources* resources) ||
                resources->r_LordUnitId == 0 || !IsToolsPlayer(action.Requester, false))
                return SerpLocalization.Get("Tools.AiNotRunning");
            if (action.Kind != TestActionKind.Cancel && !IsToolsPlayer(action.Target, false)) return SerpLocalization.Get("Tools.InvalidTarget");
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
            return SerpLocalization.Get(accepted ? "Tools.OrderAccepted" : "Tools.OrderRejected");
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
