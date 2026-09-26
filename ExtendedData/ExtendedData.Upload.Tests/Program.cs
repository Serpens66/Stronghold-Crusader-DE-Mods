using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Logging;

namespace ExtendedData
{
    internal static class Program
    {
        private static int failures;

        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--expect-unsafe-package")
                return ExpectUnsafePackage(args[1]);

            Run("complete package and retry", TestCompletePackageAndRetry);
            Run("upload allowlist and excluded gameplay gate", TestUploadAllowlistAndExcludedWarning);
            Run("local and Workshop Lord source selection", TestLordSourceSelection);
            Run("conflict rollback", TestConflictRollback);
            Run("dynamic rules", TestDynamicRules);
            Run("unknown extender identity upload", TestUnknownExtenderIdentityUpload);
            Run("version-specific info.json warning", TestVersionSpecificInfoWarning);
            Run("exact tag workflow", TestExactTagWorkflow);
            Run("yes/no workflow", TestConfirmationWorkflow);
            Run("completion callback tracking", TestCompletionCallbackTracking);
            Run("staging failure aborts upload", TestStagingFailureAbortsUpload);
            Run("explicit Vanilla upload bypasses extras", TestVanillaOnlyWorkflow);
            Run("staging reset removes stale extras", TestStagingReset);
            Run("valid metadata and version", TestValidPreflight);
            Run("message IDs", TestMessageIds);
            Run("invalid WAV", TestInvalidWave);
            Run("separate WAV defects", TestSeparateWaveDefects);
            Run("misplaced paths", TestMisplacedPaths);
            Run("generic Override asset names", TestGenericOverrideAssetNames);

            Console.WriteLine(failures == 0
                ? "All CustomLordUpload tests passed."
                : failures + " CustomLordUpload test(s) failed.");
            return failures == 0 ? 0 : 1;
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
                WriteText(Path.Combine(source, "Locales", "en-US", "crusader.txt"), "text");
                WriteText(Path.Combine(source, "Scripts", "init.lua"), "return true");

                WriteText(Path.Combine(staging, "lord.lordjson"), "vanilla lord");
                WriteText(Path.Combine(staging, "castle.aivjson"), "vanilla castle");
                WriteText(Path.Combine(staging, "avatar.png"), "vanilla avatar");

                bool first = CustomLordWorkshopPackagePolicy.TryStageFiles(
                    source, staging, out int copied, out int existing,
                    out int packageFiles, out long packageBytes, out string error);
                Assert(first, error);
                Assert(copied == 7, "unexpected copied file count: " + copied);
                Assert(existing == 0, "unexpected initial existing count: " + existing);
                Assert(packageFiles == 7, "unexpected package file count: " + packageFiles);
                Assert(packageBytes > 0, "package byte count was not reported");
                Assert(File.ReadAllText(Path.Combine(staging, "avatar.png")) == "vanilla avatar", "avatar overwritten");
                Assert(!File.Exists(Path.Combine(staging, "local.data")), ".data copied");
                Assert(!File.Exists(Path.Combine(staging, "local.ldata")), ".ldata copied");
                Assert(File.Exists(Path.Combine(staging, "Override", "Locales", "de-DE", "fx", "Speech", "line.ogg")), "localized speech missing");

