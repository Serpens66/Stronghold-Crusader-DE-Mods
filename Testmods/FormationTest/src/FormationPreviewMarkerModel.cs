using System;
using System.Collections.Generic;

namespace FormationTest
{
    internal static class FormationPreviewMarkerModel
    {
        internal const int NativeTileCount = 320800;
        internal const int FirstSyntheticIdentity = 250;
        internal const int NativeMode8IdentityCapacity = 4250;
        internal const int MaximumMarkers =
            NativeMode8IdentityCapacity - FirstSyntheticIdentity;

        internal static int[] NormalizeTileIds(IEnumerable<int> tileIds)
        {
            if (tileIds == null)
                return Array.Empty<int>();

            var unique = new SortedSet<int>();
            foreach (int tileId in tileIds)
            {
                if ((uint)tileId < NativeTileCount)
                    unique.Add(tileId);
            }

            int count = Math.Min(unique.Count, MaximumMarkers);
            var result = new int[count];
            int index = 0;
            foreach (int tileId in unique)
            {
                if (index == result.Length)
                    break;
                result[index++] = tileId;
            }
            return result;
        }
    }
}
