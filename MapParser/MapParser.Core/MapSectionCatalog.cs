using System.Collections.Generic;

namespace MapParser.Core
{
    public static class MapSectionCatalog
    {
        public const int Logic = 1003;
        public const int Organism = 1004;
        public const int Height = 1005;
        public const int Building = 1012;
        public const int BuildingObjects = 1013;
        public const int ExtendedBuildingObjects = 4013;
        public const int Entity = 1026;
        public const int Logic2 = 1037;
        public const int WallOwner = 1043;
        public const int DefaultHeight = 1045;

        private static readonly HashSet<int> KnownTileSectionIds = new HashSet<int>
        {
            1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010,
            1012, 1020, 1021, 1026, 1028, 1029, 1030, 1033, 1036, 1037,
            1043, 1045, 1049, 1103, 1104, 1105, 1118
        };

        public static int GetLogicalSectionId(int sectionId)
        {
            // Current editor maps double the object capacity while preserving the record layout.
            if (sectionId == ExtendedBuildingObjects)
                return BuildingObjects;

            // SCDE moved the enlarged tile layers from 1xxx to 3xxx.
            int candidate = sectionId - 2000;
            return KnownTileSectionIds.Contains(candidate) ? candidate : sectionId;
        }

        public static bool TryGetBuildingObjectRecordCount(int sectionId, out int recordCount)
        {
            if (sectionId == BuildingObjects)
            {
                recordCount = 2000;
                return true;
            }
            if (sectionId == ExtendedBuildingObjects)
            {
                recordCount = 4000;
                return true;
            }

            recordCount = 0;
            return false;
        }

        public static string GetName(int logicalSectionId)
        {
            switch (logicalSectionId)
            {
                case Logic: return "Logic";
                case Organism: return "Organism";
                case Height: return "Height";
                case Building: return "Building";
                case BuildingObjects: return "BuildingObjects";
                case Entity: return "Entity";
                case Logic2: return "Logic2";
                case WallOwner: return "WallOwner";
                case DefaultHeight: return "DefaultHeight";
                default: return "Section";
            }
        }
    }
}
