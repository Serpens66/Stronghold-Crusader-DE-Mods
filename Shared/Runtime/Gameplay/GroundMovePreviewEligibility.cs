using System;
using System.Runtime.InteropServices;

namespace Shared
{
    internal enum GroundMovePreviewRejection
    {
        None = 0,
        OutsideMap,
        CursorOutsideGame,
        CursorSnapshotMismatch,
        UnderCursorUnit,
        HoveredUnit,
        TileOccupiedByUnit,
        HoveredBuilding,
        HoveredWall,
        TileOccupiedByBuilding,
        TargetUnavailable,
        MissingPathComponent,
        NonMoveCommandMode
    }

    internal readonly struct GroundMovePreviewSnapshot
    {
        internal GroundMovePreviewSnapshot(
            bool insideMap,
            bool cursorInGame,
            bool cursorSnapshotMatches,
            int underCursorUnitCount,
            int hoveredUnitId,
            int tileUnitId,
            int hoveredBuildingId,
            bool hoveringWall,
            int tileBuildingId,
            bool targetAvailable,
            bool hasPathComponent)
        {
            InsideMap = insideMap;
            CursorInGame = cursorInGame;
            CursorSnapshotMatches = cursorSnapshotMatches;
            UnderCursorUnitCount = underCursorUnitCount;
            HoveredUnitId = hoveredUnitId;
            TileUnitId = tileUnitId;
            HoveredBuildingId = hoveredBuildingId;
            HoveringWall = hoveringWall;
            TileBuildingId = tileBuildingId;
            TargetAvailable = targetAvailable;
            HasPathComponent = hasPathComponent;
        }

        internal bool InsideMap { get; }
        internal bool CursorInGame { get; }
        internal bool CursorSnapshotMatches { get; }
        internal int UnderCursorUnitCount { get; }
        internal int HoveredUnitId { get; }
        internal int TileUnitId { get; }
        internal int HoveredBuildingId { get; }
        internal bool HoveringWall { get; }
        internal int TileBuildingId { get; }
        internal bool TargetAvailable { get; }
        internal bool HasPathComponent { get; }
    }

    internal static class GroundMovePreviewEligibility
    {
        internal const int OrdinaryMoveCommandMode = 1;

        internal static GroundMovePreviewRejection EvaluateCommandMode(
            int nativeCommandMode) =>
            nativeCommandMode == OrdinaryMoveCommandMode
                ? GroundMovePreviewRejection.None
                : GroundMovePreviewRejection.NonMoveCommandMode;

        internal static GroundMovePreviewRejection EvaluateInitial(
            GroundMovePreviewSnapshot snapshot)
        {
            GroundMovePreviewRejection fixedTarget = EvaluateFixedTarget(
                snapshot.InsideMap,
                snapshot.TileUnitId,
                snapshot.TileBuildingId,
                snapshot.TargetAvailable,
                snapshot.HasPathComponent);
            if (fixedTarget != GroundMovePreviewRejection.None)
                return fixedTarget;
            if (!snapshot.CursorInGame)
                return GroundMovePreviewRejection.CursorOutsideGame;
            if (!snapshot.CursorSnapshotMatches)
                return GroundMovePreviewRejection.CursorSnapshotMismatch;
            if (snapshot.UnderCursorUnitCount > 0)
                return GroundMovePreviewRejection.UnderCursorUnit;
            if (snapshot.HoveredUnitId > 0)
                return GroundMovePreviewRejection.HoveredUnit;
            if (snapshot.HoveredBuildingId > 0)
                return GroundMovePreviewRejection.HoveredBuilding;
            if (snapshot.HoveringWall)
                return GroundMovePreviewRejection.HoveredWall;
            return GroundMovePreviewRejection.None;
        }

        internal static GroundMovePreviewRejection EvaluateFixedTarget(
            bool insideMap,
            int tileUnitId,
            int tileBuildingId,
            bool targetAvailable,
            bool hasPathComponent)
        {
            if (!insideMap)
                return GroundMovePreviewRejection.OutsideMap;
            if (tileUnitId > 0)
                return GroundMovePreviewRejection.TileOccupiedByUnit;
            if (tileBuildingId > 0)
                return GroundMovePreviewRejection.TileOccupiedByBuilding;
            if (!targetAvailable)
                return GroundMovePreviewRejection.TargetUnavailable;
            if (!hasPathComponent)
                return GroundMovePreviewRejection.MissingPathComponent;
            return GroundMovePreviewRejection.None;
        }
    }

    internal sealed class NativeTroopCommandModeReader
    {
        internal const int CommandModeRva = 0x67E8410;
        internal const int AttackHereSetupRva = 0x90729;
        internal const int CommandDispatcherReadRva = 0x8D323;

        private static readonly byte[] AttackHereSetupBytes =
        {
            0xC7, 0x05, 0xE1, 0x7C, 0x75, 0x06, 0x05, 0x00, 0x00, 0x00,
            0xC7, 0x05, 0xD3, 0x7C, 0x75, 0x06, 0x05, 0x00, 0x00, 0x00
        };

        private static readonly byte[] CommandDispatcherReadBytes =
        {
            0xBA, 0x02, 0x00, 0x00, 0x00,
            0x8B, 0x05, 0xE2, 0xB0, 0x75, 0x06
        };

        private readonly IntPtr commandModeAddress;

        internal NativeTroopCommandModeReader(
            IntPtr moduleHandle,
            ReadOnlySpan<byte> nativeImage)
        {
            if (moduleHandle == IntPtr.Zero)
                throw new ArgumentException(
                    "The native module handle must not be zero.", nameof(moduleHandle));

            ValidateContract(nativeImage);
            commandModeAddress = new IntPtr(
                checked(moduleHandle.ToInt64() + CommandModeRva));
        }

        internal int Read() => Marshal.ReadInt32(commandModeAddress);

        internal static void ValidateContract(ReadOnlySpan<byte> nativeImage)
        {
            ValidateBytes(
                nativeImage,
                AttackHereSetupRva,
                AttackHereSetupBytes,
                "Troops_AttackHere command-mode setup");
            ValidateBytes(
                nativeImage,
                CommandDispatcherReadRva,
                CommandDispatcherReadBytes,
                "troop command dispatcher mode read");
        }

        private static void ValidateBytes(
            ReadOnlySpan<byte> nativeImage,
            int rva,
            byte[] expected,
            string name)
        {
            if (rva < 0 || expected == null ||
                rva > nativeImage.Length - expected.Length)
            {
                throw new InvalidOperationException(
                    $"{name} lies outside the native image.");
            }

            for (int index = 0; index < expected.Length; index++)
            {
                if (nativeImage[rva + index] != expected[index])
                {
                    throw new InvalidOperationException(
                        $"{name} byte mismatch at RVA 0x{rva + index:X}: " +
                        $"expected 0x{expected[index]:X2}, " +
                        $"got 0x{nativeImage[rva + index]:X2}.");
                }
            }
        }
    }
}
