using System;
using System.IO;

namespace SnapMove
{
    internal static class Log
    {
        private static readonly object Gate = new object();

        public static string FilePath
        {
            get { return Path.Combine(Path.GetTempPath(), "SnapMove", "snapmove.log"); }
        }

        public static void Info(string message)
        {
            try
            {
                lock (Gate)
                {
                    string dir = Path.GetDirectoryName(FilePath);
                    Directory.CreateDirectory(dir);
                    FileInfo info = new FileInfo(FilePath);
                    if (info.Exists && info.Length > 1024 * 1024)
                        File.Delete(FilePath);
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine);
                }
            }
            catch
            {
            }
        }
    }
}
