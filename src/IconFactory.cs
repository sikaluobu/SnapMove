using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SnapMove
{
    internal static class IconFactory
    {
        public static Icon CreateIcon()
        {
            using (Bitmap bitmap = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    Rectangle box = new Rectangle(1, 1, 62, 62);
                    using (GraphicsPath path = Ui.RoundedRect(box, 16))
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        new Rectangle(0, 0, 64, 64),
                        Color.FromArgb(255, 96, 165, 250),
                        Color.FromArgb(255, 37, 99, 235),
                        55f))
                    {
                        g.FillPath(brush, path);
                    }

                    using (Pen pen = new Pen(Color.White, 6f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, 30, 18, 18, 18);
                        g.DrawLine(pen, 18, 18, 18, 30);
                        g.DrawLine(pen, 34, 18, 46, 18);
                        g.DrawLine(pen, 46, 18, 46, 30);
                        g.DrawLine(pen, 18, 34, 18, 46);
                        g.DrawLine(pen, 18, 46, 30, 46);
                        g.DrawLine(pen, 46, 34, 46, 46);
                        g.DrawLine(pen, 46, 46, 34, 46);
                    }
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                        return (Icon)temporary.Clone();
                }
                finally
                {
                    Native.DestroyIcon(handle);
                }
            }
        }
    }
}
