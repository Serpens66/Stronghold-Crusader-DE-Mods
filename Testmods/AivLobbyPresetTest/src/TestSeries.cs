using Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace AivLobbyPresetTest
{
    internal sealed class TestRun
    {
        internal string Id;
        internal int PreBuild;
        internal LobbyPreset Preset;
    }

    internal sealed class TestSeries
    {
        internal bool Enabled;
        internal string Id;
        internal readonly List<TestRun> Runs = new List<TestRun>();

        internal static TestSeries Read(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > 1024 * 1024)
                throw new InvalidDataException("Test series missing or larger than 1 MiB: " + path);
            var root = DependencyFreeJson.Parse(File.ReadAllText(path)) as IDictionary<string, object>
                ?? throw new InvalidDataException("Expected test-series JSON object.");
            var series = new TestSeries { Enabled = Bool(root, "enabled") };
            if (!series.Enabled)
                return series;
            series.Id = Text(root, "seriesId");
            var runs = root["runs"] as IList;
            if (runs == null || runs.Count < 1 || runs.Count > 20)
                throw new InvalidDataException("Test series requires 1-20 runs.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (object value in runs)
            {
                var item = value as IDictionary<string, object>
                    ?? throw new InvalidDataException("Expected run object.");
                string id = Text(item, "id");
                if (!ids.Add(id))
                    throw new InvalidDataException("Duplicate run ID: " + id);
                if (!item.TryGetValue("preBuild", out object preBuildValue) ||
                    !(preBuildValue is bool preBuild))
                    throw new InvalidDataException("Missing preBuild Boolean for " + id);
                if (!item.TryGetValue("preset", out object presetValue))
                    throw new InvalidDataException("Missing preset for " + id);
                LobbyPreset preset = LobbyPreset.FromValue(presetValue);
                if (!preset.Enabled)
                    throw new InvalidDataException("Disabled run preset: " + id);
                series.Runs.Add(new TestRun
                {
                    Id = id,
                    PreBuild = preBuild ? 1 : 0,
                    Preset = preset
                });
            }
            return series;
        }

        private static bool Bool(IDictionary<string, object> root, string key) =>
            root.TryGetValue(key, out object value) && value is bool flag
                ? flag : throw new InvalidDataException("Missing Boolean: " + key);

        private static string Text(IDictionary<string, object> root, string key) =>
            root.TryGetValue(key, out object value) && value is string text &&
            !string.IsNullOrWhiteSpace(text) && text.Length <= 80
                ? text : throw new InvalidDataException("Missing or invalid text: " + key);
    }

    internal sealed class TestSeriesProgress
    {
        internal string SeriesId;
        internal string LastCompletedRunId;
        internal int NextIndex;

        internal static TestSeriesProgress Read(string path, TestSeries series)
        {
            if (!File.Exists(path))
                return new TestSeriesProgress
                {
                    SeriesId = series.Id,
                    LastCompletedRunId = string.Empty,
                    NextIndex = 0
                };
            var file = new FileInfo(path);
            if (file.Length > 4096)
                throw new InvalidDataException("Test-series progress is too large.");
            var root = DependencyFreeJson.Parse(File.ReadAllText(path)) as IDictionary<string, object>
                ?? throw new InvalidDataException("Expected progress JSON object.");
            var progress = new TestSeriesProgress
            {
                SeriesId = root.TryGetValue("seriesId", out object seriesId) ? seriesId as string : null,
                LastCompletedRunId = root.TryGetValue("lastCompletedRunId", out object last) ? last as string : null,
                NextIndex = root.TryGetValue("nextIndex", out object next) ? Convert.ToInt32(next) : -1
            };
            if (progress.SeriesId != series.Id || progress.NextIndex < 0 ||
                progress.NextIndex > series.Runs.Count ||
                progress.LastCompletedRunId != (progress.NextIndex == 0
                    ? string.Empty : series.Runs[progress.NextIndex - 1].Id))
                throw new InvalidDataException("Progress does not match the test-series order; reset it explicitly.");
            return progress;
        }

        internal void Complete(string path, TestSeries series, int index, string runId)
        {
            if (index != NextIndex || index >= series.Runs.Count || series.Runs[index].Id != runId)
                throw new InvalidOperationException("Test-series progress changed before completion.");
            var next = new Dictionary<string, object>
            {
                ["seriesId"] = series.Id,
                ["lastCompletedRunId"] = runId,
                ["nextIndex"] = index + 1
            };
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, DependencyFreeJson.Serialize(next));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                LastCompletedRunId = runId;
                NextIndex = index + 1;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
