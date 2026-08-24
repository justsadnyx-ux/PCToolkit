namespace PcToolkit
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;
    using Microsoft.Win32;

    internal static class Installer
    {
        public const string AppName = "PC Toolkit";
        public const string Version = "1.0.0";

        private const string RegRoot = @"Software\PCToolkit";
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PCToolkit";

        public static string ExePath =>
            Process.GetCurrentProcess().MainModule?.FileName ?? Application.ExecutablePath;

        public static string DefaultInstallDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "PCToolkit");

        public static bool IsInstalled()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RegRoot);
                if (k?.GetValue("Portable") is int p && p == 1)
                    return true;

                var path = k?.GetValue("InstallPath") as string;
                if (string.IsNullOrWhiteSpace(path))
                    return false;
                return SameDir(path, AppContext.BaseDirectory);
            }
            catch
            {
                return false;
            }
        }

        public static void SetPortableFlag()
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(RegRoot);
                k.SetValue("Portable", 1, RegistryValueKind.DWord);
            }
            catch { }
        }

        private static bool SameDir(string a, string b)
        {
            static string Norm(string p) => p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
            return Norm(a) == Norm(b);
        }

        public static string InstallTo(string targetDir, bool makeShortcuts)
        {
            Directory.CreateDirectory(targetDir);

            string destExe = Path.Combine(targetDir, Path.GetFileName(ExePath));
            File.Copy(ExePath, destExe, overwrite: true);

            long sizeKb = Math.Max(1, new FileInfo(destExe).Length / 1024);

            using (var k = Registry.CurrentUser.CreateSubKey(RegRoot))
            {
                k.SetValue("InstallPath", targetDir);
                k.SetValue("Version", Version);
            }

            using (var uk = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                uk.SetValue("DisplayName", AppName);
                uk.SetValue("DisplayVersion", Version);
                uk.SetValue("Publisher", "PCToolkit Project");
                uk.SetValue("InstallLocation", targetDir);
                uk.SetValue("DisplayIcon", destExe);
                uk.SetValue("UninstallString", "\"" + destExe + "\" --uninstall");
                uk.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
                uk.SetValue("NoModify", 1, RegistryValueKind.DWord);
                uk.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }

            if (makeShortcuts)
            {
                TryShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName, destExe, targetDir);
                TryShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"), AppName, destExe, targetDir);
            }

            return destExe;
        }

        public static void TryShortcut(string folder, string name, string targetExe, string workDir)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string linkPath = Path.Combine(folder, name + ".lnk");

                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType is null)
                    return;

                dynamic shell = Activator.CreateInstance(shellType)!;
                try
                {
                    dynamic lnk = shell.CreateShortcut(linkPath);
                    try
                    {
                        lnk.TargetPath = targetExe;
                        lnk.WorkingDirectory = workDir;
                        lnk.Description = AppName + " " + Version;
                        lnk.Save();
                    }
                    finally
                    {
                        Marshal.FinalReleaseComObject((object)lnk);
                    }
                }
                finally
                {
                    Marshal.FinalReleaseComObject((object)shell);
                }
            }
            catch { }
        }

        public static void RemoveShortcuts()
        {
            TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"));
            TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppName + ".lnk"));
        }

        public static void RunUninstall()
        {
            var r = MessageBox.Show(
                "Uninstall " + AppName + "? Shortcuts and registry entries will be removed.",
                "Uninstall " + AppName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (r != DialogResult.Yes)
                return;

            RemoveShortcuts();
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(RegRoot, false); } catch { }

            MessageBox.Show(
                AppName + " was uninstalled. You may delete the program folder if any files remain.",
                "Uninstall complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
