using BepInEx.Configuration;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.Network;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class FormationRuntime
    {
        private static bool TryCaptureSelection(
            out SelectionIdentity[] identities,
            out int tribeId)
        {
            MainViewModel main = MainViewModel.viewModelLoaded ? MainViewModel.Instance : null;
            int playerId = main != null && main.IsMapEditorMode
                ? EditorDirector.instance?.ActivePlayerID ?? -1
                : GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (playerId < 1 || playerId > 8 ||
                playerId != GamePlayerManagerAPI.Instance.GetLocalPlayerId())
            {
                identities = Array.Empty<SelectionIdentity>();
                tribeId = 0;
                return false;
            }
            if (!APIShared.LocalSelectionAPI.TryCapture(playerId, out APIShared.LocalSelectionSnapshot selected) ||
                selected.Count < 2 || selected.Count > FormationPreviewMarkerModel.MaximumMarkers)
            {
                identities = Array.Empty<SelectionIdentity>();
                tribeId = 0;
                return false;
            }

            identities = new SelectionIdentity[selected.Count];
            tribeId = -1;
            long sumX = 0;
            long sumY = 0;
            for (int index = 0; index < selected.Count; index++)
            {
                int unitId = selected[index].UnitId;
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_GlobalId == 0)
                    return false;
                if (tribeId < 0)
                    tribeId = unit->r_TribeId;
                else if (tribeId != unit->r_TribeId)
                    return false;
                identities[index] = new SelectionIdentity(
                    unitId,
                    unit->r_GlobalId,
                    (int)unit->r_UnitChimp,
                    unit->r_CurrentTilePositionX,
                    unit->r_CurrentTilePositionY);
                sumX += unit->r_CurrentTilePositionX;
                sumY += unit->r_CurrentTilePositionY;
            }
            Array.Sort(identities, (left, right) => left.UnitId.CompareTo(right.UnitId));

            if (!GameTribeManagerAPI.Instance.IsValidId(tribeId) ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) ||
                tribe == null || tribe->r_AliveState != AliveState.IsAlive)
                return false;
            bool isMapEditor = main != null && main.IsMapEditorMode;
            if (!isMapEditor &&
                (GamePlayerManagerAPI.Instance.IsAIPlayer(tribe->r_PlayerIdOwner) ||
                 tribe->r_PlayerIdOwner != GamePlayerManagerAPI.Instance.GetLocalPlayerId()))
                return false;
            return true;
        }

        private static bool SelectionMatches(SelectionIdentity[] expected, int expectedTribeId)
        {
            if (!TryCaptureSelection(out SelectionIdentity[] current, out int currentTribeId) ||
                currentTribeId != expectedTribeId ||
                current.Length != expected.Length)
                return false;
            for (int index = 0; index < current.Length; index++)
            {
                if (current[index].UnitId != expected[index].UnitId ||
                    current[index].GlobalId != expected[index].GlobalId ||
                    current[index].UnitType != expected[index].UnitType)
                    return false;
            }
            return true;
        }

        private static bool TryCaptureTarget(out GroundTarget target)
        {
            target = default;
            if (GameMap.instance == null)
                return false;
            UnityEngine.Vector3 mouseMap = UnityEngine.Vector3.zero;
            Vector3Int tileMap = Vector3Int.zero;
            int clickDepth = 0;
            GameMap.instance.CalcMapTileFromMousePos(
                Input.mousePosition, ref mouseMap, ref tileMap, ref clickDepth);
            GameMapTile mapTile = GameMap.instance.getMapTile(tileMap.x, tileMap.y);
            if (mapTile == null || (uint)mapTile.gameMapX >= MapWidth ||
                (uint)mapTile.gameMapY >= MapWidth)
                return false;
            target = new GroundTarget(mapTile.gameMapX, mapTile.gameMapY);
            return true;
        }

        private bool TryCaptureCommandTarget(
            out GroundTarget target,
            out APIShared.Internal.GroundMovePreviewRejection rejection)
        {
            rejection = APIShared.Internal.GroundMovePreviewRejection.OutsideMap;
            if (!TryCaptureTarget(out target))
                return false;

            // A candidate is not an object-free-ground verdict. Coherent final
            // Vanilla feedback, observed after native dispatch, authorizes Move.
            rejection = EvaluateFormationTargetBounds(target);
            return rejection == APIShared.Internal.GroundMovePreviewRejection.None;
        }

        private APIShared.Internal.GroundMovePreviewRejection EvaluateCommandMode()
        {
            if (commandModeReader == null)
                return APIShared.Internal.GroundMovePreviewRejection.NonMoveCommandMode;
            return APIShared.Internal.GroundMovePreviewEligibility.EvaluateCommandMode(
                commandModeReader.Read());
        }

        private APIShared.Internal.GroundMovePreviewRejection EvaluateFormationTargetBounds(
            GroundTarget target)
        {
            GameTileManagerView tileManager = GameTileManagerAPI.Instance.TileManager;
            int tileId = GameTileManagerAPI.Instance.GetTileId(
                target.NativeX, target.NativeY);
            bool insideMap = IsTargetInsideNativeMap(target, tileId, tileManager);
            return insideMap ? APIShared.Internal.GroundMovePreviewRejection.None :
                APIShared.Internal.GroundMovePreviewRejection.OutsideMap;
        }

        private static bool IsTargetInsideNativeMap(
            GroundTarget target,
            int tileId,
            GameTileManagerView tileManager)
        {
            return tileManager != null &&
                (uint)target.NativeX < MapWidth &&
                (uint)target.NativeY < MapWidth &&
                (uint)tileId < (uint)tileManager.TileUnitIdGrid.Length &&
                (uint)tileId < (uint)tileManager.StructureGrid.Length &&
                (uint)tileId < (uint)tileManager.PathConnectionGrid.Length;
        }

        private void LogTargetRejection(
            APIShared.Internal.GroundMovePreviewRejection rejection,
            GroundTarget target)
        {
            string reason = ToRejectionReason(rejection);
            if (!loggedTargetRejections.Add(reason))
                return;
            LogDebugNoThrow(
                $"FORMATION_DRAG_REJECTED: reason={reason}, " +
                $"target={target.NativeX},{target.NativeY}; " +
                "further occurrences of this reason are suppressed.");
        }

        private static string ToRejectionReason(
            APIShared.Internal.GroundMovePreviewRejection rejection) =>
            rejection.ToString().ToLowerInvariant();

        private static bool HasValidMap() =>
            FatControler.currentScene == Enums.SceneIDS.ActualMainGame &&
            GameMap.instance != null && EditorDirector.instance != null &&
            MainControls.instance != null;

        private readonly struct SelectionIdentity
        {
            internal SelectionIdentity(
                int unitId, uint globalId, int unitType, int x, int y)
            {
                UnitId = unitId;
                GlobalId = globalId;
                UnitType = unitType;
                X = x;
                Y = y;
            }

            internal int UnitId { get; }
            internal uint GlobalId { get; }
            internal int UnitType { get; }
            internal int X { get; }
            internal int Y { get; }
        }

        private readonly struct GroundTarget
        {
            internal GroundTarget(int nativeX, int nativeY)
            {
                NativeX = nativeX;
                NativeY = nativeY;
            }

            internal int NativeX { get; }
            internal int NativeY { get; }
        }
    }
}
