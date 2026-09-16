using System;
using Shared;

namespace APIShared
{
    /// <summary>Map objective subtype, independent of campaign/trail/editor provenance.</summary>
    public enum MissionMapType
    {
        /// <summary>Subtype is not available.</summary>
        Unknown,
        /// <summary>Unrestricted building map.</summary>
        FreeBuild,
        /// <summary>Economic objectives.</summary>
        Economy,
        /// <summary>Siege objectives.</summary>
        Siege,
        /// <summary>Invasion objectives.</summary>
        Invasion
    }
    /// <summary>How an interactive mission is initialized.</summary>
    public enum MissionStartKind
    {
        /// <summary>A new gameplay mission.</summary>
        NewGame,
        /// <summary>A gameplay save, including multiplayer restoration.</summary>
        LoadedSave,
        /// <summary>A blank interactive editor map.</summary>
        EditorCreated,
        /// <summary>An existing interactive editor map.</summary>
        EditorLoaded
    }

    /// <summary>Lifecycle publication type.</summary>
    public enum MissionLifecycleKind
    {
        /// <summary>An explicitly timed initialization checkpoint.</summary>
        Initialization,
        /// <summary>Successful managed initialization has completed.</summary>
        Start,
        /// <summary>The attempt or ready session has ended.</summary>
        End
    }

    /// <summary>Checkpoints are not interchangeable; some routes omit native start checkpoints.</summary>
    public enum MissionInitializationPhase
    {
        /// <summary>No initialization checkpoint.</summary>
        None,
        /// <summary>Immediately before the native map creation/load wrapper; managed setup may already have run.</summary>
        BeforeLoad,
        /// <summary>Immediately before the native new-game initializer; also occurs for MP saves.</summary>
        BeforeNativeStart,
        /// <summary>Immediately after a successful native initializer, before its enclosing loader returns.</summary>
        AfterNativeStart,
        /// <summary>Native loading and synchronous archive callbacks have returned successfully.</summary>
        NativeLoaded,
        /// <summary>The complete managed interactive operation has returned successfully.</summary>
        ManagedReady
    }

    /// <summary>Logical retirement; it does not promise physical native memory disposal.</summary>
    public enum MissionEndReason
    {
        /// <summary>Not an end publication.</summary>
        None,
        /// <summary>A replacement operation began.</summary>
        Replaced,
        /// <summary>Vanilla or the extender invalidated the active map.</summary>
        Unloaded,
        /// <summary>The actual gameplay scene was left.</summary>
        SceneChanged,
        /// <summary>Initialization returned without success.</summary>
        Failed,
        /// <summary>Vanilla initialization threw an exception.</summary>
        Exception,
        /// <summary>Orderly application exit was requested.</summary>
        ApplicationExit
    }

