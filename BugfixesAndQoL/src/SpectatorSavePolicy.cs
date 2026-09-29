namespace BugfixesAndQoL
{
    // Keep the save decision independent of Unity so every ambiguous roster can be tested.
    internal static class SpectatorSavePolicy
    {
        internal static bool TryRecognize(short[] humans, short[] computers,
            bool realMultiplayer, int rawView, out int initialView, out string reason)
        {
            initialView = 0;
            reason = null;
            if (realMultiplayer)
            {
                reason = "network participant present";
                return false;
            }
            if (humans == null || humans.Length < 9 || computers == null || computers.Length < 9 ||
                rawView < 0 || rawView > 8)
            {
                reason = "missing roster or invalid native view";
                return false;
            }

            int firstCpu = 0;
            for (int player = 1; player <= 8; player++)
            {
                if (computers[player] != -1 && firstCpu == 0) firstCpu = player;
                if (humans[player] != -1)
                {
                    // A lone 1 at the restored view can be either a real human or Vanilla's
                    // synthetic save-view slot. Without save provenance it is ambiguous.
                    reason = player == rawView && computers[player] == -1 && humans[player] == 1
                        ? "ambiguous human or synthetic save-view slot" : "human registration present";
                    return false;
                }
            }
            if (firstCpu == 0)
            {
                reason = "no usable CPU view";
                return false;
            }
            initialView = rawView >= 1 && computers[rawView] != -1 ? rawView : firstCpu;
            return true;
        }

        internal static bool TryRecognizeMarked(short[] humans, short[] computers,
            bool realMultiplayer, int rawView, int markedView, out int initialView, out string reason)
        {
            initialView = 0;
            reason = null;
            if (realMultiplayer)
            {
                reason = "network participant present";
                return false;
            }
            if (humans == null || humans.Length < 9 || computers == null || computers.Length < 9 ||
                rawView < 0 || rawView > 8 || markedView < 1 || markedView > 8 ||
                computers[markedView] == -1)
            {
                reason = "marked CPU view unavailable";
                return false;
            }
            for (int player = 1; player <= 8; player++)
            {
                if (humans[player] == -1) continue;
                // Vanilla can synthesize exactly one human at the restored native view.
                if (player != rawView || computers[player] != -1 || humans[player] != 1)
                {
                    reason = "unexpected human registration";
                    return false;
                }
            }
            initialView = markedView;
            return true;
        }
    }
}
