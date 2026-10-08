using APIShared.GameModes;
// Managed/native input stand-ins. Policy, state machine and adapter are the actual production sources.
internal static class EngineInterface
{
    internal struct LoadMapReturnData
    {
        internal int game_type, skirmishGameType, skirmishTrail, coopTrailID;
    }
}
namespace APIShared
{
    public sealed class NativeCapabilityDiagnostic { }
    internal static class MissionLifecycleService
    {
        internal static APIShared.GameModes.GameModeSnapshot Snapshot;
        internal static bool HasContext => Snapshot.Kind != APIShared.GameModes.GameModeKind.Unknown;
    }
}
