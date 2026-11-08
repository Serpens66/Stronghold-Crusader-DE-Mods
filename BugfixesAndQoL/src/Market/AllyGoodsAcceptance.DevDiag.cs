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
                if (lines.Length != 2 || lines[1] != "ally-goods" ||
                    !Guid.TryParseExact(lines[0], "N", out Guid runId) || runId == Guid.Empty)
                    throw new InvalidDataException("Expected a run GUID and the ally-goods scenario.");
                File.Delete(request);

                string fixture = Path.Combine(Paths.BepInExRootPath, "SerpAutomationRollback", "fixture.sav");
                const string expectedSha = "E610341C2638AC0B6067A3753E90DB437FB6AFC4FB74FE63707A1DB891D44F27";
                using (var input = File.OpenRead(fixture))
                using (var sha = SHA256.Create())
                    if (!string.Equals(BitConverter.ToString(sha.ComputeHash(input)).Replace("-", ""), expectedSha,
                        StringComparison.Ordinal))
                        throw new InvalidDataException("The staged ally-goods fixture has an unexpected SHA-256.");

                if (runtime.AllyGoodsAmountDisplay == null)
                    throw new InvalidOperationException("QoL did not install the ally-goods controls.");
                Settings.EnableClientFeatures = true;
                Settings.EnableAllyGoodsAmountModifiers = true;
                var acceptance = new AllyGoodsAcceptance(Logger);
                ApiShared.ForMod(PluginGuid).Automation.Start(runId, new[]
                {
                    new AutomationStep("ally-goods-load", TimeSpan.FromSeconds(120), run => run.LoadSave(fixture)),
                    new AutomationStep("ally-goods-confirm-once", TimeSpan.FromSeconds(40),
                        acceptance.Check, missionBound: true)
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

            internal AllyGoodsAcceptance(ManualLogSource log) => this.log = log;

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
                    null, ObserveManaged, out string reason), reason);
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
            }

            private static AllyGoodsPanelContext ReadSelection()
            {
                Require(AllyGoodsPanelExtensions.TryGetContext(out var selection) &&
                    selection.IsSendMode && selection.LocalPlayerId == Sender && selection.ViewedPlayerId == Sender &&
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

            private void ObserveManaged(GameActionPostEventArgs args)
            {
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
}
#endif
