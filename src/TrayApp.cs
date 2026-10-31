using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace SnapMove
{
    /// <summary>Tray icon application that hides in the notification area and runs captures.</summary>
    internal sealed class TrayApp : ApplicationContext
    {
        private readonly Icon appIcon;
        private readonly NotifyIcon tray;
        private readonly HotkeyHost hotkeyHost;
        private readonly List<PinnedWindow> pinned = new List<PinnedWindow>();
        private readonly ToolStripMenuItem captureItem;
        private readonly ToolStripMenuItem autoStartItem;

        private string hotkeyText = "";
        private bool capturing;
        private bool disposed;

        public TrayApp()
        {
            hotkeyHost = new HotkeyHost();
            hotkeyHost.HotkeyPressed += delegate { BeginCapture(); };
            hotkeyHost.ShownRequested += delegate { BeginCapture(); };

            captureItem = new ToolStripMenuItem("截图");
            captureItem.Click += delegate { BeginCapture(); };

            autoStartItem = new ToolStripMenuItem("开机自动启动");
            autoStartItem.Click += delegate { ToggleAutoStart(); };

            ToolStripMenuItem openFolder = new ToolStripMenuItem("打开截图保存文件夹");
            openFolder.Click += delegate { OpenSaveFolder(); };

            ToolStripMenuItem help = new ToolStripMenuItem("使用说明");
            help.Click += delegate { ShowHelp(); };

            ToolStripMenuItem exit = new ToolStripMenuItem("退出 SnapMove");
            exit.Click += delegate { ExitApp(); };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Microsoft YaHei UI", 9f);
            menu.ShowImageMargin = false;
            menu.Items.Add(captureItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(autoStartItem);
            menu.Items.Add(openFolder);
            menu.Items.Add(help);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);

            appIcon = IconFactory.CreateIcon();
            tray = new NotifyIcon();
            tray.Icon = appIcon;
            tray.Text = "SnapMove 截图";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.DoubleClick += delegate { BeginCapture(); };

            RegisterHotkey();
            UpdateAutoStartChecked();
            Log.Info("Tray app started");
        }

        internal CaptureOverlay CurrentOverlay { get; private set; }

        internal IList<PinnedWindow> PinnedWindows
        {
            get { return pinned; }
        }

        internal void BeginCapture()
        {
            if (capturing || disposed)
                return;
            capturing = true;
            Log.Info("BeginCapture");
            try
            {
                Rectangle bounds;
                Bitmap screenshot = ScreenCapture.CaptureVirtualScreen(out bounds);
                if (screenshot == null)
                {
                    capturing = false;
                    Toast.ShowToast("截图失败：无法读取屏幕内容");
                    return;
                }
                Log.Info("Screenshot " + screenshot.Width + "x" + screenshot.Height + " bounds=" + bounds);

                CaptureOverlay overlay = new CaptureOverlay(screenshot, bounds);
                CurrentOverlay = overlay;
                overlay.Done += OnOverlayDone;
                overlay.Cancelled += OnOverlayCancelled;
                overlay.Show();
                overlay.Activate();
            }
            catch (Exception ex)
            {
                capturing = false;
                CurrentOverlay = null;
                Log.Info("BeginCapture failed: " + ex);
                Toast.ShowToast("截图失败：" + ex.Message);
            }
        }

        private void OnOverlayDone(object sender, CaptureResult result)
        {
            CaptureOverlay overlay = (CaptureOverlay)sender;
            CurrentOverlay = null;
            SafeClose(overlay);

            try
            {
                PinnedWindow window = new PinnedWindow(result.Image, result.ScreenLocation);
                window.PinnedClosed += OnPinnedClosed;
                pinned.Add(window);
                window.Show();
                window.Activate();
                window.BringToFront();
                window.FlashHighlight();
                window.CopyToClipboard();
                Log.Info("Captured " + result.Image.Width + "x" + result.Image.Height +
                         " at " + result.ScreenLocation.X + "," + result.ScreenLocation.Y);
                Toast.ShowToast("已截图 " + result.Image.Width + " × " + result.Image.Height + "  ·  拖动图片可移动，Esc 关闭");
            }
            catch (Exception ex)
            {
                Log.Info("Pinned window failed: " + ex);
                Toast.ShowToast("显示截图失败：" + ex.Message);
                try { result.Image.Dispose(); }
                catch { }
            }
            finally
            {
                capturing = false;
            }
        }

        private void OnOverlayCancelled(object sender, EventArgs e)
        {
            CaptureOverlay overlay = (CaptureOverlay)sender;
            CurrentOverlay = null;
            SafeClose(overlay);
            capturing = false;
            Log.Info("Capture cancelled");
        }

        private void OnPinnedClosed(object sender, EventArgs e)
        {
            PinnedWindow window = sender as PinnedWindow;
            if (window != null)
                pinned.Remove(window);
        }

        private static void SafeClose(Form form)
        {
            try { form.Close(); }
            catch { }
            try { form.Dispose(); }
            catch { }
        }

        private void RegisterHotkey()
        {
            HotkeyCandidate[] candidates = new HotkeyCandidate[]
            {
                new HotkeyCandidate(Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.A, "Ctrl+Alt+A"),
                new HotkeyCandidate(Native.MOD_CONTROL | Native.MOD_SHIFT, (uint)Keys.A, "Ctrl+Shift+A"),
                new HotkeyCandidate(Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.S, "Ctrl+Alt+S"),
                new HotkeyCandidate(Native.MOD_CONTROL | Native.MOD_SHIFT, (uint)Keys.S, "Ctrl+Shift+S"),
                new HotkeyCandidate(Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.X, "Ctrl+Alt+X")
            };

            foreach (HotkeyCandidate candidate in candidates)
            {
                if (hotkeyHost.Register(candidate.Modifiers, candidate.Key))
                {
                    hotkeyText = candidate.Text;
                    captureItem.Text = "截图  (" + hotkeyText + ")";
                    tray.Text = "SnapMove 截图  " + hotkeyText;
                    Log.Info("Hotkey registered: " + hotkeyText);
                    return;
                }
                Log.Info("Hotkey unavailable: " + candidate.Text + " error=" + System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            }

            hotkeyText = "";
            captureItem.Text = "截图  (热键被占用)";
            tray.Text = "SnapMove 截图（无可用热键）";
            Log.Info("No hotkey available");
            Toast.ShowToast("截图热键都被占用了，请从托盘菜单点“截图”");
        }

        private void ToggleAutoStart()
        {
            bool enable = !AutoStart.IsEnabled();
            AutoStart.SetEnabled(enable);
            UpdateAutoStartChecked();
            Toast.ShowToast(enable ? "已开启开机自动启动" : "已关闭开机自动启动");
        }

        private void UpdateAutoStartChecked()
        {
            autoStartItem.Checked = AutoStart.IsEnabled();
        }

        private void OpenSaveFolder()
        {
            try
            {
                Process.Start("explorer.exe", "\"" + Utils.EnsureSaveDir() + "\"");
            }
            catch (Exception ex)
            {
                Log.Info("Open folder failed: " + ex.Message);
            }
        }

        private void ShowHelp()
        {
            string hotkey = hotkeyText.Length > 0 ? hotkeyText : "（未注册，可从托盘菜单截图）";
            string text =
                "SnapMove 截图\n\n" +
                "截图：" + hotkey + "，或双击托盘图标，或右键托盘图标选“截图”。\n" +
                "框选：按住鼠标左键拖动；右键或 Esc 取消。\n" +
                "贴图：截图会立刻变成一张无边框图片，按住左键即可拖动到任意位置。\n" +
                "贴图快捷键：Ctrl+C 复制、Ctrl+S 另存为、Ctrl+滚轮缩放、Esc 关闭。\n" +
                "双击贴图图片 = 复制到剪贴板。\n\n" +
                "截图默认保存目录（右键贴图可保存到这里）：" + Utils.SaveDir + "\n" +
                "截图后会自动复制到剪贴板。";
            MessageBox.Show(text, "SnapMove 使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExitApp()
        {
            Dispose();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true;
                Log.Info("Tray app shutting down");
                hotkeyHost.Unregister();

                PinnedWindow[] windows = pinned.ToArray();
                pinned.Clear();
                foreach (PinnedWindow window in windows)
                {
                    try { window.Close(); }
                    catch { }
                }

                hotkeyHost.Dispose();
                if (tray != null)
                {
                    tray.Visible = false;
                    tray.Dispose();
                }
                if (appIcon != null)
                    appIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        private struct HotkeyCandidate
        {
            public readonly uint Modifiers;
            public readonly uint Key;
            public readonly string Text;

            public HotkeyCandidate(uint modifiers, uint key, string text)
            {
                Modifiers = modifiers;
                Key = key;
                Text = text;
            }
        }
    }
}
