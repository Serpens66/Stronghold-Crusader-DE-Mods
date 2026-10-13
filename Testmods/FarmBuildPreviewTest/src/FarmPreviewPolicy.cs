using System;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace FarmBuildPreviewTest
{
    internal readonly struct GroundCounts
    {
        internal readonly int Coverage, Green, Resource;
        internal GroundCounts(int coverage, int green, int resource)
        { Coverage = coverage; Green = green; Resource = resource; }
    }
    internal static class FarmPreviewPolicy
    {
        // Native construction palette protocol tokens, not terrain/building IDs.
        internal const int RedPreview = 55, DarkPreview = 62;
        internal static int Scale(eMappers mapper)
        {
            switch (mapper)
            {
                case eMappers.MAPPER_WHEATFARM: case eMappers.MAPPER_HOPSFARM: return 9;
                case eMappers.MAPPER_APPLEFARM: case eMappers.MAPPER_CATTLEFARM: return 10;
                case eMappers.MAPPER_QUARRY: return 6;
                case eMappers.MAPPER_IRON_MINE: case eMappers.MAPPER_PITCH_WORKINGS: return 4;
                default: return 0;
            }
        }
        internal static bool IsFarm(eMappers mapper) => mapper == eMappers.MAPPER_WHEATFARM ||
            mapper == eMappers.MAPPER_HOPSFARM || mapper == eMappers.MAPPER_APPLEFARM || mapper == eMappers.MAPPER_CATTLEFARM;
        internal static TilePropertyFlag ResourceFlag(eMappers mapper) => mapper == eMappers.MAPPER_QUARRY ? TilePropertyFlag.HasStone :
            mapper == eMappers.MAPPER_IRON_MINE ? TilePropertyFlag.HasIron : mapper == eMappers.MAPPER_PITCH_WORKINGS ? TilePropertyFlag.IsPitch : TilePropertyFlag.None;
        internal static int Required(eMappers mapper) => IsFarm(mapper) ? Scale(mapper)*Scale(mapper) :
            mapper == eMappers.MAPPER_QUARRY ? 8 : mapper == eMappers.MAPPER_IRON_MINE ? 4 : mapper == eMappers.MAPPER_PITCH_WORKINGS ? 1 : 0;
        internal static int Green(int logic, byte terrain)
        {
            if (((TilePropertyFlag)(uint)logic & TilePropertyFlag.River) != 0) return 0;
            var type = (TileType)terrain;
            return ((type & TileType.OasisGrass) != 0 ? 1 : 0) + ((type & TileType.ThickFoliage) != 0 ? 1 : 0);
        }
        internal static int Coverage(int logic, byte terrain) => Green(logic, terrain) +
            ((((TilePropertyFlag)(uint)logic & TilePropertyFlag.River) == 0 && ((TileType)terrain & TileType.Foliage) != 0) ? 1 : 0);
        internal static bool Missing(eMappers mapper, GroundCounts counts, int logic, byte terrain)
        {
            if (IsFarm(mapper)) return (counts.Coverage < Required(mapper) && Coverage(logic,terrain) == 0) ||
                (counts.Green < 50 && Green(logic,terrain) == 0);
            return counts.Resource < Required(mapper) && ((TilePropertyFlag)(uint)logic & ResourceFlag(mapper)) == 0;
        }
        internal static int Mark(eMappers mapper, bool blocked, ReadOnlySpan<int> logic,
            ReadOnlySpan<byte> terrain, Span<int> preview, out GroundCounts counts)
        {
            if (logic.Length != terrain.Length || terrain.Length != preview.Length || logic.Length != Scale(mapper)*Scale(mapper) || Scale(mapper) == 0)
                throw new ArgumentException("Unsupported or inconsistent footprint.");
            int coverage=0, green=0, resource=0;
            for (int i=0;i<logic.Length;i++)
            {
                coverage += Coverage(logic[i],terrain[i]); green += Green(logic[i],terrain[i]);
                if (((TilePropertyFlag)(uint)logic[i] & ResourceFlag(mapper)) != 0) resource++;
            }
            counts = new GroundCounts(coverage,green,resource);
            if (!blocked) return 0;
            int changed=0;
            for (int i=0;i<preview.Length;i++)
                if (preview[i] == DarkPreview && Missing(mapper,counts,logic[i],terrain[i])) { preview[i]=RedPreview; changed++; }
            return changed;
        }
    }
}
