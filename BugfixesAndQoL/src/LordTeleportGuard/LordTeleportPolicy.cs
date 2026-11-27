using APIShared.Units;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL.LordTeleportGuard
{
    // BQoL policy only. APIShared owns the shared relocation entry and its native integrity.
    internal static unsafe class LordTeleportPolicy
    {
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int AiLineUpRva = 0x8574C44;
        internal const int AiLineUpAnchorRva = 0x28A7D;

        internal static bool ShouldEnable(bool available, bool modEnabled, bool optionEnabled) =>
            available && modEnabled && optionEnabled;

        internal static bool MatchesIdentity(GameUnit* unit, int id, uint globalId, eChimps type, int owner) =>
            id >= GameUnitManager.FirstLiveUnitId && id <= GameUnitManager.LastLiveUnitId &&
            globalId != 0 && unit != null && unit->r_AliveState == AliveState.IsAlive &&
            unit->r_GlobalId == globalId && unit->r_UnitChimp == type && unit->r_ControllableForPlayerId == owner;

        internal static bool ShouldCancel(UnitRelocationOrigin origin, eChimps type, bool alive,
            int owner, bool ai, int homeX, int homeY, int targetX, int targetY) =>
            origin != UnitRelocationOrigin.InvalidStandingTerrainRecovery &&
            type == eChimps.CHIMP_TYPE_LORD && alive && owner >= 1 && owner <= GamePlayerManagerAPI.MAX_PLAYERS &&
            !ai && targetX == homeX && targetY == homeY;

        internal static void ValidateLayouts()
        {
            Offset(nameof(GameUnit.r_AliveState), 0x88);
            Offset(nameof(GameUnit.r_UnitChimp), 0x8A);
            Offset(nameof(GameUnit.r_ControllableForPlayerId), 0x92);
            Offset(nameof(GameUnit.r_GlobalId), 0x94);
            Offset(nameof(GameUnit.r_LinkedProductionBuildingTileEndX), 0x336);
            Offset(nameof(GameUnit.r_LinkedProductionBuildingTileEndY), 0x338);
            if (Marshal.SizeOf(typeof(GameUnit)) != GameUnitManager.UnitRecordByteSize ||
                GameUnitManager.UnitRecordByteSize != 0x490 || GamePlayerManagerAPI.MAX_PLAYERS != 8)
                throw new InvalidOperationException("Lord policy unit/player layout changed.");
        }

        // The public SE GetAILord/IsAIPlayer reads this exact native lineup (players1..8).
        // The anchor remains BQoL evidence for its human/AI decision, not an SDK hook dependency.
        internal static void ValidateAiLineUpAnchor(ReadOnlySpan<byte> memory, ulong image, ulong resolvedLineUp)
        {
            if (image == 0 || resolvedLineUp != image + AiLineUpRva || memory.Length < AiLineUpAnchorRva + 7)
                throw new InvalidOperationException("Lord policy AI lineup binding changed.");
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(memory.Slice(AiLineUpAnchorRva, 7).ToArray()));
            decoder.IP = image + AiLineUpAnchorRva;
            Instruction instruction = decoder.Decode();
            if (instruction.Length != 7 || instruction.Mnemonic != Mnemonic.Lea || instruction.Op0Register != Register.RAX ||
                !instruction.IsIPRelativeMemoryOperand || instruction.IPRelativeMemoryAddress != resolvedLineUp)
                throw new InvalidOperationException("Lord policy AI lineup anchor changed.");
        }

        private static void Offset(string field, int expected)
        {
            if (Marshal.OffsetOf(typeof(GameUnit), field).ToInt32() != expected)
                throw new InvalidOperationException("Lord policy field layout changed: " + field);
        }
    }
}
