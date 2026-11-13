using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ImprovedHunters
{
    internal sealed partial class ImprovedHuntersRuntime
    {
        // The plan's corpse bonus in the shared ranking; about 2.5 tiles of walking.
        private const int CorpseCostBonus = 50;
        private const int MaximumCorpsesPerHunter = 24;
        private const int CorpseChoiceEveryScans = 10;
        private const ushort HunterWalksToCorpseState = 0xB;

        private readonly List<int> freeCorpses = new List<int>();
        private readonly Dictionary<int, int> hunterHitCorpses = new Dictionary<int, int>();
        private readonly HashSet<int> corpsesChosenThisScan = new HashSet<int>();
        private HunterCorpseChoicePatch hunterCorpseChoicePatch;

        // Only this setting decides; better prey selection and reliable arrows may be off.
        private bool IsCorpseChoiceEnabled =>
            hunterCorpseChoicePatch?.IsAvailable == true &&
            Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
            settings.CollectFreeCorpses &&
            targetSelectionModeAllowed;

        private void InitializeHunterCorpseChoicePatch(ReadOnlySpan<byte> memory, ulong imageBase, bool referenceHashMatches)
        {
            try
            {
                hunterCorpseChoicePatch = new HunterCorpseChoicePatch(log, hookRegion, memory, imageBase, referenceHashMatches);
                ApplyCorpseChoiceRule();
            }
            catch (Exception exception)
            {
                hunterCorpseChoicePatch?.Dispose();
                hunterCorpseChoicePatch = null;
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters corpse choice is unavailable; Hunters keep Vanilla's corpse rule: {exception}");
            }
        }

        private void ApplyCorpseChoiceRule() =>
            hunterCorpseChoicePatch?.SetEnabled(Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) && settings.CollectFreeCorpses);

        private void ResetCorpseChoices()
        {
            hunterCorpseChoicePatch?.Clear();
            hunterHitCorpses.Clear();
            freeCorpses.Clear();
        }

        // A Hunter's own arrow killed an animal he was not aiming at: he takes it if it is free.
        private unsafe void OnUnitKilledByProjectile(UnitKilledByProjectileEventArgs args)
        {
            try
            {
                if (!IsCorpseChoiceEnabled ||
                    args.AttackedUnitId <= 0 || args.AttackedUnitId > int.MaxValue ||
                    args.ProjectileId <= 0 || args.ProjectileId > int.MaxValue ||
                    !GameProjectileManagerAPI.Instance.TryGetProjectileById((int)args.ProjectileId, out GameProjectile* projectile) ||
                    projectile == null ||
                    projectile->r_ProjectileType != ProjectileType.ArcherArrow ||
                    projectile->r_TargetUnidId == args.AttackedUnitId ||
                    !APIShared.UnitAccess.TryGetById(projectile->r_SourceUnitId, out GameUnit* shooter, out _) ||
                    shooter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                {
                    return;
                }

                hunterHitCorpses[projectile->r_SourceUnitId] = (int)args.AttackedUnitId;
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters could not read a projectile kill: {exception}");
            }
        }

        private unsafe void UpdateCorpseChoices(SimpleNativeArray<GameUnit> units, List<IntPtr> hunters, long timestamp)
        {
            if (!IsCorpseChoiceEnabled)
            {
                // Idle Hunters must not keep waking up for a choice that nothing will use.
                if (freeCorpses.Count != 0)
                {
                    freeCorpses.Clear();
                    ResetCorpseChoices();
                }

                return;
            }

            freeCorpses.Clear();
            for (int index = 0; index < units.Length; index++)
            {
                if (IsFreeUsableCorpse(units.GetValuePointer(index)))
                    freeCorpses.Add(index + 1);
            }

            corpsesChosenThisScan.Clear();
            long scan = timestamp / NativeScanIntervalMapTicks;
            foreach (IntPtr hunterAddress in hunters)
            {
                GameUnit* hunter = (GameUnit*)hunterAddress.ToPointer();
                if (hunter == null || !APIShared.UnitAccess.IsReallyAlive(hunter) || hunter->r_CurrentHealth == 0)
                    continue;

                int hunterUnitId = checked((int)(hunter - units._array) + 1);
                KeepCorpseForWalkingHunter(units, hunter);
                ForgetChoiceNoLongerFree(units, hunterUnitId);
                if (hunterHitCorpses.TryGetValue(hunterUnitId, out int hitCorpse))
                {
                    // Stays his until it is taken or no longer free, ahead of the normal ranking.
                    if (TryOfferCorpse(units, hunter, hunterUnitId, hitCorpse, timestamp))
                    {
                        // A Hunter on his way drops that prey now; Vanilla then searches, which takes the corpse.
                        if (*(ushort*)((byte*)hunter + 0x2BC) == 1 && *(uint*)((byte*)hunter + 0x39C) != 0)
                            TryAbandonHunterTarget((byte*)hunter, hunterUnitId);
                        continue;
                    }

                    hunterHitCorpses.Remove(hunterUnitId);
                }

                if (freeCorpses.Count == 0)
                {
                    hunterCorpseChoicePatch.Set(hunterUnitId, 0, 0);
                    continue;
                }

                ushort state = *(ushort*)((byte*)hunter + 0x2BC);
                if (state != 0 && (scan + hunterUnitId) % CorpseChoiceEveryScans != 0)
                    continue;

                ChooseCorpseForHunter(units, hunter, hunterUnitId, timestamp);
            }
        }

        // Vanilla only sends a Hunter to a deer or goat corpse beside him, which outlasts that walk. A free corpse
        // from farther away would vanish on the way, so its timer waits until he has arrived and Vanilla sets it.
        private static unsafe void KeepCorpseForWalkingHunter(SimpleNativeArray<GameUnit> units, GameUnit* hunter)
        {
            byte* bytes = (byte*)hunter;
            int target = *(ushort*)(bytes + 0x39A);
            if (*(ushort*)(bytes + 0x2BC) != HunterWalksToCorpseState || target <= 0 || target > units.Length)
                return;

            GameUnit* corpse = units.GetValuePointer(target - 1);
            if (corpse->r_GlobalId == *(uint*)(bytes + 0x39C) && *(ushort*)((byte*)corpse + 0x29C) != 0 && !IsFadingCorpse(corpse))
                *(ushort*)((byte*)corpse + 0x2C4) = 0;
        }

        // A deer or goat corpse fades out once a Hunter has reached it or its time is over. Its reservation ends
        // then too, so it must not count as free: its meat is taken, and another Hunter would get it again.
        // For rabbits, camels and chickens this state is the normal corpse; the mod removes those on arrival.
        private static unsafe bool IsFadingCorpse(GameUnit* corpse) =>
            !IsShortLivedPrey(corpse->r_UnitChimp) && *(ushort*)((byte*)corpse + 0x2BC) == HunterCorpsePickupAiState;

        // A choice can be a second old when the Hunter comes to use it, and the native check does not know this state.
        private unsafe void ForgetChoiceNoLongerFree(SimpleNativeArray<GameUnit> units, int hunterUnitId)
        {
            if (!hunterCorpseChoicePatch.TryGetChoice(hunterUnitId, out int corpseUnitId, out uint corpseGlobalId))
                return;

            GameUnit* corpse = corpseUnitId <= units.Length ? units.GetValuePointer(corpseUnitId - 1) : null;
            if (corpse == null || corpse->r_GlobalId != corpseGlobalId || !IsFreeUsableCorpse(corpse))
                hunterCorpseChoicePatch.Set(hunterUnitId, 0, 0);
        }

        private unsafe bool TryOfferCorpse(SimpleNativeArray<GameUnit> units, GameUnit* hunter, int hunterUnitId, int corpseUnitId, long timestamp)
        {
            if (corpseUnitId <= 0 || corpseUnitId > units.Length)
                return false;

            GameUnit* corpse = units.GetValuePointer(corpseUnitId - 1);
            if (!IsFreeUsableCorpse(corpse) ||
                corpsesChosenThisScan.Contains(corpseUnitId) ||
                !IsOwnerAllowed(hunter->r_ControllableForPlayerId, corpse) ||
                !IsCorpseReachable(hunterUnitId, corpseUnitId, corpse, timestamp))
            {
                return false;
            }

            corpsesChosenThisScan.Add(corpseUnitId);
            hunterCorpseChoicePatch.Set(hunterUnitId, corpseUnitId, corpse->r_GlobalId);
            return true;
        }

        private unsafe void ChooseCorpseForHunter(SimpleNativeArray<GameUnit> units, GameUnit* hunter, int hunterUnitId, long timestamp)
        {
            int owner = hunter->r_ControllableForPlayerId;
            if (!TryGetHunterOrigin(hunter, owner, out int originX, out int originY, out int roundTrip))
            {
                hunterCorpseChoicePatch.Set(hunterUnitId, 0, 0);
                return;
            }

            bool hasBest = false;
            BestTarget best = default;
            int checkedCorpses = 0;
            foreach (int corpseUnitId in freeCorpses)
            {
                GameUnit* corpse = units.GetValuePointer(corpseUnitId - 1);
                if (corpsesChosenThisScan.Contains(corpseUnitId) || !IsOwnerAllowed(owner, corpse))
                    continue;

                int distance = GetChebyshevDistance(originX, originY, corpse->r_CurrentTilePositionX, corpse->r_CurrentTilePositionY);
                if (!HunterSearchRangePolicy.IsWithin(distance, settings.HunterSearchRange) ||
                    checkedCorpses++ >= MaximumCorpsesPerHunter ||
                    !IsCorpseReachable(hunterUnitId, corpseUnitId, corpse, timestamp))
                {
                    continue;
                }

                int approach = distance * 10;
                int cycle = HunterHutWorkCost + GetPreyHandlingCost(corpse->r_UnitChimp) + roundTrip + approach * 2 - CorpseCostBonus;
                var candidate = new BestTarget(corpseUnitId, corpse->r_GlobalId, corpse->r_UnitChimp, settings.GetMeatAmount(corpse->r_UnitChimp), approach, roundTrip, cycle);
                if (!hasBest || IsBetterTarget(candidate, best))
                {
                    best = candidate;
                    hasBest = true;
                }
            }

            // A living animal that pays better stays the better choice; the bonus is no absolute priority.
            if (hasBest &&
                TryGetTargetSelectionForHunter(hunterUnitId, timestamp, out TargetSelection selection) &&
                selection.HasTarget &&
                !IsBetterTarget(best, selection.BestTarget))
            {
                hasBest = false;
            }

            if (!hasBest)
            {
                hunterCorpseChoicePatch.Set(hunterUnitId, 0, 0);
                return;
            }

            corpsesChosenThisScan.Add(best.UnitId);
            hunterCorpseChoicePatch.Set(hunterUnitId, best.UnitId, best.GlobalId);
        }

        private unsafe bool IsFreeUsableCorpse(GameUnit* unit)
        {
            if (unit == null || unit->r_AliveState != AliveState.IsAlive)
                return false;

            byte* bytes = (byte*)unit;
            if (*(ushort*)(bytes + 0x29C) == 0 ||
                *(ushort*)(bytes + 0x448) != 0 ||
                IsFadingCorpse(unit) ||
                !settings.IsKnownAnimal(unit->r_UnitChimp) ||
                !IsRuntimeHuntingEnabled(unit->r_UnitChimp))
            {
                return false;
            }

            // Rabbits and the like vanish soon; one whose keep-alive time is over is no target any more.
            return !shortLivedCorpsePreserveUntil.TryGetValue(unit->r_GlobalId, out long preserveUntil) ||
                preserveUntil != ExpiredShortLivedCorpsePreserve;
        }

        private unsafe bool IsCorpseReachable(int hunterUnitId, int corpseUnitId, GameUnit* corpse, long timestamp)
        {
            return hunterPclReachability == null ||
                !hunterPclReachability.TryIsReachable(hunterUnitId, corpseUnitId, corpse->r_GlobalId, corpse->r_UnitChimp, timestamp, out bool reachable, allowCorpse: true) ||
                reachable;
        }
    }
}