                bool retry = CustomLordWorkshopPackagePolicy.TryStageFiles(
                    source, staging, out int retryCopied, out int retryExisting,
                    out int retryPackageFiles, out long retryPackageBytes, out string retryError);
                Assert(retry, retryError);
                Assert(retryCopied == 0, "retry copied files");
                Assert(retryExisting == copied, "retry did not recognize all existing extras");
                Assert(retryPackageFiles == packageFiles, "retry package file count changed");
                Assert(retryPackageBytes == packageBytes, "retry package byte count changed");
            });
        }

        private static void TestLordSourceSelection()
        {
            string local = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "LocalLord"));
            string workshop = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WorkshopLord"));
            string[] candidates = { local, workshop };
            Assert(TrailLordSourceSelector.SelectIndex(candidates, workshop, "Lord") == 1,
                "the selected Workshop copy was not retained");
            Assert(TrailLordSourceSelector.SelectIndex(candidates, local + Path.DirectorySeparatorChar,
                "Lord") == 0, "the selected local copy was not retained");
            Assert(TrailLordSourceSelector.SelectIndex(new[] { workshop }, null, "Lord") == 0,
                "a unique source was not used when the saved path was absent");
            bool ambiguousRejected = false;
            try { TrailLordSourceSelector.SelectIndex(candidates, null, "Lord"); }
            catch (InvalidDataException) { ambiguousRejected = true; }
            Assert(ambiguousRejected, "ambiguous copies were selected without a saved path");
            bool wrongPathRejected = false;
            try
            {
                TrailLordSourceSelector.SelectIndex(candidates,
                    Path.Combine(Path.GetTempPath(), "MissingLord"), "Lord");
            }
            catch (InvalidDataException) { wrongPathRejected = true; }
            Assert(wrongPathRejected, "a saved source path was ignored when it did not match");
        }

        private static void TestConflictRollback()
        {
            WithRoots((source, staging) =>
            {
                WriteText(Path.Combine(source, "Scripts", "a-created.lua"), "created");
                WriteText(Path.Combine(source, "Scripts", "z-conflict.lua"), "source");
                WriteText(Path.Combine(staging, "Scripts", "z-conflict.lua"), "destination");

                bool result = CustomLordWorkshopPackagePolicy.TryStageFiles(
                    source, staging, out _, out _, out _, out _, out string error);
                Assert(!result, "conflict unexpectedly succeeded");
                Assert(error.IndexOf("different package destination", StringComparison.OrdinalIgnoreCase) >= 0, "wrong conflict error");
                Assert(!File.Exists(Path.Combine(staging, "Scripts", "a-created.lua")), "rollback left copied file");
                Assert(File.ReadAllText(Path.Combine(staging, "Scripts", "z-conflict.lua")) == "destination", "conflict target changed");
            });
        }

        private static void TestUploadAllowlistAndExcludedWarning()
        {
            WithRoots((source, staging) =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "avatar.png"), "avatar");
                WriteText(Path.Combine(source, "local.data"), "control");
                WriteText(Path.Combine(source, "local.ldata"), "control");

                string[] allowed =
                {
                    "info.json",
                    "lordmeta.json",
                    "custom-settings.JSON",
                    "Resources/config.json",
                    "Override/Atlas/atlas.json",
                    "Override/Fixes/preferences.json",
                    "init.lua",
                    "helper.LUA",
                    "Scripts/init.lua",
                    "Scripts/lib/utils.lua",
                    "MapAreas/nested/keep.SEMA",
                    "Locales/en-US/crusader.txt",
                    "oVeRrIdE/Assets/GUI/Sprites/lord-face.PNG",
                    "Override/Assets/GUI/Sprites/lord-banner.jpg",
                    "Override/Assets/GUI/Sprites/lord-portrait.tga",
                    "Override/fx/speech/lord-voice.OGG",
                    "Override/fx/speech/lord-line.wav",
                    "Override/Assets/GUI/Video/lord-video.WEBM",
                    "Override/Assets/GUI/Video/lord-alternate.mp4",
                    "Override/Locales/de-DE/fx/speech/lord-voice.ogg"
                };

                foreach (string relativePath in allowed)
                {
                    string path = Path.Combine(source, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    if (relativePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                        WriteValidWave(path);
                    else if (string.Equals(relativePath, "info.json", StringComparison.OrdinalIgnoreCase))
                        WriteText(path, ValidInfo);
                    else if (string.Equals(relativePath, "lordmeta.json", StringComparison.OrdinalIgnoreCase))
                        WriteText(path, ValidLordMeta);
                    else
                        WriteText(path, "allowed");
                }

                string[] excluded =
                {
                    "README.md",
                    "Override/photo.jpeg",
                    "Locales/en-US/notes.txt",
                    "screenshot.png",
                    "Scripts/readme.txt",
                    "MapAreas/readme.txt"
                };
                string[] blockedGameplay =
                {
                    "secret.env",
                    "tool.exe",
                    "archive.zip",
                    "_LegacyMediaSource/zMediafiles.aivjson",
                    "Override/Assets/GUI/XAML/panel.xaml",
                    "Override/AssetBundles/lordbundle",
                    "Patches/ui.xaml"
                };
                foreach (string relativePath in excluded)
                    WriteText(Path.Combine(source, relativePath.Replace('/', Path.DirectorySeparatorChar)), "excluded");

                bool staged = CustomLordWorkshopPackagePolicy.TryStageFiles(
                    source, staging, out int copied, out int existing,
                    out int packageFiles, out long packageBytes, out string error);
                Assert(staged, error);
                Assert(copied == allowed.Length, "unexpected allowed copied count: " + copied);
                Assert(existing == 0, "unexpected existing allowed count: " + existing);
                Assert(packageFiles == allowed.Length, "unexpected allowed package count: " + packageFiles);
                Assert(packageBytes > 0, "allowed package byte count was not reported");

                foreach (string relativePath in allowed)
                {
                    string stagedPath = Path.Combine(staging, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    Assert(File.Exists(stagedPath), "allowed file was not staged: " + relativePath);
                }

                bool repeated = CustomLordWorkshopPackagePolicy.TryStageFiles(
                    source, staging, out int retryCopied, out int retryExisting,
                    out int retryPackageFiles, out long retryPackageBytes, out string retryError);
                Assert(repeated, retryError);
                Assert(retryCopied == 0, "allowlisted retry unexpectedly copied files");
                Assert(retryExisting == allowed.Length, "allowlisted retry did not recognize every existing file");
                Assert(retryPackageFiles == allowed.Length, "allowlisted retry reported the wrong package count");
                Assert(retryPackageBytes == packageBytes, "allowlisted retry reported a different byte count");

                foreach (string relativePath in blockedGameplay)
                    WriteText(Path.Combine(source, relativePath.Replace('/', Path.DirectorySeparatorChar)), "gameplay");
                bool blocked = CustomLordWorkshopPackagePolicy.TryStageFiles(
                    source, staging, out int blockedCopied, out _, out _, out _, out string blockedError);
                Assert(!blocked && blockedCopied == 0, "upload omitted gameplay files without blocking");
                Assert(blockedError.IndexOf("archive.zip",
                    StringComparison.OrdinalIgnoreCase) >= 0,
                    "upload gate did not name an excluded gameplay file: " + blockedError);

                foreach (string relativePath in excluded)
                {
                    string stagedPath = Path.Combine(staging, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    Assert(!File.Exists(stagedPath), "excluded file was staged: " + relativePath);
                }
                Assert(!File.Exists(Path.Combine(staging, "local.data")), "control .data was staged");
                Assert(!File.Exists(Path.Combine(staging, "local.ldata")), "control .ldata was staged");

                IReadOnlyList<CustomLordUploadIssue> issues = Inspect(source);
                CustomLordUploadIssue[] excludedIssues = issues
                    .Where(issue => issue.Code == "ExcludedPackageFiles")
                    .ToArray();
                Assert(excludedIssues.Length == 1, "excluded files were not grouped into one warning");
                Assert(excludedIssues[0].Replacements.Length >= 2 &&
                       Convert.ToInt32(excludedIssues[0].Replacements[1]) == excluded.Length + blockedGameplay.Length,
                    "excluded warning reported the wrong count");
                foreach (string relativePath in excluded.Concat(blockedGameplay))
                {
                    Assert(excludedIssues[0].TechnicalDetail.IndexOf(relativePath, StringComparison.OrdinalIgnoreCase) >= 0,
                        "excluded warning omitted: " + relativePath);
                }
                Assert(excludedIssues[0].TechnicalDetail.IndexOf("local.data", StringComparison.OrdinalIgnoreCase) < 0,
                    "local control file was reported as excluded");
                Assert(excludedIssues[0].TechnicalDetail.IndexOf("avatar.png", StringComparison.OrdinalIgnoreCase) < 0,
                    "Vanilla-owned file was reported as excluded");
            });
        }

        private static void TestValidPreflight()
        {
            WithSource(source =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "info.json"), ValidInfo);
                WriteText(Path.Combine(source, "lordmeta.json"), ValidLordMeta);
                IReadOnlyList<CustomLordUploadIssue> issues = Inspect(source);
                Assert(issues.Count == 0, string.Join(" | ", issues));

                WriteText(Path.Combine(source, "info.json"),
                    "{\"GUID\":\"test\",\"Version\":\"v1.0.0-beta.2\"}");
                issues = Inspect(source);
                Assert(!issues.Any(issue => issue.Code == "InfoVersionInvalid"),
                    "accepted SemVer-style version warned");
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
                IReadOnlyList<CustomLordUploadIssue> issues = Inspect(source);
                Assert(!issues.Any(issue => issue.Code == "DuplicateMessageId"),
                    "corrected IDs reported as duplicate");

                WriteText(
                    Path.Combine(source, "lordmeta.json"),
                    "{\"Messages\":{\"DefeatedAgain\":[],\"16\":[]}}");
                issues = Inspect(source);
                Assert(issues.Any(issue => issue.Code == "DuplicateMessageId"),
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
                IReadOnlyList<CustomLordUploadIssue> issues = Inspect(source);
                Assert(issues.Any(issue => issue.Code == "WaveHeader"),
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
                IReadOnlyList<CustomLordUploadIssue> issues = Inspect(source);
                Assert(issues.Any(issue => issue.Code == "MisplacedMetadata"), "misplaced info not reported");
                Assert(issues.Any(issue => issue.Code == "RootFx"), "root/fx not reported");
            });
        }

        private static void TestSeparateWaveDefects()
        {
            WithSource(source =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "info.json"), ValidInfo);
                WriteText(Path.Combine(source, "lordmeta.json"), ValidLordMeta);
                string wavePath = Path.Combine(source, "Override", "fx", "Speech", "bad.wav");
                string? directory = Path.GetDirectoryName(wavePath);
                if (directory != null)
                    Directory.CreateDirectory(directory);
                byte[] wave = new byte[48];
                Encoding.ASCII.GetBytes("RIFF").CopyTo(wave, 0);
                BitConverter.GetBytes(40).CopyTo(wave, 4);
                Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(wave, 8);
                BitConverter.GetBytes(16).CopyTo(wave, 16);
                BitConverter.GetBytes((short)3).CopyTo(wave, 20);
                BitConverter.GetBytes((short)4).CopyTo(wave, 22);
                BitConverter.GetBytes(22050).CopyTo(wave, 24);
                BitConverter.GetBytes((short)8).CopyTo(wave, 34);
                Encoding.ASCII.GetBytes("data").CopyTo(wave, 36);
                BitConverter.GetBytes(4).CopyTo(wave, 40);
                File.WriteAllBytes(wavePath, wave);

                IReadOnlyList<CustomLordUploadIssue> issues = Inspect(source);
                Assert(issues.Any(issue => issue.Code == "WaveFormat"), "WAV format defect missing");
                Assert(issues.Any(issue => issue.Code == "WaveChannels"), "WAV channel defect missing");
                Assert(issues.Any(issue => issue.Code == "WaveSampleRate"), "WAV sample-rate defect missing");
                Assert(issues.Any(issue => issue.Code == "WaveBits"), "WAV bit-depth defect missing");
            });
        }

        private static void TestGenericOverrideAssetNames()
        {
            WithSource(source =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "info.json"), ValidInfo);
                WriteText(Path.Combine(source, "lordmeta.json"), ValidLordMeta);
                WriteText(Path.Combine(source, "Override", "fx", "speech", "ATTACK_01.ogg"), "audio");
                WriteValidWave(Path.Combine(source, "Override", "fx", "speech", "add_player.wav"));
                WriteText(Path.Combine(source, "Override", "Assets", "GUI", "Video", "neutral.webm"), "video");
                WriteText(Path.Combine(source, "Override", "Assets", "GUI", "Sprites", "icon.png"), "image");
                WriteText(Path.Combine(source, "Override", "Locales", "de-DE", "fx", "speech", "attack_01.ogg"), "audio-de");
                WriteText(Path.Combine(source, "Override", "fx", "speech", "test-lord-attack.ogg"), "unique");
                WriteText(Path.Combine(source, "Override", "fx", "speech", "attack.mp3"), "excluded-format");
                WriteText(Path.Combine(source, "Media", "attack.ogg"), "outside-override");

                IReadOnlyList<CustomLordUploadIssue> issues = Inspect(source);
                CustomLordUploadIssue[] genericIssues = issues
                    .Where(issue => issue.Code == "GenericOverrideAssetNames")
                    .ToArray();

                Assert(genericIssues.Length == 1, "generic assets were not grouped into one warning");
                Assert(genericIssues[0].Replacements.Length >= 2 &&
                       Convert.ToInt32(genericIssues[0].Replacements[1]) == 5,
                    "generic asset warning reported the wrong count");
                Assert(genericIssues[0].TechnicalDetail.IndexOf("ATTACK_01.ogg", StringComparison.Ordinal) >= 0,
                    "case-insensitive numbered generic name was not reported");
                Assert(genericIssues[0].TechnicalDetail.IndexOf("Locales/de-DE", StringComparison.Ordinal) >= 0,
                    "localized generic asset was not reported");
                Assert(genericIssues[0].TechnicalDetail.IndexOf("test-lord-attack", StringComparison.OrdinalIgnoreCase) < 0,
                    "lord-prefixed asset was incorrectly reported");
                Assert(genericIssues[0].TechnicalDetail.IndexOf("attack.mp3", StringComparison.OrdinalIgnoreCase) < 0,
                    "excluded Override format was incorrectly reported as an uploaded generic asset");
                Assert(genericIssues[0].TechnicalDetail.IndexOf("Media/attack", StringComparison.OrdinalIgnoreCase) < 0,
                    "asset outside Override was incorrectly reported");

                WriteText(Path.Combine(source, "info.json"),
                    "{\"GUID\":\"test-lord\",\"Version\":\"1.0.110\",\"AssetMode\":\"Local\"}");
                IReadOnlyList<CustomLordUploadIssue> localIssues = Inspect(source);
                Assert(!localIssues.Any(issue => issue.Code == "GenericOverrideAssetNames"),
                    "local assets were incorrectly reported as global collisions");
                Assert(localIssues.Any(issue => issue.Code == "ExcludedPackageFiles"),
                    "local asset mode suppressed an unrelated package warning");

                WriteText(Path.Combine(source, "info.json"),
                    "{\"GUID\":\"test-lord\",\"Version\":\"1.0.110\",\"AssetMode\":\"Global\"}");
                Assert(Inspect(source).Any(issue => issue.Code == "GenericOverrideAssetNames"),
                    "explicit global mode did not report generic assets");

                WriteText(Path.Combine(source, "info.json"),
                    "{\"GUID\":\"test-lord\",\"Version\":\"1.0.110\",\"AssetMode\":\"invalid\"}");
                Assert(Inspect(source).Any(issue => issue.Code == "GenericOverrideAssetNames"),
                    "invalid asset mode incorrectly suppressed generic asset warning");
            });
        }

        private static void TestDynamicRules()
        {
            CustomLordRuntimeRules rules = CustomLordRuntimeRules.Discover(
                typeof(TestExtender.LordInfo).Assembly,
                "1.0.0+a7775a6",
                typeof(TestExtender.LordInfo).FullName,
                typeof(TestExtender.AILordMessageType).FullName);
            Assert(rules.MessageTypes["AllyNotificationCongratulations"] == 17, "message ID 17 not reflected");
            Assert(rules.MessageTypes["FutureMessage"] == 34, "future message enum not reflected");
            Assert(rules.LordInfoFields.Contains("FutureMetadata"), "future LordInfo property not reflected");
        }

        private static void TestUnknownExtenderIdentityUpload()
        {
            WithSource(source =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "info.json"), ValidInfo);
                WriteText(Path.Combine(source, "lordmeta.json"), ValidLordMeta);
                CustomLordRuntimeRules rules = CustomLordRuntimeRules.CreateCompatibilityProfile("future-build");
                IReadOnlyList<CustomLordUploadIssue> issues = CustomLordWorkshopPreflight.Inspect(source, rules);
                Assert(issues.Count == 0, "unknown extender identity raised a package warning");

                FakeStager stager = new FakeStager(source);
                FakeConfirmation confirmation = new FakeConfirmation();
                CustomLordUploadWorkflow workflow = new CustomLordUploadWorkflow(
                    new ManualLogSource("ExtendedDataUploadTests"), stager, confirmation, rules);
                int originalCalls = 0;
                int successes = 0;
                workflow.Handle(
                    CreateRequest(new[] { "Custom Lord" }, () => successes++, () => { }),
                    request =>
                    {
                        originalCalls++;
                        request.SuccessAction();
                    });
                Assert(confirmation.ShowCount == 0, "unknown identity opened the warning dialog");
                Assert(stager.StageCount == 1, "unknown identity did not stage extended files");
                Assert(originalCalls == 1 && successes == 1, "unknown identity did not complete the upload flow");
            });
        }

        private static void TestVersionSpecificInfoWarning()
        {
            WithSource(source =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "info.json"), "{\"GUID\":\"test-lord\",\"Version\":\"invalid\"}");
                WriteText(Path.Combine(source, "lordmeta.json"), ValidLordMeta);

                IReadOnlyList<CustomLordUploadIssue> v142Issues = CustomLordWorkshopPreflight.Inspect(
                    source,
                    CustomLordRuntimeRules.CreateCompatibilityProfile("1.0.0+171d68e"));
                Assert(v142Issues.Any(issue => issue.Code == "InfoVersionRecommended"),
                    "v1.42 recommendation missing");
                Assert(!v142Issues.Any(issue => issue.Code == "InfoVersionInvalid"),
                    "v1.42 incorrectly claimed versioned duplicate resolution");

                IReadOnlyList<CustomLordUploadIssue> branchAIssues = CustomLordWorkshopPreflight.Inspect(
                    source,
                    CustomLordRuntimeRules.CreateCompatibilityProfile("1.0.0+a7775a6"));
                Assert(branchAIssues.Any(issue => issue.Code == "InfoVersionInvalid"),
                    "Branch-A version rule missing");
                Assert(!branchAIssues.Any(issue => issue.Code == "InfoVersionRecommended"),
                    "Branch A used the legacy recommendation");
            });
        }

        private static IReadOnlyList<CustomLordUploadIssue> Inspect(string source)
        {
            return CustomLordWorkshopPreflight.Inspect(
                source,
                CustomLordRuntimeRules.CreateCompatibilityProfile("1.0.0+a7775a6"));
        }

        private static void TestExactTagWorkflow()
        {
            WithSource(source =>
            {
                FakeStager stager = new FakeStager(source);
                FakeConfirmation confirmation = new FakeConfirmation();
                CustomLordUploadWorkflow workflow = CreateWorkflow(stager, confirmation);
                int originals = 0;
                workflow.Handle(
                    CreateRequest(new[] { "custom lord" }, () => { }, () => { }),
                    _ => originals++);
                Assert(originals == 1, "case-variant tag did not pass directly to Vanilla");
                Assert(confirmation.ShowCount == 0, "case-variant tag triggered preflight");

                workflow.Handle(
                    CreateRequest(new[] { "Custom Lord Extra" }, () => { }, () => { }),
                    _ => originals++);
                Assert(originals == 2, "partial tag did not pass directly to Vanilla");
            });
        }

        private static void TestConfirmationWorkflow()
        {
            WithSource(source =>
            {
                FakeStager stager = new FakeStager(source);
                FakeConfirmation confirmation = new FakeConfirmation();
                CustomLordUploadWorkflow workflow = CreateWorkflow(stager, confirmation);
                int originalCalls = 0;
                int failures = 0;
                workflow.Handle(
                    CreateRequest(new[] { "Custom Lord" }, () => { }, () => failures++),
                    _ => originalCalls++);
                Assert(confirmation.ShowCount == 1, "warnings did not open confirmation");
                Assert(originalCalls == 0, "Vanilla upload started before confirmation");
                confirmation.Cancel();
                Assert(failures == 1 && originalCalls == 0, "No did not cancel safely");

                confirmation = new FakeConfirmation();
                workflow = CreateWorkflow(stager, confirmation);
                workflow.Handle(
                    CreateRequest(new[] { "Custom Lord" }, () => { }, () => failures++),
                    request =>
                    {
                        originalCalls++;
                        request.SuccessAction();
                    });
                confirmation.Confirm();
                Assert(originalCalls == 1, "Yes did not continue to Vanilla");
                Assert(stager.StageCount == 1, "Yes did not add extended staging files");
            });
        }

        private static void TestCompletionCallbackTracking()
        {
            WithSource(source =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "info.json"), ValidInfo);
                WriteText(Path.Combine(source, "lordmeta.json"), ValidLordMeta);

                FakeStager stager = new FakeStager(source);
                FakeConfirmation confirmation = new FakeConfirmation();
                CustomLordUploadWorkflow workflow = CreateWorkflow(stager, confirmation);
                int successes = 0;
                int failures = 0;
                int originalCalls = 0;

                workflow.Handle(
                    CreateRequest(new[] { "Custom Lord" }, () => successes++, () => failures++),
                    request =>
                    {
                        originalCalls++;
                        request.SuccessAction();
                        request.SuccessAction();
                        request.FailAction();
                    });

                Assert(confirmation.ShowCount == 0, "valid package unexpectedly requested confirmation");
                Assert(originalCalls == 1, "valid upload was not handed to Vanilla exactly once");
                Assert(successes == 1, "success callback was not forwarded exactly once");
                Assert(failures == 0, "late failure callback was incorrectly forwarded");
                Assert(stager.StageCount == 1, "valid upload did not extend staging");
            });
        }

        private static void TestVanillaOnlyWorkflow()
        {
            WithSource(source =>
            {
                FakeStager stager = new FakeStager(source);
                FakeConfirmation confirmation = new FakeConfirmation();
                CustomLordUploadWorkflow workflow = CreateWorkflow(stager, confirmation);
                int originals = 0;
                workflow.Handle(
                    new CustomLordUploadRequest(
                        null!, "staging", "testlord", "", new[] { "Custom Lord" },
                        false, "", () => { }, () => { }, includeAdditionalFiles: false),
                    request =>
                    {
                        originals++;
                        request.SuccessAction();
                    });
                Assert(originals == 1, "Vanilla upload was not called");
                Assert(stager.StageCount == 0, "extended staging ran for a Vanilla-only upload");
                Assert(confirmation.ShowCount == 0, "preflight confirmation ran for a Vanilla-only upload");
            });
        }

        private static void TestStagingFailureAbortsUpload()
        {
            WithSource(source =>
            {
                WriteVanillaBase(source);
                WriteText(Path.Combine(source, "info.json"), ValidInfo);
                WriteText(Path.Combine(source, "lordmeta.json"), ValidLordMeta);
                FakeStager stager = new FakeStager(source) { FailStaging = true };
                CustomLordUploadWorkflow workflow = CreateWorkflow(stager, new FakeConfirmation());
                int originals = 0;
                int failures = 0;
                workflow.Handle(
                    CreateRequest(new[] { "Custom Lord" }, () => { }, () => failures++),
                    _ => originals++);
                Assert(originals == 0, "Vanilla upload started after extended staging failed");
                Assert(failures == 1, "staging failure did not reach the terminal failure callback");
            });
        }

        private static void TestStagingReset()
        {
            string root = NewTempRoot();
            try
            {
                string stagingRoot = Path.Combine(root, "upload-content");
                string destination = Path.Combine(stagingRoot, "Lord");
                WriteText(Path.Combine(destination, "Override", "stale.txt"), "stale");
                WriteText(Path.Combine(destination, "info.json"), "stale");

                bool result = Shared.WorkshopUploadStaging.TryResetDirectChild(
                    stagingRoot,
                    "Lord",
                    out string actualDestination,
                    out string error);
                Assert(result, error);
                Assert(string.Equals(destination, actualDestination, StringComparison.OrdinalIgnoreCase),
                    "unexpected staging destination");
                Assert(Directory.Exists(destination), "clean staging destination was not recreated");
                Assert(!Directory.EnumerateFileSystemEntries(destination).Any(), "stale files survived reset");

                Assert(!Shared.WorkshopUploadStaging.TryResetDirectChild(
                        stagingRoot,
                        "..\\escape",
                        out _,
                        out _),
                    "unsafe item name was accepted");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static CustomLordUploadWorkflow CreateWorkflow(
            ICustomLordUploadStager stager,
            ICustomLordUploadConfirmation confirmation)
        {
            return new CustomLordUploadWorkflow(
                new ManualLogSource("ExtendedDataUploadTests"),
                stager,
                confirmation,
                CustomLordRuntimeRules.CreateCompatibilityProfile("1.0.0+a7775a6"));
        }

        private static CustomLordUploadRequest CreateRequest(
            string[] tags,
            Action success,
            Action failure)
        {
            return new CustomLordUploadRequest(
                null!, "staging-" + Guid.NewGuid().ToString("N"), "testlord", "", tags,
                false, "", success, failure);
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
            string root = Path.Combine(Path.GetTempPath(), "ExtendedDataUploadTests", Guid.NewGuid().ToString("N"));
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

        private static void WriteValidWave(string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (directory != null)
                Directory.CreateDirectory(directory);

            byte[] wave = new byte[46];
            Encoding.ASCII.GetBytes("RIFF").CopyTo(wave, 0);
            BitConverter.GetBytes(38).CopyTo(wave, 4);
            Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(wave, 8);
            BitConverter.GetBytes(16).CopyTo(wave, 16);
            BitConverter.GetBytes((short)1).CopyTo(wave, 20);
            BitConverter.GetBytes((short)1).CopyTo(wave, 22);
            BitConverter.GetBytes(44100).CopyTo(wave, 24);
            BitConverter.GetBytes(88200).CopyTo(wave, 28);
            BitConverter.GetBytes((short)2).CopyTo(wave, 32);
            BitConverter.GetBytes((short)16).CopyTo(wave, 34);
            Encoding.ASCII.GetBytes("data").CopyTo(wave, 36);
            BitConverter.GetBytes(2).CopyTo(wave, 40);
            File.WriteAllBytes(path, wave);
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
                source, out _, out string error);
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

        private sealed class FakeStager : ICustomLordUploadStager
        {
            private readonly string source;

            internal FakeStager(string source) { this.source = source; }
            internal int StageCount { get; private set; }
            internal bool FailStaging { get; set; }

            public bool TryResolveSource(string mapTitle, out string sourcePath, out string error)
            {
                sourcePath = source;
                error = string.Empty;
                return true;
            }

            public bool TryExtendStaging(
                string uploadContentRoot,
                string mapTitle,
                out ExtendedDataLordUploadStagingSummary? summary,
                out string error)
            {
                StageCount++;
                if (FailStaging)
                {
                    summary = null;
                    error = "simulated staging failure";
                    return false;
                }
                summary = new ExtendedDataLordUploadStagingSummary(
                    source,
                    Path.Combine(uploadContentRoot, mapTitle),
                    1,
                    123,
                    1,
                    0);
                error = string.Empty;
                return true;
            }
        }

        private sealed class FakeConfirmation : ICustomLordUploadConfirmation
        {
            private Action? confirm;
            private Action? cancel;

            internal int ShowCount { get; private set; }

            public void Show(IReadOnlyList<CustomLordUploadIssue> issues, Action confirmAction, Action cancelAction)
            {
                ShowCount++;
                confirm = confirmAction;
                cancel = cancelAction;
            }

            internal void Confirm() => confirm?.Invoke();
            internal void Cancel() => cancel?.Invoke();
        }
    }
}
