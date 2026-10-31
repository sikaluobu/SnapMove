using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SnapMove
{
    /// <summary>
    /// Headless regression test: drives the real capture overlay and pinned window
    /// with posted mouse messages, then writes selftest.log next to the exe.
    /// </summary>
    internal static class SelfTest
    {
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int MK_LBUTTON = 0x0001;

        private const int SelectionLeft = 200;
        private const int SelectionTop = 150;
        private const int SelectionRight = 520;
        private const int SelectionBottom = 370;

        public static int Run()
        {
            StringBuilder log = new StringBuilder();
            log.AppendLine("SnapMove self test " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            int exitCode = 1;

            try
            {
                SelfTestContext context = new SelfTestContext(log);
                Application.Run(context);
                exitCode = context.Success ? 0 : 1;
            }
            catch (Exception ex)
            {
                log.AppendLine("EXCEPTION: " + ex);
                exitCode = 1;
            }

            log.AppendLine(exitCode == 0 ? "RESULT: PASS" : "RESULT: FAIL");
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log");
            try
            {
                File.WriteAllText(path, log.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
            return exitCode;
        }

        private sealed class SelfTestContext : ApplicationContext
        {
            private readonly TrayApp app;
            private readonly StringBuilder log;
            private readonly Timer timer;

            private int step;
            private int failures;
            private DateTime deadline;
            private Rectangle overlayBounds;
            private Point pinStart;
            private bool success;

            public SelfTestContext(StringBuilder log)
            {
                this.log = log;
                app = new TrayApp();
                timer = new Timer();
                timer.Interval = 120;
                timer.Tick += OnTick;
                timer.Start();
            }

            public bool Success
            {
                get { return success; }
            }

            private void OnTick(object sender, EventArgs e)
            {
                try
                {
                    RunStep();
                }
                catch (Exception ex)
                {
                    Fail("unexpected exception: " + ex);
                }
            }

            private void RunStep()
            {
                switch (step)
                {
                    case 0:
                        log.AppendLine("STEP 1: request capture");
                        deadline = DateTime.UtcNow.AddSeconds(10);
                        step = 1;
                        app.BeginCapture();
                        timer.Interval = 150;
                        break;

                    case 1:
                    {
                        CaptureOverlay overlay = app.CurrentOverlay;
                        if (overlay == null)
                        {
                            if (PastDeadline()) Fail("capture overlay did not appear");
                            return;
                        }
                        if (!overlay.IsHandleCreated || !overlay.Visible)
                            return;

                        overlayBounds = overlay.Bounds;
                        log.AppendLine("Overlay bounds: " + overlayBounds);
                        Check(overlayBounds.Width > 0 && overlayBounds.Height > 0, "overlay covers a screen area");

                        PostMouse(overlay.Handle, WM_LBUTTONDOWN, SelectionLeft, SelectionTop, true);
                        PostMouse(overlay.Handle, WM_MOUSEMOVE, SelectionRight, SelectionBottom, true);
                        PostMouse(overlay.Handle, WM_LBUTTONUP, SelectionRight, SelectionBottom, false);
                        deadline = DateTime.UtcNow.AddSeconds(8);
                        step = 2;
                        break;
                    }

                    case 2:
                    {
                        if (app.CurrentOverlay != null)
                        {
                            if (PastDeadline()) Fail("overlay did not close after selection");
                            return;
                        }
                        if (app.PinnedWindows.Count == 0)
                        {
                            if (PastDeadline()) Fail("pinned window did not appear");
                            return;
                        }

                        PinnedWindow pin = app.PinnedWindows[0];
                        int width = SelectionRight - SelectionLeft;
                        int height = SelectionBottom - SelectionTop;
                        Check(pin.ImageSize.Width == width && pin.ImageSize.Height == height,
                            "captured image is " + width + "x" + height + ", actual " + pin.ImageSize);
                        Point expected = new Point(overlayBounds.X + SelectionLeft, overlayBounds.Y + SelectionTop);
                        Check(pin.Location == expected, "pinned at " + expected + ", actual " + pin.Location);
                        Check(pin.ClientSize == pin.ImageSize, "pinned window shows image at 1:1");
                        Check(pin.TopMost, "pinned window is top most");
                        Check(pin.Text == "SnapMove_Pin", "pinned window title is SnapMove_Pin");

                        ClipboardCheck(pin);

                        pinStart = pin.Location;
                        PostMouse(pin.Handle, WM_LBUTTONDOWN, 10, 10, true);
                        PostMouse(pin.Handle, WM_MOUSEMOVE, 60, 80, true);
                        PostMouse(pin.Handle, WM_LBUTTONUP, 60, 80, false);
                        deadline = DateTime.UtcNow.AddSeconds(5);
                        step = 3;
                        break;
                    }

                    case 3:
                    {
                        if (app.PinnedWindows.Count == 0)
                        {
                            Fail("pinned window disappeared during drag");
                            return;
                        }

                        PinnedWindow pin = app.PinnedWindows[0];
                        Point moved = new Point(pinStart.X + 50, pinStart.Y + 70);
                        Check(pin.Location == moved, "dragging moved window to " + moved + ", actual " + pin.Location);

                        Point centerBefore = new Point(pin.Left + pin.Width / 2, pin.Top + pin.Height / 2);
                        pin.ApplyZoom(2f);
                        Point centerAfter = new Point(pin.Left + pin.Width / 2, pin.Top + pin.Height / 2);
                        Check(pin.ClientSize.Width == (SelectionRight - SelectionLeft) * 2 &&
                              pin.ClientSize.Height == (SelectionBottom - SelectionTop) * 2,
                            "zoom 200% doubles window size, actual " + pin.ClientSize);
                        Check(Math.Abs(centerAfter.X - centerBefore.X) <= 1 && Math.Abs(centerAfter.Y - centerBefore.Y) <= 1,
                            "zoom keeps the picture center in place");

                        pin.ApplyZoom(1f);
                        Check(pin.ClientSize.Width == SelectionRight - SelectionLeft &&
                              pin.ClientSize.Height == SelectionBottom - SelectionTop,
                            "zoom back to 100% restores size");

                        pin.HandleShortcutForTest(Keys.Escape);
                        Check(pin.IsDisposed, "Esc closes the pinned window");
                        Check(app.PinnedWindows.Count == 0, "closed window is removed from the list");

                        log.AppendLine("STEP 5: cancel path");
                        deadline = DateTime.UtcNow.AddSeconds(10);
                        step = 4;
                        app.BeginCapture();
                        break;
                    }

                    case 4:
                    {
                        CaptureOverlay overlay = app.CurrentOverlay;
                        if (overlay == null)
                        {
                            if (PastDeadline()) Fail("second capture overlay did not appear");
                            return;
                        }
                        if (!overlay.IsHandleCreated || !overlay.Visible)
                            return;
                        PostMouse(overlay.Handle, WM_RBUTTONDOWN, 100, 100, false);
                        deadline = DateTime.UtcNow.AddSeconds(8);
                        step = 5;
                        break;
                    }

                    case 5:
                        if (app.CurrentOverlay != null)
                        {
                            if (PastDeadline()) Fail("overlay did not close after right click");
                            return;
                        }
                        Check(app.PinnedWindows.Count == 0, "cancelling does not create a pinned window");
                        log.AppendLine("STEP 6: done, " + failures + " failure(s)");
                        success = failures == 0;
                        timer.Stop();
                        app.Dispose();
                        ExitThread();
                        break;
                }
            }

            private void ClipboardCheck(PinnedWindow pin)
            {
                try
                {
                    pin.CopyToClipboard();
                    if (!Clipboard.ContainsImage())
                    {
                        log.AppendLine("WARN: clipboard does not contain an image");
                        return;
                    }
                    using (Image clipboardImage = Clipboard.GetImage())
                    {
                        if (clipboardImage == null)
                        {
                            log.AppendLine("WARN: clipboard image is null");
                            return;
                        }
                        Check(clipboardImage.Width == pin.ImageSize.Width && clipboardImage.Height == pin.ImageSize.Height,
                            "clipboard holds the capture, actual " + clipboardImage.Width + "x" + clipboardImage.Height);
                    }
                }
                catch (Exception ex)
                {
                    log.AppendLine("WARN: clipboard check failed: " + ex.Message);
                }
            }

            private void Check(bool condition, string description)
            {
                if (condition)
                {
                    log.AppendLine("PASS " + description);
                }
                else
                {
                    failures++;
                    log.AppendLine("FAIL " + description);
                }
            }

            private void Fail(string message)
            {
                failures++;
                log.AppendLine("FAIL " + message);
                log.AppendLine("Aborting, " + failures + " failure(s)");
                success = false;
                timer.Stop();
                try { app.Dispose(); }
                catch { }
                ExitThread();
            }

            private bool PastDeadline()
            {
                return DateTime.UtcNow > deadline;
            }

            private static void PostMouse(IntPtr handle, int message, int clientX, int clientY, bool leftDown)
            {
                if (handle == IntPtr.Zero)
                    return;
                IntPtr lParam = new IntPtr((clientY << 16) | (clientX & 0xFFFF));
                IntPtr wParam = new IntPtr(leftDown ? MK_LBUTTON : 0);
                Native.PostMessage(handle, message, wParam, lParam);
            }
        }
    }
}
