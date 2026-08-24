namespace PcToolkit
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    internal sealed class MainForm : Form
    {
        private TabControl _tabs = null!;
        private Label _subtitle = null!;
        private Label _statusLabel = null!;

        private TableLayoutPanel _specTable = null!;
        private Label? _uptimeValue;
        private ListView _drivesView = null!;

        private ProgressBar _cpuBar = null!;
        private ProgressBar _ramBar = null!;
        private Label _cpuLabel = null!;
        private Label _ramLabel = null!;
        private FlowLayoutPanel _drivesFlow = null!;
        private readonly Dictionary<string, (ProgressBar Bar, Label Info)> _driveBars = new();
        private string _driveSig = "";

        private TextBox _urlBox = null!;

        private Label _latestVerLabel = null!;
        private Button _checkBtn = null!;
        private string _releasesUrl = Updater.ReleasesUrl;

        private Timer _timer = null!;

        public MainForm()
        {
            Text = Installer.AppName + " — " + Installer.Version;
            ClientSize = new Size(900, 620);
            MinimumSize = new Size(800, 560);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Back;
            Font = Theme.MakeFont(9f);

            BuildTabs();
            BuildHeader();
            BuildFooter();

            PopulateDrivesList();
            UpdateLiveStats();

            _timer = new Timer { Interval = 1000 };
            _timer.Tick += (_, _) => UpdateLiveStats();
            _timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            base.OnFormClosed(e);
        }

        private void BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Surface };
            Controls.Add(header);

            var title = Theme.MakeLabel(Installer.AppName.ToUpperInvariant(), 15f, true, Theme.Text);
            title.Location = new Point(20, 11);
            header.Controls.Add(title);

            _subtitle = Theme.MakeLabel(SubText(), 9f, false, Theme.SubText);
            _subtitle.Location = new Point(22, 38);
            header.Controls.Add(_subtitle);

            var ver = Theme.MakeLabel("v" + Installer.Version, 9.5f, true, Theme.Accent);
            ver.Location = new Point(ClientSize.Width - 90, 24);
            ver.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            header.Controls.Add(ver);
        }

        private void BuildFooter()
        {
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Theme.Surface };
            Controls.Add(bar);

            _statusLabel = Theme.MakeLabel("Ready.", 8.75f, false, Theme.SubText);
            _statusLabel.Location = new Point(12, 7);
            bar.Controls.Add(_statusLabel);

            var right = Theme.MakeLabel("PC Toolkit Bootstrapper", 8.75f, false, Color.FromArgb(108, 112, 134));
            right.Location = new Point(ClientSize.Width - 170, 7);
            right.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bar.Controls.Add(right);
        }

        private void BuildTabs()
        {
            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                SizeMode = TabSizeMode.Fixed,
                ItemSize = new Size(130, 40),
                DrawMode = TabDrawMode.OwnerDrawFixed
            };
            _tabs.DrawItem += Tabs_DrawItem;
            _tabs.SelectedIndexChanged += (_, _) => _tabs.Invalidate();

            var specs = new TabPage("System Specs") { BackColor = Theme.Back, Padding = new Padding(4) };
            var live = new TabPage("Live Stats") { BackColor = Theme.Back, Padding = new Padding(4) };
            var maint = new TabPage("Maintenance") { BackColor = Theme.Back, Padding = new Padding(4) };
            var updates = new TabPage("Updates") { BackColor = Theme.Back, Padding = new Padding(4) };
            var logsPage = new TabPage("Logs & Tools") { BackColor = Theme.Back, Padding = new Padding(4) };

            _tabs.TabPages.AddRange(new[] { specs, live, maint, updates, logsPage });
            Controls.Add(_tabs);

            BuildSpecsTab(specs);
            BuildLiveTab(live);
            BuildMaintTab(maint);
            BuildUpdatesTab(updates);
            BuildLogsTab(logsPage);
        }

        private void Tabs_DrawItem(object? sender, DrawItemEventArgs e)
        {
            using (var bg = new SolidBrush(Theme.Surface))
                e.Graphics.FillRectangle(bg, e.Bounds);

            bool selected = e.Index == _tabs.SelectedIndex;
            using (var f = Theme.MakeFont(10f, selected))
            {
                TextRenderer.DrawText(e.Graphics, _tabs.TabPages[e.Index].Text, f, e.Bounds,
                    selected ? Theme.Accent : Theme.SubText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            if (selected)
            {
                using var pen = new Pen(Theme.Accent, 2f);
                e.Graphics.DrawLine(pen, e.Bounds.Left + 12, e.Bounds.Bottom - 2, e.Bounds.Right - 12, e.Bounds.Bottom - 2);
            }
        }

        private static Label SectionTitle(Control parent, string text, float size, int x, int y, Color? color = null)
        {
            var l = Theme.MakeLabel(text, size, true, color ?? Theme.Text);
            l.Location = new Point(x, y);
            parent.Controls.Add(l);
            return l;
        }

        private static Panel NewCard(Control parent, int x, int y, int w, int h)
        {
            var card = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Theme.Card };
            parent.Controls.Add(card);
            return card;
        }

        private void BuildSpecsTab(TabPage page)
        {
            var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Back };
            page.Controls.Add(host);

            SectionTitle(host, "System information", 13f, 16, 14);

            _specTable = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                BackColor = Theme.Back,
                Location = new Point(16, 46),
                Width = 620
            };
            _specTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            _specTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            host.Controls.Add(_specTable);

            AddSpecRow("Device", $"{SystemInfo.MachineName}  ({SystemInfo.UserName})");
            AddSpecRow("Operating system", SystemInfo.OsLabel);
            AddSpecRow("Processor", $"{SystemInfo.CpuName}   ({SystemInfo.CpuPhysicalCores} cores, {SystemInfo.CpuLogicalCores} threads)");
            AddSpecRow("Graphics", SystemInfo.Gpu);
            AddSpecRow("Memory", RamLine());
            if (!string.IsNullOrWhiteSpace(SystemInfo.Motherboard))
                AddSpecRow("Motherboard", SystemInfo.Motherboard);
            _uptimeValue = AddSpecRow("Uptime", UptimeText());

            int rows = Math.Max(1, _specTable.RowCount);
            int dy = 46 + rows * 30 + 14;

            SectionTitle(host, "Storage", 13f, 16, dy);

            _drivesView = new ListView
            {
                Location = new Point(16, dy + 32),
                Size = new Size(660, 150),
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.MakeFont(9f)
            };
            _drivesView.Columns.Add("Drive", 70);
            _drivesView.Columns.Add("Label", 160);
            _drivesView.Columns.Add("Total", 100, HorizontalAlignment.Right);
            _drivesView.Columns.Add("Used", 100, HorizontalAlignment.Right);
            _drivesView.Columns.Add("Free", 100, HorizontalAlignment.Right);
            _drivesView.Columns.Add("Used %", 80, HorizontalAlignment.Right);
            host.Controls.Add(_drivesView);
        }

        private Label AddSpecRow(string key, string val)
        {
            int r = _specTable.RowCount++;
            _specTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var k = Theme.MakeLabel(key, 9f, true, Theme.SubText);
            k.Dock = DockStyle.Fill;
            k.TextAlign = ContentAlignment.MiddleLeft;
            k.Margin = new Padding(0, 5, 10, 5);

            var v = Theme.MakeLabel(val, 9.5f, false, Theme.Text);
            v.AutoSize = true;
            v.Margin = new Padding(0, 5, 0, 5);

            _specTable.Controls.Add(k, 0, r);
            _specTable.Controls.Add(v, 1, r);
            return v;
        }

        private void PopulateDrivesList()
        {
            _drivesView.BeginUpdate();
            _drivesView.Items.Clear();
            foreach (var d in SystemInfo.GetDrives())
            {
                long used = d.Total - d.Free;
                int pct = d.Total > 0 ? (int)(used * 100 / d.Total) : 0;
                var item = new ListViewItem(d.Letter);
                item.SubItems.Add(d.Label);
                item.SubItems.Add(Format.Bytes(d.Total));
                item.SubItems.Add(Format.Bytes(used));
                item.SubItems.Add(Format.Bytes(d.Free));
                item.SubItems.Add(pct + "%");
                _drivesView.Items.Add(item);
            }
            _drivesView.EndUpdate();
        }

        private void BuildLiveTab(TabPage page)
        {
            var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Back };
            page.Controls.Add(host);

            SectionTitle(host, "Real-time usage", 13f, 16, 14);

            var cpuCard = NewCard(host, 16, 48, 540, 96);
            var ct = Theme.MakeLabel("CPU", 10f, true, Theme.Text);
            ct.Location = new Point(14, 9);
            cpuCard.Controls.Add(ct);
            _cpuBar = new ProgressBar { Location = new Point(14, 34), Size = new Size(512, 20) };
            cpuCard.Controls.Add(_cpuBar);
            _cpuLabel = Theme.MakeLabel("0%", 9f, true, Theme.Accent);
            _cpuLabel.Location = new Point(14, 62);
            cpuCard.Controls.Add(_cpuLabel);

            var ramCard = NewCard(host, 16, 158, 540, 106);
            var rt = Theme.MakeLabel("Memory", 10f, true, Theme.Text);
            rt.Location = new Point(14, 9);
            ramCard.Controls.Add(rt);
            _ramBar = new ProgressBar { Location = new Point(14, 34), Size = new Size(512, 20) };
            ramCard.Controls.Add(_ramBar);
            _ramLabel = Theme.MakeLabel("", 9f, true, Theme.Accent);
            _ramLabel.Location = new Point(14, 62);
            ramCard.Controls.Add(_ramLabel);

            SectionTitle(host, "Drives", 13f, 16, 282);

            _drivesFlow = new FlowLayoutPanel
            {
                Location = new Point(16, 316),
                Size = new Size(740, 230),
                AutoScroll = true,
                WrapContents = true,
                BackColor = Theme.Back
            };
            host.Controls.Add(_drivesFlow);
        }

        private void SyncDriveCards(List<DriveSlot> drives)
        {
            string sig = string.Join("|", drives.Select(d => d.Letter));
            if (sig != _driveSig)
            {
                _driveSig = sig;
                _drivesFlow.SuspendLayout();
                _drivesFlow.Controls.Clear();
                _driveBars.Clear();

                foreach (var d in drives)
                {
                    var card = new Panel { Size = new Size(350, 98), BackColor = Theme.Card, Margin = new Padding(0, 0, 14, 14) };

                    var name = Theme.MakeLabel(d.Letter + "  " + d.Label, 10f, true, Theme.Text);
                    name.Location = new Point(12, 8);
                    card.Controls.Add(name);

                    var bar = new ProgressBar { Location = new Point(12, 36), Size = new Size(326, 18) };
                    card.Controls.Add(bar);

                    var info = Theme.MakeLabel("", 8.75f, false, Theme.SubText);
                    info.Location = new Point(12, 62);
                    card.Controls.Add(info);

                    _drivesFlow.Controls.Add(card);
                    _driveBars[d.Letter] = (bar, info);
                }
                _drivesFlow.ResumeLayout();
            }

            foreach (var d in drives)
            {
                if (!_driveBars.TryGetValue(d.Letter, out var ui))
                    continue;

                long used = d.Total - d.Free;
                int pct = d.Total > 0 ? (int)Math.Clamp(used * 100 / d.Total, 0L, 100L) : 0;
                ui.Bar.Value = pct;
                ui.Info.Text = $"{pct}% used  •  {Format.Bytes(d.Free)} free of {Format.Bytes(d.Total)}";
            }
        }

        private void UpdateLiveStats()
        {
            double cpu = SystemInfo.GetCpuPercent();
            _cpuBar.Value = (int)Math.Round(Math.Clamp(cpu, 0, 100));
            _cpuLabel.Text = $"CPU load: {_cpuBar.Value}%";

            var mem = SystemInfo.MemSnapshot();
            _ramBar.Value = (int)Math.Clamp(mem.Percent, 0u, 100u);
            _ramLabel.Text = $"{Format.Bytes(mem.Used)} / {Format.Bytes(mem.Total)}  ({mem.Percent}%)";

            SyncDriveCards(SystemInfo.GetDrives());

            if (_uptimeValue is not null)
                _uptimeValue.Text = UptimeText();

            _subtitle.Text = SubText();
        }

        private string RamLine()
        {
            return SystemInfo.RamSpeedMhz > 0
                ? $"{Format.Bytes(SystemInfo.RamTotalBytes)} @ {SystemInfo.RamSpeedMhz} MHz"
                : Format.Bytes(SystemInfo.RamTotalBytes);
        }

        private static string UptimeText()
        {
            var up = SystemInfo.Uptime;
            return $"{up.Days}d {up.Hours}h {up.Minutes}m";
        }

        private static string SubText()
        {
            return $"{SystemInfo.UserName}@{SystemInfo.MachineName}    •    {SystemInfo.CpuLogicalCores} threads    •    {Format.Bytes(SystemInfo.RamTotalBytes)} RAM";
        }

        private void BuildMaintTab(TabPage page)
        {
            var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Back };
            page.Controls.Add(host);

            SectionTitle(host, "Maintenance actions", 13f, 16, 14);

            int y = 50;
            AddAction(host, ref y, "Clean temporary files",
                "Removes leftovers from your user and Windows temp folders.",
                async () =>
                {
                    SetStatus("Scanning temp folders...");
                    var result = await Maintenance.CleanTempAsync(new Progress<string>(s => SetStatus(s)));
                    Info("Temp cleanup", result);
                });

            AddAction(host, ref y, "Flush DNS cache",
                "Clears the Windows DNS resolver cache.",
                () =>
                {
                    var r = Maintenance.FlushDns();
                    SetStatus(r);
                    Info("Flush DNS", r);
                });

            AddAction(host, ref y, "Restart Windows Explorer",
                "Restarts the desktop shell; the taskbar will blink briefly.",
                () =>
                {
                    SetStatus("Restarting explorer...");
                    Maintenance.RestartExplorer();
                    SetStatus("Explorer restarted.");
                });

            AddAction(host, ref y, "Open Disk Cleanup",
                "Launches the built-in Windows Disk Cleanup utility.",
                () => Maintenance.OpenApp("cleanmgr.exe"));

            AddAction(host, ref y, "Scan system files (SFC)",
                "Runs 'sfc /scannow' as administrator to repair system files.",
                () =>
                {
                    if (!Maintenance.StartElevated("sfc.exe", "/scannow"))
                        Error("SFC scan was cancelled or could not be elevated.");
                    else
                        SetStatus("SFC scan started in its own window.");
                });

            AddAction(host, ref y, "Open Task Manager",
                "Shows running processes and resource usage.",
                () => Maintenance.OpenApp("taskmgr.exe"));

            AddAction(host, ref y, "Generate full system report",
                "Writes specs, top processes and recent warnings/errors into a log file.",
                async () =>
                {
                    SetStatus("Collecting system report...");
                    string path = await Task.Run(Logs.CollectReport);
                    SetStatus("Report saved: " + path);
                    if (MessageBox.Show(this, "Report saved to:\n" + path + "\n\nOpen the folder?", "Report ready",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        Maintenance.OpenApp(Logs.Folder);
                });
        }

        private void AddAction(Control host, ref int y, string title, string desc, Action onClick)
        {
            var card = NewCard(host, 16, y, 700, 74);

            var t = Theme.MakeLabel(title, 10f, true, Theme.Text);
            t.Location = new Point(14, 10);
            card.Controls.Add(t);

            var dd = Theme.MakeLabel(desc, 8.75f, false, Theme.SubText);
            dd.Location = new Point(14, 33);
            card.Controls.Add(dd);

            var btn = Theme.MakeButton("Run");
            btn.Location = new Point(700 - btn.Width - 14, 20);
            btn.Click += (_, _) =>
            {
                try { onClick(); }
                catch (Exception ex) { Error(ex.Message); }
            };
            card.Controls.Add(btn);

            y += 88;
        }

        private void BuildLogsTab(TabPage page)
        {
            var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Back };
            page.Controls.Add(host);

            SectionTitle(host, "Download log bundle", 13f, 16, 14);

            var dlCard = NewCard(host, 16, 46, 700, 110);

            var expl = Theme.MakeLabel(
                "Enter a direct http(s) URL to a log/archive file (.zip, .txt, ...) and it will be saved into the local logs folder.",
                8.75f, false, Theme.SubText);
            expl.Location = new Point(14, 10);
            expl.Width = 650;
            dlCard.Controls.Add(expl);

            _urlBox = new TextBox
            {
                Location = new Point(14, 44),
                Width = 480,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle
            };
            dlCard.Controls.Add(_urlBox);

            var dlBtn = Theme.MakeButton("Download");
            dlBtn.Location = new Point(506, 42);
            dlBtn.Click += async (_, _) => await DownloadClickedAsync(dlBtn);
            dlCard.Controls.Add(dlBtn);

            SectionTitle(host, "Local logs", 13f, 16, 176);

            var loCard = NewCard(host, 16, 208, 700, 120);

            var pathLbl = Theme.MakeLabel("Folder: " + Logs.Folder, 8.75f, false, Theme.SubText);
            pathLbl.AutoEllipsis = true;
            pathLbl.Location = new Point(14, 10);
            pathLbl.Width = 660;
            loCard.Controls.Add(pathLbl);

            var openBtn = Theme.MakeButton("Open folder");
            openBtn.Location = new Point(14, 66);
            openBtn.Click += (_, _) =>
            {
                try
                {
                    Directory.CreateDirectory(Logs.Folder);
                    Maintenance.OpenApp(Logs.Folder);
                }
                catch (Exception ex) { Error(ex.Message); }
            };
            loCard.Controls.Add(openBtn);

            var repBtn = Theme.MakeButton("Generate report now");
            repBtn.Location = new Point(140, 66);
            repBtn.Click += async (_, _) =>
            {
                try
                {
                    SetStatus("Generating report...");
                    string path = await Task.Run(Logs.CollectReport);
                    SetStatus("Report saved: " + path);
                    Info("Report saved", path);
                }
                catch (Exception ex) { Error(ex.Message); }
            };
            loCard.Controls.Add(repBtn);
        }

        private void BuildUpdatesTab(TabPage page)
        {
            var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Back };
            page.Controls.Add(host);

            SectionTitle(host, "Software updates", 13f, 16, 14);

            var card = NewCard(host, 16, 46, 700, 150);

            var cur = Theme.MakeLabel("Installed version: " + Installer.Version, 10.5f, true, Theme.Text);
            cur.Location = new Point(14, 12);
            card.Controls.Add(cur);

            _latestVerLabel = Theme.MakeLabel("Latest version: checking…", 9.5f, false, Theme.SubText);
            _latestVerLabel.Location = new Point(14, 40);
            card.Controls.Add(_latestVerLabel);

            var hint = Theme.MakeLabel(
                "Updates are pulled automatically from the private GitHub releases feed on startup.",
                8.75f, false, Theme.SubText);
            hint.Location = new Point(14, 66);
            card.Controls.Add(hint);

            _checkBtn = Theme.MakeButton("Check now");
            _checkBtn.Location = new Point(14, 100);
            _checkBtn.Click += async (_, _) => await CheckForUpdates(false);
            card.Controls.Add(_checkBtn);

            var openBtn = Theme.MakeButton("View releases");
            openBtn.Location = new Point(130, 100);
            openBtn.Click += (_, _) =>
            {
                try { Maintenance.OpenApp(_releasesUrl); }
                catch (Exception ex) { Error(ex.Message); }
            };
            card.Controls.Add(openBtn);
        }

        private async Task CheckForUpdates(bool silent)
        {
            _checkBtn.Enabled = false;
            SetStatus(silent ? "Checking for updates in the background..." : "Checking for updates...");

            try
            {
                var upd = await Task.Run(() => Updater.CheckAsync());

                if (upd is null)
                {
                    _latestVerLabel.Text = "Latest version: " + Installer.Version + "  (up to date)";
                    _latestVerLabel.ForeColor = Theme.SubText;
                    SetStatus("You are up to date.");
                    if (!silent)
                        Info("No updates", $"You are running the latest version (v{Installer.Version}).");
                    return;
                }

                _latestVerLabel.Text = "Latest version: v" + upd.Version + "  — UPDATE AVAILABLE";
                _latestVerLabel.ForeColor = Theme.Good;
                _releasesUrl = upd.HtmlUrl;
                SetStatus("Update v" + upd.Version + " is available.");

                string notes = upd.Notes ?? "";
                if (notes.Length > 500) notes = notes[..500] + "…";

                var choice = MessageBox.Show(
                    this,
                    $"A new version is available.\n\nInstalled:  v{Installer.Version}\nLatest:      v{upd.Version}" +
                    (notes.Length > 0 ? $"\n\n{notes}" : "") +
                    "\n\nDownload and install now?",
                    "Update available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (choice != DialogResult.Yes)
                    return;

                SetStatus("Downloading update v" + upd.Version + "...");
                string tmp = await Updater.DownloadToTempAsync(upd, new Progress<string>(SetStatus));

                SetStatus("Restarting to apply update...");
                Updater.ApplyUpdate(tmp);
            }
            catch (Exception ex)
            {
                _latestVerLabel.Text = "Latest version: unavailable";
                _latestVerLabel.ForeColor = Theme.Bad;
                SetStatus("Update check failed.");
                if (!silent)
                    Error("Update check failed: " + ex.Message);
            }
            finally
            {
                _checkBtn.Enabled = true;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _ = CheckForUpdates(true);
        }

        private async Task DownloadClickedAsync(Button btn)
        {
            string url = _urlBox.Text.Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            {
                Error("Enter a valid http(s) URL.");
                return;
            }

            btn.Enabled = false;
            try
            {
                SetStatus("Starting download...");
                string path = await Logs.DownloadBundleAsync(url, new Progress<string>(SetStatus));
                SetStatus("Downloaded: " + path);
                Info("Download complete", "Saved to:\n" + path);
            }
            catch (Exception ex)
            {
                SetStatus("Download failed.");
                Error("Download failed: " + ex.Message);
            }
            finally
            {
                btn.Enabled = true;
            }
        }

        private void SetStatus(string msg)
        {
            if (_statusLabel.InvokeRequired)
            {
                _statusLabel.BeginInvoke(() => { _statusLabel.Text = msg; _statusLabel.ForeColor = Theme.SubText; });
            }
            else
            {
                _statusLabel.Text = msg;
                _statusLabel.ForeColor = Theme.SubText;
            }
        }

        private void Info(string title, string message)
        {
            MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Error(string message)
        {
            MessageBox.Show(this, message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
