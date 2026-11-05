// Feature: Decide whether a right-click belongs to an active queued minimap notification.
namespace BugfixesAndQoL
{
    internal static class NotificationSkipPolicy
    {
        public static bool ShouldArmVideo(
            bool enabled,
            bool queueActive,
            bool hasVideo,
            bool videoVisible,
            bool videoPlaying,
            bool briefingVisible)
        {
            return enabled && queueActive && hasVideo && videoVisible && videoPlaying && !briefingVisible;
        }

        public static bool ShouldArmMinimap(
            bool enabled,
            bool queueActive,
            bool hasVideo,
            bool briefingVisible)
        {
            return enabled && queueActive && !hasVideo && !briefingVisible;
        }

        public static bool ShouldCompleteOnRightClick(
            bool armed,
            bool rightMouseButton,
            bool singleClick)
        {
            return armed && rightMouseButton && singleClick;
        }

        public static bool AudioPathMatches(string notificationAudioPath, string requestedSoundName)
        {
            if (string.IsNullOrEmpty(notificationAudioPath) || string.IsNullOrEmpty(requestedSoundName))
                return false;

            return string.Equals(
                GetPortableFileName(notificationAudioPath),
                GetPortableFileName(requestedSoundName),
                System.StringComparison.OrdinalIgnoreCase);
        }

        private static string GetPortableFileName(string path)
        {
            int separator = System.Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            return separator >= 0 ? path.Substring(separator + 1) : path;
        }
    }
}
