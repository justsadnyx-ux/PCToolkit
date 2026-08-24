namespace PcToolkit
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    internal sealed class InstallDialog : Form
    {
        private TextBox _pathBox = null!;
        private CheckBox _shortcutsChk = null!;
        private CheckBox _launchChk = null!;
        private ProgressBar _progress = null!;
        private Label _status = null!;
        private Button _installBtn = null!;
        private Button _exitBtn = null!;
        private Button _browseBtn = null!;

        public bool LaunchRequested { get; private set; }
        public string InstalledExe { get; private set; } = "";

        public InstallDialog()
        {
            Text = Installer.AppName + " " + Installer.Version + " — Setup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 470);
            BackColor = Theme.Back;
            Font = Theme.MakeFont(9f);

            BuildSidePanel();
            BuildMainArea();
        }

        private void BuildSidePanel()
        {
            var side = new Panel { Dock = DockStyle.Left, Width = 260, BackColor = Theme.Accent };
            Controls.Add(side);

            var brand = Theme.MakeLabel(Installer.AppName.ToUpperInvariant(), 19f, true, Theme.Ink);
            brand.Location = new Point(24, 44);
            side.Controls.Add(brand);

            var tagline = Theme.MakeLabel("Bootstrapper v" + Installer.Version, 10f, false, Color.FromArgb(41, 55, 86));
            tagline.Location = new Point(24, 80);
            side.Controls.Add(tagline);

            int y = 140;
            y = AddSpec(side, "CPU", SystemInfo.CpuName, y);
            var mem = SystemInfo.MemSnapshot();
            y = AddSpec(side, "Memory", Format.Bytes(mem.Total), y);
            y = AddSpec(side, "Cores", SystemInfo.CpuPhysicalCores + " physical / " + SystemInfo.CpuLogicalCores + " logical", y);
            y = AddSpec(side, "OS", SystemInfo.OsLabel, y);
            AddSpec(side, "Free disk", FreeOnSystemDrive(), y);
        }

        private int AddSpec(Control parent, string key, string val, int y)
        {
            if (val.Length > 34)
                val = val[..33] + "…";

            var k = Theme.MakeLabel(key.ToUpperInvariant(), 8f, true, Color.FromArgb(41, 55, 86));
            k.Location = new Point(24, y);
            parent.Controls.Add(k);

            var v = Theme.MakeLabel(val, 9.5f, true, Theme.Ink);
            v.Location = new Point(24, y + 15);
            parent.Controls.Add(v);

            return y + 54;
        }

        private string FreeOnSystemDrive()
        {
            try
            {
                var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                var d = new DriveInfo(root);
                return Format.Bytes(d.AvailableFreeSpace);
            }
            catch
            {
                return "?";
            }
        }

        private void BuildMainArea()
        {
            int left = 292;
            int w = ClientSize.Width - left - 28;

            var head = Theme.MakeLabel("Install " + Installer.AppName, 15f, true, Theme.Text);
            head.Location = new Point(left, 36);
            Controls.Add(head);

            var blurb = new Label
            {
                Text = "This setup will copy the application to your computer, create shortcuts and register an uninstaller in Windows Settings.\n\nReview the destination folder below, then click Install.",
                Location = new Point(left, 68),
                Size = new Size(w, 66),
                Font = Theme.MakeFont(9.5f),
                ForeColor = Theme.SubText,
                BackColor = Color.Transparent
            };
            Controls.Add(blurb);

            var pathLbl = Theme.MakeLabel("Install to:", 9f, true, Theme.Text);
            pathLbl.Location = new Point(left, 152);
            Controls.Add(pathLbl);

            _pathBox = new TextBox
            {
                Location = new Point(left, 176),
                Width = w - 100,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Text = Installer.DefaultInstallDir
            };
            Controls.Add(_pathBox);

            _browseBtn = Theme.MakeButton("Browse...");
            _browseBtn.Location = new Point(left + w - 92, 173);
            _browseBtn.Click += (_, _) =>
            {
                using var dlg = new FolderBrowserDialog { SelectedPath = _pathBox.Text, ShowNewFolderButton = true };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _pathBox.Text = dlg.SelectedPath;
            };
            Controls.Add(_browseBtn);

            _shortcutsChk = new CheckBox
            {
                Text = "Create desktop and Start Menu shortcuts",
                AutoSize = true,
                Checked = true,
                ForeColor = Theme.Text,
                Location = new Point(left, 220)
            };
            Controls.Add(_shortcutsChk);

            _launchChk = new CheckBox
            {
                Text = "Launch after installation",
                AutoSize = true,
                Checked = true,
                ForeColor = Theme.Text,
                Location = new Point(left, 246)
            };
            Controls.Add(_launchChk);

            _progress = new ProgressBar
            {
                Location = new Point(left, 286),
                Size = new Size(w, 22),
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };
            Controls.Add(_progress);

            _status = Theme.MakeLabel("Ready to install.", 9f, false, Theme.SubText);
            _status.Location = new Point(left, 318);
            _status.Size = new Size(w, 18);
            Controls.Add(_status);

            _installBtn = Theme.MakeButton("Install");
            _installBtn.BackColor = Theme.Accent;
            _installBtn.ForeColor = Theme.Ink;
            _installBtn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(Theme.Accent);
            _installBtn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(Theme.Accent);
            _installBtn.Location = new Point(ClientSize.Width - 212, ClientSize.Height - 60);
            _installBtn.Click += async (_, _) => await DoInstallAsync();
            Controls.Add(_installBtn);

            _exitBtn = Theme.MakeButton("Close");
            _exitBtn.Location = new Point(ClientSize.Width - 106, ClientSize.Height - 60);
            _exitBtn.Click += (_, _) => Close();
            Controls.Add(_exitBtn);

            var skip = new LinkLabel
            {
                Text = "Continue without installing (portable)",
                AutoSize = true,
                LinkColor = Theme.Accent,
                LinkBehavior = LinkBehavior.HoverUnderline,
                Location = new Point(left, ClientSize.Height - 52)
            };
            skip.LinkClicked += (_, _) =>
            {
                Installer.SetPortableFlag();
                Close();
            };
            Controls.Add(skip);

            AcceptButton = _installBtn;
        }

        private async Task DoInstallAsync()
        {
            string target = _pathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                _status.ForeColor = Theme.Bad;
                _status.Text = "Choose a destination folder first.";
                return;
            }

            try
            {
                _installBtn.Enabled = false;
                _browseBtn.Enabled = false;
                _exitBtn.Enabled = false;
                _progress.Visible = true;
                _status.ForeColor = Theme.SubText;
                _status.Text = "Installing…";

                string exe = await Task.Run(() => Installer.InstallTo(target, _shortcutsChk.Checked));

                _progress.Visible = false;
                _status.ForeColor = Theme.Good;
                _status.Text = "Installed successfully to " + target;

                LaunchRequested = _launchChk.Checked;
                InstalledExe = exe;

                await Task.Delay(700);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _progress.Visible = false;
                _status.ForeColor = Theme.Bad;
                _status.Text = "Installation failed: " + ex.Message;
                _installBtn.Enabled = true;
                _browseBtn.Enabled = true;
                _exitBtn.Enabled = true;
            }
        }
    }
}
