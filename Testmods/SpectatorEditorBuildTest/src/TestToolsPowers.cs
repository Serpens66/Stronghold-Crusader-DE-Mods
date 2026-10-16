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

namespace SpectatorEditorBuildTest
{
    public sealed partial class SpectatorEditorBuildTestPlugin
    {
        private sealed class PowerSelection
        {
            internal long Session;
            internal int Actor;
            internal ToolPower Power;
        }
        private static PowerSelection pendingPower;
        private static long powerGeneration;
        private static int toolMouseState, powerMouseState;
        private static bool consumePowerGesture;
        private static IDisposable powerEscapeSubscription;
        private static DateTime nextPowerRefresh;
        private static int powerRefreshActor;

        private static void RegisterPowerInput()
        {
            RegisterVanillaPowerActions();
            powerEscapeSubscription = InputR3EventHooks.OnKeyDown.Observable.Subscribe(args => {
                if (args.Phase == EventHookPhase.Pre && args.Key == KeyCode.Escape && pendingPower != null)
                { CancelPowerSelection(); args.Result = false; }
            });
        }

        private static ToolPower DescribePower(int id)
        {
            var power = new ToolPower { Id = id, Cost = (id + 1) * 636,
                TargetKind = id == (int)ExtremePower.GrantGold ? 0 : 1,
                Name = SerpLocalization.Get("Tools.Power" + id), Help = "",
                Sprite = "extreme power " + (id + 1) };
            OptionalExtremePowers.Describe(power);
            power.Enabled = nativePower != null && power.TargetKind >= 0 && power.TargetKind <= 1 &&
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
            toolsModel.RefreshPowerFlag(GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled());
            // Optional API has no metadata-change event; this bounded refresh never samples input.
            if (powerRefreshActor == humanSelectedOwner && DateTime.UtcNow < nextPowerRefresh) return;
            powerRefreshActor = humanSelectedOwner;
            nextPowerRefresh = DateTime.UtcNow.AddMilliseconds(250);
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
            CancelPowerSelection();
            MainControls.instance?.StopAllPlacement();
            if ((uint)id > (uint)ExtremePower.RockVolley || !IsSelectableAi(humanSelectedOwner)) return;
            ToolPower power = DescribePower(id);
            if (!power.Enabled) { toolsModel.Status = SerpLocalization.Get("Tools.PowerUnavailable"); return; }
            if (power.TargetKind == 0)
                QueueTestAction(new TestAction { Kind = TestActionKind.Power, Actor = humanSelectedOwner, Power = power, Tile = -1 });
            else
            {
                pendingPower = new PowerSelection { Session = CurrentSessionId, Actor = humanSelectedOwner, Power = power };
                toolsModel.Status = SerpLocalization.Get("Tools.PowerChooseTarget");
            }
        }

        private static void CancelPowerSelection()
        {
            powerGeneration++;
            if (pendingPower != null && toolsModel != null) toolsModel.Status = SerpLocalization.Get("Tools.PowerCancelled");
            pendingPower = null;
        }

        // Called by the existing EditorDirector mouse adapter, before native selection sees the gesture.
        private static void ObserveToolMouse(ref int state, ref bool rightDown, ref bool rightUp)
        {
            toolMouseState = state;
            if (pendingPower != null && (!IsToolsSession() || pendingPower.Actor != humanSelectedOwner ||
                !IsCurrentSession(pendingPower.Session) || !GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled()))
                CancelPowerSelection();
            if (pendingPower == null && !consumePowerGesture) return;
            powerMouseState = state;
            if (state == 1) consumePowerGesture = true;
            if (rightDown || rightUp) CancelPowerSelection();
            state = 0; rightDown = rightUp = false;
            if (powerMouseState == 3) consumePowerGesture = false;
        }

