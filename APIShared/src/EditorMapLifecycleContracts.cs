using System;

namespace APIShared
{
    /// <summary>Origin of a successfully initialized editor map.</summary>
    public enum EditorMapOrigin
    {
        /// <summary>A newly created map.</summary>
        Created,
        /// <summary>A map loaded from disk.</summary>
        Loaded
    }

    /// <summary>Editor session transition.</summary>
    public enum EditorMapLifecycleKind
    {
        /// <summary>Vanilla's managed editor initialization has returned successfully.</summary>
        Ready,
        /// <summary>The session is no longer usable.</summary>
        Ended
    }

    /// <summary>Reason an editor session ended.</summary>
    public enum EditorMapEndReason
    {
        /// <summary>The notification is a ready notification.</summary>
        None,
        /// <summary>Another map operation began, even if that operation subsequently fails.</summary>
        MapReplacement,
        /// <summary>Vanilla unloaded native map state outside an editor load operation.</summary>
        NativeUnload,
        /// <summary>The editor's actual game screen was left.</summary>
        SceneChanged
    }

    /// <summary>
    /// Immutable editor-session notification. Ready does not guarantee a first PlayState,
    /// rendered visuals, or any particular building. All callbacks run on the Unity thread.
    /// </summary>
    public sealed class EditorMapLifecycleNotification
    {
        internal EditorMapLifecycleNotification(long sessionId, EditorMapOrigin origin,
            string filePath, EditorMapLifecycleKind kind, EditorMapEndReason endReason, bool replay)
        {
            SessionId = sessionId;
            Origin = origin;
            FilePath = filePath;
            Kind = kind;
            EndReason = endReason;
            IsReplay = replay;
        }

        /// <summary>Process-local monotonically increasing identity; failed attempts can leave gaps.</summary>
        public long SessionId { get; }
        /// <summary>Creation or file-load origin.</summary>
        public EditorMapOrigin Origin { get; }
        /// <summary>Loaded file path, or null for a new map.</summary>
        public string FilePath { get; }
        /// <summary>Ready or ended transition.</summary>
        public EditorMapLifecycleKind Kind { get; }
        /// <summary>End reason, or None for ready.</summary>
        public EditorMapEndReason EndReason { get; }
        /// <summary>True only for delivery of the current session to a late observer.</summary>
        public bool IsReplay { get; }
    }

    /// <summary>Owner-bound access to the process-wide editor lifecycle; use on the Unity thread.</summary>
    public interface IEditorMapLifecycleCapability
    {
        /// <summary>Current ready session, or null during loading, after failure, or outside the editor.</summary>
        EditorMapLifecycleNotification Current { get; }
        /// <summary>
        /// Registers a process-lifetime observer. An active session is replayed once before return,
        /// except during a notification dispatch, when replay follows the current publication.
        /// </summary>
        bool TryRegisterObserver(string registrationId, Action<EditorMapLifecycleNotification> observer,
            out NativeCapabilityDiagnostic diagnostic);
    }
}
