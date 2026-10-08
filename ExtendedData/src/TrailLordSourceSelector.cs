using System;
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

        internal static bool MatchesIdentity(string candidateName, int candidateType,
            string candidateConfigName, string selectedName, int selectedType,
            string selectedConfigName, bool unnamedExtendedLord)
        {
            return string.Equals(candidateName, selectedName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidateConfigName, selectedConfigName, StringComparison.OrdinalIgnoreCase) &&
                (!unnamedExtendedLord || candidateType == selectedType);
        }
        internal static int SelectIndex(IReadOnlyList<string> candidateDirectories,
            string selectedDirectory, string lordName)
        {
            if (candidateDirectories == null)
                throw new ArgumentNullException(nameof(candidateDirectories));
            string selected = string.IsNullOrWhiteSpace(selectedDirectory)
                ? null : Normalize(selectedDirectory);
            int found = -1;
            for (int index = 0; index < candidateDirectories.Count; index++)
            {
                if (selected != null && !string.Equals(Normalize(candidateDirectories[index]),
                        selected, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (found >= 0)
                    throw new InvalidDataException("The author Lord package " + lordName +
                        " has multiple matching sources; select a unique installed copy.");
                found = index;
            }
            if (found < 0)
                throw new InvalidDataException("The author Lord package " + lordName +
                    (selected == null ? " could not be found." :
                        " could not be found at its selected source path."));
            return found;
        }

        private static string Normalize(string path) =>
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
