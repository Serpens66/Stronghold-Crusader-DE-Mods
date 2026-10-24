using SHCDESE.Interop;

namespace CastlePlanner
{
    internal static class SupplementalPrefabVisualPolicy
    {
        // Native AIV builder 51790 uses 0x0f for every mapper accepted below.
        // This selects automatic sprites, whereas 0 selects camera-relative sheets.
        // No Extender enum names this sentinel. See UpdateToNewDLL.md for the
        // hash-bound audit, including the constructors that preserve family styles.
        private const int AutomaticSpriteVariation = 0x0f;

        internal static bool TryGetVariation(eMappers mapper, out int variation)
        {
            variation = AutomaticSpriteVariation;
            switch (mapper)
            {
                case eMappers.MAPPER_STORES:
                case eMappers.MAPPER_GRANARY:
                case eMappers.MAPPER_ARMOURY:
                case eMappers.MAPPER_GARDEN1:
                case eMappers.MAPPER_GARDEN7:
                case eMappers.MAPPER_GARDEN10:
                case eMappers.MAPPER_MAYPOLE:
                case eMappers.MAPPER_GALLOWS:
                case eMappers.MAPPER_STOCKS:
                case eMappers.MAPPER_CESS_PIT1:
                case eMappers.MAPPER_BURNING_STAKE:
                case eMappers.MAPPER_GIBBET:
                case eMappers.MAPPER_DUNGEON:
                case eMappers.MAPPER_RACK_STRETCHING:
                case eMappers.MAPPER_CHOPPING_BLOCK:
                case eMappers.MAPPER_DUNKING_STOOL:
                case eMappers.MAPPER_STATUE1:
                case eMappers.MAPPER_SHRINE1:
                case eMappers.MAPPER_DANCING_BEAR:
                case eMappers.MAPPER_POND1:
                case eMappers.MAPPER_POND3:
                    return true;
                default:
                    // New supplemental types need an audit: direction/style arguments
                    // are not interchangeable with automatic sprite selection.
                    return false;
            }
        }
    }
}
