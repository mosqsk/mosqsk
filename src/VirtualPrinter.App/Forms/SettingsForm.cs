using VirtualPrinter.Core.Models;

namespace VirtualPrinter.App.Forms;

/// <summary>
/// Modal settings dialog.  Changes take effect on the in-memory config object;
/// persistent save writes appsettings.json.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly PrinterConfiguration _config;

    // Server
    private NumericUpDown _nudPort = null!;
    private TextBox _tbListenAddress = null!;

    // Printer
    private TextBox _tbPrinterName = null!;
    private TextBox _tbPortName = null!;

    // Jobs
    private CheckBox _chkSaveJobs = null!;
    private TextBox _tbOutputDir = null!;
    private Button _btnBrowse = null!;

    // Rendering
    private CheckBox _chkRender = null!;
    private TextBox _tbLabelaryUrl = null!;
    private TextBox _tbDensity = null!;
    private TextBox _tbLabelWidth = null!;
    private TextBox _tbLabelHeight = null!;

    // Startup
    private CheckBox _chkStartMinimized = null!;
    private NumericUpDown _nudMaxHistory = null!;

    public SettingsForm(PrinterConfiguration config)
    {
        _config = config;
        InitializeComponent();
        PopulateFields();
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        Text = "Settings";
        Size = new Size(520, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9F);

        int y = 12;
        const int labelW = 160;
        const int inputX = 175;
        const int inputW = 300;
        const int rowH = 32;

        void AddSection(string title)
        {
            var lbl = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(37, 99, 235),
                Location = new Point(12, y),
                AutoSize = true
            };
            Controls.Add(lbl);
            y += rowH;
        }

        Control AddRow(string label, Control input)
        {
            Controls.Add(new Label { Text = label, Location = new Point(12, y + 4), Width = labelW, AutoSize = false });
            input.Location = new Point(inputX, y);
            input.Width = inputW;
            Controls.Add(input);
            y += rowH;
            return input;
        }

        // ---- TCP Server ----
        AddSection("TCP Server");
        _nudPort = (NumericUpDown)AddRow("Listen Port:", new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 9100 });
        _tbListenAddress = (TextBox)AddRow("Listen Address:", new TextBox());

        // ---- Windows Printer ----
        y += 4;
        AddSection("Windows Printer");
        _tbPrinterName = (TextBox)AddRow("Printer Name:", new TextBox());
        _tbPortName = (TextBox)AddRow("Port Name:", new TextBox());

        // ---- Job Storage ----
        y += 4;
        AddSection("Job Storage");
        _chkSaveJobs = new CheckBox { Text = "Save received jobs to disk", Location = new Point(inputX, y), AutoSize = true };
        Controls.Add(_chkSaveJobs);
        y += rowH;

        _tbOutputDir = (TextBox)AddRow("Output Directory:", new TextBox());
        _btnBrowse = new Button { Text = "...", Width = 30, Height = 23, Location = new Point(inputX + inputW + 4, y - rowH) };
        _btnBrowse.Click += BtnBrowse_Click;
        Controls.Add(_btnBrowse);

        // ---- ZPL Rendering ----
        y += 4;
        AddSection("ZPL Preview (Labelary)");
        _chkRender = new CheckBox { Text = "Render PNG preview via Labelary API", Location = new Point(inputX, y), AutoSize = true };
        Controls.Add(_chkRender);
        y += rowH;

        _tbLabelaryUrl = (TextBox)AddRow("Labelary Base URL:", new TextBox());
        _tbDensity = (TextBox)AddRow("Density:", new TextBox { Width = 80 });
        _tbLabelWidth = (TextBox)AddRow("Label Width (in):", new TextBox { Width = 60 });
        _tbLabelHeight = (TextBox)AddRow("Label Height (in):", new TextBox { Width = 60 });

        // ---- Misc ----
        y += 4;
        AddSection("Behaviour");
        _chkStartMinimized = new CheckBox { Text = "Start minimized to tray", Location = new Point(inputX, y), AutoSize = true };
        Controls.Add(_chkStartMinimized);
        y += rowH;

        _nudMaxHistory = (NumericUpDown)AddRow("Max Job History:", new NumericUpDown { Minimum = 10, Maximum = 1000, Value = 100 });

        // ---- Buttons ----
        y += 8;
        var btnOk = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(inputX + inputW - 160, y),
            Width = 75
        };
        btnOk.Click += BtnOk_Click;

        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(inputX + inputW - 80, y),
            Width = 75
        };

        Controls.AddRange(new Control[] { btnOk, btnCancel });
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        ClientSize = new Size(520, y + 50);
        ResumeLayout(false);
    }

    private void PopulateFields()
    {
        _nudPort.Value = _config.ListenPort;
        _tbListenAddress.Text = _config.ListenAddress;
        _tbPrinterName.Text = _config.PrinterName;
        _tbPortName.Text = _config.PortName;
        _chkSaveJobs.Checked = _config.SaveJobsToFile;
        _tbOutputDir.Text = _config.JobOutputDirectory;
        _chkRender.Checked = _config.EnableZplRendering;
        _tbLabelaryUrl.Text = _config.LabelaryBaseUrl;
        _tbDensity.Text = _config.LabelDensity;
        _tbLabelWidth.Text = _config.LabelWidth;
        _tbLabelHeight.Text = _config.LabelHeight;
        _chkStartMinimized.Checked = _config.StartMinimized;
        _nudMaxHistory.Value = _config.MaxJobHistory;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { SelectedPath = _tbOutputDir.Text };
        if (dlg.ShowDialog() == DialogResult.OK)
            _tbOutputDir.Text = dlg.SelectedPath;
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        _config.ListenPort = (int)_nudPort.Value;
        _config.ListenAddress = _tbListenAddress.Text.Trim();
        _config.PrinterName = _tbPrinterName.Text.Trim();
        _config.PortName = _tbPortName.Text.Trim();
        _config.SaveJobsToFile = _chkSaveJobs.Checked;
        _config.JobOutputDirectory = _tbOutputDir.Text.Trim();
        _config.EnableZplRendering = _chkRender.Checked;
        _config.LabelaryBaseUrl = _tbLabelaryUrl.Text.Trim();
        _config.LabelDensity = _tbDensity.Text.Trim();
        _config.LabelWidth = _tbLabelWidth.Text.Trim();
        _config.LabelHeight = _tbLabelHeight.Text.Trim();
        _config.StartMinimized = _chkStartMinimized.Checked;
        _config.MaxJobHistory = (int)_nudMaxHistory.Value;

        // Persist to appsettings.json
        try
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var json = System.Text.Json.JsonSerializer.Serialize(
                new { PrinterConfiguration = _config },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
        }
        catch { /* non-fatal — settings held in memory */ }
    }
}
