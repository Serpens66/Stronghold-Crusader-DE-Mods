using System;
using System.Collections.Generic;
using System.IO;

namespace ExtendedData
{
    internal static class TrailLordSourceSelector
    {
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
