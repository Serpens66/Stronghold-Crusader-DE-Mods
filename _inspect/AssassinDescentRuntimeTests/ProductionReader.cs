using System;
using System.Threading;
namespace EnemyGatePathfindingTest { unsafe class Reader {
internal ulong module; internal int* active; internal Func<RouteTilePolicySnapshot> policy;
        internal bool Allows(X64SmartCPUContext* c)
        {
            int id = (int)c->RBX, direction = (int)c->R14;
            if (Volatile.Read(ref *active) == 0 || c->RDI != module + 0x67E8400 || id < 1 || id > 10000 ||
                c->R12 != (ulong)id * 0x490 || (c->RBP & 255) != 0 ||
                !AssassinPathAPI.DirectGatehouseClimbingEnabled ||
                !UnitAccess.TryGetById(id, out GameUnit* u, out _) || u == null || !UnitAccess.IsReallyAlive(in *u) ||
                (ulong)u != c->RDI + c->R12 + 0x65C || u->r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN ||
                u->r_ControllableForPlayerId < 1 || u->r_ControllableForPlayerId > 8 || u->r_GlobalId == 0 ||
                (uint)direction > 7 || u->r_CurrentTilePositionY >= 800) return false;
            uint global = u->r_GlobalId; ushort player = u->r_ControllableForPlayerId;
            int tile = (int)u->r_CurrentPositionTileId;
            if ((uint)tile >= 320800) return false;
            int target = tile + *(int*)(module + 0x405EDB0 + (uint)(direction + u->r_CurrentTilePositionY * 8) * 4UL);
            if ((uint)target >= 320800 || target == tile) return false;
            RouteTilePolicySnapshot snapshot = policy();
            int gate = *(short*)(module + 0x4B6AA50 + (uint)tile * 2UL);
            if (snapshot == null || !snapshot.GateIdentities.TryGetValue(gate, out var identity) || identity.Global == 0 ||
                !GameBuildingManagerAPI.Instance.IsValidId(gate) ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(gate, out GameBuilding* b) || b == null ||
                b->r_GlobalId != identity.Global || b->r_PlayerIdOwner != identity.Owner || b->r_CapturedByPlayerId != identity.Capturer ||
                b->r_GateState != 2 || !AssassinPathAPI.IsDirectGatehouseClimbEndpoint(tile)) return false;
            // Audited raw WORDs: path mode +90, alternate step +36C. R9w is the special-step state.
            bool permitted = AssassinGateDescentRule.Allows(direction, (int)u->r_MovementDirection,
                u->r_CurrentPathPlanIndex, u->r_PathPlanLength, (int)c->RDX, (ushort)c->R9,
                *(short*)((byte*)u + 0x36C), u->r_AIState, *(short*)((byte*)u + 0x90) == 0,
                *(byte*)(module + 0x51890D0 + (uint)tile), *(byte*)(module + 0x51890D0 + (uint)target),
                *(byte*)(module + 0x312620 + (uint)direction), *(byte*)(module + 0x312620 + (uint)(direction ^ 4)),
                *(uint*)(module + 0x48F71B0 + (uint)tile * 4UL), *(uint*)(module + 0x48F71B0 + (uint)target * 4UL));
            return permitted && Volatile.Read(ref *active) != 0 && ReferenceEquals(snapshot, policy()) &&
                u->r_GlobalId == global && u->r_ControllableForPlayerId == player && u->r_CurrentPositionTileId == tile &&
                b->r_GlobalId == identity.Global && b->r_PlayerIdOwner == identity.Owner && b->r_CapturedByPlayerId == identity.Capturer && b->r_GateState == 2;
        }
}}
