using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CustomLordUpload
{
    internal static class Program
    {
        private static int failures;

        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--expect-unsafe-package")
                return ExpectUnsafePackage(args[1]);

            Run("exact tag", TestExactTag);
            Run("complete package and retry", TestCompletePackageAndRetry);
            Run("conflict rollback", TestConflictRollback);
            Run("valid metadata and version", TestValidPreflight);
            Run("message IDs", TestMessageIds);
            Run("invalid WAV", TestInvalidWave);
            Run("misplaced paths", TestMisplacedPaths);

            Console.WriteLine(failures == 0
                ? "All CustomLordUpload tests passed."
                : failures + " CustomLordUpload test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void TestExactTag()
        {
            Assert(CustomLordWorkshopPackagePolicy.IsCustomLordUpload(new[] { "Custom Lord" }), "exact tag rejected");
            Assert(!CustomLordWorkshopPackagePolicy.IsCustomLordUpload(new[] { "custom lord" }), "case variant accepted");
            Assert(!CustomLordWorkshopPackagePolicy.IsCustomLordUpload(new[] { "Map", "Custom Lord Extra" }), "partial tag accepted");
            Assert(!CustomLordWorkshopPackagePolicy.IsCustomLordUpload(null), "null tags accepted");
        }

        private static void TestCompletePackageAndRetry()
        {
            WithRoots((source, staging) =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "avatar.png"), "source avatar");
                WriteText(Path.Combine(source, "local.data"), "control");
                WriteText(Path.Combine(source, "local.ldata"), "control");
                WriteText(Path.Combine(source, "info.json"), ValidInfo);
                WriteText(Path.Combine(source, "lordmeta.json"), ValidLordMeta);
                WriteText(Path.Combine(source, "init.lua"), "return true");
                WriteText(Path.Combine(source, "Override", "fx", "Speech", "line.ogg"), "audio");
                WriteText(Path.Combine(source, "Override", "Locales", "de-DE", "fx", "Speech", "line.ogg"), "audio-de");
                WriteText(Path.Combine(source, "Locales", "en-US", "text.txt"), "text");
                WriteText(Path.Combine(source, "Scripts", "init.lua"), "return true");

                WriteText(Path.Combine(staging, "lord.lordjson"), "vanilla lord");
                WriteText(Path.Combine(staging, "castle.aivjson"), "vanilla castle");
                WriteText(Path.Combine(staging, "avatar.png"), "vanilla avatar");

                bool first = CustomLordWorkshopPackagePolicy.TryStageFiles(
                    source, staging, out int copied, out int existing, out string error);
                Assert(first, error);
                Assert(copied == 7, "unexpected copied file count: " + copied);
                Assert(existing == 0, "unexpected initial existing count: " + existing);
                Assert(File.ReadAllText(Path.Combine(staging, "avatar.png")) == "vanilla avatar", "avatar overwritten");
                Assert(!File.Exists(Path.Combine(staging, "local.data")), ".data copied");
                Assert(!File.Exists(Path.Combine(staging, "local.ldata")), ".ldata copied");
                Assert(File.Exists(Path.Combine(staging, "Override", "Locales", "de-DE", "fx", "Speech", "line.ogg")), "localized speech missing");

                bool retry = CustomLordWorkshopPackagePolicy.TryStageFiles(
                    source, staging, out int retryCopied, out int retryExisting, out string retryError);
                Assert(retry, retryError);
                Assert(retryCopied == 0, "retry copied files");
                Assert(retryExisting == copied, "retry did not recognize all existing extras");
            });
        }

        private static void TestConflictRollback()
        {
            WithRoots((source, staging) =>
            {
                WriteText(Path.Combine(source, "a-created.txt"), "created");
                WriteText(Path.Combine(source, "z-conflict.txt"), "source");
                WriteText(Path.Combine(staging, "z-conflict.txt"), "destination");

                bool result = CustomLordWorkshopPackagePolicy.TryStageFiles(
                    source, staging, out _, out _, out string error);
                Assert(!result, "conflict unexpectedly succeeded");
                Assert(error.IndexOf("different package destination", StringComparison.OrdinalIgnoreCase) >= 0, "wrong conflict error");
                Assert(!File.Exists(Path.Combine(staging, "a-created.txt")), "rollback left copied file");
                Assert(File.ReadAllText(Path.Combine(staging, "z-conflict.txt")) == "destination", "conflict target changed");
            });
        }

        private static void TestValidPreflight()
        {
            WithSource(source =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "info.json"), ValidInfo);
                WriteText(Path.Combine(source, "lordmeta.json"), ValidLordMeta);
                IReadOnlyList<string> issues = CustomLordWorkshopPreflight.Inspect(source);
                Assert(issues.Count == 0, string.Join(" | ", issues));

                WriteText(Path.Combine(source, "info.json"),
                    "{\"GUID\":\"test\",\"Version\":\"v1.0.0-beta.2\"}");
                issues = CustomLordWorkshopPreflight.Inspect(source);
                Assert(!issues.Any(issue => issue.IndexOf("Version", StringComparison.OrdinalIgnoreCase) >= 0),
                    "accepted SemVer-style version warned: " + string.Join(" | ", issues));
            });
        }

        private static void TestMessageIds()
        {
            WithSource(source =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "info.json"), ValidInfo);
                WriteText(
                    Path.Combine(source, "lordmeta.json"),
                    "{\"Messages\":{\"DefeatedAgain\":[],\"AllyNotificationCongratulations\":[]}}");
                IReadOnlyList<string> issues = CustomLordWorkshopPreflight.Inspect(source);
                Assert(!issues.Any(issue => issue.IndexOf("both use native ID", StringComparison.OrdinalIgnoreCase) >= 0),
                    "corrected IDs reported as duplicate");

                WriteText(
                    Path.Combine(source, "lordmeta.json"),
                    "{\"Messages\":{\"DefeatedAgain\":[],\"16\":[]}}");
                issues = CustomLordWorkshopPreflight.Inspect(source);
                Assert(issues.Any(issue => issue.IndexOf("both use native ID 16", StringComparison.OrdinalIgnoreCase) >= 0),
                    "real duplicate ID not reported");
            });
        }

        private static void TestInvalidWave()
        {
            WithSource(source =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "info.json"), ValidInfo);
                WriteText(Path.Combine(source, "lordmeta.json"), ValidLordMeta);
                WriteText(Path.Combine(source, "Override", "fx", "Speech", "bad.wav"), "not-wave");
                IReadOnlyList<string> issues = CustomLordWorkshopPreflight.Inspect(source);
                Assert(issues.Any(issue => issue.IndexOf("WAV", StringComparison.OrdinalIgnoreCase) >= 0),
                    "invalid WAV not reported");
            });
        }

        private static void TestMisplacedPaths()
        {
            WithSource(source =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "nested", "info.json"), ValidInfo);
                WriteText(Path.Combine(source, "fx", "speech.ogg"), "audio");
                IReadOnlyList<string> issues = CustomLordWorkshopPreflight.Inspect(source);
                Assert(issues.Any(issue => issue.IndexOf("misplaced", StringComparison.OrdinalIgnoreCase) >= 0), "misplaced info not reported");
                Assert(issues.Any(issue => issue.IndexOf("root/fx", StringComparison.OrdinalIgnoreCase) >= 0), "root/fx not reported");
            });
        }

        private static void WriteVanillaBase(string source)
        {
            WriteText(Path.Combine(source, "lord.lordjson"), "{}");
            WriteText(Path.Combine(source, "castle.aivjson"), "{}");
        }

        private static void WithSource(Action<string> test)
        {
            string root = NewTempRoot();
            string source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            try { test(source); }
            finally { Directory.Delete(root, true); }
        }

        private static void WithRoots(Action<string, string> test)
        {
            string root = NewTempRoot();
            string source = Path.Combine(root, "source");
            string staging = Path.Combine(root, "staging");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(staging);
            try { test(source, staging); }
            finally { Directory.Delete(root, true); }
        }

        private static string NewTempRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "CustomLordUploadTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void WriteText(string path, string content)
        {
            string? directory = Path.GetDirectoryName(path);
            if (directory != null)
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS: " + name);
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine("FAIL: " + name + " - " + exception.Message);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static int ExpectUnsafePackage(string source)
        {
            bool result = CustomLordWorkshopPackagePolicy.TryCollectFilesForInspection(
                source, out _, out _, out string error);
            if (!result && error.IndexOf("reparse point", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("PASS: reparse point rejected");
                return 0;
            }

            Console.WriteLine("FAIL: reparse point was not rejected: " + error);
            return 1;
        }

        private const string ValidInfo = "{\"GUID\":\"test-lord\",\"Version\":\"1.0.110\"}";
        private const string ValidLordMeta = "{\"Messages\":{\"WillAttack\":[]}}";
    }
}
