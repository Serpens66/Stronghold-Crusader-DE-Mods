using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.UnitCommands
{
    internal static class QueueNativeContract
    {
        internal static bool IsTargetCommand(int command) => command == 4 || command == 9 || command == 36;
        public const int MoveChoreOpcode = 17;
        public const int TargetOrderChoreOpcode = 36;
        public const int WaypointAppendChoreOpcode = 71;
        public const int MoveChoreHandlerRva = 0x10AE0;
        public const int TargetOrderChoreHandlerRva = 0x12BF0;
        public const int WaypointAppendChoreHandlerRva = 0x176C0;
        public const int ChoreHandlerTableRva = 0x2C7A30;
        public const int MoveChoreHandlerSize = 470;
        public const int TargetOrderChoreHandlerSize = 450;
        public const int WaypointAppendChoreHandlerSize = 487;
        public const int ChoreModeRva = 0x85F8FEC;
        public const int ChoreTribeIdRva = 0x86C132C;
        public const int ChoreCommandOrTileXRva = 0x86C1330;
        public const int ChoreTileYRva = 0x86C1334;
        public const int ChoreMoveTypeRva = 0x86C133C;
        public const int MoveQueueMarker = 0x40;
        public const int ExecutedFastMoveType = -255;
        public const int TargetQueueMarker = 0x80;
        public const int ChoreExecuteMode = 0;
        public const int ChorePackMode = 1;

        public const int GameTribePointerAdjustment = 0x2A;
        public const int ManagerRelativeWaypointIndexOffset = 0x5DC;
        public const int ManagerRelativeWaypointCountOffset = 0x5DE;
        public const int ManagerRelativeMovementModeOffset = 0x582;
        public const int GameTribeWaypointBaseOffset = 0x58A;
        public const int GameTribeWaypointIndexOffset =
            ManagerRelativeWaypointIndexOffset - GameTribePointerAdjustment;
        public const int GameTribeWaypointCountOffset =
            ManagerRelativeWaypointCountOffset - GameTribePointerAdjustment;
        public const int GameTribeMovementModeOffset =
            ManagerRelativeMovementModeOffset - GameTribePointerAdjustment;
        public const int GameUnitSize = 0x490;
        public const int GameUnitGlobalIdOffset = 0x94;
        public const int GameUnitAttackMarkerOffset = 0x68;
        public const int GameBuildingSize = 0x32C;
        public const int GameBuildingGlobalIdOffset = 0xD8;
        public const int GameBuildingAttackMarkerOffset = 0xC2;

        // Chore 71 stores the public, one-based game tribe ID. It is not a span index.
        public static int WaypointChoreValueToTribeId(int serializedTribeId) => serializedTribeId;

        public static bool TryMarkMoveTypeForQueue(int serializedMoveType, out int markedMoveType)
        {
            int payloadByte = serializedMoveType & 0xFF;
            // The sole Vanilla producer emits 0, 1 or 0x81. Bit 7 has its own meaning and
            // is stripped by the unpack thunk; bit 6 reaches the move-order event unchanged.
            int vanillaPayload = payloadByte;
            if (serializedMoveType != payloadByte ||
                (vanillaPayload != 0 && vanillaPayload != 1 && vanillaPayload != 0x81))
            {
                markedMoveType = serializedMoveType;
                return false;
            }

            markedMoveType = payloadByte | MoveQueueMarker;
            return true;
        }

        public static bool TryDecodeQueuedMoveType(int moveType, out int decodedMoveType)
        {
            if ((moveType & MoveQueueMarker) == 0)
            {
                decodedMoveType = moveType;
                return false;
            }

            decodedMoveType = moveType & ~MoveQueueMarker;
            if (IsKnownExecutedMoveType(decodedMoveType, allowQueueMarker: false))
                return true;

            decodedMoveType = moveType;
            return false;
        }

        private static bool IsKnownExecutedMoveType(int moveType, bool allowQueueMarker)
        {
            if (moveType == 0 || moveType == 1 || moveType == ExecutedFastMoveType)
                return true;
            if (!allowQueueMarker || (moveType & MoveQueueMarker) == 0)
                return false;

            int withoutQueueMarker = moveType & ~MoveQueueMarker;
            return withoutQueueMarker == 0 || withoutQueueMarker == 1 ||
                withoutQueueMarker == ExecutedFastMoveType;
        }

        public static bool TryMarkTargetCommandForQueue(int command, out int markedCommand)
        {
            if (!IsTargetCommand(command))
            {
                markedCommand = command;
                return false;
            }

            markedCommand = command | TargetQueueMarker;
            return true;
        }

        public static bool TryDecodeQueuedTargetCommand(int command, out int decodedCommand)
        {
            decodedCommand = command & ~TargetQueueMarker;
            return (command & TargetQueueMarker) != 0 &&
                IsTargetCommand(decodedCommand);
        }
    }
}
