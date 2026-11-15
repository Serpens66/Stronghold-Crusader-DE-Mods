using System.Reflection;
using BepInEx;
using BepInEx.Logging;

[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

namespace CrashDiagnosticsBenchmark
{
    [BepInPlugin(Guid, "Crash Diagnostics Benchmark", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.53.0")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "CrashDiagnosticsBenchmark_Serp";
        private void Awake() { Shared.DevDiag.Attach(Logger); InitializeBenchmark(Logger); }
        partial void InitializeBenchmark(ManualLogSource log);
    }
}
