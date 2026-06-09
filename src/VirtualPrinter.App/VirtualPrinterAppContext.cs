using VirtualPrinter.App.Forms;
using VirtualPrinter.Core;
using VirtualPrinter.Core.Models;
using VirtualPrinter.Core.Services;

namespace VirtualPrinter.App;

/// <summary>
/// Custom ApplicationContext that wires together the server, job processor,
/// tray icon and main window without requiring a visible form at startup.
/// </summary>
public sealed class VirtualPrinterAppContext : ApplicationContext
{
    private readonly PrinterConfiguration _config;
    private readonly PrinterServer _server;
    private readonly ZplProcessor _processor;
    private readonly TrayManager _tray;
    private MainForm? _mainForm;

    public VirtualPrinterAppContext(PrinterConfiguration config)
    {
        _config = config;
        _server = new PrinterServer(config);
        _processor = new ZplProcessor(config);

        _server.JobReceived += OnJobReceived;
        _server.LogMessage += OnServerLog;
        _server.ErrorOccurred += OnServerError;
        _processor.LogMessage += OnProcessorLog;

        _tray = new TrayManager();
        _tray.ShowWindowRequested += OnShowWindowRequested;
        _tray.ExitRequested += OnExitRequested;
        _tray.StartStopRequested += OnStartStopRequested;

        ShowMainWindow();

        // Auto-start the listener
        _ = _server.StartAsync();
        _tray.UpdateStatus(running: true, port: config.ListenPort);
    }

    // -------------------------------------------------------------------------
    // Window management
    // -------------------------------------------------------------------------

    private void ShowMainWindow()
    {
        if (_mainForm is { IsDisposed: false })
        {
            _mainForm.Show();
            _mainForm.BringToFront();
            return;
        }

        _mainForm = new MainForm(_config, _server, _processor);
        _mainForm.FormClosed += (_, _) => _mainForm = null;

        if (_config.StartMinimized)
            _mainForm.WindowState = FormWindowState.Minimized;

        _mainForm.Show();
    }

    // -------------------------------------------------------------------------
    // Server event handlers
    // -------------------------------------------------------------------------

    private async void OnJobReceived(object? sender, PrintJob job)
    {
        _mainForm?.AppendJob(job);
        await _processor.ProcessAsync(job);
        _mainForm?.RefreshJob(job);
        _tray.FlashNotification($"Job received from {job.ClientAddress} ({job.SizeBytes:N0} B)");
    }

    private void OnServerLog(object? sender, string msg) =>
        _mainForm?.AppendLog(msg);

    private void OnServerError(object? sender, Exception ex) =>
        _mainForm?.AppendLog($"ERROR: {ex.Message}");

    private void OnProcessorLog(object? sender, string msg) =>
        _mainForm?.AppendLog(msg);

    // -------------------------------------------------------------------------
    // Tray event handlers
    // -------------------------------------------------------------------------

    private void OnShowWindowRequested(object? sender, EventArgs e) =>
        ShowMainWindow();

    private void OnStartStopRequested(object? sender, EventArgs e)
    {
        if (_server.IsRunning)
        {
            _server.Stop();
            _tray.UpdateStatus(running: false, port: _config.ListenPort);
        }
        else
        {
            _ = _server.StartAsync();
            _tray.UpdateStatus(running: true, port: _config.ListenPort);
        }

        _mainForm?.UpdateServerStatus();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _server.Stop();
        _tray.Dispose();
        _mainForm?.Close();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _server.Dispose();
            _processor.Dispose();
            _tray.Dispose();
        }

        base.Dispose(disposing);
    }
}
