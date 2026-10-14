// TEMP_GATE_MIGRATION_DIAGNOSTICS: retained for old/new acceptance; see removal inventory.
// TEMP_GATE_ROUTE_ACCEPTANCE: validated public SDK fields; no state changes.
using System;
using System.Diagnostics;
using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
namespace BugfixesAndQoL.GateRouting
{
    internal sealed unsafe partial class NativeAssassinMovementDiagnostics
    {
        internal static Sample ReadUnit(ulong module, int id)
        {
            var s = new Sample { Unit = id, NextDirection = -1, GateState = -1,
                Mono = Stopwatch.GetTimestamp(), Utc = DateTime.UtcNow };
            if (id < 1 || id > 10000 || !UnitAccess.TryGetById(id, out GameUnit* u, out _) || !UnitAccess.IsReallyAlive(in *u) ||
                u->r_UnitChimp != eChimps.CHIMP_TYPE_ARAB_ASSASIN) return s;
            s.Global = u->r_GlobalId; s.Player = u->r_ControllableForPlayerId;
            s.State = u->r_AIState; s.Command = u->r_AI_LastIssuedTribeCommand;
            s.X = u->r_CurrentTilePositionX; s.Y = u->r_CurrentTilePositionY;
            s.TX = u->r_TargetTilePositionX2; s.TY = u->r_TargetTilePositionY2;
            // Native manager offsets 70E/710/712/714/A6C/A70 minus unit base 65C.
            // DCE60 initializes climb height; 16CD70 consumes progress; 184FD0 adds
            // progress to the render offset. These are raw values, not screen coordinates.
            s.WorldX = *(ushort*)((byte*)u + 0xB2); s.WorldY = *(ushort*)((byte*)u + 0xB4);
            s.BaseHeight = *(ushort*)((byte*)u + 0xB6); s.RenderOffset = *(short*)((byte*)u + 0xB8);
            s.ClimbHeight = *(short*)((byte*)u + 0x410); s.ClimbProgress = *(short*)((byte*)u + 0x414);
            s.Index = u->r_CurrentPathPlanIndex; s.Length = u->r_PathPlanLength;
            s.PathState = u->r_PathPlanStateBitFlags; s.GateContextRaw = u->N000001F6_2;
            // Audited native consumed-direction WORD: manager+90C = GameUnit+2B0.
            s.Direction = (int)u->r_MovementDirection;
            s.Buffer = module + 0x67E8400 + 0xB4FE78 + (uint)id * 1000UL;
            if (s.Length <= 2000 && s.Index < s.Length)
            { int packed = *(byte*)(s.Buffer + (uint)(s.Index / 2)); s.NextDirection = (packed >> ((s.Index & 1) * 4)) & 15; }
            uint tile = u->r_CurrentPositionTileId;
            if (tile < 320800)
            {
                int building = *(short*)(module + 0x4B6AA50 + tile * 2UL);
                if (GameBuildingManagerAPI.Instance.IsValidId(building) &&
                    GameBuildingManagerAPI.Instance.TryGetBuildingById(building, out GameBuilding* b) && b != null &&
                    (b->r_BuildingType == eStructs.STRUCT_GATE_MAIN || b->r_BuildingType == eStructs.STRUCT_GATE_INNER))
                { s.Gate = building; s.GateGlobal = b->r_GlobalId; s.GateOwner = b->r_PlayerIdOwner;
                    s.Capturer = b->r_CapturedByPlayerId; s.GateState = b->r_GateState; }
            }
            s.Valid = s.Global != 0 && s.Player >= 1 && s.Player <= 8 && u->r_GlobalId == s.Global;
            return s;
        }
    }
}
