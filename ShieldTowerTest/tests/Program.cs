using System.Text.RegularExpressions;
using ShieldTowerTest;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

Check(PortableShieldClimbSelectionPolicy.ShouldOverrideVanilla(true, 0, 1, 0, 0, 0),
    "A pure local movable-shield selection must override a negative Vanilla result.");
Check(!PortableShieldClimbSelectionPolicy.ShouldOverrideVanilla(false, 0, 1, 0, 0, 0),
    "A disabled feature must pass Vanilla through.");
Check(!PortableShieldClimbSelectionPolicy.ShouldOverrideVanilla(true, 1, 1, 0, 0, 0),
    "A positive Vanilla result must remain unchanged.");
Check(!PortableShieldClimbSelectionPolicy.ShouldOverrideVanilla(true, 0, 1, 1, 0, 0),
    "A mixed local selection must pass Vanilla through.");
Check(!PortableShieldClimbSelectionPolicy.ShouldOverrideVanilla(true, 0, 1, 0, 1, 0),
    "A foreign selection must pass Vanilla through.");
Check(!PortableShieldClimbSelectionPolicy.ShouldOverrideVanilla(true, 0, 1, 0, 0, 1),
    "A non-movable shield must pass Vanilla through.");
Check(!PortableShieldClimbSelectionPolicy.ShouldOverrideVanilla(true, 0, 0, 0, 0, 0),
    "An empty selection must pass Vanilla through.");

string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string mod = Path.Combine(root, "ShieldTowerTest");
string plugin = File.ReadAllText(Path.Combine(mod, "src", "ShieldTowerTestPlugin.cs"));
string feature = File.ReadAllText(Path.Combine(mod, "src", "PortableShieldClimbOverride.cs"));
string project = File.ReadAllText(Path.Combine(mod, "ShieldTowerTest.csproj"));
string manifest = File.ReadAllText(Path.Combine(mod, "info.json"));

Check(plugin.Contains("CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded"),
    "Runtime initialization must wait for CrusaderLibrary.LibraryLoaded.");
Check(plugin.Contains("private static PortableShieldClimbOverride feature"),
    "The feature must remain statically rooted after startup component destruction.");
Check(!plugin.Contains("OnDestroy") && !plugin.Contains("OnApplicationQuit"),
    "The startup component must not tear process-wide hooks down.");
Check(feature.Contains("FailureMode = TransactionFailureMode.RollbackAndThrow") &&
      feature.Contains("OwnsHooks = true") && feature.Contains("result.IsCompleteSuccess"),
    "The native detour must be installed atomically and fail closed.");
Check(feature.Contains("for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)") &&
      feature.Contains("int unitId = spanIndex + 1"),
    "Span indices and one-based game IDs must be converted exactly once.");
Check(feature.Contains("Marshal.SizeOf(typeof(GameUnit)) != 0x490") &&
      Regex.Matches(feature, "Marshal.OffsetOf").Count == 6,
    "The audited managed GameUnit layout must be checked before hooking.");
Check(feature.Contains("SetDestinationReferenceRva = 0x196280") &&
      feature.Contains("CanAUnitClimbReferenceRva = 0x18DC40") &&
      !feature.Contains("0x195E30"),
    "Only the reconstructible native targets may be present.");
Check(project.Contains("RedBird.Abstractions") && project.Contains("RedBird.Core") &&
      project.Contains("RedBird.X64") && !project.Contains("Zhuqiaomon"),
    "The port must use the Script Extender 2.2.0 RedBird hook API.");
Check(manifest.Contains("\"NetworkMode\": 1") &&
      manifest.Contains("\"MinimumScriptExtenderVersion\": \"2.2.0\""),
    "The gameplay test mod must require matching multiplayer installations and Script Extender 2.2.0.");

Console.WriteLine("ShieldTowerTest policy and static contract tests passed.");