        private static void CompleteToolMouse(int x, int y)
        {
            int state = powerMouseState; powerMouseState = 0;
            if (pendingPower != null && state == 1 && x >= 0 && y >= 0 && !EditorDirector.instance.overUI())
            {
                PowerSelection selected = pendingPower;
                var tiles = GameTileManagerAPI.Instance;
                if (tiles.IsTileInsideMapBounds(x, y))
                {
                    int tile = tiles.GetTileId(x, y);
                    if (ValidPowerTarget(selected.Power, selected.Actor, tile, out string reason))
                    {
                        pendingPower = null;
                        QueueTestAction(new TestAction { Kind = TestActionKind.Power, Actor = selected.Actor,
                            Power = selected.Power, Tile = tile });
                    }
                    else toolsModel.Status = reason;
                }
            }
            else if (pendingPower == null && !consumePowerGesture && toolMouseState == 1 &&
                MainControls.instance?.CurrentAction == 0 && !EditorDirector.instance.overUI())
                SelectTestGate(x, y);
            toolMouseState = 0;
        }

        private static unsafe bool ValidPowerTarget(ToolPower power, int actor, int tile, out string reason)
        {
            reason = SerpLocalization.Get("Tools.PowerInvalidTarget");
            var players = GamePlayerManagerAPI.Instance;
            if (!players.TryGetPlayerResourcesById(actor, out GamePlayerResources* own) || own->r_IsPaused != 0) return false;
            if (power.TargetKind == 0) return true;
            var tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsValidTileId(tile)) return false;
            var point = tiles.GetTileVectorFromId(tile);
            if (!tiles.IsTileInsideMapBounds(point.X, point.Y)) return false;
            bool spawn = power.Id == (int)ExtremePower.SpawnSpearmen || power.Id == (int)ExtremePower.SpawnEngineers ||
                power.Id == (int)ExtremePower.SpawnMacemen || power.Id == (int)ExtremePower.SpawnKnights;
            if (!spawn) return true;
            uint flags = (uint)tiles.GetTilePropertyFlag(tile);
            // Audited Vanilla target mask, not a replacement walkability approximation.
            if ((flags & 0x40501481u) != 0 || tiles.GetPathConnectionLayer()[tile] == 0 ||
                (power.Id == (int)ExtremePower.SpawnKnights && (flags & (uint)TilePropertyFlag.IsWall) != 0)) return false;
            int radius = power.Id == (int)ExtremePower.SpawnEngineers ? 0x3100 : 0x9c4;
            bool aroundLord = Marshal.ReadInt32(new IntPtr(toolsImage + PowersAroundLordRva)) != 0;
            bool nearHome = ToolPolicy.InRadius(point.X, point.Y, (int)own->r_KeepTilePositionX, (int)own->r_KeepTilePositionY, radius);
            if (aroundLord && TryLordPosition(actor, out int lordX, out int lordY))
                nearHome |= ToolPolicy.InRadius(point.X, point.Y, lordX, lordY, radius);
            if (!nearHome) return false;
            var state = GameData.Instance.lastGameState;
            if (state.teams == null || state.teams.Length <= 8) return false;
            for (int enemy = 1; enemy <= 8; enemy++)
            {
                if (enemy == actor || state.teams[enemy] == state.teams[actor] ||
                    !players.TryGetPlayerResourcesById(enemy, out GamePlayerResources* other) ||
                    other->r_LordUnitId == 0 || other->r_WinLossState == WinLossState.Loss) continue;
                if (!ToolPolicy.InRadius(point.X, point.Y, (int)other->r_KeepTilePositionX, (int)other->r_KeepTilePositionY, 624)) continue;
                if (!aroundLord || (TryLordPosition(enemy, out int enemyX, out int enemyY) &&
                    ToolPolicy.InRadius(point.X, point.Y, enemyX, enemyY, radius - 1))) return false;
            }
            return true;
        }

        private static unsafe bool TryLordPosition(int player, out int x, out int y)
        {
            x = y = 0;
            var players = GamePlayerManagerAPI.Instance;
            int id = players.GetLordUnitId(player), global = players.GetLordUnitGlobalId(player);
            if (id <= 0 || global == 0 || !APIShared.UnitAccess.TryGetById(GameUnitManagerAPI.Instance, id, out GameUnit* unit, out _) ||
                !APIShared.UnitAccess.IsReallyAlive(unit) || unit->r_GlobalId != unchecked((uint)global)) return false;
            x = unit->r_CurrentTilePositionX; y = unit->r_CurrentTilePositionY; return true;
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
            if (!current.Enabled || !ValidPowerTarget(current, action.Actor, action.Tile, out string reason))
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
