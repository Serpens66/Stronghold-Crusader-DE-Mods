using System;
using System.Globalization;
class Program
{
    static int Main()
    {
        var original = CultureInfo.CurrentCulture;
        bool passed = true;
        try
        {
            foreach (var name in new[] { "de-DE", "en-US" })
            {
                var culture = CultureInfo.GetCultureInfo(name);
                CultureInfo.CurrentCulture = culture;
                Console.WriteLine("CULTURE " + name);
                passed &= CrusaderDETweaker.Tests.CoreTestRunner.RunAllTests();
                if (!ReferenceEquals(CultureInfo.CurrentCulture, culture)) throw new Exception("Test leaked its culture.");
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
        return passed ? 0 : 1;
    }
}
namespace CrusaderDETweaker
{
    static class Plugin { internal static readonly ConsoleLogger Logger = new ConsoleLogger(); }
    sealed class ConsoleLogger
    {
        public void LogInfo(object text) => Console.WriteLine(text);
        public void LogWarning(object text) => Console.WriteLine("EXPECTED TEST WARNING: " + text);
        public void LogError(object text) => Console.WriteLine("ERROR: " + text);
        public void LogDebug(object text) { }
    }
}
namespace CrusaderDETweaker.Data { internal class NamespaceMarker { } }
namespace SHCDESE.Interop { internal class NamespaceMarker { } }

namespace BepInEx { static class Paths { public static string ConfigPath => System.IO.Path.GetTempPath(); } }
