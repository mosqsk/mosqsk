using System.Management;
using VirtualPrinter.Core.Models;

namespace VirtualPrinter.Core.Services;

/// <summary>
/// Installs and removes a real Windows TCP/IP printer using WMI.
/// The printer points at 127.0.0.1 on the configured port so the OS
/// (and the D365 Document Routing Agent) sees it as a normal Windows printer.
/// Requires administrator privileges.
/// </summary>
public static class WindowsPrinterManager
{
    private const string GenericDriver = "Generic / Text Only";

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public static PrinterInstallResult Install(PrinterConfiguration config)
    {
        try
        {
            EnsurePortExists(config.PortName, "127.0.0.1", config.ListenPort);
            EnsurePrinterExists(config.PrinterName, config.PortName);
            return PrinterInstallResult.Success($"Printer '{config.PrinterName}' installed successfully.");
        }
        catch (Exception ex)
        {
            return PrinterInstallResult.Failure(
                $"Failed to install printer: {ex.Message}\n\nMake sure the application is running as Administrator.");
        }
    }

    public static PrinterInstallResult Uninstall(PrinterConfiguration config)
    {
        try
        {
            DeletePrinter(config.PrinterName);
            DeletePort(config.PortName);
            return PrinterInstallResult.Success($"Printer '{config.PrinterName}' removed.");
        }
        catch (Exception ex)
        {
            return PrinterInstallResult.Failure($"Failed to remove printer: {ex.Message}");
        }
    }

    public static bool PrinterExists(string printerName)
    {
        using var searcher = new ManagementObjectSearcher(
            $"SELECT Name FROM Win32_Printer WHERE Name = '{Escape(printerName)}'");
        return searcher.Get().Count > 0;
    }

    // -------------------------------------------------------------------------
    // Port management
    // -------------------------------------------------------------------------

    private static void EnsurePortExists(string portName, string hostAddress, int portNumber)
    {
        if (PortExists(portName)) return;

        using var cls = new ManagementClass("Win32_TCPIPPrinterPort");
        using var port = cls.CreateInstance()
            ?? throw new InvalidOperationException("Could not create Win32_TCPIPPrinterPort instance.");

        port["Name"] = portName;
        port["HostAddress"] = hostAddress;
        port["PortNumber"] = portNumber;
        port["Protocol"] = 1;         // 1 = RAW, 2 = LPR
        port["SNMPEnabled"] = false;
        port.Put();
    }

    private static bool PortExists(string portName)
    {
        using var searcher = new ManagementObjectSearcher(
            $"SELECT Name FROM Win32_TCPIPPrinterPort WHERE Name = '{Escape(portName)}'");
        return searcher.Get().Count > 0;
    }

    private static void DeletePort(string portName)
    {
        using var searcher = new ManagementObjectSearcher(
            $"SELECT * FROM Win32_TCPIPPrinterPort WHERE Name = '{Escape(portName)}'");

        foreach (ManagementObject obj in searcher.Get())
            obj.Delete();
    }

    // -------------------------------------------------------------------------
    // Printer management
    // -------------------------------------------------------------------------

    private static void EnsurePrinterExists(string printerName, string portName)
    {
        if (PrinterExists(printerName)) return;

        using var cls = new ManagementClass("Win32_Printer");
        using var printer = cls.CreateInstance()
            ?? throw new InvalidOperationException("Could not create Win32_Printer instance.");

        printer["Name"] = printerName;
        printer["DriverName"] = GenericDriver;
        printer["PortName"] = portName;
        printer["Shared"] = false;
        printer["RawOnly"] = true;
        printer["Comment"] = "Virtual ZPL Printer — D365 FnO Document Routing Agent";
        printer.Put();
    }

    private static void DeletePrinter(string printerName)
    {
        using var searcher = new ManagementObjectSearcher(
            $"SELECT * FROM Win32_Printer WHERE Name = '{Escape(printerName)}'");

        foreach (ManagementObject obj in searcher.Get())
            obj.Delete();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string Escape(string value) => value.Replace("'", "\\'");
}

public record PrinterInstallResult(bool Ok, string Message)
{
    public static PrinterInstallResult Success(string message) => new(true, message);
    public static PrinterInstallResult Failure(string message) => new(false, message);
}
