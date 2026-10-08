using APIShared.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
namespace APIShared.Core.Tests;
[TestClass]
public class JsonTests
{
    [TestMethod]
    public void NumbersEscapesAndRoundTripAreCultureIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var payload = (Dictionary<string, object>)DependencyFreeJson.Parse("{\"number\":1.25,\"max\":18446744073709551615,\"text\":\"line\\n\\\"\\\\ä\",\"array\":[null,true,false,-2]}");
            Assert.AreEqual(1.25, payload["number"]);
            Assert.AreEqual(ulong.MaxValue, payload["max"]);
            Assert.AreEqual("line\n\"\\ä", payload["text"]);
            var encoded = DependencyFreeJson.Serialize(payload);
            Assert.IsTrue(encoded.EndsWith("\r\n", StringComparison.Ordinal));
            var decoded = (Dictionary<string, object>)DependencyFreeJson.Parse(encoded);
            Assert.AreEqual(payload["number"], decoded["number"]);
            Assert.AreEqual(payload["text"], decoded["text"]);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    [TestMethod]
    [DataRow("{\"x\":1,\"x\":2}")]
    [DataRow("{\"x\":1,}")]
    [DataRow("[1,]")]
    [DataRow("01")]
    [DataRow("1e999")]
    [DataRow("true false")]
    [DataRow("\"bad\ntext\"")]
    [DataRow("{")]
    [DataRow("[")]
    [DataRow("")]
    public void InvalidInputIsRejected(string input) =>
        Assert.ThrowsExactly<InvalidDataException>(() => DependencyFreeJson.Parse(input));
    [TestMethod] public void TrailingCommaIsExplicitlyOptIn()
    {
        var parsed = (List<object>)DependencyFreeJson.Parse("[1,]", true);
        Assert.HasCount(1, parsed);
    }
    [TestMethod] public void DepthNonFiniteNumbersAndCyclesAreRejected()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => DependencyFreeJson.Parse(new string('[',70)+"0"+new string(']',70)));
        Assert.ThrowsExactly<InvalidDataException>(() => DependencyFreeJson.Serialize(double.NaN));
        Assert.ThrowsExactly<InvalidDataException>(() => DependencyFreeJson.Serialize(double.PositiveInfinity));
        var cycle = new List<object>(); cycle.Add(cycle);
        Assert.ThrowsExactly<InvalidDataException>(() => DependencyFreeJson.Serialize(cycle));
    }
}
