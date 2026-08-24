namespace PcToolkit
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;

    internal static class Logs
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

        public static string Folder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCToolkit", "logs");

        public static string CollectReport()
        {
            Directory.CreateDirectory(Folder);

            var sb = new StringBuilder();
            sb.AppendLine("PC Toolkit system report");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Machine : " + SystemInfo.MachineName + " (" + SystemInfo.UserName + ")");
            sb.AppendLine("OS      : " + SystemInfo.OsLabel);
            sb.AppendLine("Uptime  : " + SystemInfo.Uptime.ToString(@"dd\.hh\:mm\:ss"));
            sb.AppendLine($"CPU     : {SystemInfo.CpuName} ({SystemInfo.CpuPhysicalCores}C/{SystemInfo.CpuLogicalCores}T)");
            sb.AppendLine("GPU     : " + SystemInfo.Gpu);
            if (!string.IsNullOrWhiteSpace(SystemInfo.Motherboard))
                sb.AppendLine("Board   : " + SystemInfo.Motherboard);

            var mem = SystemInfo.MemSnapshot();
            sb.AppendLine($"RAM     : {Format.Bytes(mem.Total)} total, {mem.Percent}% in use");

            sb.AppendLine();
            sb.AppendLine("-- Disks --");
            foreach (var d in SystemInfo.GetDrives())
                sb.AppendLine($"{d.Letter} [{d.Label}] {Format.Bytes(d.Total)} total, {Format.Bytes(d.Free)} free");

            sb.AppendLine();
            sb.AppendLine("-- Top processes by memory --");
            foreach (var p in Process.GetProcesses().OrderByDescending(SafeWs).Take(15))
                sb.AppendLine(string.Format("{0,-34} {1}", p.ProcessName, Format.Bytes(SafeWs(p))));

            sb.AppendLine();
            sb.AppendLine("-- Recent warnings/errors --");
            AppendEventLog(sb, "Application");
            AppendEventLog(sb, "System");

            string path = Path.Combine(Folder, "system-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        private static long SafeWs(Process p)
        {
            try { return p.WorkingSet64; } catch { return 0; }
        }

        private static void AppendEventLog(StringBuilder sb, string name)
        {
            try
            {
                using var log = new EventLog(name);
                int taken = 0;
                sb.AppendLine("[" + name + "]");
                for (int i = log.Entries.Count - 1; i >= 0 && taken < 30; i--)
                {
                    var e = log.Entries[i];
                    if (e.EntryType is not (EventLogEntryType.Error or EventLogEntryType.Warning))
                        continue;
                    sb.AppendLine($"  {e.TimeGenerated:yyyy-MM-dd HH:mm} {e.Source}: {Truncate(e.Message, 220)}");
                    taken++;
                }
                if (taken == 0)
                    sb.AppendLine("  (none)");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine("[" + name + "] unavailable: " + ex.Message);
                sb.AppendLine();
            }
        }

        private static string Truncate(string? s, int max)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ");
            return s.Length <= max ? s : s[..max] + "...";
        }

        public static async Task<string> DownloadBundleAsync(string url, IProgress<string> progress)
        {
            Directory.CreateDirectory(Folder);

            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            long? len = resp.Content.Headers.ContentLength;
            string fileName = GetFileName(url);
            string dest = UniquePath(Path.Combine(Folder, fileName));

            progress.Report("Downloading " + fileName + "...");

            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await src.ReadAsync(buffer)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read));
                total += read;
                progress.Report(len is > 0
                    ? $"Downloading {fileName}... {Format.Bytes(total)} / {Format.Bytes(len.Value)}"
                    : $"Downloading {fileName}... {Format.Bytes(total)}");
            }

            progress.Report("Saved to " + dest);
            return dest;
        }

        private static string GetFileName(string url)
        {
            try
            {
                var n = Path.GetFileName(new Uri(url).LocalPath);
                if (!string.IsNullOrWhiteSpace(n))
                    return n;
            }
            catch { }
            return "log-bundle-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bin";
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path))
                return path;
            var dir = Path.GetDirectoryName(path)!;
            var stem = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            for (int i = 1; ; i++)
            {
                var p = Path.Combine(dir, $"{stem}-{i}{ext}");
                if (!File.Exists(p))
                    return p;
            }
        }
    }
}
