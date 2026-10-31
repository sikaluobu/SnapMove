using System.Drawing;
using System.Drawing.Drawing2D;

namespace SnapMove
{
    internal static class Ui
    {
        public static readonly Color Accent = Color.FromArgb(255, 59, 130, 246);
        public static readonly Color AccentLight = Color.FromArgb(255, 96, 165, 250);
        public static readonly Color PanelSolid = Color.FromArgb(255, 24, 26, 32);

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d > bounds.Width) d = bounds.Width;
            if (d > bounds.Height) d = bounds.Height;
            if (d <= 1)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
