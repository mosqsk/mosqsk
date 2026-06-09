using VirtualPrinter.Core;
using VirtualPrinter.Core.Models;
using VirtualPrinter.Core.Services;

namespace VirtualPrinter.App.Forms;

public sealed class MainForm : Form
{
    private readonly PrinterConfiguration _config;
    private readonly PrinterServer _server;
    private readonly ZplProcessor _processor;

    // Status bar
    private Label _lblServerStatus = null!;
    private Label _lblPrinterStatus = null!;
    private Button _btnStartStop = null!;
    private Button _btnInstall = null!;
    private Button _btnUninstall = null!;
    private Button _btnSettings = null!;

    // Job list
    private ListView _lvJobs = null!;

    // Log
    private RichTextBox _rtbLog = null!;

    // Detail panel
    private Panel _pnlDetail = null!;
    private Label _lblDetailTitle = null!;
    private RichTextBox _rtbZpl = null!;
    private PictureBox _pbPreview = null!;

    public MainForm(PrinterConfiguration config, PrinterServer server, ZplProcessor processor)
    {
        _config = config;
        _server = server;
        _processor = processor;

        InitializeComponent();
        RefreshPrinterStatus();
        UpdateServerStatus();
    }

    // =========================================================================
    // UI construction
    // =========================================================================

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Virtual ZPL Printer";
        Size = new Size(960, 680);
        MinimumSize = new Size(760, 520);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        FormClosing += OnFormClosing;

        BuildToolbar();
        BuildStatusBar();
        BuildSplitArea();

