using System;
using System.Collections.Generic;
using System.IO;

namespace BugfixesAndQoL
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Check("load requester exposes built-ins", true,
                VanillaMapEditorPolicy.ShouldExposeBuiltIns(true, true));
            Check("save requester keeps user-only list", false,
                VanillaMapEditorPolicy.ShouldExposeBuiltIns(true, false));
            Check("disabled feature keeps user-only list", false,
                VanillaMapEditorPolicy.ShouldExposeBuiltIns(false, true));

            TestMergeAndFiltering();
            TestSortModes();
            TestMissingUserMapFiltering();
            TestDeletePaths();
            TestProtectedSavePaths();

            Console.WriteLine(failures == 0
                ? "Vanilla map-editor policy tests passed."
                : $"Vanilla map-editor policy tests failed: {failures}.");
            return failures == 0 ? 0 : 1;
        }

        private static void TestMergeAndFiltering()
        {
            Header user = HeaderAt("User", @"C:\Users\Test\Maps\User.map", false, true);
            Header freebuild = HeaderAt("Freebuild", @"C:\Game\StreamingAssets\Maps\Freebuild.map", true, true);
            Header invasion = HeaderAt("Invasion", @"C:\Game\StreamingAssets\Maps\Invasion.map", true, true);
            Header multiplayer = HeaderAt("Multiplayer", @"C:\Game\StreamingAssets\Maps\Multiplayer.map", true, true);
            Header locked = HeaderAt("Locked", @"C:\Game\StreamingAssets\Maps\Locked.map", true, false);
            Header nonBuiltIn = HeaderAt("Workshop", @"C:\Workshop\Workshop.map", false, true);
            Header duplicate = HeaderAt("Duplicate", @"c:\game\streamingassets\maps\FREEBUILD.map", true, true);

            List<Header> merged = Merge(
                new[] { user },
                new[]
                {
                    new[] { freebuild, locked },
                    new[] { invasion, nonBuiltIn },
                    new[] { multiplayer, duplicate },
                },
                0,
                true);

            Check("user and three built-in categories remain", 4, merged.Count);
            Check("Freebuild included", true, merged.Contains(freebuild));
            Check("Invasion included", true, merged.Contains(invasion));
            Check("multiplayer included", true, merged.Contains(multiplayer));
            Check("locked map excluded", false, merged.Contains(locked));
            Check("non-built-in candidate excluded", false, merged.Contains(nonBuiltIn));
            Check("case-insensitive duplicate excluded", false, merged.Contains(duplicate));
        }

        private static void TestSortModes()
        {
            Header alpha = HeaderAt("Alpha", @"C:\Maps\Alpha.map", false, true, 3, 100, "Zeta");
            Header beta = HeaderAt("Beta", @"C:\Maps\Beta.map", true, true, 1, 300, "Alpha");
            alpha.Written = new DateTime(2025, 1, 2);
            beta.Written = new DateTime(2024, 1, 2);

            Check("name ascending", "Alpha", Merge(new[] { beta, alpha }, EmptyGroups(), 0, true)[0].Name);
            Check("date ascending", "Beta", Merge(new[] { alpha, beta }, EmptyGroups(), 1, true)[0].Name);
            Check("size descending", "Beta", Merge(new[] { alpha, beta }, EmptyGroups(), 3, false)[0].Name);
            Check("type ascending", "Beta", Merge(new[] { alpha, beta }, EmptyGroups(), 4, true)[0].Name);
        }

        private static void TestProtectedSavePaths()
        {
            const string builtIn = @"C:\Game\StreamingAssets\Maps";
            const string user = @"C:\Users\Test\Maps";
            string protectedPath = @"c:/GAME/StreamingAssets/Maps/Close Encounters.map";
            string redirected = VanillaMapEditorPolicy.ResolveProtectedSavePath(
                protectedPath, builtIn, user, true, true);
            Check("protected target redirected", Path.Combine(user, "Close Encounters.map"), redirected);

            string normalUserPath = Path.Combine(user, "Close Encounters.map");
            Check("user target unchanged", normalUserPath,
                VanillaMapEditorPolicy.ResolveProtectedSavePath(normalUserPath, builtIn, user, true, true));

            string similarPrefix = @"C:\Game\StreamingAssets\MapsBackup\Close Encounters.map";
            Check("similar directory prefix unchanged", similarPrefix,
                VanillaMapEditorPolicy.ResolveProtectedSavePath(similarPrefix, builtIn, user, true, true));

            Check("non-map save unchanged", protectedPath,
                VanillaMapEditorPolicy.ResolveProtectedSavePath(protectedPath, builtIn, user, true, false));
            Check("disabled protection unchanged", protectedPath,
                VanillaMapEditorPolicy.ResolveProtectedSavePath(protectedPath, builtIn, user, false, true));
        }

        private static void TestMissingUserMapFiltering()
        {
            Header present = HeaderAt("Present", @"C:\Users\Test\Maps\Present.map", false, true);
            Header deleted = HeaderAt("Deleted", @"C:\Users\Test\Maps\Deleted.map", false, true);
            Header builtIn = HeaderAt("BuiltIn", @"C:\Game\Maps\BuiltIn.map", true, true);
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                present.Path,
            };

            List<Header> filtered = VanillaMapEditorPolicy.RemoveMissingUserMaps(
                new[] { present, deleted, builtIn },
                header => header.BuiltIn,
                header => header.Path,
                existing.Contains);

            Check("present user map retained", true, filtered.Contains(present));
            Check("deleted cached user map removed", false, filtered.Contains(deleted));
            Check("built-in retained without user-file existence check", true, filtered.Contains(builtIn));
        }

        private static void TestDeletePaths()
        {
            const string user = @"C:\Users\Test\Maps";
            string directMap = @"c:/USERS/Test/Maps/My Map.MAP";
            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.GetFullPath(directMap),
                Path.GetFullPath(@"C:\Users\Test\Maps\Sub\Nested.map"),
                Path.GetFullPath(@"C:\Users\Test\MapsBackup\Prefix.map"),
                Path.GetFullPath(@"C:\Game\StreamingAssets\Maps\Vanilla.map"),
                Path.GetFullPath(@"C:\Users\Test\Maps\Wrong.sav"),
            };

            Check("direct user map deletable", true,
                VanillaMapEditorPolicy.TryResolveDeletableUserMapPath(
                    directMap, user, existing.Contains, out string resolved));
            Check("delete path normalized", Path.GetFullPath(directMap), resolved);

            CheckDeleteRejected("nested user map rejected", @"C:\Users\Test\Maps\Sub\Nested.map", user, existing);
            CheckDeleteRejected("similar prefix rejected", @"C:\Users\Test\MapsBackup\Prefix.map", user, existing);
            CheckDeleteRejected("Vanilla map rejected", @"C:\Game\StreamingAssets\Maps\Vanilla.map", user, existing);
            CheckDeleteRejected("wrong extension rejected", @"C:\Users\Test\Maps\Wrong.sav", user, existing);
            CheckDeleteRejected("missing map rejected", @"C:\Users\Test\Maps\Missing.map", user, existing);
        }

        private static void CheckDeleteRejected(
            string name,
            string path,
            string userDirectory,
            HashSet<string> existing)
        {
            Check(name, false,
                VanillaMapEditorPolicy.TryResolveDeletableUserMapPath(
                    path, userDirectory, existing.Contains, out _));
        }

        private static List<Header> Merge(
            IEnumerable<Header> originals,
            IEnumerable<IEnumerable<Header>> groups,
            int sortMode,
            bool ascending) =>
            VanillaMapEditorPolicy.MergeEditableBuiltIns(
                originals,
                groups,
                header => header.BuiltIn,
                header => header.Editable,
                header => header.Path,
                list => Sort(list, sortMode, ascending));

        private static IEnumerable<IEnumerable<Header>> EmptyGroups() =>
            new IEnumerable<Header>[0];

        private static List<Header> Sort(List<Header> headers, int mode, bool ascending)
        {
            Comparison<Header> comparison;
            switch (mode)
            {
                case 1:
                    comparison = (left, right) => left.Written.CompareTo(right.Written);
                    break;
                case 3:
                    comparison = (left, right) => left.Size.CompareTo(right.Size);
                    break;
                case 4:
                    comparison = (left, right) => left.Type.CompareTo(right.Type);
                    break;
                default:
                    comparison = (left, right) => left.Name.CompareTo(right.Name);
                    break;
            }
            headers.Sort((left, right) => ascending
                ? comparison(left, right)
                : comparison(right, left));
            return headers;
        }

        private static Header HeaderAt(
            string name,
            string path,
            bool builtIn,
            bool editable,
            int players = 0,
            int size = 0,
            string type = "") =>
            new Header
            {
                Name = name,
                Path = path,
                BuiltIn = builtIn,
                Editable = editable,
                Players = players,
                Size = size,
                Type = type,
            };

        private static void Check<T>(string name, T expected, T actual)
        {
            if (Equals(expected, actual))
                return;

            failures++;
            Console.Error.WriteLine($"FAIL {name}: expected={expected}, actual={actual}");
        }

        private sealed class Header
        {
            internal string Name { get; set; }
            internal string Path { get; set; }
            internal bool BuiltIn { get; set; }
            internal bool Editable { get; set; }
            internal DateTime Written { get; set; }
            internal int Players { get; set; }
            internal int Size { get; set; }
            internal string Type { get; set; }
        }
    }
}
