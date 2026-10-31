using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace SnapMove
{
    internal static class Utils
    {
        public static string SaveDir
        {
            get
            {
                string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                if (string.IsNullOrEmpty(pictures))
                    pictures = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures");
                return Path.Combine(pictures, "SnapMove");
            }
        }

        public static string EnsureSaveDir()
        {
            string dir = SaveDir;
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
            }
            return dir;
        }

        public static string NextFileName()
        {
            return "Snap_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        }

        public static string NextShotPath()
        {
            return Path.Combine(EnsureSaveDir(), NextFileName());
        }
    }
}
