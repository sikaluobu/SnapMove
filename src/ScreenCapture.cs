using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace SnapMove
{
    internal static class ScreenCapture
    {
        public static Rectangle VirtualBounds()
        {
            Rectangle result = Rectangle.Empty;
            foreach (Screen screen in Screen.AllScreens)
                result = Rectangle.Union(result, screen.Bounds);
            if (result.Width <= 0 || result.Height <= 0)
                result = SystemInformation.VirtualScreen;
            return result;
        }

        /// <summary>Grab every monitor at once. Coordinates and bitmap are in physical pixels.</summary>
        public static Bitmap CaptureVirtualScreen(out Rectangle bounds)
        {
            bounds = VirtualBounds();
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }
            return bitmap;
        }
    }
}
