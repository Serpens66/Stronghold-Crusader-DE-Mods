using System;
using System.Collections.Generic;

namespace AIVParser.Core
{
    public static class AivBlockedAreaCatalog
    {
        public static IReadOnlyList<AivBlockedArea> Resolve(
            AivMapperInfo mapper,
            AivGridPoint buildingAnchor,
            AivRotation rotation)
        {
            if (mapper == null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            var result = new List<AivBlockedArea>();
            switch (mapper.Value)
            {
                case 60:
                case 61:
                case 62:
                    AddNativeKeepAreas(result, mapper, buildingAnchor, rotation);
                    break;
                case 79:
                case 86:
                case 87:
                    // Native X grows opposite to the exported JSON Row axis.
                    // DE retains the classic three 5x5 barracks reservations.
                    Add(result, "Barracks yard below", -5, 0, 5, buildingAnchor, rotation);
                    Add(result, "Barracks yard right", 0, 5, 5, buildingAnchor, rotation);
                    Add(result, "Barracks yard below-right", -5, 5, 5, buildingAnchor, rotation);
                    break;
                case 88:
                case 89:
                    Add(result, "Engineers Guild yard", -5, 0, 5, buildingAnchor, rotation);
                    break;
                case 180:
                    Add(result, "Oil Smelter yard", -4, 0, 4, buildingAnchor, rotation);
                    break;
            }

            return result;
        }

        private static void AddNativeKeepAreas(
            ICollection<AivBlockedArea> result,
            AivMapperInfo mapper,
            AivGridPoint buildingAnchor,
            AivRotation rotation)
        {
            if (!mapper.FootprintSize.HasValue)
            {
                return;
            }

            // Historical evidence: RVA 0x54590 in CrusaderDE.dll SHA-256 17F8DD4A...
            // stamps these offsets. The values are data semantics, not a runtime RVA dependency.
            Add(result, "Keep native area 5x5", -2, 7, 5, buildingAnchor, rotation,
                AivBlockedAreaKind.Campfire);
            Add(result, "Keep native area 7x7", -8, 0, 7, buildingAnchor, rotation);
            Add(result, "Keep native connector 1", -7, 2, 1, buildingAnchor, rotation);
            Add(result, "Keep native connector 2", -7, 3, 1, buildingAnchor, rotation);
            Add(result, "Keep native connector 3", -7, 4, 1, buildingAnchor, rotation);
        }

        private static void Add(
            ICollection<AivBlockedArea> result,
            string name,
            int rowOffset,
            int columnOffset,
            int size,
            AivGridPoint buildingAnchor,
            AivRotation rotation,
            AivBlockedAreaKind kind = AivBlockedAreaKind.PlacementReserve)
        {
            var rawAnchor = new AivGridPoint(
                buildingAnchor.Row + rowOffset,
                buildingAnchor.Column + columnOffset);
            result.Add(new AivBlockedArea(
                name,
                kind,
                AivBlockedAreaSource.DefinitiveEditionNativeTable,
                AivGridTransform.GetFootprint(rawAnchor, size, rotation)));
        }
    }
}
