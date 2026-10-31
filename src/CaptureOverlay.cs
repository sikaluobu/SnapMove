using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace SnapMove
{
    internal sealed class CaptureResult : EventArgs
    {
        public readonly Bitmap Image;
        public readonly Point ScreenLocation;

        public CaptureResult(Bitmap image, Point screenLocation)
        {
            Image = image;
            ScreenLocation = screenLocation;
        }
    }

    /// <summary>Full-screen frozen-image overlay used to select a rectangle.</summary>
    internal sealed class CaptureOverlay : Form
    {
        private const int WM_MOUSEMOVE = 0x0200;

        private readonly Bitmap screen;
        private readonly Rectangle virtualBounds;
        private readonly Font hintFont;
        private readonly Font labelFont;

        private Point startPoint = new Point(-1, -1);
        private Point currentPoint = new Point(-1, -1);
        private bool selecting;
        private bool finished;
        private bool resourcesDisposed;

        public event EventHandler<CaptureResult> Done;
        public event EventHandler Cancelled;

        public CaptureOverlay(Bitmap screenshot, Rectangle bounds)
        {
            screen = screenshot;
            virtualBounds = bounds;

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            TopMost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            KeyPreview = true;
            Text = "SnapMove_Overlay";
            BackColor = Color.Black;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            hintFont = new Font("Microsoft YaHei UI", 10f);
            labelFont = new Font("Consolas", 10.5f);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            Activate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                CancelSelection();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (finished)
                return;

            if (e.Button == MouseButtons.Right)
            {
                CancelSelection();
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                selecting = true;
                startPoint = ClampPoint(e.Location);
                currentPoint = startPoint;
                Capture = true;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (finished)
                return;
            currentPoint = ClampPoint(e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (finished)
                return;
            if (e.Button != MouseButtons.Left || !selecting)
                return;

            selecting = false;
            Capture = false;
            currentPoint = ClampPoint(e.Location);

            Rectangle selection = Selection();
            if (selection.Width < 4 || selection.Height < 4)
            {
                CancelSelection();
                return;
            }
            Finish(selection);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (screen == null)
                return;

            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImageUnscaled(screen, 0, 0);

            Rectangle selection = Selection();
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
                g.FillRectangle(dim, ClientRectangle);

            if (selection.Width > 0 && selection.Height > 0)
            {
                g.DrawImage(screen,
                    new Rectangle(selection.X, selection.Y, selection.Width, selection.Height),
                    selection, GraphicsUnit.Pixel);
                DrawSelectionChrome(g, selection);
            }
            else
            {
                if (currentPoint.X >= 0 && currentPoint.Y >= 0)
                    DrawCrosshair(g, currentPoint);
                DrawHint(g, "按住鼠标左键拖动框选  ·  右键或 Esc 取消");
            }
        }

        private void DrawCrosshair(Graphics g, Point p)
        {
            using (Pen pen = new Pen(Color.FromArgb(190, 255, 255, 255), 1f))
            {
                pen.DashStyle = DashStyle.Dot;
                g.DrawLine(pen, 0, p.Y, ClientSize.Width, p.Y);
                g.DrawLine(pen, p.X, 0, p.X, ClientSize.Height);
            }
        }

        private void DrawSelectionChrome(Graphics g, Rectangle selection)
        {
            using (Pen shadow = new Pen(Color.FromArgb(160, 0, 0, 0), 3f))
                g.DrawRectangle(shadow, selection.X - 1, selection.Y - 1, selection.Width + 1, selection.Height + 1);
            using (Pen border = new Pen(Ui.AccentLight, 1f))
                g.DrawRectangle(border, selection.X, selection.Y, selection.Width - 1, selection.Height - 1);

            string text = selection.Width + " × " + selection.Height;
            Size textSize = TextRenderer.MeasureText(text, labelFont);
            Rectangle box = new Rectangle(selection.X, selection.Y - textSize.Height - 10, textSize.Width + 18, textSize.Height + 8);
            if (box.Y < 4) box.Y = selection.Y + 6;
            if (box.X + box.Width > ClientSize.Width - 4) box.X = ClientSize.Width - box.Width - 4;
            if (box.X < 4) box.X = 4;

            using (GraphicsPath path = Ui.RoundedRect(box, 5))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(230, 20, 22, 28)))
                g.FillPath(brush, path);
            TextRenderer.DrawText(g, text, labelFont, box, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private void DrawHint(Graphics g, string text)
        {
            Size textSize = TextRenderer.MeasureText(text, hintFont);
            int width = textSize.Width + 30;
            int height = textSize.Height + 16;
            Rectangle box = new Rectangle((ClientSize.Width - width) / 2, 28, width, height);
            using (GraphicsPath path = Ui.RoundedRect(box, 8))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(210, 20, 22, 28)))
                g.FillPath(brush, path);
            TextRenderer.DrawText(g, text, hintFont, box, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private Rectangle Selection()
        {
            if (startPoint.X < 0 || currentPoint.X < 0)
                return Rectangle.Empty;
            int x = Math.Min(startPoint.X, currentPoint.X);
            int y = Math.Min(startPoint.Y, currentPoint.Y);
            int width = Math.Abs(startPoint.X - currentPoint.X);
            int height = Math.Abs(startPoint.Y - currentPoint.Y);
            Rectangle rectangle = new Rectangle(x, y, width, height);
            return Rectangle.Intersect(rectangle, ClientRectangle);
        }

        private Point ClampPoint(Point point)
        {
            int x = point.X;
            int y = point.Y;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x > ClientSize.Width - 1) x = ClientSize.Width - 1;
            if (y > ClientSize.Height - 1) y = ClientSize.Height - 1;
            return new Point(x, y);
        }

        private void CancelSelection()
        {
            if (finished)
                return;
            finished = true;
            Hide();
            if (Cancelled != null)
                Cancelled(this, EventArgs.Empty);
        }

        private void Finish(Rectangle selection)
        {
            if (finished)
                return;
            finished = true;
            Hide();

            Bitmap result = new Bitmap(selection.Width, selection.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(screen,
                    new Rectangle(0, 0, selection.Width, selection.Height),
                    selection, GraphicsUnit.Pixel);
            }

            Point location = new Point(virtualBounds.X + selection.X, virtualBounds.Y + selection.Y);
            if (Done != null)
                Done(this, new CaptureResult(result, location));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                if (screen != null) screen.Dispose();
                if (hintFont != null) hintFont.Dispose();
                if (labelFont != null) labelFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
