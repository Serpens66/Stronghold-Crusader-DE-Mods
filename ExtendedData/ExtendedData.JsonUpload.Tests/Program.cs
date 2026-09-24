using System;
using System.IO;

namespace ExtendedData
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Run("exact upload classification", TestClassification);
            Run("recursive JSON staging", TestRootJsonStaging);
            Run("no JSON succeeds", TestNoJson);
            Run("idempotent retry", TestIdempotentRetry);
            Run("conflict rolls back", TestConflictRollback);
            Run("invalid child is rejected", TestInvalidChild);
            Console.WriteLine(failures == 0
                ? "All CustomLordJsonUpload tests passed."
                : failures + " CustomLordJsonUpload test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void TestClassification()
        {
            Assert(CustomLordJsonUploadPolicy.Classify(new[] { "Custom Lord" }) ==
                   CustomLordJsonUploadMode.CustomLord, "Custom Lord was not recognized");
            Assert(CustomLordJsonUploadPolicy.Classify(new[] { "Extended CPU Lord", "Rat" }) ==
                   CustomLordJsonUploadMode.ExtendedCpuLord, "Extended CPU Lord was not recognized");
            Assert(CustomLordJsonUploadPolicy.Classify(new[] { "Extended AIV Castle", "Rat" }) ==
                   CustomLordJsonUploadMode.None, "Extended AIV Castle was incorrectly recognized");
            Assert(CustomLordJsonUploadPolicy.Classify(new[] { "custom lord" }) ==
                   CustomLordJsonUploadMode.None, "case-variant tag was incorrectly recognized");
            Assert(CustomLordJsonUploadPolicy.Classify(new[] { "Custom Lord", "Extended CPU Lord" }) ==
                   CustomLordJsonUploadMode.None, "ambiguous upload tags were incorrectly recognized");
        }

        private static void TestRootJsonStaging()
        {
            WithDirectories((source, root, destination) =>
            {
                File.WriteAllText(Path.Combine(source, "info.json"), "info");
                File.WriteAllText(Path.Combine(source, "LORDMETA.JSON"), "meta");
                File.WriteAllText(Path.Combine(source, "lord.lordjson"), "vanilla lord");
                File.WriteAllText(Path.Combine(source, "castle.aivjson"), "vanilla castle");
                File.WriteAllText(Path.Combine(source, "notes.txt"), "notes");
                string nested = Directory.CreateDirectory(Path.Combine(source, "nested")).FullName;
                File.WriteAllText(Path.Combine(nested, "nested.json"), "nested");

                bool result = CustomLordJsonUploadPolicy.TryStageDirectJsonFiles(
                    source, root, "Lord", out int copied, out int existing, out string error);

                Assert(result, error);
                Assert(copied == 3 && existing == 0, "unexpected JSON staging counts");
                Assert(File.Exists(Path.Combine(destination, "info.json")), "info.json was not copied");
                Assert(File.Exists(Path.Combine(destination, "LORDMETA.JSON")), "case-variant .json was not copied");
                Assert(!File.Exists(Path.Combine(destination, "lord.lordjson")), ".lordjson was copied");
                Assert(!File.Exists(Path.Combine(destination, "castle.aivjson")), ".aivjson was copied");
                Assert(File.Exists(Path.Combine(destination, "nested", "nested.json")), "nested JSON was not copied");
            });
        }

        private static void TestNoJson()
        {
            WithDirectories((source, root, destination) =>
            {
                File.WriteAllText(Path.Combine(source, "lord.lordjson"), "vanilla");
                bool result = CustomLordJsonUploadPolicy.TryStageDirectJsonFiles(
                    source, root, "Lord", out int copied, out int existing, out string error);
                Assert(result, error);
                Assert(copied == 0 && existing == 0, "empty JSON set reported files");
            });
        }

        private static void TestIdempotentRetry()
        {
            WithDirectories((source, root, destination) =>
            {
                File.WriteAllText(Path.Combine(source, "info.json"), "same");
                string nested = Directory.CreateDirectory(Path.Combine(source, "Override", "Fixes")).FullName;
                File.WriteAllText(Path.Combine(nested, "preferences.json"), "same nested");
                Assert(CustomLordJsonUploadPolicy.TryStageDirectJsonFiles(
                    source, root, "Lord", out int firstCopied, out _, out string firstError), firstError);
                Assert(CustomLordJsonUploadPolicy.TryStageDirectJsonFiles(
                    source, root, "Lord", out int retryCopied, out int retryExisting, out string retryError), retryError);
                Assert(firstCopied == 2 && retryCopied == 0 && retryExisting == 2, "retry was not idempotent");
            });
        }

        private static void TestConflictRollback()
        {
            WithDirectories((source, root, destination) =>
            {
                string nested = Directory.CreateDirectory(Path.Combine(source, "nested")).FullName;
                string staged = Directory.CreateDirectory(Path.Combine(destination, "nested")).FullName;
                File.WriteAllText(Path.Combine(nested, "a.json"), "new");
                File.WriteAllText(Path.Combine(nested, "z.json"), "source");
                File.WriteAllText(Path.Combine(staged, "z.json"), "destination");
                bool result = CustomLordJsonUploadPolicy.TryStageDirectJsonFiles(
                    source, root, "Lord", out _, out _, out string error);
                Assert(!result, "different existing JSON unexpectedly succeeded");
                Assert(error.IndexOf("different JSON destination", StringComparison.OrdinalIgnoreCase) >= 0,
                    "unexpected conflict error");
                Assert(!File.Exists(Path.Combine(staged, "a.json")), "copied JSON was not rolled back");
                Assert(File.ReadAllText(Path.Combine(staged, "z.json")) == "destination",
                    "existing JSON was changed");
            });
        }

        private static void TestInvalidChild()
        {
            WithDirectories((source, root, destination) =>
            {
                bool result = CustomLordJsonUploadPolicy.TryStageDirectJsonFiles(
                    source, root, "..", out _, out _, out _);
                Assert(!result, "parent path was accepted");
            });
        }

        private static void WithDirectories(Action<string, string, string> test)
        {
            string testRoot = Path.Combine(
                Path.GetTempPath(), "CustomLordJsonUploadTests", Guid.NewGuid().ToString("N"));
            string source = Directory.CreateDirectory(Path.Combine(testRoot, "source")).FullName;
            string root = Directory.CreateDirectory(Path.Combine(testRoot, "staging")).FullName;
            string destination = Directory.CreateDirectory(Path.Combine(root, "Lord")).FullName;
            try
            {
                test(source, root, destination);
            }
            finally
            {
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, recursive: true);
            }
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
    }
}
