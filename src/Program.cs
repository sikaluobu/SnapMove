using System;
using System.Windows.Forms;

namespace SnapMove
{
    internal static class Program
    {
        private const string MutexName = "Local\\SnapMove_SingleInstance";

        [STAThread]
        private static void Main(string[] args)
        {
            bool selfTest = false;
            foreach (string arg in args)
            {
                if (string.Equals(arg, "--selftest", StringComparison.OrdinalIgnoreCase))
                    selfTest = true;
            }

            Native.EnableDpiAwareness();
            Log.Info("DPI awareness=" + Native.GetDpiAwareness() +
                     " virtualScreen=" + System.Windows.Forms.SystemInformation.VirtualScreen);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                Log.Info("ThreadException: " + e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Log.Info("UnhandledException: " + e.ExceptionObject);
            };

            if (selfTest)
            {
                int code = SelfTest.Run();
                Environment.ExitCode = code;
                return;
            }

            bool createdNew;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Ask the already running copy to show its capture overlay.
                    Native.NotifyExistingInstance();
                    return;
                }

                try
                {
                    Application.Run(new TrayApp());
                }
                finally
                {
                    try { mutex.ReleaseMutex(); }
                    catch { }
                }
            }
        }
    }
}
