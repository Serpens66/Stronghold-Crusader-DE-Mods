int assertions = 0;
APISharedTests.MissionLifecycleTests.Run((condition, message) =>
{
    assertions++;
    if (!condition) throw new Exception(message);
});
Console.WriteLine($"PASS: {assertions} mission state assertions (production state machine and contracts).");

// External type placeholders only; transition behavior comes exclusively from production sources.
namespace Shared { public readonly struct GameModeSnapshot { } }
namespace APIShared { public sealed class NativeCapabilityDiagnostic { } }
