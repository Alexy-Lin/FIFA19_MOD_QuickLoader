using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace FIFA19QuickLoader
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using (var mutex = new Mutex(true, "FIFA19QuickLoader_SingleInstance", out var first))
            {
                if (!first)
                {
                    MessageBox.Show("FIFA 19 MOD 快速加载器已经打开了。", "FIFA 19 MOD 快速加载器");
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => ReportCrash(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash(e.ExceptionObject as Exception);
                Application.Run(new MainForm(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')));
            }
        }

        private static void ReportCrash(Exception ex)
        {
            var log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            try { File.AppendAllText(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n"); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            MessageBox.Show("发生了意外错误：\r\n" + ex?.Message + "\r\n\r\n详细信息已写入 " + log, "FIFA 19 MOD 快速加载器",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
