using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {
        // Mirrors the per-unit switch in Vanilla's DigMoatTileId (command 6) handler.
        // There is no generic capability field in that command path.
        internal bool CanDigMoat(GameUnit* unit) =>
            settings.EnableMod && (ExtensionsEnabled || settings.EnableImprovedMoatFilling || settings.EnableLadderAttackPathfindingFix) && unit != null && CanDigMoat(unit->r_UnitChimp);

        internal static bool CanDigMoat(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_ARCHER:
                case eChimps.CHIMP_TYPE_SPEARMAN:
                case eChimps.CHIMP_TYPE_PIKEMAN:
                case eChimps.CHIMP_TYPE_MACEMAN:
                case eChimps.CHIMP_TYPE_ENGINEER:
                case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
                case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER:
                    return true;
                default:
                    return false;
            }
        }

        internal bool TryGetSelectedVanillaDigger(
            int preferredUnitId,
            int expectedPlayerId,
            out int unitId,
            out GameUnit* unit)
        {
            unitId = 0;
            unit = null;
            if (preferredUnitId > 0 &&
                APIShared.UnitAccess.TryGetById(preferredUnitId, out GameUnit* preferred, out _) &&
                preferred != null && APIShared.UnitAccess.IsReallyAlive(preferred) &&
                (expectedPlayerId < 0 || preferred->r_ControllableForPlayerId == expectedPlayerId) &&
                preferred->r_UnitSelected != 0 &&
                CanDigMoat(preferred))
            {
                unitId = preferredUnitId;
                unit = preferred;
                return true;
            }

            if (!CaptureCursorSelection(expectedPlayerId, out int[] selectedUnitIds, out _)) return false;
            for (int index = 0; index < selectedUnitIds.Length; index++)
            {
                int selectedUnitId = selectedUnitIds[index];
                if (selectedUnitId <= 0 ||
                    !APIShared.UnitAccess.TryGetById(selectedUnitId, out GameUnit* selected, out _) ||
                    selected == null || !APIShared.UnitAccess.IsReallyAlive(selected) ||
                    (expectedPlayerId >= 0 && selected->r_ControllableForPlayerId != expectedPlayerId) ||
                    !CanDigMoat(selected))
                {
                    continue;
                }

                unitId = selectedUnitId;
                unit = selected;
                return true;
            }

            return false;
        }

        internal void LogDiggerDecision(
            string source, int unitId, GameUnit* unit, int targetX, int targetY,
            bool accepted, bool friendlyMoatRequired = false)
        {
            if (unit == null)
                return;
            string key = $"{mapEpoch}:{source}:{unit->r_UnitChimp}:{unit->r_AI_LastIssuedTribeCommand}:" +
                $"{targetX}:{targetY}:{accepted}:{friendlyMoatRequired}";
            if (!loggedDiggerDecisions.Add(key))
                return;
            LogCommandDiagnostic(
                $"stage=vanilla-digger source={source} unit={unitId} " +
                $"type={unit->r_UnitChimp} command=" +
                $"{(TribeAICommand)unit->r_AI_LastIssuedTribeCommand} " +
                $"target=({targetX},{targetY}) accepted={accepted} " +
                $"friendlyMoatRequired={friendlyMoatRequired}");
        }

    }
}
