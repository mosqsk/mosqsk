namespace VirtualPrinter.Core.Models;

public class PrinterConfiguration
{
    // Windows printer registration
    public string PrinterName { get; set; } = "Virtual ZPL Printer";
    public string PortName { get; set; } = "VirtualZPL_9100";

    // TCP listener
    public int ListenPort { get; set; } = 9100;
    public string ListenAddress { get; set; } = "0.0.0.0";

    // Job handling
    public bool SaveJobsToFile { get; set; } = true;
    public string JobOutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "VirtualPrinter", "Jobs");

    // ZPL rendering via Labelary API
    public bool EnableZplRendering { get; set; } = true;
    public string LabelaryBaseUrl { get; set; } = "http://api.labelary.com/v1/printers";
    public string LabelDensity { get; set; } = "8dpmm";   // 8dpmm = 203 dpi, 12dpmm = 300 dpi
    public string LabelWidth { get; set; } = "4";         // inches
    public string LabelHeight { get; set; } = "6";        // inches

    // UI behaviour
    public bool StartMinimized { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public int MaxJobHistory { get; set; } = 100;

    public string LabelaryUrl =>
        $"{LabelaryBaseUrl.TrimEnd('/')}/{LabelDensity}/labels/{LabelWidth}x{LabelHeight}/0/";
}
