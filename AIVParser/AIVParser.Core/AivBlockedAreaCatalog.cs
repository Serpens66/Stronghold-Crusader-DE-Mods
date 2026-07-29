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
                    AddKeepCampfire(result, mapper, buildingAnchor, rotation);
                    break;
                case 86:
                case 87:
                    // Native X grows opposite to the exported JSON Row axis.
                    // DE retains the classic three 5x5 barracks reservations.
                    Add(result, "Barracks yard below", -5, 0, 5, buildingAnchor, rotation);
                    Add(result, "Barracks yard right", 0, 5, 5, buildingAnchor, rotation);
                    Add(result, "Barracks yard below-right", -5, 5, 5, buildingAnchor, rotation);
                    break;
                case 88:
                    Add(result, "Engineers Guild yard", -5, 0, 5, buildingAnchor, rotation);
                    break;
                case 180:
                    Add(result, "Oil Smelter yard", -4, 0, 4, buildingAnchor, rotation);
                    break;
            }

            return result;
        }

        private static void AddKeepCampfire(
            ICollection<AivBlockedArea> result,
            AivMapperInfo mapper,
            AivGridPoint buildingAnchor,
            AivRotation rotation)
        {
            if (!mapper.FootprintSize.HasValue)
            {
                return;
            }

            int keepSize = mapper.FootprintSize.Value;
            int campfireSize = Math.Min(5, keepSize);
            int centeredColumnOffset = (keepSize - campfireSize) / 2;
            var rawAnchor = new AivGridPoint(
                buildingAnchor.Row - keepSize,
                buildingAnchor.Column + centeredColumnOffset);
            var footprint = AivGridTransform.GetFootprint(
                rawAnchor,
                campfireSize,
                rotation);
            result.Add(new AivBlockedArea(
                "Keep campfire",
                AivBlockedAreaKind.Campfire,
                AivBlockedAreaSource.EditorDerivedKeepCampfire,
                footprint));
        }

        private static void Add(
            ICollection<AivBlockedArea> result,
            string name,
            int rowOffset,
            int columnOffset,
            int size,
            AivGridPoint buildingAnchor,
            AivRotation rotation)
        {
            var rawAnchor = new AivGridPoint(
                buildingAnchor.Row + rowOffset,
                buildingAnchor.Column + columnOffset);
            result.Add(new AivBlockedArea(
                name,
                AivBlockedAreaKind.PlacementReserve,
                AivBlockedAreaSource.DefinitiveEditionNativeTable,
                AivGridTransform.GetFootprint(rawAnchor, size, rotation)));
        }
    }
}
