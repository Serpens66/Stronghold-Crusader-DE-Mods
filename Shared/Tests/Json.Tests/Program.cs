using System.Globalization;

int checks = 0;
void Assert(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
var codecs = new[] {
    ("workspace", (Func<string, bool, object>)Shared.DependencyFreeJson.Parse, (Func<object, string>)Shared.DependencyFreeJson.Serialize),
    ("APIShared", (Func<string, bool, object>)APIShared.Internal.DependencyFreeJson.Parse, (Func<object, string>)APIShared.Internal.DependencyFreeJson.Serialize)
};
void Reject(Action action, string label) {
    try { action(); } catch (InvalidDataException) { checks++; return; }
    throw new Exception("Accepted invalid JSON: " + label);
}
var oldCulture = CultureInfo.CurrentCulture;
try {
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
    foreach (var (name, parse, serialize) in codecs) {
        var root = (Dictionary<string, object>)parse("{\"number\":1.25,\"max\":18446744073709551615,\"text\":\"line\\n\\\"\\\\ä\",\"array\":[null,true,false,-2]}", false);
        Assert((double)root["number"] == 1.25, name + " invariant number");
        Assert((ulong)root["max"] == ulong.MaxValue, name + " unsigned integer");
        Assert((string)root["text"] == "line\n\"\\ä", name + " escaping");
        string encoded = serialize(root);
        Assert(encoded.EndsWith("\r\n", StringComparison.Ordinal), name + " serialization CRLF");
        Assert((double)((Dictionary<string, object>)parse(encoded, false))["number"] == 1.25, name + " round trip");
        foreach (string invalid in new[] {"{\"x\":1,\"x\":2}", "{\"x\":1,}", "[1,]", "01", "1e999", "true false", "\"bad\ntext\"", "{", "[", ""})
            Reject(() => parse(invalid, false), name + ": " + invalid);
        Assert(((List<object>)parse("[1,]", true)).Count == 1, name + " explicit trailing comma");
        Reject(() => parse(new string('[', 70) + "0" + new string(']', 70), false), name + " depth limit");
        Reject(() => serialize(double.NaN), name + " NaN");
        Reject(() => serialize(double.PositiveInfinity), name + " infinity");
        var cyclic = new List<object>(); cyclic.Add(cyclic);
        Reject(() => serialize(cyclic), name + " cycle");
    }
    var payload = new Dictionary<string, object> {
        ["schema"] = 1, ["guid"] = "foreign.mod", ["values"] = new List<object> { "ä\n", true, null, 1.5 }
    };
    Assert(codecs[0].Item3(payload) == codecs[1].Item3(payload), "Existing serialization wire format remains compatible");
    Assert(typeof(Shared.DependencyFreeJson).FullName != typeof(APIShared.Internal.DependencyFreeJson).FullName,
        "Implementations have independent type identities");
} finally { CultureInfo.CurrentCulture = oldCulture; }
Console.WriteLine($"PASS: {checks} JSON behavior checks on independently compiled workspace and APIShared parsers.");
