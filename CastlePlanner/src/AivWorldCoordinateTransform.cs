using AIVParser.Core;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using SHCDESE.Interop.Enums;

namespace CastlePlanner
{
    internal static class AivProjectileTransform
    {
        private const int MapSizeInTiles = 800;
        private const int ProjectileUnitsPerTile = 8;

        public static int ToProjectileCoordinate(int tileCoordinate)
        {
            if (tileCoordinate < 0 || tileCoordinate >= MapSizeInTiles)
                throw new ArgumentOutOfRangeException(nameof(tileCoordinate));

            return checked(tileCoordinate * ProjectileUnitsPerTile);
        }
    }

    internal static class AivNativeKeepAlignment
    {
        public static AivWorldTile ResolveNativeReference(
            AivGridPoint keepAnchor,
            int footprintSize,
            int liveKeepX,
            int liveKeepY,
            AivRotation rotation)
        {
            if (footprintSize < 1)
                throw new ArgumentOutOfRangeException(nameof(footprintSize));

            AivFootprint footprint = AivGridTransform.GetFootprint(
                keepAnchor,
                footprintSize,
                AivRotation.Degrees0);
            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            for (int row = footprint.Minimum.Row; row <= footprint.Maximum.Row; row++)
            {
                for (int column = footprint.Minimum.Column;
                     column <= footprint.Maximum.Column;
                     column++)
                {
                    AivWorldTile projected = AivWorldTransform.ProjectNativeFit(
                        new AivGridPoint(row, column),
                        0,
                        0,
                        rotation);
                    minimumX = Math.Min(minimumX, projected.X);
                    minimumY = Math.Min(minimumY, projected.Y);
                }
            }

            // Native rotates the complete 100x100 AIV grid. Undo the resulting
            // Keep-footprint offset so the projected footprint starts at the live Keep.
            return new AivWorldTile(liveKeepX - minimumX, liveKeepY - minimumY);
        }
    }

    internal static class AivNativeBuildingPlacement
    {
        public static AivWorldTile ResolveBuildStructureOrigin(
            AivGridPoint rawAnchor,
            int footprintSize,
            int nativeReferenceX,
            int nativeReferenceY,
            AivRotation rotation)
        {
            if (footprintSize < 1)
                throw new ArgumentOutOfRangeException(nameof(footprintSize));

            AivFootprint rawFootprint = AivGridTransform.GetFootprint(
                rawAnchor,
                footprintSize,
                AivRotation.Degrees0);
            int minimumX = int.MaxValue;
            int minimumY = int.MaxValue;
            for (int row = rawFootprint.Minimum.Row; row <= rawFootprint.Maximum.Row; row++)
            {
                for (int column = rawFootprint.Minimum.Column;
                     column <= rawFootprint.Maximum.Column;
                     column++)
                {
                    AivWorldTile projected = AivWorldTransform.ProjectNativeFit(
                        new AivGridPoint(row, column),
                        nativeReferenceX,
                        nativeReferenceY,
                        rotation);
                    minimumX = Math.Min(minimumX, projected.X);
                    minimumY = Math.Min(minimumY, projected.Y);
                }
            }

            // BuildStructure expects the minimum world corner of the rotated
            // reservation, not the projected AIV anchor stored in the frame.
            return new AivWorldTile(minimumX, minimumY);
        }
    }

    internal readonly struct AivCompoundBuildingPlacement
    {
        public AivCompoundBuildingPlacement(
            int sourceOrdinal,
            eMappers mapper,
            int encodedPosition,
            AivWorldTile buildOrigin)
        {
            SourceOrdinal = sourceOrdinal;
            Mapper = mapper;
            EncodedPosition = encodedPosition;
            BuildOrigin = buildOrigin;
        }

        public int SourceOrdinal { get; }
        public eMappers Mapper { get; }
        public int EncodedPosition { get; }
        public AivWorldTile BuildOrigin { get; }
    }

    internal static class AivCompoundBuildingPlan
    {
        public static List<AivCompoundBuildingPlacement> Create(
            AivJsonDocument document,
            int nativeReferenceX,
            int nativeReferenceY,
            AivRotation rotation)
        {
            if (document?.frames == null)
                throw new ArgumentNullException(nameof(document));

            var result = new List<AivCompoundBuildingPlacement>();
            int sourceOrdinal = 0;
            foreach (AivJsonFrame frame in document.frames)
            {
                eMappers mapper = (eMappers)frame.itemType;
                if (mapper != eMappers.MAPPER_GRANARY &&
                    mapper != eMappers.MAPPER_ARMOURY)
                {
                    continue;
                }

                if (frame.tilePositionOfsets == null)
                    throw new InvalidOperationException($"AIV frame {mapper} has no positions.");

                int footprintSize = AivMapperCatalog.Resolve(frame.itemType)
                    .FootprintSize.GetValueOrDefault(1);
                if (footprintSize < 1)
                    throw new InvalidOperationException($"No footprint is known for {mapper}.");

                foreach (int encodedPosition in frame.tilePositionOfsets)
                {
                    result.Add(new AivCompoundBuildingPlacement(
                        sourceOrdinal++,
                        mapper,
                        encodedPosition,
                        AivNativeBuildingPlacement.ResolveBuildStructureOrigin(
                            new AivGridPoint(encodedPosition),
                            footprintSize,
                            nativeReferenceX,
                            nativeReferenceY,
                            rotation)));
                }
            }

            return result;
        }
    }
}
