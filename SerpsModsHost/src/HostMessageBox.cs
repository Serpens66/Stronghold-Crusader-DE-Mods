using System;
using System.Runtime.InteropServices;

namespace SerpsModsHost
{
    // Native message box for the "APIShared required" notice, shown from Awake on the game's main
    // thread. That thread hides the system cursor (the ShowCursor counter is per thread) and confines
    // it to the game window, and an unowned box opens behind the game, so neither the box nor other
    // programs were reachable with the mouse. The box is owned by the game window (application-modal
    // only), brought to the front, and cursor visibility and confinement are released while it is
    // open and restored afterwards.
    // Same behaviour as APIShared's NativeMessageBox, kept as an own copy on purpose: the notice
    // appears exactly when APIShared is missing or too old, and consumers never compile APIShared
    // implementation sources.
    internal static class HostMessageBox
    {
        private const uint SetForeground = 0x10000; // MB_SETFOREGROUND

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int MessageBoxW(IntPtr window, string text, string caption, uint flags);
        [DllImport("user32.dll")] private static extern int ShowCursor(bool show);
        [DllImport("user32.dll")] private static extern bool ClipCursor(IntPtr rect);
        [DllImport("user32.dll")] private static extern bool ClipCursor(ref Rect rect);
        [DllImport("user32.dll")] private static extern bool GetClipCursor(out Rect rect);
        [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread, EnumWindowsProc callback, IntPtr state);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        private delegate bool EnumWindowsProc(IntPtr window, IntPtr state);
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }

        // Returns the MessageBoxW result (IDYES = 6, IDNO = 7); 0 means the box could not be shown.
        internal static int Show(string text, string caption, uint flags)
        {
            Rect clip;
            bool clipped = GetClipCursor(out clip);
            int shown = 0;
            try
            {
                ClipCursor(IntPtr.Zero);
                while (ShowCursor(true) < 0 && shown < 64) shown++;
                shown++;
                return MessageBoxW(GameWindow(), text, caption, flags | SetForeground);
            }
            finally
            {
                for (int i = 0; i < shown; i++) ShowCursor(false);
                // GetClipCursor also reports a rectangle when nothing was clipped; restoring that
                // could confine a multi-monitor player to one screen. Only a real clip returns.
                if (clipped && !(clip.Left == GetSystemMetrics(76) && clip.Top == GetSystemMetrics(77) && // SM_X/YVIRTUALSCREEN
                    clip.Right - clip.Left == GetSystemMetrics(78) && clip.Bottom - clip.Top == GetSystemMetrics(79))) // SM_CX/CYVIRTUALSCREEN
                    ClipCursor(ref clip);
            }
        }

        // Visible unowned top-level window of the calling (main) thread, i.e. the game window; zero
        // before it exists, which keeps an unowned box.
        private static IntPtr GameWindow()
        {
            IntPtr found = IntPtr.Zero;
            EnumThreadWindows(GetCurrentThreadId(), (window, _) =>
            {
                if (!IsWindowVisible(window) || GetWindow(window, 4) != IntPtr.Zero) return true; // GW_OWNER
                found = window;
                return false;
            }, IntPtr.Zero);
            return found;
        }
    }
}
