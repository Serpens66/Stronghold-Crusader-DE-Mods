using CrusaderDE;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using Marshal = System.Runtime.InteropServices.Marshal;
using UnityEngine;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private sealed class PowerSelection
        {
            internal long Session, Generation;
            internal int Actor;
            internal ToolPower Power;
            internal bool Rejected;
        }
        private static PowerSelection pendingPower;
        private static long powerGeneration;
        private static int toolMouseState;
        private static NativePowerContext powerContext;
        [ThreadStatic] private static bool selectingTestPower;
        private static int powerCallbackFailure;
        private static DateTime nextPowerRefresh;
        private static int powerRefreshActor;

        private static void RegisterPowerInput() => RegisterVanillaPowerActions();

        private static ToolPower DescribePower(int id)
        {
            var power = new ToolPower { Id = id, Cost = (id + 1) * 636,
                TargetKind = id == (int)ExtremePower.GrantGold ? 0 : 1,
                Name = SerpLocalization.Get("Tools.Power" + id), Help = "",
                Sprite = "extreme power " + (id + 1) };
            OptionalExtremePowers.Describe(power);
            power.Enabled = nativePower != null && powerContext?.Ready == true && power.TargetKind >= 0 && power.TargetKind <= 1 &&
                GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled() && IsSelectableAi(humanSelectedOwner);
            if (humanPanel != null)
            {
                string sprite = string.IsNullOrWhiteSpace(power.Sprite) ? "extreme power " + (id + 1) : power.Sprite.Trim();
                power.Icon = humanPanel.TryFindResource(sprite + "a") as ImageSource ??
                    humanPanel.TryFindResource("extreme power " + (id + 1) + "a") as ImageSource;
            }
            power.Help = power.Help + " " + SerpLocalization.Get("Tools.PowerHelp");
            if (IsEditorToolsSession()) power.Help += " " + SerpLocalization.Get("Tools.EditorNotice");
            return power;
        }

        private static void RefreshPowers()
        {
            if (toolsModel == null) return;
            if (pendingPower != null && (!IsCurrentSession(pendingPower.Session) || pendingPower.Actor != humanSelectedOwner ||
                !GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled())) CancelPowerSelection();
            else if (pendingPower != null && powerContext?.Ready == true && powerContext.Mode != 4 && powerContext.Mode != 5)
                pendingPower = null; // Native confirmation/cancellation finished; no synthetic key handling.
            toolsModel.RefreshPowerFlag(GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled());
            if (!toolsModel.PowersEnabled) { powerRefreshActor = 0; return; }
            // Optional API has no metadata-change event; refresh only the active vanilla bar.
            if (powerRefreshActor == humanSelectedOwner && DateTime.UtcNow < nextPowerRefresh) return;
            powerRefreshActor = humanSelectedOwner;
            nextPowerRefresh = DateTime.UtcNow.AddSeconds(1);
            var powers = new ToolPower[8];
            for (int id = 0; id < powers.Length; id++)
                try { powers[id] = DescribePower(id); }
                catch (Exception error)
                {
                    powers[id] = new ToolPower { Id = id, Name = SerpLocalization.Get("Tools.Power" + id), Help = SerpLocalization.Get("Tools.PowerUnavailable") };
                    LogOnce("TEST_POWER_COMPATIBILITY_ERROR: " + error);
                }
            toolsModel.SetPowers(powers);
        }

        private static void SelectPower(int id)
        {
            amountInput?.EndEditing();
            lock (engineLock)
            {
                CancelPowerSelection();
                MainControls.instance?.StopAllPlacement();
                if ((uint)id > (uint)ExtremePower.RockVolley || !IsSelectableAi(humanSelectedOwner)) return;
                ToolPower power = DescribePower(id);
                if (!power.Enabled) { toolsModel.Status = SerpLocalization.Get("Tools.PowerUnavailable"); return; }
                pendingPower = new PowerSelection { Session = CurrentSessionId, Generation = powerGeneration,
                    Actor = humanSelectedOwner, Power = power };
                selectingTestPower = true;
                try
                {
                    if (power.Replacement != null && power.TargetKind == 0)
                        NativePowerQuery(3);
                    else
                    {
                        int nativeId = power.Id == (int)ExtremePower.GrantGold && power.TargetKind == 1
                            ? (int)ExtremePower.ArrowVolley : power.Id;
                        ToolPolicy.WithoutCost(() => ReadMana(humanSelectedOwner), value => WriteMana(humanSelectedOwner, value),
                            (uint)Math.Max(power.Cost, (nativeId + 1) * 636), () => {
                                powerContext.Select(nativeId, humanSelectedOwner); return true;
                            });
                        if (pendingPower != null && powerContext.Mode == 5)
                            powerContext.SetSelectedPower(power.Id); // Same Gold-map-target mapping as ExtremePowers.
                        else if (pendingPower != null) pendingPower = null; // Vanilla refused selection (e.g. player paused).
                    }
                }
                catch { CancelPowerSelection(); throw; }
                finally { selectingTestPower = false; }
            }
        }

        private static void CancelPowerSelection()
        {
            if (engineLock == null) { pendingPower = null; powerGeneration++; return; }
            lock (engineLock)
            {
                PowerSelection previous = pendingPower;
                pendingPower = null; powerGeneration++;
                if (previous != null && IsCurrentSession(previous.Session) && powerContext?.Ready == true &&
                    (powerContext.Mode == 4 || powerContext.Mode == 5) && powerContext.SelectedPower == previous.Power.Id)
                    powerContext.Cancel();
            }
        }

        // Called inside Vanilla under its engine monitor. Never read Noesis or change a bound ViewModel here.
        private static int NativePowerQuery(int site)
        {
            PowerSelection selected = pendingPower;
            if (powerContext?.Ready != true || selected == null || (site == 0 || site == 3) && !selectingTestPower) return 0;
            try
            {
                if (selected.Rejected)
                {
                    if (site >= 2) pendingPower = null;
                    return site >= 2 ? 1 : selected.Actor;
                }
                if (!ToolPolicy.PowerContextMatches(selected.Session, CurrentSessionId, selected.Actor, humanSelectedOwner,
                    selected.Generation, powerGeneration, IsCurrentSession(selected.Session) &&
                    GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled())) return site >= 2 ? 1 : 0;
                if (site < 2) return selected.Actor;
                // Atomically consume the native confirmation, retaining the captured AI/session for the queue.
                if (!ReferenceEquals(System.Threading.Interlocked.CompareExchange(ref pendingPower, null, selected), selected)) return 1;
                testActions.Enqueue(new TestAction { Kind = TestActionKind.Power, Actor = selected.Actor,
                    Session = selected.Session, PowerGeneration = selected.Generation, Power = selected.Power,
                    Tile = site == 3 ? -1 : powerContext.TargetTile,
                    Operation = System.Threading.Interlocked.Increment(ref nextTestAction) });
                return 1;
            }
            catch (Exception error)
            {
                selected.Rejected = true;
                if (site >= 2) pendingPower = null;
                if (System.Threading.Interlocked.Exchange(ref powerCallbackFailure, 1) == 0)
                    log.LogError("TEST_POWER_CONTEXT_CALLBACK: " + error);
                return site >= 2 ? 1 : selected.Actor;
            }
        }

        // Observes clicks only for gates. Power input is never consumed or remapped by the mod.
        private static void ObserveToolMouse(ref int state, ref bool rightDown, ref bool rightUp) => toolMouseState = state;

        private static void CompleteToolMouse(int x, int y)
        {
            if (pendingPower == null && toolMouseState == 1 && MainControls.instance?.CurrentAction == 0 &&
                !EditorDirector.instance.overUI()) SelectTestGate(x, y);
            toolMouseState = 0;
        }

        private static unsafe uint ReadMana(int actor)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(actor, out GamePlayerResources* resources))
                throw new InvalidOperationException("Power player disappeared.");
            return resources->r_ExtremePowersMana;
        }
        private static unsafe void WriteMana(int actor, uint value)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(actor, out GamePlayerResources* resources))
                throw new InvalidOperationException("Power player disappeared.");
            resources->r_ExtremePowersMana = value;
        }

        private static string ExecutePower(TestAction action)
        {
            if (!ToolPolicy.PowerContextMatches(action.Session, CurrentSessionId, action.Actor, humanSelectedOwner,
                action.PowerGeneration, powerGeneration, HasPowerTestContext())) return SerpLocalization.Get("Tools.PowerCancelled");
            if (!GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled()) return SerpLocalization.Get("Tools.PowerUnavailable");
            ToolPower current = DescribePower(action.Power.Id);
            if (!action.Power.SameContract(current)) return SerpLocalization.Get("Tools.PowerChanged");
            if (!current.Enabled || (current.TargetKind == 1 && !GameTileManagerAPI.Instance.IsValidTileId(action.Tile)))
                return SerpLocalization.Get("Tools.PowerInvalidTarget");
            RequireActionEntry(PowerRva, powerEntry, true);
            uint required = (uint)Math.Max((current.Id + 1) * 636, current.Cost);
            return ToolPolicy.WithoutCost(() => ReadMana(action.Actor), value => WriteMana(action.Actor, value), required, () => {
                if (current.Replacement != null)
                {
                    if (!OptionalExtremePowers.ExecuteReplacement(current, action.Actor, action.Tile,
                        (ulong)action.Operation, GameTimeManagerAPI.Instance.GetElapsedMapTicks(), out string rejected))
                    {
                        log.LogInfo("TEST_POWER_REJECTED actor=" + action.Actor + " reason=" + rejected);
                        return SerpLocalization.Get("Tools.PowerRejected");
                    }
                }
                else nativePower(new IntPtr(unchecked((long)GameGlobalsManager.Instance.GamePlayerManagerVA)), action.Actor, current.Id, action.Tile);
                return SerpLocalization.Get("Tools.PowerExecuted");
            });
        }
    }
}