        ResumeLayout(false);
        PerformLayout();
    }

    private void BuildToolbar()
    {
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Color.FromArgb(37, 99, 235),    // blue
            Padding = new Padding(8, 8, 8, 8)
        };

        var title = new Label
        {
            Text = "Virtual ZPL Printer  —  D365 FnO Document Routing Agent",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(12, 14)
        };

        _btnStartStop = MakeButton("Stop Server", Color.FromArgb(220, 38, 38));
        _btnStartStop.Location = new Point(toolbar.Width - 4 * (120 + 8), 10);
        _btnStartStop.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnStartStop.Click += BtnStartStop_Click;

        _btnInstall = MakeButton("Install Printer", Color.FromArgb(5, 150, 105));
        _btnInstall.Location = new Point(toolbar.Width - 3 * (120 + 8), 10);
        _btnInstall.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnInstall.Click += BtnInstall_Click;

        _btnUninstall = MakeButton("Remove Printer", Color.FromArgb(100, 116, 139));
        _btnUninstall.Location = new Point(toolbar.Width - 2 * (120 + 8), 10);
        _btnUninstall.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnUninstall.Click += BtnUninstall_Click;

        _btnSettings = MakeButton("Settings", Color.FromArgb(100, 116, 139));
        _btnSettings.Location = new Point(toolbar.Width - 1 * (120 + 8), 10);
        _btnSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnSettings.Click += BtnSettings_Click;

        toolbar.Controls.AddRange(new Control[] { title, _btnStartStop, _btnInstall, _btnUninstall, _btnSettings });
        Controls.Add(toolbar);
    }

    private static Button MakeButton(string text, Color backColor)
    {
        return new Button
        {
            Text = text,
            Width = 120,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F),
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand
        };
    }

    private void BuildStatusBar()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(12, 4, 12, 4)
        };

        _lblServerStatus = new Label
        {
            AutoSize = true,
            Location = new Point(12, 7),
            Font = new Font("Segoe UI", 8.5F)
        };

        _lblPrinterStatus = new Label
        {
            AutoSize = true,
            Location = new Point(320, 7),
            Font = new Font("Segoe UI", 8.5F)
        };

        bar.Controls.Add(_lblServerStatus);
        bar.Controls.Add(_lblPrinterStatus);
        Controls.Add(bar);
    }

    private void BuildSplitArea()
    {
        // Main split: left = jobs + log, right = detail panel
        var outerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 580,
            Panel1MinSize = 400,
            Panel2MinSize = 260
        };

        // Left side: jobs list on top, log at bottom
        var leftSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 300,
            Panel1MinSize = 150,
            Panel2MinSize = 100
        };

        BuildJobList(leftSplit.Panel1);
        BuildLog(leftSplit.Panel2);
        BuildDetailPanel(outerSplit.Panel2);

        outerSplit.Panel1.Controls.Add(leftSplit);
        Controls.Add(outerSplit);
    }

    private void BuildJobList(Control parent)
    {
        var header = SectionHeader("Print Jobs");
        header.Dock = DockStyle.Top;
        parent.Controls.Add(header);

        _lvJobs = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Consolas", 8.5F),
            MultiSelect = false
        };

        _lvJobs.Columns.Add("Time", 130);
        _lvJobs.Columns.Add("From", 110);
        _lvJobs.Columns.Add("Size", 75);
        _lvJobs.Columns.Add("Status", 85);
        _lvJobs.Columns.Add("File", 220);

        _lvJobs.SelectedIndexChanged += LvJobs_SelectedIndexChanged;

        parent.Controls.Add(_lvJobs);
    }

    private void BuildLog(Control parent)
    {
        var header = SectionHeader("Log");
        header.Dock = DockStyle.Top;
        parent.Controls.Add(header);

        _rtbLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Consolas", 8.5F),
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = false
        };

        parent.Controls.Add(_rtbLog);
    }

    private void BuildDetailPanel(Control parent)
    {
        var header = SectionHeader("Job Detail");
        header.Dock = DockStyle.Top;
        parent.Controls.Add(header);

        _lblDetailTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            Padding = new Padding(6, 4, 6, 0),
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.Gray,
            Text = "Select a job to inspect"
        };
        parent.Controls.Add(_lblDetailTitle);

        // ZPL source on top, preview below
        var detailSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 200,
            Panel1MinSize = 80,
            Panel2MinSize = 80
        };

        _rtbZpl = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(248, 250, 252),
            Font = new Font("Consolas", 8F),
            ScrollBars = RichTextBoxScrollBars.Both,
            WordWrap = false
        };
        detailSplit.Panel1.Controls.Add(_rtbZpl);

        _pbPreview = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White
        };
        detailSplit.Panel2.Controls.Add(_pbPreview);

        parent.Controls.Add(detailSplit);
    }

    private static Panel SectionHeader(string title)
    {
        var p = new Panel
        {
            Height = 24,
            BackColor = Color.FromArgb(226, 232, 240)
        };
        p.Controls.Add(new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(8, 4)
        });
        return p;
    }

    // =========================================================================
    // Public update methods — called thread-safely from AppContext
    // =========================================================================

    public void AppendJob(PrintJob job)
    {
        if (InvokeRequired) { Invoke(() => AppendJob(job)); return; }

        var item = new ListViewItem(job.ReceivedAt.ToString("HH:mm:ss.fff"))
        {
            Tag = job,
            ForeColor = Color.DimGray
        };
        item.SubItems.Add(job.ClientAddress);
        item.SubItems.Add(FormatBytes(job.SizeBytes));
        item.SubItems.Add(job.Status.ToString());
        item.SubItems.Add(job.SavedFilePath ?? "—");

        _lvJobs.Items.Insert(0, item);

        // Trim history
        while (_lvJobs.Items.Count > _config.MaxJobHistory)
            _lvJobs.Items.RemoveAt(_lvJobs.Items.Count - 1);
    }

    public void RefreshJob(PrintJob job)
    {
        if (InvokeRequired) { Invoke(() => RefreshJob(job)); return; }

        foreach (ListViewItem item in _lvJobs.Items)
        {
            if (item.Tag is PrintJob j && j.Id == job.Id)
            {
                item.SubItems[3].Text = job.Status.ToString();
                item.SubItems[4].Text = job.SavedFilePath ?? "—";
                item.ForeColor = job.Status == PrintJobStatus.Failed ? Color.Red : Color.Black;
                break;
            }
        }
    }

    public void AppendLog(string message)
    {
        if (InvokeRequired) { Invoke(() => AppendLog(message)); return; }

        var ts = DateTime.Now.ToString("HH:mm:ss");
        _rtbLog.AppendText($"[{ts}] {message}\n");
        _rtbLog.ScrollToCaret();
    }

    public void UpdateServerStatus()
    {
        if (InvokeRequired) { Invoke(UpdateServerStatus); return; }

        if (_server.IsRunning)
        {
            _lblServerStatus.Text = $"Server: RUNNING  |  Port: {_config.ListenPort}";
            _lblServerStatus.ForeColor = Color.FromArgb(5, 150, 105);
            _btnStartStop.Text = "Stop Server";
            _btnStartStop.BackColor = Color.FromArgb(220, 38, 38);
        }
        else
        {
            _lblServerStatus.Text = "Server: STOPPED";
            _lblServerStatus.ForeColor = Color.FromArgb(220, 38, 38);
            _btnStartStop.Text = "Start Server";
            _btnStartStop.BackColor = Color.FromArgb(5, 150, 105);
        }
    }

    // =========================================================================
    // Event handlers
    // =========================================================================

    private void LvJobs_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_lvJobs.SelectedItems.Count == 0 || _lvJobs.SelectedItems[0].Tag is not PrintJob job)
            return;

        _lblDetailTitle.Text = $"Job {job.Id}  —  {job.ReceivedAt:yyyy-MM-dd HH:mm:ss}  —  {FormatBytes(job.SizeBytes)}";
        _rtbZpl.Text = job.ZplContent;

        _pbPreview.Image?.Dispose();
        _pbPreview.Image = null;

        if (job.PreviewImagePath is not null && File.Exists(job.PreviewImagePath))
        {
            try
            {
                using var fs = File.OpenRead(job.PreviewImagePath);
                _pbPreview.Image = Image.FromStream(fs, false, false);
            }
            catch { /* preview load failed — ignore */ }
        }
    }

    private async void BtnStartStop_Click(object? sender, EventArgs e)
    {
        if (_server.IsRunning)
        {
            _server.Stop();
            AppendLog("Server stopped by user.");
        }
        else
        {
            await _server.StartAsync();
            AppendLog($"Server started on port {_config.ListenPort}.");
        }

        UpdateServerStatus();
    }

    private void BtnInstall_Click(object? sender, EventArgs e)
    {
        var result = WindowsPrinterManager.Install(_config);

        if (result.Ok)
            MessageBox.Show(result.Message, "Printer Installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show(result.Message, "Install Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);

        RefreshPrinterStatus();
    }

    private void BtnUninstall_Click(object? sender, EventArgs e)
    {
        if (MessageBox.Show($"Remove '{_config.PrinterName}' from Windows?",
                "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            return;

        var result = WindowsPrinterManager.Uninstall(_config);

        if (result.Ok)
            MessageBox.Show(result.Message, "Printer Removed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show(result.Message, "Remove Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);

        RefreshPrinterStatus();
    }

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        using var dlg = new SettingsForm(_config);
        dlg.ShowDialog(this);
        UpdateServerStatus();
        RefreshPrinterStatus();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();   // minimize to tray rather than exit
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private void RefreshPrinterStatus()
    {
        var exists = WindowsPrinterManager.PrinterExists(_config.PrinterName);
        _lblPrinterStatus.Text = exists
            ? $"Printer: '{_config.PrinterName}' INSTALLED"
            : $"Printer: '{_config.PrinterName}' NOT INSTALLED";
        _lblPrinterStatus.ForeColor = exists
            ? Color.FromArgb(5, 150, 105)
            : Color.FromArgb(220, 38, 38);

        _btnUninstall.Enabled = exists;
    }

    private static string FormatBytes(int bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024):F1} MB"
    };
}
