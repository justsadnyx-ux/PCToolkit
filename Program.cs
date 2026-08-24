namespace PcToolkit
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows.Forms;

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length >= 3 && args[0].Equals("--apply-update", StringComparison.OrdinalIgnoreCase))
            {
                RunApplyUpdate(args[1], args[2]);
                return;
            }

            if (args.Any(a =>
                    a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("-uninstall", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                Installer.RunUninstall();
                return;
            }

            ApplicationConfiguration.Initialize();

            if (!Installer.IsInstalled())
            {
                bool launchInstalled = false;
                string installedExe = "";

                using (var dialog = new InstallDialog())
                {
                    dialog.ShowDialog();
                    launchInstalled = dialog.DialogResult == DialogResult.OK && dialog.LaunchRequested;
                    installedExe = dialog.InstalledExe;
                }

                if (launchInstalled && File.Exists(installedExe))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(installedExe) { UseShellExecute = true });
                        return;
                    }
                    catch { }
                }
            }

            Application.Run(new MainForm());
        }

        private static void RunApplyUpdate(string pidArg, string targetExe)
        {
            try
            {
                if (int.TryParse(pidArg, out var oldPid))
                {
                    try
                    {
                        var old = Process.GetProcessById(oldPid);
                        if (!old.WaitForExit(20000))
                            old.Kill();
                    }
                    catch { }
                }

                string self = Environment.ProcessPath
                    ?? Process.GetCurrentProcess().MainModule?.FileName
                    ?? throw new InvalidOperationException("cannot resolve own executable path");

                for (int i = 0; ; i++)
                {
                    try
                    {
                        File.Copy(self, targetExe, overwrite: true);
                        break;
                    }
                    catch (IOException) when (i < 30)
                    {
                        Thread.Sleep(500);
                    }
                }

                Process.Start(new ProcessStartInfo(targetExe) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
