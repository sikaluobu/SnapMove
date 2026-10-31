using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SnapMove
{
    /// <summary>
    /// Borderless, always-on-top window that shows a captured picture at 1:1.
    /// Drag anywhere on the picture to move it.
    /// </summary>
    internal sealed class PinnedWindow : Form
    {
        private readonly Bitmap image;
        private float zoom = 1f;
        private bool dragging;
        private Point grabOffset;
        private bool imageDisposed;
        private readonly System.Windows.Forms.Timer highlightTimer;
        private bool highlight;

        public event EventHandler PinnedClosed;

        public PinnedWindow(Bitmap picture, Point screenLocation)
        {
            if (picture == null) throw new ArgumentNullException("picture");
            image = picture;

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Black;
            DoubleBuffered = true;
            KeyPreview = true;
            Text = "SnapMove_Pin";
            ClientSize = new Size(image.Width, image.Height);
            Location = screenLocation;
            Cursor = Cursors.SizeAll;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            highlightTimer = new System.Windows.Forms.Timer();
            highlightTimer.Interval = 1800;
            highlightTimer.Tick += delegate
            {
                highlight = false;
                highlightTimer.Stop();
                Invalidate();
            };

            ContextMenuStrip = BuildMenu();
            Log.Info("Pin created at " + screenLocation + " image=" + image.Size + " client=" + ClientSize);
        }

        public Size ImageSize
        {
            get { return image.Size; }
        }

        public float Zoom
        {
            get { return zoom; }
        }

        public void CopyToClipboard()
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Clipboard.SetImage(image);
                    return;
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    Thread.Sleep(60);
                }
            }
        }

        public void SaveAs()
        {
            bool wasTopMost = TopMost;
            TopMost = false;
            try
            {
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Title = "保存截图";
                    dialog.Filter = "PNG 图片 (*.png)|*.png|JPEG 图片 (*.jpg)|*.jpg|BMP 图片 (*.bmp)|*.bmp";
                    dialog.FileName = Utils.NextFileName();
                    dialog.InitialDirectory = Utils.EnsureSaveDir();
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    try
                    {
                        SaveImage(dialog.FileName);
                        Toast.ShowToast("已保存：" + dialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "保存失败：" + ex.Message, "SnapMove", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            finally
            {
                TopMost = wasTopMost;
            }
        }

        public void SaveToDefaultFolder()
        {
            try
            {
                string path = Utils.NextShotPath();
                SaveImage(path);
                Toast.ShowToast("已保存：" + path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败：" + ex.Message, "SnapMove", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ApplyZoom(float value)
        {
            value = Math.Max(0.1f, Math.Min(4f, value));
            if (Math.Abs(value - zoom) < 0.0005f)
                return;
            zoom = value;

            int width = Math.Max(1, (int)Math.Round(image.Width * zoom));
            int height = Math.Max(1, (int)Math.Round(image.Height * zoom));
            Point center = new Point(Left + Width / 2, Top + Height / 2);
            ClientSize = new Size(width, height);
            Location = new Point(center.X - Width / 2, center.Y - Height / 2);
            Invalidate();
        }

        /// <summary>Briefly draws an accent frame so a freshly pinned picture is easy to spot.</summary>
        public void FlashHighlight()
        {
            highlight = true;
            highlightTimer.Stop();
            highlightTimer.Start();
            Invalidate();
        }

        internal bool HandleShortcutForTest(Keys keyData)
        {
            return HandleShortcut(keyData);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                cp.ExStyle |= 0x00000080;    // WS_EX_TOOLWINDOW: hides from Alt+Tab
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (zoom == 1f)
            {
                e.Graphics.DrawImageUnscaled(image, 0, 0);
            }
            else
            {
                e.Graphics.InterpolationMode = zoom < 1f ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                e.Graphics.DrawImage(image, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
            }

            if (highlight)
            {
                using (Pen pen = new Pen(Ui.Accent, 2f))
                {
                    int w = Math.Max(1, ClientSize.Width - 3);
                    int h = Math.Max(1, ClientSize.Height - 3);
                    e.Graphics.DrawRectangle(pen, 1, 1, w, h);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                grabOffset = e.Location;
                Capture = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
            {
                Point screen = PointToScreen(e.Location);
                Location = new Point(screen.X - grabOffset.X, screen.Y - grabOffset.Y);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (dragging)
            {
                dragging = false;
                Capture = false;
            }
            base.OnMouseUp(e);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            CopyToClipboard();
            base.OnDoubleClick(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                ApplyZoom(zoom + (e.Delta > 0 ? 0.1f : -0.1f));
                return;
            }
            base.OnMouseWheel(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (HandleShortcut(keyData))
                return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private bool HandleShortcut(Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            if (keyData == (Keys.Control | Keys.C))
            {
                CopyToClipboard();
                return true;
            }
            if (keyData == (Keys.Control | Keys.S))
            {
                SaveAs();
                return true;
            }
            return false;
        }

        private void SaveImage(string path)
        {
            ImageFormat format = ImageFormat.Png;
            string extension = Path.GetExtension(path);
            if (extension != null)
            {
                string lower = extension.ToLowerInvariant();
                if (lower == ".jpg" || lower == ".jpeg") format = ImageFormat.Jpeg;
                else if (lower == ".bmp") format = ImageFormat.Bmp;
            }
            image.Save(path, format);
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Microsoft YaHei UI", 9f);
            menu.ShowImageMargin = false;

            ToolStripMenuItem copy = new ToolStripMenuItem("复制到剪贴板  (Ctrl+C)");
            copy.Click += delegate { CopyToClipboard(); };

            ToolStripMenuItem saveAs = new ToolStripMenuItem("另存为...  (Ctrl+S)");
            saveAs.Click += delegate { SaveAs(); };

            ToolStripMenuItem quickSave = new ToolStripMenuItem("保存到截图文件夹");
            quickSave.Click += delegate { SaveToDefaultFolder(); };

            ToolStripMenuItem actualSize = new ToolStripMenuItem("原始大小");
            actualSize.Click += delegate { ApplyZoom(1f); };

            ToolStripMenuItem zoomIn = new ToolStripMenuItem("放大 10%  (Ctrl+滚轮)");
            zoomIn.Click += delegate { ApplyZoom(zoom + 0.1f); };

            ToolStripMenuItem zoomOut = new ToolStripMenuItem("缩小 10%  (Ctrl+滚轮)");
            zoomOut.Click += delegate { ApplyZoom(zoom - 0.1f); };

            ToolStripMenuItem onTop = new ToolStripMenuItem("始终置顶");
            onTop.CheckOnClick = true;
            onTop.Checked = TopMost;
            onTop.CheckedChanged += delegate { TopMost = onTop.Checked; };

            ToolStripMenuItem close = new ToolStripMenuItem("关闭  (Esc)");
            close.Click += delegate { Close(); };

            menu.Items.Add(copy);
            menu.Items.Add(saveAs);
            menu.Items.Add(quickSave);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(actualSize);
            menu.Items.Add(zoomIn);
            menu.Items.Add(zoomOut);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(onTop);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(close);
            return menu;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (ContextMenuStrip != null)
            {
                ContextMenuStrip.Dispose();
                ContextMenuStrip = null;
            }
            if (PinnedClosed != null)
                PinnedClosed(this, EventArgs.Empty);
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !imageDisposed)
            {
                imageDisposed = true;
                highlightTimer.Stop();
                highlightTimer.Dispose();
                image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
