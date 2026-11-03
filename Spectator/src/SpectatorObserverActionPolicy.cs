using CrusaderDE;

namespace Spectator
{
    // Command policy for a confirmed eliminated observer whose native viewpoint can show another player.
    // Vanilla's local queue 0x23990 takes its network source from that viewpoint, so the commands below
    // would act AS THE VIEWED PLAYER. Classification from the complete DLL_GameAction (0x81870) audit of
    // CrusaderDE.dll FBCB9319… (2026-11-03, baseline knowledge GAMEACTION_OBSERVER_CLASSES.md).
    // Only local input is ever vetoed; synchronized simulation/Chore calls must run on every peer.
    internal static class SpectatorObserverActionPolicy
    {
        internal enum Decision { Allow, Veto, VetoUnaudited }

        internal static Decision Decide(bool localInput, bool confirmedObserver, Enums.GameActionCommand command,
            bool exceptionAllowed)
        {
            if (!localInput || !confirmedObserver || exceptionAllowed) return Decision.Allow;
            if (IsViewedPlayerAction(command)) return Decision.Veto;
            return IsAuditedAllowed(command) ? Decision.Allow : Decision.VetoUnaudited;
        }

        // Enqueues as the viewed player, arms a cursor/placement that later enqueues as it, or writes its
        // player/building state directly.
        internal static bool IsViewedPlayerAction(Enums.GameActionCommand command)
        {
            switch (command)
            {
                case Enums.GameActionCommand.GateHouseState:
                case Enums.GameActionCommand.RepairBuilding:
                case Enums.GameActionCommand.ToggleSleep:
                case Enums.GameActionCommand.SetTaxRate:
                case Enums.GameActionCommand.SetRationing:
                case Enums.GameActionCommand.SetFoodEaten:
                case Enums.GameActionCommand.MakeTroop:
                case Enums.GameActionCommand.DrawbridgeState:
                case Enums.GameActionCommand.Troops_Stop:
                case Enums.GameActionCommand.Troops_Disband:
                case Enums.GameActionCommand.Troops_AttackHere:
                case Enums.GameActionCommand.Troops_Cow:
                case Enums.GameActionCommand.EngBuild_Catapult:
                case Enums.GameActionCommand.EngBuild_Trebuchet:
                case Enums.GameActionCommand.EngBuild_SiegeTower:
                case Enums.GameActionCommand.EngBuild_Shield:
                case Enums.GameActionCommand.EngBuild_BatteringRam:
                case Enums.GameActionCommand.Troops_Patrol:
                case Enums.GameActionCommand.Troops_ChangeStance:
                case Enums.GameActionCommand.Troops_DeSelectType:
                case Enums.GameActionCommand.Troops_DeSelectExceptType:
                case Enums.GameActionCommand.SetNextWeaponMade:
                case Enums.GameActionCommand.BuyGoods:
                case Enums.GameActionCommand.SellGoods:
                case Enums.GameActionCommand.SetCurrentTradedGood:
                case Enums.GameActionCommand.FreeBuild_Event:
                case Enums.GameActionCommand.AmmoRecharge:
                case Enums.GameActionCommand.SH1Cheats:
                // Opens the viewed player's building panels and writes its mirror fields.
                case Enums.GameActionCommand.SelectBuildingType:
                case Enums.GameActionCommand.Autotrade_Pause:
                case Enums.GameActionCommand.Autotrade_Apply:
                case Enums.GameActionCommand.SetOutpostState:
                case Enums.GameActionCommand.SetOutpostSize:
                case Enums.GameActionCommand.SetOutpostDelay:
                case Enums.GameActionCommand.EngBuild_ArabBallista:
                case Enums.GameActionCommand.Ally_Orders:
                case Enums.GameActionCommand.Ally_CancelGoodsReq:
                case Enums.GameActionCommand.Ally_SendGoods:
                case Enums.GameActionCommand.Ally_RequestGoods:
                case Enums.GameActionCommand.Ally_ConfirmOrders:
                case Enums.GameActionCommand.Ally_CancelOrders:
                case Enums.GameActionCommand.ExtremePower:
                    return true;
                default:
                    return false;
            }
        }

