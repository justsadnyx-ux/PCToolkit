namespace PcToolkit
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    internal static class Updater
    {
        public const string Repo = "justsadnyx-ux/PCToolkit";
        public const string ReleasesUrl = "https://github.com/" + Repo + "/releases";

        private static string Token()
        {
            var env = Environment.GetEnvironmentVariable("PCTOOLKIT_GH_TOKEN");
            if (!string.IsNullOrWhiteSpace(env))
                return env.Trim();

            try
            {
                var f = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PCToolkit", "token.txt");
                if (File.Exists(f))
                    return File.ReadAllText(f).Trim();
            }
            catch { }

            return "";
        }

        private static HttpClient MakeClient()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("PCToolkit-Updater/" + Installer.Version);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            var token = Token();
            if (token.Length > 0)
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return http;
        }

        public sealed record UpdateInfo(string Version, string ExeUrl, string Notes, string HtmlUrl);

        public static async Task<UpdateInfo?> CheckAsync()
        {
            using var http = MakeClient();
            using var resp = await http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"GitHub API returned {(int)resp.StatusCode} ({resp.StatusCode}).");

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            string tag = root.GetProperty("tag_name").GetString() ?? "";
            string version = tag.TrimStart('v', 'V');

            if (!IsNewer(version, Installer.Version))
                return null;

            string exeUrl = "";
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    exeUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                    break;
                }
            }
            if (exeUrl.Length == 0)
                throw new Exception("The latest release contains no .exe asset.");

            string notes = root.TryGetProperty("body", out var body) ? (body.GetString() ?? "") : "";
            string html = root.TryGetProperty("html_url", out var htmlEl) ? (htmlEl.GetString() ?? ReleasesUrl) : ReleasesUrl;

            return new UpdateInfo(version, exeUrl, notes, html);
        }

        private static bool IsNewer(string candidate, string current)
        {
            if (!Version.TryParse(candidate, out var c))
                return false;
            if (!Version.TryParse(current, out var cur))
                return true;
            return c > cur;
        }

        public static async Task<string> DownloadToTempAsync(UpdateInfo info, IProgress<string>? progress)
        {
            using var http = MakeClient();
            using var resp = await http.GetAsync(info.ExeUrl, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            long? len = resp.Content.Headers.ContentLength;
            string dest = Path.Combine(Path.GetTempPath(), $"PCToolkit-update-{info.Version}.exe");

            progress?.Report("Downloading update...");
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await src.ReadAsync(buffer)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read));
                total += read;
                progress?.Report(len is > 0
                    ? $"Downloading update... {Format.Bytes(total)} / {Format.Bytes(len.Value)}"
                    : $"Downloading update... {Format.Bytes(total)}");
            }

            return dest;
        }

        public static void ApplyUpdate(string downloadedExe)
        {
            int pid = Environment.ProcessId;
            string target = Installer.ExePath;

            Process.Start(new ProcessStartInfo
            {
                FileName = downloadedExe,
                Arguments = $"--apply-update {pid} \"{target}\"",
                UseShellExecute = true
            });

            Application.Exit();
        }
    }
}
