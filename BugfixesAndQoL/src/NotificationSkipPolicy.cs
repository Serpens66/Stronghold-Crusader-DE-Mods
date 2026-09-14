// Feature: Decide whether a right-click belongs to an active queued minimap notification video.
namespace BugfixesAndQoL
{
    internal static class NotificationSkipPolicy
    {
        public static bool ShouldArm(
            bool enabled,
            bool queueActive,
            bool hasVideo,
            bool videoVisible,
            bool videoPlaying)
        {
            return enabled && queueActive && hasVideo && videoVisible && videoPlaying;
        }

        public static bool ShouldCompleteOnRightClick(
            bool armed,
            bool rightMouseButton,
            bool singleClick)
        {
            return armed && rightMouseButton && singleClick;
        }
    }
}
