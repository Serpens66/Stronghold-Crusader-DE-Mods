using System;
using System.Collections.Generic;
using AIVParser.Core;
using BepInEx.Bootstrap;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using PlanRotation = AIVParser.Core.AivRotation;
using NativeRotation = SHCDESE.Interop.Enums.AivRotation;

namespace CastlePlanner
{
    internal sealed partial class BlueprintRuntimeController
    {
        private enum SpectatorReadKind { Keeps, CheckKeeps, Plans }

        private static int GetSpectatorOrderedTileCapacity()
        {
            int capacity = AivVillageState.ORDERED_MAP_TILE_CAPACITY;
            if (Chainloader.PluginInfos.TryGetValue("fixes", out var plugin) &&
                plugin.Instance is IRelocatedNativeBuffers buffers)
                capacity = Math.Max(capacity, buffers.OrderedMapTileCapacityPerVillage);
            return capacity;
        }

        private sealed class SpectatorReadRequest
        {
            internal readonly long Session;
            internal readonly SpectatorReadKind Kind;
            internal readonly BlueprintSpectatorKeep[] Keeps;
            private readonly GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            private readonly GameBuildingManagerAPI buildings = GameBuildingManagerAPI.Instance;
            private readonly GameAIVManagerAPI aiv;
            private readonly GameTileManagerAPI tiles;
            internal readonly int KeepMask;
            private readonly SpectatorPlanBuffer[] plans;
            internal volatile bool Cancelled;
            internal volatile bool Completed;
            internal Exception Error;
            internal int LostKeeps;

            internal SpectatorReadRequest(long session, SpectatorReadKind kind,
                BlueprintSpectatorKeep[] keeps = null, int keepMask = 0, int tileCapacity = 0)
            {
                Session = session;
                Kind = kind;
                Keeps = keeps ?? new BlueprintSpectatorKeep[GamePlayerManagerAPI.MAX_PLAYERS + 1];
                KeepMask = keepMask;
                if (kind != SpectatorReadKind.Plans)
                    return;

                aiv = GameAIVManagerAPI.Instance;
                tiles = GameTileManagerAPI.Instance;
                plans = new SpectatorPlanBuffer[Keeps.Length];
                for (int player = 1; player < plans.Length; player++)
                    if (Keeps[player].Id > 0 && (keepMask & (1 << player)) == 0)
                        plans[player] = new SpectatorPlanBuffer(tileCapacity);
            }

            // Runs only at APIShared's completed-buffer boundary. Copy bounded native data
            // into storage already owned by this request, with no UI work or monitor entry.
            internal void CopyFromSimulation()
            {
                for (int player = 1; player < Keeps.Length && !Cancelled; player++)
                {
                    var original = Keeps[player];
                    if (Kind == SpectatorReadKind.CheckKeeps && (KeepMask & (1 << player)) == 0)
                        continue;
                    if (Kind == SpectatorReadKind.Plans && plans[player] == null)
                        continue;

                    var keep = Kind == SpectatorReadKind.Keeps
                        ? FindKeep(player)
                        : ReadKeep(player, original.Id, allowInitializing: false);
                    if (Kind == SpectatorReadKind.Keeps)
                    {
                        Keeps[player] = keep;
                        continue;
                    }
                    if (original.Id > 0 && !original.Matches(keep))
                    {
                        LostKeeps |= 1 << player;
                        continue;
                    }
                    if (Kind == SpectatorReadKind.Plans && keep.Id > 0)
                        CopyPlan(player, plans[player]);
                }
            }

            private unsafe BlueprintSpectatorKeep FindKeep(int player)
            {
                if (!players.IsAIPlayer(player) ||
                    !players.TryGetPlayerResourcesById(player, out GamePlayerResources* resources))
                    return default;

                return ReadKeep(player, (int)resources->r_KeepId, allowInitializing: true);
            }

            private unsafe BlueprintSpectatorKeep ReadKeep(int player, int id, bool allowInitializing)
            {
                if (!global::APIShared.BuildingAccess.TryGetById(buildings, id, out GameBuilding* keep, out _) ||
                    keep->r_PlayerIdOwner != player || !IsKeep(keep->r_BuildingType) ||
                    (keep->r_AliveState != AliveState.IsAlive &&
                     !(allowInitializing && keep->r_AliveState == AliveState.NeedsInit)) || keep->r_GlobalId == 0)
                    return default;

                return new BlueprintSpectatorKeep(id, keep->r_GlobalId,
                    keep->r_TilePositionXBegin, keep->r_TilePositionYBegin);
            }