    /// <summary>Immutable mission evidence. Missing data is null, never borrowed from a previous session.</summary>
    public sealed class MissionContext
    {
        internal MissionContext(long id, MissionStartKind kind, GameModeSnapshot mode, string path,
            string name, bool restart, int? size = null, int? difficulty = null, int? player = null,
            int? missionIndex = null, bool? host = null, MissionMapType mapType = MissionMapType.Unknown,
            bool? multiplayerMap = null, bool siegeThatEditor = false)
        {
            SessionId = id; StartKind = kind; Mode = mode; FilePath = path; MapName = name;
            IsRestart = restart; MapSize = size; Difficulty = difficulty; LocalPlayerId = player;
            MissionIndex = missionIndex; IsHost = host;
            MapType = mapType; IsMultiplayerMap = multiplayerMap; IsSiegeThatEditor = siegeThatEditor;
        }
        /// <summary>Monotonically increasing process-local attempt identity, retained on success.</summary>
        public long SessionId { get; }
        /// <summary>New game, restored save, or interactive editor operation.</summary>
        public MissionStartKind StartKind { get; }
        /// <summary>Shared authoritative mode evidence and customization provenance.</summary>
        public GameModeSnapshot Mode { get; }
        /// <summary>Source file, when provided by the operation.</summary>
        public string FilePath { get; }
        /// <summary>Display name, when provided by the operation.</summary>
        public string MapName { get; }
        /// <summary>True only when an explicit restart entry point was observed.</summary>
        public bool IsRestart { get; }
        /// <summary>Square map size in tiles, if known.</summary>
        public int? MapSize { get; }
        /// <summary>Vanilla difficulty value, if known.</summary>
        public int? Difficulty { get; }
        /// <summary>One-based local player ID, if assigned.</summary>
        public int? LocalPlayerId { get; }
        /// <summary>Zero-based mission index within a campaign or trail, if known.</summary>
        public int? MissionIndex { get; }
        /// <summary>Host role for real multiplayer; null for local or unresolved contexts.</summary>
        public bool? IsHost { get; }
        /// <summary>Map objective subtype; distinct from the mission's provenance.</summary>
        public MissionMapType MapType { get; }
        /// <summary>Whether the map supports multiplayer; this does not indicate connected peers.</summary>
        public bool? IsMultiplayerMap { get; }
        /// <summary>Whether the editor was explicitly entered in Siege That creation mode.</summary>
        public bool IsSiegeThatEditor { get; }
        /// <summary>Whether this is either editor operation.</summary>
        public bool IsEditor => StartKind == MissionStartKind.EditorCreated || StartKind == MissionStartKind.EditorLoaded;
        /// <summary>Whether gameplay state is being restored, including multiplayer saves.</summary>
        public bool IsSave => StartKind == MissionStartKind.LoadedSave;
        internal MissionContext With(long id, GameModeSnapshot mode) => new MissionContext(id, StartKind,
            mode, FilePath, MapName, IsRestart, MapSize, Difficulty, LocalPlayerId, MissionIndex, IsHost,
            MapType, IsMultiplayerMap, IsSiegeThatEditor);
    }

    /// <summary>Immutable publication. End carries the last context captured before retirement.</summary>
    public sealed class MissionLifecycleNotification
    {
        internal MissionLifecycleNotification(MissionContext context, MissionLifecycleKind kind,
            MissionInitializationPhase phase, MissionEndReason reason, bool started, bool replay = false)
        { Context = context; Kind = kind; Phase = phase; EndReason = reason; HasStarted = started; IsReplay = replay; }
        /// <summary>Context for this attempt or session.</summary>
        public MissionContext Context { get; }
        /// <summary>Initialization, start or end.</summary>
        public MissionLifecycleKind Kind { get; }
        /// <summary>Current or last reached checkpoint.</summary>
        public MissionInitializationPhase Phase { get; }
        /// <summary>Reason for end, otherwise None.</summary>
        public MissionEndReason EndReason { get; }
        /// <summary>True if this session reached OnStart.</summary>
        public bool HasStarted { get; }
        /// <summary>True only for a late observer's ready-session replay.</summary>
        public bool IsReplay { get; }
        /// <summary>Whether this checkpoint occurs before loading or native initialization.</summary>
        public bool IsBeforeInitialization => Kind == MissionLifecycleKind.Initialization &&
            (Phase == MissionInitializationPhase.BeforeLoad || Phase == MissionInitializationPhase.BeforeNativeStart);
        /// <summary>Returns a replay marker only for an already successful start publication.</summary>
        public MissionLifecycleNotification AsReplay()
        {
            if (Kind != MissionLifecycleKind.Start) throw new InvalidOperationException("Only ready sessions can replay.");
            return new MissionLifecycleNotification(Context, Kind, Phase, EndReason, HasStarted, true);
        }
    }

    /// <summary>Owner-bound, process-lifetime lifecycle access. Register on the Unity thread.</summary>
    public interface IMissionLifecycleCapability
    {
        /// <summary>The active ready session, or null while loading/outside a session.</summary>
        MissionContext Current { get; }
        /// <summary>Register callbacks; replay applies only to OnStart, never initialization mutations.</summary>
        bool TryRegisterObserver(string registrationId, Action<MissionLifecycleNotification> onStart,
            Action<MissionLifecycleNotification> onEnd, Action<MissionLifecycleNotification> onInitialization,
            out NativeCapabilityDiagnostic diagnostic);
    }
}
