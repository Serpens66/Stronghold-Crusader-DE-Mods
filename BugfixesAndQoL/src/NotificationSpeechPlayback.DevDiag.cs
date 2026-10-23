#if SERP_DEVDIAG
namespace BugfixesAndQoL
{
    internal sealed partial class NotificationSpeechPlayback
    {
        partial void DiagSpeech(MyAudioManager audio, long generation, string stage, string detail)
        {
            NotificationLastFrameFeature.ObserveSpeech(audio, generation, stage, detail);
        }
    }
}
#endif