            private unsafe void CopyPlan(int player, SpectatorPlanBuffer target)
            {
                if (!aiv.TryGetVillageSlotByPlayerId(player, out int slot) ||
                    !aiv.TryGetVillageBySlot(slot, out AivVillageState* village) ||
                    village->OwnerPlayerId != player || village->LayoutSelectionState == AivLayoutSelectionState.None)
                    return;

                switch (village->Rotation)
                {
                    case NativeRotation.South: target.Rotation = PlanRotation.Degrees0; break;
                    case NativeRotation.East: target.Rotation = PlanRotation.Degrees90; break;
                    case NativeRotation.North: target.Rotation = PlanRotation.Degrees180; break;
                    case NativeRotation.West: target.Rotation = PlanRotation.Degrees270; break;
                    default: return;
                }

                var steps = aiv.GetBuildSteps(slot);
                int maximum = village->MaximumBuildStep;
                if (maximum < 0 || maximum >= steps.Length || maximum >= target.Steps.Length)
                    return;

                var ordered = aiv.GetOrderedMapTiles(slot);
                bool copiedOrdered = false;
                for (int index = 0; index <= maximum && !Cancelled; index++)
                {
                    var step = steps[index];
                    if (!BlueprintSpectatorPolicy.IncludeStep(step.State) || step.BuildingType == 0 || step.TileCount == 0)
                        continue;

                    int position = step.MapTileIdOrBufferIndex;
                    int count = 0;
                    BlueprintWorldTile single = default;
                    if (GameAIVManagerAPI.UsesOrderedMapTileBuffer(step.BuildingType))
                    {
                        count = step.TileCount;
                        if (position < 0 || position > ordered.Length - count || ordered.Length > target.OrderedTiles.Length)
                            return;

                        if (!copiedOrdered)
                        {
                            for (int tile = 0; tile < ordered.Length && !Cancelled; tile++)
                                target.OrderedTiles[tile] = ReadTile(ordered[tile]);
                            copiedOrdered = true;
                        }
                    }
                    else
                        single = ReadTile(position);

                    target.Steps[target.StepCount++] = new SpectatorPlanStep((int)step.BuildingType, position, count, single);
                }

                if (Cancelled || target.StepCount == 0)
                    return;

                target.Player = player;
                target.Variant = village->SelectedVariantIndex;
                target.OriginX = village->LayoutOriginX;
                target.OriginY = village->LayoutOriginY;
                target.KeepX = village->KeepX;
                target.KeepY = village->KeepY;
            }

            private BlueprintWorldTile ReadTile(int tileId)
            {
                if (tileId <= 0 || tileId >= GameTileManagerView.NativePackedTileCapacity)
                    return new BlueprintWorldTile(-1, -1);

                var position = tiles.GetTileVectorFromId(tileId);
                return new BlueprintWorldTile(position.X, position.Y);
            }

            internal BlueprintLivePlan[] MaterializePlans()
            {
                var result = new BlueprintLivePlan[plans.Length];
                for (int player = 1; player < plans.Length; player++)
                    if (plans[player]?.Player == player)
                        result[player] = plans[player].Materialize();
                return result;
            }
        }

        private readonly struct SpectatorPlanStep
        {
            internal SpectatorPlanStep(int mapper, int start, int count, BlueprintWorldTile single)
            { Mapper = mapper; Start = start; Count = count; Single = single; }

            internal readonly int Mapper, Start, Count;
            internal readonly BlueprintWorldTile Single;
        }

        private sealed class SpectatorPlanBuffer
        {
            internal readonly SpectatorPlanStep[] Steps = new SpectatorPlanStep[AivVillageState.BUILD_STEP_CAPACITY];
            internal readonly BlueprintWorldTile[] OrderedTiles;
            internal int StepCount, Player, Variant, OriginX, OriginY, KeepX, KeepY;
            internal PlanRotation Rotation;

            internal SpectatorPlanBuffer(int tileCapacity) => OrderedTiles = new BlueprintWorldTile[tileCapacity];

            internal BlueprintLivePlan Materialize()
            {
                var steps = new List<BlueprintLiveStep>(StepCount);
                for (int i = 0; i < StepCount; i++)
                {
                    var step = Steps[i];
                    if (step.Count == 0)
                    {
                        if (step.Single.X >= 0)
                            steps.Add(new BlueprintLiveStep(step.Mapper, new[] { step.Single }));
                        continue;
                    }

                    int end = step.Start + step.Count;
                    int index = step.Start;
                    while (index < end && OrderedTiles[index].X >= 0)
                        index++;

                    if (index == end)
                    {
                        var positions = new BlueprintWorldTile[step.Count];
                        Array.Copy(OrderedTiles, step.Start, positions, 0, step.Count);
                        steps.Add(new BlueprintLiveStep(step.Mapper, positions));
                    }
                }
                return steps.Count == 0 ? null :
                    new BlueprintLivePlan(Player, Variant, OriginX, OriginY, KeepX, KeepY, Rotation, steps.ToArray());
            }
        }
    }
}
