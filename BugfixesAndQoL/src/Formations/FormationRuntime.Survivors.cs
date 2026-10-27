using System;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class FormationRuntime
    {
        private enum GestureSelectionState { Unchanged, Survivors, Empty, Changed, Unavailable }

        // Engine lock is already held. Inspect only this gesture's original identities;
        // the ordinary unchanged pass reuses all buffers and allocates nothing here.
        private GestureSelectionState RefreshGestureSelection(ActiveDrag state)
        {
            if (speechEngineLock == null || !System.Threading.Monitor.IsEntered(speechEngineLock))
                return GestureSelectionState.Unavailable;
            lock (stateSync)
                return ReferenceEquals(drag, state) ? RefreshGestureSelectionLocked(state) : GestureSelectionState.Changed;
        }

        private GestureSelectionState RefreshGestureSelectionLocked(ActiveDrag state)
        {
            if (!APIShared.LocalSelectionAPI.TryCapture(state.PlayerId, out var selected))
                return GestureSelectionState.Unavailable;
            if (selected.Count > state.OriginalSelection.Length) return GestureSelectionState.Changed;
            Array.Clear(state.SeenSurvivors, 0, state.SeenSurvivors.Length);
            int count = 0, tribeId = 0;
            bool fallback = false;
            for (int i = 0; i < selected.Count; i++)
            {
                int id = selected[i].UnitId;
                if (!state.OriginalOrdinals.TryGetValue(id, out int ordinal)) return GestureSelectionState.Changed;
                if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out var failure))
                {
                    if (failure == APIShared.UnitLookupFailure.ManagerUnavailable) return GestureSelectionState.Unavailable;
                    fallback = true; continue; // A completed selection may still contain a removed corpse.
                }
                SelectionIdentity original = state.OriginalSelection[ordinal];
                if (unit->r_GlobalId != original.GlobalId)
                {
                    if (unit->r_GlobalId == 0 && !APIShared.UnitAccess.IsReallyAlive(unit))
                    {
                        fallback = true; continue; // Cleared corpse slot, not a newly selected identity.
                    }
                    return GestureSelectionState.Changed;
                }
                if (!APIShared.UnitAccess.IsReallyAlive(unit)) continue;
                if (!state.RemainingOriginals[ordinal] || state.SeenSurvivors[ordinal] ||
                    unit->r_ControllableForPlayerId != state.PlayerId || (int)unit->r_UnitChimp != original.UnitType)
                    return GestureSelectionState.Changed;
                if (count == 0) tribeId = unit->r_TribeId;
                else if (tribeId != unit->r_TribeId) return GestureSelectionState.Unavailable;
                state.SeenSurvivors[ordinal] = true;
                state.SelectionScratch[ordinal] = new SelectionIdentity(id, unit->r_GlobalId,
                    (int)unit->r_UnitChimp, unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
                count++;
            }
            if (count == state.Selection.Length && (count == 0 || tribeId == state.TribeId))
                return count == 0 ? GestureSelectionState.Empty : GestureSelectionState.Unchanged;
            if (count == state.Selection.Length) return GestureSelectionState.Changed;
            for (int i = 0; i < state.RemainingOriginals.Length; i++)
            {
                if (!state.RemainingOriginals[i] || state.SeenSurvivors[i]) continue;
                SelectionIdentity missing = state.OriginalSelection[i];
                if (APIShared.UnitAccess.TryGetById(missing.UnitId, out GameUnit* unit, out var failure))
                {
                    if (unit->r_GlobalId == missing.GlobalId && APIShared.UnitAccess.IsReallyAlive(unit))
                        return GestureSelectionState.Changed; // A live deselection is not a death.
                    fallback |= unit->r_GlobalId != missing.GlobalId;
                }
                else
                {
                    if (failure == APIShared.UnitLookupFailure.ManagerUnavailable) return GestureSelectionState.Unavailable;
                    fallback = true;
                }
            }
            if (count != 0 && (!GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) ||
                tribe == null || tribe->r_AliveState != AliveState.IsAlive || tribe->r_PlayerIdOwner != state.PlayerId))
                return GestureSelectionState.Unavailable;
            state.UseVanillaFallback |= fallback || (count != 0 && tribeId != state.TribeId);
            var survivors = new SelectionIdentity[count];
            int next = 0;
            for (int i = 0; i < state.RemainingOriginals.Length; i++)
            {
                state.RemainingOriginals[i] = state.SeenSurvivors[i];
                if (state.SeenSurvivors[i]) survivors[next++] = state.SelectionScratch[i];
            }
            state.Selection = survivors;
            if (count != 0) state.TribeId = tribeId;
            state.Authorization.RebindSurvivors(state.TribeId, count);
            if (state.PreviewUnits != null)
            {
                var preview = new FormationUnit[Math.Min(state.PreviewUnits.Length, count)];
                next = 0;
                foreach (FormationUnit member in state.PreviewUnits)
                    if (state.OriginalOrdinals.TryGetValue(member.UnitId, out int ordinal) && state.SeenSurvivors[ordinal])
                        preview[next++] = member;
                if (next != preview.Length) Array.Resize(ref preview, next);
                state.PreviewUnits = preview;
            }
            state.Geometry = state.Geometry.WithCount(state.PreviewUnits?.Length ?? count);
            state.HasLastPreviewKey = false; state.HasPreviewPlan = false; state.PreviewDirty = true;
            return count == 0 ? GestureSelectionState.Empty : GestureSelectionState.Survivors;
        }
    }
}
