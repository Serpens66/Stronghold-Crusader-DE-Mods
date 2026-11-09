#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Commands;
using APIShared.Economy;
using APIShared.Presentation;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using UnityEngine;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLPlugin
    {
        partial void TryStartAllyGoodsAcceptance()
        {
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", PluginGuid + ".run");
            if (!File.Exists(request))
                return;

            try
            {
                string[] lines = File.ReadAllLines(request);
                if (lines.Length != 2 || (lines[1] != "ally-goods" && lines[1] != "normal-selection") ||
                    !Guid.TryParseExact(lines[0], "N", out Guid runId) || runId == Guid.Empty)
                    throw new InvalidDataException("Expected a run GUID and a supported QoL scenario.");
                File.Delete(request);

                string fixture = Path.Combine(Paths.BepInExRootPath, "SerpAutomationRollback", "fixture.sav");
                const string expectedSha = "E610341C2638AC0B6067A3753E90DB437FB6AFC4FB74FE63707A1DB891D44F27";
                using (var input = File.OpenRead(fixture))
                using (var sha = SHA256.Create())
                    if (!string.Equals(BitConverter.ToString(sha.ComputeHash(input)).Replace("-", ""), expectedSha,
                        StringComparison.Ordinal))
                        throw new InvalidDataException("The staged ally-goods fixture has an unexpected SHA-256.");

                var acceptance = new AllyGoodsAcceptance(Logger);
                AutomationStep exercise;
                if (lines[1] == "normal-selection")
                {
                    exercise = new AutomationStep("normal-selection", TimeSpan.FromSeconds(35),
                        acceptance.CheckSelection, missionBound: true);
                }
                else
                {
                    if (runtime.AllyGoodsAmountDisplay == null)
                        throw new InvalidOperationException("QoL did not install the ally-goods controls.");
                    Settings.EnableClientFeatures = true;
                    Settings.EnableAllyGoodsAmountModifiers = true;
                    exercise = new AutomationStep("ally-goods-confirm-once", TimeSpan.FromSeconds(40),
                        acceptance.Check, missionBound: true);
                }

                ApiShared.ForMod(PluginGuid).Automation.Start(runId, new[]
                {
                    new AutomationStep("ally-goods-load", TimeSpan.FromSeconds(120), run => run.LoadSave(fixture)),
                    exercise
                }, Logger, quitWhenDone: true);
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogError(Logger, "QOL_ALLY_GOODS_START_FAILED " + error);
            }
        }

        private sealed class AllyGoodsAcceptance
        {
            private const int Sender = 1;
            private const int Recipient = 2;
            private const int Amount = 10;
            private const eGoods Good = eGoods.STORED_GOLD;
            private readonly ManualLogSource log;
            private int armed, managedCalls, successfulManagedCalls, nativeCalls, balancedNativeCalls;
            private int requestArmed, requestCalls, vetoedRequests;

            internal AllyGoodsAcceptance(ManualLogSource log) => this.log = log;

            internal IEnumerable<AutomationWait> CheckSelection(AutomationRun run)
            {
                var mission = run.CurrentMission;
                Require(mission != null && mission.IsSave && mission.Mode.IsSingleplayerSkirmish &&
                    !mission.Mode.IsRealMultiplayer && mission.LocalPlayerId == Sender,
                    "Expected the isolated local save with player 1 controlled.");
                yield return run.WaitForPlayState(mission);
                foreach (var wait in run.SetPause(mission, false))
                    yield return wait;
                yield return run.WaitForTicks(mission, 2);

                Require(run.InSimulation(mission, () =>
                    IsLocalView() && GamePlayerManagerAPI.Instance.SetSelectedChimps(new List<int>())),
                    "The local selection clear was refused.");
                yield return run.WaitForTicks(mission, 3);
                yield return AutomationWait.Until("empty local selection", () =>
                    LocalSelectionAPI.TryCapture(Sender, out var snapshot) && snapshot.Count == 0);

                var target = run.InSimulation(mission, () =>
                {
                    Require(IsLocalView(), "The controlled/viewed player changed before selection.");
                    var selected = FindSelectionTarget();
                    Require(GamePlayerManagerAPI.Instance.SetSelectedChimps(new List<int> { selected.UnitId }),
                        "The local unit selection was refused.");
                    return selected;
                });
                yield return run.WaitForTicks(mission, 3);
                yield return AutomationWait.Until("selected local unit and troop HUD", () =>
                    IsLocalView() && LocalSelectionAPI.TryCapture(Sender, out var snapshot) &&
                    snapshot.Count == 1 && snapshot[0].UnitId == target.UnitId &&
                    snapshot[0].UnitType == (int)target.Type &&
                    MainViewModel.viewModelLoaded && MainViewModel.Instance.Show_HUD_Troops);
                Require(run.InSimulation(mission, () => MatchesSelectionTarget(target)),
                    "The selected unit's ownership or identity changed.");
                Shared.DebugLogHelper.LogInfo(log,
                    $"QOL_NORMAL_SELECTION_PASS session={mission.SessionId} local={Sender} selected=1 unit={target.UnitId} global={target.GlobalId} type={target.Type} hud=visible source=script-extender");
            }

            private static bool IsLocalView() => PlayerPerspectiveAPI.GetControlledPlayerId() == Sender &&
                PlayerPerspectiveAPI.GetViewedPlayerId() == Sender;

            private static unsafe (int UnitId, uint GlobalId, eChimps Type) FindSelectionTarget()
            {
                foreach (int unitId in UnitAccess.GetAllReallyAliveUnits())
                {
                    if (UnitAccess.TryGetById(unitId, out var unit, out _) && unit->r_ControllableForPlayerId == Sender &&
                        unit->r_GlobalId != 0 && (unit->r_UnitChimp == eChimps.CHIMP_TYPE_ARCHER ||
                        unit->r_UnitChimp == eChimps.CHIMP_TYPE_SPEARMAN || unit->r_UnitChimp == eChimps.CHIMP_TYPE_SWORDSMAN))
                        return (unitId, unit->r_GlobalId, unit->r_UnitChimp);
                }

                int lordId = GamePlayerManagerAPI.Instance.GetLordUnitId(Sender);
                if (UnitAccess.TryGetById(lordId, out var lord, out _) && UnitAccess.IsReallyAlive(lord) &&
                    lord->r_ControllableForPlayerId == Sender && lord->r_GlobalId != 0 &&
                    lord->r_UnitChimp == eChimps.CHIMP_TYPE_LORD)
                    return (lordId, lord->r_GlobalId, lord->r_UnitChimp);
                throw new InvalidOperationException("No eligible local unit was found in the saved fixture.");
            }

            private static unsafe bool MatchesSelectionTarget((int UnitId, uint GlobalId, eChimps Type) target) =>
                UnitAccess.TryGetById(target.UnitId, out var unit, out _) && UnitAccess.IsReallyAlive(unit) &&
                unit->r_ControllableForPlayerId == Sender && unit->r_GlobalId == target.GlobalId &&
                unit->r_UnitChimp == target.Type;

            internal IEnumerable<AutomationWait> Check(AutomationRun run)
            {
                var mission = run.CurrentMission;
                Require(mission != null && mission.IsSave && mission.Mode.IsSingleplayerSkirmish &&
                    !mission.Mode.IsRealMultiplayer && mission.LocalPlayerId == Sender,
                    "Expected the isolated local save with player 1 controlled.");
                yield return run.WaitForPlayState(mission);
                foreach (var wait in run.SetPause(mission, false))
                    yield return wait;
                yield return run.WaitForTicks(mission, 2);

                Require(run.InSimulation(mission, () => {
                    var players = GamePlayerManagerAPI.Instance;
                    int team = players.GetPlayerTeam(Sender);
                    int hostileTeam = players.GetPlayerTeam(4);
                    return team > 0 && hostileTeam > 0 && hostileTeam != team &&
                        !players.IsAIPlayer(Sender) && players.IsAIPlayer(Recipient) &&
                        players.IsAIPlayer(3) && players.IsAIPlayer(4) &&
                        players.GetPlayerTeam(Recipient) == team && players.GetPlayerTeam(3) == team;
                }), "Loaded save did not expose the expected two allies and one enemy.");
                yield return AutomationWait.Until("native ally transfer observer available", () => AllyTransfers.IsAvailable);
                Require(GameActionEvents.TryRegister(PluginGuid, "diag-ally-goods-intent",
                    VetoDiagnosticRequest, ObserveManaged, out string reason), reason);
                Require(AllyTransferEvents.TryRegister(PluginGuid, "diag-ally-goods-execution",
                    null, ObserveNative, out reason), reason);

                HUD_AlliesPanel.Open(true);
                var main = MainViewModel.Instance;
                var panel = main.HUDAlliesPanel;
                Require(panel != null, "The actual ally HUD is unavailable.");
                panel.ButtonClicked(FindRecipientSlot(panel).ToString(CultureInfo.InvariantCulture));
                panel.ButtonClicked("Send");
                panel.ButtonClicked("G" + ((int)Good).ToString("00", CultureInfo.InvariantCulture));
                for (int attempt = 0; attempt < 128 && ReadSelection().Amount > 0; attempt++)
                    panel.ButtonClicked("X500-");
                Require(ReadSelection().Amount.GetValueOrDefault() == 0, "Could not clear a prefilled quantity.");
                Require(!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift) &&
                    !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl),
                    "Release Shift and Ctrl before the unmodified amount case.");
                panel.ButtonClicked("X10");
                Require(ReadSelection().Amount == Amount && main.Allies_GoodConfirmVis,
                    "The unmodified X10 command did not produce a confirmable amount of 10.");
                panel.ButtonClicked("X10-");
                Require(ReadSelection().Amount == 0, "Unmodified subtract did not clear the amount.");

                CheckModifiedAmount(panel, shift: true, control: false, expected: 50);
                CheckModifiedAmount(panel, shift: false, control: true, expected: 2);
                CheckModifiedAmount(panel, shift: true, control: true, expected: Amount, keep: true);
                Require(main.Allies_GoodConfirmVis, "The amount after both modifiers was not confirmable.");
                Shared.DebugLogHelper.LogInfo(log, $"QOL_ALLY_GOODS_MODIFIERS_PASS session={mission.SessionId} baseline=10 shift=50 ctrl=2 both=10 source=devdiag");

                Volatile.Write(ref armed, 1);
                try
                {
                    panel.ButtonClicked("ConfirmGoods");
                    Require(ReadSelection().Amount == Amount, "QoL confirmation did not leave the same selection open.");
                    yield return AutomationWait.Until("native ally gift receipt", () => Volatile.Read(ref nativeCalls) > 0);
                    const int followupTicks = 40;
                    yield return run.WaitForTicks(mission, followupTicks);
                    Require(Volatile.Read(ref managedCalls) == 1 && Volatile.Read(ref successfulManagedCalls) == 1 &&
                        Volatile.Read(ref nativeCalls) == 1 && Volatile.Read(ref balancedNativeCalls) == 1,
                        $"Expected exactly one managed command and balanced gift, got managed={managedCalls}/{successfulManagedCalls} native={nativeCalls}/{balancedNativeCalls}.");
                    Shared.DebugLogHelper.LogInfo(log, $"QOL_ALLY_GOODS_PASS session={mission.SessionId} managed=1 native=1 debit=10 credit=10 followupTicks={followupTicks}");
                }
                finally { Volatile.Write(ref armed, 0); }

                panel.ButtonClicked("Back");
                panel.ButtonClicked(FindRecipientSlot(panel).ToString(CultureInfo.InvariantCulture));
                panel.ButtonClicked("Request");
                panel.ButtonClicked("G" + ((int)Good).ToString("00", CultureInfo.InvariantCulture));
                panel.ButtonClicked("X10");
                Require(ReadSelection(send: false).Amount == Amount && main.Allies_GoodConfirmVis,
                    "The ordinary request did not produce a confirmable amount of 10.");
                Volatile.Write(ref requestArmed, 1);
                try
                {
                    panel.ButtonClicked("ConfirmGoods");
                    yield return AutomationWait.Until("managed goods request veto receipt", () => Volatile.Read(ref requestCalls) > 0);
                    yield return run.WaitForTicks(mission, 2);
                    Require(Volatile.Read(ref requestCalls) == 1 && Volatile.Read(ref vetoedRequests) == 1,
                        $"Expected one managed request veto, got requests={requestCalls} matchingVeto={vetoedRequests}.");
                    Shared.DebugLogHelper.LogInfo(log, $"QOL_ALLY_GOODS_REQUEST_PASS session={mission.SessionId} managed=1 vetoed=1 native=not-executed");
                }
                finally { Volatile.Write(ref requestArmed, 0); }
            }

            private static void CheckModifiedAmount(HUD_AlliesPanel panel, bool shift, bool control,
                int expected, bool keep = false)
            {
                AllyGoodsAmountModifierHook.WithDiagnosticModifiers(shift, control, () =>
                {
                    panel.ButtonClicked("X10");
                    Require(ReadSelection().Amount == expected,
                        $"Expected amount {expected} with Shift={shift} and Ctrl={control}.");
                    if (!keep)
                    {
                        panel.ButtonClicked("X10-");
                        Require(ReadSelection().Amount == 0, "Modified subtract did not clear the amount.");
                    }
                });
            }

            private static AllyGoodsPanelContext ReadSelection(bool send = true)
            {
                Require(AllyGoodsPanelExtensions.TryGetContext(out var selection) &&
                    (send ? selection.IsSendMode : selection.IsRequestMode) &&
                    selection.LocalPlayerId == Sender && selection.ViewedPlayerId == Sender &&
                    selection.RecipientPlayerId == Recipient && selection.Good == Good,
                    "The selected recipient, ware or player viewpoint changed.");
                return selection;
            }

            private static int FindRecipientSlot(HUD_AlliesPanel panel)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var ids = typeof(HUD_AlliesPanel).GetField("ally", flags)?.GetValue(panel) as int[];
                object count = typeof(HUD_AlliesPanel).GetField("num_allies", flags)?.GetValue(panel);
                if (ids == null || !(count is int size) || size <= 0 || size > ids.Length)
                    throw new InvalidDataException("The validated ally roster is unavailable.");
                int index = Array.IndexOf(ids, Recipient, 0, size);
                Require(index >= 0, "The expected AI ally is missing from the visible HUD roster.");
                return index;
            }

            private void VetoDiagnosticRequest(GameActionPreEventArgs args)
            {
                // This probe checks managed routing. An AI gold request's native write path is unqualified.
                if (Volatile.Read(ref requestArmed) != 0 && args.Command == Enums.GameActionCommand.Ally_RequestGoods)
                    args.SkipOriginalFunction = true;
            }

            private void ObserveManaged(GameActionPostEventArgs args)
            {
                if (Volatile.Read(ref requestArmed) != 0 && args.Command == Enums.GameActionCommand.Ally_RequestGoods)
                {
                    if (args.WasSkipped && args.StructureId == Recipient && args.ActionState == (int)Good &&
                        args.Value2 == Amount)
                        Interlocked.Increment(ref vetoedRequests);
                    Interlocked.Increment(ref requestCalls);
                    return;
                }
                if (Volatile.Read(ref armed) == 0 || args.Command != Enums.GameActionCommand.Ally_SendGoods)
                    return;
                if (!args.WasSkipped && args.Result == 0 && args.StructureId == Recipient &&
                    args.ActionState == (int)Good && args.Value2 == Amount)
                    Interlocked.Increment(ref successfulManagedCalls);
                Interlocked.Increment(ref managedCalls);
            }

            private void ObserveNative(AllyTransferPostEventArgs args)
            {
                var result = args.Result;
                if (Volatile.Read(ref armed) == 0 || result.SenderPlayerId != Sender)
                    return;
                if (result.RecipientPlayerId == Recipient && args.GoodId == (int)Good &&
                    result.RequestedAmount == Amount && result.Status == AllyTransferStatus.Completed &&
                    result.OriginalWasCalled && !result.HasNestedTransfer &&
                    result.SenderDebit == Amount && result.RecipientCredit == Amount)
                    Interlocked.Increment(ref balancedNativeCalls);
                Interlocked.Increment(ref nativeCalls);
            }

            private static void Require(bool condition, string error)
            {
                if (!condition)
                    throw new InvalidOperationException(error);
            }
        }
    }

    internal sealed partial class AllyGoodsAmountModifierHook
    {
        [ThreadStatic] private static bool? diagnosticShift;
        [ThreadStatic] private static bool? diagnosticControl;

        internal static void WithDiagnosticModifiers(bool shift, bool control, Action click)
        {
            bool? oldShift = diagnosticShift;
            bool? oldControl = diagnosticControl;
            diagnosticShift = shift;
            diagnosticControl = control;
            try { click(); }
            finally
            {
                diagnosticShift = oldShift;
                diagnosticControl = oldControl;
            }
        }

        static partial void OverrideDiagnosticKey(KeyCode left, ref bool held)
        {
            if (left == KeyCode.LeftShift)
                held = diagnosticShift ?? held;
            else if (left == KeyCode.LeftControl)
                held = diagnosticControl ?? held;
        }
    }
}
#endif
