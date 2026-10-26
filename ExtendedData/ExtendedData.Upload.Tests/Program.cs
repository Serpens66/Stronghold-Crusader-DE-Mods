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

            Run("Coop launch blocks remain visible without diagnostics", TestCoopLaunchBlockLogs);
            Run("Coop block chat chunks and transition deduplication", TestCoopBlockChatState);
            Run("Coop block logs retain reasons beyond the shared detail limit", TestLongCoopBlockLog);
            Run("Coop package messages classify players and keep technical errors out of chat", TestCoopPackageMessages);
            Run("complete package and retry", TestCompletePackageAndRetry);
            Run("upload allowlist and excluded gameplay gate", TestUploadAllowlistAndExcludedWarning);
            Run("local and Workshop Lord source selection", TestLordSourceSelection);
            Run("unnamed Extended Lord source identity", TestExtendedLordSourceIdentity);
            Run("decoded Trail Lord accepts only equivalent package replicas", TestDecodedLordReplicas);
            Run("conflict rollback", TestConflictRollback);
            Run("dynamic rules", TestDynamicRules);
            Run("unknown extender identity upload", TestUnknownExtenderIdentityUpload);
            Run("version-specific info.json warning", TestVersionSpecificInfoWarning);
            Run("exact tag workflow", TestExactTagWorkflow);
            Run("yes/no workflow", TestConfirmationWorkflow);
            Run("completion callback tracking", TestCompletionCallbackTracking);
            Run("staging failure aborts upload", TestStagingFailureAbortsUpload);
            Run("explicit Vanilla upload bypasses extras", TestVanillaOnlyWorkflow);
            Run("activation and delayed upload decisions", TestActivationAndDelayedDecisions);
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

        private static void TestCoopBlockChatState()
        {
            string reason = string.Concat(Enumerable.Repeat("\U0001F40D", 300)) + " complete final reason";
            string[] messages = CoopLaunchBlockDiagnostics.ChatMessages("Ready", new[] { reason, "other failure" });
            Assert(messages.Length > 1 && messages.All(message => message.Length <= 280), "chat text was truncated or exceeds Vanilla limits");
            string reconstructed = string.Concat(messages.Select(message =>
                System.Text.RegularExpressions.Regex.Replace(message.Substring(CoopLaunchBlockDiagnostics.ChatPrefix.Length), @"^\[\d+/\d+\] ", "")));
            Assert(reconstructed == "Ready: " + reason + "; other failure", "chat chunks lost or reordered a reason");
            Assert(messages.All(message => !char.IsHighSurrogate(message[message.Length - 1])), "chunk split a surrogate pair");
            var state = new CoopLaunchAnnouncementState();
            DateTime now = DateTime.UtcNow;
            Assert(state.ShouldSend("lobby1", "Lobby", new[] { "a", "b" }, true, now), "first transition hidden");
            Assert(!state.ShouldSend("lobby1", "Lobby", new[] { "b", "a" }, true, now.AddMinutes(1)), "unchanged set was repeated");
            Assert(state.ShouldSend("lobby1", "Ready", new[] { "c" }, false, now), "different click reason hidden");
            Assert(!state.ShouldSend("lobby1", "Ready", new[] { "c" }, false, now.AddSeconds(1)), "click throttle missing");
            Assert(state.ShouldSend("lobby1", "Ready", new[] { "c" }, false, now.AddSeconds(3)), "explicit retry hidden indefinitely");
            Assert(!state.ShouldSend("lobby1", "Lobby", Array.Empty<string>(), true, now.AddSeconds(3)), "cleared state announced as a block");
            Assert(state.ShouldSend("lobby1", "Lobby", new[] { "a", "b" }, true, now.AddSeconds(3)), "reappearing reason hidden");
            Assert(state.ShouldSend("lobby2", "Lobby", new[] { "a", "b" }, true, now.AddSeconds(3)), "new lobby inherited suppression");
        }

        private static void TestCoopLaunchBlockLogs()
        {
            Assert(!Shared.DebugLogHelper.DiagnosticsEnabled, "fixture requires default-disabled diagnostics");
            using (var log = new ManualLogSource("coop-block-fixture"))
            {
                var messages = new List<LogEventArgs>();
                log.LogEvent += (_, args) => messages.Add(args);
                string context = "role=host, lobby=12, package=test, fingerprint=ABC, mission=Trail1/2, localStatus=OK|test|ABC";
                string[] reasons = { "Restart the game", "Package differs", "Required Lord files missing", "Preparation failed" };
                foreach (string reason in reasons)
                    CoopLaunchBlockDiagnostics.ReportLocal(log, "Ready", reason, context);
                Assert(messages.Count == reasons.Length && messages.All(item => item.Level == LogLevel.Error),
                    "local block logs were hidden or had the wrong severity");
                for (int index = 0; index < reasons.Length; index++)
                    Assert(messages[index].Data.ToString().Contains(reasons[index]) &&
                        messages[index].Data.ToString().Contains(context), "block reason or context lost");
                CoopLaunchBlockDiagnostics.ReportLocal(log, "Ready", reasons[0], context);
                Assert(messages.Count == reasons.Length, "identical blocks were not throttled");
                string longReason = new string('x', 350) + " FULL_REASON_END";
                CoopLaunchBlockDiagnostics.ReportLocal(log, "COOP_START", longReason, context,
                    "Player Two[playerId=2, status=mismatch, reported=OK|other|DEF]; sync missing");
                string local = messages.Last().Data.ToString();
                Assert(local.Contains(longReason) && local.Contains("Player Two") && local.Contains("sync missing"),
                    "local log was shortened to chat length or lost participant details");
                CoopLaunchBlockDiagnostics.ReportRemote(log, 34, "12", "ordinary chat");
                CoopLaunchBlockDiagnostics.ReportRemote(log, 34, "12", null);
                Assert(messages.Count == reasons.Length + 1, "unrelated chat was logged");
                string chat = CoopLaunchBlockDiagnostics.ChatPrefix + "Package differs";
                CoopLaunchBlockDiagnostics.ReportRemote(log, 34, "12", chat);
                Assert(messages.Last().Level == LogLevel.Warning &&
                    messages.Last().Data.ToString().Contains(chat) &&
                    messages.Last().Data.ToString().Contains("sender=34"), "remote reason or sender missing");
                CoopLaunchBlockDiagnostics.ReportRemote(log, 34, "12", chat);
                Assert(messages.Count == reasons.Length + 2, "identical remote blocks were not throttled");
                CoopLaunchBlockDiagnostics.ReportRemote(log, 35, "12", chat);
                Assert(messages.Count == reasons.Length + 3, "independent sender was suppressed");
                Assert(messages.All(item => System.Text.RegularExpressions.Regex.IsMatch(item.Data.ToString(),
                    @"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]")), "millisecond timestamp missing");
            }
        }

        private static void TestLongCoopBlockLog()
        {
            using (var log = new ManualLogSource("long-coop-block-fixture"))
            {
                var messages = new List<string>();
                log.LogEvent += (_, args) => messages.Add(args.Data.ToString());
                string reason = new string('x', 8000) + " complete final reason";
                CoopLaunchBlockDiagnostics.ReportLocal(log, "Ready", reason, "package and mission context");
                Assert(messages.Count > 1 && messages.Last().Contains("complete final reason") &&
                    messages.Last().Contains("package and mission context"), "shared ledger truncated a long block report");
            }
        }

        private static void TestCoopPackageMessages()
        {
            var cultures = new[] { "de-DE", "en-US" };
            foreach (string culture in cultures)
            {
                var catalog = File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Locales", culture + ".txt"))
                    .Where(line => line.StartsWith("ExtendedData.", StringComparison.Ordinal) && line.Contains("="))
                    .ToDictionary(line => line.Substring(0, line.IndexOf('=')), line => line.Substring(line.IndexOf('=') + 1));
                SerpLocalization.Overrides = catalog;
                try
                {
                    const string expected = "OK|package|host-fingerprint|1/2/mission-digest/lord-selection";
                    const string technical = "contentFingerprint does not match; C:/private-path/package";
                    var cases = new[] {
                        new KeyValuePair<string, string>(CoopPackageStatusCodes.Missing, "ErrorParticipantsMissing"),
                        new KeyValuePair<string, string>(CoopPackageStatusCodes.Mismatch, "ErrorParticipantsMismatch"),
                        new KeyValuePair<string, string>("OK|package|old-fingerprint", "ErrorParticipantsMismatch"),
                        new KeyValuePair<string, string>("OK|other-package|host-fingerprint", "ErrorParticipantsMismatch"),
                        new KeyValuePair<string, string>("OK|package|host-fingerprint", "ErrorParticipantsSelectionNotReady"),
                        new KeyValuePair<string, string>("OK|package|host-fingerprint|1/1/mission-digest/lord-selection", "ErrorParticipantsSelectionNotReady"),
                        new KeyValuePair<string, string>("OK|package|host-fingerprint|1/2/mission-digest/other-lords", "ErrorParticipantsSelectionNotReady"),
                        new KeyValuePair<string, string>(CoopPackageStatusCodes.InvalidPrefix + technical, "ErrorParticipantsInvalid"),
                        new KeyValuePair<string, string>(CoopPackageStatusCodes.Disabled, "ErrorParticipantsDisabled"),
                        new KeyValuePair<string, string>(CoopPackageStatusCodes.Waiting, "ErrorParticipantsNotReady"),
                        new KeyValuePair<string, string>(null, "ErrorParticipantsNotReady"),
                    };
                    foreach (var item in cases)
                    {
                        string message = CoopPackageBlockMessages.Participants(new[] {
                            new KeyValuePair<string, string>("Guest", item.Key),
                            new KeyValuePair<string, string>("Ready player", expected),
                        }, expected);
                        Assert(message == catalog["ExtendedData." + item.Value].Replace("{Players}", "Guest"),
                            "wrong category or player attribution: " + culture + " " + item.Key);
                        Assert(!message.Contains(technical) && !message.Contains("Ready player"),
                            "chat leaked technical status or blamed a ready player");
                    }
                    string missing = CoopPackageBlockMessages.LocalLoadFailure(true, "selected-package");
                    string invalid = CoopPackageBlockMessages.LocalLoadFailure(false, "selected-package");
                    Assert(missing == catalog["ExtendedData.ErrorPackageMissingHelp"].Replace("{Package}", "selected-package"),
                        "missing package was reported as invalid");
                    Assert(invalid == catalog["ExtendedData.ErrorPackageValidationFailed"], "invalid package help missing");
                    var mixedReports = new[] {
                        new KeyValuePair<string, string>("First guest", CoopPackageStatusCodes.Mismatch),
                        new KeyValuePair<string, string>("Second guest", "OK|package|old-fingerprint"),
                        new KeyValuePair<string, string>("Pending guest", CoopPackageStatusCodes.Waiting),
                    };
                    string reason = CoopPackageBlockMessages.Participants(mixedReports, expected);
                    Assert(reason.Contains("First guest, Second guest") && reason.Contains("Pending guest"),
                        "combined report lost players or their groups");
                    Assert(CoopPackageBlockMessages.Participants(mixedReports.Reverse(), expected) == reason,
                        "roster ordering changed the cause and bypassed announcement deduplication");
                    Assert(CoopPackageBlockMessages.Participants(new[] {
                        new KeyValuePair<string, string>("Guest", "OK|package|host-fingerprint|stale-selection"),
                    }, "OK|package|host-fingerprint") == catalog["ExtendedData.ErrorParticipantsSelectionNotReady"].Replace("{Players}", "Guest"),
                        "an outdated selection suffix was reported as different package files");
                    string[] chunks = CoopLaunchBlockDiagnostics.ChatMessages("Ready", new[] { reason, invalid });
                    string reconstructed = string.Concat(chunks.Select(message =>
                        System.Text.RegularExpressions.Regex.Replace(message.Substring(CoopLaunchBlockDiagnostics.ChatPrefix.Length),
                            @"^\[\d+/\d+\] ", "")));
                    Assert(chunks.All(message => message.Length <= 280) &&
                        reconstructed == "Ready: " + (reason + "; " + invalid).Replace('\r', ' ').Replace('\n', ' '),
                        "localized package messages were shortened in chat");
                    var state = new CoopLaunchAnnouncementState();
                    DateTime now = DateTime.UtcNow;
                    Assert(state.ShouldSend("lobby", "Lobby", new[] { reason }, true, now) &&
                        !state.ShouldSend("lobby", "Lobby", new[] { reason }, true, now.AddMinutes(1)) &&
                        state.ShouldSend("lobby", "Lobby", new[] { invalid }, true, now.AddMinutes(1)),
                        "unchanged localized blocks repeated or a different cause was hidden");
                    using (var log = new ManualLogSource("coop-package-message-" + culture))
                    {
                        var entries = new List<string>();
                        log.LogEvent += (_, args) => entries.Add(args.Data.ToString());
                        CoopLaunchBlockDiagnostics.ReportLocal(log, "Ready", invalid, "role=client; package=selected-package", technical);
                        Assert(entries.Single().Contains(technical), "friendly message removed technical log detail");
                    }
                }
                finally { SerpLocalization.Overrides = null; }
            }
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

        private static void TestExtendedLordSourceIdentity()
        {
            string[] names = { "Rat", "Snake", "Pig", "Wolf" };
            Assert(TrailLordSourceSelector.ResolveLordName("", 3, names) == "Wolf",
                "Vanilla Lord with custom AIC was not resolved by type");
            Assert(TrailLordSourceSelector.ResolveLordName(null, 0, names) == "Rat",
                "zero-based type zero was rejected");
            Assert(TrailLordSourceSelector.ResolveLordName("123\\Custom", -1, names) == "123\\Custom",
                "Workshop identity changed");
            foreach (int type in new[] { -1, names.Length })
            {
                bool rejected = false;
                try { TrailLordSourceSelector.ResolveLordName("", type, names); }
                catch (InvalidDataException) { rejected = true; }
                Assert(rejected, "invalid unnamed Lord type accepted: " + type);
            }
            Assert(TrailLordSourceSelector.MatchesIdentity("Wolf", 3, "aic", "Wolf", 3, "AIC", true),
                "matching Extended Lord config rejected");
            Assert(!TrailLordSourceSelector.MatchesIdentity("Wolf", 2, "aic", "Wolf", 3, "aic", true),
                "another type accepted");
            Assert(!TrailLordSourceSelector.MatchesIdentity("Wolf", 3, "other", "Wolf", 3, "aic", true),
                "another config accepted");
            Assert(!TrailLordSourceSelector.MatchesIdentity("Local", -1, "aic", "123\\Local", -1, "aic", false),
                "local and Workshop identities confused");
            bool missingRejected = false;
            try { TrailLordSourceSelector.SelectIndex(Array.Empty<string>(), null, "Wolf"); }
            catch (InvalidDataException) { missingRejected = true; }
            Assert(missingRejected, "empty source list accepted");
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

        private static void TestDecodedLordReplicas()
        {
            WithRoots((local, workshop) =>
            {
                foreach (string directory in new[] { local, workshop })
                {
                    WriteText(Path.Combine(directory, "rat_custom.lordjson"), "same AIC");
                    WriteText(Path.Combine(directory, "castle.aivjson"), "same AIV");
                    WriteText(Path.Combine(directory, "rat_custom.modlord.json"),
                        "{\"mod\":{\"enabled\":true,\"values\":[1,2]}}");
                    WriteText(Path.Combine(directory, "Override", "Fixes", "preferences.json"),
                        "{\"Build\":1,\"Other\":{\"A\":true,\"B\":null}}");
                    WriteText(Path.Combine(directory, "info.json"), ValidInfo);
                    WriteText(Path.Combine(directory, "lordmeta.json"), ValidLordMeta);
                }
                // Exercise the actual installed Vanilla codec used by saved Trail Lord data.
                // Its path and workshop fields disappear even when the author selected Workshop.
                var original = new CustomisationFileManager.CustomLordConfig
                {
                    name = "rat_custom", path = workshop, workshop = true,
                };
                var decoded = CustomisationFileManager.CustomLordConfig.decode(original.encode());
                Assert(decoded.path == null && !decoded.workshop && decoded.name == original.name,
                    "Vanilla codec no longer drops source identity; re-audit the Trail contract");
                Func<string[], string> resolve = paths => paths[TrailLordSourceSelector.SelectIndex(
                    paths, decoded.path, "Rat", decoded.name)];
                string chosen = resolve(new[] { local, workshop });
                Assert(resolve(new[] { workshop, local }) == chosen,
                    "equivalent replicas depend on scan order");

                WriteText(Path.Combine(workshop, "avatar.png"), "different presentation");
                WriteText(Path.Combine(local, "README.md"), "local documentation");
                WriteText(Path.Combine(local, "rat_custom.ldata"), "local Workshop metadata");
                WriteText(Path.Combine(workshop, "Override", "Fixes", "preferences.json"),
                    "{\"Other\":{\"B\":null,\"A\":true},\"Build\":1}");
                Assert(resolve(new[] { local, workshop }) == chosen,
                    "presentation, control files or JSON property order changed equivalence");

                string[] jsonPaths = { "rat_custom.modlord.json", "Override/Fixes/preferences.json",
                    "info.json", "lordmeta.json" };
                foreach (string relative in jsonPaths)
                {
                    string path = Path.Combine(workshop, relative);
                    string saved = File.ReadAllText(path);
                    WriteText(path, "{\"different\":true}");
                    ExpectReplicaConflict(() => resolve(new[] { local, workshop }), local, workshop, relative);
                    File.Delete(path);
                    ExpectReplicaConflict(() => resolve(new[] { local, workshop }), local, workshop, relative);
                    WriteText(path, "{");
                    ExpectReplicaConflict(() => resolve(new[] { local, workshop }), workshop, null, relative);
                    WriteText(path, "null");
                    ExpectReplicaConflict(() => resolve(new[] { local, workshop }), workshop, null, relative);
                    WriteText(path, saved);
                    using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        ExpectReplicaConflict(() => resolve(new[] { local, workshop }), workshop, null, relative);
                }
                string modPath = Path.Combine(workshop, "rat_custom.modlord.json");
                string modSaved = File.ReadAllText(modPath);
                WriteText(modPath, "{\"mod\":{\"enabled\":true,\"values\":[2,1]}}");
                ExpectReplicaConflict(() => resolve(new[] { local, workshop }), local, workshop,
                    "rat_custom.modlord.json");
                WriteText(modPath, modSaved);
                string aiv = Path.Combine(workshop, "castle.aivjson");
                WriteText(aiv, "different AIV");
                ExpectReplicaConflict(() => resolve(new[] { local, workshop }), local, workshop, "gameplay");
                Assert(TrailLordSourceSelector.SelectIndex(new[] { local, workshop }, workshop,
                    "Rat", decoded.name) == 1, "explicit source was replaced by replica comparison");
                ExpectReplicaConflict(() => TrailLordSourceSelector.SelectIndex(new[] { local, workshop },
                    Path.Combine(local, "missing"), "Rat", decoded.name), null, null, "selected source path");
                WriteText(aiv, "same AIV");
                WriteText(Path.Combine(workshop, "extra.lua"), "return 1");
                ExpectReplicaConflict(() => resolve(new[] { local, workshop }), local, workshop, "gameplay");
                Assert(TrailLordSourceSelector.SelectIndex(new[] { workshop }, null,
                    "Rat", decoded.name) == 0, "unique source unexpectedly required replica equivalence");
            });
        }

        private static void ExpectReplicaConflict(Action action, string first, string second, string detail)
        {
            try { action(); }
            catch (InvalidDataException exception)
            {
                Assert((first == null || exception.Message.Contains(first)) &&
                    (second == null || exception.Message.Contains(second)) &&
                    exception.Message.IndexOf(detail, StringComparison.OrdinalIgnoreCase) >= 0,
                    "replica conflict lost source or difference detail: " + exception.Message);
                return;
            }
            throw new Exception("Conflicting or unverifiable replicas were accepted: " + detail);
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

        private static void TestActivationAndDelayedDecisions()
        {
            WithSource(source =>
            {
                WriteVanillaBase(source);
                var stager = new FakeStager(source);
                var confirmation = new FakeConfirmation();
                bool enabled = false;
                var workflow = new CustomLordUploadWorkflow(new ManualLogSource("ActivationTests"),
                    stager, confirmation, CustomLordRuntimeRules.CreateCompatibilityProfile("1.0.0+a7775a6"), () => enabled);
                int originals = 0, failures = 0;
                workflow.Handle(CreateRequest(new[] { "Custom Lord" }, () => { }, () => failures++), request => { originals++; request.SuccessAction?.Invoke(); });
                Assert(originals == 1 && stager.StageCount == 0 && confirmation.ShowCount == 0,
                    "disabled workflow must pass directly to its original without inspecting the package");
                enabled = true;
                workflow.Handle(CreateRequest(new[] { "Custom Lord" }, () => { }, () => failures++), request => { originals++; request.SuccessAction?.Invoke(); });
                Assert(confirmation.ShowCount == 1 && stager.StageCount == 0, "missing metadata should defer staging");
                Action oldYes = confirmation.CaptureConfirm();
                enabled = false;
                workflow.InvalidatePending();
                enabled = true;
                oldYes(); oldYes();
                Assert(originals == 1 && failures == 1 && stager.StageCount == 0,
                    "an off/on cycle must invalidate the old confirmation exactly once");
                workflow.Handle(CreateRequest(new[] { "Custom Lord" }, () => { }, () => failures++), request => { originals++; request.SuccessAction?.Invoke(); });
                Action staleYes = confirmation.CaptureConfirm();
                workflow.Handle(CreateRequest(new[] { "Custom Lord" }, () => { }, () => failures++), request => { originals++; request.SuccessAction?.Invoke(); });
                staleYes();
                Assert(originals == 1 && failures == 2 && stager.StageCount == 0,
                    "an older attempt may not stage or replace a newer confirmation");
                confirmation.Confirm(); confirmation.Confirm();
                Assert(originals == 2 && stager.StageCount == 1, "only the current accepted attempt starts once");

                bool currentAttempt = true;
                workflow.Handle(CreateRequest(new[] { "Custom Lord" }, () => { }, () => failures++),
                    request => { originals++; request.SuccessAction?.Invoke(); }, () => currentAttempt);
                currentAttempt = false;
                confirmation.Confirm();
                Assert(originals == 2 && failures == 3 && stager.StageCount == 1,
                    "changing the editor upload attempt invalidates its deferred confirmation before staging");

                WriteText(Path.Combine(source, "info.json"), ValidInfo);
                WriteText(Path.Combine(source, "lordmeta.json"), ValidLordMeta);
                CustomLordUploadRequest handed = null;
                int successes = 0;
                workflow.Handle(CreateRequest(new[] { "Custom Lord" }, () => successes++, () => failures++), request => handed = request);
                Assert(handed != null, "valid package should be handed to the transport");
                enabled = false; workflow.InvalidatePending();
                handed.SuccessAction(); handed.SuccessAction(); handed.FailAction();
                Assert(successes == 1 && failures == 3, "disabling must not lose or duplicate an already handed-off completion");
            });
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

            internal Action CaptureConfirm() => confirm ?? throw new InvalidOperationException("No pending confirmation");
            internal void Confirm() => confirm?.Invoke();
            internal void Cancel() => cancel?.Invoke();
        }
    }
}
