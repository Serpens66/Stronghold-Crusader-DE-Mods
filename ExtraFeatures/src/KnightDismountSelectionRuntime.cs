using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System.Collections.Generic;
using System.Threading;

namespace ExtraFeatures
{
    internal sealed unsafe partial class KnightDismountRuntime
    {
        private void AdvanceSelectionTransfers(bool eligibleEngineTick)
        {
            if (!eligibleEngineTick) return;
            selectionTransfers.SetContext(Volatile.Read(ref selectionSessionId), GetSelectionPlayerId());
            selectionTransfers.Advance(eligibleEngineTick, selectionWorld);
        }

        private void QueueSelectionTransfer(int unitId)
        {
            long session = Volatile.Read(ref selectionSessionId);
            int player = GetSelectionPlayerId();
            selectionTransfers.SetContext(session, player);
            if (session <= 0 || !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                unit->r_ControllableForPlayerId != player)
                return;
            selectionTransfers.Enqueue(new KnightSelectionIdentity(
                session, player, unitId, (int)unit->r_GlobalId, (int)unit->r_UnitChimp));
        }

        private sealed class KnightSelectionWorld : IKnightSelectionTransferWorld
        {
            private readonly KnightDismountRuntime runtime;
            internal KnightSelectionWorld(KnightDismountRuntime runtime) { this.runtime = runtime; }

            public KnightSelectionUnitState Read(KnightSelectionIdentity identity)
            {
                if (identity.SessionId != Volatile.Read(ref selectionSessionId) ||
                    identity.PlayerId != GetSelectionPlayerId() ||
                    !APIShared.UnitAccess.TryGetById(identity.UnitId, out GameUnit* unit, out _) ||
                    unit->r_GlobalId != (uint)identity.GlobalId ||
                    unit->r_ControllableForPlayerId != identity.PlayerId ||
                    (int)unit->r_UnitChimp != identity.UnitType || unit->r_IsKilledByProjectile != 0)
                    return KnightSelectionUnitState.Invalid;

                if (unit->r_AliveState == AliveState.NeedsInit) return KnightSelectionUnitState.NeedsInit;
                if (!APIShared.UnitAccess.IsReallyAlive(unit)) return KnightSelectionUnitState.Invalid;
                return IsSelected(unit) ? KnightSelectionUnitState.Selected : KnightSelectionUnitState.Ready;
            }

            public KnightSelectionIdentity CaptureIdentity(int unitId)
            {
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    !APIShared.UnitAccess.IsReallyAlive(unit)) return default;
                return new KnightSelectionIdentity(Volatile.Read(ref selectionSessionId),
                    unit->r_ControllableForPlayerId, unitId, (int)unit->r_GlobalId, (int)unit->r_UnitChimp);
            }

            public void CaptureCurrentSelection(List<int> ids)
            {
                int player = GetSelectionPlayerId();
                var units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
                {
                    ref GameUnit unit = ref units[spanIndex];
                    if (unit.r_UnitHover != 0 && unit.r_ControllableForPlayerId == player &&
                        APIShared.UnitAccess.IsReallyAlive(in unit))
                        ids.Add(spanIndex + 1);
                }
            }

            public bool TrySetSelection(List<int> ids) => GamePlayerManagerAPI.Instance.SetSelectedChimps(ids);

            public void Report(KnightSelectionIdentity identity, string reason) => runtime.LogError(
                $"Knight selection transfer ended: reason={reason}, sessionId={identity.SessionId}, " +
                $"playerId={identity.PlayerId}, unitId={identity.UnitId}, globalId={identity.GlobalId}, " +
                $"expectedType={identity.UnitType}.");
        }
    }
}
