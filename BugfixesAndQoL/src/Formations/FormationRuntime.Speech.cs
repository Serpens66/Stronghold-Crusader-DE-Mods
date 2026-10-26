using System;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class FormationRuntime
    {
        private IFormationSpeechNative speechNative;
        private object speechEngineLock;
        private readonly FormationSpeechQueue speechQueue = new FormationSpeechQueue();

        private void InitializeEngineSynchronization()
        {
            FieldInfo engineLock = typeof(EngineInterface).GetField("threadLock",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (engineLock == null || !engineLock.IsPrivate || engineLock.FieldType != typeof(object) ||
                (speechEngineLock = engineLock.GetValue(null)) == null)
                throw new InvalidOperationException("Native engine lock contract changed.");
        }

        private void InitializeSpeech()
        {
            try
            {
                if (Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.N000000C4)).ToInt32() != 0x388)
                    throw new InvalidOperationException("Engineer speech exclusion field layout changed.");
                speechNative = new FormationSpeechNative(libraryContext.ModuleHandle, libraryContext.Memory,
                    message => Shared.DebugLogHelper.LogInfo(log, message));
                markerRenderer.NativeFrameCompleted = OnSpeechNativeFrame;
            }
            catch (Exception exception) { DisableSpeech(exception); }
        }

        private FormationSpeechRequest CaptureSpeech(ActiveDrag state, TribeMoveType moveType)
        {
            if (speechNative == null) return null;
            bool taken = false;
            try
            {
                // Called on command release from EngineInterface.run, outside stateSync.
                // Acquire in Engine -> state order; input/preview never waits here.
                if (speechEngineLock == null) return null;
                Monitor.Enter(speechEngineLock, ref taken);
                if (!GameTribeManagerAPI.Instance.TryGetTribeById(state.TribeId, out GameTribe* tribe) ||
                    tribe == null || tribe->r_PlayerIdOwner != state.PlayerId || tribe->r_GlobalId == 0 ||
                    !APIShared.UnitAccess.TryGetById(tribe->r_LeaderUnitId, out GameUnit* leader, out _) ||
                    leader == null || !APIShared.UnitAccess.IsReallyAlive(leader)) return null;
                eChimps voice = (eChimps)speechNative.CaptureVoice(nativeTribeManager, state.TribeId);
                if (!FormationSpeechRequest.AllowsVoice(voice, moveType)) return null;
                int representativeId = speechNative.CaptureRepresentative();
                if (!APIShared.UnitAccess.TryGetById(representativeId, out GameUnit* selected, out _) ||
                    selected == null || !APIShared.UnitAccess.IsReallyAlive(selected) ||
                    selected->r_ControllableForPlayerId != state.PlayerId || selected->r_TribeId != state.TribeId)
                    return null;
                var logic = GameTileManagerAPI.Instance.TileManager.LogicGrid;
                uint tile = selected->r_CurrentPositionTileId;
                if (tile >= (uint)logic.Length || (logic[(int)tile] & 0x40000000) != 0 ||
                    (selected->r_UnitChimp == eChimps.CHIMP_TYPE_ENGINEER && selected->N000000C4 != 0))
                    return null;
                return new FormationSpeechRequest(state.PlayerId, state.TribeId, tribe->r_GlobalId,
                    state.MapEpoch, voice, leader->r_CurrentTilePositionX, leader->r_CurrentTilePositionY,
                    state.Target.NativeX, state.Target.NativeY,
                    GameTileManagerAPI.Instance.GetTileId(state.Target.NativeX, state.Target.NativeY));
            }
            catch (Exception exception) { DisableSpeech(exception); return null; }
            finally { if (taken) Monitor.Exit(speechEngineLock); }
        }

        private void OnSpeechNativeFrame()
        {
            if (!speechQueue.HasPending) return;
            // Render end is inside DLL_RunTick, after current audio inputs and before its audio export.
            // A no-buffer managed run never reaches this callback, so pending feedback survives it.
            // Runs every render pass; the callbacks are created once instead of per frame.
            if (speechAcceptCallback == null)
            {
                speechAcceptCallback = AcceptSpeech;
                speechEmitCallback = request => speechNative.Emit(request);
                speechDisableCallback = DisableSpeech;
            }
            lock (stateSync)
                speechQueue.OnNativeFrame(nativeFeedbackRunActive &&
                    FormationNativeRunContext.HasRender(this) &&
                    speechEngineLock != null && Monitor.IsEntered(speechEngineLock),
                    speechAcceptCallback, speechEmitCallback, speechDisableCallback);
            RefreshNativeObserverDemand();
        }

        private Func<FormationSpeechRequest, bool> speechAcceptCallback;
        private Action<FormationSpeechRequest> speechEmitCallback;
        private Action<Exception> speechDisableCallback;

        private bool AcceptSpeech(FormationSpeechRequest request) =>
            LocalInputEnabled && speechNative != null && HasValidMap() &&
            request.MapEpoch == commandRuntime.mapEpoch &&
            request.PlayerId == GamePlayerManagerAPI.Instance.GetLocalPlayerId() &&
            GameTribeManagerAPI.Instance.TryGetTribeById(request.TribeId, out GameTribe* tribe) &&
            tribe != null && tribe->r_AliveState == AliveState.IsAlive &&
            tribe->r_GlobalId == request.TribeGlobalId && tribe->r_PlayerIdOwner == request.PlayerId;

        private void QueueSpeech(FormationSpeechRequest request)
        {
            try { lock (stateSync) { speechQueue.Enqueue(request); RefreshNativeObserverDemand(); } }
            catch (Exception exception) { DisableSpeech(exception); }
        }

        private void DisableSpeech(Exception exception)
        {
            lock (stateSync) { speechNative = null; speechQueue.Clear(); RefreshNativeObserverDemand(); }
            // Isolated from the movement/marker error paths: never restore an accepted input edge.
            try { Shared.DebugLogHelper.LogErrorLimited(log, "FormationSpeech",
                "Formation speech unavailable; movement remains active: " + exception.Message); }
            catch { }
        }

        private void RefreshNativeObserverDemand()
        {
            lock (stateSync) groundFrameObserver?.SetActive(drag != null || speechQueue.HasPending);
        }
    }
}
