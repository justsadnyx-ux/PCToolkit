namespace PcToolkit
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    internal static class Maintenance
    {
        public static Task<string> CleanTempAsync(IProgress<string>? status)
        {
            return Task.Run(() =>
            {
                long freed = 0;
                int files = 0;

                freed += Sweep(Path.GetTempPath(), ref files);
                freed += Sweep(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), ref files);

                string msg = $"Deleted {files} temp files ({Format.Bytes(freed)} freed). In-use files are skipped automatically.";
                status?.Report(msg);
                return msg;
            });
        }

        private static long Sweep(string dir, ref int files)
        {
            long bytes = 0;
            try
            {
                if (!Directory.Exists(dir))
                    return 0;

                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        bytes += fi.Length;
                        fi.Delete();
                        files++;
                    }
                    catch { }
                }
            }
            catch { }
            return bytes;
        }

        public static string FlushDns()
        {
            var (ok, output) = Run("ipconfig", "/flushdns", 15000);
            return ok ? "DNS resolver cache flushed." : "Failed to flush DNS: " + output;
        }

        public static void RestartExplorer()
        {
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                try { p.Kill(); } catch { }
            }
            System.Threading.Thread.Sleep(400);
            try { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true }); } catch { }
        }

        public static bool StartElevated(string fileName, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(fileName, args) { Verb = "runas", UseShellExecute = true };
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void OpenApp(string target)
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }

        private static (bool Ok, string Output) Run(string fileName, string args, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo(fileName, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p is null)
                    return (false, "could not start process");
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    return (false, "timed out");
                }
                return (p.ExitCode == 0, p.StandardOutput.ReadToEnd().Trim());
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
