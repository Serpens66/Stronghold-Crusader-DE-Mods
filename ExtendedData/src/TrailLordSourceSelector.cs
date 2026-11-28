using System.Collections.Generic;
using System.IO;

namespace ExtendedData
{
    internal static class TrailLordSourceSelector
    {
        internal static string ResolveLordName(string savedName, int lordType,
            IReadOnlyList<string> extendedNames)
        {
            if (!string.IsNullOrWhiteSpace(savedName)) return savedName;
            if (extendedNames == null || lordType < 0 || lordType >= extendedNames.Count ||
                string.IsNullOrWhiteSpace(extendedNames[lordType]))
                throw new InvalidDataException("An unnamed author Lord has no valid Extended Lord type: " + lordType);
            return extendedNames[lordType];
        }
    }
}
