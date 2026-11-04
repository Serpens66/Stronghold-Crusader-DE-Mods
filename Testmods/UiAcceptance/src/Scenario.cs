using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Shared;

namespace UiAcceptance
{
    internal sealed class Expectation
    {
        internal Regex Marker;
        internal int Min = 1, Max = int.MaxValue;
        internal string Text;
    }

    internal sealed class Step
    {
        internal string Id, Do;
        internal string[] Args = new string[0];
        internal int TimeoutMs = 30000;
        internal bool StopOnFail, AllowErrors;
        internal readonly List<Expectation> Expect = new List<Expectation>();
        internal string Arg(int index, string fallback = null) => index < Args.Length ? Args[index] : fallback ??
            throw new InvalidDataException("Step " + Id + " (" + Do + ") needs argument " + index);
        internal int Int(int index, int? fallback = null) =>
            index < Args.Length ? int.Parse(Args[index], CultureInfo.InvariantCulture) :
            fallback ?? throw new InvalidDataException("Step " + Id + " (" + Do + ") needs integer argument " + index);
    }

    // { "enabled": true, "series": "SP-1", "role": "sp|host|client", "quitWhenDone": true,
    //   "steps": [ { "id": "load", "do": "LoadSave", "args": ["x.sav"], "timeoutMs": 90000,
    //                "stopOnFail": true, "allowErrors": false,
    //                "expect": [ { "marker": "regex", "min": 1, "max": 1 } ] } ] }
    internal sealed class Scenario
    {
        internal bool Enabled, QuitWhenDone = true;
        internal string Series, Role = "sp";
        internal readonly List<Step> Steps = new List<Step>();

        internal static Scenario Read(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length > 1024 * 1024) throw new InvalidDataException("Scenario missing or larger than 1 MiB: " + path);
            var root = DependencyFreeJson.Parse(File.ReadAllText(path)) as IDictionary<string, object>
                ?? throw new InvalidDataException("Expected scenario JSON object.");
            var scenario = new Scenario
            {
                Enabled = Get(root, "enabled", false),
                Series = Get(root, "series", Path.GetFileNameWithoutExtension(path)),
                Role = Get(root, "role", "sp").ToLowerInvariant(),
                QuitWhenDone = Get(root, "quitWhenDone", true)
            };
            if (scenario.Role != "sp" && scenario.Role != "host" && scenario.Role != "client")
                throw new InvalidDataException("role must be sp, host or client.");
            var steps = root.TryGetValue("steps", out object value) ? value as IList : null;
            if (steps == null || steps.Count == 0 || steps.Count > 500) throw new InvalidDataException("Scenario needs 1-500 steps.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < steps.Count; i++)
            {
                var item = steps[i] as IDictionary<string, object> ?? throw new InvalidDataException("Expected step object at " + i);
                var step = new Step
                {
                    Id = Get(item, "id", (i + 1).ToString("000", CultureInfo.InvariantCulture)),
                    Do = Get<string>(item, "do", null) ?? throw new InvalidDataException("Step " + i + " has no 'do'."),
                    TimeoutMs = (int)Get(item, "timeoutMs", 30000.0),
                    StopOnFail = Get(item, "stopOnFail", false),
                    AllowErrors = Get(item, "allowErrors", false)
                };
                if (!ids.Add(step.Id)) throw new InvalidDataException("Duplicate step id " + step.Id);
                if (item.TryGetValue("args", out object args) && args is IList list)
                {
                    step.Args = new string[list.Count];
                    for (int a = 0; a < list.Count; a++)
                        step.Args[a] = Convert.ToString(list[a], CultureInfo.InvariantCulture);
                }
                if (item.TryGetValue("expect", out object expect) && expect is IList expectations)
                    foreach (object entry in expectations)
                    {
                        var e = entry as IDictionary<string, object> ?? throw new InvalidDataException("Expected expectation object in " + step.Id);
                        string marker = Get<string>(e, "marker", null) ?? throw new InvalidDataException("Expectation without marker in " + step.Id);
                        step.Expect.Add(new Expectation
                        {
                            Text = marker,
                            Marker = new Regex(marker, RegexOptions.CultureInvariant),
                            Min = (int)Get(e, "min", 1.0),
                            Max = e.ContainsKey("max") ? (int)Get(e, "max", 0.0) : int.MaxValue
                        });
                    }
                scenario.Steps.Add(step);
            }
            return scenario;
        }

        private static T Get<T>(IDictionary<string, object> map, string key, T fallback)
        {
            if (!map.TryGetValue(key, out object value) || value == null) return fallback;
            if (value is T typed) return typed;
            if (typeof(T) == typeof(double)) return (T)(object)Convert.ToDouble(value, CultureInfo.InvariantCulture);
            throw new InvalidDataException("Field '" + key + "' has type " + value.GetType().Name + ", expected " + typeof(T).Name);
        }
    }
}
