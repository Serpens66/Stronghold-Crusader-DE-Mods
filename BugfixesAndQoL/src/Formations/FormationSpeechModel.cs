using System;
using System.Diagnostics;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.UnitCommands
{
    // Captured before dispatch: delayed MP execution must not choose a different voice or distance.
    internal sealed class FormationSpeechRequest
    {
        internal readonly int PlayerId, TribeId, MapEpoch, TargetTileId, Response;
        internal readonly uint TribeGlobalId;
        internal readonly eChimps VoiceType;

        internal FormationSpeechRequest(int player, int tribe, uint globalId, int epoch,
            eChimps voice, int leaderX, int leaderY, int targetX, int targetY, int targetTileId)
        {
            PlayerId = player; TribeId = tribe; TribeGlobalId = globalId; MapEpoch = epoch;
            VoiceType = voice;
            // Native 195E30 uses inclusive axis bounds, not a radial distance.
            Response = Math.Abs((long)leaderX - targetX) <= 72 &&
                Math.Abs((long)leaderY - targetY) <= 72 ? 6 : 10;
            TargetTileId = Response == 6 ? targetTileId : 0;
        }

        internal static bool AllowsVoice(eChimps voice, TribeMoveType moveType)
        {
            // 195E30: only mode zero excludes these mobile voices; debug archer remains allowed.
            return !(moveType == TribeMoveType.NoChange &&
                    (voice == eChimps.CHIMP_TYPE_ARCHER || voice == eChimps.CHIMP_TYPE_XBOWMAN ||
                     voice == eChimps.CHIMP_TYPE_CATAPULT)) &&
                voice != eChimps.CHIMP_TYPE_TREBUCHET && voice != eChimps.CHIMP_TYPE_MANGONEL &&
                voice != eChimps.CHIMP_TYPE_BALLISTA;
        }
    }

    internal sealed class FormationSpeechQueue
    {
        private volatile FormationSpeechRequest pending;
        private long capturedAt;
        private readonly Func<long> clock;
        private readonly long lifetime;
        internal FormationSpeechQueue(Func<long> clock = null, long frequency = 0)
        {
            this.clock = clock ?? Stopwatch.GetTimestamp;
            lifetime = frequency > 0 ? frequency : Stopwatch.Frequency;
        }
        internal bool HasPending => pending != null;
        internal void Clear() => pending = null;
        internal void Enqueue(FormationSpeechRequest request)
        {
            // Latest accepted gesture wins, including an ineligible (null) response.
            pending = request;
            if (request != null) capturedAt = clock();
        }

        internal void OnNativeFrame(bool activeRun, Func<FormationSpeechRequest, bool> valid,
            Action<FormationSpeechRequest> emit, Action<Exception> failed)
        {
            if (!activeRun) return;
            FormationSpeechRequest request = pending;
            pending = null; // Consume before any callback: failures and Busy never retry.
            if (request == null || clock() - capturedAt >= lifetime) return;
            try { if (valid(request)) emit(request); }
            catch (Exception exception) { failed(exception); }
        }
    }
}
