using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
            string selectedDirectory, string lordName, string configName = null)
        {
            if (candidateDirectories == null)
                throw new ArgumentNullException(nameof(candidateDirectories));
            string selected = string.IsNullOrWhiteSpace(selectedDirectory)
                ? null : Normalize(selectedDirectory);
            var matching = new List<int>();
            for (int index = 0; index < candidateDirectories.Count; index++)
                if (selected == null || string.Equals(Normalize(candidateDirectories[index]),
                        selected, StringComparison.OrdinalIgnoreCase))
                    matching.Add(index);
            if (matching.Count == 0)
                throw new InvalidDataException("The author Lord package " + lordName +
                    (selected == null ? " could not be found." :
                        " could not be found at its selected source path."));
            if (matching.Count == 1) return matching[0];
            if (selected != null || string.IsNullOrWhiteSpace(configName))
                throw new InvalidDataException("The author Lord package " + lordName +
                    " has multiple matching sources; select a unique installed copy.");

            // Vanilla's Trail codec drops both path and workshop. Only equivalent packages
            // may stand in for that lost selection; the decoded false flag is not evidence.
            matching.Sort((left, right) =>
            {
                string a = Normalize(candidateDirectories[left]);
                string b = Normalize(candidateDirectories[right]);
                int order = StringComparer.OrdinalIgnoreCase.Compare(a, b);
                return order != 0 ? order : StringComparer.Ordinal.Compare(a, b);
            });
            int chosen = matching[0];
            string baselinePath = Normalize(candidateDirectories[chosen]);
            PackageState baseline = ReadPackage(baselinePath, configName);
            for (int offset = 1; offset < matching.Count; offset++)
            {
                string candidatePath = Normalize(candidateDirectories[matching[offset]]);
                PackageState candidate = ReadPackage(candidatePath, configName);
                string difference = FindDifference(baseline, candidate);
                if (difference != null)
                    throw new InvalidDataException("The author Lord package " + lordName +
                        " has conflicting matching sources [" + baselinePath + "] and [" +
                        candidatePath + "]: " + difference + ".");
            }
            return chosen;
        }

        private sealed class PackageState
        {
            internal string GameplayDigest;
            internal readonly Dictionary<string, object> Json =
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        private static PackageState ReadPackage(string directory, string configName)
        {
            try
            {
                LordPackageFileState files = LordPackageFingerprint.Capture(directory, configName);
                var state = new PackageState { GameplayDigest = files.Digest };
                string[] relevant = { configName + ".modlord.json", "Override/Fixes/preferences.json",
                    "info.json", "lordmeta.json" };
                // Inventory supplies actual names and fails on inaccessible paths/links. File.Exists
                // alone would conflate an unreadable file with a legitimately absent one.
                foreach (LordPackageFile file in LordPackageFingerprint.Inventory(directory))
                    foreach (string relative in relevant)
                        if (string.Equals(file.RelativePath, relative, StringComparison.OrdinalIgnoreCase))
                        {
                            object json;
                            try
                            {
                                json = Shared.DependencyFreeJson.Parse(File.ReadAllText(file.FullPath, new UTF8Encoding(false, true)));
                                if (!(json is Dictionary<string, object>))
                                    throw new InvalidDataException("Expected a JSON object.");
                            }
                            catch (Exception exception)
                            {
                                throw new InvalidDataException("Cannot compare " + relative + ": " +
                                    exception.Message, exception);
                            }
                            state.Json.Add(relative, json);
                        }
                return state;
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("Cannot verify matching Lord source [" + directory +
                    "]: " + exception.Message, exception);
            }
        }

        private static string FindDifference(PackageState left, PackageState right)
        {
            if (!string.Equals(left.GameplayDigest, right.GameplayDigest, StringComparison.Ordinal))
                return "gameplay file inventories or contents differ";
            var names = new SortedSet<string>(left.Json.Keys, StringComparer.OrdinalIgnoreCase);
            names.UnionWith(right.Json.Keys);
            foreach (string name in names)
            {
                bool hasLeft = left.Json.TryGetValue(name, out object leftJson);
                bool hasRight = right.Json.TryGetValue(name, out object rightJson);
                if (hasLeft != hasRight) return name + " is present in only one source";
                if (!JsonEquals(leftJson, rightJson)) return name + " contains different JSON values";
            }
            return null;
        }

        private static bool JsonEquals(object left, object right)
        {
            if (left is Dictionary<string, object> leftObject)
            {
                if (!(right is Dictionary<string, object> rightObject) ||
                    leftObject.Count != rightObject.Count) return false;
                foreach (var property in leftObject)
                    if (!rightObject.TryGetValue(property.Key, out object value) ||
                        !JsonEquals(property.Value, value)) return false;
                return true;
            }
            if (left is List<object> leftArray)
            {
                if (!(right is List<object> rightArray) || leftArray.Count != rightArray.Count)
                    return false;
                for (int index = 0; index < leftArray.Count; index++)
                    if (!JsonEquals(leftArray[index], rightArray[index])) return false;
                return true;
            }
            if (right is Dictionary<string, object> || right is List<object>) return false;
            return string.Equals(Shared.DependencyFreeJson.Serialize(left),
                Shared.DependencyFreeJson.Serialize(right), StringComparison.Ordinal);
        }

        private static string Normalize(string path) =>
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
