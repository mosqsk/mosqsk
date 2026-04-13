using Microsoft.Extensions.Configuration;
using VirtualPrinter.App;
using VirtualPrinter.Core.Models;

// Prevent multiple instances
using var mutex = new Mutex(true, "VirtualZPLPrinter_SingleInstance", out var isNewInstance);

if (!isNewInstance)
{
    MessageBox.Show(
        "Virtual ZPL Printer is already running.\nCheck the system tray.",
        "Already Running",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);
    return;
}

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

// Load configuration
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .Build()
    .GetSection("PrinterConfiguration")
    .Get<PrinterConfiguration>() ?? new PrinterConfiguration();

Application.Run(new VirtualPrinterAppContext(config));
