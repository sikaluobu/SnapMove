using System;
using System.Windows.Forms;

namespace SnapMove
{
    /// <summary>
    /// Invisible window that owns the global hotkey and receives the broadcast
    /// fired by a second instance of SnapMove.
    /// </summary>
    internal sealed class HotkeyHost : Form
    {
        public const int HotkeyId = 0x04A1;

        private readonly int captureMessage;

        public event EventHandler HotkeyPressed;
        public event EventHandler ShownRequested;

        public HotkeyHost()
        {
            Text = Native.HostWindowTitle;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new System.Drawing.Size(0, 0);
            captureMessage = Native.GetCaptureMessage();
            // Force the window handle so the message loop has somewhere to deliver WM_HOTKEY.
            IntPtr handle = Handle;
            Log.Info("Hotkey host ready, handle=" + handle.ToString() + ", capture message=" + captureMessage.ToString());
        }

        public bool Register(uint modifiers, uint vk)
        {
            return Native.RegisterHotKey(Handle, HotkeyId, modifiers | Native.MOD_NOREPEAT, vk);
        }

        public void Unregister()
        {
            try
            {
                if (IsHandleCreated)
                    Native.UnregisterHotKey(Handle, HotkeyId);
            }
            catch
            {
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                Log.Info("Global hotkey received");
                if (HotkeyPressed != null)
                    HotkeyPressed(this, EventArgs.Empty);
                return;
            }

            if (captureMessage != 0 && m.Msg == captureMessage)
            {
                Log.Info("Capture broadcast received");
                if (ShownRequested != null)
                    ShownRequested(this, EventArgs.Empty);
                return;
            }

            base.WndProc(ref m);
        }
    }
}
