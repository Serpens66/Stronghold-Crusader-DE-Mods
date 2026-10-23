using BepInEx.Logging;
using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    // Carries one feature's Script Extender archive record from the native load to the
    // gameplay session that restores it.
    // - Multiplayer saves load through OnLoadMap, whose LoadContext reports IsSaveFile=false
    //   (knowledge/SAVEGAME_MODSETTINGS_LOAD.md); the started session's IsSave decides instead.
    // - The Extender reads the record in both Pre and Post of the native load; the last decode
    //   wins. Every root load clears the slot at BeforeLoad and every mission End clears it,
    //   so a record never reaches another session.
    // - The lifecycle reports a loaded session before the first simulation tick (local loads
    //   return synchronously; multiplayer simulation waits for packet 4), so every peer applies
    //   its own save copy at the same simulation state.
    // - Each in-scope gameplay save writes a record, even an empty one: the Extender keeps the
    //   loaded archive entry when a handler returns null, which would resurrect stale state.
    // The Extender handler and the lifecycle subscriptions are process-lifetime registrations.
    internal sealed class SessionSaveRecordSlot<T> where T : class
    {
        private readonly object sync = new object();
        private readonly ManualLogSource log;
        private readonly string identifier;
        private readonly Func<T> capture;
        private readonly Func<T, byte[]> serialize;
        private readonly Func<byte[], T> deserialize;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>(2);
        private T pending;

        private SessionSaveRecordSlot(
            ManualLogSource log,
            string identifier,
            Func<T> capture,
            Func<T, byte[]> serialize,
            Func<byte[], T> deserialize)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
            this.serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
            this.deserialize = deserialize ?? throw new ArgumentNullException(nameof(deserialize));
        }

        internal static SessionSaveRecordSlot<T> Register(
            ManualLogSource log,
            string identifier,
            Func<T> capture,
            Func<T, byte[]> serialize,
            Func<byte[], T> deserialize)
        {
            var slot = new SessionSaveRecordSlot<T>(log, identifier, capture, serialize, deserialize);
            slot.subscriptions.Add(Shared.MissionEvents.Initialization
                .Where(args => args.Phase == APIShared.MissionInitializationPhase.BeforeLoad)
                .Subscribe(_ => slot.Clear()));
            slot.subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(_ => slot.Clear()));
            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(identifier, slot.Save, slot.Load))
            {
                // Not yet published: roll back this candidate's lifecycle subscriptions.
                foreach (IDisposable subscription in slot.subscriptions)
                    subscription.Dispose();
                throw new InvalidOperationException($"Save-data handler '{identifier}' could not be registered.");
            }
            return slot;
        }

        // Returns the record restored for this session. Any other start discards it.
        internal bool TryTake(Shared.GameplaySessionStartedContext context, out T state)
        {
            lock (sync)
            {
                state = pending;
                pending = null;
            }

            if (context == null || !context.IsLoadedSave || context.IsEditor || context.IsReplay)
                state = null;
            return state != null;
        }

        private void Clear()
        {
            lock (sync)
                pending = null;
        }

        private byte[] Save(SaveContext context)
        {
            if (context == null || !context.IsSaveFile || context.IsMapEditorSave)
                return null;

            try
            {
                T state = capture();
                if (state != null)
                    return serialize(state);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/SessionSaveRecordSlot.cs:" + identifier + ":save",
                    $"Save data '{identifier}' could not be captured; this save restores the feature's default state: {ex}");
            }

            // A MessagePack nil is rejected by every reader, so the archive cannot keep an
            // older entry that no longer matches the saved game.
            return new byte[] { MessagePackCode.Nil };
        }

        private void Load(byte[] bytes, LoadContext context)
        {
            T state = null;
            try
            {
                state = deserialize(bytes);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarningLimited(log, "BugfixesAndQoL/src/SessionSaveRecordSlot.cs:" + identifier + ":load",
                    $"Save data '{identifier}' was rejected; the loaded game starts this feature from its default state: {ex.Message}");
            }

            lock (sync)
                pending = state;
        }
    }
}