        // Local presentation, menus, options, player-neutral globals, editor-only or self-guarded branches.
        // CloseTroopsPanel (ESC, Reports) only deselects as the view while a local selection exists; the
        // observer's selection is cleared once while its own view is still current.
        internal static bool IsAuditedAllowed(Enums.GameActionCommand command)
        {
            switch (command)
            {
                case Enums.GameActionCommand.OpenDogCage:
                case Enums.GameActionCommand.CycleBookmarks:
                case Enums.GameActionCommand.RadarClicked:
                case Enums.GameActionCommand.AlignSprites:
                case Enums.GameActionCommand.ResizeRadarMapBuffer:
                case Enums.GameActionCommand.FreeBuild_InvasionCharSet:
                case Enums.GameActionCommand.FreeBuild_InvasionCount:
                case Enums.GameActionCommand.FreeBuild_InvasionStart:
                case Enums.GameActionCommand.ActionPointClicked:
                case Enums.GameActionCommand.Undo:
                case Enums.GameActionCommand.SetStartingTeam:
                case Enums.GameActionCommand.Set_AI_Patrolling:
                case Enums.GameActionCommand.Fix_Tribes:
                case Enums.GameActionCommand.CloseTroopsPanel:
                case Enums.GameActionCommand.AI_Target_Special:
                case Enums.GameActionCommand.CentreMarker:
                case Enums.GameActionCommand.HideObjectiveProgress:
                case Enums.GameActionCommand.SetRadarMapRotationType:
                case Enums.GameActionCommand.Game_Paused:
                case Enums.GameActionCommand.RotateBuilding:
                case Enums.GameActionCommand.Autotrade_OnOff:
                case Enums.GameActionCommand.Autotrade_SetBuy:
                case Enums.GameActionCommand.Autotrade_SetSell:
                case Enums.GameActionCommand.GenieSpeech:
                case Enums.GameActionCommand.LordType:
                case Enums.GameActionCommand.ShowPlannedMoat:
                case Enums.GameActionCommand.SkirmishInsult:
                case Enums.GameActionCommand.MPConfig:
                case Enums.GameActionCommand.ResetTime:
                case Enums.GameActionCommand.SandsCheatAllowed:
                case Enums.GameActionCommand.SpectatorMode:
                case Enums.GameActionCommand.Scenario_Set_Starting_Month:
                case Enums.GameActionCommand.Scenario_Set_Starting_Year:
                case Enums.GameActionCommand.Scenario_Set_Trading:
                case Enums.GameActionCommand.Scenario_Set_Special:
                case Enums.GameActionCommand.Scenario_Set_StartingGoods:
                case Enums.GameActionCommand.Scenario_Set_Starting_Popularity:
                case Enums.GameActionCommand.Scenario_Set_Starting_Special_Gold:
                case Enums.GameActionCommand.Scenario_Set_Starting_Special_Rations:
                case Enums.GameActionCommand.Scenario_Set_Starting_Special_Tax:
                case Enums.GameActionCommand.Scenario_Set_BuildingAvailable:
                case Enums.GameActionCommand.Scenario_Set_TroopAvailable:
                case Enums.GameActionCommand.Scenario_Set_SwordAvailable:
                case Enums.GameActionCommand.Scenario_Set_XbowAvailable:
                case Enums.GameActionCommand.Scenario_Set_PikeAvailable:
                case Enums.GameActionCommand.EditorWipeGoods:
                case Enums.GameActionCommand.RespawnLord:
                case Enums.GameActionCommand.WipeAnimals:
                case Enums.GameActionCommand.HoldTime:
                case Enums.GameActionCommand.SetMarkerState:
                case Enums.GameActionCommand.FastGoodsFeedin:
                case Enums.GameActionCommand.Scenario_Set_MaceAvailable:
                case Enums.GameActionCommand.Scenario_Set_bowAvailable:
                case Enums.GameActionCommand.Scenario_Set_SpearAvailable:
                case Enums.GameActionCommand.Scenario_Set_MercTroopAvailable:
                case Enums.GameActionCommand.Scenario_Set_BedouinTroopAvailable:
                case Enums.GameActionCommand.Balanced:
                    return true;
                default:
                    return false;
            }
        }
    }
}
