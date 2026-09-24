using System;
using System.IO;

namespace AivLobbyPresetTest
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string path = Path.Combine(Path.GetTempPath(), "aiv-lobby-preset-" + Guid.NewGuid() + ".json");
            string progressPath = path + ".progress";
            try
            {
                string sample = File.ReadAllText(args[0]);
                File.WriteAllText(path, sample);
                var first = LobbyPreset.Read(path);
                Require(first.Enabled && first.Players.Count == 3 &&
                    first.Players[1].Id == 2 && first.Players[1].AivDefault == 6 &&
                    first.Players[1].KeepSlot == 6, "initial Crater Lake setup");
                File.WriteAllText(path, sample.Replace("\"aivDefault\": 6", "\"aivDefault\": 5"));
                Require(LobbyPreset.Read(path).Players[1].AivDefault == 5,
                    "config reloaded on next lobby opening");
                string multiAiv = sample.Replace("\"aivDefault\": 6", "\"aivDefaults\": [1, 8]");
                File.WriteAllText(path, multiAiv);
                PresetPlayer multiPlayer = LobbyPreset.Read(path).Players[1];
                Require(multiPlayer.AivDefaults.Count == 2 &&
                    multiPlayer.AivDefaults[0] == 1 && multiPlayer.AivDefaults[1] == 8,
                    "ordered built-in AIV variants");
                Reject(path, multiAiv.Replace("[1, 8]", "[1, 1]"), "duplicate AIV variant");
                Reject(path, multiAiv.Replace("[1, 8]", "[1, 9]"), "out-of-range AIV variant");
                Reject(path, multiAiv.Replace("[1, 8]", "[]"), "empty AIV list");
                Reject(path, multiAiv.Replace("[1, 8]", "[1, true]"), "non-numeric AIV variant");
                Reject(path, multiAiv.Replace("\"aivDefaults\": [1, 8]",
                    "\"aivDefault\": 6, \"aivDefaults\": [1, 8]"), "conflicting AIV forms");
                Reject(path, sample.Replace("\"keepSlot\": 5", "\"keepSlot\": 6"), "duplicate Keep slot");
                Reject(path, sample.Replace("\"id\": 3", "\"id\": 4"), "player order gap");
                Reject(path, sample.Replace("\"aivDefault\": 6", "\"aivDefault\": 9"), "invalid AIV variant");
                File.WriteAllText(path, sample.Replace("\"enabled\": true", "\"enabled\": false"));
                Require(!LobbyPreset.Read(path).Enabled, "disabled preset yields control");
                TestSeries series = TestSeries.Read(args[1]);
                Require(series.Enabled && series.Runs.Count == 10, "ten queued runs");
                for (int index = 0; index < series.Runs.Count; index += 2)
                {
                    Require(series.Runs[index].PreBuild == 0 &&
                        series.Runs[index + 1].PreBuild == 1 &&
                        series.Runs[index].Preset.Players.Count == 8 &&
                        series.Runs[index + 1].Preset.Players.Count == 8,
                        "paired seven-AI runs");
                }
                Require(series.Runs[0].Preset.Players[1].AivDefault == 6 &&
                    series.Runs[2].Preset.Players[1].AivDefault == 5 &&
                    series.Runs[4].Preset.Players[1].LordType == 18,
                    "fixed variant and reversed-order profiles");
                TestSeriesProgress progress = TestSeriesProgress.Read(progressPath, series);
                Require(progress.NextIndex == 0, "new series starts at first run");
                progress.Complete(progressPath, series, 0, series.Runs[0].Id);
                progress = TestSeriesProgress.Read(progressPath, series);
                Require(progress.NextIndex == 1 &&
                    progress.LastCompletedRunId == series.Runs[0].Id,
                    "progress resumes after restart");
                bool duplicateRejected = false;
                try { progress.Complete(progressPath, series, 0, series.Runs[0].Id); }
                catch (InvalidOperationException) { duplicateRejected = true; }
                Require(duplicateRejected, "duplicate completion rejected");
                File.WriteAllText(progressPath, "{\"seriesId\":\"wrong\",\"nextIndex\":1,\"lastCompletedRunId\":\"x\"}");
                bool mismatchRejected = false;
                try { TestSeriesProgress.Read(progressPath, series); }
                catch (InvalidDataException) { mismatchRejected = true; }
                Require(mismatchRejected, "mismatched progress rejected");
                string seriesJson = File.ReadAllText(args[1]);
                var seriesRoot = (System.Collections.Generic.Dictionary<string, object>)
                    Shared.DependencyFreeJson.Parse(seriesJson);
                var runList = (System.Collections.IList)seriesRoot["runs"];
                var secondRun = (System.Collections.Generic.IDictionary<string, object>)runList[1];
                secondRun["id"] = series.Runs[0].Id;
                File.WriteAllText(path, Shared.DependencyFreeJson.Serialize(seriesRoot));
                RejectSeries(path, "duplicate run ID");
                TestSeries fullGridProbe = TestSeries.Read(args[2]);
                Require(fullGridProbe.Enabled && fullGridProbe.Runs.Count == 1 &&
                    fullGridProbe.Runs[0].Id == "CC-A-on" &&
                    fullGridProbe.Runs[0].PreBuild == 1 &&
                    fullGridProbe.Runs[0].Preset.Players.Count == 8,
                    "single Craggy full-grid probe");
                TestSeries craterProbe = TestSeries.Read(args[3]);
                Require(craterProbe.Enabled && craterProbe.Runs.Count == 1 &&
                    craterProbe.Runs[0].Id == "CL-A-on" &&
                    craterProbe.Runs[0].PreBuild == 1 &&
                    craterProbe.Runs[0].Preset.Players.Count == 8 &&
                    craterProbe.Runs[0].Preset.MapFileName == "Crater Lake.map",
                    "single Crater 180-degree probe");
                File.Delete(progressPath);
                TestSeriesProgress probeProgress = TestSeriesProgress.Read(progressPath, craterProbe);
                probeProgress.Complete(progressPath, craterProbe, 0, craterProbe.Runs[0].Id);
                Require(TestSeriesProgress.Read(progressPath, craterProbe).NextIndex == 1,
                    "single probe stops after its match");
                TestSeries sixMatches = TestSeries.Read(args[4]);
                string[] sixMatchIds = {
                    "CL-A-off", "CL-A-on", "CL-B-on", "CL-Reverse-on", "CC-B-off", "CC-B-on"
                };
                Require(sixMatches.Enabled && sixMatches.Runs.Count == sixMatchIds.Length &&
                    sixMatches.Id == "aiv-full-grid-six-match-20260924",
                    "six-match full-grid series");
                for (int index = 0; index < sixMatchIds.Length; index++)
                {
                    TestRun actual = sixMatches.Runs[index];
                    TestRun expected = null;
                    foreach (TestRun sourceRun in series.Runs)
                        if (sourceRun.Id == sixMatchIds[index]) expected = sourceRun;
                    Require(expected != null && actual.Id == sixMatchIds[index] &&
                        actual.PreBuild == expected.PreBuild &&
                        actual.Preset.MapFileName == expected.Preset.MapFileName &&
                        actual.Preset.MapSha256 == expected.Preset.MapSha256 &&
                        actual.Preset.Players.Count == 8,
                        "six-match source identity and option at " + index);
                    for (int playerIndex = 0; playerIndex < 8; playerIndex++)
                    {
                        PresetPlayer a = actual.Preset.Players[playerIndex];
                        PresetPlayer b = expected.Preset.Players[playerIndex];
                        Require(a.Id == b.Id && a.Human == b.Human &&
                            a.LordType == b.LordType && a.AivDefault == b.AivDefault &&
                            a.KeepSlot == b.KeepSlot && a.RadarX == b.RadarX &&
                            a.RadarY == b.RadarY && a.KeepX == b.KeepX &&
                            a.KeepY == b.KeepY,
                            "six-match player source identity at " + index + "/" + playerIndex);
                    }
                }
                TestSeries multiAivSeries = TestSeries.Read(args[5]);
                Require(multiAivSeries.Enabled && multiAivSeries.Runs.Count == 8 &&
                    multiAivSeries.Id == "aiv-multi-default-eight-20260924",
                    "eight-run multi-AIV series");
                for (int index = 0; index < multiAivSeries.Runs.Count; index++)
                {
                    TestRun run = multiAivSeries.Runs[index];
                    bool crater = index < 4;
                    bool early = index % 4 < 2;
                    TestRun source = sixMatches.Runs[crater ? 0 : 4];
                    int changedPlayer = early ? 1 : 4;
                    int firstVariant = early ? 1 : 2;
                    int secondVariant = early ? 8 : 7;
                    Require(run.Id == (crater ? "CL" : "CC") +
                        (early ? "-Early-" : "-Middle-") + (index % 2 == 0 ? "off" : "on") &&
                        run.PreBuild == index % 2 &&
                        run.Preset.MapFileName == source.Preset.MapFileName &&
                        run.Preset.MapSha256 == source.Preset.MapSha256 &&
                        run.Preset.Players.Count == 8,
                        "multi-AIV map and option at " + index);
                    for (int playerIndex = 0; playerIndex < 8; playerIndex++)
                    {
                        PresetPlayer actual = run.Preset.Players[playerIndex];
                        PresetPlayer original = source.Preset.Players[playerIndex];
                        Require(actual.Id == original.Id && actual.Human == original.Human &&
                            actual.LordType == original.LordType &&
                            actual.KeepSlot == original.KeepSlot &&
                            actual.KeepX == original.KeepX && actual.KeepY == original.KeepY &&
                            actual.RadarX == original.RadarX && actual.RadarY == original.RadarY,
                            "multi-AIV player identity at " + index + "/" + playerIndex);
                        if (!actual.Human)
                            Require(playerIndex == changedPlayer
                                ? actual.AivDefaults.Count == 2 &&
                                  actual.AivDefaults[0] == firstVariant &&
                                  actual.AivDefaults[1] == secondVariant
                                : actual.AivDefaults.Count == 1 &&
                                  actual.AivDefault == original.AivDefault,
                                "multi-AIV candidate order at " + index + "/" + playerIndex);
                    }
                }
                File.Delete(progressPath);
                TestSeriesProgress sixProgress = TestSeriesProgress.Read(progressPath, sixMatches);
                for (int index = 0; index < sixMatches.Runs.Count; index++)
                {
                    sixProgress.Complete(progressPath, sixMatches, index, sixMatches.Runs[index].Id);
                    sixProgress = TestSeriesProgress.Read(progressPath, sixMatches);
                    Require(sixProgress.NextIndex == index + 1,
                        "six-match progress at " + index);
                }
                Console.WriteLine("AIV lobby preset parser tests passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(progressPath)) File.Delete(progressPath);
            }
        }

        private static void Reject(string path, string contents, string name)
        {
            File.WriteAllText(path, contents);
            try { LobbyPreset.Read(path); }
            catch (InvalidDataException) { return; }
            throw new Exception("Expected rejection: " + name);
        }

        private static void Require(bool condition, string name)
        {
            if (!condition) throw new Exception("Failed: " + name);
        }

        private static void RejectSeries(string path, string name)
        {
            try { TestSeries.Read(path); }
            catch (InvalidDataException) { return; }
            throw new Exception("Expected rejection: " + name);
        }
    }
}
