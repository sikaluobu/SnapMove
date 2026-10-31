using System;
using System.Runtime.InteropServices;

namespace SnapMove
{
    internal static class Native
    {
        public const int WM_HOTKEY = 0x0312;

        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;

        public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        /// <summary>Window title of the hidden window that receives capture requests.</summary>
        public static readonly string HostWindowTitle = "SnapMove_HotkeyHost";

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern IntPtr GetThreadDpiAwarenessContext();

        [DllImport("user32.dll")]
        private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);

        /// <summary>0 = unaware, 1 = system aware, 2 = per-monitor aware.</summary>
        public static int GetDpiAwareness()
        {
            try { return GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()); }
            catch { return -1; }
        }

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr handle);

        private static int captureMessage;

        /// <summary>Broadcast message used to bring up the capture overlay in an already running instance.</summary>
        public static int GetCaptureMessage()
        {
            if (captureMessage == 0)
            {
                try { captureMessage = (int)RegisterWindowMessage("SnapMove_ShowCapture"); }
                catch { captureMessage = 0; }
            }
            return captureMessage;
        }

        /// <summary>
        /// Ask the already running instance to start a capture. A direct post to its hidden
        /// hotkey window is reliable; HWND_BROADCAST is only a fallback (it can be dropped).
        /// </summary>
        public static void NotifyExistingInstance()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                IntPtr host = FindWindow(null, HostWindowTitle);
                if (host != IntPtr.Zero)
                {
                    PostMessage(host, GetCaptureMessage(), IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                System.Threading.Thread.Sleep(100);
            }
            PostMessage(HWND_BROADCAST, GetCaptureMessage(), IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>Prefer per-monitor DPI v2 so screen coordinates stay in physical pixels.</summary>
        public static void EnableDpiAwareness()
        {
            try
            {
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
            }
            catch { }

            try
            {
                if (SetProcessDpiAwareness(2) == 0) return;
            }
            catch { }

            try
            {
                if (SetProcessDpiAwareness(1) == 0) return;
            }
            catch { }

            try { SetProcessDPIAware(); }
            catch { }
        }
    }
}
