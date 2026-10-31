using System;
using System.Drawing;
using System.Windows.Forms;

namespace SnapMove
{
    /// <summary>Small non-activating notice in the lower right corner.</summary>
    internal sealed class Toast : Form
    {
        private static Toast current;

        private readonly Timer timer;
        private readonly Font textFont;
        private readonly string message;

        private Toast(string text)
        {
            message = text;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "SnapMove_Toast";
            BackColor = Ui.PanelSolid;
            Opacity = 0.94;
            Cursor = Cursors.Hand;

            textFont = new Font("Microsoft YaHei UI", 10f);
            Size textSize = TextRenderer.MeasureText(message, textFont);
            int width = Math.Max(280, textSize.Width + 48);
            int height = 56;

            Rectangle working = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(width, height);
            Location = new Point(working.Right - width - 28, working.Bottom - height - 28);

            using (System.Drawing.Drawing2D.GraphicsPath path = Ui.RoundedRect(new Rectangle(0, 0, width, height), 12))
                Region = new Region(path);

            timer = new Timer();
            timer.Interval = 4500;
            timer.Tick += delegate
            {
                timer.Stop();
                Close();
            };
            Click += delegate
            {
                timer.Stop();
                Close();
            };
        }

        public static void ShowToast(string text)
        {
            if (current != null && !current.IsDisposed)
            {
                try { current.Close(); }
                catch { }
            }
            current = new Toast(text);
            current.Show();
            current.timer.Start();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Ui.PanelSolid);
            using (SolidBrush foreground = new SolidBrush(Color.White))
            {
                RectangleF area = new RectangleF(20, 0, ClientSize.Width - 40, ClientSize.Height);
                using (StringFormat format = new StringFormat())
                {
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;
                    e.Graphics.DrawString(message, textFont, foreground, area, format);
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Dispose();
            textFont.Dispose();
            if (current == this)
                current = null;
            base.OnFormClosed(e);
        }
    }
}
