using SHCDESE.Interop;

namespace BugfixesAndQoL.AreaConstruction
{
    internal static class PlacementVariants
    {
        internal static bool SameSelection(eMappers first, eMappers second) =>
            first == second || (TryGetCycle(first, out var a, out var b) && second >= a && second <= b);

        internal static eMappers Next(eMappers mapper) =>
            TryGetCycle(mapper, out var first, out var last) ? mapper == last ? first : mapper + 1 : mapper;

        // Vanilla's closed cycles in 0x7BC50 with one structure, cost row and footprint.
        // The pond cycle leading into ravines crosses footprints and placement paths.
        private static bool TryGetCycle(eMappers mapper, out eMappers first, out eMappers last)
        {
            (first, last) = mapper switch
            {
                >= eMappers.MAPPER_GARDEN1 and <= eMappers.MAPPER_GARDEN6 => (eMappers.MAPPER_GARDEN1, eMappers.MAPPER_GARDEN6),
                >= eMappers.MAPPER_GARDEN7 and <= eMappers.MAPPER_GARDEN9 => (eMappers.MAPPER_GARDEN7, eMappers.MAPPER_GARDEN9),
                >= eMappers.MAPPER_GARDEN10 and <= eMappers.MAPPER_GARDEN12 => (eMappers.MAPPER_GARDEN10, eMappers.MAPPER_GARDEN12),
                >= eMappers.MAPPER_STATUE1 and <= eMappers.MAPPER_STATUE5 => (eMappers.MAPPER_STATUE1, eMappers.MAPPER_STATUE5),
                >= eMappers.MAPPER_SHRINE1 and <= eMappers.MAPPER_SHRINE2 => (eMappers.MAPPER_SHRINE1, eMappers.MAPPER_SHRINE2),
                >= eMappers.MAPPER_CESS_PIT1 and <= eMappers.MAPPER_CESS_PIT4 => (eMappers.MAPPER_CESS_PIT1, eMappers.MAPPER_CESS_PIT4),
                >= eMappers.MAPPER_POND1 and <= eMappers.MAPPER_POND2 => (eMappers.MAPPER_POND1, eMappers.MAPPER_POND2),
                >= eMappers.MAPPER_POND3 and <= eMappers.MAPPER_POND4 => (eMappers.MAPPER_POND3, eMappers.MAPPER_POND4),
                >= eMappers.MAPPER_POND5 and <= eMappers.MAPPER_POND6 => (eMappers.MAPPER_POND5, eMappers.MAPPER_POND6),
                _ => default
            };
            return first != default;
        }
    }
}
