namespace VirtualPrinter.App;

/// <summary>
/// Manages the system-tray icon, tooltip and context menu.
/// </summary>
public sealed class TrayManager : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _startStopItem;
    private bool _running;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? StartStopRequested;

    public TrayManager()
    {
        _startStopItem = new ToolStripMenuItem("Stop Server", null, (_, _) => StartStopRequested?.Invoke(this, EventArgs.Empty));

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Virtual ZPL Printer", null, (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startStopItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _icon = new NotifyIcon
        {
            Text = "Virtual ZPL Printer",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };

        _icon.DoubleClick += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateStatus(bool running, int port)
    {
        _running = running;
        _icon.Text = running
            ? $"Virtual ZPL Printer — Listening on :{port}"
            : "Virtual ZPL Printer — Stopped";

        _startStopItem.Text = running ? "Stop Server" : "Start Server";
    }

    public void FlashNotification(string text)
    {
        _icon.BalloonTipTitle = "Virtual ZPL Printer";
        _icon.BalloonTipText = text;
        _icon.BalloonTipIcon = ToolTipIcon.Info;
        _icon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
