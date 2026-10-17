using System;
using System.Collections.Generic;
using SHCDESE.Interop;

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

        internal static bool AllowsVoice(eChimps voice)
        {
            // DefaultInSync exclusions in 195E30; Archer_debug is deliberately allowed.
            return voice != eChimps.CHIMP_TYPE_ARCHER && voice != eChimps.CHIMP_TYPE_XBOWMAN &&
                voice != eChimps.CHIMP_TYPE_CATAPULT && voice != eChimps.CHIMP_TYPE_TREBUCHET &&
                voice != eChimps.CHIMP_TYPE_MANGONEL && voice != eChimps.CHIMP_TYPE_BALLISTA;
        }
    }

    internal sealed class FormationSpeechQueue
    {
        private readonly Queue<FormationSpeechRequest> pending = new Queue<FormationSpeechRequest>();
        internal void Clear() => pending.Clear();
        internal void Enqueue(FormationSpeechRequest request)
        {
            if (request != null) pending.Enqueue(request);
        }

        internal void OnNativeFrame(bool activeRun, Func<FormationSpeechRequest, bool> valid,
            Action<FormationSpeechRequest> emit, Action<Exception> failed)
        {
            if (!activeRun) return;
            // Consume before validation/call: suppression and exceptions must never retry a voice.
            int count = pending.Count;
            while (count-- > 0 && pending.Count != 0)
            {
                FormationSpeechRequest request = pending.Dequeue();
                try { if (valid(request)) emit(request); }
                catch (Exception exception) { failed(exception); }
            }
        }
    }
}
